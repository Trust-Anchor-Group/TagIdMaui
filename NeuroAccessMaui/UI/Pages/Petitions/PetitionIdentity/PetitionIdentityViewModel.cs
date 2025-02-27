using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeuroAccessMaui.Extensions;
using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services;
using NeuroAccessMaui.Services.Contacts;
using NeuroAccessMaui.Services.UI.Photos;
using System.Collections.ObjectModel;
using Waher.Networking.XMPP;
using Waher.Networking.XMPP.Contracts;
using Waher.Persistence;

namespace NeuroAccessMaui.UI.Pages.Petitions.PetitionIdentity
{
	/// <summary>
	/// The view model to bind to when displaying petitioning of an identity in a view or page.
	/// </summary>
	public partial class PetitionIdentityViewModel : BaseViewModel
	{
		private readonly PhotosLoader photosLoader;
		private readonly string? requestorFullJid;
		private readonly string? requestedIdentityId;
		private readonly string? petitionId;
		private readonly string? bareJid;

		/// <summary>
		/// Creates a new instance of the <see cref="PetitionIdentityViewModel"/> class.
		/// </summary>
		/// <param name="Args">Navigation arguments.</param>
		public PetitionIdentityViewModel(PetitionIdentityNavigationArgs? Args)
		{
			this.photosLoader = new PhotosLoader(this.Photos);

			if (Args is not null)
			{
				this.RequestorIdentity = Args.RequestorIdentity;
				this.requestorFullJid = Args.RequestorFullJid;
				this.requestedIdentityId = Args.RequestedIdentityId;
				this.petitionId = Args.PetitionId;
				this.Purpose = Args.Purpose;

				if (!string.IsNullOrEmpty(this.requestorFullJid))
					this.bareJid = XmppClient.GetBareJID(this.requestorFullJid);
				else if (Args.RequestorIdentity is not null)
					this.bareJid = Args.RequestorIdentity.GetJid();
				else
					this.bareJid = string.Empty;
			}
		}

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
			EvaluateAllCommands();

			this.ReloadPhotos();
		}

		private async void ReloadPhotos()
		{
			try
			{
				this.photosLoader.CancelLoadPhotos();

				Attachment[]? Attachments = this.RequestorIdentity?.Attachments;

				if (Attachments is not null)
				{
					Photo? First = await this.photosLoader.LoadPhotos(Attachments, SignWith.LatestApprovedId);

					this.FirstPhotoSource = First?.Source;
					this.FirstPhotoRotation = First?.Rotation ?? 0;
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

		private static void EvaluateAllCommands()
		{
		}

		/// <summary>
		/// The list of photos related to the identity being petitioned.
		/// </summary>
		public ObservableCollection<Photo> Photos { get; } = [];

		/// <summary>
		/// The identity of the requestor.
		/// </summary>
		public LegalIdentity? RequestorIdentity { get; private set; }

		[RelayCommand]
		private async Task Accept()
		{
			if (!await App.AuthenticateUserAsync(AuthenticationPurpose.PetitionIdentity))
				return;

			bool Succeeded = await ServiceRef.NetworkService.TryRequest(() =>
			{
				return ServiceRef.XmppService.SendPetitionIdentityResponse(this.requestedIdentityId, this.petitionId!,
					this.requestorFullJid!, true);
			});

			if (Succeeded)
				await ServiceRef.UiService.GoBackAsync();
		}

		[RelayCommand]
		private async Task Decline()
		{
			bool Succeeded = await ServiceRef.NetworkService.TryRequest(() =>
			{
				return ServiceRef.XmppService.SendPetitionIdentityResponse(this.requestedIdentityId, this.petitionId!,
					this.requestorFullJid!, false);
			});

			if (Succeeded)
				await this.GoBack();
		}

		[RelayCommand]
		private async Task Ignore()
		{
			await this.GoBack();
		}

		// Full name of requesting entity.
		public string FullName => ContactInfo.GetFullName(this.FirstName, this.MiddleNames, this.LastNames);

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
		/// When the identity expires
		/// </summary>
		[ObservableProperty]
		private DateTime? expires;

		/// <summary>
		/// Legal id of the identity
		/// </summary>
		[ObservableProperty]
		private string? legalId;

		/// <summary>
		/// Network ID
		/// </summary>
		[ObservableProperty]
		private string? networkId;

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
		private string? firstName;

		/// <summary>
		/// Middle name(s) of the identity
		/// </summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(FullName))]
		private string? middleNames;

		/// <summary>
		/// Last name(s) of the identity
		/// </summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(FullName))]
		private string? lastNames;

		/// <summary>
		/// Personal number of the identity
		/// </summary>
		[ObservableProperty]
		private string? personalNumber;

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
		private string? countryCode;

		/// <summary>
		/// Nationality (ISO code)
		/// </summary>
		[ObservableProperty]
		private string? nationalityCode;

		/// <summary>
		/// Gender
		/// </summary>
		[ObservableProperty]
		private string? gender;

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
		private bool hasOrg;

		/// <summary>
		/// If photos are available.
		/// </summary>
		[ObservableProperty]
		private bool hasPhotos;

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
		/// Gets or sets whether the identity is in the contact list or not.
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(AddContactCommand))]
		[NotifyCanExecuteChangedFor(nameof(RemoveContactCommand))]
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

		#endregion

		private void AssignProperties()
		{
			if (this.RequestorIdentity is not null)
			{
				this.Created = this.RequestorIdentity.Created;
				this.Updated = this.RequestorIdentity.Updated.GetDateOrNullIfMinValue();
				this.Expires = this.RequestorIdentity.To.GetDateOrNullIfMinValue();
				this.LegalId = this.RequestorIdentity.Id;
				this.NetworkId = this.RequestorIdentity.GetJid();
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
				this.HasPhotos = this.Photos.Count > 0;
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
				this.HasPhotos = false;
				this.PhoneNr = Constants.NotAvailableValue;
				this.EMail = Constants.NotAvailableValue;
				this.DeviceId = Constants.NotAvailableValue;
				this.IsApproved = false;
			}
		}

		/// <summary>
		/// Copies Item to clipboard
		/// </summary>
		[RelayCommand]
		private async Task Copy(object Item)
		{
			try
			{
				if (Item is string Label)
				{
					if (Label == this.LegalId)
					{
						await Clipboard.SetTextAsync(Constants.UriSchemes.IotId + ":" + this.LegalId);
						await ServiceRef.UiService.DisplayAlert(
							ServiceRef.Localizer[nameof(AppResources.SuccessTitle)],
							ServiceRef.Localizer[nameof(AppResources.IdCopiedSuccessfully)]);
					}
					else
					{
						await Clipboard.SetTextAsync(Label);
						await ServiceRef.UiService.DisplayAlert(
							ServiceRef.Localizer[nameof(AppResources.SuccessTitle)],
							ServiceRef.Localizer[nameof(AppResources.TagValueCopiedToClipboard)]);
					}
				}
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				await ServiceRef.UiService.DisplayException(ex);
			}
		}

		/// <summary>
		/// If <see cref="AddContactCommand"/> can be executed.
		/// </summary>
		public bool CanAddContact => !this.ThirdPartyInContacts;

		/// <summary>
		/// Adds the remote identity from the list of contacgs.
		/// </summary>
		[RelayCommand(CanExecute = nameof(CanAddContact))]
		private async Task AddContact()
		{
			if (this.RequestorIdentity is null)
				return;

			try
			{
				string FriendlyName = ContactInfo.GetFriendlyName(this.RequestorIdentity);
				string BareJid = XmppClient.GetBareJID(this.requestorFullJid);

				RosterItem? Item = ServiceRef.XmppService.GetRosterItem(BareJid);
				if (Item is null)
					ServiceRef.XmppService.AddRosterItem(new RosterItem(BareJid, FriendlyName));

				ContactInfo Info = await ContactInfo.FindByBareJid(BareJid);
				if (Info is null)
				{
					Info = new ContactInfo()
					{
						BareJid = BareJid,
						LegalId = this.RequestorIdentity.Id,
						LegalIdentity = this.RequestorIdentity,
						FriendlyName = FriendlyName,
						IsThing = false
					};

					await Database.Insert(Info);
				}
				else
				{
					Info.LegalId = this.requestedIdentityId;
					Info.LegalIdentity = this.RequestorIdentity;
					Info.FriendlyName = FriendlyName;

					await Database.Update(Info);
				}

				await ServiceRef.AttachmentCacheService.MakePermanent(this.LegalId!);
				await Database.Provider.Flush();

				this.ThirdPartyInContacts = true;
			}
			catch (Exception ex)
			{
				await ServiceRef.UiService.DisplayException(ex);
			}
		}

		/// <summary>
		/// If <see cref="RemoveContactCommand"/> can be executed.
		/// </summary>
		public bool CanRemoveContact => this.ThirdPartyInContacts;

		/// <summary>
		/// Removes the remote identity from the list of contacgs.
		/// </summary>
		[RelayCommand(CanExecute = nameof(CanRemoveContact))]
		private async Task RemoveContact()
		{
			try
			{
				if (!await ServiceRef.UiService.DisplayAlert(ServiceRef.Localizer[nameof(AppResources.Confirm)],
					ServiceRef.Localizer[nameof(AppResources.AreYouSureYouWantToRemoveContact)],
					ServiceRef.Localizer[nameof(AppResources.Yes)], ServiceRef.Localizer[nameof(AppResources.Cancel)]))
				{
					return;
				}

				string BareJid = XmppClient.GetBareJID(this.requestorFullJid);

				ContactInfo Info = await ContactInfo.FindByBareJid(BareJid);
				if (Info is not null)
				{
					await Database.Delete(Info);
					await ServiceRef.AttachmentCacheService.MakeTemporary(Info.LegalId);
					await Database.Provider.Flush();
				}

				RosterItem? Item = ServiceRef.XmppService.GetRosterItem(BareJid);
				if (Item is not null)
					ServiceRef.XmppService.RemoveRosterItem(BareJid);

				this.ThirdPartyInContacts = false;
			}
			catch (Exception ex)
			{
				await ServiceRef.UiService.DisplayException(ex);
			}
		}
	}
}
