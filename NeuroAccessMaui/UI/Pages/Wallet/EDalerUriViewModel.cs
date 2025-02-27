using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EDaler;
using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services;
using NeuroAccessMaui.Services.UI;
using NeuroAccessMaui.UI.Converters;
using NeuroAccessMaui.UI.Pages.Main.Calculator;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using Waher.Content;
using Waher.Networking.XMPP.Contracts;
using Waher.Networking.XMPP.StanzaErrors;

namespace NeuroAccessMaui.UI.Pages.Wallet
{
	/// <summary>
	/// The view model to bind to for when displaying the contents of an eDaler URI.
	/// </summary>
	public partial class EDalerUriViewModel : QrXmppViewModel
	{
		private readonly EDalerUriNavigationArgs? navigationArguments;
		private readonly IShareQrCode? shareQrCode;
		private readonly TaskCompletionSource<string?>? uriToSend = null;

		/// <summary>
		/// The view model to bind to for when displaying the contents of an eDaler URI.
		/// </summary>
		/// <param name="ShareQrCode">Interface for pages with a share button.</param>
		/// <param name="Args">Navigation arguments</param>
		public EDalerUriViewModel(IShareQrCode? ShareQrCode, EDalerUriNavigationArgs? Args)
			: base()
		{
			this.navigationArguments = Args;
			this.shareQrCode = ShareQrCode;

			this.uriToSend = Args?.UriToSend;
			this.FriendlyName = Args?.FriendlyName;

			if (Args?.Uri is not null)
			{
				this.Uri = Args.Uri.UriString;
				this.Id = Args.Uri.Id;
				this.Amount = Args.Uri.Amount;
				this.AmountExtra = Args.Uri.AmountExtra;
				this.Currency = Args.Uri.Currency;
				this.Created = Args.Uri.Created;
				this.Expires = Args.Uri.Expires;
				this.ExpiresStr = this.Expires.ToShortDateString();
				this.From = Args.Uri.From;
				this.FromType = Args.Uri.FromType;
				this.To = Args.Uri.To;
				this.ToType = Args.Uri.ToType;
				this.ToPreset = !string.IsNullOrEmpty(Args.Uri.To);
				this.Complete = Args.Uri.Complete;
			}

			this.NotPaid = true;

			this.AmountText = !this.Amount.HasValue || this.Amount.Value <= 0 ? string.Empty : MoneyToString.ToString(this.Amount.Value);
			this.AmountOk = CommonTypes.TryParse(this.AmountText, out decimal d) && d > 0;
			this.AmountPreset = !string.IsNullOrEmpty(this.AmountText) && this.AmountOk;
			this.AmountAndCurrency = this.AmountText + " " + this.Currency;

			this.AmountExtraText = this.AmountExtra.HasValue ? MoneyToString.ToString(this.AmountExtra.Value) : string.Empty;
			this.AmountExtraOk = !this.AmountExtra.HasValue || this.AmountExtra.Value >= 0;
			this.AmountExtraPreset = this.AmountExtra.HasValue;
			this.AmountExtraAndCurrency = this.AmountExtraText + " " + this.Currency;

			StringBuilder Url = new();

			Url.Append("https://");
			Url.Append(this.From);
			Url.Append("/Images/eDalerFront200.png");

			this.EDalerFrontGlyph = Url.ToString();

			Url.Clear();
			Url.Append("https://");
			Url.Append(ServiceRef.TagProfile.Domain);
			Url.Append("/Images/eDalerBack200.png");
			this.EDalerBackGlyph = Url.ToString();
		}

		/// <inheritdoc/>
		protected override async Task OnInitialize()
		{
			await base.OnInitialize();

			if (this.navigationArguments is not null)
			{
				if (this.navigationArguments.Uri?.EncryptedMessage is not null)
				{
					if (this.navigationArguments.Uri.EncryptionPublicKey is null)
						this.Message = Encoding.UTF8.GetString(this.navigationArguments.Uri.EncryptedMessage);
					else
					{
						this.Message = await ServiceRef.XmppService.TryDecryptMessage(this.navigationArguments.Uri.EncryptedMessage,
						this.navigationArguments.Uri.EncryptionPublicKey, this.navigationArguments.Uri.Id, this.navigationArguments.Uri.From);
					}
					this.HasMessage = !string.IsNullOrEmpty(this.Message);
				}

				this.MessagePreset = !string.IsNullOrEmpty(this.Message);
				this.CanEncryptMessage = this.navigationArguments.Uri?.ToType == EntityType.LegalId;
				this.EncryptMessage = this.CanEncryptMessage;
			}
		}

		/// <inheritdoc/>
		protected override async Task OnDispose()
		{
			this.uriToSend?.TrySetResult(null);

			await base.OnDispose();
		}

		#region Properties

		/// <summary>
		/// edaler URI to process
		/// </summary>
		[ObservableProperty]
		private string? uri;

		/// <summary>
		/// Amount of eDaler to process
		/// </summary>
		[ObservableProperty]
		private decimal? amount;

		/// <summary>
		/// If <see cref="Amount"/> is OK.
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(PayOnlineCommand))]
		[NotifyCanExecuteChangedFor(nameof(GenerateQrCodeCommand))]
		[NotifyCanExecuteChangedFor(nameof(SendPaymentCommand))]
		private bool amountOk;

		protected override void OnPropertyChanged(PropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);

			switch (e.PropertyName)
			{
				case nameof(this.IsConnected):
					this.AcceptCommand.NotifyCanExecuteChanged();
					this.PayOnlineCommand.NotifyCanExecuteChanged();
					this.SubmitCommand.NotifyCanExecuteChanged();
					break;

				case nameof(this.HasQrCode):
					this.PayOnlineCommand.NotifyCanExecuteChanged();
					this.GenerateQrCodeCommand.NotifyCanExecuteChanged();
					this.ShareCommand.NotifyCanExecuteChanged();
					break;

				case nameof(this.AmountText):
					if (CommonTypes.TryParse(this.AmountText, out decimal d) && d > 0)
					{
						this.Amount = d;
						this.AmountOk = true;
					}
					else
						this.AmountOk = false;

					this.AmountAndCurrency = this.AmountText + " " + this.Currency;
					break;

				case nameof(this.AmountExtraText):
					if (string.IsNullOrEmpty(this.AmountExtraText))
					{
						this.AmountExtra = null;
						this.AmountExtraOk = true;
					}
					else if (CommonTypes.TryParse(this.AmountExtraText, out d) && d >= 0)
					{
						this.AmountExtra = d;
						this.AmountExtraOk = true;
					}
					else
						this.AmountExtraOk = false;

					this.AmountExtraAndCurrency = this.AmountExtraText + " " + this.Currency;
					break;
			}
		}

		/// <summary>
		/// <see cref="Amount"/> as text.
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(PayOnlineCommand))]
		[NotifyCanExecuteChangedFor(nameof(GenerateQrCodeCommand))]
		[NotifyCanExecuteChangedFor(nameof(SendPaymentCommand))]
		private string? amountText;

		/// <summary>
		/// <see cref="AmountText"/> and <see cref="Currency"/>.
		/// </summary>
		[ObservableProperty]
		private string? amountAndCurrency;

		/// <summary>
		/// If <see cref="Amount"/> is preset.
		/// </summary>
		[ObservableProperty]
		private bool amountPreset;

		/// <summary>
		/// AmountExtra of eDaler to process
		/// </summary>
		[ObservableProperty]
		private decimal? amountExtra;

		/// <summary>
		/// If <see cref="AmountExtra"/> is OK.
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(PayOnlineCommand))]
		[NotifyCanExecuteChangedFor(nameof(GenerateQrCodeCommand))]
		[NotifyCanExecuteChangedFor(nameof(SendPaymentCommand))]
		private bool amountExtraOk;

		/// <summary>
		/// <see cref="AmountExtra"/> as text.
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(PayOnlineCommand))]
		[NotifyCanExecuteChangedFor(nameof(GenerateQrCodeCommand))]
		[NotifyCanExecuteChangedFor(nameof(SendPaymentCommand))]
		private string? amountExtraText;

		/// <summary>
		/// <see cref="AmountExtraText"/> and <see cref="Currency"/>.
		/// </summary>
		[ObservableProperty]
		private string? amountExtraAndCurrency;

		/// <summary>
		/// If <see cref="AmountExtra"/> is preset.
		/// </summary>
		[ObservableProperty]
		private bool amountExtraPreset;

		/// <summary>
		/// Currency of eDaler to process
		/// </summary>
		[ObservableProperty]
		private string? currency;

		/// <summary>
		/// When code was created.
		/// </summary>
		[ObservableProperty]
		private DateTime created;

		/// <summary>
		/// When code expires
		/// </summary>
		[ObservableProperty]
		private DateTime expires;

		/// <summary>
		/// When code expires
		/// </summary>
		[ObservableProperty]
		private string? expiresStr;

		/// <summary>
		/// Globally unique identifier of code
		/// </summary>
		[ObservableProperty]
		private Guid id;

		/// <summary>
		/// From who eDaler is to be transferred
		/// </summary>
		[ObservableProperty]
		private string? from;

		/// <summary>
		/// Type of identity specified in <see cref="From"/>
		/// </summary>
		[ObservableProperty]
		private EntityType fromType;

		/// <summary>
		/// To whom eDaler is to be transferred
		/// </summary>
		[ObservableProperty]
		private string? to;

		/// <summary>
		/// If <see cref="To"/> is preset
		/// </summary>
		[ObservableProperty]
		private bool toPreset;

		/// <summary>
		/// Type of identity specified in <see cref="To"/>
		/// </summary>
		[ObservableProperty]
		private EntityType toType;

		/// <summary>
		/// Optional FriendlyName associated with URI
		/// </summary>
		[ObservableProperty]
		private string? friendlyName;

		/// <summary>
		/// If the URI is complete or not.
		/// </summary>
		[ObservableProperty]
		private bool complete;

		/// <summary>
		/// Message to recipient
		/// </summary>
		[ObservableProperty]
		private string? message;

		/// <summary>
		/// If <see cref="Message"/> should be encrypted in payment.
		/// </summary>
		[ObservableProperty]
		private bool encryptMessage;

		/// <summary>
		/// If <see cref="Message"/> should be encrypted in payment.
		/// </summary>
		[ObservableProperty]
		private bool canEncryptMessage;

		/// <summary>
		/// If a message is defined
		/// </summary>
		[ObservableProperty]
		private bool hasMessage;

		/// <summary>
		/// If <see cref="Message"/> is preset.
		/// </summary>
		[ObservableProperty]
		private bool messagePreset;

		/// <summary>
		/// If the URI is complete or not.
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(PayOnlineCommand))]
		[NotifyCanExecuteChangedFor(nameof(GenerateQrCodeCommand))]
		[NotifyCanExecuteChangedFor(nameof(SendPaymentCommand))]
		private bool notPaid;

		/// <summary>
		/// eDaler glyph URL
		/// </summary>
		[ObservableProperty]
		private string? eDalerFrontGlyph;

		/// <summary>
		/// eDaler glyph URL
		/// </summary>
		[ObservableProperty]
		private string? eDalerBackGlyph;

		#endregion

		/// <summary>
		/// Command to bind to for detecting when a from label has been clicked on.
		/// </summary>
		[RelayCommand]
		private async Task FromClick()
		{
			try
			{
				string? Value = this.From;
				if (Value is null)
					return;

				if ((Value.StartsWith("http://", StringComparison.CurrentCultureIgnoreCase) ||
					Value.StartsWith("https://", StringComparison.CurrentCultureIgnoreCase)) &&
					System.Uri.TryCreate(Value, UriKind.Absolute, out Uri? Uri) && await Launcher.TryOpenAsync(Uri))
				{
					return;
				}

				if (System.Uri.TryCreate("https://" + Value, UriKind.Absolute, out Uri) && await Launcher.TryOpenAsync(Uri))
					return;

				await Clipboard.SetTextAsync(Value);
				await ServiceRef.UiService.DisplayAlert(ServiceRef.Localizer[nameof(AppResources.SuccessTitle)],
					ServiceRef.Localizer[nameof(AppResources.TagValueCopiedToClipboard)]);
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				await ServiceRef.UiService.DisplayException(ex);
			}
		}

		/// <summary>
		/// The command to bind to for accepting the URI
		/// </summary>
		[RelayCommand(CanExecute = nameof(IsConnected))]
		private async Task Accept()
		{
			try
			{
				if (this.Uri is null)
					return;

				if (!await App.AuthenticateUserAsync(AuthenticationPurpose.AcceptEDalerUri, true))
					return;

				(bool succeeded, Transaction? Transaction) = await ServiceRef.NetworkService.TryRequest(
					() => ServiceRef.XmppService.SendEDalerUri(this.Uri));

				if (succeeded)
				{
					await this.GoBack();
					await ServiceRef.UiService.DisplayAlert(ServiceRef.Localizer[nameof(AppResources.SuccessTitle)],
						ServiceRef.Localizer[nameof(AppResources.TransactionAccepted)]);
				}
				else
					await ServiceRef.UiService.DisplayAlert(ServiceRef.Localizer[nameof(AppResources.ErrorTitle)],
						ServiceRef.Localizer[nameof(AppResources.UnableToProcessEDalerUri)]);
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				await ServiceRef.UiService.DisplayException(ex);
			}
		}

		/// <summary>
		/// The command to bind to for paying online
		/// </summary>
		[RelayCommand(CanExecute = nameof(CanPayOnline))]
		private async Task PayOnline()
		{
			try
			{
				if (!this.NotPaid)
				{
					await ServiceRef.UiService.DisplayAlert(ServiceRef.Localizer[nameof(AppResources.ErrorTitle)],
						ServiceRef.Localizer[nameof(AppResources.PaymentAlreadySent)]);
					return;
				}

				if (!await App.AuthenticateUserAsync(AuthenticationPurpose.PayOnline, true))
					return;

				string Uri;

				if (this.EncryptMessage && this.ToType == EntityType.LegalId)
				{
					try
					{
						LegalIdentity LegalIdentity = await ServiceRef.XmppService.GetLegalIdentity(this.To);
						Uri = await ServiceRef.XmppService.CreateFullEDalerPaymentUri(LegalIdentity, this.Amount ?? 0, this.AmountExtra,
							this.Currency ?? string.Empty, 3, this.Message ?? string.Empty);
					}
					catch (ForbiddenException)
					{
						// This happens if you try to view someone else's legal identity.
						// When this happens, try to send a petition to view it instead.
						// Normal operation. Should not be logged.

						this.NotPaid = true;

						MainThread.BeginInvokeOnMainThread(async () =>
						{
							bool Succeeded = await ServiceRef.NetworkService.TryRequest(() => ServiceRef.XmppService.PetitionIdentity(
								this.To, Guid.NewGuid().ToString(), ServiceRef.Localizer[nameof(AppResources.EncryptedPayment)]));

							if (Succeeded)
							{
								await ServiceRef.UiService.DisplayAlert(ServiceRef.Localizer[nameof(AppResources.PetitionSent)],
									ServiceRef.Localizer[nameof(AppResources.APetitionHasBeenSentForEncryption)]);
							}
						});

						return;
					}
				}
				else
				{
					Uri = await ServiceRef.XmppService.CreateFullEDalerPaymentUri(this.To!, this.Amount ?? 0, this.AmountExtra,
						this.Currency!, 3, this.Message ?? string.Empty);
				}

				// TODO: Validate To is a Bare JID or proper Legal Identity
				// TODO: Offline options: Expiry days

				this.NotPaid = false;

				(bool succeeded, Transaction? Transaction) = await ServiceRef.NetworkService.TryRequest(
					() => ServiceRef.XmppService.SendEDalerUri(Uri));

				if (succeeded)
				{
					await this.GoBack();
					await ServiceRef.UiService.DisplayAlert(ServiceRef.Localizer[nameof(AppResources.SuccessTitle)],
						ServiceRef.Localizer[nameof(AppResources.PaymentSuccess)]);
				}
				else
				{
					this.NotPaid = true;
					await ServiceRef.UiService.DisplayAlert(ServiceRef.Localizer[nameof(AppResources.ErrorTitle)],
						ServiceRef.Localizer[nameof(AppResources.UnableToProcessEDalerUri)]);
				}
			}
			catch (Exception ex)
			{
				this.NotPaid = true;
				ServiceRef.LogService.LogException(ex);
				await ServiceRef.UiService.DisplayException(ex);
			}
		}

		/// <summary>
		/// The command to bind to for generating a QR code
		/// </summary>
		[RelayCommand(CanExecute = nameof(CanGenerateQrCode))]
		private async Task GenerateQrCode()
		{
			if (!this.NotPaid)
			{
				await ServiceRef.UiService.DisplayAlert(ServiceRef.Localizer[nameof(AppResources.ErrorTitle)],
					ServiceRef.Localizer[nameof(AppResources.PaymentAlreadySent)]);
				return;
			}

			if (!await App.AuthenticateUserAsync(AuthenticationPurpose.PayOffline, true))
				return;

			try
			{
				string Uri;

				if (this.EncryptMessage && this.ToType == EntityType.LegalId)
				{
					LegalIdentity LegalIdentity = await ServiceRef.XmppService.GetLegalIdentity(this.To);
					Uri = await ServiceRef.XmppService.CreateFullEDalerPaymentUri(LegalIdentity, this.Amount ?? 0, this.AmountExtra,
						this.Currency ?? string.Empty, 3, this.Message ?? string.Empty);
				}
				else
				{
					Uri = await ServiceRef.XmppService.CreateFullEDalerPaymentUri(this.To ?? string.Empty, this.Amount ?? 0, this.AmountExtra,
						this.Currency ?? string.Empty, 3, this.Message ?? string.Empty);
				}

				// TODO: Validate To is a Bare JID or proper Legal Identity
				// TODO: Offline options: Expiry days

				if (this.IsAppearing)
				{
					MainThread.BeginInvokeOnMainThread(async () =>
					{
						this.GenerateQrCode(Uri);

						if (this.shareQrCode is not null)
							await this.shareQrCode.ShowQrCode();
					});
				}
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				await ServiceRef.UiService.DisplayException(ex);
			}
		}

		private bool CanPayOnline => this.AmountOk && this.AmountExtraOk && !this.HasQrCode && this.IsConnected && this.NotPaid; // TODO: Add To field OK
		private bool CanGenerateQrCode => this.AmountOk && this.AmountExtraOk && !this.HasQrCode && this.NotPaid; // TODO: Add To field OK
		private bool CanShare => this.HasQrCode;

		/// <summary>
		/// The command to bind to for sharing the QR code
		/// </summary>
		[RelayCommand(CanExecute = nameof(CanShare))]
		private async Task Share()
		{
			if (this.QrCodeBin is null)
				return;

			try
			{
				string? Message = this.Message?? this.AmountAndCurrency;

				ServiceRef.PlatformSpecific.ShareImage(this.QrCodeBin,
					string.Format(CultureInfo.CurrentCulture, Message ?? string.Empty, this.Amount, this.Currency),
					ServiceRef.Localizer[nameof(AppResources.Share)], "RequestPayment.png");
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				await ServiceRef.UiService.DisplayException(ex);
			}
		}

		/// <summary>
		/// The command to bind to for resubmitting a payment.
		/// </summary>
		[RelayCommand(CanExecute = nameof(IsConnected))]
		private async Task Submit()
		{
			if (this.Uri is null)
				return;

			try
			{
				if (!await App.AuthenticateUserAsync(AuthenticationPurpose.SubmitEDalerUri))
					return;

				(bool succeeded, Transaction? Transaction) = await ServiceRef.NetworkService.TryRequest(() => ServiceRef.XmppService.SendEDalerUri(this.Uri));
				if (succeeded)
				{
					await this.GoBack();
					await ServiceRef.UiService.DisplayAlert(ServiceRef.Localizer[nameof(AppResources.SuccessTitle)],
						ServiceRef.Localizer[nameof(AppResources.PaymentSuccess)]);
				}
				else
					await ServiceRef.UiService.DisplayAlert(ServiceRef.Localizer[nameof(AppResources.ErrorTitle)],
						ServiceRef.Localizer[nameof(AppResources.UnableToProcessEDalerUri)]);
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				await ServiceRef.UiService.DisplayException(ex);
			}
		}

		/// <summary>
		/// The command to bind to for displaying eDaler URI as a QR Code
		/// </summary>
		[RelayCommand]
		private async Task ShowCode()
		{
			if (this.Uri is null)
				return;

			if (!await App.AuthenticateUserAsync(AuthenticationPurpose.ShowUriAsQr, true))
				return;

			try
			{
				if (this.IsAppearing)
				{
					MainThread.BeginInvokeOnMainThread(async () =>
					{
						this.GenerateQrCode(this.Uri);

						if (this.shareQrCode is not null)
							await this.shareQrCode.ShowQrCode();
					});
				}
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				await ServiceRef.UiService.DisplayException(ex);
			}
		}

		private bool CanSendPayment()
		{
			return this.uriToSend is not null && this.AmountOk && this.AmountExtraOk && this.NotPaid;
		}

		/// <summary>
		/// The command to bind to send the payment via the parent page.
		/// </summary>
		[RelayCommand(CanExecute = nameof(CanSendPayment))]
		private async Task SendPayment()
		{
			if (!this.NotPaid)
			{
				await ServiceRef.UiService.DisplayAlert(ServiceRef.Localizer[nameof(AppResources.ErrorTitle)],
					ServiceRef.Localizer[nameof(AppResources.PaymentAlreadySent)]);
				return;
			}

			if (!await App.AuthenticateUserAsync(AuthenticationPurpose.SendPayment, true))
				return;

			try
			{
				string Uri;

				if (this.EncryptMessage && this.ToType == EntityType.LegalId)
				{
					LegalIdentity LegalIdentity = await ServiceRef.XmppService.GetLegalIdentity(this.To);
					Uri = await ServiceRef.XmppService.CreateFullEDalerPaymentUri(LegalIdentity, this.Amount ?? 0, this.AmountExtra,
						this.Currency!, 3, this.Message ?? string.Empty);
				}
				else
				{
					Uri = await ServiceRef.XmppService.CreateFullEDalerPaymentUri(this.To!, this.Amount ?? 0, this.AmountExtra,
						this.Currency!, 3, this.Message ?? string.Empty);
				}

				// TODO: Validate To is a Bare JID or proper Legal Identity
				// TODO: Offline options: Expiry days

				this.uriToSend?.TrySetResult(Uri);
				await this.GoBack();
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				await ServiceRef.UiService.DisplayException(ex);
			}
		}

		/// <summary>
		/// Opens the calculator for calculating the value of a numerical property.
		/// </summary>
		/// <param name="Parameter">Property to calculate</param>
		[RelayCommand]
		public async Task OpenCalculator(object Parameter)
		{
			try
			{
				switch (Parameter?.ToString())
				{
					case "AmountText":
						CalculatorNavigationArgs AmountArgs = new(this, nameof(this.AmountText));

						await ServiceRef.UiService.GoToAsync(nameof(CalculatorPage), AmountArgs, BackMethod.Pop);
						break;

					case "AmountExtraText":
						CalculatorNavigationArgs ExtraArgs = new(this, nameof(this.AmountExtraText));

						await ServiceRef.UiService.GoToAsync(nameof(CalculatorPage), ExtraArgs, BackMethod.Pop);
						break;
				}
			}
			catch (Exception ex)
			{
				await ServiceRef.UiService.DisplayException(ex);
			}
		}

		#region ILinkableView

		/// <summary>
		/// Title of the current view
		/// </summary>
		public override Task<string> Title => Task.FromResult<string>(ServiceRef.Localizer[nameof(AppResources.Payment)]);

		#endregion

	}
}
