using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services;
using NeuroAccessMaui.Services.Tag;
using NeuroAccessMaui.UI.Pages.Registration.Views;
using Waher.Networking.XMPP;

namespace NeuroAccessMaui.UI.Pages.Main.ChangePassword
{
	/// <summary>
	/// View model for the <see cref="ChangePasswordPage"/> page.
	/// </summary>
	public partial class ChangePasswordViewModel : XmppViewModel
	{
		/// <summary>
		/// View model for the <see cref="ChangePasswordPage"/> page.
		/// </summary>
		public ChangePasswordViewModel()
			: base()
		{
		}

		/// <summary>
		/// Old password text entry
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(ChangePasswordCommand))]
		private string oldPassword = string.Empty;

		/// <summary>
		/// New password text entry
		/// </summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(IsPassword1NotValid))]
		[NotifyPropertyChangedFor(nameof(IsPassword2NotValid))]
		[NotifyPropertyChangedFor(nameof(LocalizedValidationError))]
		[NotifyCanExecuteChangedFor(nameof(ChangePasswordCommand))]
		private string newPassword = string.Empty;

		/// <summary>
		/// New password text entry, retyped
		/// </summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(IsPassword2NotValid))]
		[NotifyPropertyChangedFor(nameof(NewPasswordsMatch))]
		[NotifyCanExecuteChangedFor(nameof(ChangePasswordCommand))]
		private string newPassword2 = string.Empty;

		/// <summary>
		/// If new password is OK
		/// </summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(NewPasswordsMatch))]
		[NotifyCanExecuteChangedFor(nameof(ChangePasswordCommand))]
		private bool incorrectPasswordAlertShown = false;

		/// <summary>
		/// If new password can be used.
		/// </summary>
		public bool CanChangePassword => this.NewPasswordStrength == PasswordStrength.Strong && this.NewPasswordMatchesRetypedNewPassword && this.IsConnected && !this.IsBusy;

		/// <summary>
		/// Strength of new password
		/// </summary>
		public PasswordStrength NewPasswordStrength => ServiceRef.TagProfile.ValidatePasswordStrength(this.NewPassword);

		/// <summary>
		/// Gets the value indicating whether the entered <see cref="NewPassword"/> is the same as the entered <see cref="NewPassword2"/>.
		/// </summary>
		public bool NewPasswordsMatch => string.IsNullOrEmpty(this.NewPassword) ? string.IsNullOrEmpty(this.NewPassword2) : string.Equals(this.NewPassword, this.NewPassword2, StringComparison.Ordinal);

		/// <summary>
		/// If First New Password entry is not valid.
		/// </summary>
		public bool IsPassword1NotValid => !string.IsNullOrEmpty(this.NewPassword) && this.NewPasswordStrength != PasswordStrength.Strong;

		/// <summary>
		/// If Second New password entry is not valid.
		/// </summary>
		public bool IsPassword2NotValid => !string.IsNullOrEmpty(this.NewPassword2) && !this.NewPasswordsMatch;

		/// <summary>
		/// If both new passwords match.
		/// </summary>
		public bool NewPasswordMatchesRetypedNewPassword => string.IsNullOrEmpty(this.NewPassword) ? string.IsNullOrEmpty(this.NewPassword2) : string.Equals(this.NewPassword, this.NewPassword2, StringComparison.Ordinal);

		/// <summary>
		/// Localized validation error message.
		/// </summary>
		public string LocalizedValidationError => DefinePasswordViewModel.GetLocalizedValidationError(this.NewPasswordStrength);

		protected override async Task OnInitialize()
		{
			await base.OnInitialize();
			this.NotifyCommandsCanExecuteChanged();
		}

		/// <inheritdoc/>
		protected override Task XmppService_ConnectionStateChanged(object? Sender, XmppState NewState)
		{
			return MainThread.InvokeOnMainThreadAsync(async () =>
			{
				await base.XmppService_ConnectionStateChanged(Sender, NewState);

				this.NotifyCommandsCanExecuteChanged();
			});
		}

		/// <inheritdoc/>
		public override void SetIsBusy(bool IsBusy)
		{
			base.SetIsBusy(IsBusy);
			this.NotifyCommandsCanExecuteChanged();
		}

		private void NotifyCommandsCanExecuteChanged()
		{
			this.ChangePasswordCommand.NotifyCanExecuteChanged();
		}

		/// <summary>
		/// Tries to change the password
		/// </summary>
		[RelayCommand(CanExecute = nameof(CanChangePassword))]
		private async Task ChangePassword()
		{
			if (!string.IsNullOrEmpty(this.OldPassword) && this.CanChangePassword)
			{
				if (await App.CheckPasswordAndUnblockUserAsync(this.OldPassword))
				{
					try
					{
						string NewPassword = ServiceRef.CryptoService.CreateRandomPassword();

						if (!await ServiceRef.XmppService.ChangePassword(NewPassword))
						{
							await ServiceRef.UiService.DisplayAlert(
								ServiceRef.Localizer[nameof(AppResources.ErrorTitle)],
								ServiceRef.Localizer[nameof(AppResources.UnableToChangePassword)]);
							return;
						}

						ServiceRef.TagProfile.LocalPassword = this.NewPassword;
						ServiceRef.TagProfile.SetAccount(ServiceRef.TagProfile.Account!, NewPassword, string.Empty);

						await ServiceRef.UiService.DisplayAlert(
							ServiceRef.Localizer[nameof(AppResources.SuccessTitle)],
							ServiceRef.Localizer[nameof(AppResources.PasswordChanged)]);

						await this.GoBack();
					}
					catch (Exception ex)
					{
						ServiceRef.LogService.LogException(ex);
						await ServiceRef.UiService.DisplayException(ex);
					}
				}
				else
				{
					this.OldPassword = string.Empty;

					long PasswordAttemptCounter = await App.GetCurrentPasswordCounterAsync();
					long RemainingAttempts = Math.Max(0, Constants.Password.FirstMaxPasswordAttempts - PasswordAttemptCounter);

					await ServiceRef.UiService.DisplayAlert(
						ServiceRef.Localizer[nameof(AppResources.ErrorTitle)],
						ServiceRef.Localizer[nameof(AppResources.PasswordIsInvalid), RemainingAttempts]);

					await App.CheckUserBlockingAsync();
				}
			}
		}
	}
}
