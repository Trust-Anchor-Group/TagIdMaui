using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mopups.Services;
using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services;
using NeuroAccessMaui.Services.Data;
using NeuroAccessMaui.Services.Localization;
using NeuroAccessMaui.Services.Tag;
using NeuroAccessMaui.UI.Popups;
using Waher.Content;
using System.Globalization;
using NeuroAccessMaui.Services.UI;
using NeuroAccessMaui.UI.Pages.Main.VerifyCode;
using Waher.Content.Xml;
using Waher.Networking.XMPP;
using Waher.Networking.XMPP.Contracts;
using ZXing;

namespace NeuroAccessMaui.UI.Pages.Registration.Views
{
	public partial class ContactSupportViewModel : BaseRegistrationViewModel, ICodeVerification
	{

		public ContactSupportViewModel() : base(RegistrationStep.ContactSupport)
		{
			this.SupportEmail = "neuro-access@trustanchorgroup.com";
		}

		[ObservableProperty]
		private string supportEmail;

		[RelayCommand]
		private async Task ContactSupport()
		{
			string email = this.SupportEmail;
			string subject = ServiceRef.Localizer[nameof(AppResources.SupportEmailSubject)];

			string mailtoUri = $"mailto:{email}?subject={Uri.EscapeDataString(subject)}";

			try
			{
				if (!await Launcher.OpenAsync(new Uri(mailtoUri)))
				{
					await ServiceRef.UiService.DisplayAlert(
						ServiceRef.Localizer[nameof(AppResources.ErrorTitle)],
						ServiceRef.Localizer[nameof(AppResources.EmailClientNotAvailable), this.SupportEmail],
						ServiceRef.Localizer[nameof(AppResources.Ok)]);
				}
			}
			catch (Exception)
			{
				await ServiceRef.UiService.DisplayAlert(
					 ServiceRef.Localizer[nameof(AppResources.ErrorTitle)],
					 ServiceRef.Localizer[nameof(AppResources.EmailClientNotAvailable), this.SupportEmail],
					 ServiceRef.Localizer[nameof(AppResources.Ok)]);
			}
		}
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

			try
			{
				ContentResponse Result = await InternetContent.PostAsync(
					new Uri("https://" + Constants.Domains.IdDomain + "/ID/CountryCode.ws"), string.Empty,
					new KeyValuePair<string, string>("Accept", "application/json"));

				if (Result.HasError)
				{
					ServiceRef.LogService.LogException(Result.Error);
					return;
				}

				if ((Result.Decoded is Dictionary<string, object> Response) &&
					 Response.TryGetValue("CountryCode", out object? cc) &&
					 (cc is string CountryCode))
				{
					if (ISO_3166_1.TryGetCountryByCode(CountryCode, out ISO_3166_Country? Country))
						this.SelectedCountry = Country;
				}
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
			}
		}

		public void LocalizationManagerEventHandler(object? sender, PropertyChangedEventArgs e)
		{
			this.OnPropertyChanged(nameof(this.LocalizedSendCodeText));
		}

		protected override void OnPropertyChanged(PropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);

			if (e.PropertyName == nameof(this.IsBusy))
				this.SendCommand.NotifyCanExecuteChanged();
		}


		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(CanSend))]
		[NotifyPropertyChangedFor(nameof(EmailValidationError))]
		[NotifyCanExecuteChangedFor(nameof(this.SendCommand))]
		private bool emailIsValid;

		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(CanSend))]
		[NotifyPropertyChangedFor(nameof(PhoneValidationError))]
		[NotifyCanExecuteChangedFor(nameof(this.SendCommand))]
		private bool phoneIsValid;

		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(CanSend))]
		[NotifyCanExecuteChangedFor(nameof(this.SendCommand))]
		private string emailText = string.Empty;

		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(CanSend))]
		[NotifyCanExecuteChangedFor(nameof(this.SendCommand))]
		private string phoneText = string.Empty;

		public string EmailValidationError => !this.EmailIsValid ? ServiceRef.Localizer[nameof(AppResources.EmailValidationFormat)] : string.Empty;

		public string PhoneValidationError => !this.PhoneIsValid ? ServiceRef.Localizer[nameof(AppResources.PhoneValidationDigits)] : string.Empty;

		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(LocalizedSendCodeText))]
		[NotifyCanExecuteChangedFor(nameof(SendCommand))]
		[NotifyCanExecuteChangedFor(nameof(ResendCodeCommand))]
		private int countDownSeconds;

		[ObservableProperty]
		private IDispatcherTimer? countDownTimer;

		public bool CanSend => this.EmailIsValid && this.PhoneIsValid &&
									!this.IsBusy &&
									(this.CountDownSeconds <= 0) &&
									!string.IsNullOrEmpty(this.EmailText) && !string.IsNullOrEmpty(this.PhoneText);

		[ObservableProperty]
		ISO_3166_Country selectedCountry = ISO_3166_1.DefaultCountry;


		[ObservableProperty]
		private string? localizedPhoneValidationError;
		public string LocalizedSendCodeText
		{
			get
			{
				if (this.CountDownSeconds > 0)
					return ServiceRef.Localizer[nameof(AppResources.SendSeconds), this.CountDownSeconds];

				return ServiceRef.Localizer[nameof(AppResources.Send)];
			}
		}



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
		public bool CanResendCode => this.CountDownSeconds <= 0;

		[RelayCommand(CanExecute = nameof(this.CanSend))]
		private async Task Send()
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

				string FullPhoneNumber = $"+{this.SelectedCountry.DialCode}{this.PhoneText}";

				if (this.SelectedCountry.DialCode == "46") // Adjust for Swedish numbers
					FullPhoneNumber = $"+{this.SelectedCountry.DialCode}{this.PhoneText.TrimStart('0')}";

				// Send phone verification code
				ContentResponse PhoneSendResult = await InternetContent.PostAsync(
					 new Uri("https://" + Constants.Domains.IdDomain + "/ID/SendVerificationMessage.ws"),
					 new Dictionary<string, object>
					 {
						 { "Nr", FullPhoneNumber },
						 { "AppName", Constants.Application.Name },
						 { "Language", CultureInfo.CurrentCulture.TwoLetterISOLanguageName }
					 }, new KeyValuePair<string, string>("Accept", "application/json"));

				PhoneSendResult.AssertOk();

				bool PhoneSent = PhoneSendResult.Decoded is Dictionary<string, object> PhoneResponse &&
									  PhoneResponse.TryGetValue("Status", out object? PhoneObj) &&
									  PhoneObj is bool PhoneStatus &&
									  PhoneStatus;

				if (PhoneSent)
				{
					this.StartTimer();

					// Navigate to VerifyCodePage for phone code
					if (!await this.VerifyCodeAsync(FullPhoneNumber, IsEmail: false))
					{
						return;
					}

					// Verification successful - DO STUFF
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
		private Task ResendCode()
		{
			return Task.CompletedTask;
		}

		private async Task<bool> VerifyCodeAsync(string Identifier, bool IsEmail)
		{
			VerifyCodeNavigationArgs navigationArgs = new(this, Identifier);
			await ServiceRef.UiService.GoToAsync(nameof(VerifyCodePage), navigationArgs, BackMethod.Pop);
			string? code = await navigationArgs.VarifyCode!.Task;

			if (!string.IsNullOrEmpty(code))
			{
				Dictionary<string, object> parameters = new()
				{
					{ IsEmail ? "EMail" : "Nr", Identifier },
					{ "Code", int.Parse(code, NumberStyles.None, CultureInfo.InvariantCulture) }
				};

				ContentResponse Response = await InternetContent.PostAsync(
					new Uri("https://" + Constants.Domains.IdDomain + "/ID/VerifyNumber.ws"),
					parameters, new KeyValuePair<string, string>("Accept", "application/json"));

				Response.AssertOk();

				bool Verified = Response.Decoded is Dictionary<string, object> VerifyResponse &&
									 VerifyResponse.TryGetValue("Status", out object? Obj) &&
									 Obj is bool Status &&
									 Status;

				return Verified;
			}

			return false;
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

		public bool CanScanQrCode => !this.IsBusy;


		[RelayCommand(CanExecute = nameof(CanScanQrCode))]
		private async Task ScanQrCode()
		{
			string? Url = await Services.UI.QR.QrCode.ScanQrCode(nameof(AppResources.QrPageTitleScanInvitation),
				[Constants.UriSchemes.Onboarding]);

			if (string.IsNullOrEmpty(Url))
				return;

			string Scheme = Constants.UriSchemes.GetScheme(Url) ?? string.Empty;

			if (!string.Equals(Scheme, Constants.UriSchemes.Onboarding, StringComparison.OrdinalIgnoreCase))
				return;

			string[] Parts = Url.Split(':');

			if (Parts.Length != 5)
			{
				await ServiceRef.UiService.DisplayAlert(
					ServiceRef.Localizer[nameof(AppResources.ErrorTitle)],
					ServiceRef.Localizer[nameof(AppResources.InvalidInvitationCode)],
					ServiceRef.Localizer[nameof(AppResources.Ok)]);

				return;
			}

			string Domain = Parts[1];
			string Code = Parts[2];
			string KeyStr = Parts[3];
			string IVStr = Parts[4];
			string EncryptedStr;
			Uri Uri;

			try
			{
				Uri = new Uri("https://" + Domain + "/Onboarding/GetInfo");
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);

				await ServiceRef.UiService.DisplayAlert(
					ServiceRef.Localizer[nameof(AppResources.ErrorTitle)],
					ServiceRef.Localizer[nameof(AppResources.InvalidInvitationCode)],
					ServiceRef.Localizer[nameof(AppResources.Ok)]);

				return;
			}

			this.IsBusy = true;

			try
			{
				try
				{
					ContentBinaryResponse Response = await InternetContent.PostAsync(Uri, Encoding.ASCII.GetBytes(Code), "text/plain",
						new KeyValuePair<string, string>("Accept", "text/plain"));
					Response.AssertOk();

					ContentResponse Decoded = await InternetContent.DecodeAsync(Response.ContentType, Response.Encoded, Uri);
					Decoded.AssertOk();

					EncryptedStr = (string)Decoded.Decoded;
				}
				catch (Exception ex)
				{
					ServiceRef.LogService.LogException(ex);

					await ServiceRef.UiService.DisplayAlert(
						ServiceRef.Localizer[nameof(AppResources.ErrorTitle)],
						ServiceRef.Localizer[nameof(AppResources.UnableToAccessInvitation)],
						ServiceRef.Localizer[nameof(AppResources.Ok)]);
					this.IsBusy = false;
					return;
				}

				try
				{
					byte[] Key = Convert.FromBase64String(KeyStr);
					byte[] IV = Convert.FromBase64String(IVStr);
					byte[] Encrypted = Convert.FromBase64String(EncryptedStr);

					using Aes Aes = Aes.Create();
					Aes.BlockSize = 128;
					Aes.KeySize = 256;
					Aes.Mode = CipherMode.CBC;
					Aes.Padding = PaddingMode.PKCS7;

					using ICryptoTransform Decryptor = Aes.CreateDecryptor(Key, IV);
					byte[] Decrypted = Decryptor.TransformFinalBlock(Encrypted, 0, Encrypted.Length);
					string Xml = Encoding.UTF8.GetString(Decrypted);

					XmlDocument Doc = new()
					{
						PreserveWhitespace = true
					};

					Doc.LoadXml(Xml);

					if ((Doc.DocumentElement is null) || (Doc.DocumentElement.NamespaceURI != ContractsClient.NamespaceOnboarding))
						throw new Exception("Invalid Invitation XML");

					LinkedList<XmlElement> ToProcess = new();
					ToProcess.AddLast(Doc.DocumentElement);

					bool AccountDone = false;
					XmlElement? LegalIdDefinition = null;
					string? Pin = null;

					while (ToProcess.First is not null)
					{
						XmlElement E = ToProcess.First.Value;
						ToProcess.RemoveFirst();

						switch (E.LocalName)
						{
							case "ApiKey":
								KeyStr = XML.Attribute(E, "key");
								string Secret = XML.Attribute(E, "secret");
								Domain = XML.Attribute(E, "domain");

								await SelectDomain(Domain, KeyStr, Secret);

								await ServiceRef.UiService.DisplayAlert(
									ServiceRef.Localizer[nameof(AppResources.InvitationAccepted)],
									ServiceRef.Localizer[nameof(AppResources.InvitedToCreateAccountOnDomain), Domain],
									ServiceRef.Localizer[nameof(AppResources.Ok)]);
								break;

							case "Account":
								string UserName = XML.Attribute(E, "userName");
								string Password = XML.Attribute(E, "password");
								string PasswordMethod = XML.Attribute(E, "passwordMethod");
								Domain = XML.Attribute(E, "domain");

								string DomainBak = ServiceRef.TagProfile?.Domain ?? string.Empty;
								bool DefaultConnectivityBak = ServiceRef.TagProfile?.DefaultXmppConnectivity ?? false;
								string ApiKeyBak = ServiceRef.TagProfile?.ApiKey ?? string.Empty;
								string ApiSecretBak = ServiceRef.TagProfile?.ApiSecret ?? string.Empty;

								await SelectDomain(Domain, string.Empty, string.Empty);

								if (!await this.ConnectToAccount(UserName, Password, PasswordMethod, string.Empty, LegalIdDefinition, Pin ?? string.Empty))
								{
									ServiceRef.TagProfile?.SetDomain(DomainBak, DefaultConnectivityBak, ApiKeyBak, ApiSecretBak);
									throw new Exception("Invalid account.");
								}

								LegalIdDefinition = null;
								AccountDone = true;
								break;

							case "LegalId":
								LegalIdDefinition = E;
								break;

							case "Pin":
								Pin = XML.Attribute(E, "pin");
								break;

							case "Transfer":
								foreach (XmlNode N in E.ChildNodes)
								{
									if (N is XmlElement E2)
									{
										ToProcess.AddLast(E2);
									}
								}
								break;

							default:
								throw new Exception("Invalid Invitation XML");
						}
					}

					if (AccountDone && LegalIdDefinition is not null)
					{
						await ServiceRef.XmppService.ImportSigningKeys(LegalIdDefinition);
						GoToRegistrationStep(RegistrationStep.Finalize);
					}
					else if (AccountDone)
					{
						GoToRegistrationStep(RegistrationStep.ValidatePhone);
					}
					else
						GoToRegistrationStep(RegistrationStep.ChooseProvider);
				}
				catch (Exception ex)
				{
					ServiceRef.LogService.LogException(ex);

					await ServiceRef.UiService.DisplayAlert(
						ServiceRef.Localizer[nameof(AppResources.ErrorTitle)],
						ServiceRef.Localizer[nameof(AppResources.InvalidInvitationCode)],
						ServiceRef.Localizer[nameof(AppResources.Ok)]);
				}
			}
			finally
			{
				this.IsBusy = false;
			}
		}

		private static async Task SelectDomain(string Domain, string Key, string Secret)
		{
			bool DefaultConnectivity;

			try
			{
				(string HostName, int PortNumber, bool IsIpAddress) = await ServiceRef.NetworkService.LookupXmppHostnameAndPort(Domain);
				DefaultConnectivity = HostName == Domain && PortNumber == XmppCredentials.DefaultPort;
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				DefaultConnectivity = false;
			}

			ServiceRef.TagProfile.SetDomain(Domain, DefaultConnectivity, Key, Secret);
		}


		private async Task<bool> ConnectToAccount(string AccountName, string Password, string PasswordMethod, string LegalIdentityJid, XmlElement? LegalIdDefinition, string Pin)
		{
			try
			{
				async Task OnConnected(XmppClient client)
				{
					DateTime now = DateTime.Now;
					LegalIdentity? createdIdentity = null;
					LegalIdentity? approvedIdentity = null;

					bool serviceDiscoverySucceeded;

					if (ServiceRef.TagProfile.NeedsUpdating())
					{
						serviceDiscoverySucceeded = await ServiceRef.XmppService.DiscoverServices(client);
					}
					else
					{
						serviceDiscoverySucceeded = true;
					}

					if (serviceDiscoverySucceeded && !string.IsNullOrEmpty(ServiceRef.TagProfile.LegalJid))
					{
						bool DestroyContractsClient = false;

						if (!client.TryGetExtension(typeof(ContractsClient), out IXmppExtension Extension) ||
							Extension is not ContractsClient ContractsClient)
						{
							ContractsClient = new ContractsClient(client, ServiceRef.TagProfile.LegalJid);
							DestroyContractsClient = true;
						}

						try
						{
							if (LegalIdDefinition is not null)
								await ContractsClient.ImportKeys(LegalIdDefinition);

							LegalIdentity[] Identities = await ContractsClient.GetLegalIdentitiesAsync();

							foreach (LegalIdentity Identity in Identities)
							{
								try
								{
									if ((string.IsNullOrEmpty(LegalIdentityJid) || string.Compare(LegalIdentityJid, Identity.Id, StringComparison.OrdinalIgnoreCase) == 0) &&
										Identity.HasClientSignature &&
										Identity.HasClientPublicKey &&
										Identity.From <= now &&
										Identity.To >= now &&
										(Identity.State == IdentityState.Approved || Identity.State == IdentityState.Created) &&
										Identity.ValidateClientSignature() &&
										await ContractsClient.HasPrivateKey(Identity))
									{
										if (Identity.State == IdentityState.Approved)
										{
											approvedIdentity = Identity;
											break;
										}

										createdIdentity ??= Identity;
									}
								}
								catch (Exception)
								{
									// Keys might not be available. Ignore at this point. Keys will be generated later.
								}
							}

							/*
							if (approvedIdentity is not null)
							{
								this.LegalIdentity = approvedIdentity;
							}
							else if (createdIdentity is not null)
							{
								this.LegalIdentity = createdIdentity;
							}*/
							LegalIdentity? selectedIdentity = approvedIdentity ?? createdIdentity;

							string SelectedId;

							if (selectedIdentity is not null)
							{
								await ServiceRef.TagProfile.SetAccountAndLegalIdentity(AccountName, client.PasswordHash, client.PasswordHashMethod, selectedIdentity);
								SelectedId = selectedIdentity.Id;
							}
							else
							{
								ServiceRef.TagProfile.SetAccount(AccountName, client.PasswordHash, client.PasswordHashMethod);
								SelectedId = string.Empty;
							}

							if (!string.IsNullOrEmpty(Pin))
							{
								ServiceRef.TagProfile.LocalPassword = Pin;
							}

							foreach (LegalIdentity Identity in Identities)
							{
								if (Identity.Id == SelectedId)
								{
									continue;
								}

								switch (Identity.State)
								{
									case IdentityState.Approved:
									case IdentityState.Created:
										await ContractsClient.ObsoleteLegalIdentityAsync(Identity.Id);
										break;
								}
							}
						}
						finally
						{
							if (DestroyContractsClient)
							{
								ContractsClient.Dispose();
							}
						}
					}
				}

				(string hostName, int portNumber, bool isIpAddress) = await ServiceRef.NetworkService.LookupXmppHostnameAndPort(
					ServiceRef.TagProfile?.Domain ?? string.Empty);

				(bool succeeded, string? errorMessage, string[]? alternatives) = await ServiceRef.XmppService.TryConnectAndConnectToAccount(
					ServiceRef.TagProfile?.Domain ?? string.Empty,
					isIpAddress, hostName, portNumber, AccountName, Password, PasswordMethod, Constants.LanguageCodes.Default,
					typeof(App).Assembly, OnConnected);

				if (!succeeded)
				{
					await ServiceRef.UiService.DisplayAlert(
						ServiceRef.Localizer[nameof(AppResources.ErrorTitle)],
						errorMessage ?? string.Empty,
						ServiceRef.Localizer[nameof(AppResources.Ok)]);
				}
				return succeeded;
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);

				await ServiceRef.UiService.DisplayAlert(
					ServiceRef.Localizer[nameof(AppResources.ErrorTitle)],
					ServiceRef.Localizer[nameof(AppResources.UnableToConnectTo), ServiceRef.TagProfile?.Domain ?? string.Empty],
					ServiceRef.Localizer[nameof(AppResources.Ok)]);
			}

			return false;
		}
	}
}
