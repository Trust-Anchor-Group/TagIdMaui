using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services;
using NeuroAccessMaui.Services.Localization;
using NeuroAccessMaui.Services.Tag;
using NeuroAccessMaui.Services.UI;
using NeuroAccessMaui.UI.Pages.Main.VerifyCode;
using Waher.Content;

namespace NeuroAccessMaui.UI.Pages.Registration.Views
{
	public partial class ValidateEmailViewModel : BaseRegistrationViewModel, ICodeVerification
	{
		public ValidateEmailViewModel()
			: base(RegistrationStep.ValidateEmail)
		{
		}

		/// <inheritdoc/>
		protected override async Task OnInitialize()
		{
			await base.OnInitialize();

			LocalizationManager.Current.PropertyChanged += this.LocalizationManagerEventHandler;

			if (App.Current is not null)
			{
				this.CountDownTimer = App.Current.Dispatcher.CreateTimer();
				this.CountDownTimer.Interval = TimeSpan.FromMilliseconds(1000);
				this.CountDownTimer.Tick += this.CountDownEventHandler;
			}
		}

		/// <inheritdoc/>
		protected override async Task OnDispose()
		{
			LocalizationManager.Current.PropertyChanged -= this.LocalizationManagerEventHandler;

			if (this.CountDownTimer is not null)
			{
				this.CountDownTimer.Stop();
				this.CountDownTimer.Tick -= this.CountDownEventHandler;
				this.CountDownTimer = null;
			}

			await base.OnDispose();
		}

		/// <inheritdoc />
		public override async Task DoAssignProperties()
		{
			await base.DoAssignProperties();

			if (!string.IsNullOrEmpty(ServiceRef.TagProfile.EMail))
			{
				this.EmailText = ServiceRef.TagProfile.EMail;
				this.SendCodeCommand.NotifyCanExecuteChanged();
			}
		}

		protected override void OnPropertyChanged(PropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);

			if (e.PropertyName == nameof(this.IsBusy))
				this.SendCodeCommand.NotifyCanExecuteChanged();
		}

		public void LocalizationManagerEventHandler(object? sender, PropertyChangedEventArgs e)
		{
			this.OnPropertyChanged(nameof(this.LocalizedSendCodeText));
			this.OnPropertyChanged(nameof(this.LocalizedValidationError));
		}

		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(SendCodeCommand))]
		[NotifyPropertyChangedFor(nameof(LocalizedValidationError))]
		bool emailIsValid;

		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(LocalizedSendCodeText))]
		[NotifyCanExecuteChangedFor(nameof(SendCodeCommand))]
		[NotifyCanExecuteChangedFor(nameof(ResendCodeCommand))]
		private int countDownSeconds;

		[ObservableProperty]
		private IDispatcherTimer? countDownTimer;

		public string LocalizedSendCodeText
		{
			get
			{
				if (this.CountDownSeconds > 0)
					return ServiceRef.Localizer[nameof(AppResources.SendCodeSeconds), this.CountDownSeconds];

				return ServiceRef.Localizer[nameof(AppResources.SendCode)];
			}
		}

		public string LocalizedValidationError
		{
			get
			{
				if (!this.EmailIsValid)
					return ServiceRef.Localizer[nameof(AppResources.EmailValidationFormat)];

				return string.Empty;
			}
		}

		/// <summary>
		/// Email
		/// </summary>
		[ObservableProperty]
		private string emailText = string.Empty;

		public bool CanSendCode => this.EmailIsValid && !this.IsBusy &&
			(this.EmailText.Length > 0) && (this.CountDownSeconds <= 0);

		public bool CanResendCode => this.CountDownSeconds <= 0;

		[RelayCommand(CanExecute = nameof(CanSendCode))]
		private async Task SendCode()
		{
			this.IsBusy = true;

			try
			{
				if (!ServiceRef.NetworkService.IsOnline)
				{
					await ServiceRef.UiService.DisplayAlert(
						ServiceRef.Localizer[nameof(AppResources.ErrorTitle)],
						ServiceRef.Localizer[nameof(AppResources.NetworkSeemsToBeMissing)]);
					return;
				}

				ContentResponse SendResult = await InternetContent.PostAsync(
					new Uri("https://" + Constants.Domains.IdDomain + "/ID/SendVerificationMessage.ws"),
					new Dictionary<string, object>()
					{
						{ "EMail", this.EmailText },
						{ "AppName", Constants.Application.Name },
						{ "Language", CultureInfo.CurrentCulture.TwoLetterISOLanguageName }
					}, new KeyValuePair<string, string>("Accept", "application/json"));

				SendResult.AssertOk();

				if (SendResult.Decoded is Dictionary<string, object> SendResponse &&
					SendResponse.TryGetValue("Status", out object? Obj) &&
					Obj is bool SendStatus &&
					SendStatus)
				{
					this.StartTimer();

					VerifyCodeNavigationArgs NavigationArgs = new(this, this.EmailText);
					await ServiceRef.UiService.GoToAsync(nameof(VerifyCodePage), NavigationArgs, BackMethod.Pop);
					string? Code = await NavigationArgs.VarifyCode!.Task;

					if (!string.IsNullOrEmpty(Code))
					{
						ContentResponse VerifyResult = await InternetContent.PostAsync(
							new Uri("https://" + Constants.Domains.IdDomain + "/ID/VerifyNumber.ws"),
							new Dictionary<string, object>()
							{
								{ "EMail", this.EmailText },
								{ "Code", int.Parse(Code, NumberStyles.None, CultureInfo.InvariantCulture) }
							}, new KeyValuePair<string, string>("Accept", "application/json"));

						VerifyResult.AssertOk();

						if (VerifyResult.Decoded is Dictionary<string, object> VerifyResponse &&
							VerifyResponse.TryGetValue("Status", out Obj) &&
							Obj is bool VerifyStatus &&
							VerifyStatus)
						{
							ServiceRef.TagProfile.EMail = this.EmailText;

							if(string.IsNullOrEmpty(ServiceRef.TagProfile.Account))
								GoToRegistrationStep(RegistrationStep.NameEntry);
							else
								GoToRegistrationStep(RegistrationStep.CreateAccount);
						}
						else
						{
							await ServiceRef.UiService.DisplayAlert(
								ServiceRef.Localizer[nameof(AppResources.ErrorTitle)],
								ServiceRef.Localizer[nameof(AppResources.UnableToVerifyCode)],
								ServiceRef.Localizer[nameof(AppResources.Ok)]);
						}
					}
				}
				else
				{
					await ServiceRef.UiService.DisplayAlert(
						ServiceRef.Localizer[nameof(AppResources.ErrorTitle)],
						ServiceRef.Localizer[nameof(AppResources.SomethingWentWrongWhenSendingPhoneCode)]);
				}
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);

				await ServiceRef.UiService.DisplayAlert(
					ServiceRef.Localizer[nameof(AppResources.ErrorTitle)], ex.Message,
					ServiceRef.Localizer[nameof(AppResources.Ok)]);
			}
			finally
			{
				this.IsBusy = false;
			}
		}

		[RelayCommand(CanExecute = nameof(CanResendCode))]
		private async Task ResendCode()
		{
			try
			{
				if (!ServiceRef.NetworkService.IsOnline)
				{
					await ServiceRef.UiService.DisplayAlert(
						ServiceRef.Localizer[nameof(AppResources.ErrorTitle)],
						ServiceRef.Localizer[nameof(AppResources.NetworkSeemsToBeMissing)]);
					return;
				}

				ContentResponse SendResult = await InternetContent.PostAsync(
					new Uri("https://" + Constants.Domains.IdDomain + "/ID/SendVerificationMessage.ws"),
					new Dictionary<string, object>()
					{
						{ "EMail", this.EmailText },
						{ "AppName", Constants.Application.Name },
						{ "Language", CultureInfo.CurrentCulture.TwoLetterISOLanguageName }
					}, new KeyValuePair<string, string>("Accept", "application/json"));

				SendResult.AssertOk();

				if (SendResult.Decoded is Dictionary<string, object> SendResponse &&
					SendResponse.TryGetValue("Status", out object? Obj) && Obj is bool SendStatus && SendStatus)
				{
					this.StartTimer();
				}
				else
				{
					await ServiceRef.UiService.DisplayAlert(
						ServiceRef.Localizer[nameof(AppResources.ErrorTitle)],
						ServiceRef.Localizer[nameof(AppResources.SomethingWentWrongWhenSendingPhoneCode)]);
				}
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);

				await ServiceRef.UiService.DisplayAlert(
					ServiceRef.Localizer[nameof(AppResources.ErrorTitle)], ex.Message,
					ServiceRef.Localizer[nameof(AppResources.Ok)]);
			}
		}

		private void StartTimer()
		{
			if (this.CountDownTimer is not null)
			{
				this.CountDownSeconds = 30;

				if (!this.CountDownTimer.IsRunning)
					this.CountDownTimer.Start();
			}
		}

		private void CountDownEventHandler(object? sender, EventArgs e)
		{
			if (this.CountDownTimer is not null)
			{
				if (this.CountDownSeconds > 0)
					this.CountDownSeconds--;
				else
					this.CountDownTimer.Stop();
			}
		}
	}
}
