using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeuroAccessMaui.Extensions;
using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services;
using NeuroAccessMaui.Services.Data;
using NeuroAccessMaui.Services.Data.PersonalNumbers;
using NeuroAccessMaui.Services.UI;
using NeuroAccessMaui.Services.UI.Photos;
using NeuroAccessMaui.UI.Pages.Identity.ViewIdentity;
using NeuroAccessMaui.UI.Pages.Registration;
using NeuroAccessMaui.UI.Pages.Utility.Images;
using NeuroAccessMaui.UI.Pages.Wallet.ServiceProviders;
using SkiaSharp;
using Waher.Content;
using Waher.Content.Html.Elements;
using Waher.Networking.XMPP;
using Waher.Networking.XMPP.Contracts;
using Waher.Networking.XMPP.StanzaErrors;
using IServiceProvider = Waher.Networking.XMPP.Contracts.IServiceProvider;

namespace NeuroAccessMaui.UI.Pages.Applications.ApplyId
{
	/// <summary>
	/// The view model to bind to for when displaying the an application for a Personal ID.
	/// </summary>
	public partial class ApplyIdViewModel : RegisterIdentityModel
	{
		private const string profilePhotoFileName = "ProfilePhoto.jpg";
		private readonly string localPhotoFileName;
		private readonly PhotosLoader photosLoader;
		private LegalIdentityAttachment? photo;
		private ServiceProviderWithLegalId[]? peerReviewServices = null;

		/// <summary>
		/// Creates an instance of the <see cref="ApplyIdViewModel"/> class.
		/// </summary>
		public ApplyIdViewModel()
			: base()
		{
			this.localPhotoFileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), profilePhotoFileName);
			this.photosLoader = new PhotosLoader();
			this.countries = new ObservableCollection<ISO_3166_Country>(ISO_3166_1.Countries);
			this.genders = new ObservableCollection<ISO_5218_Gender>(ISO_5218.Genders);
		}

		protected override async Task OnInitialize()
		{
			this.ApplicationId = null;

			if (ServiceRef.TagProfile.IdentityApplication is not null)
			{
				if (ServiceRef.TagProfile.IdentityApplication.IsDiscarded())
					await ServiceRef.TagProfile.SetIdentityApplication(null, true);
			}

			LegalIdentity? IdentityReference;

			if (ServiceRef.TagProfile.IdentityApplication is not null)
			{
				IdentityReference = ServiceRef.TagProfile.IdentityApplication;
				this.ApplicationSent = true;
				this.ApplicationId = IdentityReference.Id;
				this.NrReviews = ServiceRef.TagProfile.NrReviews;

				await Task.Run(this.LoadFeaturedPeerReviewers);
			}
			else
			{
				this.ApplicationSent = false;
				IdentityReference = ServiceRef.TagProfile.LegalIdentity;
				this.NrReviews = 0;
				this.peerReviewServices = null;
				this.HasFeaturedPeerReviewers = false;
			}

			ApplyIdNavigationArgs? Args = ServiceRef.UiService.PopLatestArgs<ApplyIdNavigationArgs>();

			if (Args is not null)
			{
				this.Personal = Args.Personal;
				this.Organizational = Args.Organizational;
			}
			else if (IdentityReference is not null)
			{
				this.Organizational = IdentityReference.IsOrganizational();
				this.Personal = !this.Organizational;
			}

			if (IdentityReference is not null)
			{
				await this.SetProperties(IdentityReference, Args?.ReusePhoto ?? true, true, true, this.Organizational, true);

				if (string.IsNullOrEmpty(this.OrgCountryCode) && this.Organizational)
					this.OrgCountryCode = this.CountryCode;

				if (string.IsNullOrEmpty(this.NationalityCode) && this.RequiresNationality)
					this.NationalityCode = this.CountryCode;
			}

			this.RequiresOrgName = this.Organizational;
			this.RequiresOrgDepartment = this.Organizational;
			this.RequiresOrgRole = this.Organizational;
			this.RequiresOrgNumber = this.Organizational;

			ServiceRef.XmppService.IdentityApplicationChanged += this.XmppService_IdentityApplicationChanged;

			await base.OnInitialize();

			if (!this.HasApplicationAttributes && this.IsConnected)
				await Task.Run(this.LoadApplicationAttributes);
		}

		protected override Task OnDispose()
		{
			this.photosLoader.CancelLoadPhotos();

			ServiceRef.XmppService.IdentityApplicationChanged -= this.XmppService_IdentityApplicationChanged;

			return base.OnDispose();
		}

		private Task XmppService_IdentityApplicationChanged(object? Sender, LegalIdentityEventArgs e)
		{
			MainThread.BeginInvokeOnMainThread(async () =>
			{
				this.ApplicationSent = ServiceRef.TagProfile.IdentityApplication is not null;
				this.NrReviews = ServiceRef.TagProfile.NrReviews;

				if (this.ApplicationId is not null && this.ApplicationId == ServiceRef.TagProfile.LegalIdentity?.Id)
					await ServiceRef.UiService.GoToAsync(nameof(ViewIdentityPage), BackMethod.Pop2);
				else
				{
					if (this.ApplicationSent)
					{
						if (this.peerReviewServices is null)
							await Task.Run(this.LoadFeaturedPeerReviewers);
					}
					else
					{
						this.peerReviewServices = null;
						this.HasFeaturedPeerReviewers = false;
					}

					if (!this.ApplicationSent && !this.IsRevoking)
					{
						await ServiceRef.UiService.DisplayAlert(
							ServiceRef.Localizer[nameof(AppResources.Rejected)],
							ServiceRef.Localizer[nameof(AppResources.YourApplicationWasRejected)]);
					}
				}
			});

			return Task.CompletedTask;
		}

		/// <inheritdoc/>
		protected override async Task XmppService_ConnectionStateChanged(object? Sender, XmppState NewState)
		{
			await base.XmppService_ConnectionStateChanged(Sender, NewState);
			this.OnPropertyChanged(nameof(this.ApplicationSentAndConnected));
		}

		/// <inheritdoc/>
		protected override async Task OnConnected()
		{
			await base.OnConnected();

			if (!this.HasApplicationAttributes && this.IsConnected)
				await Task.Run(this.LoadApplicationAttributes);
		}

		/// <inheritdoc/>
		public override void SetIsBusy(bool IsBusy)
		{
			base.SetIsBusy(IsBusy);
			this.NotifyCommandsCanExecuteChanged();
		}

		/// <summary>
		/// Sets the properties of the view model.
		/// </summary>
		/// <param name="Identity">Identity containing properties to set.</param>
		/// <param name="SetPhoto">If Photo is to be set.</param>
		/// <param name="ClearPropertiesNotFound">If properties should be cleared if they are not found in <paramref name="Identity"/>.</param>
		/// <param name="SetPersonalProperties">If personal properties are to be set.</param>
		/// <param name="SetOrganizationalProperties">If organizational properties are to be set.</param>
		/// <param name="SetAppProperties">If app-specific properties are to be set.</param>
		protected async Task SetProperties(LegalIdentity Identity, bool SetPhoto, bool ClearPropertiesNotFound, bool SetPersonalProperties,
			bool SetOrganizationalProperties, bool SetAppProperties)
		{
			await base.SetProperties(Identity, ClearPropertiesNotFound, SetPersonalProperties, SetOrganizationalProperties, SetAppProperties);

			int i = 0;

			foreach (ISO_3166_Country Country in this.Countries)
			{
				if (Country.Alpha2 == this.CountryCode)
				{
					this.Countries.Move(i, 0);
					break;
				}

				i++;
			}

			if (SetPhoto && Identity?.Attachments is not null)
			{
				Photo? First = await this.photosLoader.LoadPhotos(Identity.Attachments, SignWith.LatestApprovedIdOrCurrentKeys);

				if (First is null)
				{
					if (ClearPropertiesNotFound)
					{
						this.photo = null;
						this.Image = null;
						this.ImageBin = null;
						this.HasPhoto = false;
					}
				}
				else
				{
					this.Image = First.Source;
					this.ImageBin = First.Binary;
					this.HasPhoto = true;
				}
			}
		}

		private async Task LoadApplicationAttributes()
		{
			try
			{
				IdApplicationAttributesEventArgs e = await ServiceRef.XmppService.GetIdApplicationAttributes();
				if (e.Ok)
				{
					MainThread.BeginInvokeOnMainThread(() =>
					{
						bool RequiresFirstName = false;
						bool RequiresMiddleNames = false;
						bool RequiresLastNames = false;
						bool RequiresPersonalNumber = false;
						bool RequiresAddress = false;
						bool RequiresAddress2 = false;
						bool RequiresZipCode = false;
						bool RequiresArea = false;
						bool RequiresCity = false;
						bool RequiresRegion = false;
						bool RequiresCountry = false;
						bool RequiresNationality = false;
						bool RequiresGender = false;
						bool RequiresBirthDate = false;

						foreach (string Name in e.RequiredProperties)
						{
							switch (Name)
							{
								case Constants.XmppProperties.FirstName:
									RequiresFirstName = true;
									break;

								case Constants.XmppProperties.MiddleNames:
									RequiresMiddleNames = true;
									break;

								case Constants.XmppProperties.LastNames:
									RequiresLastNames = true;
									break;

								case Constants.XmppProperties.PersonalNumber:
									RequiresPersonalNumber = true;
									break;

								case Constants.XmppProperties.Address:
									RequiresAddress = true;
									break;

								case Constants.XmppProperties.Address2:
									RequiresAddress2 = true;
									break;

								case Constants.XmppProperties.Area:
									RequiresArea = true;
									break;

								case Constants.XmppProperties.City:
									RequiresCity = true;
									break;

								case Constants.XmppProperties.ZipCode:
									RequiresZipCode = true;
									break;

								case Constants.XmppProperties.Region:
									RequiresRegion = true;
									break;

								case Constants.XmppProperties.Country:
									RequiresCountry = true;
									break;

								case Constants.XmppProperties.Nationality:
									RequiresNationality = true;
									break;

								case Constants.XmppProperties.Gender:
									RequiresGender = true;
									break;

								case Constants.XmppProperties.BirthDay:
								case Constants.XmppProperties.BirthMonth:
								case Constants.XmppProperties.BirthYear:
									RequiresBirthDate = true;
									break;
							}
						}

						this.PeerReview = e.PeerReview;
						this.NrPhotos = e.NrPhotos;
						this.NrReviewers = e.NrReviewers;
						this.RequiresCountryIso3166 = e.Iso3166;
						this.RequiresFirstName = RequiresFirstName;
						this.RequiresMiddleNames = RequiresMiddleNames;
						this.RequiresLastNames = RequiresLastNames;
						this.RequiresPersonalNumber = RequiresPersonalNumber;
						this.RequiresAddress = RequiresAddress;
						this.RequiresAddress2 = RequiresAddress2;
						this.RequiresZipCode = RequiresZipCode;
						this.RequiresArea = RequiresArea;
						this.RequiresCity = RequiresCity;
						this.RequiresRegion = RequiresRegion;
						this.RequiresCountry = RequiresCountry;
						this.RequiresNationality = RequiresNationality;
						this.RequiresGender = RequiresGender;
						this.RequiresBirthDate = RequiresBirthDate;
						this.RequiresOrgAddress = this.Organizational && RequiresAddress;
						this.RequiresOrgAddress2 = this.Organizational && RequiresAddress2;
						this.RequiresOrgZipCode = this.Organizational && RequiresZipCode;
						this.RequiresOrgArea = this.Organizational && RequiresArea;
						this.RequiresOrgCity = this.Organizational && RequiresCity;
						this.RequiresOrgRegion = this.Organizational && RequiresRegion;
						this.RequiresOrgCountry = this.Organizational && RequiresCountry;
						this.HasApplicationAttributes = true;
					});
				}
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
			}
		}

		#region Properties

		/// <summary>
		/// If the user consents to the processing of the information.
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
		private bool consent;

		/// <summary>
		/// If the user affirms information provided is correct.
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
		private bool correct;

		/// <summary>
		/// If the view model has ID Application attributes loaded.
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
		private bool hasApplicationAttributes;

		/// <summary>
		/// Number of photos required.
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
		private int nrPhotos;

		/// <summary>
		/// Number of reviewers required.
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
		private int nrReviewers;

		/// <summary>
		/// Number of reviews on current application.
		/// </summary>
		[ObservableProperty]
		private int nrReviews;

		/// <summary>
		/// If peer review is permitted.
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
		[NotifyPropertyChangedFor(nameof(FeaturedPeerReviewers))]
		private bool peerReview;

		/// <summary>
		/// If an ID application has been sent.
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
		[NotifyCanExecuteChangedFor(nameof(ScanQrCodeCommand))]
		[NotifyCanExecuteChangedFor(nameof(RequestReviewCommand))]
		[NotifyCanExecuteChangedFor(nameof(RevokeApplicationCommand))]
		[NotifyPropertyChangedFor(nameof(CanEdit))]
		[NotifyPropertyChangedFor(nameof(CanRemovePhoto))]
		[NotifyPropertyChangedFor(nameof(CanTakePhoto))]
		[NotifyPropertyChangedFor(nameof(ApplicationSentAndConnected))]
		[NotifyPropertyChangedFor(nameof(CanRequestFeaturedPeerReviewer))]
		[NotifyPropertyChangedFor(nameof(FeaturedPeerReviewers))]
		private bool applicationSent;

		/// <summary>
		/// If application is personal.
		/// </summary>
		[ObservableProperty]
		private bool personal;

		/// <summary>
		/// If application is organizational.
		/// </summary>
		[ObservableProperty]
		private bool organizational;

		/// <summary>
		/// If a photo is available.
		/// </summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(CanRemovePhoto))]
		[NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
		[NotifyCanExecuteChangedFor(nameof(RemovePhotoCommand))]
		private bool hasPhoto;

		/// <summary>
		/// Photo
		/// </summary>
		[ObservableProperty]
		private ImageSource? image;

		/// <summary>
		/// Binary representation of photo
		/// </summary>
		[ObservableProperty]
		private byte[]? imageBin;

		/// <summary>
		/// Rotation of <see cref="Image"/>
		/// </summary>
		[ObservableProperty]
		private int imageRotation;

		/// <summary>
		/// If the form can be edited.
		/// </summary>
		public bool CanEdit => !this.ApplicationSent;

		/// <summary>
		/// If the form can be edited.
		/// </summary>
		public bool CanRemovePhoto => this.CanEdit && this.HasPhoto;

		/// <summary>
		/// If a photo can be taken.
		/// </summary>
		public bool CanTakePhoto => this.CanEdit && MediaPicker.IsCaptureSupported;

		/// <summary>
		/// If application has been sent and app is connected.
		/// </summary>
		public bool ApplicationSentAndConnected => this.ApplicationSent && this.IsConnected;

		/// <summary>
		/// If app is in the processing of uploading application.
		/// </summary>
		[ObservableProperty]
		private bool isApplying;

		/// <summary>
		/// If app is in the processing of revoking an application.
		/// </summary>
		[ObservableProperty]
		private bool isRevoking;

		/// <summary>
		/// ID of application.
		/// </summary>
		[ObservableProperty]
		private string? applicationId;

		/// <summary>
		/// If view has featured peer reviewers.
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(this.RequestReviewCommand))]
		[NotifyPropertyChangedFor(nameof(CanRequestFeaturedPeerReviewer))]
		[NotifyPropertyChangedFor(nameof(FeaturedPeerReviewers))]
		private bool hasFeaturedPeerReviewers;

		/// <summary>
		/// If the user can request a review from a featured peer reviewer.
		/// </summary>
		public bool CanRequestFeaturedPeerReviewer => this.ApplicationSent && this.HasFeaturedPeerReviewers;

		/// <summary>
		/// If the option to request featured peer reviewer should be shown.
		/// </summary>
		public bool FeaturedPeerReviewers => this.CanRequestFeaturedPeerReviewer && this.PeerReview;

		/// <summary>
		/// Available country definitions
		/// </summary>
		[ObservableProperty]
		private ObservableCollection<ISO_3166_Country> countries;

		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(NationalityCode))]
		[NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
		private ISO_3166_Country? nationality;

		/// <summary>
		/// Available gender definitions
		/// </summary>
		[ObservableProperty]
		private ObservableCollection<ISO_5218_Gender> genders;

		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(GenderCode))]
		[NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
		private ISO_5218_Gender? gender;

		/// <inheritdoc/>
		protected override void OnPropertyChanged(PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(this.Nationality):
					this.NationalityCode = this.Nationality?.Alpha2 ?? string.Empty;
					break;

				case nameof(this.Gender):
					this.GenderCode = this.Gender?.Letter ?? string.Empty;
					break;
			}

			base.OnPropertyChanged(e);
		}

		#endregion

		#region Commands

		/// <summary>
		/// Toggles <see cref="Consent"/>
		/// </summary>
		[RelayCommand]
		public void ToggleConsent()
		{
			this.Consent = !this.Consent;
		}

		/// <summary>
		/// Toggles <see cref="Correct"/>
		/// </summary>
		[RelayCommand]
		public void ToggleCorrect()
		{
			this.Correct = !this.Correct;
		}

		/// <summary>
		/// If user can Apply for a new ID.
		/// </summary>
		public override bool CanApply
		{
			get
			{
				if (!this.CanExecuteCommands || !this.Consent || !this.Correct || this.ApplicationSent || !this.HasPhoto)
					return false;

				if (this.HasApplicationAttributes)
				{
					if (!this.FirstNameOk ||
						!this.MiddleNamesOk ||
						!this.LastNamesOk ||
						!this.PersonalNumberOk ||
						!this.AddressOk ||
						!this.Address2Ok ||
						!this.ZipCodeOk ||
						!this.AreaOk ||
						!this.CityOk ||
						!this.RegionOk ||
						!this.CountryOk ||
						!this.NationalityOk ||
						!this.GenderOk ||
						!this.BirthDateOk)
					{
						return false;
					}

					if (this.RequiresCountryIso3166 && !ISO_3166_1.TryGetCountryByCode(this.CountryCode, out _))
						return false;

					if (this.Organizational)
					{
						if (!this.OrgNameOk ||
							!this.OrgDepartmentOk ||
							!this.OrgRoleOk ||
							!this.OrgNumberOk ||
							!this.OrgAddressOk ||
							!this.OrgAddress2Ok ||
							!this.OrgZipCodeOk ||
							!this.OrgAreaOk ||
							!this.OrgCityOk ||
							!this.OrgRegionOk ||
							!this.OrgCountryOk)
						{
							return false;
						}
					}
				}

				return true;
			}
		}

		/// <summary>
		/// Executes the application command.
		/// </summary>
		protected override async Task Apply()
		{
			if (this.ApplicationSent)
				return;

			if (!await AreYouSure(ServiceRef.Localizer[nameof(AppResources.AreYouSureYouWantToSendThisIdApplication)]))
				return;

			if (!await App.AuthenticateUser(AuthenticationPurpose.SignApplication, true))
				return;

			try
			{
				LegalIdentityAttachment[] Photos = this.photo is null ? [] : [this.photo];

				this.SetIsBusy(true);
				this.IsApplying = true;
				NumberInformation Info = await PersonalNumberSchemes.Validate(this.CountryCode!, this.PersonalNumber!);
				this.PersonalNumber = Info.PersonalNumber;


				bool HasIdWithPrivateKey = ServiceRef.TagProfile.LegalIdentity is not null &&
					await ServiceRef.XmppService.HasPrivateKey(ServiceRef.TagProfile.LegalIdentity.Id);

				(bool Succeeded, LegalIdentity? AddedIdentity) = await ServiceRef.NetworkService.TryRequest(() =>
					ServiceRef.XmppService.AddLegalIdentity(this, !HasIdWithPrivateKey, Photos));

				if (Succeeded && AddedIdentity is not null)
				{
					await ServiceRef.TagProfile.SetIdentityApplication(AddedIdentity, true);
					this.ApplicationSent = true;
					this.ApplicationId = AddedIdentity.Id;

					await Task.Run(this.LoadFeaturedPeerReviewers);

					if (this.HasPhoto)
					{
						Attachment? FirstImage = AddedIdentity.Attachments.GetFirstImageAttachment();

						if (FirstImage is not null && this.ImageBin is not null)
							await ServiceRef.AttachmentCacheService.Add(FirstImage.Url, AddedIdentity.Id, true, this.ImageBin, FirstImage.ContentType);
					}
				}
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				await ServiceRef.UiService.DisplayException(ex);
			}
			finally
			{
				this.SetIsBusy(false);
				this.IsApplying = false;
			}
		}

		/// <summary>
		/// Revokes the current application.
		/// </summary>
		[RelayCommand(CanExecute = nameof(ApplicationSent))]
		private async Task RevokeApplication()
		{
			LegalIdentity? Application = ServiceRef.TagProfile.IdentityApplication;
			if (Application is null)
			{
				this.ApplicationSent = false;
				this.peerReviewServices = null;
				this.HasFeaturedPeerReviewers = false;
				return;
			}

			if (!await AreYouSure(ServiceRef.Localizer[nameof(AppResources.AreYouSureYouWantToRevokeTheCurrentIdApplication)]))
				return;

			if (!await App.AuthenticateUser(AuthenticationPurpose.RevokeApplication, true))
				return;

			try
			{
				this.SetIsBusy(true);
				this.IsRevoking = true; // Will be cleared from event-handle.

				try
				{
					await ServiceRef.XmppService.ObsoleteLegalIdentity(Application.Id);
				}
				catch (ForbiddenException)
				{
					// Ignore. Application may have been rejected or elapsed outside of the
					// scope of the app.
				}

				await ServiceRef.TagProfile.SetIdentityApplication(null, true);
				this.ApplicationSent = false;
				this.peerReviewServices = null;
				this.HasFeaturedPeerReviewers = false;
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				await ServiceRef.UiService.DisplayException(ex);
				this.IsRevoking = false;
			}
			finally
			{
				this.SetIsBusy(false);
			}
		}

		/// <summary>
		/// Scan a QR-code belonging to a peer
		/// </summary>
		[RelayCommand(CanExecute = nameof(ApplicationSent))]
		private async Task ScanQrCode()
		{
			string? Url = await Services.UI.QR.QrCode.ScanQrCode(nameof(AppResources.QrPageTitleScanPeerId),
				[
					Constants.UriSchemes.IotId
				]);

			if (string.IsNullOrEmpty(Url) || !Constants.UriSchemes.StartsWithIdScheme(Url))
				return;

			await this.SendPeerReviewRequest(Constants.UriSchemes.RemoveScheme(Url));
		}

		private async Task SendPeerReviewRequest(string? ReviewerId)
		{
			LegalIdentity? ToReview = ServiceRef.TagProfile.IdentityApplication;
			if (ToReview is null || string.IsNullOrEmpty(ReviewerId))
				return;

			try
			{
				this.SetIsBusy(true);

				await ServiceRef.XmppService.PetitionPeerReviewId(ReviewerId, ToReview, Guid.NewGuid().ToString(),
					ServiceRef.Localizer[nameof(AppResources.CouldYouPleaseReviewMyIdentityInformation)]);

				await ServiceRef.UiService.DisplayAlert(ServiceRef.Localizer[nameof(AppResources.PetitionSent)],
					ServiceRef.Localizer[nameof(AppResources.APetitionHasBeenSentToYourPeer)]);
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				await ServiceRef.UiService.DisplayException(ex);
			}
			finally
			{
				this.SetIsBusy(false);
			}
		}

		/// <summary>
		/// Takes a new photo
		/// </summary>
		[RelayCommand(CanExecute = nameof(CanTakePhoto))]
		private async Task TakePhoto()
		{
			if (!this.CanTakePhoto)
				return;

			try
			{
				FileResult? Result = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions()
				{
					Title = ServiceRef.Localizer[nameof(AppResources.TakePhotoOfYourself)]
				});

				if (Result is null)
					return;

				Stream stream = await Result.OpenReadAsync();

				byte[] InputBin = stream.ToByteArray() ?? throw new Exception("Failed to read photo stream");

				TaskCompletionSource<byte[]?> TCS = new();
				await ServiceRef.UiService.GoToAsync(nameof(ImageCroppingPage), new ImageCroppingNavigationArgs(ImageSource.FromStream(() => new MemoryStream(InputBin)), TCS));

				byte[] OutputBin = await TCS.Task ?? throw new Exception("Failed to crop photo");

				MemoryStream MS = new(OutputBin);


				await this.AddPhoto(MS, Result.FullPath, true);
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				await ServiceRef.UiService.DisplayAlert(
					ServiceRef.Localizer[nameof(AppResources.ErrorTitle)],
					ServiceRef.Localizer[nameof(AppResources.FailedToLoadPhoto)]);
			}
		}

		/// <summary>
		/// Adds a photo from the specified path to use as a profile photo.
		/// </summary>
		/// <param name="Bin">Binary content</param>
		/// <param name="ContentType">Content-Type</param>
		/// <param name="Rotation">Rotation to use, to display the image correctly.</param>
		/// <param name="saveLocalCopy">Set to <c>true</c> to save a local copy, <c>false</c> otherwise.</param>
		/// <param name="showAlert">Set to <c>true</c> to show an alert if photo is too large; <c>false</c> otherwise.</param>
		public async Task AddPhoto(byte[] Bin, string ContentType, int Rotation, bool saveLocalCopy, bool showAlert)
		{
			if (Bin.Length > ServiceRef.TagProfile.HttpFileUploadMaxSize)
			{
				if (showAlert)
					await ServiceRef.UiService.DisplayAlert(
						ServiceRef.Localizer[nameof(AppResources.ErrorTitle)],
						ServiceRef.Localizer[nameof(AppResources.PhotoIsTooLarge)]);

				return;
			}

			this.RemovePhoto(saveLocalCopy);

			if (saveLocalCopy)
			{
				try
				{
					File.WriteAllBytes(this.localPhotoFileName, Bin);
				}
				catch (Exception ex)
				{
					ServiceRef.LogService.LogException(ex);
				}
			}

			this.photo = new LegalIdentityAttachment(this.localPhotoFileName, ContentType, Bin);
			this.ImageRotation = Rotation;
			this.Image = ImageSource.FromStream(() => new MemoryStream(Bin));
			this.ImageBin = Bin;
			this.HasPhoto = true;
		}

		/// <summary>
		/// Adds a photo from a filestream to use as a profile photo.
		/// </summary>
		/// <param name="InputStream">Stream containing the photo.</param>
		/// <param name="FilePath">The full path to the file.</param>
		/// <param name="SaveLocalCopy">Set to <c>true</c> to save a local copy, <c>false</c> otherwise.</param>
		public async Task AddPhoto(Stream InputStream, string FilePath, bool SaveLocalCopy)
		{
			SKData? ImageData = null;

			try
			{
				bool FallbackOriginal = true;

				if (SaveLocalCopy)
				{
					// try to downscale and comress the image
					ImageData = CompressImage(InputStream);

					if (ImageData is not null)
					{
						FallbackOriginal = false;
						await this.AddPhoto(ImageData.ToArray(), Constants.MimeTypes.Jpeg, 0, SaveLocalCopy, true);
					}
				}

				if (FallbackOriginal)
				{
					byte[] Bin = File.ReadAllBytes(FilePath);
					if (!InternetContent.TryGetContentType(Path.GetExtension(FilePath), out string ContentType))
						ContentType = "application/octet-stream";

					await this.AddPhoto(Bin, ContentType, PhotosLoader.GetImageRotation(Bin), SaveLocalCopy, true);
				}
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				await ServiceRef.UiService.DisplayAlert(
						ServiceRef.Localizer[nameof(AppResources.ErrorTitle)],
						ServiceRef.Localizer[nameof(AppResources.FailedToLoadPhoto)]);
			}
			finally
			{
				ImageData?.Dispose();
			}
		}

		private static SKData? CompressImage(Stream inputStream)
		{
			try
			{
				using SKManagedStream ManagedStream = new(inputStream);
				using SKData ImageData = SKData.Create(ManagedStream);

				SKCodec Codec = SKCodec.Create(ImageData);
				SKBitmap SkBitmap = SKBitmap.Decode(ImageData);

				SkBitmap = HandleOrientation(SkBitmap, Codec.EncodedOrigin);

				bool Resize = false;
				int Height = SkBitmap.Height;
				int Width = SkBitmap.Width;

				// downdsample to FHD
				if ((Width >= Height) && (Width > 1920))
				{
					Height = (int)(Height * (1920.0 / Width) + 0.5);
					Width = 1920;
					Resize = true;
				}
				else if ((Height > Width) && (Height > 1920))
				{
					Width = (int)(Width * (1920.0 / Height) + 0.5);
					Height = 1920;
					Resize = true;
				}

				if (Resize)
				{
					SKImageInfo Info = SkBitmap.Info;
					SKImageInfo NewInfo = new(Width, Height, Info.ColorType, Info.AlphaType, Info.ColorSpace);
					SkBitmap = SkBitmap.Resize(NewInfo, SKFilterQuality.High);
				}

				return SkBitmap.Encode(SKEncodedImageFormat.Jpeg, 80);
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				return null;
			}
		}

		private static SKBitmap HandleOrientation(SKBitmap Bitmap, SKEncodedOrigin Orientation)
		{
			SKBitmap Rotated;

			switch (Orientation)
			{
				case SKEncodedOrigin.BottomRight:
					Rotated = new SKBitmap(Bitmap.Width, Bitmap.Height);

					using (SKCanvas Surface = new(Rotated))
					{
						Surface.RotateDegrees(180, Bitmap.Width / 2, Bitmap.Height / 2);
						Surface.DrawBitmap(Bitmap, 0, 0);
					}
					break;

				case SKEncodedOrigin.RightTop:
					Rotated = new SKBitmap(Bitmap.Height, Bitmap.Width);

					using (SKCanvas Surface = new(Rotated))
					{
						Surface.Translate(Rotated.Width, 0);
						Surface.RotateDegrees(90);
						Surface.DrawBitmap(Bitmap, 0, 0);
					}
					break;

				case SKEncodedOrigin.LeftBottom:
					Rotated = new SKBitmap(Bitmap.Height, Bitmap.Width);

					using (SKCanvas Surface = new(Rotated))
					{
						Surface.Translate(0, Rotated.Height);
						Surface.RotateDegrees(270);
						Surface.DrawBitmap(Bitmap, 0, 0);
					}
					break;

				default:
					return Bitmap;
			}

			return Rotated;
		}

		private void RemovePhoto(bool RemoveFileOnDisc)
		{
			try
			{
				this.photo = null;
				this.Image = null;
				this.ImageBin = null;
				this.HasPhoto = false;

				if (RemoveFileOnDisc && File.Exists(this.localPhotoFileName))
					File.Delete(this.localPhotoFileName);
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
			}
		}

		/// <summary>
		/// Takes a new photo
		/// </summary>
		[RelayCommand(CanExecute = nameof(CanEdit))]
		private async Task PickPhoto()
		{
			try
			{
				FileResult? Result = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions()
				{
					Title = ServiceRef.Localizer[nameof(AppResources.PickPhotoOfYourself)]
				});

				if (Result is null)
					return;

				Stream InputStream = await Result.OpenReadAsync();
				byte[] InputBin = InputStream.ToByteArray() ?? throw new Exception("Failed to read photo stream");

				TaskCompletionSource<byte[]?> Tcs = new();
				await ServiceRef.UiService.GoToAsync(
					nameof(ImageCroppingPage),
					new ImageCroppingNavigationArgs(ImageSource.FromStream(() => new MemoryStream(InputBin)), Tcs)
				);

				byte[] OutputBin = await Tcs.Task ?? throw new Exception("Failed to crop photo");
				MemoryStream OutputStream = new(OutputBin);

				await this.AddPhoto(OutputStream, Result.FullPath, true);
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				await ServiceRef.UiService.DisplayException(ex);
			}
		}


		/// <summary>
		/// Removes the current photo.
		/// </summary>
		[RelayCommand(CanExecute = nameof(CanRemovePhoto))]
		private void RemovePhoto()
		{
			this.RemovePhoto(true);
		}

		private async Task LoadFeaturedPeerReviewers()
		{
			await ServiceRef.NetworkService.TryRequest(async () =>
			{
				this.peerReviewServices = await ServiceRef.XmppService.GetServiceProvidersForPeerReviewAsync();

				MainThread.BeginInvokeOnMainThread(() =>
				{
					this.HasFeaturedPeerReviewers = this.peerReviewServices.Length > 0;
				});
			});
		}

		/// <summary>
		/// Select from a list of featured peer reviewers.
		/// </summary>
		[RelayCommand(CanExecute = nameof(CanRequestFeaturedPeerReviewer))]
		private async Task RequestReview()
		{
			if (this.peerReviewServices is null)
				await this.LoadFeaturedPeerReviewers();

			if ((this.peerReviewServices?.Length ?? 0) > 0)
			{
				List<ServiceProviderWithLegalId> ServiceProviders = [.. this.peerReviewServices, new RequestFromPeer()];

				ServiceProvidersNavigationArgs e = new([.. ServiceProviders],
					ServiceRef.Localizer[nameof(AppResources.RequestReview)],
					ServiceRef.Localizer[nameof(AppResources.SelectServiceProviderPeerReview)]);

				await ServiceRef.UiService.GoToAsync(nameof(ServiceProvidersPage), e, BackMethod.Pop);

				if (e.ServiceProvider is not null)
				{
					IServiceProvider? ServiceProvider = await e.ServiceProvider.Task;

					if (ServiceProvider is ServiceProviderWithLegalId ServiceProviderWithLegalId &&
						!string.IsNullOrEmpty(ServiceProviderWithLegalId.LegalId))
					{
						if (!ServiceProviderWithLegalId.External)
						{
							if (!await ServiceRef.NetworkService.TryRequest(async () =>
								await ServiceRef.XmppService.SelectPeerReviewService(ServiceProvider.Id, ServiceProvider.Type)))
							{
								return;
							}
						}

						await this.SendPeerReviewRequest(ServiceProviderWithLegalId.LegalId);
						return;
					}
					else if (ServiceProvider is null)
						return;
				}
			}

			await this.ScanQrCode();
		}

		#endregion
	}
}
