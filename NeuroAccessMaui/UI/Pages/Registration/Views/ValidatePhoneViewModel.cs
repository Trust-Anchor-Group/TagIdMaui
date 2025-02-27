using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mopups.Services;
using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services;
using NeuroAccessMaui.Services.Data;
using NeuroAccessMaui.Services.Localization;
using NeuroAccessMaui.Services.Tag;
using NeuroAccessMaui.Services.UI;
using NeuroAccessMaui.UI.Pages.Main.VerifyCode;
using NeuroAccessMaui.UI.Popups;
using Waher.Content;

namespace NeuroAccessMaui.UI.Pages.Registration.Views
{
	public partial class ValidatePhoneViewModel : BaseRegistrationViewModel, ICodeVerification
	{
		public ValidatePhoneViewModel()
			: base(RegistrationStep.ValidatePhone)
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

			if (string.IsNullOrEmpty(ServiceRef.TagProfile.PhoneNumber))
			{
				try
				{
					ContentResponse Result = await InternetContent.PostAsync(
						new Uri("https://" + Constants.Domains.IdDomain + "/ID/CountryCode.ws"), string.Empty,
						new KeyValuePair<string, string>("Accept", "application/json"));

					Result.AssertOk();

					if ((Result.Decoded is Dictionary<string, object> Response) &&
						Response.TryGetValue("CountryCode", out object? cc) &&
						cc is string CountryCode &&
						ISO_3166_1.TryGetCountryByCode(CountryCode, out ISO_3166_Country? Country))
					{
						this.SelectedCountry = Country;
					}
				}
				catch (Exception ex)
				{
					ServiceRef.LogService.LogException(ex);
				}
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

			string? PhoneNumber = ServiceRef.TagProfile.PhoneNumber;

			if (string.IsNullOrEmpty(PhoneNumber))
			{
				try
				{
					ContentResponse Result = await InternetContent.PostAsync(
						new Uri("https://" + Constants.Domains.IdDomain + "/ID/CountryCode.ws"), string.Empty,
						new KeyValuePair<string, string>("Accept", "application/json"));

					Result.AssertOk();

					if ((Result.Decoded is Dictionary<string, object> Response) &&
						Response.TryGetValue("CountryCode", out object? cc) &&
						cc is string CountryCode &&
						ISO_3166_1.TryGetCountryByCode(CountryCode, out ISO_3166_Country? Country))
					{
						this.SelectedCountry = Country;
					}
				}
				catch (Exception ex)
				{
					ServiceRef.LogService.LogException(ex);
				}
			}
			else
			{
				string SelectedCountry = ServiceRef.TagProfile.SelectedCountry!;

				if (ISO_3166_1.TryGetCountryByCode(SelectedCountry, out ISO_3166_Country? Country))
				{
					this.SelectedCountry = Country;
					this.PhoneNumber = PhoneNumber[(Country.DialCode.Length + 1)..];
					this.SendCodeCommand.NotifyCanExecuteChanged();
				}
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
		ISO_3166_Country selectedCountry = ISO_3166_1.DefaultCountry;

		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(SendCodeCommand))]
		bool numberIsValid;

		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(LocalizedValidationError))]
		bool typeIsValid;

		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(LocalizedValidationError))]
		bool lengthIsValid;

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
				if (!this.TypeIsValid)
					return ServiceRef.Localizer[nameof(AppResources.PhoneValidationDigits)];

				if (!this.LengthIsValid)
					return ServiceRef.Localizer[nameof(AppResources.PhoneValidationLength)];

				return string.Empty;
			}
		}

		/// <summary>
		/// Phone number
		/// </summary>
		[ObservableProperty]
		private string phoneNumber = string.Empty;

		public bool CanSendCode => this.NumberIsValid && !this.IsBusy &&
			(this.PhoneNumber.Length > 0) && (this.CountDownSeconds <= 0);

		public bool CanResendCode => this.CountDownSeconds <= 0;

		[RelayCommand]
		private async Task SelectPhoneCode()
		{
			SelectPhoneCodePopup Page = new();
			await MopupService.Instance.PushAsync(Page);

			ISO_3166_Country? Result = await Page.Result;

			if (Result is not null)
				this.SelectedCountry = Result;

			return;
		}

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
				string FullPhoneNumber = $"+{this.SelectedCountry.DialCode}{this.PhoneNumber}";

				if (this.SelectedCountry.DialCode == "46") //TODO: Make this more generic for other countries
					FullPhoneNumber = $"+{this.SelectedCountry.DialCode}{this.PhoneNumber.TrimStart('0')}";

				ContentResponse SendResult = await InternetContent.PostAsync(
					new Uri("https://" + Constants.Domains.IdDomain + "/ID/SendVerificationMessage.ws"),
					new Dictionary<string, object>()
					{
						{ "Nr", FullPhoneNumber },
						{ "AppName", Constants.Application.Name },
						{ "Language", CultureInfo.CurrentCulture.TwoLetterISOLanguageName }
					}, new KeyValuePair<string, string>("Accept", "application/json"));

				SendResult.AssertOk();

				if (SendResult.Decoded is Dictionary<string, object> SendResponse &&
					SendResponse.TryGetValue("Status", out object? Obj) && Obj is bool SendStatus && SendStatus &&
					SendResponse.TryGetValue("IsTemporary", out Obj) && Obj is bool SendIsTemporary)
				{
					if (!string.IsNullOrEmpty(ServiceRef.TagProfile.PhoneNumber) && (ServiceRef.TagProfile.TestOtpTimestamp is null) && SendIsTemporary)
					{
						await ServiceRef.UiService.DisplayAlert(
						ServiceRef.Localizer[nameof(AppResources.ErrorTitle)],
						ServiceRef.Localizer[nameof(AppResources.SwitchingToTestPhoneNumberNotAllowed)]);
					}
					else
					{
						this.StartTimer();

						VerifyCodeNavigationArgs NavigationArgs = new(this, FullPhoneNumber);
						await ServiceRef.UiService.GoToAsync(nameof(VerifyCodePage), NavigationArgs, BackMethod.Pop);
						string? Code = await NavigationArgs.VarifyCode!.Task;

						if (!string.IsNullOrEmpty(Code))
						{
							//!!! The old code for existing accounts. Should be implemented somehow else
							// bool IsTest = this.PurposeRequired ? this.IsEducationalPurpose || this.IsExperimentalPurpose : this.TagProfile.IsTest;
							// PurposeUse Purpose = this.PurposeRequired ? (PurposeUse)this.PurposeNr : this.TagProfile.Purpose;

							PurposeUse Purpose = ServiceRef.TagProfile.Purpose;
							bool IsTest = (Purpose == PurposeUse.Educational) || (Purpose == PurposeUse.Experimental);

							ContentResponse VerifyResult = await InternetContent.PostAsync(
								new Uri("https://" + Constants.Domains.IdDomain + "/ID/VerifyNumber.ws"),
								new Dictionary<string, object>()
								{
									{ "Nr", FullPhoneNumber },
									{ "Code", int.Parse(Code, NumberStyles.None, CultureInfo.InvariantCulture) },
									{ "Test", IsTest }
								}, new KeyValuePair<string, string>("Accept", "application/json"));

							VerifyResult.AssertOk();

							if (VerifyResult.Decoded is Dictionary<string, object> VerifyResponse &&
								VerifyResponse.TryGetValue("Status", out Obj) && Obj is bool VerifyStatus && VerifyStatus &&
								VerifyResponse.TryGetValue("Domain", out Obj) && Obj is string VerifyDomain &&
								VerifyResponse.TryGetValue("Key", out Obj) && Obj is string VerifyKey &&
								VerifyResponse.TryGetValue("Secret", out Obj) && Obj is string VerifySecret &&
								VerifyResponse.TryGetValue("Temporary", out Obj) && Obj is bool VerifyIsTemporary)
							{
								ServiceRef.TagProfile.SetPhone(this.SelectedCountry.Alpha2, FullPhoneNumber);
								ServiceRef.TagProfile.SetPurpose(IsTest, Purpose);
								ServiceRef.TagProfile.TestOtpTimestamp = VerifyIsTemporary ? DateTime.Now : null;

								//!!! The old code for existing accounts. Should be implemented somehow else
								//!!! if (this.IsRevalidating)


								if (string.IsNullOrEmpty(ServiceRef.TagProfile.Domain))
								{
									bool DefaultConnectivity;

									try
									{
										(string HostName, int PortNumber, bool IsIpAddress) = await ServiceRef.NetworkService.LookupXmppHostnameAndPort(VerifyDomain);
										DefaultConnectivity = HostName == VerifyDomain && PortNumber == Waher.Networking.XMPP.XmppCredentials.DefaultPort;
									}
									catch (Exception)
									{
										DefaultConnectivity = false;
									}

									ServiceRef.TagProfile.SetDomain(VerifyDomain, DefaultConnectivity, VerifyKey, VerifySecret);
								}

								if (VerifyIsTemporary)
									GoToRegistrationStep(RegistrationStep.NameEntry);
								else
									GoToRegistrationStep(RegistrationStep.ValidateEmail);
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

				string FullPhoneNumber = $"+{this.SelectedCountry.DialCode}{this.PhoneNumber}";

				if (this.SelectedCountry.DialCode == "46") //If swedish phone nr
					FullPhoneNumber = $"+{this.SelectedCountry.DialCode}{this.PhoneNumber.TrimStart('0')}";

				ContentResponse SendResult = await InternetContent.PostAsync(
					new Uri("https://" + Constants.Domains.IdDomain + "/ID/SendVerificationMessage.ws"),
					new Dictionary<string, object>()
					{
						{ "Nr", FullPhoneNumber },
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
				this.CountDownSeconds = 300;

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
