using CommunityToolkit.Maui.Layouts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeuroAccessMaui.Extensions;
using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services;
using NeuroAccessMaui.Services.Contacts;
using NeuroAccessMaui.Services.UI.Photos;
using NeuroAccessMaui.UI.Converters;
using NeuroAccessMaui.UI.Pages.Contacts.Chat;
using NeuroAccessMaui.UI.Pages.Contracts.MyContracts.ObjectModels;
using NeuroAccessMaui.UI.Pages.Contracts.NewContract;
using NeuroAccessMaui.UI.Pages.Contracts.ObjectModel;
using NeuroAccessMaui.UI.Pages.Main.QR;
using NeuroAccessMaui.UI.Pages.Signatures.ClientSignature;
using NeuroAccessMaui.UI.Pages.Signatures.ServerSignature;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text;
using Waher.Content;
using Waher.Networking.XMPP.Contracts;
using Waher.Networking.XMPP.Contracts.EventArguments;
using Waher.Networking.XMPP.HttpFileUpload;
using Waher.Script;

namespace NeuroAccessMaui.UI.Pages.Contracts.ViewContract
{
	/// <summary>
	/// View model for displaying and managing a contract in the "View Contract" page.
	/// </summary>
	public partial class ViewContractViewModel : QrXmppViewModel
	{
		#region Fields

		private readonly ViewContractNavigationArgs? args;

		#endregion

		#region Constructor and Initialization

		/// <summary>
		/// Default constructor. Retrieves navigation arguments and sets up the view model for displaying a contract.
		/// </summary>
		public ViewContractViewModel()
		{
			this.args = ServiceRef.UiService.PopLatestArgs<ViewContractNavigationArgs>();

			this.XmppUriClicked = new Command(async Parameter => await this.ExecuteUriClicked(Parameter as string ?? "", UriScheme.Xmpp));
			this.IotIdUriClicked = new Command(async Parameter => await this.ExecuteUriClicked(Parameter as string ?? "", UriScheme.IotId));
			this.IotScUriClicked = new Command(async Parameter => await this.ExecuteUriClicked(Parameter as string ?? "", UriScheme.IotSc));
			this.NeuroFeatureUriClicked = new Command(async Parameter => await this.ExecuteUriClicked(Parameter as string ?? "", UriScheme.NeuroFeature));
			this.IotDiscoUriClicked = new Command(async Parameter => await this.ExecuteUriClicked(Parameter as string ?? "", UriScheme.IotDisco));
			this.EDalerUriClicked = new Command(async Parameter => await this.ExecuteUriClicked(Parameter as string ?? "", UriScheme.EDaler));
			this.HyperlinkClicked = new Command(async Parameter => await this.ExecuteHyperlinkClicked(Parameter));
		}

		/// <inheritdoc/>
		protected override async Task OnInitialize()
		{
			await base.OnInitialize();

			// No valid contract arguments
			if (this.args is null || this.args.Contract is null)
				return; // TODO: Consider changing view state to Error.

			Console.WriteLine("INITIALIZING");

			ServiceRef.XmppService.ContractUpdated += this.ContractsClient_ContractUpdated;
			ServiceRef.XmppService.ContractSigned += this.ContractsClient_ContractSigned;
			this.SignableRoles.CollectionChanged += this.SignableRoles_CollectionChanged;

			try
			{
				// Create an observable contract wrapper
				ObservableContract contract = await ObservableContract.CreateAsync(this.args.Contract);

				// If proposal info is available, try to find a friendly name
				string proposalFriendlyName = await this.ResolveProposalFriendlyName();

				// Update UI properties on the main threadRe
				await MainThread.InvokeOnMainThreadAsync(() =>
				{
					this.Contract = contract;
					this.GenerateQrCode(Constants.UriSchemes.CreateSmartContractUri(this.Contract.ContractId));
					this.ProposalFriendlyName = proposalFriendlyName;
					this.ProposalRole = this.args.Role ?? string.Empty;
					this.ProposalMessage = this.args.Proposal ?? string.Empty;
				});

				// Prepare displayable parameters
				await MainThread.InvokeOnMainThreadAsync(() =>
				{
					this.PrepareDisplayableParameters();
				});

				// Prepare roles that can be signed
				await MainThread.InvokeOnMainThreadAsync(() =>
				{
					this.PrepareSignableRoles();
				});

				// Move to the initial "Overview" state
				await this.GoToState(ViewContractStep.Overview);
			}
			catch (Exception ex)
			{
				// Logging error and informing user
				ServiceRef.LogService.LogException(ex);
				await ServiceRef.UiService.DisplayAlert(
					ServiceRef.Localizer[nameof(AppResources.ErrorTitle)],
					ServiceRef.Localizer[nameof(AppResources.SomethingWentWrong)]);

				await this.GoBack();
			}
		}




		/// <inheritdoc/>
		protected override async Task OnDispose()
		{
			ServiceRef.XmppService.ContractUpdated -= this.ContractsClient_ContractUpdated;
			ServiceRef.XmppService.ContractSigned -= this.ContractsClient_ContractSigned;
			this.SignableRoles.CollectionChanged -= this.SignableRoles_CollectionChanged;
			await base.OnDispose();
		}

		#endregion

		#region Properties

		protected override void OnPropertyChanged(PropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);

			if (e.PropertyName == nameof(this.IsBusy))
			{
				this.OnPropertyChanged(nameof(this.CanSign));
			}
		}

		/// <summary>
		/// The current contract displayed by the view model.
		/// </summary>
		[ObservableProperty]
		private ObservableContract? contract;

		/// <summary>
		/// The state object containing all views. Is set by the view.
		/// </summary>
		public BindableObject? StateObject { get; set; }

		/// <summary>
		/// If the view can change state.
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(GoToParametersCommand))]
		[NotifyCanExecuteChangedFor(nameof(BackCommand))]
		private bool canStateChange;

		/// <summary>
		/// The current state of the view.
		/// </summary>
		[ObservableProperty]
		private string currentState = nameof(NewContractStep.Loading);

		/// <summary>
		/// The human-readable representation of the contract.
		/// </summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(HasHumanReadableText))]
		private VerticalStackLayout? humanReadableText;

		/// <summary>
		/// Indicates if the human readable text is present.
		/// </summary>
		public bool HasHumanReadableText => this.HumanReadableText is not null;

		/// <summary>
		/// All parameters that can be displayed and are supported.
		/// </summary>
		public ObservableCollection<ObservableParameter> DisplayableParameters { get; set; } = new();

		/// <summary>
		/// Indicates if the user has reviewed the contract and deems it valid.
		/// </summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(ReadyToSign))]
		[NotifyCanExecuteChangedFor(nameof(SignCommand))]
		private bool isContractOk;

		/// <summary>
		/// The friendly name of the proposal sender.
		/// </summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(HasProposalFriendlyName))]
		[NotifyPropertyChangedFor(nameof(IsProposal))]
		private string? proposalFriendlyName;

		/// <summary>
		/// The role specified in the proposal.
		/// </summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(HasProposalRole))]
		private string? proposalRole;

		/// <summary>
		/// The message included in the proposal.
		/// </summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(HasProposalMessage))]
		[NotifyPropertyChangedFor(nameof(IsProposal))]
		private string? proposalMessage;

		/// <summary>
		/// Indicates whether we are viewing the contract as a proposal.
		/// </summary>
		public bool IsProposal =>
			!string.IsNullOrEmpty(this.ProposalRole) ||
			!string.IsNullOrEmpty(this.ProposalMessage) ||
			!string.IsNullOrEmpty(this.ProposalFriendlyName);

		/// <summary>
		/// Indicates if the proposal sender has a friendly name.
		/// </summary>
		public bool HasProposalFriendlyName => !string.IsNullOrEmpty(this.ProposalFriendlyName);

		/// <summary>
		/// Indicates if the proposal has a role specified.
		/// </summary>
		public bool HasProposalRole => !string.IsNullOrEmpty(this.ProposalRole);

		/// <summary>
		/// Indicates if the proposal has a message.
		/// </summary>
		public bool HasProposalMessage => !string.IsNullOrEmpty(this.ProposalMessage);

		/// <summary>
		/// String representation of the contract visibility.
		/// </summary>
		public string? Visibility => this.Contract?.Visibility switch
		{
			ContractVisibility.Public => ServiceRef.Localizer[nameof(AppResources.ContractVisibility_Public)],
			ContractVisibility.CreatorAndParts => ServiceRef.Localizer[nameof(AppResources.ContractVisibility_CreatorAndParts)],
			ContractVisibility.DomainAndParts => ServiceRef.Localizer[nameof(AppResources.ContractVisibility_DomainAndParts)],
			ContractVisibility.PublicSearchable => ServiceRef.Localizer[nameof(AppResources.ContractVisibility_PublicSearchable)],
			_ => string.Empty
		};

		#endregion

		#region Signing

		/// <summary>
		/// A collection of roles that the current user can sign.
		/// </summary>
		public ObservableCollection<ObservableRole> SignableRoles { get; set; } = new();

		/// <summary>
		/// Indicates if the user can sign the contract in the current state.
		/// </summary>
		public bool CanSign =>
			this.IsBusy == false &&
			this.SignableRoles.Count > 0 &&
			(this.Contract?.ContractState == ContractState.Approved || this.Contract?.ContractState == ContractState.BeingSigned) &&
			this.Contract?.Roles.Any(r => r.Parts.Any(p => p.IsMe && p.HasSigned)) is false;

		public bool ReadyToSign =>
			this.SelectedRole is not null &&
			this.IsContractOk;

		/// <summary>
		/// The currently selected role for signing.
		/// </summary>
		[ObservableProperty]
		private ObservableRole? selectedRole;

		/// <summary>
		/// Invoked when the selected signing role changes.
		/// Ensures the user is added or removed as a part to the appropriate role.
		/// </summary>
		/// <param name="oldValue">Previously selected role.</param>
		/// <param name="newValue">Newly selected role.</param>
		async partial void OnSelectedRoleChanged(ObservableRole? oldValue, ObservableRole? newValue)
		{
			string? myLegalID = ServiceRef.TagProfile.LegalIdentity?.Id;
			if (string.IsNullOrEmpty(myLegalID))
				return;

			if (oldValue is not null)
				oldValue.RemovePart(myLegalID);

			if (newValue is not null)
				await newValue.AddPart(myLegalID);
			this.OnPropertyChanged(nameof(this.ReadyToSign));
			this.SignCommand.NotifyCanExecuteChanged();
		}

		[RelayCommand(AllowConcurrentExecutions = false, CanExecute = nameof(ReadyToSign))]
		public async Task SignAsync()
		{
			if (this.Contract is null || this.SelectedRole is null)
				return;

			await MainThread.InvokeOnMainThreadAsync(() =>
			{
				this.SetIsBusy(true);
			});

			string Role = this.SelectedRole.Name;

			try
			{
				await this.GoToState(ViewContractStep.Overview);
				await ServiceRef.XmppService.SignContract(this.Contract.Contract, Role, false);
				await this.RefreshContract(null);
			}
			catch (Exception)
			{
				await ServiceRef.UiService.DisplayAlert(
					ServiceRef.Localizer[nameof(AppResources.ErrorTitle)],
					ServiceRef.Localizer[nameof(AppResources.SomethingWentWrong)]);
			}
			finally
			{
				await MainThread.InvokeOnMainThreadAsync(() =>
				{
					this.SetIsBusy(false);
				});
			}
		}

		#endregion

		#region Navigation Commands

		/// <summary>
		/// Returns to the previous state or page depending on the current step.
		/// </summary>
		[RelayCommand(CanExecute = nameof(CanStateChange))]
		public async Task Back()
		{
			try
			{
				ViewContractStep currentStep = (ViewContractStep)Enum.Parse(typeof(ViewContractStep), this.CurrentState);

				switch (currentStep)
				{
					case ViewContractStep.Loading:
					case ViewContractStep.Overview:
						// If we're at Overview or Loading, just go back in navigation
						await base.GoBack();
						break;

					default:
						// Otherwise, return to the Overview state
						await this.GoToState(ViewContractStep.Overview);
						break;
				}
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
			}
		}

		/// <summary>
		/// Navigates to the Parameters state.
		/// </summary>
		[RelayCommand(CanExecute = nameof(CanStateChange))]
		private async Task GoToParameters()
		{
			await this.GoToState(ViewContractStep.Loading);
			await this.GoToState(ViewContractStep.Parameters);
		}

		/// <summary>
		/// Navigates to the Roles state.
		/// </summary>
		[RelayCommand(CanExecute = nameof(CanStateChange))]
		private async Task GoToRoles()
		{
			await this.GoToState(ViewContractStep.Loading);
			await this.GoToState(ViewContractStep.Roles);
		}

		/// <summary>
		/// Navigates to the Sign state.
		/// If a proposal role is defined, that role is preselected for signing.
		/// </summary>
		[RelayCommand(CanExecute = nameof(CanStateChange))]
		private async Task GoToSign()
		{
			await this.GoToState(ViewContractStep.Loading);
			await this.GoToState(ViewContractStep.Sign);

			// Preselect the role if there is a proposal role or only one signable role
			if (!string.IsNullOrEmpty(this.ProposalRole))
			{
				this.SelectedRole = this.SignableRoles.FirstOrDefault(r => r.Name == this.ProposalRole);
			}
			else if (this.SignableRoles.Count == 1)
			{
				this.SelectedRole = this.SignableRoles[0];
			}
		}

		/// <summary>
		/// Loads the humand readable part of the contract and navigates to the Preview view
		/// </summary>
		/// <returns></returns>
		[RelayCommand(CanExecute = nameof(CanStateChange))]
		private async Task GoToReview()
		{
			if (this.Contract is null)
				return;

			await this.GoToState(ViewContractStep.Loading);

			string hrt = await this.Contract.Contract.ToMauiXaml(this.Contract.Contract.DeviceLanguage());
			VerticalStackLayout hrtLayout = new VerticalStackLayout().LoadFromXaml(hrt);
			await MainThread.InvokeOnMainThreadAsync(() =>
			{
				this.HumanReadableText = hrtLayout;
			});

			await this.GoToState(ViewContractStep.Review);
		}

		[RelayCommand]
		private async Task OpenServerSignatureAsync()
		{
			Contract? Contract = this.Contract?.Contract;
			if (Contract is null)
				return;
			await ServiceRef.UiService.GoToAsync(nameof(ServerSignaturePage), new ServerSignatureNavigationArgs(Contract), Services.UI.BackMethod.Pop);
		}

		#endregion

		#region Contract Management Commands

		/// <summary>
		/// Marks the current contract as obsolete.
		/// </summary>
		[RelayCommand]
		private async Task ObsoleteContract()
		{
			if (this.Contract is null)
				return;

			try
			{
				if (!await AreYouSure(ServiceRef.Localizer[nameof(AppResources.AreYouSureYouWantToObsoleteContract)]))
					return;

				if (!await App.AuthenticateUserAsync(AuthenticationPurpose.ObsoleteContract, true))
					return;

				await ServiceRef.XmppService.ObsoleteContract(this.Contract.ContractId);

				await ServiceRef.UiService.DisplayAlert(
					ServiceRef.Localizer[nameof(AppResources.SuccessTitle)],
					ServiceRef.Localizer[nameof(AppResources.ContractHasBeenObsoleted)]);
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				await ServiceRef.UiService.DisplayException(ex);
			}
		}

		/// <summary>
		/// Deletes the current contract.
		/// </summary>
		[RelayCommand]
		private async Task DeleteContract()
		{
			if (this.Contract is null)
				return;

			try
			{
				if (!await AreYouSure(ServiceRef.Localizer[nameof(AppResources.AreYouSureYouWantToDeleteContract)]))
					return;

				if (!await App.AuthenticateUserAsync(AuthenticationPurpose.DeleteContract, true))
					return;

				await ServiceRef.XmppService.DeleteContract(this.Contract.ContractId);

				await ServiceRef.UiService.DisplayAlert(
					ServiceRef.Localizer[nameof(AppResources.SuccessTitle)],
					ServiceRef.Localizer[nameof(AppResources.ContractHasBeenDeleted)]);
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				await ServiceRef.UiService.DisplayException(ex);
			}
		}

		/// <summary>
		/// Shows machine-readable contract details by uploading them to a server and providing a link.
		/// </summary>
		[RelayCommand]
		private async Task ShowDetails()
		{
			if (this.Contract is null)
				return;

			try
			{
				byte[] bin = Encoding.UTF8.GetBytes(this.Contract.Contract.ForMachines.OuterXml);
				HttpFileUploadEventArgs e = await ServiceRef.XmppService.RequestUploadSlotAsync(
					this.Contract.ContractId + ".xml", "text/xml; charset=utf-8", bin.Length);

				if (e.Ok)
				{
					await e.PUT(bin, "text/xml", (int)Constants.Timeouts.UploadFile.TotalMilliseconds);
					if (!await App.OpenUrlAsync(e.GetUrl, false))
						await this.Copy(e.GetUrl);
				}
				else
				{
					// Show error if upload slot was not granted
					await ServiceRef.UiService.DisplayException(
						e.StanzaError ?? new Exception(e.ErrorText));
				}
			}
			catch (Exception ex)
			{
				await ServiceRef.UiService.DisplayException(ex);
			}
		}

		#endregion

		#region Clipboard and Link Commands

		/// <summary>
		/// Copies the specified item to the clipboard.
		/// If the item is the contract ID, a success message specific to contract ID copying is shown.
		/// </summary>
		[RelayCommand]
		private async Task Copy(object item)
		{
			try
			{
				this.SetIsBusy(true);

				if (item is string label)
				{
					if (label == this.Contract?.ContractId)
					{
						await Clipboard.SetTextAsync(Constants.UriSchemes.IotSc + ":" + this.Contract.ContractId);
						await ServiceRef.UiService.DisplayAlert(
							ServiceRef.Localizer[nameof(AppResources.SuccessTitle)],
							ServiceRef.Localizer[nameof(AppResources.ContractIdCopiedSuccessfully)]);
					}
					else
					{
						await Clipboard.SetTextAsync(label);
						await ServiceRef.UiService.DisplayAlert(
							ServiceRef.Localizer[nameof(AppResources.SuccessTitle)],
							ServiceRef.Localizer[nameof(AppResources.TagValueCopiedToClipboard)]);
					}
				}
				else
				{
					await Clipboard.SetTextAsync(item?.ToString() ?? string.Empty);
					await ServiceRef.UiService.DisplayAlert(
						ServiceRef.Localizer[nameof(AppResources.SuccessTitle)],
						ServiceRef.Localizer[nameof(AppResources.TagValueCopiedToClipboard)]);
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
			}
		}

		/// <summary>
		/// Opens a contract link if it’s a known contract ID, otherwise copies it to the clipboard.
		/// </summary>
		[RelayCommand]
		private static async Task OpenContract(object item)
		{
			if (item is string contractId)
				await App.OpenUrlAsync(Constants.UriSchemes.IotSc + ":" + contractId);
		}

		/// <summary>
		/// Opens a link if possible, otherwise copies it to the clipboard.
		/// </summary>
		[RelayCommand]
		private async Task OpenLink(object item)
		{
			if (item is string url)
			{
				if (!await App.OpenUrlAsync(url, false))
					await this.Copy(url);
			}
			else
				await this.Copy(item);
		}

		#endregion

		#region State Navigation Helpers

		/// <summary>
		/// Navigates to the specified state. If <see cref="CanStateChange"/> is false, waits until state change is possible.
		/// </summary>
		/// <param name="newStep">The new step to navigate to.</param>
		private async Task GoToState(ViewContractStep newStep)
		{
			if (this.StateObject is null)
				return;

			string newState = newStep.ToString();

			if (newState == this.CurrentState)
				return;

			// Wait until the state can change
			while (!this.CanStateChange)
				await Task.Delay(100);

			await MainThread.InvokeOnMainThreadAsync(async () =>
			{
				await StateContainer.ChangeStateWithAnimation(this.StateObject, newState);
			});
		}

		#endregion

		#region Event Handlers

		/// <summary>
		/// Handles changes in the signable roles collection, triggering UI updates.
		/// </summary>
		private void SignableRoles_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			MainThread.BeginInvokeOnMainThread(() => this.OnPropertyChanged(nameof(this.CanSign)));
		}

		private async Task ContractsClient_ContractSigned(object? Sender, ContractSignedEventArgs e)
		{
			if (e.ContractId != this.Contract?.ContractId || e.LegalId == ServiceRef.TagProfile.LegalIdentity?.Id)
				return;

			//Wait for RefreshContractCommand to finish, before calling it
			while (!this.RefreshContractCommand.CanExecute(null))
			{
				await Task.Delay(100);
			}

			await this.RefreshContractCommand.ExecuteAsync(null);
		}

		private async Task ContractsClient_ContractUpdated(object Sender, ContractReferenceEventArgs e)
		{
			if (e.ContractId != this.Contract?.ContractId)
				return;

			//Wait for RefreshContractCommand to finish, before calling it
			while (!this.RefreshContractCommand.CanExecute(null))
			{
				await Task.Delay(100);
			}

			await this.RefreshContractCommand.ExecuteAsync(null);
		}

		#endregion

		#region Private Helper Methods

		[RelayCommand(AllowConcurrentExecutions = false)]
		private async Task RefreshContract(Contract? NewContract)
		{
			if (this.Contract is null)
				return;

			try
			{
				NewContract ??= await ServiceRef.XmppService.GetContract(this.Contract.ContractId);
			}
			catch (Exception)
			{
				// Ignore and continue with the current contract
			}

			// If the contract is the same, do nothing
			if (NewContract is null || NewContract.ServerSignature.Timestamp == this.Contract.Contract.ServerSignature.Timestamp)
				return;

			ObservableContract? NewContractWrapper = new(NewContract);


			ViewContractStep currentStep = (ViewContractStep)Enum.Parse(typeof(ViewContractStep), this.CurrentState);
			await this.GoToState(ViewContractStep.Loading);

			await MainThread.InvokeOnMainThreadAsync(async () =>
			{
				this.SelectedRole = null;
				this.Contract = NewContractWrapper;
				await this.Contract.InitializeAsync();
			});

			await MainThread.InvokeOnMainThreadAsync(() =>
			{
				this.PrepareDisplayableParameters();
				this.PrepareSignableRoles();
			});

			await this.GoToState(currentStep);
		}

		/// <summary>
		/// Attempts to resolve the friendly name of the proposal sender if available.
		/// </summary>
		private async Task<string> ResolveProposalFriendlyName()
		{
			if (string.IsNullOrEmpty(this.args?.Proposal) || string.IsNullOrEmpty(this.args?.FromJID))
				return string.Empty;

			try
			{
				ContactInfo? info = await ContactInfo.FindByBareJid(this.args.FromJID);
				if (!string.IsNullOrEmpty(info?.FriendlyName))
					return info.FriendlyName;

				return this.args.FromJID;
			}
			catch
			{
				// If name cannot be resolved, ignore and just return empty.
				return string.Empty;
			}
		}

		/// <summary>
		/// Prepares the displayable parameters based on supported parameter types.
		/// </summary>
		private void PrepareDisplayableParameters()
		{
			this.DisplayableParameters.Clear();

			if (this.Contract?.Parameters is null)
				return;

			Variables Variables = new();

			foreach (ObservableParameter p in this.Contract.Parameters)
			{
				p.Parameter.Populate(Variables);
				if (p.Parameter is BooleanParameter
					|| p.Parameter is StringParameter
					|| p.Parameter is NumericalParameter
					|| p.Parameter is DateParameter
					|| p.Parameter is TimeParameter
					|| p.Parameter is DurationParameter
					|| p.Parameter is DateTimeParameter
					|| p.Parameter is CalcParameter)
				{
					this.DisplayableParameters.Add(p);
				}
			}

			//This evaluates all CalcParameters
			foreach(ObservableParameter p in this.DisplayableParameters)
			{
				p.Parameter.IsParameterValid(Variables, ServiceRef.XmppService.ContractsClient); 
			}
		}

		/// <summary>
		/// Prepares the signable roles available to the user based on the contract and proposal conditions.
		/// </summary>
		private void PrepareSignableRoles()
		{
			if (this.Contract is null)
				return;

			this.SignableRoles.Clear();

			// If a proposal role is specified, only allow signing as that role (if available)
			if (!string.IsNullOrEmpty(this.ProposalRole))
			{
				ObservableRole? role = this.Contract?.Roles.FirstOrDefault(r => r.Name == this.ProposalRole);
				if (role is not null && !role.HasReachedMaxCount)
					this.SignableRoles.Add(role);
			}
			else if (this.Contract?.Contract.PartsMode == ContractParts.Open)
			{
				// If contract is open, allow signing any appropriate role
				foreach (ObservableRole r in this.Contract.Roles)
				{
					if (!r.HasReachedMaxCount)
						this.SignableRoles.Add(r);
				}
			}
			else
			{
				// Add Role of which you are defined as a part
				foreach (ObservableRole r in this.Contract!.Roles)
				{
					if (r.Parts.Any(p => p.IsMe))
						this.SignableRoles.Add(r);
				}
			}
		}

		#endregion

		#region ILinkableView Implementation

		/// <inheritdoc/>
		public override string? Link { get; }

		/// <inheritdoc/>
		public override Task<string> Title => ContractModel.GetName(this.Contract?.Contract);

		#endregion

		#region Markdown Link handelers
		/// <summary>
		/// Command executed when a multi-media-link with the xmpp URI scheme is clicked.
		/// </summary>
		public Command XmppUriClicked { get; }

		/// <summary>
		/// Command executed when a multi-media-link with the iotid URI scheme is clicked.
		/// </summary>
		public Command IotIdUriClicked { get; }

		/// <summary>
		/// Command executed when a multi-media-link with the iotsc URI scheme is clicked.
		/// </summary>
		public Command IotScUriClicked { get; }

		/// <summary>
		/// Command executed when a multi-media-link with the nfeat URI scheme is clicked.
		/// </summary>
		public Command NeuroFeatureUriClicked { get; }

		/// <summary>
		/// Command executed when a multi-media-link with the iotdisco URI scheme is clicked.
		/// </summary>
		public Command IotDiscoUriClicked { get; }

		/// <summary>
		/// Command executed when a multi-media-link with the edaler URI scheme is clicked.
		/// </summary>
		public Command EDalerUriClicked { get; }

		/// <summary>
		/// Command executed when a hyperlink in rendered markdown has been clicked.
		/// </summary>
		public Command HyperlinkClicked { get; }

		private async Task ExecuteUriClicked(object Parameter, UriScheme Scheme)
		{
			if (Parameter is not string Uri)
				return;
			await App.OpenUrlAsync(Uri);
		}

		private async Task ExecuteHyperlinkClicked(object Parameter)
		{
			if (Parameter is not string Url)
				return;

			await App.OpenUrlAsync(Url);
		}

		#endregion
	}
}
