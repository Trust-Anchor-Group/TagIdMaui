using NeuroAccessMaui.Extensions;
using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services.UI;
using NeuroAccessMaui.UI.Pages.Contracts.NewContract;
using NeuroAccessMaui.UI.Pages.Contracts.ViewContract;
using NeuroAccessMaui.UI.Pages.Identity.ViewIdentity;
using NeuroAccessMaui.UI.Pages.Petitions.PetitionIdentity;
using NeuroAccessMaui.UI.Pages.Petitions.PetitionPeerReview;
using NeuroAccessMaui.UI.Pages.Petitions.PetitionSignature;
using NeuroFeatures;
using System.Globalization;
using System.Reflection;
using System.Text;
using Waher.Content.Xml;
using Waher.Networking.XMPP;
using Waher.Networking.XMPP.Contracts;
using Waher.Networking.XMPP.StanzaErrors;
using Waher.Persistence;
using Waher.Persistence.Filters;
using Waher.Runtime.Inventory;

namespace NeuroAccessMaui.Services.Contracts
{
	[Singleton]
	internal class ContractOrchestratorService : LoadableService, IContractOrchestratorService
	{
		public ContractOrchestratorService()
		{
		}

		public override Task Load(bool IsResuming, CancellationToken CancellationToken)
		{
			if (this.BeginLoad(IsResuming, CancellationToken))
			{
				ServiceRef.XmppService.ConnectionStateChanged += this.Contracts_ConnectionStateChanged;
				ServiceRef.XmppService.PetitionForPeerReviewIdReceived += this.Contracts_PetitionForPeerReviewIdReceived;
				ServiceRef.XmppService.PetitionForIdentityReceived += this.Contracts_PetitionForIdentityReceived;
				ServiceRef.XmppService.PetitionForSignatureReceived += this.Contracts_PetitionForSignatureReceived;
				ServiceRef.XmppService.PetitionedIdentityResponseReceived += this.Contracts_PetitionedIdentityResponseReceived;
				ServiceRef.XmppService.PetitionedPeerReviewIdResponseReceived += this.Contracts_PetitionedPeerReviewResponseReceived;
				ServiceRef.XmppService.SignaturePetitionResponseReceived += this.Contracts_SignaturePetitionResponseReceived;
				ServiceRef.XmppService.ContractProposalReceived += this.Contracts_ContractProposalRecieved;

				this.EndLoad(true);
			}

			return Task.CompletedTask;
		}

		public override Task Unload()
		{
			if (this.BeginUnload())
			{
				ServiceRef.XmppService.ConnectionStateChanged -= this.Contracts_ConnectionStateChanged;
				ServiceRef.XmppService.PetitionForPeerReviewIdReceived -= this.Contracts_PetitionForPeerReviewIdReceived;
				ServiceRef.XmppService.PetitionForIdentityReceived -= this.Contracts_PetitionForIdentityReceived;
				ServiceRef.XmppService.PetitionForSignatureReceived -= this.Contracts_PetitionForSignatureReceived;
				ServiceRef.XmppService.PetitionedIdentityResponseReceived -= this.Contracts_PetitionedIdentityResponseReceived;
				ServiceRef.XmppService.PetitionedPeerReviewIdResponseReceived -= this.Contracts_PetitionedPeerReviewResponseReceived;
				ServiceRef.XmppService.SignaturePetitionResponseReceived -= this.Contracts_SignaturePetitionResponseReceived;
				ServiceRef.XmppService.ContractProposalReceived -= this.Contracts_ContractProposalRecieved;

				this.EndUnload();
			}

			return Task.CompletedTask;
		}

		#region Event Handlers

		private async Task Contracts_PetitionForPeerReviewIdReceived(object? Sender, SignaturePetitionEventArgs e)
		{
			try
			{
				LegalIdentity Identity = e.RequestorIdentity;

				if (Identity?.Properties is not null)
				{
					foreach (Property Property in Identity.Properties)
					{
						switch (Property.Name)
						{
							case Constants.XmppProperties.FirstName:
							case Constants.XmppProperties.MiddleNames:
							case Constants.XmppProperties.LastNames:
							case Constants.XmppProperties.PersonalNumber:
							case Constants.XmppProperties.Address:
							case Constants.XmppProperties.Address2:
							case Constants.XmppProperties.Area:
							case Constants.XmppProperties.City:
							case Constants.XmppProperties.ZipCode:
							case Constants.XmppProperties.Region:
							case Constants.XmppProperties.Country:
							case Constants.XmppProperties.Nationality:
							case Constants.XmppProperties.Gender:
							case Constants.XmppProperties.BirthDay:
							case Constants.XmppProperties.BirthMonth:
							case Constants.XmppProperties.BirthYear:
							case Constants.XmppProperties.OrgName:
							case Constants.XmppProperties.OrgNumber:
							case Constants.XmppProperties.OrgAddress:
							case Constants.XmppProperties.OrgAddress2:
							case Constants.XmppProperties.OrgArea:
							case Constants.XmppProperties.OrgCity:
							case Constants.XmppProperties.OrgZipCode:
							case Constants.XmppProperties.OrgRegion:
							case Constants.XmppProperties.OrgCountry:
							case Constants.XmppProperties.OrgDepartment:
							case Constants.XmppProperties.OrgRole:
							case Constants.XmppProperties.DeviceId:
							case Constants.XmppProperties.Jid:
							case Constants.XmppProperties.Phone:
							case Constants.XmppProperties.EMail:
								break;

							default:
								byte[] Signature = await ServiceRef.XmppService.Sign(e.ContentToSign, SignWith.LatestApprovedId);

								await ServiceRef.XmppService.SendPetitionSignatureResponse(e.SignatoryIdentityId, e.ContentToSign, Signature,
									e.PetitionId, e.RequestorFullJid, false);

								return;
						}
					}

					PetitionPeerReviewNavigationArgs Args = new(Identity, e.RequestorFullJid, e.SignatoryIdentityId, e.PetitionId,
						e.Purpose, e.ContentToSign);

					await ServiceRef.UiService.GoToAsync(nameof(PetitionPeerReviewPage), Args);
				}
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
			}
		}

		private async Task Contracts_PetitionForIdentityReceived(object? Sender, LegalIdentityPetitionEventArgs e)
		{
			if(ServiceRef.UiService.CurrentPage is ViewContractPage)
				return;

			try
			{
				LegalIdentity Identity;

				if (e.RequestedIdentityId == ServiceRef.TagProfile.LegalIdentity?.Id)
					Identity = ServiceRef.TagProfile.LegalIdentity;
				else
				{
					(bool Succeeded, LegalIdentity? LegalId) = await ServiceRef.NetworkService.TryRequest(() => ServiceRef.XmppService.GetLegalIdentity(e.RequestedIdentityId));

					if (!Succeeded || LegalId is null)
						return;

					Identity = LegalId;
				}

				if (Identity is null)
				{
					ServiceRef.LogService.LogWarning("Identity is missing or cannot be retrieved, ignore.",
						new KeyValuePair<string, object?>("Type", this.GetType().Name),
						new KeyValuePair<string, object?>("Method", nameof(Contracts_PetitionForIdentityReceived)));

					return;
				}

				if (Identity.State == IdentityState.Compromised ||
					Identity.State == IdentityState.Rejected)
				{
					await ServiceRef.NetworkService.TryRequest(() =>
					{
						return ServiceRef.XmppService.SendPetitionIdentityResponse(
							e.RequestedIdentityId, e.PetitionId, e.RequestorFullJid, false);
					});
				}
				else
				{
					Identity = e.RequestorIdentity;

					if (Identity is not null)
					{
						await ServiceRef.UiService.GoToAsync(nameof(PetitionIdentityPage), new PetitionIdentityNavigationArgs(
							Identity, e.RequestorFullJid, e.RequestedIdentityId, e.PetitionId, e.Purpose));
					}
				}
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
			}
		}

		private async Task Contracts_PetitionForSignatureReceived(object? Sender, SignaturePetitionEventArgs e)
		{
			try
			{
				LegalIdentity Identity;

				if (e.SignatoryIdentityId == ServiceRef.TagProfile.LegalIdentity?.Id)
					Identity = ServiceRef.TagProfile.LegalIdentity;
				else
				{
					(bool Succeeded, LegalIdentity? LegalId) = await ServiceRef.NetworkService.TryRequest(() => ServiceRef.XmppService.GetLegalIdentity(e.SignatoryIdentityId));
					if (!Succeeded || LegalId is null)
						return;

					Identity = LegalId;
				}

				if (Identity is null)
				{
					ServiceRef.LogService.LogWarning("Identity is missing or cannot be retrieved, ignore.",
						new KeyValuePair<string, object?>("Type", this.GetType().Name),
						new KeyValuePair<string, object?>("Method", nameof(Contracts_PetitionForSignatureReceived)));

					return;
				}

				if (Identity.State == IdentityState.Compromised || Identity.State == IdentityState.Rejected)
				{
					await ServiceRef.NetworkService.TryRequest(() =>
					{
						return ServiceRef.XmppService.SendPetitionSignatureResponse(
							e.SignatoryIdentityId, e.ContentToSign, [], e.PetitionId, e.RequestorFullJid, false);
					});
				}
				else
				{
					Identity = e.RequestorIdentity;

					if (Identity is not null)
					{
						if (!await App.AuthenticateUser(AuthenticationPurpose.PetitionForSignatureReceived))
							return;

						await ServiceRef.UiService.GoToAsync(nameof(PetitionSignaturePage), new PetitionSignatureNavigationArgs(
							Identity, e.RequestorFullJid, e.SignatoryIdentityId, e.ContentToSign, e.PetitionId, e.Purpose));
					}
				}
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
			}
		}

		private async Task Contracts_PetitionedIdentityResponseReceived(object? Sender, LegalIdentityPetitionResponseEventArgs e)
		{
			try
			{
				LegalIdentity Identity = e.RequestedIdentity;

				if (!e.Response || Identity is null)
				{
					await ServiceRef.UiService.DisplayAlert(
						ServiceRef.Localizer[nameof(AppResources.Message)],
						ServiceRef.Localizer[nameof(AppResources.SignaturePetitionDenied)],
						ServiceRef.Localizer[nameof(AppResources.Ok)]);
				}
				else
					await ServiceRef.UiService.GoToAsync(nameof(ViewIdentityPage), new ViewIdentityNavigationArgs(Identity));
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
			}
		}

		private async Task Contracts_PetitionedPeerReviewResponseReceived(object? Sender, SignaturePetitionResponseEventArgs e)
		{
			try
			{
				LegalIdentity? ReviewedIdentity = ServiceRef.TagProfile.IdentityApplication;
				if (ReviewedIdentity is null)
					return;

				if (!e.Response)
				{
					await ServiceRef.UiService.DisplayAlert(
						ServiceRef.Localizer[nameof(AppResources.PeerReviewRejected)],
						ServiceRef.Localizer[nameof(AppResources.APeerYouRequestedToReviewHasRejected)],
						ServiceRef.Localizer[nameof(AppResources.Ok)]);

					return;
				}

				LegalIdentity ReviewerIdentity = e.RequestedIdentity;
				if (ReviewerIdentity is null)
					return;

				try
				{
					StringBuilder Xml = new();
					ReviewedIdentity.Serialize(Xml, true, true, true, true, true, true, true);
					string s = Xml.ToString();
					byte[] Data = Encoding.UTF8.GetBytes(s);
					bool? Result;

					try
					{
						Result = ServiceRef.XmppService.ValidateSignature(ReviewerIdentity, Data, e.Signature);
					}
					catch (Exception ex)
					{
						await ServiceRef.UiService.DisplayException(ex);
						return;
					}

					if (!Result.HasValue || !Result.Value)
					{
						await ServiceRef.UiService.DisplayAlert(
							ServiceRef.Localizer[nameof(AppResources.PeerReviewRejected)],
							ServiceRef.Localizer[nameof(AppResources.APeerYouRequestedToReviewHasBeenRejectedDueToSignatureError)],
							ServiceRef.Localizer[nameof(AppResources.Ok)]);
					}
					else
					{
						(bool Succeeded, LegalIdentity? LegalIdentity) = await ServiceRef.NetworkService.TryRequest(async () =>
						{
							LegalIdentity Result = await ServiceRef.XmppService.AddPeerReviewIdAttachment(ReviewedIdentity, ReviewerIdentity, e.Signature);
							await ServiceRef.TagProfile.IncrementNrPeerReviews();
							return Result;
						});

						if (Succeeded)
						{
							await ServiceRef.UiService.DisplayAlert(
								ServiceRef.Localizer[nameof(AppResources.PeerReviewAccepted)],
								ServiceRef.Localizer[nameof(AppResources.APeerReviewYouhaveRequestedHasBeenAccepted)],
								ServiceRef.Localizer[nameof(AppResources.Ok)]);
						}
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
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
			}
		}

		private async Task Contracts_ContractProposalRecieved(object? sender, ContractProposalEventArgs e)
		{
			try
			{
				Contract Contract = await ServiceRef.XmppService.GetContract(e.ContractId);
			
				await ServiceRef.UiService.GoToAsync(nameof(ViewContractPage), new ViewContractNavigationArgs(
							Contract, false, e.Role, e.MessageText, e.FromBareJID));
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
			}
		}

		private Task Contracts_ConnectionStateChanged(object _, XmppState NewState)
		{
			try
			{
				if (ServiceRef.XmppService.IsOnline && ServiceRef.TagProfile.IsCompleteOrWaitingForValidation())
				{
					if (ServiceRef.TagProfile.LegalIdentity is not null)
					{
						Task _2 = Task.Run(async () =>
						{
							try
							{
								await Task.Delay(Constants.Timeouts.XmppInit);
								await ReDownloadLegalIdentity();
							}
							catch (Exception ex)
							{
								ServiceRef.LogService.LogException(ex);
							}
						});
					}
				}
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
			}

			return Task.CompletedTask;
		}

		#endregion

		protected static async Task ReDownloadLegalIdentity()
		{
			if (ServiceRef.XmppService is null ||
				!await ServiceRef.XmppService.WaitForConnectedState(Constants.Timeouts.XmppConnect) ||
				!ServiceRef.XmppService.IsOnline ||
				ServiceRef.TagProfile.LegalIdentity is null)
			{
				return;
			}

			LegalIdentity? Identity;

			try
			{
				Identity = await ServiceRef.XmppService!.GetLegalIdentity(ServiceRef.TagProfile.LegalIdentity.Id);
			}
			catch (ForbiddenException)    // Old ID belonging to a previous account, for example. Simply discard.
			{
				await ServiceRef.TagProfile.ClearLegalIdentity();
				await App.SetRegistrationPageAsync();
				return;
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				return;
			}

			if (Identity is not null)
			{
				MainThread.BeginInvokeOnMainThread(async () =>
				{
					string? userMessage = null;
					bool gotoRegistrationPage = false;

					if (Identity.State == IdentityState.Compromised)
					{
						userMessage = ServiceRef.Localizer[nameof(AppResources.YourLegalIdentityHasBeenCompromised)];
						await ServiceRef.TagProfile.CompromiseLegalIdentity(Identity);
						gotoRegistrationPage = true;
					}
					else if (Identity.State == IdentityState.Obsoleted)
					{
						userMessage = ServiceRef.Localizer[nameof(AppResources.YourLegalIdentityHasBeenObsoleted)];
						await ServiceRef.TagProfile.RevokeLegalIdentity(Identity);
						gotoRegistrationPage = true;
					}
					else if (Identity.State == IdentityState.Approved && !await ServiceRef.XmppService!.HasPrivateKey(Identity.Id))
					{
						bool Response = await ServiceRef.UiService.DisplayAlert(
							ServiceRef.Localizer[nameof(AppResources.WarningTitle)],
							ServiceRef.Localizer[nameof(AppResources.UnableToGetAccessToYourPrivateKeys)],
							ServiceRef.Localizer[nameof(AppResources.Continue)],
							ServiceRef.Localizer[nameof(AppResources.Repair)]);

						if (Response)
							await ServiceRef.TagProfile.SetLegalIdentity(Identity, true);
						else
						{
							try
							{
								File.WriteAllText(Path.Combine(ServiceRef.StorageService.DataFolder, "Start.txt"),
									DateTime.Now.AddHours(1).Ticks.ToString(CultureInfo.InvariantCulture));
							}
							catch (Exception ex)
							{
								ServiceRef.LogService.LogException(ex);
							}

							await App.Stop();
							return;
						}
					}
					else
						await ServiceRef.TagProfile.SetLegalIdentity(Identity, true);

					if (gotoRegistrationPage)
					{
						await App.SetRegistrationPageAsync();

						// After navigating to the registration page, show the user why this happened.
						if (!string.IsNullOrWhiteSpace(userMessage))
						{
							// Do a begin invoke here so the page animation has time to finish,
							// and the view model loads state et.c. before showing the alert.
							// This gives a better UX experience.
							MainThread.BeginInvokeOnMainThread(async () =>
							{
								await ServiceRef.UiService.DisplayAlert(
									ServiceRef.Localizer[nameof(AppResources.YourLegalIdentity)], userMessage);
							});
						}
					}
				});
			}
		}

		/// <summary>
		/// Downloads the specified <see cref="LegalIdentity"/> and opens the corresponding page in the app to show it.
		/// </summary>
		/// <param name="LegalId">The id of the legal identity to show.</param>
		/// <param name="Purpose">The purpose to state if the identity can't be downloaded and needs to be petitioned instead.</param>
		public async Task OpenLegalIdentity(string LegalId, string Purpose)
		{
			try
			{
				LegalIdentity identity = await ServiceRef.XmppService.GetLegalIdentity(LegalId);
				MainThread.BeginInvokeOnMainThread(async () =>
				{
					await ServiceRef.UiService.GoToAsync(nameof(ViewIdentityPage), new ViewIdentityNavigationArgs(identity));
				});
			}
			catch (ForbiddenException)
			{
				// This happens if you try to view someone else's legal identity.
				// When this happens, try to send a petition to view it instead.
				// Normal operation. Should not be logged.

				MainThread.BeginInvokeOnMainThread(async () =>
				{
					bool succeeded = await ServiceRef.NetworkService.TryRequest(() => ServiceRef.XmppService.PetitionIdentity(LegalId, Guid.NewGuid().ToString(), Purpose));
					if (succeeded)
					{
						await ServiceRef.UiService.DisplayAlert(
							ServiceRef.Localizer[nameof(AppResources.PetitionSent)],
							ServiceRef.Localizer[nameof(AppResources.APetitionHasBeenSentToTheOwner)]);
					}
				});
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex, this.GetClassAndMethod(MethodBase.GetCurrentMethod()));
				await ServiceRef.UiService.DisplayException(ex);
			}
		}

		/// <summary>
		/// Tries to get a legal identity.
		/// </summary>
		/// <param name="LegalId">The id of the legal identity to show.</param>
		/// <param name="Purpose">The purpose to state if the identity can't be downloaded and needs to be petitioned instead.</param>
		/// <returns>Legal Identity, if possible to get, null otherwise.</returns>
		public async Task<LegalIdentity?> TryGetLegalIdentity(string LegalId, string Purpose)
		{
			try
			{
				LegalIdentity Identity = await ServiceRef.XmppService.GetLegalIdentity(LegalId);
				return Identity;
			}
			catch (ForbiddenException)
			{
				// This happens if you try to view someone else's legal identity.
				// When this happens, try to send a petition to view it instead.
				// Normal operation. Should not be logged.

				await ServiceRef.XmppService.PetitionIdentity(LegalId, Guid.NewGuid().ToString(), Purpose);
				return null;
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex, this.GetClassAndMethod(MethodBase.GetCurrentMethod()));
				return null;
			}
		}

		/// <summary>
		/// Downloads the specified <see cref="Contract"/> and opens the corresponding page in the app to show it.
		/// </summary>
		/// <param name="ContractId">The id of the contract to show.</param>
		/// <param name="Purpose">The purpose to state if the contract can't be downloaded and needs to be petitioned instead.</param>
		/// <param name="ParameterValues">Parameter values to set in new contract.</param>
		public async Task OpenContract(string ContractId, string Purpose, Dictionary<CaseInsensitiveString, object>? ParameterValues)
		{
			try
			{
				Contract Contract = await ServiceRef.XmppService.GetContract(ContractId);

				ContractReference Ref = await Database.FindFirstDeleteRest<ContractReference>(
					new FilterFieldEqualTo("ContractId", Contract.ContractId));

				if (Ref is not null)
				{
					if (Ref.Updated != Contract.Updated || !Ref.ContractLoaded)
					{
						await Ref.SetContract(Contract);
						await Database.Update(Ref);
					}

					ServiceRef.TagProfile.CheckContractReference(Ref);
				}

				MainThread.BeginInvokeOnMainThread(async () =>
				{
					if (Contract.PartsMode == ContractParts.TemplateOnly && Contract.State == ContractState.Approved)
					{
						if (Ref is null)
						{
							Ref = new()
							{
								ContractId = Contract.ContractId
							};

							await Ref.SetContract(Contract);
							await Database.Insert(Ref);

							ServiceRef.TagProfile.CheckContractReference(Ref);
						}
						if (Contract.ForMachinesNamespace == NeuroFeaturesClient.NamespaceNeuroFeatures
						|| Contract.ForMachinesNamespace == Constants.ContractMachineNames.PaymentInstructionsNamespace)
						{
							CreationAttributesEventArgs creationAttr = await ServiceRef.XmppService.GetNeuroFeatureCreationAttributes();
							ServiceRef.TagProfile.TrustProviderId = creationAttr.TrustProviderId;
							ParameterValues ??= [];
							ParameterValues.TryAdd(new CaseInsensitiveString("TrustProvider"), creationAttr.TrustProviderId);
						}

						NewContractNavigationArgs e = new(Contract, ParameterValues);

						await ServiceRef.UiService.GoToAsync(nameof(NewContractPage), e, BackMethod.CurrentPage);
					}
					else
					{
						ViewContractNavigationArgs e = new(Contract, false);

						await ServiceRef.UiService.GoToAsync(nameof(ViewContractPage), e, BackMethod.Pop);
					}
				});
			}
			catch (ForbiddenException)
			{
				// This happens if you try to view someone else's contract.
				// When this happens, try to send a petition to view it instead.
				// Normal operation. Should not be logged.

				MainThread.BeginInvokeOnMainThread(async () =>
				{
					bool succeeded = await ServiceRef.NetworkService.TryRequest(() =>
						ServiceRef.XmppService.PetitionContract(ContractId, Guid.NewGuid().ToString(), Purpose));

					if (succeeded)
					{
						await ServiceRef.UiService.DisplayAlert(ServiceRef.Localizer[nameof(AppResources.PetitionSent)],
							ServiceRef.Localizer[nameof(AppResources.APetitionHasBeenSentToTheContract)]);
					}
				});
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex, this.GetClassAndMethod(MethodBase.GetCurrentMethod()));
				await ServiceRef.UiService.DisplayException(ex);
			}
		}

		/// <summary>
		/// TAG Signature request scanned.
		/// </summary>
		/// <param name="Request">Request string.</param>
		public async Task TagSignature(string Request)
		{
			int i = Request.IndexOf(',');

			if (i < 0)
				throw new InvalidOperationException(ServiceRef.Localizer[nameof(AppResources.InvalidTagSignatureId)]);

			string JID = System.Web.HttpUtility.UrlDecode(Request[..i]);
			string Key = Request[(i + 1)..];

			LegalIdentity ID = (ServiceRef.TagProfile.LegalIdentity)
				?? throw new InvalidOperationException(ServiceRef.Localizer[nameof(AppResources.NoLegalIdSelected)]);

			if (ID.State != IdentityState.Approved)
				throw new InvalidOperationException(ServiceRef.Localizer[nameof(AppResources.LegalIdNotApproved)]);

			string IdRef = ServiceRef.TagProfile.LegalIdentity?.Id ?? string.Empty;

			if (!await App.AuthenticateUser(AuthenticationPurpose.TagSignature))
				return;

			StringBuilder Xml = new();

			Xml.Append("<ql xmlns='https://tagroot.io/schema/Signature' key='");
			Xml.Append(XML.Encode(Key));
			Xml.Append("' legalId='");
			Xml.Append(XML.Encode(IdRef));
			Xml.Append("'/>");

			if (!ServiceRef.XmppService.IsOnline &&
				!await ServiceRef.XmppService.WaitForConnectedState(TimeSpan.FromSeconds(10)))
			{
				throw new InvalidOperationException(ServiceRef.Localizer[nameof(AppResources.AppNotConnected)]);
			}

			await ServiceRef.XmppService.IqSetAsync(JID, Xml.ToString());
		}

		private async Task Contracts_SignaturePetitionResponseReceived(object? Sender, SignaturePetitionResponseEventArgs e)
		{
			try
			{
				LegalIdentity Identity = e.RequestedIdentity;

				if (!e.Response || Identity is null)
				{
					await ServiceRef.UiService.DisplayAlert(
						ServiceRef.Localizer[nameof(AppResources.Message)],
						ServiceRef.Localizer[nameof(AppResources.PetitionToViewLegalIdentityWasDenied)],
						ServiceRef.Localizer[nameof(AppResources.Ok)]);
				}
				else
					await ServiceRef.UiService.GoToAsync(nameof(ViewIdentityPage), new ViewIdentityNavigationArgs(Identity));
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
			}
		}
	}
}
