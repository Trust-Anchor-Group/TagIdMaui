using NeuroAccessMaui.Extensions;
using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services.Contracts;
using NeuroAccessMaui.Services.Storage;
using NeuroAccessMaui.UI;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Waher.Events;
using Waher.Networking.XMPP.Contracts;
using Waher.Runtime.Inventory;
using Waher.Security;

namespace NeuroAccessMaui.Services.Tag
{
	/// <summary>
	/// The TAG Profile is the heart of the digital identity for a specific user/device.
	/// Use this instance to add and make a profile more complete.
	/// The TAG Profile holds relevant data connected to not only where the user is in the registration process,
	/// but also Xmpp identifiers.
	/// </summary>
	[Singleton]
	public partial class TagProfile : ITagProfile
	{
		private readonly WeakEventManager onStepChangedEventManager = new();
		private readonly WeakEventManager onChangedEventManager = new();

		/// <summary>
		/// An event that fires every time the <see cref="Step"/> property changes.
		/// </summary>
		public event EventHandler? StepChanged
		{
			add => this.onStepChangedEventManager.AddEventHandler(value);
			remove => this.onStepChangedEventManager.RemoveEventHandler(value);
		}

		/// <summary>
		/// An event that fires every time any property changes.
		/// </summary>
		public event PropertyChangedEventHandler? Changed
		{
			add => this.onChangedEventManager.AddEventHandler(value);
			remove => this.onChangedEventManager.RemoveEventHandler(value);
		}

		private LegalIdentity? legalIdentity;
		private LegalIdentity? identityApplication;
		private string? objectId;
		private string? initialDomain;
		private string? initialApiKey;
		private string? initialApiSecret;
		private string? domain;
		private string? apiKey;
		private string? apiSecret;
		private string? selectedCountry;
		private string? firstName;
		private string? lastName;
		private string? friendlyName;
		private string? phoneNumber;
		private string? eMail;
		private string? account;
		private string? xmppPasswordHash;
		private string? xmppPasswordHashMethod;
		private string? legalJid;
		private string? registryJid;
		private string? provisioningJid;
		private string? httpFileUploadJid;
		private string? logJid;
		private string? eDalerJid;
		private string? neuroFeaturesJid;
		private string? trustProviderId;
		private string? localPasswordHash;
		private long httpFileUploadMaxSize;
		private bool supportsPushNotification;
		private bool isNumericPassword;
		private int nrReviews;
		private bool isTest;
		private PurposeUse purpose;
		private DateTime? testOtpTimestamp;
		private RegistrationStep step;
		private AppTheme? theme;
		private AuthenticationMethod authenticationMethod;
		private bool loadingProperties;
		private bool initialDefaultXmppConnectivity;
		private bool defaultXmppConnectivity;
		private bool hasContractReferences;
		private bool hasContractTemplateReferences;
		private bool hasContractTokenCreationTemplatesReferences;
		private bool hasWallet;
		private bool hasThing;
		private DateTime? lastIdentityUpdate;

		/// <summary>
		/// Creates an instance of a <see cref="TagProfile"/>.
		/// </summary>
		public TagProfile()
		{
		}

		/// <summary>
		/// Invoked whenever the current <see cref="Step"/> changes, to fire the <see cref="StepChanged"/> event.
		/// </summary>
		/// <param name="e"></param>
		protected virtual void OnStepChanged(EventArgs e)
		{
			this.onStepChangedEventManager.HandleEvent(this, EventArgs.Empty, nameof(StepChanged));
		}

		/// <summary>
		/// Invoked whenever any property changes, to fire the <see cref="Changed"/> event.
		/// </summary>
		/// <param name="e"></param>
		protected virtual void OnChanged(PropertyChangedEventArgs e)
		{
			this.onChangedEventManager.HandleEvent(this, e, nameof(Changed));
		}

		/// <summary>
		/// Converts the current <see cref="ITagProfile"/> to a <see cref="TagConfiguration"/> object that can be persisted to the <see cref="IStorageService"/>.
		/// </summary>
		/// <returns>Configuration object</returns>
		public TagConfiguration ToConfiguration()
		{
			TagConfiguration Clone = new()
			{
				ObjectId = this.objectId,
				InitialDefaultXmppConnectivity = this.InitialDefaultXmppConnectivity,
				InitialDomain = this.InitialDomain,
				InitialApiKey = this.InitialApiKey,
				InitialApiSecret = this.InitialApiSecret,
				Domain = this.Domain,
				ApiKey = this.ApiKey,
				ApiSecret = this.ApiSecret,
				SelectedCountry = this.SelectedCountry,
				FirstName = this.FirstName,
				LastName = this.LastName,
				FriendlyName = this.FriendlyName,
				PhoneNumber = this.PhoneNumber,
				EMail = this.EMail,
				DefaultXmppConnectivity = this.DefaultXmppConnectivity,
				Account = this.Account,
				PasswordHash = this.XmppPasswordHash,
				PasswordHashMethod = this.XmppPasswordHashMethod,
				LegalJid = this.LegalJid,
				RegistryJid = this.RegistryJid,
				ProvisioningJid = this.ProvisioningJid,
				HttpFileUploadJid = this.HttpFileUploadJid,
				HttpFileUploadMaxSize = this.HttpFileUploadMaxSize,
				LogJid = this.LogJid,
				EDalerJid = this.EDalerJid,
				NeuroFeaturesJid = this.NeuroFeaturesJid,
				TrustProviderId = this.TrustProviderId,
				SupportsPushNotification = this.SupportsPushNotification,
				PinHash = this.LocalPasswordHash,
				IsNumericPassword = this.IsNumericPassword,
				IsTest = this.IsTest,
				Purpose = this.Purpose,
				TestOtpTimestamp = this.TestOtpTimestamp,
				LegalIdentity = this.LegalIdentity,
				IdentityApplication = this.identityApplication,
				NrReviews = this.NrReviews,
				Step = this.Step,
				Theme = this.Theme,
				AuthenticationMethod = this.AuthenticationMethod,
				HasContractReferences = this.HasContractReferences,
				HasContractTemplateReferences = this.HasContractTemplateReferences,
				HasContractTokenCreationTemplatesReferences = this.HasContractTokenCreationTemplatesReferences,
				HasWallet = this.HasWallet,
				HasThing = this.HasThing,
				LastIdentityUpdate = this.LastIdentityUpdate
			};

			return Clone;
		}

		/// <summary>
		/// Copies values from the <see cref="TagConfiguration"/> to this instance.
		/// </summary>
		/// <param name="Configuration">Configuration object.</param>
		public void FromConfiguration(TagConfiguration Configuration)
		{
			try
			{
				this.loadingProperties = true;
				this.IsDirty = false;

				this.objectId = Configuration.ObjectId;
				this.InitialDomain = Configuration.InitialDomain;
				this.InitialApiKey = Configuration.InitialApiKey;
				this.InitialApiSecret = Configuration.InitialApiSecret;
				this.Domain = Configuration.Domain;
				this.ApiKey = Configuration.ApiKey;
				this.ApiSecret = Configuration.ApiSecret;
				this.SelectedCountry = Configuration.SelectedCountry;
				this.FirstName = Configuration.FirstName;
				this.LastName = Configuration.LastName;
				this.FriendlyName = Configuration.FriendlyName;
				this.PhoneNumber = Configuration.PhoneNumber;
				this.EMail = Configuration.EMail;
				this.InitialDefaultXmppConnectivity = Configuration.InitialDefaultXmppConnectivity;
				this.DefaultXmppConnectivity = Configuration.DefaultXmppConnectivity;
				this.Account = Configuration.Account;
				this.XmppPasswordHash = Configuration.PasswordHash;
				this.XmppPasswordHashMethod = Configuration.PasswordHashMethod;
				this.LegalJid = Configuration.LegalJid;
				this.RegistryJid = Configuration.RegistryJid;
				this.ProvisioningJid = Configuration.ProvisioningJid;
				this.HttpFileUploadJid = Configuration.HttpFileUploadJid;
				this.HttpFileUploadMaxSize = Configuration.HttpFileUploadMaxSize;
				this.LogJid = Configuration.LogJid;
				this.EDalerJid = Configuration.EDalerJid;
				this.NeuroFeaturesJid = Configuration.NeuroFeaturesJid;
				this.TrustProviderId = Configuration.TrustProviderId;
				this.SupportsPushNotification = Configuration.SupportsPushNotification;
				this.LocalPasswordHash = Configuration.PinHash;
				this.IsNumericPassword = Configuration.IsNumericPassword;
				this.IsTest = Configuration.IsTest;
				this.Purpose = Configuration.Purpose;
				this.TestOtpTimestamp = Configuration.TestOtpTimestamp;
				this.identityApplication = Configuration.IdentityApplication;
				this.nrReviews = Configuration.NrReviews;
				this.Theme = Configuration.Theme;
				this.AuthenticationMethod = Configuration.AuthenticationMethod;
				this.HasContractReferences = Configuration.HasContractReferences;
				this.HasContractTemplateReferences = Configuration.HasContractTemplateReferences;
				this.HasContractTokenCreationTemplatesReferences = Configuration.HasContractTokenCreationTemplatesReferences;
				this.HasWallet = Configuration.HasWallet;
				this.HasThing = Configuration.HasThing;
				this.LastIdentityUpdate = Configuration.LastIdentityUpdate ?? DateTime.MinValue;

				this.SetLegalIdentityInternal(Configuration.LegalIdentity);

				this.SetTheme();
				// Do this last, as listeners will read the other properties when the event is fired.
				if (Configuration.Step > RegistrationStep.GetStarted && Configuration.Step <= RegistrationStep.CreateAccount)
					this.GoToStep(RegistrationStep.ValidatePhone);
				else
					this.GoToStep(Configuration.Step);
			}
			finally
			{
				this.loadingProperties = false;
			}
		}

		/// <summary>
		/// Returns <c>true</c> if the current <see cref="ITagProfile"/> needs to have its values updated, <c>false</c> otherwise.
		/// </summary>
		/// <returns>If values need updating</returns>
		public virtual bool NeedsUpdating()
		{
			return string.IsNullOrWhiteSpace(this.legalJid) ||
					string.IsNullOrWhiteSpace(this.registryJid) ||
					string.IsNullOrWhiteSpace(this.provisioningJid) ||
					string.IsNullOrWhiteSpace(this.httpFileUploadJid) ||
					string.IsNullOrWhiteSpace(this.logJid) ||
					string.IsNullOrWhiteSpace(this.eDalerJid) ||
					string.IsNullOrWhiteSpace(this.neuroFeaturesJid);
		}

		/// <summary>
		/// Returns <c>true</c> if the current <see cref="ITagProfile"/> needs to have its legal identity updated, <c>false</c> otherwise.
		/// </summary>
		/// <returns>If legal identity need updating</returns>
		public virtual bool LegalIdentityNeedsUpdating()
		{
			return this.legalIdentity?.IsDiscarded() ?? true;
		}

		/// <summary>
		/// If the current <see cref="ITagProfile"/> needs to have its legal identity refreshed, because it can have missed offline messages.
		/// </summary>
		/// <returns>Returns <c>true</c> if the current <see cref="ITagProfile"/> needs to have its legal identity refreshed, <c>false</c> otherwise.</returns>
		public virtual bool LegalIdentityNeedsRefreshing()
		{
			return (DateTime.UtcNow - this.LastIdentityUpdate) > Constants.Intervals.ForceRefresh;
		}

		/// <summary>
		/// Returns <c>true</c> if the registration process for this <see cref="ITagProfile"/> has an account but not a legal id,
		/// <c>false</c> otherwise.
		/// </summary>
		/// <returns><c>true</c> if the registration process for this <see cref="ITagProfile"/> has an account but not a legal id,
		/// <c>false</c> otherwise.</returns>
		public virtual bool ShouldCreateClient()
		{
			return this.Step >= RegistrationStep.CreateAccount && !string.IsNullOrEmpty(this.Account);
		}

		/// <summary>
		/// Returns <c>true</c> if the registration process for this <see cref="ITagProfile"/> is either fully complete or is just awaiting validation, <c>false</c> otherwise.
		/// </summary>
		/// <returns><c>true</c> if the registration process for this <see cref="ITagProfile"/> is either fully complete or is just awaiting validation, <c>false</c> otherwise.</returns>
		public virtual bool IsCompleteOrWaitingForValidation()
		{
			return this.Step >= RegistrationStep.CreateAccount &&
				this.LegalIdentity is not null &&
				this.LegalIdentity.State == IdentityState.Created;
		}

		/// <summary>
		/// Returns <c>true</c> if the registration process for this <see cref="ITagProfile"/> is either fully complete, <c>false</c> otherwise.
		/// </summary>
		/// <returns><c>true</c> if the registration process for this <see cref="ITagProfile"/> is either fully complete, <c>false</c> otherwise.</returns>
		public virtual bool IsComplete()
		{
			return this.Step == RegistrationStep.Complete;
		}

		#region Properties

		/// <summary>
		/// Initial domain.
		/// </summary>
		public string? InitialDomain
		{
			get => this.initialDomain;
			private set
			{
				if (!string.Equals(this.initialDomain, value, StringComparison.Ordinal))
				{
					this.initialDomain = value;
					this.FlagAsDirty(nameof(this.InitialDomain));
				}
			}
		}

		/// <summary>
		/// If the <see cref="InitialDomain"/> employs default XMPP connectivity.
		/// </summary>
		public bool InitialDefaultXmppConnectivity
		{
			get => this.initialDefaultXmppConnectivity;
			private set
			{
				if (this.initialDefaultXmppConnectivity != value)
				{
					this.initialDefaultXmppConnectivity = value;
					this.FlagAsDirty(nameof(this.InitialDefaultXmppConnectivity));
				}
			}
		}

		/// <summary>
		/// API Key for the <see cref="InitialDomain"/>
		/// </summary>
		public string? InitialApiKey
		{
			get => this.initialApiKey;
			private set
			{
				if (!string.Equals(this.initialApiKey, value, StringComparison.Ordinal))
				{
					this.initialApiKey = value;
					this.FlagAsDirty(nameof(this.InitialApiKey));
				}
			}
		}

		/// <summary>
		/// Secret for the <see cref="InitialApiKey"/>
		/// </summary>
		public string? InitialApiSecret
		{
			get => this.initialApiSecret;
			private set
			{
				if (!string.Equals(this.initialApiSecret, value, StringComparison.Ordinal))
				{
					this.initialApiSecret = value;
					this.FlagAsDirty(nameof(this.InitialApiSecret));
				}
			}
		}

		/// <summary>
		/// The domain this profile is connected to.
		/// </summary>
		public string? Domain
		{
			get => this.domain;
			private set
			{
				if (!string.Equals(this.domain, value, StringComparison.Ordinal))
				{
					this.domain = value;
					this.FlagAsDirty(nameof(this.Domain));
				}
			}
		}

		/// <summary>
		/// If connecting to the domain can be done using default parameters (host=domain, default c2s port).
		/// </summary>
		public bool DefaultXmppConnectivity
		{
			get => this.defaultXmppConnectivity;
			private set
			{
				if (this.defaultXmppConnectivity != value)
				{
					this.defaultXmppConnectivity = value;
					this.FlagAsDirty(nameof(this.DefaultXmppConnectivity));
				}
			}
		}

		/// <summary>
		/// API Key, for creating new account.
		/// </summary>
		public string? ApiKey
		{
			get => this.apiKey;
			private set
			{
				if (!string.Equals(this.apiKey, value, StringComparison.Ordinal))
				{
					this.apiKey = value;
					this.FlagAsDirty(nameof(this.ApiKey));
				}
			}
		}

		/// <summary>
		/// API Secret, for creating new account.
		/// </summary>
		public string? ApiSecret
		{
			get => this.apiSecret;
			private set
			{
				if (!string.Equals(this.apiSecret, value, StringComparison.Ordinal))
				{
					this.apiSecret = value;
					this.FlagAsDirty(nameof(this.ApiSecret));
				}
			}
		}

		/// <summary>
		/// Selected country. Some countries have the same phone code, so we want to save the selected country
		/// </summary>
		public string? SelectedCountry
		{
			get => this.selectedCountry;
			private set
			{
				if (!string.Equals(this.selectedCountry, value, StringComparison.Ordinal))
				{
					this.selectedCountry = value;
					this.FlagAsDirty(nameof(this.SelectedCountry));
				}
			}
		}

		/// <summary>
		/// User's first name(s).
		/// </summary>
		public string? FirstName
		{
			get => this.firstName;
			set
			{
				if (!string.Equals(this.firstName, value, StringComparison.Ordinal))
				{
					this.firstName = value;
					this.FlagAsDirty(nameof(this.FirstName));
				}
			}
		}

		/// <summary>
		/// User's last name(s).
		/// </summary>
		public string? LastName
		{
			get => this.lastName;
			set
			{
				if (!string.Equals(this.lastName, value, StringComparison.Ordinal))
				{
					this.lastName = value;
					this.FlagAsDirty(nameof(this.LastName));
				}
			}
		}

		public string? FriendlyName
		{
			get => this.friendlyName;
			set
			{
				if (!string.Equals(this.friendlyName, value, StringComparison.Ordinal))
				{
					this.friendlyName = value;
					this.FlagAsDirty(nameof(this.FriendlyName));
				}
			}
		}

		/// <summary>
		/// Verified phone number.
		/// </summary>
		public string? PhoneNumber
		{
			get => this.phoneNumber;
			private set
			{
				if (!string.Equals(this.phoneNumber, value, StringComparison.Ordinal))
				{
					this.phoneNumber = value;
					this.FlagAsDirty(nameof(this.PhoneNumber));
				}
			}
		}

		/// <summary>
		/// Verified e-mail address.
		/// </summary>
		public string? EMail
		{
			get => this.eMail;
			set
			{
				if (!string.Equals(this.eMail, value, StringComparison.Ordinal))
				{
					this.eMail = value;
					this.FlagAsDirty(nameof(this.EMail));
				}
			}
		}

		/// <summary>
		/// The account name for this profile
		/// </summary>
		public string? Account
		{
			get => this.account;
			private set
			{
				if (!string.Equals(this.account, value, StringComparison.Ordinal))
				{
					this.account = value;
					this.FlagAsDirty(nameof(this.Account));
				}
			}
		}

		/// <summary>
		/// A hash of the current XMPP password.
		/// </summary>
		public string? XmppPasswordHash
		{
			get => this.xmppPasswordHash;
			private set
			{
				if (!string.Equals(this.xmppPasswordHash, value, StringComparison.Ordinal))
				{
					this.xmppPasswordHash = value;
					this.FlagAsDirty(nameof(this.XmppPasswordHash));
				}
			}
		}

		/// <summary>
		/// The hash method used for hashing the XMPP password.
		/// </summary>
		public string? XmppPasswordHashMethod
		{
			get => this.xmppPasswordHashMethod;
			private set
			{
				if (!string.Equals(this.xmppPasswordHashMethod, value, StringComparison.Ordinal))
				{
					this.xmppPasswordHashMethod = value;
					this.FlagAsDirty(nameof(this.XmppPasswordHashMethod));
				}
			}
		}

		/// <summary>
		/// The Jabber Legal JID for this user/profile.
		/// </summary>
		public string? LegalJid
		{
			get => this.legalJid;
			set
			{
				if (!string.Equals(this.legalJid, value, StringComparison.Ordinal))
				{
					this.legalJid = value;
					this.FlagAsDirty(nameof(this.LegalJid));
				}
			}
		}

		/// <summary>
		/// The Thing Registry JID
		/// </summary>
		public string? RegistryJid
		{
			get => this.registryJid;
			set
			{
				if (!string.Equals(this.registryJid, value, StringComparison.Ordinal))
				{
					this.registryJid = value;
					this.FlagAsDirty(nameof(this.RegistryJid));
				}
			}
		}

		/// <summary>
		/// The XMPP server's provisioning Jid.
		/// </summary>
		public string? ProvisioningJid
		{
			get => this.provisioningJid;
			set
			{
				if (!string.Equals(this.provisioningJid, value, StringComparison.Ordinal))
				{
					this.provisioningJid = value;
					this.FlagAsDirty(nameof(this.ProvisioningJid));
				}
			}
		}

		/// <summary>
		/// The XMPP server's file upload Jid.
		/// </summary>
		public string? HttpFileUploadJid
		{
			get => this.httpFileUploadJid;
			private set
			{
				if (!string.Equals(this.httpFileUploadJid, value, StringComparison.Ordinal))
				{
					this.httpFileUploadJid = value;
					this.FlagAsDirty(nameof(this.HttpFileUploadJid));
				}
			}
		}

		/// <summary>
		/// The XMPP server's max size for file uploads.
		/// </summary>
		public long HttpFileUploadMaxSize
		{
			get => this.httpFileUploadMaxSize;
			private set
			{
				if (this.httpFileUploadMaxSize != value)
				{
					this.httpFileUploadMaxSize = value;
					this.FlagAsDirty(nameof(this.HttpFileUploadMaxSize));
				}
			}
		}

		/// <summary>
		/// The XMPP server's log Jid.
		/// </summary>
		public string? LogJid
		{
			get => this.logJid;
			set
			{
				if (!string.Equals(this.logJid, value, StringComparison.Ordinal))
				{
					this.logJid = value;
					this.FlagAsDirty(nameof(this.LogJid));
				}
			}
		}

		/// <summary>
		/// The XMPP server's eDaler service JID.
		/// </summary>
		public string? EDalerJid
		{
			get => this.eDalerJid;
			set
			{
				if (!string.Equals(this.eDalerJid, value, StringComparison.Ordinal))
				{
					this.eDalerJid = value;
					this.FlagAsDirty(nameof(this.EDalerJid));
				}
			}
		}

		/// <summary>
		/// The XMPP server's Neuro-Features service JID.
		/// </summary>
		public string? NeuroFeaturesJid
		{
			get => this.neuroFeaturesJid;
			set
			{
				if (!string.Equals(this.neuroFeaturesJid, value, StringComparison.Ordinal))
				{
					this.neuroFeaturesJid = value;
					this.FlagAsDirty(nameof(this.NeuroFeaturesJid));
				}
			}
		}

		public string? TrustProviderId
		{
			get => this.trustProviderId;
			set
			{
				if (!string.Equals(this.trustProviderId, value, StringComparison.Ordinal))
				{
					this.trustProviderId = value;
					this.FlagAsDirty(nameof(this.TrustProviderId));
				}
			}
		}

		/// <summary>
		/// If Push Notification is supported by server.
		/// </summary>
		public bool SupportsPushNotification
		{
			get => this.supportsPushNotification;
			set
			{
				if (this.supportsPushNotification != value)
				{
					this.supportsPushNotification = value;
					this.FlagAsDirty(nameof(this.SupportsPushNotification));
				}
			}
		}

		/// <summary>
		/// If there exist <see cref="ContractReference"/> objects created.
		/// </summary>
		public bool HasContractReferences
		{
			get => this.hasContractReferences;
			set
			{
				if (this.hasContractReferences != value)
				{
					this.hasContractReferences = value;
					this.FlagAsDirty(nameof(this.HasContractReferences));
				}
			}
		}

		/// <summary>
		/// If there exist <see cref="ContractReference"/> objects created, referencing contract templates.
		/// </summary>
		public bool HasContractTemplateReferences
		{
			get => this.hasContractTemplateReferences;
			set
			{
				if (this.hasContractTemplateReferences != value)
				{
					this.hasContractTemplateReferences = value;
					this.FlagAsDirty(nameof(this.HasContractTemplateReferences));
				}
			}
		}

		/// <summary>
		/// If there exist <see cref="ContractReference"/> objects created, referencing contract templates for the creation of tokens.
		/// </summary>
		public bool HasContractTokenCreationTemplatesReferences
		{
			get => this.hasContractTokenCreationTemplatesReferences;
			set
			{
				if (this.hasContractTokenCreationTemplatesReferences != value)
				{
					this.hasContractTokenCreationTemplatesReferences = value;
					this.FlagAsDirty(nameof(this.HasContractTokenCreationTemplatesReferences));
				}
			}
		}

		/// <summary>
		/// If the user has a wallet.
		/// </summary>
		public bool HasWallet
		{
			get => this.hasWallet;
			set
			{
				if (this.hasWallet != value)
				{
					this.hasWallet = value;
					this.FlagAsDirty(nameof(this.HasWallet));
				}
			}
		}

		public bool HasThing
		{
			get => this.hasThing;
			set
			{
				if (this.hasThing != value)
				{
					this.hasThing = value;
					this.FlagAsDirty(nameof(this.HasThing));
				}
			}
		}

		public DateTime LastIdentityUpdate
		{
			get => this.lastIdentityUpdate ?? DateTime.MinValue;
			set
			{
				if (this.lastIdentityUpdate != value)
				{
					this.lastIdentityUpdate = value;
					this.FlagAsDirty(nameof(this.LastIdentityUpdate));
				}
			}
		}

		/// <summary>
		/// This profile's current registration step.
		/// </summary>
		public RegistrationStep Step => this.step;

		/// <summary>
		/// Changes the current onboarding step.
		/// </summary>
		/// <param name="NewStep">New step</param>
		/// <param name="SupressEvent">If registration step event should be supressed (default=false).</param>
		public void GoToStep(RegistrationStep NewStep, bool SupressEvent = false)
		{
			if (this.step != NewStep)
			{
				this.step = NewStep;
				this.FlagAsDirty(nameof(this.Step));

				if (!SupressEvent)
					this.OnStepChanged(EventArgs.Empty);
			}
		}

		/// <summary>
		/// Returns <c>true</c> if file upload is supported for the specified XMPP server, <c>false</c> otherwise.
		/// </summary>
		public bool FileUploadIsSupported
		{
			get => !string.IsNullOrWhiteSpace(this.HttpFileUploadJid) && this.HttpFileUploadMaxSize > 0;
		}

		/// <summary>
		/// The user's password.
		/// </summary>
		public string LocalPassword
		{
			set
			{
				this.LocalPasswordHash = this.ComputePasswordHash(value);
				this.IsNumericPassword = value.IsDigits();
			}
		}

		/// <summary>
		/// A hashed version of the user's <see cref="LocalPassword"/>.
		/// </summary>
		public string? LocalPasswordHash
		{
			get => this.localPasswordHash;
			private set
			{
				if (!string.Equals(this.localPasswordHash, value, StringComparison.Ordinal))
				{
					this.localPasswordHash = value;
					this.FlagAsDirty(nameof(this.LocalPasswordHash));
				}
			}
		}

		/// <summary>
		/// Indicates if the user has a <see cref="LocalPassword"/>.
		/// </summary>
		public bool HasLocalPassword
		{
			get => !string.IsNullOrEmpty(this.LocalPasswordHash);
		}

		/// <summary>
		/// Indicates if the password is numeric.
		/// </summary>
		public bool IsNumericPassword
		{
			get => this.isNumericPassword;
			private set
			{
				if (this.isNumericPassword != value)
				{
					this.isNumericPassword = value;
					this.FlagAsDirty(nameof(this.IsNumericPassword));
				}

			}
		}

		/// <summary>
		/// How the user authenticates itself with the App.
		/// </summary>
		public AuthenticationMethod AuthenticationMethod
		{
			get => this.authenticationMethod;
			set
			{
				if (this.authenticationMethod != value)
				{
					this.authenticationMethod = value;
					this.FlagAsDirty(nameof(this.AuthenticationMethod));
				}
			}
		}

		/// <summary>
		/// Returns <c>true</c> if the user choose the educational or experimental purpose.
		/// </summary>
		public bool IsTest
		{
			get => this.isTest;
			private set
			{
				if (this.isTest != value)
				{
					this.isTest = value;
					this.FlagAsDirty(nameof(this.IsTest));
				}
			}
		}

		/// <summary>
		/// Purpose for using the app.
		/// </summary>
		public PurposeUse Purpose
		{
			get => this.purpose;
			private set
			{
				if (this.purpose != value)
				{
					this.purpose = value;
					this.FlagAsDirty(nameof(this.Purpose));
				}
			}
		}

		/// <summary>
		/// Returns a timestamp if the user used a Test OTP Code.
		/// </summary>
		public DateTime? TestOtpTimestamp
		{
			get => this.testOtpTimestamp;
			set
			{
				if (this.testOtpTimestamp != value)
				{
					this.testOtpTimestamp = value;
					this.FlagAsDirty(nameof(this.TestOtpTimestamp));
				}
			}
		}

		/// <summary>
		/// The legal identity of the current user/profile.
		/// </summary>
		public LegalIdentity? LegalIdentity => this.legalIdentity;

		/// <summary>
		/// Sets the legal identity of the profile.
		/// </summary>
		/// <param name="Identity">Identity to set.</param>
		/// <param name="RemoveOldAttachments">If old attachments should be removed.</param>
		public async Task SetLegalIdentity(LegalIdentity? Identity, bool RemoveOldAttachments)
		{
			bool ScanIdUnlocked =
				this.legalIdentity is not null &&
				!this.legalIdentity.HasApprovedPersonalInformation() &&
				Identity is not null &&
				Identity.HasApprovedPersonalInformation();

			Attachment[]? OldAttachments = this.SetLegalIdentityInternal(Identity);

			this.LastIdentityUpdate = DateTime.UtcNow;

			if (RemoveOldAttachments)
				await ServiceRef.AttachmentCacheService.RemoveAttachments(OldAttachments);

			if (ScanIdUnlocked)
			{
				MainThread.BeginInvokeOnMainThread(() =>
				{
					ServiceRef.UiService.DisplayAlert(
						ServiceRef.Localizer[nameof(AppResources.Unlocked)],
						ServiceRef.Localizer[nameof(AppResources.YouCanNowScanIdCodes)]);
				});
			}
		}

		private Attachment[]? SetLegalIdentityInternal(LegalIdentity? Identity)
		{
			if (Equals(this.legalIdentity, Identity))
				return null;

			Attachment[]? OldAttachments = this.legalIdentity?.Attachments;
			bool RemoveAttachments = (this.legalIdentity is not null) && (Identity is null || this.legalIdentity.Id != Identity.Id);

			this.legalIdentity = Identity;
			this.FlagAsDirty(nameof(this.LegalIdentity));

			return RemoveAttachments ? OldAttachments : null;
		}

		/// <summary>
		/// Any current Identity application.
		/// </summary>
		public LegalIdentity? IdentityApplication => this.identityApplication;

		/// <summary>
		/// Sets the legal identity of the profile.
		/// </summary>
		/// <param name="Identity">Identity to set.</param>
		/// <param name="RemoveOldAttachments">If old attachments should be removed.</param>
		public async Task SetIdentityApplication(LegalIdentity? Identity, bool RemoveOldAttachments)
		{
			if (!Equals(this.identityApplication, Identity))
			{
				Attachment[]? OldAttachments = this.identityApplication?.Attachments;
				bool RemoveAttachments = (this.identityApplication is not null) && (Identity is null || this.identityApplication.Id != Identity.Id);

				this.identityApplication = Identity;
				this.FlagAsDirty(nameof(this.IdentityApplication));

				if (RemoveOldAttachments || Identity is null)
					this.NrReviews = 0;

				if (RemoveOldAttachments && RemoveAttachments)
					await ServiceRef.AttachmentCacheService.RemoveAttachments(OldAttachments);
			}
		}

		/// <summary>
		/// Number of peer reviews accepted for the current identity application.
		/// </summary>
		public int NrReviews
		{
			get => this.nrReviews;
			set
			{
				if (this.nrReviews != value)
				{
					this.nrReviews = value;
					this.FlagAsDirty(nameof(this.NrReviews));
				}
			}
		}

		/// <summary>
		/// Increments the number of peer reviews performed on the current identity application.
		/// </summary>
		public Task IncrementNrPeerReviews()
		{
			this.nrReviews++;
			this.FlagAsDirty(nameof(this.NrReviews));
			return Task.CompletedTask;
		}

		/// <summary>
		/// Currently selected theme.
		/// </summary>
		public AppTheme? Theme
		{
			get => this.theme;
			private set
			{
				if (!Equals(this.theme, value))
				{
					this.theme = value;
					this.FlagAsDirty(nameof(this.Theme));
				}
			}
		}

		/// <summary>
		/// Returns <c>true</c> if the current <see cref="ITagProfile"/> has changed values and need saving, <c>false</c> otherwise.
		/// </summary>
		public bool IsDirty { get; private set; }

		private void FlagAsDirty(string PropertyName)
		{
			if (!this.loadingProperties)
			{
				this.IsDirty = true;
				this.OnChanged(new PropertyChangedEventArgs(PropertyName));
			}
		}

		/// <summary>
		/// Resets the <see cref="IsDirty"/> flag, can be used after persisting values to <see cref="IStorageService"/>.
		/// </summary>
		public void ResetIsDirty()
		{
			this.IsDirty = false;

			try
			{
				this.OnPropertiesChanged.Raise(this, EventArgs.Empty);
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
			}
		}

		/// <summary>
		/// Event raised when properties have been changed.
		/// </summary>
		public event EventHandler? OnPropertiesChanged;

		#endregion

		#region Build Steps

		/// <summary>
		/// Sets the phone number used for contacting the user.
		/// </summary>
		/// <param name="Country">Country of the phone number.</param>
		/// <param name="PhoneNumber">Verified phone number.</param>
		public void SetPhone(string Country, string PhoneNumber)
		{
			this.SelectedCountry = Country;
			this.PhoneNumber = PhoneNumber;
		}

		/// <summary>
		/// Set the domain name to connect to.
		/// </summary>
		/// <param name="DomainName">The domain name.</param>
		/// <param name="DefaultXmppConnectivity">If connecting to the domain can be done using default parameters (host=domain, default c2s port).</param>
		/// <param name="Key">Key to use, if an account is to be created.</param>
		/// <param name="Secret">Secret to use, if an account is to be created.</param>
		public void SetDomain(string DomainName, bool DefaultXmppConnectivity, string Key, string Secret)
		{
			if (string.IsNullOrEmpty(this.InitialDomain))
			{
				this.InitialDefaultXmppConnectivity = this.DefaultXmppConnectivity;
				this.InitialDomain = this.Domain;
				this.InitialApiKey = this.ApiKey;
				this.InitialApiSecret = this.ApiSecret;
			}

			if (string.IsNullOrEmpty(this.InitialDomain))
			{
				this.InitialDefaultXmppConnectivity = DefaultXmppConnectivity;
				this.InitialDomain = DomainName;
				this.InitialApiKey = Key;
				this.InitialApiSecret = Secret;
			}

			this.DefaultXmppConnectivity = DefaultXmppConnectivity;
			this.Domain = DomainName;
			this.ApiKey = Key;
			this.ApiSecret = Secret;
		}

		/// <summary>
		/// Reverses the SetDomain to the Initial* values.
		/// </summary>
		public void UndoDomainSelection()
		{
			if (this.InitialDomain is not null)
			{
				this.DefaultXmppConnectivity = this.InitialDefaultXmppConnectivity;
				this.Domain = this.InitialDomain;
				this.ApiKey = this.InitialApiKey;
				this.ApiSecret = this.InitialApiSecret;
			}
		}

		/// <summary>
		/// Revert the SetDomain
		/// </summary>
		public void ClearDomain()
		{
			this.DefaultXmppConnectivity = false;
			this.Domain = string.Empty;
			this.ApiKey = string.Empty;
			this.ApiSecret = string.Empty;
		}

		/// <summary>
		/// Set the account name and password for a <em>new</em> account.
		/// </summary>
		/// <param name="AccountName">The account/user name.</param>
		/// <param name="ClientPasswordHash">The password hash (never send the real password).</param>
		/// <param name="ClientPasswordHashMethod">The hash method used when hashing the password.</param>
		public void SetAccount(string AccountName, string ClientPasswordHash, string ClientPasswordHashMethod)
		{
			this.XmppPasswordHash = ClientPasswordHash;
			this.XmppPasswordHashMethod = ClientPasswordHashMethod;
			this.ApiKey = string.Empty;
			this.ApiSecret = string.Empty;

			// It's important for this to be the last, since it will fire the account change notification.
			this.Account = AccountName;
		}

		/// <summary>
		/// Set the account name and password for an <em>existing</em> account.
		/// </summary>
		/// <param name="AccountName">The account/user name.</param>
		/// <param name="ClientPasswordHash">The password hash (never send the real password).</param>
		/// <param name="ClientPasswordHashMethod">The hash method used when hashing the password.</param>
		/// <param name="Identity">The new identity.</param>
		public async Task SetAccountAndLegalIdentity(string AccountName, string ClientPasswordHash, string ClientPasswordHashMethod, LegalIdentity Identity)
		{
			this.XmppPasswordHash = ClientPasswordHash;
			this.XmppPasswordHashMethod = ClientPasswordHashMethod;
			this.ApiKey = string.Empty;
			this.ApiSecret = string.Empty;

			await this.SetLegalIdentity(Identity, true);

			// It's important for this to be the last, since it will fire the account change notification.
			this.Account = AccountName;
		}

		/// <summary>
		/// Revert the Set Account
		/// </summary>
		public void ClearAccount()
		{
			this.XmppPasswordHash = string.Empty;
			this.XmppPasswordHashMethod = string.Empty;
			this.LegalJid = string.Empty;
			this.RegistryJid = null;
			this.ProvisioningJid = null;
			this.EDalerJid = null;
			this.NeuroFeaturesJid = null;
			this.SupportsPushNotification = false;

			// It's important for this to be the last, since it will fire the account change notification.
			this.Account = string.Empty;
		}

		/// <summary>
		/// Revert the Set LegalIdentity
		/// </summary>
		public Task ClearLegalIdentity()
		{
			return this.SetLegalIdentity(null, true);
		}

		/// <summary>
		/// Sets the current <see cref="LegalIdentity"/> to the revoked identity, and reverses the <see cref="Step"/> property.
		/// </summary>
		/// <param name="RevokedIdentity">The revoked identity to use.</param>
		public Task RevokeLegalIdentity(LegalIdentity RevokedIdentity)
		{
			return this.SetLegalIdentity(RevokedIdentity, true);
		}

		/// <summary>
		/// Sets the current <see cref="LegalIdentity"/> to the compromised identity, and reverses the <see cref="Step"/> property.
		/// </summary>
		/// <param name="CompromisedIdentity">The compromised identity to use.</param>
		public async Task CompromiseLegalIdentity(LegalIdentity CompromisedIdentity)
		{
			await this.SetLegalIdentity(CompromisedIdentity, true);
			await ServiceRef.XmppService.GenerateNewKeys();
		}

		/// <summary>
		/// Set if the user choose the educational or experimental purpose.
		/// </summary>
		/// <param name="IsTest">If app is in test mode.</param>
		/// <param name="Purpose">Purpose for using the app</param>
		public void SetPurpose(bool IsTest, PurposeUse Purpose)
		{
			this.IsTest = IsTest;
			this.Purpose = Purpose;
		}

		/// <summary>
		/// Used during XMPP service discovery. Sets the file upload parameters.
		/// </summary>
		/// <param name="HttpFileUploadJid">The http file upload id.</param>
		/// <param name="MaxSize">The max size allowed.</param>
		public void SetFileUploadParameters(string HttpFileUploadJid, long MaxSize)
		{
			this.HttpFileUploadJid = HttpFileUploadJid;
			this.HttpFileUploadMaxSize = MaxSize;
		}

		/// <summary>
		/// Sets the preferred theme.
		/// </summary>
		/// <param name="Theme">Theme</param>
		public void SetTheme(AppTheme Theme)
		{
			this.Theme = Theme;
			this.SetTheme();
		}

		/// <summary>
		/// Sets the preferred theme.
		/// </summary>
		public void SetTheme()
		{
			if (Application.Current is null || !this.Theme.HasValue)
				return;
			MainThread.BeginInvokeOnMainThread(() =>
			{
				try
				{
					Application.Current.UserAppTheme = this.Theme.Value;
				}
				catch (Exception)
				{
					return;
				}

			});
		}

		#endregion

		/// <summary>
		/// Computes a hash of the specified password.
		/// </summary>
		/// <param name="Password">The password whose hash to compute.</param>
		/// <returns>Hash Digest</returns>
		public string ComputePasswordHash(string Password)
		{
			StringBuilder sb = new();

			sb.Append(this.objectId);
			sb.Append(':');
			sb.Append(this.domain);
			sb.Append(':');
			sb.Append(this.account);
			sb.Append(':');
			sb.Append(this.legalJid);
			sb.Append(':');
			sb.Append(Password);

			string s = sb.ToString();
			byte[] Data = Encoding.UTF8.GetBytes(s);

			return Hashes.ComputeSHA384HashString(Data);
		}

		/// <summary>
		/// Clears the entire profile.
		/// </summary>
		public void ClearAll()
		{
			this.legalIdentity = null;
			this.identityApplication = null;
			this.domain = string.Empty;
			this.apiKey = string.Empty;
			this.apiSecret = string.Empty;
			this.selectedCountry = string.Empty;
			this.phoneNumber = string.Empty;
			this.eMail = string.Empty;
			this.account = string.Empty;
			this.xmppPasswordHash = string.Empty;
			this.xmppPasswordHashMethod = string.Empty;
			this.legalJid = string.Empty;
			this.registryJid = string.Empty;
			this.provisioningJid = string.Empty;
			this.httpFileUploadJid = string.Empty;
			this.logJid = string.Empty;
			this.eDalerJid = string.Empty;
			this.neuroFeaturesJid = string.Empty;
			this.localPasswordHash = string.Empty;
			this.httpFileUploadMaxSize = 0;
			this.isTest = false;
			this.TestOtpTimestamp = null;
			this.step = RegistrationStep.GetStarted;
			this.defaultXmppConnectivity = false;
			this.nrReviews = 0;
			this.supportsPushNotification = false;

			this.IsDirty = true;
		}

		/// <summary>
		/// Validates if the <paramref name="Password"/> is strong enough.
		/// </summary>
		/// <param name="Password">Password to validate.</param>
		/// <returns>A <see cref="PasswordStrength"/> value indicating if the <paramref name="Password"/> is strong enough.</returns>
		public PasswordStrength ValidatePasswordStrength(string? Password)
		{
			if (Password is null)
			{
				return PasswordStrength.NotEnoughDigitsLettersSigns;
			}

			Password = Password.Normalize();

			int DigitsCount = 0;
			int LettersCount = 0;
			int SignsCount = 0;

			Dictionary<int, int> DistinctSymbolsCount = [];

			int[] SlidingWindow = new int[Constants.Security.MaxPasswordSequencedSymbols + 1];
			SlidingWindow.Initialize();

			for (int i = 0; i < Password.Length;)
			{
				if (char.IsDigit(Password, i))
					DigitsCount++;
				else if (char.IsLetter(Password, i))
					LettersCount++;
				else
					SignsCount++;

				int Symbol = char.ConvertToUtf32(Password, i);

				if (DistinctSymbolsCount.TryGetValue(Symbol, out int SymbolCount))
				{
					DistinctSymbolsCount[Symbol] = ++SymbolCount;
					if (SymbolCount > Constants.Security.MaxPasswordIdenticalSymbols)
						return PasswordStrength.TooManyIdenticalSymbols;
				}
				else
					DistinctSymbolsCount.Add(Symbol, 1);

				for (int j = 0; j < SlidingWindow.Length - 1; j++)
					SlidingWindow[j] = SlidingWindow[j + 1];

				SlidingWindow[^1] = Symbol;

				int[] SlidingWindowDifferences = new int[SlidingWindow.Length - 1];
				for (int j = 0; j < SlidingWindow.Length - 1; j++)
					SlidingWindowDifferences[j] = SlidingWindow[j + 1] - SlidingWindow[j];

				if (SlidingWindowDifferences.All(difference => difference == 1))
					return PasswordStrength.TooManySequencedSymbols;

				if (char.IsSurrogate(Password, i))
					i += 2;
				else
					i += 1;
			}

			if (this.LegalIdentity is LegalIdentity LegalIdentity)
			{
				const StringComparison Comparison = StringComparison.CurrentCultureIgnoreCase;

				if (LegalIdentity[Constants.XmppProperties.PersonalNumber] is string PersonalNumber && PersonalNumber != "" && Password.Contains(PersonalNumber, Comparison))
					return PasswordStrength.ContainsPersonalNumber;

				if (LegalIdentity[Constants.XmppProperties.Phone] is string Phone && !string.IsNullOrEmpty(Phone) && Password.Contains(Phone, Comparison))
					return PasswordStrength.ContainsPhoneNumber;

				if (LegalIdentity[Constants.XmppProperties.EMail] is string EMail && !string.IsNullOrEmpty(EMail) && Password.Contains(EMail, Comparison))
					return PasswordStrength.ContainsEMail;

				IEnumerable<string> NameWords = new string[]
				{
					Constants.XmppProperties.FirstName,
					Constants.XmppProperties.MiddleNames,
					Constants.XmppProperties.LastNames,
				}
				.SelectMany(PropertyKey => LegalIdentity[PropertyKey] is string PropertyValue ? PropertyValueSplitRegex().Split(PropertyValue) : Enumerable.Empty<string>())
				.Where(Word => Word?.GetUnicodeLength() > 2);

				if (NameWords.Any(NameWord => Password.Contains(NameWord, Comparison)))
					return PasswordStrength.ContainsName;

				IEnumerable<string> AddressWords = new string[]
				{
					Constants.XmppProperties.Address,
					Constants.XmppProperties.Address2,
				}
				.SelectMany(PropertyKey => LegalIdentity[PropertyKey] is string PropertyValue ? PropertyValueSplitRegex().Split(PropertyValue) : Enumerable.Empty<string>())
				.Where(Word => Word?.GetUnicodeLength() > 2);

				if (AddressWords.Any(AddressWord => Password.Contains(AddressWord, Comparison)))
					return PasswordStrength.ContainsAddress;
			}

			const int MinDigitsCount = Constants.Security.MinPasswordSymbolsFromDifferentClasses;
			const int MinLettersCount = Constants.Security.MinPasswordSymbolsFromDifferentClasses;
			const int MinSignsCount = Constants.Security.MinPasswordSymbolsFromDifferentClasses;

			if (DigitsCount < MinDigitsCount && LettersCount < MinLettersCount && SignsCount < MinSignsCount)
				return PasswordStrength.NotEnoughDigitsLettersSigns;

			if (DigitsCount >= MinDigitsCount && LettersCount < MinLettersCount && SignsCount < MinSignsCount)
				return PasswordStrength.NotEnoughLettersOrSigns;

			if (DigitsCount < MinDigitsCount && LettersCount >= MinLettersCount && SignsCount < MinSignsCount)
				return PasswordStrength.NotEnoughDigitsOrSigns;

			if (DigitsCount < MinDigitsCount && LettersCount < MinLettersCount && SignsCount >= MinSignsCount)
				return PasswordStrength.NotEnoughLettersOrDigits;

			if (DigitsCount + LettersCount + SignsCount < Constants.Security.MinPasswordLength)
				return PasswordStrength.TooShort;

			return PasswordStrength.Strong;
		}


		/// <summary>
		/// Returns a score for the password, which is based on the number of bits of security the password provides.
		/// The higher the score, the more secure the password. It does not account for patterns, dictionary words, etc.
		/// </summary>
		/// <param name="Password">Password to check.</param>
		/// <returns>The password score</returns>
		public double CalculatePasswordScore(string? Password)
		{
			if (string.IsNullOrEmpty(Password))
				return 0;

			Password = Password.Normalize();

			int DigitsCount = 0;
			int LettersCount = 0;
			int SignsCount = 0;

			for (int i = 0; i < Password.Length;)
			{
				if (char.IsDigit(Password, i))
					DigitsCount++;
				else if (char.IsLetter(Password, i))
					LettersCount++;
				else
					SignsCount++;

				if (char.IsSurrogate(Password, i))
					i += 2;
				else
					i += 1;
			}

			double score = 0;
			if (DigitsCount > 0)
				score += Math.Log(10, 2) * DigitsCount;
			if (LettersCount > 0)
				score += Math.Log(50, 2) * LettersCount;
			if (SignsCount > 0)
				score += Math.Log(96, 2) * SignsCount;
			return score;
		}

		[GeneratedRegex(@"\p{Zs}+")]
		private static partial Regex PropertyValueSplitRegex();

		/// <summary>
		/// Checks if Tag Profile properties need to be changed, with regards to a current <see cref="ContractReference"/> object instance.
		/// </summary>
		/// <param name="Reference">Contract reference.</param>
		public void CheckContractReference(ContractReference Reference)
		{
			this.HasContractReferences = true;

			if (Reference.IsTemplate)
				this.HasContractTemplateReferences = true;

			if (Reference.IsTokenCreationTemplate)
				this.HasContractTokenCreationTemplatesReferences = true;
		}



	}

}

