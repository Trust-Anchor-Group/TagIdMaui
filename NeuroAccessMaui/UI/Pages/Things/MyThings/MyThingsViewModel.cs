using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using NeuroAccessMaui.Extensions;
using NeuroAccessMaui.Services;
using NeuroAccessMaui.Services.Contacts;
using NeuroAccessMaui.Services.Notification;
using NeuroAccessMaui.Services.UI;
using NeuroAccessMaui.UI.Pages.Contacts.MyContacts;
using NeuroAccessMaui.UI.Pages.Things.ViewThing;
using Waher.Networking.XMPP;
using Waher.Networking.XMPP.Contracts;
using Waher.Networking.XMPP.Events;
using Waher.Networking.XMPP.Provisioning;
using Waher.Persistence;
using Waher.Persistence.Filters;

namespace NeuroAccessMaui.UI.Pages.Things.MyThings
{
	/// <summary>
	/// The view model to bind to when displaying the list of things.
	/// </summary>
	/// <param name="Args">Navigation arguments</param>
	public partial class MyThingsViewModel(MyThingsNavigationArgs? Args)
		: BaseViewModel
	{
		private readonly Dictionary<CaseInsensitiveString, List<ContactInfoModel>> byBareJid = [];
		private TaskCompletionSource<ContactInfoModel?>? result = Args?.ThingToShare;

		/// <inheritdoc/>
		protected override async Task OnInitialize()
		{
			await base.OnInitialize();

			ServiceRef.XmppService.OnPresence += this.Xmpp_OnPresence;
			ServiceRef.NotificationService.OnNewNotification += this.NotificationService_OnNewNotification;
			ServiceRef.NotificationService.OnNotificationsDeleted += this.NotificationService_OnNotificationsDeleted;
		}

		/// <inheritdoc/>
		protected override async Task OnAppearing()
		{
			await base.OnAppearing();
			this.SelectedThing = null;

			if (this.result is not null && this.result.Task.IsCompleted)
				await this.GoBack();
			else
			{
				this.result = null;

				await this.LoadThings();
			}
		}

		private async Task LoadThings()
		{
			SortedDictionary<string, ContactInfo> SortedByName = [];
			SortedDictionary<string, ContactInfo> SortedByAddress = [];
			string Name;
			string Suffix;
			string Key;
			int i;

			foreach (ContactInfo Info in await Database.Find<ContactInfo>(new FilterFieldEqualTo("IsThing", true)))
			{
				Name = Info.FriendlyName;
				if (SortedByName.ContainsKey(Name))
				{
					i = 1;

					do
					{
						Suffix = " " + (++i).ToString(CultureInfo.InvariantCulture);
					}
					while (SortedByName.ContainsKey(Name + Suffix));

					SortedByName[Name + Suffix] = Info;
				}
				else
					SortedByName[Name] = Info;

				Key = Info.BareJid + ", " + Info.SourceId + ", " + Info.Partition + ", " + Info.NodeId;
				SortedByAddress[Key] = Info;
			}

			SearchResultThing[] MyDevices = await ServiceRef.XmppService.GetAllMyDevices();
			foreach (SearchResultThing Thing in MyDevices)
			{
				Property[] MetaData = ViewClaimThing.ViewClaimThingViewModel.ToProperties(Thing.Tags);

				Key = Thing.Jid + ", " + Thing.Node.SourceId + ", " + Thing.Node.Partition + ", " + Thing.Node.NodeId;
				if (SortedByAddress.TryGetValue(Key, out ContactInfo? Info))
				{
					if (!Info.Owner.HasValue || !Info.Owner.Value || !AreSame(Info.MetaData, MetaData))
					{
						Info.Owner = true;
						Info.MetaData = MetaData;
						Info.FriendlyName = ViewClaimThing.ViewClaimThingViewModel.GetFriendlyName(MetaData) ?? string.Empty;

						await Database.Update(Info);
					}

					continue;
				}

				Info = new ContactInfo()
				{
					BareJid = Thing.Jid,
					LegalId = string.Empty,
					LegalIdentity = null,
					FriendlyName = ViewClaimThing.ViewClaimThingViewModel.GetFriendlyName(Thing.Tags) ?? string.Empty,
					IsThing = true,
					SourceId = Thing.Node.SourceId,
					Partition = Thing.Node.Partition,
					NodeId = Thing.Node.NodeId,
					Owner = true,
					MetaData = MetaData,
					RegistryJid = ServiceRef.XmppService.RegistryServiceJid
				};

				foreach (MetaDataTag Tag in Thing.Tags)
				{
					if (Tag.Name.Equals("R", StringComparison.OrdinalIgnoreCase))
						Info.RegistryJid = Tag.StringValue;
				}

				await Database.Insert(Info);

				Name = Info.FriendlyName;
				if (SortedByName.ContainsKey(Name))
				{
					i = 1;

					do
					{
						Suffix = " " + (++i).ToString(CultureInfo.InvariantCulture);
					}
					while (SortedByName.ContainsKey(Name + Suffix));

					SortedByName[Name + Suffix] = Info;
				}
				else
					SortedByName[Name] = Info;

				SortedByAddress[Key] = Info;
			}

			await Database.Provider.Flush();

			this.byBareJid.Clear();

			ObservableItemGroup<IUniqueItem> NewThings = new(nameof(this.Things), []);

			foreach (ContactInfo Info in SortedByName.Values)
			{
				NotificationEvent[]? Events = GetNotificationEvents(Info);

				ContactInfoModel InfoModel = new(Info, Events ?? []);
				NewThings.Add(InfoModel);

				if (!this.byBareJid.TryGetValue(Info.BareJid, out List<ContactInfoModel>? Contacts))
				{
					Contacts = [];
					this.byBareJid[Info.BareJid] = Contacts;
				}

				Contacts.Add(InfoModel);
			}

			this.ShowThingsMissing = SortedByName.Count == 0;

			MainThread.BeginInvokeOnMainThread(() => ObservableItemGroup<IUniqueItem>.UpdateGroupsItems(this.Things, NewThings));
		}


		/// <summary>
		/// Gets available notification events related to a thing.
		/// </summary>
		/// <param name="Thing">Thing reference</param>
		/// <returns>Array of events, null if none.</returns>
		public static NotificationEvent[]? GetNotificationEvents(ContactInfo Thing)
		{
			if (!string.IsNullOrEmpty(Thing.SourceId) ||
				!string.IsNullOrEmpty(Thing.Partition) ||
				!string.IsNullOrEmpty(Thing.NodeId) ||
				!ServiceRef.NotificationService.TryGetNotificationEvents(NotificationEventType.Contacts, Thing.BareJid, out NotificationEvent[]? ContactEvents))
			{
				ContactEvents = null;
			}

			if (!ServiceRef.NotificationService.TryGetNotificationEvents(NotificationEventType.Things, Thing.ThingNotificationCategoryKey, out NotificationEvent[]? ThingEvents))
				ThingEvents = null;
				
			return ThingEvents is null ? ContactEvents : (ContactEvents?.Join(ThingEvents) ?? ThingEvents);
		}

		/// <inheritdoc/>
		protected override async Task OnDispose()
		{
			ServiceRef.XmppService.OnPresence -= this.Xmpp_OnPresence;
			ServiceRef.NotificationService.OnNewNotification -= this.NotificationService_OnNewNotification;
			ServiceRef.NotificationService.OnNotificationsDeleted -= this.NotificationService_OnNotificationsDeleted;

			this.ShowThingsMissing = false;
			this.Things.Clear();

			this.result?.TrySetResult(null);

			await base.OnDispose();
		}

		/// <summary>
		/// Checks to sets of meta-data about a thing, to see if they match.
		/// </summary>
		/// <param name="MetaData1">First set of meta-data.</param>
		/// <param name="MetaData2">Second set of meta-data.</param>
		/// <returns>If they are the same.</returns>
		public static bool AreSame(Property[]? MetaData1, Property[]? MetaData2)
		{
			if ((MetaData1 is null) ^ (MetaData2 is null))
				return false;

			if (MetaData1 is null || MetaData2 is null)  // Second only necessary to avoid compiler warnings.
				return true;

			int i, c = MetaData1.Length;
			if (MetaData2.Length != c)
				return false;

			for (i = 0; i < c; i++)
			{
				if ((MetaData1[i].Name != MetaData2[i].Name) || (MetaData1[i].Value != MetaData2[i].Value))
					return false;
			}

			return true;
		}

		/// <summary>
		/// Gets or sets whether to show a contacts missing alert or not.
		/// </summary>
		[ObservableProperty]
		private bool showThingsMissing;

		/// <summary>
		/// Holds the list of contacts to display.
		/// </summary>
		public ObservableItemGroup<IUniqueItem> Things { get; } = new(nameof(Things), []);

		/// <summary>
		/// The currently selected contact, if any.
		/// </summary>
		[ObservableProperty]
		private ContactInfoModel? selectedThing;

		protected override void OnPropertyChanged(PropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);

			switch (e.PropertyName)
			{
				case nameof(this.SelectedThing):
					if (this.SelectedThing is not null)
						this.OnSelected(this.SelectedThing);
					break;
			}
		}

		private void OnSelected(ContactInfoModel Thing)
		{
			if (Thing.Contact is null)
				return;

			MainThread.BeginInvokeOnMainThread(async () =>
			{
				if (this.result is null)
				{
					ViewThingNavigationArgs Args = new(Thing.Contact, Thing.Events);
					await ServiceRef.UiService.GoToAsync(nameof(ViewThingPage), Args, BackMethod.Pop2);
				}
				else
				{
					this.result.TrySetResult(Thing);
					await this.GoBack();
				}
			});
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

		private Task NotificationService_OnNotificationsDeleted(object? Sender, NotificationEventsArgs e)
		{
			MainThread.BeginInvokeOnMainThread(() =>
			{
				foreach (NotificationEvent Event in e.Events)
				{
					switch (Event.Type)
					{
						case NotificationEventType.Contacts:
							foreach (IUniqueItem Item in this.Things)
							{
								if (Item is ContactInfoModel Thing)
								{

									if (!string.IsNullOrEmpty(Thing.NodeId) ||
										!string.IsNullOrEmpty(Thing.SourceId) ||
										!string.IsNullOrEmpty(Thing.Partition))
									{
										continue;
									}

									if (Event.Category != Thing.BareJid)
										continue;

									Thing.RemoveEvent(Event);
								}
							}
							break;

						case NotificationEventType.Things:
							foreach (IUniqueItem Item in this.Things)
							{
								if (Item is ContactInfoModel Thing)
								{

									if (Event.Category != ContactInfo.GetThingNotificationCategoryKey(Thing.BareJid, Thing.NodeId, Thing.SourceId, Thing.Partition))
										continue;

									Thing.RemoveEvent(Event);
								}
							}
							break;

						default:
							return;
					}
				}
			});

			return Task.CompletedTask;
		}

		private Task NotificationService_OnNewNotification(object? Sender, NotificationEventArgs e)
		{
			MainThread.BeginInvokeOnMainThread(() =>
			{
				try
				{
					switch (e.Event.Type)
					{
						case NotificationEventType.Contacts:
							foreach (IUniqueItem Item in this.Things)
							{
								if (Item is ContactInfoModel Thing)
								{
									if (!string.IsNullOrEmpty(Thing.NodeId) ||
									!string.IsNullOrEmpty(Thing.SourceId) ||
									!string.IsNullOrEmpty(Thing.Partition))
									{
										continue;
									}

									if (e.Event.Category != Thing.BareJid)
										continue;

									Thing.AddEvent(e.Event);
								}
							}
							break;

						case NotificationEventType.Things:
							foreach (IUniqueItem Item in this.Things)
							{
								if (Item is ContactInfoModel Thing)
								{
									if (e.Event.Category != ContactInfo.GetThingNotificationCategoryKey(Thing.BareJid, Thing.NodeId, Thing.SourceId, Thing.Partition))
										continue;

									Thing.AddEvent(e.Event);
								}
							}
							break;

						default:
							return;
					}
				}
				catch (Exception ex)
				{
					ServiceRef.LogService.LogException(ex);
				}
			});

			return Task.CompletedTask;
		}

	}
}
