using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeuroAccessMaui.Extensions;
using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services;
using NeuroAccessMaui.Services.Contacts;
using NeuroAccessMaui.Services.Data;
using NeuroAccessMaui.Services.UI.Photos;
using System.Collections.ObjectModel;
using Waher.Networking.XMPP;
using Waher.Networking.XMPP.Contracts;
using Waher.Persistence;

namespace NeuroAccessMaui.UI.Pages.Petitions.PetitionPeerReview
{
	/// <summary>
	/// The view model to bind to when displaying petitioning of an identity in a view or page.
	/// </summary>
	public partial class PetitionPeerReviewViewModel : QrXmppViewModel
	{
		private readonly PhotosLoader photosLoader;
		private readonly string? requestorFullJid;
		private readonly string? requestedIdentityId;
		private readonly string? petitionId;
		private readonly string? bareJid;

		/// <summary>
		/// Creates a new instance of the <see cref="PetitionPeerReviewViewModel"/> class.
		/// </summary>
		/// <param name="Args">Navigation arguments.</param>
		public PetitionPeerReviewViewModel(PetitionPeerReviewNavigationArgs? Args)
		{
			this.photosLoader = new PhotosLoader(this.Photos);

			if (Args is not null)
			{
				this.RequestorIdentity = Args.RequestorIdentity;
				this.requestorFullJid = Args.RequestorFullJid;
				this.requestedIdentityId = Args.RequestedIdentityId;
				this.petitionId = Args.PetitionId;
				this.Purpose = Args.Purpose;
				this.ContentToSign = Args.ContentToSign;

				if (!string.IsNullOrEmpty(this.requestorFullJid))
					this.bareJid = XmppClient.GetBareJID(this.requestorFullJid);
				else if (Args.RequestorIdentity is not null)
					this.bareJid = Args.RequestorIdentity.GetJid();
				else
					this.bareJid = string.Empty;
			}
		}

		/// <summary>
		/// Adds a view to the wizard dialog.
		/// </summary>
		public void AddView(ReviewStep Step, BaseContentView View)
		{
			View.BindingContext = this;
			this.stepViews[Step] = View;
		}

		/// <summary>
		/// Gets or sets the current step from the list of <see cref="stepViews"/>.
		/// </summary>
		[ObservableProperty]
		private ReviewStep currentStep = ReviewStep.Photo;

		/// <summary>
		/// The list of steps needed to register a digital identity.
		/// </summary>
		private readonly SortedDictionary<ReviewStep, BaseContentView> stepViews = [];

		/// <inheritdoc/>
		protected override async Task OnInitialize()
		{
			await base.OnInitialize();

			if (!string.IsNullOrEmpty(this.bareJid))
			{
				ContactInfo Info = await ContactInfo.FindByBareJid(this.bareJid);

				if (this.RequestorIdentity is not null &&
					Info is not null &&
					(Info.LegalIdentity is null || (
					Info.LegalId != this.RequestorIdentity?.Id &&
					Info.LegalIdentity is not null &&
					Info.LegalIdentity.Created < this.RequestorIdentity!.Created &&
					this.RequestorIdentity.State == IdentityState.Approved)))
				{
					Info.LegalId = this.LegalId;
					Info.LegalIdentity = this.RequestorIdentity;
					Info.FriendlyName = ContactInfo.GetFriendlyName(this.RequestorIdentity);

					await Database.Update(Info);
					await Database.Provider.Flush();
				}

				this.ThirdPartyInContacts = Info is not null;
			}

			this.AssignProperties();
			this.NotifyCommandsCanExecuteChanged();

			this.ReloadPhotos();
		}

		private async void ReloadPhotos()
		{
			try
			{
				this.photosLoader.CancelLoadPhotos();

				Attachment[]? Attachments = this.RequestorIdentity?.Attachments;
				Photo? First;

				if (Attachments is not null)
				{
					First = await this.photosLoader.LoadPhotos(Attachments, SignWith.LatestApprovedId);

					this.FirstPhotoSource = First?.Source;
					this.FirstPhotoRotation = First?.Rotation ?? 0;
				}

				Attachments = ServiceRef.TagProfile.LegalIdentity?.Attachments;

				if (Attachments is not null)
				{
					First = await this.photosLoader.LoadPhotos(Attachments, SignWith.LatestApprovedId);

					this.MyFirstPhotoSource = First?.Source;
					this.MyFirstPhotoRotation = First?.Rotation ?? 0;
				}
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
			}
		}

		/// <inheritdoc/>
		protected override async Task OnDispose()
		{
			this.photosLoader.CancelLoadPhotos();

			await base.OnDispose();
		}

		/// <summary>
		/// The list of photos related to the identity being petitioned.
		/// </summary>
		public ObservableCollection<Photo> Photos { get; } = [];

		/// <summary>
		/// The identity of the requestor.
		/// </summary>
		public LegalIdentity? RequestorIdentity { get; private set; }

		/// <inheritdoc/>
		protected override Task XmppService_ConnectionStateChanged(object? Sender, XmppState NewState)
		{
			return MainThread.InvokeOnMainThreadAsync(async () =>
			{
				await base.XmppService_ConnectionStateChanged(Sender, NewState);

				this.NotifyCommandsCanExecuteChanged();
			});
		}

		public override void SetIsBusy(bool IsBusy)
		{
			base.SetIsBusy(IsBusy);
			this.NotifyCommandsCanExecuteChanged();
		}

		private void NotifyCommandsCanExecuteChanged()
		{
			this.AcceptCommand.NotifyCanExecuteChanged();
			this.DeclineCommand.NotifyCanExecuteChanged();
			this.AuthenticateReviewerCommand.NotifyCanExecuteChanged();
		}

		/// <summary>
		/// If the user can accept the review.
		/// </summary>
		public bool CanAccept
		{
			get
			{
				if (!this.IsConnected || this.IsBusy || this.ContentToSign is null)
					return false;

				if (this.HasPhoto && !this.IsPhotoOk)
					return false;

				if (this.HasName && !this.IsNameOk)
					return false;

				if (this.HasPersonalNumber && !this.IsPnrOk)
					return false;

				if (this.HasNationality && !this.IsNationalityOk)
					return false;

				if (this.HasBirthDate && !this.IsBirthDateOk)
					return false;

				if (this.HasGender && !this.IsGenderOk)
					return false;

				if (this.HasPersonalAddressInfo && !this.IsPersonalAddressInfoOk)
					return false;

				if (this.HasOrganizationalInfo && !this.IsOrganizationalInfoOk)
					return false;

				if (!this.AcknowledgeResponsibility || !this.ConsentProcessing || !this.ConfirmCorrect)
					return false;

				return true;
			}
		}

		[RelayCommand(CanExecute = nameof(CanAccept))]
		private Task Accept()
		{
			return this.Accept(true);
		}

		private async Task Accept(bool GoBackIfOk)
		{
			if (this.ContentToSign is null || !await App.AuthenticateUserAsync(AuthenticationPurpose.AcceptPeerReview))
				return;

			bool Succeeded = await ServiceRef.NetworkService.TryRequest(async () =>
			{
				byte[] Signature = await ServiceRef.XmppService.Sign(this.ContentToSign!, SignWith.LatestApprovedId);

				await ServiceRef.XmppService.SendPetitionSignatureResponse(this.requestedIdentityId, this.ContentToSign, Signature,
					this.petitionId!, this.requestorFullJid!, true);
			});

			if (Succeeded && GoBackIfOk)
				await base.GoBack();
		}

		/// <summary>
		/// If the user can decline the review.
		/// </summary>
		public bool CanDecline => this.IsConnected && !this.IsBusy && this.ContentToSign is not null;

		[RelayCommand(CanExecute = nameof(CanDecline))]
		private Task Decline()
		{
			return this.Decline(true);
		}

		private async Task Decline(bool GoBackIfOk)
		{
			if (this.ContentToSign is null || !await App.AuthenticateUserAsync(AuthenticationPurpose.DeclinePeerReview))
				return;

			bool Succeeded = await ServiceRef.NetworkService.TryRequest(async () =>
			{
				byte[] Signature = await ServiceRef.XmppService.Sign(this.ContentToSign!, SignWith.LatestApprovedId);

				await ServiceRef.XmppService.SendPetitionSignatureResponse(this.requestedIdentityId, this.ContentToSign, Signature,
					this.petitionId!, this.requestorFullJid!, false);
			});

			if (Succeeded && GoBackIfOk)
				await base.GoBack();
		}

		[RelayCommand]
		private async Task Ignore()
		{
			await base.GoBack();
		}

		[RelayCommand(CanExecute = nameof(IsPhotoOk))]
		private void AcceptPhoto()
		{
			if (this.IsPhotoOk)
				this.NextPage();
		}

		[RelayCommand(CanExecute = nameof(IsNameOk))]
		private void AcceptName()
		{
			if (this.IsNameOk)
				this.NextPage();
		}

		[RelayCommand(CanExecute = nameof(IsPnrOk))]
		private void AcceptPnr()
		{
			if (this.IsPnrOk)
				this.NextPage();
		}

		[RelayCommand(CanExecute = nameof(IsNationalityOk))]
		private void AcceptNationality()
		{
			if (this.IsNationalityOk)
				this.NextPage();
		}

		[RelayCommand(CanExecute = nameof(IsBirthDateOk))]
		private void AcceptBirthDate()
		{
			if (this.IsBirthDateOk)
				this.NextPage();
		}

		[RelayCommand(CanExecute = nameof(IsGenderOk))]
		private void AcceptGender()
		{
			if (this.IsGenderOk)
				this.NextPage();
		}

		[RelayCommand(CanExecute = nameof(IsPersonalAddressInfoOk))]
		private void AcceptPersonalAddressInfo()
		{
			if (this.IsPersonalAddressInfoOk)
				this.NextPage();
		}

		[RelayCommand(CanExecute = nameof(IsOrganizationalInfoOk))]
		private void AcceptOrganizationalInfo()
		{
			if (this.IsOrganizationalInfoOk)
				this.NextPage();
		}

		[RelayCommand(CanExecute = nameof(IsConsentOk))]
		private void AcceptConsent()
		{
			if (this.IsConsentOk)
				this.NextPage();
		}

		private void NextPage()
		{
			ReviewStep Current = this.CurrentStep;
			bool IsVisible;

			do
			{
				Current++;
				IsVisible = Current switch
				{
					ReviewStep.Photo => this.HasPhoto,
					ReviewStep.Name => this.HasName,
					ReviewStep.Pnr => this.HasPersonalNumber,
					ReviewStep.Nationality => this.HasNationality,
					ReviewStep.BirthDate => this.HasBirthDate,
					ReviewStep.Gender => this.HasGender,
					ReviewStep.PersonalAddressInfo => this.HasPersonalAddressInfo,
					ReviewStep.OrganizationalInfo => this.HasOrganizationalInfo,
					_ => true,
				};
			}
			while (!IsVisible);

			this.CurrentStep = Current;
		}

		private bool PrevPage()
		{
			ReviewStep Current = this.CurrentStep;
			bool IsVisible;

			do
			{
				if (Current == 0)
					return false;

				Current--;
				IsVisible = Current switch
				{
					ReviewStep.Photo => this.HasPhoto,
					ReviewStep.Name => this.HasName,
					ReviewStep.Pnr => this.HasPersonalNumber,
					ReviewStep.Nationality => this.HasNationality,
					ReviewStep.BirthDate => this.HasBirthDate,
					ReviewStep.Gender => this.HasGender,
					ReviewStep.PersonalAddressInfo => this.HasPersonalAddressInfo,
					ReviewStep.OrganizationalInfo => this.HasOrganizationalInfo,
					_ => true,
				};
			}
			while (!IsVisible);

			this.CurrentStep = Current;

			return true;
		}

		/// <summary>
		/// If request contains a photo
		/// </summary>
		public bool HasPhoto => this.FirstPhotoSource is not null;

		/// <summary>
		/// If request contains a full name
		/// </summary>
		public bool HasName => !string.IsNullOrEmpty(this.FullName);

		/// <summary>
		/// If request contains a personal number
		/// </summary>
		public bool HasPersonalNumber => !string.IsNullOrEmpty(this.PersonalNumber);

		/// <summary>
		/// If request contains nationality
		/// </summary>
		public bool HasNationality => !string.IsNullOrEmpty(this.NationalityCode);

		/// <summary>
		/// If request contains birth date
		/// </summary>
		public bool HasBirthDate => this.BirthDate is not null;

		/// <summary>
		/// If request contains gender
		/// </summary>
		public bool HasGender => !string.IsNullOrEmpty(this.Gender);

		/// <summary>
		/// If request contains personal address information
		/// </summary>
		public bool HasPersonalAddressInfo
		{
			get
			{
				return
					!string.IsNullOrEmpty(this.Address) ||
					!string.IsNullOrEmpty(this.Address2) ||
					!string.IsNullOrEmpty(this.Area) ||
					!string.IsNullOrEmpty(this.City) ||
					!string.IsNullOrEmpty(this.Region) ||
					!string.IsNullOrEmpty(this.CountryCode);
			}
		}

		/// <summary>
		/// If request contains organizational information
		/// </summary>
		public bool HasOrganizationalInfo
		{
			get
			{
				return
					!string.IsNullOrEmpty(this.OrgName) ||
					!string.IsNullOrEmpty(this.OrgNumber) ||
					!string.IsNullOrEmpty(this.OrgDepartment) ||
					!string.IsNullOrEmpty(this.OrgRole) ||
					!string.IsNullOrEmpty(this.OrgAddress) ||
					!string.IsNullOrEmpty(this.OrgAddress2) ||
					!string.IsNullOrEmpty(this.OrgArea) ||
					!string.IsNullOrEmpty(this.OrgCity) ||
					!string.IsNullOrEmpty(this.OrgRegion) ||
					!string.IsNullOrEmpty(this.OrgCountryCode);
			}
		}

		#region Properties

		/// <summary>
		/// Created date of the identity
		/// </summary>
		[ObservableProperty]
		private DateTime created;

		/// <summary>
		/// Updated date of the identity
		/// </summary>
		[ObservableProperty]
		private DateTime? updated;

		/// <summary>
		/// Legal id of the identity
		/// </summary>
		[ObservableProperty]
		private string? legalId;

		/// <summary>
		/// Current state of the identity
		/// </summary>
		[ObservableProperty]
		private IdentityState state;

		/// <summary>
		/// From date (validity range) of the identity
		/// </summary>
		[ObservableProperty]
		private DateTime? from;

		/// <summary>
		/// To date (validity range) of the identity
		/// </summary>
		[ObservableProperty]
		private DateTime? to;

		/// <summary>
		/// First name of the identity
		/// </summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(FullName))]
		[NotifyPropertyChangedFor(nameof(PhotoReviewText))]
		[NotifyPropertyChangedFor(nameof(PeerReviewConfirmCorrectText))]
		[NotifyPropertyChangedFor(nameof(PeerReviewAuthenticationText))]
		[NotifyPropertyChangedFor(nameof(ReviewApproved))]
		private string? firstName;

		/// <summary>
		/// Middle name(s) of the identity
		/// </summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(FullName))]
		[NotifyPropertyChangedFor(nameof(PhotoReviewText))]
		[NotifyPropertyChangedFor(nameof(PeerReviewConfirmCorrectText))]
		[NotifyPropertyChangedFor(nameof(PeerReviewAuthenticationText))]
		[NotifyPropertyChangedFor(nameof(ReviewApproved))]
		private string? middleNames;

		/// <summary>
		/// Last name(s) of the identity
		/// </summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(FullName))]
		[NotifyPropertyChangedFor(nameof(PhotoReviewText))]
		[NotifyPropertyChangedFor(nameof(PeerReviewConfirmCorrectText))]
		[NotifyPropertyChangedFor(nameof(PeerReviewAuthenticationText))]
		[NotifyPropertyChangedFor(nameof(ReviewApproved))]
		private string? lastNames;

		/// <summary>
		/// Personal number of the identity
		/// </summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(PersonalNumberWithFlag))]
		private string? personalNumber;

		/// <summary>
		/// Personal number with country flag.
		/// </summary>
		public string? PersonalNumberWithFlag
		{
			get
			{
				if (string.IsNullOrEmpty(this.CountryCode) ||
					!ISO_3166_1.TryGetCountryByCode(this.CountryCode, out ISO_3166_Country? Country) ||
					Country is null)
				{
					return this.PersonalNumber;
				}

				return Country.EmojiInfo.Unicode + "\t" + this.PersonalNumber;
			}
		}

		/// <summary>
		/// Personal number of reviewer with country flag.
		/// </summary>
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static",
			Justification = "Must be instance property for view binding to work.")]
		public string? MyPersonalNumberWithFlag
		{
			get
			{
				if (ServiceRef.TagProfile.LegalIdentity is null)
					return null;

				string? MyCountryCode = ServiceRef.TagProfile.LegalIdentity[Constants.XmppProperties.Country];
				string? MyPersonalNumber = ServiceRef.TagProfile.LegalIdentity[Constants.XmppProperties.PersonalNumber];

				if (string.IsNullOrEmpty(MyCountryCode) ||
					!ISO_3166_1.TryGetCountryByCode(MyCountryCode, out ISO_3166_Country? MyCountry) ||
					MyCountry is null)
				{
					return MyPersonalNumber;
				}

				return MyCountry.EmojiInfo.Unicode + "\t" + MyPersonalNumber;
			}
		}

		/// <summary>
		/// Address of the identity
		/// </summary>
		[ObservableProperty]
		private string? address;

		/// <summary>
		/// Address (line 2) of the identity
		/// </summary>
		[ObservableProperty]
		private string? address2;

		/// <summary>
		/// Zip code of the identity
		/// </summary>
		[ObservableProperty]
		private string? zipCode;

		/// <summary>
		/// Area of the identity
		/// </summary>
		[ObservableProperty]
		private string? area;

		/// <summary>
		/// City of the identity
		/// </summary>
		[ObservableProperty]
		private string? city;

		/// <summary>
		/// Region of the identity
		/// </summary>
		[ObservableProperty]
		private string? region;

		/// <summary>
		/// Country code of the identity
		/// </summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(PersonalNumberWithFlag))]
		private string? countryCode;

		/// <summary>
		/// Nationality (ISO code)
		/// </summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(NationalityWithFlag))]
		private string? nationalityCode;

		/// <summary>
		/// Nationality with country flag.
		/// </summary>
		public string? NationalityWithFlag
		{
			get
			{
				if (string.IsNullOrEmpty(this.NationalityCode) ||
					!ISO_3166_1.TryGetCountryByCode(this.NationalityCode, out ISO_3166_Country? Country) ||
					Country is null)
				{
					return this.NationalityCode;
				}

				return Country.EmojiInfo.Unicode + "\t" + Country.Name;
			}
		}

		/// <summary>
		/// Gender
		/// </summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(GenderWithSymbol))]
		private string? gender;

		/// <summary>
		/// Gender with Unicode symbol.
		/// </summary>
		public string? GenderWithSymbol
		{
			get
			{
				if (string.IsNullOrEmpty(this.Gender) ||
					!ISO_5218.LetterToGender(this.Gender, out ISO_5218_Gender? Gender) ||
					Gender is null)
				{
					return this.Gender;
				}

				return Gender.Unicode + "\t" + ServiceRef.Localizer[Gender.LocalizedNameId];
			}
		}

		/// <summary>
		/// Birth Date
		/// </summary>
		[ObservableProperty]
		private DateTime? birthDate;

		/// <summary>
		/// The legal identity's organization name property
		/// </summary>
		[ObservableProperty]
		private string? orgName;

		/// <summary>
		/// The legal identity's organization number property
		/// </summary>
		[ObservableProperty]
		private string? orgNumber;

		/// <summary>
		/// The legal identity's organization department property
		/// </summary>
		[ObservableProperty]
		private string? orgDepartment;

		/// <summary>
		/// The legal identity's organization role property
		/// </summary>
		[ObservableProperty]
		private string? orgRole;

		/// <summary>
		/// The legal identity's organization address property
		/// </summary>
		[ObservableProperty]
		private string? orgAddress;

		/// <summary>
		/// The legal identity's organization address line 2 property
		/// </summary>
		[ObservableProperty]
		private string? orgAddress2;

		/// <summary>
		/// The legal identity's organization zip code property
		/// </summary>
		[ObservableProperty]
		private string? orgZipCode;

		/// <summary>
		/// The legal identity's organization area property
		/// </summary>
		[ObservableProperty]
		private string? orgArea;

		/// <summary>
		/// The legal identity's organization city property
		/// </summary>
		[ObservableProperty]
		private string? orgCity;

		/// <summary>
		/// The legal identity's organization region property
		/// </summary>
		[ObservableProperty]
		private string? orgRegion;

		/// <summary>
		/// The legal identity's organization country code property
		/// </summary>
		[ObservableProperty]
		private string? orgCountryCode;

		/// <summary>
		/// If organization information is available.
		/// </summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(OrgRowHeight))]
		private bool hasOrg;

		/// <summary>
		/// If organization information is available.
		/// </summary>
		public GridLength OrgRowHeight => this.HasOrg ? GridLength.Auto : new GridLength(0, GridUnitType.Absolute);

		/// <summary>
		/// PhoneNr of the identity
		/// </summary>
		[ObservableProperty]
		private string? phoneNr;

		/// <summary>
		/// EMail of the identity
		/// </summary>
		[ObservableProperty]
		private string? eMail;

		/// <summary>
		/// Device-ID of the identity
		/// </summary>
		[ObservableProperty]
		private string? deviceId;

		/// <summary>
		/// Is the contract approved?
		/// </summary>
		[ObservableProperty]
		private bool isApproved;

		/// <summary>
		/// What's the purpose of the petition?
		/// </summary>
		[ObservableProperty]
		private string? purpose;

		/// <summary>
		/// Content to sign.
		/// </summary>
		[ObservableProperty]
		private byte[]? contentToSign;

		/// <summary>
		/// Gets or sets whether the identity is in the contact list or not.
		/// </summary>
		[ObservableProperty]
		private bool thirdPartyInContacts;

		/// <summary>
		/// Image source of the first photo in the identity of the requestor.
		/// </summary>
		[ObservableProperty]
		private ImageSource? firstPhotoSource;

		/// <summary>
		/// Rotation of the first photo in the identity of the requestor.
		/// </summary>
		[ObservableProperty]
		private int firstPhotoRotation;

		/// <summary>
		/// Image source of the first photo in the identity of the reviewer.
		/// </summary>
		[ObservableProperty]
		private ImageSource? myFirstPhotoSource;

		/// <summary>
		/// Rotation of the first photo in the identity of the reviewer.
		/// </summary>
		[ObservableProperty]
		private int myFirstPhotoRotation;

		// Full name of requesting entity.
		public string FullName => ContactInfo.GetFullName(this.FirstName, this.MiddleNames, this.LastNames);

		// Full name of reviewer.
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static",
			Justification = "Must be instance property for view binding to work.")]
		public string MyFullName => ContactInfo.GetFullName(ServiceRef.TagProfile.LegalIdentity);

		/// <summary>
		/// If photo is OK
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(AcceptPhotoCommand))]
		private bool isPhotoOk;

		/// <summary>
		/// If name is OK
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(AcceptNameCommand))]
		private bool isNameOk;

		/// <summary>
		/// If personal number is OK
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(AcceptPnrCommand))]
		private bool isPnrOk;

		/// <summary>
		/// If nationality is OK
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(AcceptNationalityCommand))]
		private bool isNationalityOk;

		/// <summary>
		/// If birth date is OK
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(AcceptBirthDateCommand))]
		private bool isBirthDateOk;

		/// <summary>
		/// If gender is OK
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(AcceptGenderCommand))]
		private bool isGenderOk;

		/// <summary>
		/// If personal address information is OK
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(AcceptPersonalAddressInfoCommand))]
		private bool isPersonalAddressInfoOk;

		/// <summary>
		/// If organizational information is OK
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(AcceptOrganizationalInfoCommand))]
		private bool isOrganizationalInfoOk;

		/// <summary>
		/// If reviewer acknowledges responsibility for review
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(AcceptConsentCommand))]
		[NotifyPropertyChangedFor(nameof(IsConsentOk))]
		private bool acknowledgeResponsibility;

		/// <summary>
		/// If reviewer gives consent for processing
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(AcceptConsentCommand))]
		[NotifyPropertyChangedFor(nameof(IsConsentOk))]
		private bool consentProcessing;

		/// <summary>
		/// If reviewer confirms information given is correct.
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(AcceptConsentCommand))]
		[NotifyPropertyChangedFor(nameof(IsConsentOk))]
		private bool confirmCorrect;

		/// <summary>
		/// If details should be displayed.
		/// </summary>
		[ObservableProperty]
		private bool showDetails;

		/// <summary>
		/// If proper consent and acknowledgement has been given.
		/// </summary>
		public bool IsConsentOk => this.AcknowledgeResponsibility && this.ConsentProcessing && this.ConfirmCorrect;

		/// <summary>
		/// Instruction to reviewer when reviewing photo.
		/// </summary>
		public string PhotoReviewText => ServiceRef.Localizer[nameof(AppResources.PeerReviewPhotoText), this.FullName];

		/// <summary>
		/// Instruction to reviewer when confirming reviewed information is correct.
		/// </summary>
		public string PeerReviewConfirmCorrectText => ServiceRef.Localizer[nameof(AppResources.PeerReviewConfirmCorrectText), this.FullName];

		/// <summary>
		/// Instruction to reviewer when accepting peer review.
		/// </summary>
		public string PeerReviewAuthenticationText => ServiceRef.Localizer[nameof(AppResources.PeerReviewAuthenticationText), this.FullName];

		/// <summary>
		/// Information that peer review has been approved.
		/// </summary>
		public string ReviewApproved => ServiceRef.Localizer[nameof(AppResources.ReviewApproved), this.FullName];

		/// <summary>
		/// Toggles <see cref="IsPhotoOk"/>
		/// </summary>
		[RelayCommand]
		public void ToggleIsPhotoOk()
		{
			this.IsPhotoOk = !this.IsPhotoOk;
		}

		/// <summary>
		/// Toggles <see cref="IsNameOk"/>
		/// </summary>
		[RelayCommand]
		public void ToggleIsNameOk()
		{
			this.IsNameOk = !this.IsNameOk;
		}

		/// <summary>
		/// Toggles <see cref="IsPnrOk"/>
		/// </summary>
		[RelayCommand]
		public void ToggleIsPnrOk()
		{
			this.IsPnrOk = !this.IsPnrOk;
		}

		/// <summary>
		/// Toggles <see cref="IsNationalityOk"/>
		/// </summary>
		[RelayCommand]
		public void ToggleIsNationalityOk()
		{
			this.IsNationalityOk = !this.IsNationalityOk;
		}

		/// <summary>
		/// Toggles <see cref="IsBirthDateOk"/>
		/// </summary>
		[RelayCommand]
		public void ToggleIsBirthDateOk()
		{
			this.IsBirthDateOk = !this.IsBirthDateOk;
		}

		/// <summary>
		/// Toggles <see cref="IsGenderOk"/>
		/// </summary>
		[RelayCommand]
		public void ToggleIsGenderOk()
		{
			this.IsGenderOk = !this.IsGenderOk;
		}

		/// <summary>
		/// Toggles <see cref="IsPersonalAddressInfoOk"/>
		/// </summary>
		[RelayCommand]
		public void ToggleIsPersonalAddressInfoOk()
		{
			this.IsPersonalAddressInfoOk = !this.IsPersonalAddressInfoOk;
		}

		/// <summary>
		/// Toggles <see cref="IsOrganizationalInfoOk"/>
		/// </summary>
		[RelayCommand]
		public void ToggleIsOrganizationalInfoOk()
		{
			this.IsOrganizationalInfoOk = !this.IsOrganizationalInfoOk;
		}

		/// <summary>
		/// Toggles <see cref="AcknowledgeResponsibility"/>
		/// </summary>
		[RelayCommand]
		public void ToggleAcknowledgeResponsibility()
		{
			this.AcknowledgeResponsibility = !this.AcknowledgeResponsibility;
		}

		/// <summary>
		/// Toggles <see cref="ConsentProcessing"/>
		/// </summary>
		[RelayCommand]
		public void ToggleConsentProcessing()
		{
			this.ConsentProcessing = !this.ConsentProcessing;
		}

		/// <summary>
		/// Toggles <see cref="ConfirmCorrect"/>
		/// </summary>
		[RelayCommand]
		public void ToggleConfirmCorrect()
		{
			this.ConfirmCorrect = !this.ConfirmCorrect;
		}

		/// <summary>
		/// Toggles <see cref="ShowDetails"/>
		/// </summary>
		[RelayCommand]
		public void ToggleShowDetails()
		{
			this.ShowDetails = !this.ShowDetails;
		}

		#endregion

		private void AssignProperties()
		{
			if (this.RequestorIdentity is not null)
			{
				this.Created = this.RequestorIdentity.Created;
				this.Updated = this.RequestorIdentity.Updated.GetDateOrNullIfMinValue();
				this.LegalId = this.RequestorIdentity.Id;
				this.State = this.RequestorIdentity.State;
				this.From = this.RequestorIdentity.From.GetDateOrNullIfMinValue();
				this.To = this.RequestorIdentity.To.GetDateOrNullIfMinValue();
				this.FirstName = this.RequestorIdentity[Constants.XmppProperties.FirstName];
				this.MiddleNames = this.RequestorIdentity[Constants.XmppProperties.MiddleNames];
				this.LastNames = this.RequestorIdentity[Constants.XmppProperties.LastNames];
				this.PersonalNumber = this.RequestorIdentity[Constants.XmppProperties.PersonalNumber];
				this.Address = this.RequestorIdentity[Constants.XmppProperties.Address];
				this.Address2 = this.RequestorIdentity[Constants.XmppProperties.Address2];
				this.ZipCode = this.RequestorIdentity[Constants.XmppProperties.ZipCode];
				this.Area = this.RequestorIdentity[Constants.XmppProperties.Area];
				this.City = this.RequestorIdentity[Constants.XmppProperties.City];
				this.Region = this.RequestorIdentity[Constants.XmppProperties.Region];
				this.CountryCode = this.RequestorIdentity[Constants.XmppProperties.Country];
				this.NationalityCode = this.RequestorIdentity[Constants.XmppProperties.Nationality];
				this.Gender = this.RequestorIdentity[Constants.XmppProperties.Gender];

				string BirthDayStr = this.RequestorIdentity[Constants.XmppProperties.BirthDay];
				string BirthMonthStr = this.RequestorIdentity[Constants.XmppProperties.BirthMonth];
				string BirthYearStr = this.RequestorIdentity[Constants.XmppProperties.BirthYear];

				if (!string.IsNullOrEmpty(BirthDayStr) && int.TryParse(BirthDayStr, out int BirthDay) &&
					!string.IsNullOrEmpty(BirthMonthStr) && int.TryParse(BirthMonthStr, out int BirthMonth) &&
					!string.IsNullOrEmpty(BirthYearStr) && int.TryParse(BirthYearStr, out int BirthYear))
				{
					try
					{
						this.BirthDate = new DateTime(BirthYear, BirthMonth, BirthDay);
					}
					catch (Exception ex)
					{
						ServiceRef.LogService.LogException(ex);
						this.BirthDate = null;
					}
				}

				this.OrgName = this.RequestorIdentity[Constants.XmppProperties.OrgName];
				this.OrgNumber = this.RequestorIdentity[Constants.XmppProperties.OrgNumber];
				this.OrgDepartment = this.RequestorIdentity[Constants.XmppProperties.OrgDepartment];
				this.OrgRole = this.RequestorIdentity[Constants.XmppProperties.OrgRole];
				this.OrgAddress = this.RequestorIdentity[Constants.XmppProperties.OrgAddress];
				this.OrgAddress2 = this.RequestorIdentity[Constants.XmppProperties.OrgAddress2];
				this.OrgZipCode = this.RequestorIdentity[Constants.XmppProperties.OrgZipCode];
				this.OrgArea = this.RequestorIdentity[Constants.XmppProperties.OrgArea];
				this.OrgCity = this.RequestorIdentity[Constants.XmppProperties.OrgCity];
				this.OrgRegion = this.RequestorIdentity[Constants.XmppProperties.OrgRegion];
				this.OrgCountryCode = this.RequestorIdentity[Constants.XmppProperties.OrgCountry];
				this.HasOrg =
					!string.IsNullOrEmpty(this.OrgName) ||
					!string.IsNullOrEmpty(this.OrgNumber) ||
					!string.IsNullOrEmpty(this.OrgDepartment) ||
					!string.IsNullOrEmpty(this.OrgRole) ||
					!string.IsNullOrEmpty(this.OrgAddress) ||
					!string.IsNullOrEmpty(this.OrgAddress2) ||
					!string.IsNullOrEmpty(this.OrgZipCode) ||
					!string.IsNullOrEmpty(this.OrgArea) ||
					!string.IsNullOrEmpty(this.OrgCity) ||
					!string.IsNullOrEmpty(this.OrgRegion) ||
					!string.IsNullOrEmpty(this.OrgCountryCode);
				this.PhoneNr = this.RequestorIdentity[Constants.XmppProperties.Phone];
				this.EMail = this.RequestorIdentity[Constants.XmppProperties.EMail];
				this.DeviceId = this.RequestorIdentity[Constants.XmppProperties.DeviceId];
				this.IsApproved = this.RequestorIdentity.State == IdentityState.Approved;
			}
			else
			{
				this.Created = DateTime.MinValue;
				this.Updated = null;
				this.LegalId = Constants.NotAvailableValue;
				this.State = IdentityState.Compromised;
				this.From = null;
				this.To = null;
				this.FirstName = Constants.NotAvailableValue;
				this.MiddleNames = Constants.NotAvailableValue;
				this.LastNames = Constants.NotAvailableValue;
				this.PersonalNumber = Constants.NotAvailableValue;
				this.Address = Constants.NotAvailableValue;
				this.Address2 = Constants.NotAvailableValue;
				this.ZipCode = Constants.NotAvailableValue;
				this.Area = Constants.NotAvailableValue;
				this.City = Constants.NotAvailableValue;
				this.Region = Constants.NotAvailableValue;
				this.CountryCode = Constants.NotAvailableValue;
				this.NationalityCode = Constants.NotAvailableValue;
				this.Gender = Constants.NotAvailableValue;
				this.BirthDate = null;
				this.OrgName = Constants.NotAvailableValue;
				this.OrgNumber = Constants.NotAvailableValue;
				this.OrgDepartment = Constants.NotAvailableValue;
				this.OrgRole = Constants.NotAvailableValue;
				this.OrgAddress = Constants.NotAvailableValue;
				this.OrgAddress2 = Constants.NotAvailableValue;
				this.OrgZipCode = Constants.NotAvailableValue;
				this.OrgArea = Constants.NotAvailableValue;
				this.OrgCity = Constants.NotAvailableValue;
				this.OrgRegion = Constants.NotAvailableValue;
				this.OrgCountryCode = Constants.NotAvailableValue;
				this.HasOrg = false;
				this.PhoneNr = Constants.NotAvailableValue;
				this.EMail = Constants.NotAvailableValue;
				this.DeviceId = Constants.NotAvailableValue;
				this.IsApproved = false;
			}
		}

		[RelayCommand(CanExecute = nameof(CanAccept))]
		private async Task AuthenticateReviewer()
		{
			try
			{
				if (await App.AuthenticateUserAsync(AuthenticationPurpose.AuthenticateReviewer, true))
				{
					await this.Accept(false);
					this.NextPage();
				}
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);

				await ServiceRef.UiService.DisplayAlert(
					ServiceRef.Localizer[nameof(AppResources.ErrorTitle)],
					ServiceRef.Localizer[nameof(AppResources.UnableToSignReview)]);
			}
		}

		/// <inheritdoc/>
		public override async Task GoBack()
		{
			if (!this.PrevPage())
				await base.GoBack();
		}

		/// <summary>
		/// Closes the view.
		/// </summary>
		[RelayCommand]
		public Task Close()
		{
			return base.GoBack();
		}

		#region ILinkableView

		/// <summary>
		/// Title of the current view
		/// </summary>
		public override Task<string> Title => Task.FromResult(ContactInfo.GetFriendlyName(this.RequestorIdentity!));

		#endregion
	}
}
