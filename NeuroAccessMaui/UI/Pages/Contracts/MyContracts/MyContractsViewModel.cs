using NeuroAccessMaui.UI.Pages.Contracts.MyContracts.ObjectModels;
using NeuroAccessMaui.UI.Pages.Contracts.NewContract;
using NeuroAccessMaui.UI.Pages.Contracts.ViewContract;
using NeuroAccessMaui.Services.Contracts;
using NeuroAccessMaui.Services.Notification;
using NeuroAccessMaui.Services.Notification.Contracts;
using Waher.Networking.XMPP.Contracts;
using Waher.Persistence;
using Waher.Persistence.Filters;
using NeuroAccessMaui.Services;
using NeuroAccessMaui.Resources.Languages;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NeuroAccessMaui.Services.UI;
using Microsoft.Maui.Controls.Shapes;
using Waher.Networking.XMPP.StanzaErrors;

namespace NeuroAccessMaui.UI.Pages.Contracts.MyContracts
{
	/// <summary>
	/// The view model to bind to when displaying 'my' contracts.
	/// </summary>
	public partial class MyContractsViewModel : BaseViewModel
	{
		private readonly Dictionary<string, Contract> contractsMap = [];
		private readonly ContractsListMode contractsListMode;
		private readonly TaskCompletionSource<Contract?>? selection;
		private Contract? selectedContract = null;

		/// <summary>
		/// Creates an instance of the <see cref="MyContractsViewModel"/> class.
		/// </summary>
		/// <param name="Args">Navigation arguments.</param>
		public MyContractsViewModel(MyContractsNavigationArgs? Args)
		{
			this.IsBusy = true;
			this.Action = SelectContractAction.ViewContract;

			if (Args is not null)
			{
				this.contractsListMode = Args.Mode;
				this.Action = Args.Action;
				this.selection = Args.Selection;

				switch (this.contractsListMode)
				{
					case ContractsListMode.Contracts:
						this.Title = ServiceRef.Localizer[nameof(AppResources.Contracts)];
						this.Description = ServiceRef.Localizer[nameof(AppResources.ContractsInfoText)];
						break;

					case ContractsListMode.ContractTemplates:
						this.Title = ServiceRef.Localizer[nameof(AppResources.ContractTemplates)];
						this.Description = ServiceRef.Localizer[nameof(AppResources.ContractTemplatesInfoText)];
						break;

					case ContractsListMode.TokenCreationTemplates:
						this.Title = ServiceRef.Localizer[nameof(AppResources.TokenCreationTemplates)];
						this.Description = ServiceRef.Localizer[nameof(AppResources.TokenCreationTemplatesInfoText)];
						break;
				}
			}
		}

		/// <inheritdoc/>
		protected override async Task OnInitialize()
		{
			await base.OnInitialize();

			this.IsBusy = true;
			this.ShowContractsMissing = false;

			await this.LoadContracts();

			ServiceRef.NotificationService.OnNewNotification += this.NotificationService_OnNewNotification;
			ServiceRef.NotificationService.OnNotificationsDeleted += this.NotificationService_OnNotificationsDeleted;
		}

		/// <inheritdoc/>
		protected override async Task OnAppearing()
		{
			await base.OnAppearing();

			if (this.selection is not null && this.selection.Task.IsCompleted)
			{
				await this.GoBack();
				return;
			}
		}

		/// <inheritdoc/>
		protected override async Task OnDispose()
		{
			ServiceRef.NotificationService.OnNewNotification -= this.NotificationService_OnNewNotification;
			ServiceRef.NotificationService.OnNotificationsDeleted -= this.NotificationService_OnNotificationsDeleted;

			if (this.Action != SelectContractAction.Select)
			{
				this.ShowContractsMissing = false;
				this.contractsMap.Clear();
			}

			this.selection?.TrySetResult(this.selectedContract);

			await base.OnDispose();
		}

		/// <summary>
		/// Gets or sets the title for the view displaying contracts.
		/// </summary>
		[ObservableProperty]
		private string? title;

		/// <summary>
		/// Gets or sets the introductory text for the view displaying contracts.
		/// </summary>
		[ObservableProperty]
		private string? description;

		/// <summary>
		/// The action to take when contact has been selected.
		/// </summary>
		[ObservableProperty]
		private SelectContractAction action;

		/// <summary>
		/// Gets or sets whether to show a contracts missing alert or not.
		/// </summary>
		[ObservableProperty]
		private bool showContractsMissing;

		/// <summary>
		/// Holds the list of contracts to display, ordered by category.
		/// </summary>
		public ObservableCollection<IUniqueItem> Categories { get; } = [];

		/// <summary>
		/// Add or remove the contracts from the collection
		/// </summary>
		public void AddOrRemoveContracts(HeaderModel Category, bool Expanded)
		{
			MainThread.BeginInvokeOnMainThread(() =>
			{
				if (Expanded)
				{
					int Index = this.Categories.IndexOf(Category);

					foreach (ContractModel Contract in Category.Contracts)
						this.Categories.Insert(++Index, Contract);
				}
				else
				{
					foreach (ContractModel Contract in Category.Contracts)
						this.Categories.Remove(Contract);
				}
			});
		}
		/// <summary>
		/// Add or remove the contracts from the collection
		/// </summary>
		public void ContractSelected(string ContractId)
		{
			MainThread.BeginInvokeOnMainThread(async () =>
			{
				if (this.contractsMap.TryGetValue(ContractId, out Contract? Contract))
				{
					switch (this.Action)
					{
						case SelectContractAction.ViewContract:
							if (this.contractsListMode == ContractsListMode.Contracts)
							{
								try
								{
									Contract updatedContract = await ServiceRef.XmppService.GetContract(ContractId);
									ViewContractNavigationArgs Args = new(updatedContract, false);
									await ServiceRef.UiService.GoToAsync(nameof(ViewContractPage), Args, BackMethod.Pop);
								}
								catch (ItemNotFoundException)
								{
									if(await ServiceRef.UiService.DisplayAlert(
										ServiceRef.Localizer[nameof(AppResources.ErrorTitle)], ServiceRef.Localizer[nameof(AppResources.ContractCouldNotBeFound)],
										ServiceRef.Localizer[nameof(AppResources.Yes)],
										ServiceRef.Localizer[nameof(AppResources.No)]))
									{
										await Database.FindDelete<ContractReference>(new FilterFieldEqualTo("ContractId", ContractId));
										await this.LoadContracts();
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
							else
							{
								await ServiceRef.ContractOrchestratorService.OpenContract(ContractId, ServiceRef.Localizer[nameof(AppResources.ReferencedID)], null);
								//NewContractNavigationArgs Args = new(Contract, null);
								//await ServiceRef.UiService.GoToAsync(nameof(NewContractPage), Args, BackMethod.CurrentPage);
							}
							break;

						case SelectContractAction.Select:
							this.selectedContract = Contract;
							await this.GoBack();
							this.selection?.TrySetResult(Contract);
							break;
					}
				}
			});
		}

		private async Task LoadContracts()
		{
			try
			{
				IEnumerable<ContractReference> ContractReferences;
				bool ShowAdditionalEvents;
				Contract? Contract;

				switch (this.contractsListMode)
				{
					case ContractsListMode.Contracts:
						ContractReferences = await Database.Find<ContractReference>(new FilterAnd(
							new FilterFieldEqualTo("IsTemplate", false),
							new FilterFieldEqualTo("ContractLoaded", true)));

						ShowAdditionalEvents = true;
						break;

					case ContractsListMode.ContractTemplates:
						ContractReferences = await Database.Find<ContractReference>(new FilterAnd(
							new FilterFieldEqualTo("IsTemplate", true),
							new FilterFieldEqualTo("ContractLoaded", true)));

						ShowAdditionalEvents = false;
						break;

					case ContractsListMode.TokenCreationTemplates:
						ContractReferences = await Database.Find<ContractReference>(new FilterAnd(
							new FilterFieldEqualTo("IsTemplate", true),
							new FilterFieldEqualTo("ContractLoaded", true)));

						Dictionary<CaseInsensitiveString, bool> ContractIds = [];
						LinkedList<ContractReference> TokenCreationTemplates = [];

						foreach (ContractReference Ref in ContractReferences)
						{
							if (Ref.IsTokenCreationTemplate && Ref.ContractId is not null)
							{
								ContractIds[Ref.ContractId] = true;
								TokenCreationTemplates.AddLast(Ref);
							}
						}

						foreach (string TokenTemplateId in Constants.ContractTemplates.TokenCreationTemplates)
						{
							if (!ContractIds.ContainsKey(TokenTemplateId))
							{
								Contract = await ServiceRef.XmppService.GetContract(TokenTemplateId);
								if (Contract is not null)
								{
									ContractReference Ref = new()
									{
										ContractId = Contract.ContractId
									};

									await Ref.SetContract(Contract);
									await Database.Insert(Ref);

									ServiceRef.TagProfile.CheckContractReference(Ref);

									if (Ref.IsTokenCreationTemplate)
									{
										ContractIds[Ref.ContractId] = true;
										TokenCreationTemplates.AddLast(Ref);
									}
								}
							}
						}

						ContractReferences = TokenCreationTemplates;
						ShowAdditionalEvents = false;
						break;

					default:
						return;
				}

				SortedDictionary<string, List<ContractModel>> ContractsByCategory = new(StringComparer.CurrentCultureIgnoreCase);
				SortedDictionary<CaseInsensitiveString, NotificationEvent[]> EventsByCategory = ServiceRef.NotificationService.GetEventsByCategory(NotificationEventType.Contracts);
				bool Found = false;

				foreach (ContractReference Ref in ContractReferences)
				{
					if (Ref.ContractId is null)
						continue;

					Found = true;

					try
					{
						Contract = await Ref.GetContract();
						if (Contract is null)
							continue;
					}
					catch (Exception ex)
					{
						ServiceRef.LogService.LogException(ex);
						continue;
					}

					this.contractsMap[Ref.ContractId] = Contract;

					if (EventsByCategory.TryGetValue(Ref.ContractId, out NotificationEvent[]? Events))
					{
						EventsByCategory.Remove(Ref.ContractId);

						List<NotificationEvent> Events2 = [];
						List<NotificationEvent>? Petitions = null;

						foreach (NotificationEvent Event in Events)
						{
							if (Event is ContractPetitionNotificationEvent Petition)
							{
								Petitions ??= [];
								Petitions.Add(Petition);
							}
							else
								Events2.Add(Event);
						}

						if (Petitions is not null)
							EventsByCategory[Ref.ContractId] = [.. Petitions];

						Events = [.. Events2];
					}
					else
						Events = [];

					ContractModel Item = await ContractModel.Create(Ref.ContractId, Ref.Created, Contract, Events);
					string Category = Item.Category;

					if (!ContractsByCategory.TryGetValue(Category, out List<ContractModel>? Contracts2))
					{
						Contracts2 = [];
						ContractsByCategory[Category] = Contracts2;
					}

					Contracts2.Add(Item);
				}

				List<IUniqueItem> NewCategories = [];

				if (ShowAdditionalEvents)
				{
					foreach (KeyValuePair<CaseInsensitiveString, NotificationEvent[]> P in EventsByCategory)
					{
						foreach (NotificationEvent Event in P.Value)
						{
							Geometry Icon = await Event.GetCategoryIcon();
							string Description = await Event.GetDescription();

							NewCategories.Add(new EventModel(Event.Received, Icon, Description, Event));
						}
					}
				}

				foreach (KeyValuePair<string, List<ContractModel>> P in ContractsByCategory)
				{
					int Nr = 0;

					foreach (ContractModel Model in P.Value)
						Nr += Model.NrEvents;

					P.Value.Sort(new DateTimeDesc());
					NewCategories.Add(new HeaderModel(P.Key, P.Value.ToArray(), Nr));
				}

				MainThread.BeginInvokeOnMainThread(() =>
				{
					this.Categories.Clear();

					foreach (IUniqueItem Item in NewCategories)
						this.Categories.Add(Item);

					this.ShowContractsMissing = !Found;
				});
			}
			finally
			{
				this.IsBusy = false;
			}
		}

		private class DateTimeDesc : IComparer<ContractModel>
		{
			public int Compare(ContractModel? x, ContractModel? y)
			{
				if (x is null)
				{
					if (y is null)
						return 0;
					else
						return -1;
				}
				else if (y is null)
					return 1;
				else
					return y.Timestamp.CompareTo(x.Timestamp);
			}
		}

		private Task NotificationService_OnNotificationsDeleted(object? Sender, NotificationEventsArgs e)
		{
			MainThread.BeginInvokeOnMainThread(() =>
			{
				foreach (NotificationEvent Event in e.Events)
				{
					if (Event.Type != NotificationEventType.Contracts)
						continue;

					HeaderModel? LastHeader = null;

					foreach (IUniqueItem Group in this.Categories)
					{
						if (Group is HeaderModel Header)
							LastHeader = Header;
						else if (Group is ContractModel Contract && Contract.ContractId == Event.Category)
						{
							if (Contract.RemoveEvent(Event) && LastHeader is not null)
								LastHeader.NrEvents--;

							break;
						}
					}
				}
			});

			return Task.CompletedTask;
		}

		private Task NotificationService_OnNewNotification(object? Sender, NotificationEventArgs e)
		{
			if (e.Event.Type == NotificationEventType.Contracts)
			{
				MainThread.BeginInvokeOnMainThread(() =>
				{
					HeaderModel? LastHeader = null;

					foreach (IUniqueItem Group in this.Categories)
					{
						if (Group is HeaderModel Header)
							LastHeader = Header;
						else if (Group is ContractModel Contract && Contract.ContractId == e.Event.Category)
						{
							if (Contract.AddEvent(e.Event) && LastHeader is not null)
								LastHeader.NrEvents++;

							break;
						}
					}
				});
			}

			return Task.CompletedTask;
		}
	}
}
