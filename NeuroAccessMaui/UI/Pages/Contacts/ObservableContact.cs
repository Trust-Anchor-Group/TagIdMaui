using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services;
using NeuroAccessMaui.Services.Contacts;
using NeuroAccessMaui.Services.Notification;
using NeuroAccessMaui.UI.Popups.Xmpp.RemoveSubscription;
using NeuroAccessMaui.UI.Popups.Xmpp.SubscribeTo;
using Waher.Networking.XMPP;
using Waher.Networking.XMPP.Contracts;
using Waher.Persistence;

namespace NeuroAccessMaui.UI.Pages.Contacts
{
	/// <summary>
	/// An observable object that wraps a <see cref="ContactInfo"/> object.
	/// This allows for easier binding in the UI.
	/// Either create instances with <see cref="CreateAsync"/> or initialize with <see cref="InitializeAsync"/>.
	/// </summary>
	partial class ObservableContact : ObservableObject
	{
		#region Constructors and Destructor

		private ObservableContact(ContactInfo contact)
		{
			this.Contact = contact;

			// Initialize collections if needed
			this.MetaData = new ObservableCollection<Property>(this.Contact.MetaData ?? Enumerable.Empty<Property>());
			this.MetaData.CollectionChanged += this.MetaData_CollectionChanged;

			this.Events = [];
			this.Events.CollectionChanged += this.Events_CollectionChanged;
		}

		#endregion

		#region Initialization

		/// <summary>
		/// Creates a new instance of <see cref="ObservableContact"/> and initializes it.
		/// </summary>
		/// <param name="contact">The ContactInfo object to wrap.</param>
		public static async Task<ObservableContact> CreateAsync(ContactInfo contact)
		{
			ObservableContact contactWrapper = new(contact);
			await contactWrapper.InitializeAsync();
			return contactWrapper;
		}

		public static async Task<ObservableContact> CreateAsync(string bareJid)
		{
			try
			{
				ContactInfo? Contact = await ContactInfo.FindByBareJid(bareJid);
				Contact ??= new ContactInfo()
				{
					BareJid = bareJid
				};

				return await CreateAsync(Contact);
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				await ServiceRef.UiService.DisplayException(ex);
				return new ObservableContact(new ContactInfo());
			}



		}

		/// <summary>
		/// Initializes the contact data.
		/// </summary>
		private async Task InitializeAsync()
		{
			// Perform any asynchronous initialization here if necessary
			// For example, loading additional data or setting up event handlers

			bool ContactExists = await ContactInfo.FindByBareJid(this.BareJid) is not null;

			await MainThread.InvokeOnMainThreadAsync(() =>
			{
				this.CanAddContact = !ContactExists;
				this.CanRemoveContact = ContactExists;
			});
		}

		#endregion

		#region Collection Changed Handlers

		private void MetaData_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			// Synchronize Contact.MetaData with MetaData collection
			this.Contact.MetaData = [.. this.MetaData];
		}

		private void Events_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			// Notify changes for HasEvents and NrEvents properties
			this.OnPropertyChanged(nameof(this.HasEvents));
			this.OnPropertyChanged(nameof(this.NrEvents));
		}

		#endregion

		#region Properties

		/// <summary>
		/// The wrapped ContactInfo object.
		/// </summary>
		public ContactInfo Contact { get; }

		/// <summary>
		/// Friendly name of the contact.
		/// </summary>
		public string FriendlyName
		{
			get
			{
				if (!string.IsNullOrEmpty(this.Alias))
					return this.Alias;

				string name = ContactInfo.GetFriendlyName(this.LegalIdentity);
				if (!string.IsNullOrEmpty(name))
					return name;

				return this.BareJid;
			}
		}

		/// <summary>
		/// Alias of the contact (User defined name).
		/// </summary>
		public string Alias
		{
			get => this.Contact.Alias;
			set
			{
				if (this.Contact.Alias != value)
				{
					this.Contact.Alias = value;
					this.OnPropertyChanged();
					this.OnPropertyChanged(nameof(this.FriendlyName));
				}
			}
		}

		/// <summary>
		/// Bare JID of the contact.
		/// </summary>
		public string BareJid
		{
			get => this.Contact.BareJid;
			set
			{
				if (this.Contact.BareJid != value)
				{
					this.Contact.BareJid = value;
					this.OnPropertyChanged();
					this.ToggleSubscriptionCommand.NotifyCanExecuteChanged();
					this.OnPropertyChanged(nameof(this.FriendlyName));
				}
			}
		}

		/// <summary>
		/// Legal ID of the contact.
		/// </summary>
		public string LegalId
		{
			get => this.Contact.LegalId;
			set
			{
				if (this.Contact.LegalId != value)
				{
					this.Contact.LegalId = value;
					this.OnPropertyChanged();
				}
			}
		}

		/// <summary>
		/// Legal Identity of the contact.
		/// </summary>
		public LegalIdentity? LegalIdentity
		{
			get => this.Contact.LegalIdentity;
			set
			{
				if (this.Contact.LegalIdentity != value)
				{
					this.Contact.LegalIdentity = value;
					this.OnPropertyChanged();
					this.OnPropertyChanged(nameof(this.FriendlyName));
				}
			}
		}

		/// <summary>
		/// Meta-data of the contact.
		/// </summary>
		public ObservableCollection<Property> MetaData { get; }

		/// <summary>
		/// Notification events associated with the contact.
		/// </summary>
		public ObservableCollection<NotificationEvent> Events { get; }

		/// <summary>
		/// If the contact has associated events.
		/// </summary>
		public bool HasEvents => this.Events.Count > 0;

		/// <summary>
		/// Number of events associated with the contact.
		/// </summary>
		public int NrEvents => this.Events.Count;

		/// <summary>
		/// Subscribe to this contact.
		/// </summary>
		public bool? SubscribeTo
		{
			get => this.Contact.SubscribeTo;
			set
			{
				if (this.Contact.SubscribeTo != value)
				{
					this.Contact.SubscribeTo = value;
					this.OnPropertyChanged();
				}
			}
		}

		/// <summary>
		/// Allow subscriptions from this contact.
		/// </summary>
		public bool? AllowSubscriptionFrom
		{
			get => this.Contact.AllowSubscriptionFrom;
			set
			{
				if (this.Contact.AllowSubscriptionFrom != value)
				{
					this.Contact.AllowSubscriptionFrom = value;
					this.OnPropertyChanged();
				}
			}
		}

		/// <summary>
		/// The contact is a thing.
		/// </summary>
		public bool? IsThing
		{
			get => this.Contact.IsThing;
			set
			{
				if (this.Contact.IsThing != value)
				{
					this.Contact.IsThing = value;
					this.OnPropertyChanged();
				}
			}
		}

		/// <summary>
		/// The contact is a sensor.
		/// </summary>
		public bool? IsSensor
		{
			get => this.Contact.IsSensor;
			set
			{
				if (this.Contact.IsSensor != value)
				{
					this.Contact.IsSensor = value;
					this.OnPropertyChanged();
				}
			}
		}

		/// <summary>
		/// The contact supports sensor events.
		/// </summary>
		public bool? SupportsSensorEvents
		{
			get => this.Contact.SupportsSensorEvents;
			set
			{
				if (this.Contact.SupportsSensorEvents != value)
				{
					this.Contact.SupportsSensorEvents = value;
					this.OnPropertyChanged();
				}
			}
		}

		/// <summary>
		/// The contact is an actuator.
		/// </summary>
		public bool? IsActuator
		{
			get => this.Contact.IsActuator;
			set
			{
				if (this.Contact.IsActuator != value)
				{
					this.Contact.IsActuator = value;
					this.OnPropertyChanged();
				}
			}
		}

		/// <summary>
		/// The contact is a concentrator.
		/// </summary>
		public bool? IsConcentrator
		{
			get => this.Contact.IsConcentrator;
			set
			{
				if (this.Contact.IsConcentrator != value)
				{
					this.Contact.IsConcentrator = value;
					this.OnPropertyChanged();
				}
			}
		}

		/// <summary>
		/// If the account is registered as the owner of the thing.
		/// </summary>
		public bool? Owner
		{
			get => this.Contact.Owner;
			set
			{
				if (this.Contact.Owner != value)
				{
					this.Contact.Owner = value;
					this.OnPropertyChanged();
				}
			}
		}

		/// <summary>
		/// Source ID.
		/// </summary>
		public string SourceId
		{
			get => this.Contact.SourceId;
			set
			{
				if (this.Contact.SourceId != value)
				{
					this.Contact.SourceId = value;
					this.OnPropertyChanged();
				}
			}
		}

		/// <summary>
		/// Partition.
		/// </summary>
		public string Partition
		{
			get => this.Contact.Partition;
			set
			{
				if (this.Contact.Partition != value)
				{
					this.Contact.Partition = value;
					this.OnPropertyChanged();
				}
			}
		}

		/// <summary>
		/// Node ID.
		/// </summary>
		public string NodeId
		{
			get => this.Contact.NodeId;
			set
			{
				if (this.Contact.NodeId != value)
				{
					this.Contact.NodeId = value;
					this.OnPropertyChanged();
				}
			}
		}

		/// <summary>
		/// Registry JID.
		/// </summary>
		public string RegistryJid
		{
			get => this.Contact.RegistryJid;
			set
			{
				if (this.Contact.RegistryJid != value)
				{
					this.Contact.RegistryJid = value;
					this.OnPropertyChanged();
				}
			}
		}

		#endregion

		#region Methods
		/// <summary>
		/// Adds a notification event.
		/// </summary>
		/// <param name="evt">Notification event.</param>
		public void AddEvent(NotificationEvent evt)
		{
			if (!this.Events.Any(e => e.ObjectId == evt.ObjectId))
			{
				this.Events.Add(evt);
			}
		}

		/// <summary>
		/// Removes a notification event.
		/// </summary>
		/// <param name="evt">Notification event.</param>
		public void RemoveEvent(NotificationEvent evt)
		{
			NotificationEvent? existingEvent = this.Events.FirstOrDefault(e => e.ObjectId == evt.ObjectId);
			if (existingEvent != null)
			{
				this.Events.Remove(existingEvent);
			}
		}
		#endregion // Methods

		#region Commands
		// Indicates whether the AddContact command can execute
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(AddContactCommand))]
		private bool canAddContact;

		// Indicates whether the RemoveContact command can execute
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(RemoveContactCommand))]
		private bool canRemoveContact;

		/// <summary>
		/// Command to add the contact.
		/// </summary>
		[RelayCommand(CanExecute = nameof(CanAddContact))]
		private async Task AddContactAsync()
		{
			if (this.BareJid is null)
				return;

			try
			{
				this.Contact.FriendlyName = ContactInfo.GetFriendlyName(this.LegalIdentity);
				// Add to roster if not already present
				RosterItem? item = ServiceRef.XmppService.GetRosterItem(this.BareJid);
				if (item is null)
					ServiceRef.XmppService.AddRosterItem(new RosterItem(this.BareJid, this.FriendlyName));

				// Update or insert ContactInfo in the database
				ContactInfo info = await ContactInfo.FindByBareJid(this.BareJid);
				if (info is null)
					await Database.Insert(this.Contact);
				else
					await Database.Update(this.Contact);

				if (this.LegalId is not null)
					await ServiceRef.AttachmentCacheService.MakePermanent(this.LegalId!);
				await Database.Provider.Flush();

				MainThread.BeginInvokeOnMainThread(() =>
				{
					this.CanAddContact = false;
					this.CanRemoveContact = true;
				});
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				await ServiceRef.UiService.DisplayException(ex);
			}
		}

		/// <summary>
		/// Command to remove the contact.
		/// </summary>
		[RelayCommand(CanExecute = nameof(CanRemoveContact))]
		private async Task RemoveContactAsync()
		{
			if (string.IsNullOrEmpty(this.BareJid))
				return;

			try
			{
				ContactInfo info = await ContactInfo.FindByBareJid(this.BareJid);
				if (info is null)
					return;

				await Database.Delete(info);
				ServiceRef.XmppService.RemoveRosterItem(info.BareJid);

				// Update command states
				MainThread.BeginInvokeOnMainThread(() =>
				{
					this.CanAddContact = true;
					this.CanRemoveContact = false;
				});
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				await ServiceRef.UiService.DisplayException(ex);
			}
		}

		/// <summary>
		/// If the contact can be subscribed to or unsubscribed from.
		/// </summary>
		public bool CanToggleSubscription => !string.IsNullOrEmpty(this.BareJid);

		/// <summary>
		/// Command to toggle subscription to the contact.
		/// </summary>
		[RelayCommand(CanExecute = nameof(CanToggleSubscription))]
		private async Task ToggleSubscriptionAsync()
		{
			if (string.IsNullOrEmpty(this.BareJid))
				return;

			RosterItem? item;
			try
			{
				item = ServiceRef.XmppService.GetRosterItem(this.BareJid);
			}
			catch
			{
				await ServiceRef.UiService.DisplayAlert(ServiceRef.Localizer[nameof(AppResources.Error)],
					 ServiceRef.Localizer[nameof(AppResources.NetworkSeemsToBeMissing)],
					 ServiceRef.Localizer[nameof(AppResources.Ok)]);
				return;
			}

			bool subscribed = item != null && (item.State == SubscriptionState.To || item.State == SubscriptionState.Both);

			if (subscribed)
			{
				bool confirm = await ServiceRef.UiService.DisplayAlert(ServiceRef.Localizer[nameof(AppResources.Question)],
					 ServiceRef.Localizer[nameof(AppResources.RemoveSubscriptionFrom), this.FriendlyName ?? string.Empty],
					 ServiceRef.Localizer[nameof(AppResources.Yes)], ServiceRef.Localizer[nameof(AppResources.Cancel)]);

				if (!confirm)
					return;

				ServiceRef.XmppService.RequestPresenceUnsubscription(this.BareJid);

				if (item != null && (item.State == SubscriptionState.From || item.State == SubscriptionState.Both))
				{
					RemoveSubscriptionViewModel viewModel = new(this.BareJid);
					RemoveSubscriptionPopup page = new(viewModel);

					await ServiceRef.UiService.PushAsync(page);

					bool? remove = await viewModel.Result;

					if (remove.HasValue && remove.Value)
					{
						ServiceRef.XmppService.RequestRevokePresenceSubscription(this.BareJid);

						if (this.Contact.AllowSubscriptionFrom == true)
						{
							this.Contact.AllowSubscriptionFrom = null;
							await Database.Update(this.Contact);
						}
					}
				}
			}
			else
			{
				SubscribeToViewModel viewModel = new(this.BareJid);
				SubscribeToPopup page = new(viewModel);

				await ServiceRef.UiService.PushAsync(page);
				bool? subscribeTo = await viewModel.Result;

				if (subscribeTo.HasValue && subscribeTo.Value)
				{
					string idXml;

					if (ServiceRef.TagProfile.LegalIdentity is null)
						idXml = string.Empty;
					else
					{
						StringBuilder xml = new();
						ServiceRef.TagProfile.LegalIdentity.Serialize(xml, true, true, true, true, true, true, true);
						idXml = xml.ToString();
					}

					ServiceRef.XmppService.RequestPresenceSubscription(this.BareJid, idXml);
				}
			}
		}

		/// <summary>
		/// Command to save the contact to the database.
		/// </summary>
		[RelayCommand]
		private async Task SaveToDatabaseAsync()
		{
			try
			{
				ContactInfo existingContact = await ContactInfo.FindByBareJid(this.BareJid);
				if (existingContact is null)
					await Database.Insert(this.Contact);
				else
					await Database.Update(this);

				await Database.Provider.Flush();
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				await ServiceRef.UiService.DisplayException(ex);
			}
		}

		#endregion
	}
}
