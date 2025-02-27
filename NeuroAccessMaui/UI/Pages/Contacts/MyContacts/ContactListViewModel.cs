using CommunityToolkit.Mvvm.ComponentModel;
using EDaler;
using EDaler.Uris;
using NeuroAccessMaui.Services;
using NeuroAccessMaui.Services.Notification;
using NeuroAccessMaui.Services.UI.QR;
using NeuroAccessMaui.UI.Pages.Contacts.Chat;
using NeuroAccessMaui.UI.Pages.Identity.ViewIdentity;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.ComponentModel;
using Waher.Networking.XMPP;
using Waher.Persistence;
using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services.Contacts;
using NeuroAccessMaui.UI.Pages.Wallet.Payment;
using NeuroAccessMaui.UI.Pages.Wallet;
using CommunityToolkit.Mvvm.Input;
using NeuroAccessMaui.Services.UI;
using Waher.Networking.XMPP.Events;

namespace NeuroAccessMaui.UI.Pages.Contacts.MyContacts
{
	/// <summary>
	/// The view model to bind to when displaying the list of contacts.
	/// </summary>
	public partial class ContactListViewModel : BaseViewModel
	{
		private readonly Dictionary<CaseInsensitiveString, List<ContactInfoModel>> byBareJid;
		private readonly TaskCompletionSource<ContactInfoModel?>? selection;
		private readonly ContactListNavigationArgs? navigationArguments;

		/// <summary>
		/// Creates an instance of the <see cref="ContactListViewModel"/> class.
		/// </summary>
		/// <param name="Args">Navigation arguments.</param>
		public ContactListViewModel(ContactListNavigationArgs? Args)
		{
			this.navigationArguments = Args;
			this.Contacts = [];
			this.byBareJid = [];

			if (Args is not null)
			{
				this.Description = Args.Description;
				this.Action = Args.Action;
				this.selection = Args.Selection;
				this.CanScanQrCode = Args.CanScanQrCode;
				this.AllowAnonymous = Args.AllowAnonymous;
				this.AnonymousText = string.IsNullOrEmpty(Args.AnonymousText) ?
					ServiceRef.Localizer[nameof(AppResources.Anonymous)] : Args.AnonymousText;
			}
			else
			{
				this.Description = ServiceRef.Localizer[nameof(AppResources.ContactsDescription)];
				this.Action = SelectContactAction.ViewIdentity;
				this.selection = null;
			}
		}

		/// <inheritdoc/>
		protected override async Task OnInitialize()
		{
			await base.OnInitialize();

			await this.UpdateContactList(this.navigationArguments?.Contacts);

			ServiceRef.XmppService.OnPresence += this.Xmpp_OnPresence;
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

			this.SelectedContact = null;
		}

		private async Task UpdateContactList(IEnumerable<ContactInfo>? Contacts)
		{
			SortedDictionary<CaseInsensitiveString, ContactInfo> Sorted = [];
			Dictionary<CaseInsensitiveString, bool> Jids = [];

			Contacts ??= await Database.Find<ContactInfo>();

			foreach (ContactInfo Info in Contacts)
			{
				Jids[Info.BareJid] = true;

				if (Info.IsThing.HasValue && Info.IsThing.Value)        // Include those with IsThing=null
					continue;

				if (Info.AllowSubscriptionFrom.HasValue && !Info.AllowSubscriptionFrom.Value)
					continue;

				Add(Sorted, Info.FriendlyName, Info);
			}

			foreach (RosterItem Item in ServiceRef.XmppService.Roster)
			{
				if (Jids.ContainsKey(Item.BareJid))
					continue;

				ContactInfo Info = new()
				{
					BareJid = Item.BareJid,
					FriendlyName = Item.NameOrBareJid,
					IsThing = null
				};

				await Database.Insert(Info);

				Add(Sorted, Info.FriendlyName, Info);
			}

			NotificationEvent[]? Events;

			this.Contacts.Clear();
			this.ShowContactsMissing = Sorted.Count == 0;

			foreach (CaseInsensitiveString Category in ServiceRef.NotificationService.GetCategories(NotificationEventType.Contacts))
			{
				if (ServiceRef.NotificationService.TryGetNotificationEvents(NotificationEventType.Contacts, Category, out Events))
				{
					if (Sorted.TryGetValue(Category, out ContactInfo? Info))
						Sorted.Remove(Category);
					else
					{
						Info = await ContactInfo.FindByBareJid(Category);

						if (Info is not null)
							Remove(Sorted, Info.FriendlyName, Info);
						else
						{
							Info = new()
							{
								BareJid = Category,
								FriendlyName = Category,
								IsThing = null
							};
						}
					}

					this.Contacts.Add(new ContactInfoModel(Info, Events));
				}
			}

			foreach (ContactInfo Info in Sorted.Values)
			{
				if (!ServiceRef.NotificationService.TryGetNotificationEvents(NotificationEventType.Contacts, Info.BareJid, out Events))
					Events = [];

				this.Contacts.Add(new ContactInfoModel(Info, Events));
			}

			this.byBareJid.Clear();

			foreach (ContactInfoModel? Contact in this.Contacts)
			{
				if (string.IsNullOrEmpty(Contact?.BareJid))
					continue;

				if (!this.byBareJid.TryGetValue(Contact.BareJid, out List<ContactInfoModel>? Contacts2))
				{
					Contacts2 = [];
					this.byBareJid[Contact.BareJid] = Contacts2;
				}

				Contacts2.Add(Contact);
			}
		}

		private static void Add(SortedDictionary<CaseInsensitiveString, ContactInfo> Sorted, CaseInsensitiveString Name, ContactInfo Info)
		{
			if (Sorted.ContainsKey(Name))
			{
				int i = 1;
				string Suffix;

				do
				{
					Suffix = " " + (++i).ToString(CultureInfo.InvariantCulture);
				}
				while (Sorted.ContainsKey(Name + Suffix));

				Sorted[Name + Suffix] = Info;
			}
			else
				Sorted[Name] = Info;
		}

		private static void Remove(SortedDictionary<CaseInsensitiveString, ContactInfo> Sorted, CaseInsensitiveString Name, ContactInfo Info)
		{
			int i = 1;
			string Suffix = string.Empty;

			while (Sorted.TryGetValue(Name + Suffix, out ContactInfo? Info2))
			{
				if (Info2.BareJid == Info.BareJid &&
					Info2.SourceId == Info.SourceId &&
					Info2.Partition == Info.Partition &&
					Info2.NodeId == Info.NodeId &&
					Info2.LegalId == Info.LegalId)
				{
					Sorted.Remove(Name + Suffix);

					i++;
					string Suffix2 = " " + i.ToString(CultureInfo.InvariantCulture);

					while (Sorted.TryGetValue(Name + Suffix2, out Info2))
					{
						Sorted[Name + Suffix] = Info2;
						Sorted.Remove(Name + Suffix2);

						i++;
						Suffix2 = " " + i.ToString(CultureInfo.InvariantCulture);
					}

					return;
				}

				i++;
				Suffix = " " + i.ToString(CultureInfo.InvariantCulture);
			}
		}

		/// <inheritdoc/>
		protected override Task OnDispose()
		{
			ServiceRef.XmppService.OnPresence -= this.Xmpp_OnPresence;
			ServiceRef.NotificationService.OnNewNotification -= this.NotificationService_OnNewNotification;
			ServiceRef.NotificationService.OnNotificationsDeleted -= this.NotificationService_OnNotificationsDeleted;

			if (this.Action != SelectContactAction.Select)
			{
				this.ShowContactsMissing = false;
				this.Contacts.Clear();
			}

			this.selection?.TrySetResult(this.SelectedContact);

			return base.OnDispose();
		}

		/// <summary>
		/// Gets or sets whether to show a contacts missing alert or not.
		/// </summary>
		[ObservableProperty]
		private bool showContactsMissing;

		/// <summary>
		/// The description to present to the user.
		/// </summary>
		[ObservableProperty]
		private string? description;

		/// <summary>
		/// The description to present to the user.
		/// </summary>
		[ObservableProperty]
		private bool canScanQrCode;

		/// <summary>
		/// The description to present to the user.
		/// </summary>
		[ObservableProperty]
		private bool allowAnonymous;

		/// <summary>
		/// The description to present to the user.
		/// </summary>
		[ObservableProperty]
		private string? anonymousText;

		/// <summary>
		/// The action to take when contact has been selected.
		/// </summary>
		[ObservableProperty]
		private SelectContactAction? action;

		/// <summary>
		/// Holds the list of contacts to display.
		/// </summary>
		public ObservableCollection<ContactInfoModel?> Contacts { get; }

		/// <summary>
		/// The currently selected contact, if any.
		/// </summary>
		[ObservableProperty]
		private ContactInfoModel? selectedContact;

		/// <inheritdoc/>
		protected override void OnPropertyChanged(PropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);

			switch (e.PropertyName)
			{
				case nameof(this.SelectedContact):
					ContactInfoModel? Contact = this.SelectedContact;

					if (Contact is not null)
					{
						MainThread.BeginInvokeOnMainThread(async () =>
						{
							this.IsOverlayVisible = true;

							try
							{
								switch (this.Action)
								{
									case SelectContactAction.MakePayment:
										StringBuilder sb = new();

										sb.Append("edaler:");

										if (ServiceRef.TagProfile.LegalIdentity is null)
										{
											sb.Append("f=");
											sb.Append(ServiceRef.XmppService.BareJid);
										}
										else
										{
											sb.Append("fi=");
											sb.Append(ServiceRef.TagProfile.LegalIdentity.Id);
										}

										if (!string.IsNullOrEmpty(Contact.LegalId))
										{
											sb.Append(";ti=");
											sb.Append(Contact.LegalId);
										}
										else if (!string.IsNullOrEmpty(Contact.BareJid))
										{
											sb.Append(";t=");
											sb.Append(Contact.BareJid);
										}

										Balance Balance = await ServiceRef.XmppService.GetEDalerBalance();

										sb.Append(";cu=");
										sb.Append(Balance.Currency);

										if (!EDalerUri.TryParse(sb.ToString(), out EDalerUri Parsed))
											break;

										EDalerUriNavigationArgs Args = new(Parsed);
										// Inherit the back method here from the parrent
										await ServiceRef.UiService.GoToAsync(nameof(PaymentPage), Args, BackMethod.Pop2);

										break;

									case SelectContactAction.ViewIdentity:
									default:
										if (Contact.LegalIdentity is not null)
										{
											ViewIdentityNavigationArgs ViewIdentityArgs = new(Contact.LegalIdentity);

											await ServiceRef.UiService.GoToAsync(nameof(ViewIdentityPage), ViewIdentityArgs);
										}
										else if (!string.IsNullOrEmpty(Contact.LegalId))
										{
											await ServiceRef.ContractOrchestratorService.OpenLegalIdentity(Contact.LegalId,
												ServiceRef.Localizer[nameof(AppResources.ScannedQrCode)]);
										}
										else if (!string.IsNullOrEmpty(Contact.BareJid) && Contact.Contact is not null)
										{
											ChatNavigationArgs ChatArgs = new(Contact.Contact);
											await ServiceRef.UiService.GoToAsync(nameof(ChatPage), ChatArgs, BackMethod.Inherited, Contact.BareJid);
										}

										break;

									case SelectContactAction.Select:
										this.SelectedContact = Contact;
										await this.GoBack();
										this.selection?.TrySetResult(Contact);
										break;
								}
							}
							finally
							{
								this.IsOverlayVisible = false;
							}
						});
					}
					break;
			}
		}

		/// <summary>
		/// Command executed when the user wants to scan a contact from a QR Code.
		/// </summary>
		[RelayCommand]
		private async Task ScanQrCode()
		{
			string? Code = await QrCode.ScanQrCode(ServiceRef.Localizer[nameof(AppResources.ScanQRCode)], [Constants.UriSchemes.IotId]);
			if (string.IsNullOrEmpty(Code))
				return;

			if (Constants.UriSchemes.StartsWithIdScheme(Code))
			{
				this.SelectedContact = new ContactInfoModel(new ContactInfo()
				{
					LegalId = Constants.UriSchemes.RemoveScheme(Code)
				});
			}
			else if (!string.IsNullOrEmpty(Code))
			{
				await ServiceRef.UiService.DisplayAlert(ServiceRef.Localizer[nameof(AppResources.ErrorTitle)],
					ServiceRef.Localizer[nameof(AppResources.TheSpecifiedCodeIsNotALegalIdentity)]);
			}
		}

		/// <summary>
		/// Command executed when the user wants to perform an action with someone anonymous.
		/// </summary>
		[RelayCommand]
		private void Anonymous()
		{
			this.SelectedContact = new ContactInfoModel(null, []);
		}

		private Task Xmpp_OnPresence(object? Sender, PresenceEventArgs e)
		{
			if (this.byBareJid.TryGetValue(e.FromBareJID, out List<ContactInfoModel>? Contacts))
			{
				foreach (ContactInfoModel Contact in Contacts)
					Contact.PresenceUpdated();
			}

			return Task.CompletedTask;
		}

		private Task NotificationService_OnNewNotification(object? Sender, NotificationEventArgs e)
		{
			if (e.Event.Type == NotificationEventType.Contacts)
				this.UpdateNotifications(e.Event.Category ?? string.Empty);

			return Task.CompletedTask;
		}

		private void UpdateNotifications()
		{
			foreach (CaseInsensitiveString Category in ServiceRef.NotificationService.GetCategories(NotificationEventType.Contacts))
				this.UpdateNotifications(Category);
		}

		private void UpdateNotifications(CaseInsensitiveString Category)
		{
			if (this.byBareJid.TryGetValue(Category, out List<ContactInfoModel>? Contacts))
			{
				if (!ServiceRef.NotificationService.TryGetNotificationEvents(NotificationEventType.Contacts, Category, out NotificationEvent[]? Events))
					Events = [];

				foreach (ContactInfoModel Contact in Contacts)
					Contact.NotificationsUpdated(Events);
			}
		}

		private Task NotificationService_OnNotificationsDeleted(object? Sender, NotificationEventsArgs e)
		{
			Dictionary<CaseInsensitiveString, bool> Categories = [];

			foreach (NotificationEvent Event in e.Events)
				Categories[Event.Category ?? string.Empty] = true;

			foreach (CaseInsensitiveString Category in Categories.Keys)
				this.UpdateNotifications(Category);

			return Task.CompletedTask;
		}
	}
}
