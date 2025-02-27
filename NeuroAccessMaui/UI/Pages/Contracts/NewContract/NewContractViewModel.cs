using CommunityToolkit.Maui.Layouts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeuroAccessMaui.Extensions;
using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services;
using NeuroAccessMaui.Services.Contacts;
using NeuroAccessMaui.Services.UI;
using NeuroAccessMaui.UI.Pages.Contracts.ViewContract;
using NeuroAccessMaui.UI.Pages.Contracts.MyContracts.ObjectModels;
using NeuroAccessMaui.UI.Pages.Contracts.NewContract.ObjectModel;
using NeuroAccessMaui.UI.Pages.Contracts.ObjectModel;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using Waher.Content;
using Waher.Networking.XMPP.Contracts;
using Waher.Script;
using Waher.Persistence;
using NeuroAccessMaui.Services.Contacts;
using NeuroAccessMaui.UI.Pages.Contracts.ViewContract;
using NeuroAccessMaui.Services.UI;

namespace NeuroAccessMaui.UI.Pages.Contracts.NewContract
{
	/// <summary>
	/// The view model to bind to when displaying a new contract view or page.
	/// </summary>
	public partial class NewContractViewModel : BaseViewModel, ILinkableView
	{
		#region Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="NewContractViewModel"/> class.
		/// </summary>
		public NewContractViewModel()
		{
			this.args = ServiceRef.UiService.PopLatestArgs<NewContractNavigationArgs>();

			this.SelectedContractVisibilityItem = this.ContractVisibilityItems[0];
		}

		#endregion

		#region Fields

		private readonly NewContractNavigationArgs? args;

		#endregion

		#region Properties

		[ObservableProperty]
		private ObservableContract? contract;

		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(GoToParametersCommand))]
		[NotifyCanExecuteChangedFor(nameof(BackCommand))]
		private bool canStateChange;

		[ObservableProperty]
		private string currentState = nameof(NewContractStep.Loading);

		[ObservableProperty]
		private string contractName = string.Empty;

		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(HasHumanReadableText))]
		private VerticalStackLayout? humanReadableText;

		public ObservableCollection<ObservableRole> AvailableRoles { get; set; } = [];

		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(CanCreate))]
		private ObservableRole? selectedRole;

		private ObservableRole? persistingSelectedRole;

		public ObservableCollection<ObservableParameter> EditableParameters { get; set; } = [];




		[ObservableProperty]
		private bool canAddParts;

		/// <summary>
		/// If HumanReadableText is not empty
		/// </summary>
		public bool HasHumanReadableText => this.HumanReadableText is not null;

		/// <summary>
		/// The state object containing all views. Is set by the view.
		/// </summary>
		public BindableObject? StateObject { get; set; }

		/// <summary>
		/// A list of valid visibility items to choose from for this contract.
		/// </summary>
		public ObservableCollection<ContractVisibilityModel> ContractVisibilityItems { get; } =
		  [
				new ContractVisibilityModel(ContractVisibility.CreatorAndParts, ServiceRef.Localizer[nameof(AppResources.ContractVisibility_CreatorAndParts)]),
				new ContractVisibilityModel(ContractVisibility.DomainAndParts, ServiceRef.Localizer[nameof(AppResources.ContractVisibility_DomainAndParts)]),
				new ContractVisibilityModel(ContractVisibility.Public, ServiceRef.Localizer[nameof(AppResources.ContractVisibility_Public)]),
				new ContractVisibilityModel(ContractVisibility.PublicSearchable, ServiceRef.Localizer[nameof(AppResources.ContractVisibility_PublicSearchable)])
		  ];

		/// <summary>
		/// The selected contract visibility item.
		/// </summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(CanCreate))]
		private ContractVisibilityModel? selectedContractVisibilityItem;


		public bool HasRoles => this.Contract is not null && this.Contract.Roles.Count > 0;

		public bool HasParameters => this.Contract is not null && this.EditableParameters.Count > 0;

		/// <summary>
		/// If the parameters are valid.
		/// </summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(CanCreate))]
		[NotifyCanExecuteChangedFor(nameof(CreateCommand))]
		private bool isParametersOk;

		/// <summary>
		/// If the roles are valid.
		/// </summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(CanCreate))]
		[NotifyCanExecuteChangedFor(nameof(CreateCommand))]
		private bool isRolesOk;

		/// <summary>
		/// If the user has reviewed the contract and sees it as valid.
		/// </summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(CanCreate))]
		[NotifyCanExecuteChangedFor(nameof(CreateCommand))]
		private bool isContractOk;

		/// <summary>
		/// If Contract can be created
		/// </summary>
		public bool CanCreate =>
			this.IsParametersOk
			&& this.IsRolesOk
			&& this.IsContractOk
			&& this.persistingSelectedRole is not null
			&& this.SelectedContractVisibilityItem is not null;

		#endregion

		#region Methods
		/// <inheritdoc/>
		protected override async Task OnInitialize()
		{

			await base.OnInitialize();

			if (this.args is null || this.args?.Template is null)
			{
				await ServiceRef.UiService.DisplayAlert(
					ServiceRef.Localizer[nameof(AppResources.Error)],
					ServiceRef.Localizer[nameof(AppResources.SomethingWentWrong)],
					ServiceRef.Localizer[nameof(AppResources.Ok)]);
				await this.GoBack();
				return;
			}

			try
			{
				this.Contract = await ObservableContract.CreateAsync(this.args.Template);
				this.Contract.ParameterChanged += this.Parameter_PropertyChanged;


				TaskCompletionSource<bool> HasInitializedParameters = new();

				MainThread.BeginInvokeOnMainThread(async () =>
				{
					if (this.args.ParameterValues is not null)
					{
						// Set the parameter values
						foreach (ObservableParameter p in this.Contract.Parameters)
						{
							if (this.args.ParameterValues.TryGetValue(p.Parameter.Name, out object? value))
								p.Value = value;


						}
						// Set Role values
						foreach (ObservableRole r in this.Contract.Roles)
						{
							if (this.args.ParameterValues.TryGetValue(r.Role.Name, out object? roleValue))
							{
								if (roleValue is string legalID)
									await r.AddPart(legalID);
							}
						}
					}
					foreach (ObservableParameter p in this.Contract.Parameters)
					{
						if (p.Parameter is BooleanParameter
							|| p.Parameter is StringParameter
							|| p.Parameter is NumericalParameter
							|| p.Parameter is DateParameter
							|| p.Parameter is DateTimeParameter
							|| p.Parameter is TimeParameter
							|| p.Parameter is DurationParameter)
						{
							this.EditableParameters.Add(p);
						}
					}
					this.OnPropertyChanged(nameof(this.HasRoles));
					this.OnPropertyChanged(nameof(this.HasParameters));

					HasInitializedParameters.SetResult(true);
				});
				await HasInitializedParameters.Task;
				await this.ValidateParametersAsync();
				await this.GoToState(NewContractStep.Overview);
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				await ServiceRef.UiService.DisplayAlert(
					ServiceRef.Localizer[nameof(AppResources.Error)],
					ServiceRef.Localizer[nameof(AppResources.SomethingWentWrong)],
					ServiceRef.Localizer[nameof(AppResources.Ok)]);
				await this.GoBack();
				// TODO: Handle error, perhaps change to an error state
			}
		}

		/// <inheritdoc/>
		protected override async Task OnDispose()
		{
			if (this.Contract is not null)
			{
				this.Contract.ParameterChanged -= this.Parameter_PropertyChanged;
			}
			await base.OnDispose();
		}

		/// <summary>
		/// Navigates to the specified state.
		/// Can only navigate when <see cref="CanStateChange"/> is true.
		/// Otherwise it stalls until it can navigate.
		/// </summary>
		/// <param name="newStep">The new step to navigate to.</param>
		private async Task GoToState(NewContractStep newStep)
		{
			if (this.StateObject is null)
				return;

			string newState = newStep.ToString();

			if (newState == this.CurrentState)
				return;

			while (!this.CanStateChange)
				await Task.Delay(100);

			await MainThread.InvokeOnMainThreadAsync(async () =>
			{
				await StateContainer.ChangeStateWithAnimation(this.StateObject, newState);
			});
		}

		/// <summary>
		/// Checks if the contract can be created based on the validity of parameters and roles.
		/// </summary>
		private void CheckCanCreate()
		{
			if (this.Contract is null)
				return;

			bool ParametersOk = true;
			foreach (ObservableParameter p in this.EditableParameters)
			{
				if (p.Value is null || !p.IsValid)
				{
					ParametersOk = false;
					break;
				}
			}

			bool RolesOk = true;
			foreach (ObservableRole role in this.Contract.Roles)
			{
				if (role.Parts.Count < role.MinCount)
				{
					RolesOk = false;
					break;
				}
			}

			MainThread.BeginInvokeOnMainThread(() =>
			{
				this.IsParametersOk = ParametersOk;
				this.IsRolesOk = RolesOk;
			});
		}

		/// <summary>
		/// Validates the parameters of the contract and updates their error states.
		/// </summary>
		private async Task ValidateParametersAsync()
		{
			if (this.Contract is null)
				return;

			ContractsClient client = ServiceRef.XmppService.ContractsClient;

			try
			{
				// Populate the parameters
				Variables v = [];

				foreach (ObservableParameter p in this.EditableParameters)
					p.Parameter.Populate(v);

				await MainThread.InvokeOnMainThreadAsync(async () =>
				{
					try
					{
						foreach (ObservableParameter p in this.EditableParameters)
						{
							if (p.Value is null)
								p.IsValid = false;
							else
								p.IsValid = await p.Parameter.IsParameterValid(v, client);
							p.ValidationText = p.Parameter.ErrorText;
						}
					}
					catch (Exception ex)
					{
						ServiceRef.LogService.LogException(ex);
					}
				});
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
			}
		}
		#endregion

		#region Commands

		[RelayCommand(CanExecute = nameof(CanCreate), AllowConcurrentExecutions = false)]
		private async Task CreateAsync()
		{
			if (this.Contract is null)
				return;

			await this.GoToState(NewContractStep.Loading);

			if(!await App.AuthenticateUserAsync(AuthenticationPurpose.SignContract, true))
			{
				await this.GoToOverview();
				return;
			}

			ContractsClient client = ServiceRef.XmppService.ContractsClient;

			Contract? CreatedContract = null;
			List<Part> Parts = [];
			foreach (ObservableRole Role in this.Contract.Roles)
			{
				foreach (ObservablePart Part in Role.Parts)
				{
					Parts.Add(Part.Part);
				}
			}

			try
			{
				CreatedContract = await client.CreateContractAsync(
					this.Contract.Contract.ContractId,
					[.. Parts],
					this.Contract.Contract.Parameters,
					this.SelectedContractVisibilityItem?.Visibility ?? this.Contract.Visibility,
					ContractParts.ExplicitlyDefined,
					this.Contract.Contract.Duration ?? Duration.FromYears(1),
					this.Contract.Contract.ArchiveRequired ?? Duration.FromYears(5),
					this.Contract.Contract.ArchiveOptional ?? Duration.FromYears(5),
					null, null, false);
				CreatedContract = await ServiceRef.XmppService.SignContract(CreatedContract, this.persistingSelectedRole!.Name, false);

				foreach (Part Part in Parts)
				{
					if (this.args?.SuppressedProposalLegalIds is not null && Array.IndexOf<CaseInsensitiveString>(this.args.SuppressedProposalLegalIds, Part.LegalId) >= 0)
						continue;

					ContactInfo Info = await ContactInfo.FindByLegalId(Part.LegalId);
					if (Info is null || string.IsNullOrEmpty(Info.BareJid))
						continue;

					await ServiceRef.XmppService.ContractsClient.AuthorizeAccessToContractAsync(CreatedContract.ContractId, Info.BareJid, true);

					string? Proposal = await ServiceRef.UiService.DisplayPrompt(ServiceRef.Localizer[nameof(AppResources.Proposal)],
						ServiceRef.Localizer[nameof(AppResources.EnterProposal), Info.FriendlyName],
						ServiceRef.Localizer[nameof(AppResources.Send)],
						ServiceRef.Localizer[nameof(AppResources.Cancel)]);

					if (!string.IsNullOrEmpty(Proposal))
						await ServiceRef.XmppService.SendContractProposal(CreatedContract, Part.Role, Info.BareJid, Proposal);
				}
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
			}

			if (CreatedContract is null)
			{
				await ServiceRef.UiService.DisplayAlert(
					ServiceRef.Localizer[nameof(AppResources.Error)],
					ServiceRef.Localizer[nameof(AppResources.SomethingWentWrong)],
					ServiceRef.Localizer[nameof(AppResources.Ok)]);
				await this.GoToOverview();
				return;
			}

			ViewContractNavigationArgs Args = new(CreatedContract, false);
			await ServiceRef.UiService.GoToAsync(nameof(ViewContractPage), Args, BackMethod.Pop3);

		}


		/// <summary>
		/// A custom back command, similar to inherited GoBack with Views in mind
		/// </summary>
		/// <returns></returns>
		[RelayCommand(CanExecute = nameof(CanStateChange))]
		public async Task Back()
		{
			try
			{
				NewContractStep currentStep = (NewContractStep)Enum.Parse(typeof(NewContractStep), this.CurrentState);

				switch (currentStep)
				{
					case NewContractStep.Loading:
					case NewContractStep.Overview:
						await base.GoBack();
						break;
					case NewContractStep.Roles:
						this.persistingSelectedRole = this.SelectedRole;
						await this.GoToOverview();
						break;
					default:
						await this.GoToState(NewContractStep.Overview);
						this.CheckCanCreate();
						break;
				}
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
			}
		}

		/// <summary>
		/// Navigates to the parameters view
		/// </summary>
		[RelayCommand(CanExecute = nameof(CanStateChange))]
		private async Task GoToParameters()
		{
			await this.GoToState(NewContractStep.Loading);
			await this.GoToState(NewContractStep.Parameters);
		}

		/// <summary>
		/// Navigates to the roles view and restores SelectedRole
		/// </summary>
		[RelayCommand(CanExecute = nameof(CanStateChange))]
		private async Task GoToRoles()
		{
			await this.GoToState(NewContractStep.Loading);
			//		this.FilterAvailableRoles();
			await this.GoToState(NewContractStep.Roles);
			if (this.persistingSelectedRole is not null)
				this.SelectedRole = this.persistingSelectedRole;
			else
			{
				ObservableRole? AvailableRole = null;
				foreach (ObservableRole Role in this.Contract?.Roles ?? [])
				{
					if (Role.Parts.Count < Role.MaxCount)
					{
						if (AvailableRole is null)
							AvailableRole = Role;
						else
							return;
					}
				}
				this.SelectedRole = AvailableRole;
			}

		}

		/// <summary>
		/// Navigates to the overview view and performs logic to check if create conditions are met
		/// </summary>
		private async Task GoToOverview()
		{
			await this.GoToState(NewContractStep.Loading);
			this.CheckCanCreate();
			await this.GoToState(NewContractStep.Overview);
		}

		/// <summary>
		/// Loads the humand readable part of the contract and navigates to the Preview view
		/// </summary>
		/// <returns></returns>
		[RelayCommand(CanExecute = nameof(CanStateChange))]
		private async Task GoToPreview()
		{
			if (this.Contract is null)
				return;

			await this.GoToState(NewContractStep.Loading);

			string hrt = await this.Contract.Contract.ToMauiXaml(this.Contract.Contract.DeviceLanguage());
			VerticalStackLayout hrtLayout = new VerticalStackLayout().LoadFromXaml(hrt);
			await MainThread.InvokeOnMainThreadAsync(() =>
			{
				this.HumanReadableText = hrtLayout;
			});

			await this.GoToState(NewContractStep.Preview);
		}

		#endregion

		#region Event Handlers

		/// <summary>
		/// Event handler for when a parameter changes.
		/// Validates the parameter.
		/// </summary>
		private async void Parameter_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			await this.ValidateParametersAsync();
		}

		partial void OnSelectedRoleChanged(ObservableRole? oldValue, ObservableRole? newValue)
		{
			if (newValue is null)
				return;

			ObservableRole? MyRole = null;
			foreach (ObservableRole Role in this.Contract?.Roles ?? [])
			{
				foreach (ObservablePart Part in Role.Parts)
				{
					if (Part.IsMe)
						MyRole = Role;
				}
			}
			MainThread.BeginInvokeOnMainThread(async () =>
			{
				string? MyLegalID = ServiceRef.TagProfile.LegalIdentity?.Id;
				if (string.IsNullOrEmpty(MyLegalID))
				{
					await ServiceRef.UiService.DisplayAlert(
						ServiceRef.Localizer[nameof(AppResources.Error)],
						ServiceRef.Localizer[nameof(AppResources.NoLegalIdSelected)],
						ServiceRef.Localizer[nameof(AppResources.Ok)]);
					return;
				}

				if (newValue.Parts.Count < newValue.MaxCount)
				{
					MyRole?.RemovePart(MyLegalID, false);
					newValue?.AddPart(MyLegalID, false);
				}
				else if (MyRole != newValue)
				{
					await ServiceRef.UiService.DisplayAlert(
						ServiceRef.Localizer[nameof(AppResources.Error)],
						ServiceRef.Localizer[nameof(AppResources.SelectedRoleHasReachedMaximumNumberOfParts)],
						ServiceRef.Localizer[nameof(AppResources.Ok)]);
					this.SelectedRole = MyRole;
				}
			});
		}

		partial void OnSelectedContractVisibilityItemChanged(ContractVisibilityModel? oldValue, ContractVisibilityModel? newValue)
		{
			//Fixes losing value when switching view
			if (newValue is null)
			{
				this.SelectedContractVisibilityItem = oldValue;
				return;
			}
		}

		#endregion

		#region Interface Implementations

		/// <inheritdoc/>
		public bool IsLinkable => true;

		/// <inheritdoc/>
		public bool EncodeAppLinks => true;

		/// <inheritdoc/>
		public string Link
		{
			get
			{
				StringBuilder url = new();
				//bool first = true;

				url.Append(Constants.UriSchemes.IotSc);
				url.Append(':');
				//	url.Append(this.template?.ContractId);

				// TODO: Define and initialize 'parametersByName' if necessary
				// foreach (KeyValuePair<CaseInsensitiveString, ParameterInfo> p in this.parametersByName)
				// {
				//     if (first)
				//     {
				//         first = false;
				//         url.Append('&');
				//     }
				//     else
				//     {
				//         url.Append('?');
				//     }

				//     url.Append(p.Key);
				//     url.Append('=');

				//     if (p.Value.Control is Entry entry)
				//         url.Append(entry.Text);
				//     else if (p.Value.Control is CheckBox checkBox)
				//         url.Append(checkBox.IsChecked ? '1' : '0');
				//     else if (p.Value.Control is ExtendedDatePicker picker)
				//     {
				//         if (p.Value.Parameter is DateParameter)
				//             url.Append(XML.Encode(picker.Date, true));
				//         else
				//             url.Append(XML.Encode(picker.Date, false));
				//     }
				//     else
				//     {
				//         url.Append(p.Value.Parameter.ObjectValue?.ToString());
				//     }
				// }

				return url.ToString();
			}
		}

		/// <inheritdoc/>
		public Task<string> Title => ContractModel.GetName(this.Contract?.Contract);

		/// <inheritdoc/>
		public bool HasMedia => false;

		/// <inheritdoc/>
		public byte[]? Media => null;

		/// <inheritdoc/>
		public string? MediaContentType => null;

		#endregion
	}
}
