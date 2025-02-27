using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls.Shapes;
using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services;
using NeuroAccessMaui.Services.Localization;
using NeuroAccessMaui.Services.Tag;
using NeuroAccessMaui.UI.Popups.Info;
using Waher.Content;
using Waher.Content.Xml;
using Waher.Networking.XMPP;
using Waher.Networking.XMPP.Contracts;

namespace NeuroAccessMaui.UI.Pages.Registration.Views
{
	public partial class ChooseProviderViewModel : BaseRegistrationViewModel
	{
		public ChooseProviderViewModel()
			: base(RegistrationStep.ChooseProvider)
		{
		}

		/// <inheritdoc />
		protected override async Task OnInitialize()
		{
			await base.OnInitialize();

			ServiceRef.TagProfile.Changed += this.TagProfile_Changed;
			LocalizationManager.Current.PropertyChanged += this.Localization_Changed;
		}

		/// <inheritdoc />
		protected override async Task OnDispose()
		{
			ServiceRef.TagProfile.Changed -= this.TagProfile_Changed;
			LocalizationManager.Current.PropertyChanged -= this.Localization_Changed;

			await base.OnDispose();
		}

		/// <inheritdoc />
		public override async Task DoAssignProperties()
		{
			await base.DoAssignProperties();

			if (IsAccountCreated)
				GoToRegistrationStep(RegistrationStep.CreateAccount);
		}

		protected override void OnPropertyChanged(PropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);

			switch (e.PropertyName)
			{
				/*
				case nameof(this.SelectedButton):
					if ((this.SelectedButton is not null) && (this.SelectedButton.Button == ButtonType.Change))
					{
						MainThread.BeginInvokeOnMainThread(() =>
						{
							if (this.ScanQrCodeCommand.CanExecute(null))
								this.ScanQrCodeCommand.Execute(null);
						});
					}
					break;
				*/
				case nameof(this.IsBusy):
					this.ContinueCommand.NotifyCanExecuteChanged();
					this.ScanQrCodeCommand.NotifyCanExecuteChanged();
					break;
			}
		}

		/// <summary>
		/// The localized intro text to display to the user for explaining what 'choose account' is for.
		/// </summary>
		[ObservableProperty]
		private string domainName = string.Empty;

		/// <summary>
		/// The localized intro text to display to the user for explaining what 'choose account' is for.
		/// </summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(HasLocalizedName))]
		private string localizedName = string.Empty;

		/// <summary>
		/// The localized intro text to display to the user for explaining what 'choose account' is for.
		/// </summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(HasLocalizedDescription))]
		private string localizedDescription = string.Empty;

		/// <summary>
		/// The localized intro text to display to the user for explaining what 'choose account' is for.
		/// </summary>
		public bool HasLocalizedName => this.LocalizedName.Length > 0;

		/// <summary>
		/// The localized intro text to display to the user for explaining what 'choose account' is for.
		/// </summary>
		public bool HasLocalizedDescription => this.LocalizedDescription.Length > 0;

		/// <summary>
		/// If App has an XMPP account defined.
		/// </summary>
		public static bool IsAccountCreated => !string.IsNullOrEmpty(ServiceRef.TagProfile.Account);

		/*
				/// <summary>
				/// Holds the list of buttons to display.
				/// </summary>
				public Collection<ButtonInfo> Buttons { get; } =
					[
						new(ButtonType.Approve),
						new(ButtonType.Change),
					];


				/// <summary>
				/// The selected Button
				/// </summary>
				[ObservableProperty]
				[NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
				private ButtonInfo? selectedButton;
		*/
		private async void TagProfile_Changed(object? Sender, PropertyChangedEventArgs e)
		{
			if (this.DomainName != ServiceRef.TagProfile.Domain)
				await this.SetDomainName();
		}

		private async void Localization_Changed(object? Sender, PropertyChangedEventArgs e)
		{
			await this.SetDomainName();
		}

		private async Task SetDomainName()
		{
			if (string.IsNullOrEmpty(ServiceRef.TagProfile.Domain))
			{
				this.DomainName = string.Empty;
				this.LocalizedName = string.Empty;
				this.LocalizedDescription = string.Empty;
				return;
			}

			this.DomainName = ServiceRef.TagProfile.Domain;

			try
			{
				Uri DomainInfo = new("https://" + this.DomainName + "/Agent/Account/DomainInfo");
				string AcceptLanguage = App.SelectedLanguage.TwoLetterISOLanguageName;

				if (AcceptLanguage != "en")
					AcceptLanguage += ";q=1,en;q=0.9";

				ContentResponse Result = await InternetContent.GetAsync(DomainInfo,
					new KeyValuePair<string, string>("Accept", "application/json"),
					new KeyValuePair<string, string>("Accept-Language", AcceptLanguage),
					new KeyValuePair<string, string>("Accept-Encoding", "0"));

				if (Result.HasError)
				{
					ServiceRef.LogService.LogException(Result.Error);
					return;
				}

				if (Result.Decoded is Dictionary<string, object> Response)
				{
					if (Response.TryGetValue("humanReadableName", out object? Obj) && Obj is string LocalizedName)
						this.LocalizedName = LocalizedName;

					if (Response.TryGetValue("humanReadableDescription", out Obj) && Obj is string LocalizedDescription)
						this.LocalizedDescription = LocalizedDescription;
				}
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
			}
		}

		public bool CanScanQrCode => !this.IsBusy;

		public bool CanContinue => !this.IsBusy;
		//&& (this.SelectedButton is not null) && (this.SelectedButton.Button == ButtonType.Approve);

		[RelayCommand]
		private void Continue()
		{
			GoToRegistrationStep(RegistrationStep.ValidatePhone);
		}

		[RelayCommand]
		private static async Task ServiceProviderInfo()
		{
			string title = ServiceRef.Localizer[nameof(AppResources.WhatIsAServiceProvider)];
			string message = ServiceRef.Localizer[nameof(AppResources.ServiceProviderInfo)];
			ShowInfoPopup infoPage = new(title, message);
			await ServiceRef.UiService.PushAsync(infoPage);
		}

		[RelayCommand]
		private async Task SelectedServiceProviderInfo()
		{
			string title = this.LocalizedName;
			string message = this.LocalizedDescription;
			ShowInfoPopup infoPage = new(title, message);
			await ServiceRef.UiService.PushAsync(infoPage);
		}

		[RelayCommand]
		private static void UndoSelection()
		{
			ServiceRef.TagProfile.UndoDomainSelection();
		}

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

					if (LegalIdDefinition is not null)
						await ServiceRef.XmppService.ImportSigningKeys(LegalIdDefinition);

					if (AccountDone)
						GoToRegistrationStep(RegistrationStep.CreateAccount);
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
	/*
		public enum ButtonType
		{
			Approve = 0,
			Change = 1,
		}

		public partial class ButtonInfo : ObservableObject
		{
			public ButtonInfo(ButtonType Button)
			{
				this.Button = Button;

				LocalizationManager.CurrentCultureChanged += this.OnCurrentCultureChanged;
			}

			~ButtonInfo()
			{
				LocalizationManager.CurrentCultureChanged -= this.OnCurrentCultureChanged;
			}

			private void OnCurrentCultureChanged(object? Sender, CultureInfo Culture)
			{
				this.OnPropertyChanged(nameof(this.LocalizedName));
				//!!! not implemented yet
				// this.OnPropertyChanged(nameof(this.LocalizedDescription));
			}

			public ButtonType Button { get; set; }

			public string LocalizedName
			{
				get
				{
					return this.Button switch
					{
						ButtonType.Approve => ServiceRef.Localizer[nameof(AppResources.ProviderSectionApproveOption)],
						ButtonType.Change => ServiceRef.Localizer[nameof(AppResources.ProviderSectionChangeOption)],
						_ => throw new NotImplementedException(),
					};
				}
			}

			public Geometry ImageData
			{
				get
				{
					return this.Button switch
					{
						ButtonType.Approve => Geometries.ApproveProviderIconPath,
						ButtonType.Change => Geometries.ChangeProviderIconPath,
						_ => throw new NotImplementedException(),
					};
				}
			}
		}
		*/
}
