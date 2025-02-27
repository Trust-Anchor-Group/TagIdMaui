using System.Diagnostics.CodeAnalysis;
using Waher.Events;
using Waher.Persistence;
using Waher.Runtime.Inventory;

namespace NeuroAccessMaui.Services.Notification
{
	/// <summary>
	/// Notification service
	/// </summary>
	[Singleton]
	public class NotificationService : LoadableService, INotificationService
	{
		private const int nrTypes = 4;

		private readonly SortedDictionary<CaseInsensitiveString, List<NotificationEvent>>[] events;
		private readonly LinkedList<KeyValuePair<Type, DateTime>> expected;

		/// <summary>
		/// Notification service
		/// </summary>
		public NotificationService()
		{
			int i;

			this.events = new SortedDictionary<CaseInsensitiveString, List<NotificationEvent>>[nrTypes];
			this.expected = new LinkedList<KeyValuePair<Type, DateTime>>();

			for (i = 0; i < nrTypes; i++)
				this.events[i] = [];
		}

		/// <summary>
		/// Loads the specified service.
		/// </summary>
		/// <param name="isResuming">Set to <c>true</c> to indicate the app is resuming as opposed to starting cold.</param>
		/// <param name="cancellationToken">Will stop the service load if the token is set.</param>
		public override async Task Load(bool isResuming, CancellationToken cancellationToken)
		{
			SortedDictionary<CaseInsensitiveString, List<NotificationEvent>>? ByCategory = null;
			List<NotificationEvent>? Events = null;
			string? PrevCategory = null;
			int PrevType = -1;
			int Type;

			IEnumerable<NotificationEvent> LoadedEvents;

			try
			{
				LoadedEvents = await Database.Find<NotificationEvent>("Type", "Category");
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);

				await Database.Clear("Notifications");
				LoadedEvents = [];
			}

			foreach (NotificationEvent Event in LoadedEvents)
			{
				if (Event.Type is null || Event.Category is null)
					continue;

				Type = (int)Event.Type;
				if (Type < 0 || Type >= nrTypes)
					continue;

				if (CaseInsensitiveString.IsNullOrEmpty(Event.Category))
				{
					Log.Debug("Notification event of type " + Event.GetType().FullName + " lacked Category.");
					await Database.Delete(Event);
					continue;
				}

				lock (this.events)
				{
					if (ByCategory is null || Type != PrevType)
					{
						ByCategory = this.events[Type];
						PrevType = Type;
					}

					if (Events is null || Event.Category != PrevCategory)
					{
						if (!ByCategory.TryGetValue(Event.Category, out Events))
						{
							Events = [];
							ByCategory[Event.Category] = Events;
						}

						PrevCategory = Event.Category;
					}

					Events.Add(Event);
				}
			}

			await base.Load(isResuming, cancellationToken);
		}

		/// <summary>
		/// Registers a type of notification as expected.
		/// </summary>
		/// <typeparam name="T">Type of event to expect.</typeparam>
		/// <param name="Before">If event is received before this time, it is opened automatically.</param>
		public void ExpectEvent<T>(DateTime Before)
			where T : NotificationEvent
		{
			lock (this.expected)
			{
				this.expected.AddLast(new KeyValuePair<Type, DateTime>(typeof(T), Before));
			}
		}

		/// <summary>
		/// Registers a new event and notifies the user.
		/// </summary>
		/// <param name="Event">Notification event.</param>
		public async Task NewEvent(NotificationEvent Event)
		{
			if (Event.Type is null || Event.Category is null)
				return;

			DateTime Now = DateTime.Now;
			bool Expected = false;

			lock (this.expected)
			{
				LinkedListNode<KeyValuePair<Type, DateTime>>? Loop = this.expected.First;
				LinkedListNode<KeyValuePair<Type, DateTime>>? Next;
				Type EventType = Event.GetType();

				while (Loop is not null)
				{
					Next = Loop.Next;

					if (Loop.Value.Value < Now)
						this.expected.Remove(Loop);
					else if (Loop.Value.Key == EventType)
					{
						this.expected.Remove(Loop);
						Expected = true;
						break;
					}

					Loop = Next;
				}
			}

			if (Expected)
			{
				MainThread.BeginInvokeOnMainThread(async () =>
				{
					try
					{
						await Event.Open();
					}
					catch (Exception ex)
					{
						ServiceRef.LogService.LogException(ex);
					}
				});
			}
			else
			{
				await Database.Insert(Event);

				int Type = (int)Event.Type;
				if (Type >= 0 && Type < nrTypes)
				{
					lock (this.events)
					{
						SortedDictionary<CaseInsensitiveString, List<NotificationEvent>> ByCategory = this.events[Type];

						if (!ByCategory.TryGetValue(Event.Category, out List<NotificationEvent>? Events))
						{
							Events = [];
							ByCategory[Event.Category] = Events;
						}

						Events.Add(Event);
					}

					await this.OnNewNotification.Raise(this, new NotificationEventArgs(Event));
				}

				Task _ = Task.Run(async () =>
				{
					try
					{
						await Event.Prepare();
					}
					catch (Exception ex)
					{
						ServiceRef.LogService.LogException(ex);
					}
				});
			}
		}

		/// <summary>
		/// Deletes events for a given button and category.
		/// </summary>
		/// <param name="Type">Type of event</param>
		/// <param name="Category">Category</param>
		public async Task DeleteEvents(NotificationEventType Type, CaseInsensitiveString Category)
		{
			int TypeIndex = (int)Type;

			if (TypeIndex >= 0 && TypeIndex < nrTypes)
			{
				NotificationEvent[] ToDelete;

				lock (this.events)
				{
					SortedDictionary<CaseInsensitiveString, List<NotificationEvent>> ByCategory = this.events[TypeIndex];

					if (!ByCategory.TryGetValue(Category, out List<NotificationEvent>? Events))
						return;

					ToDelete = [.. Events];
					ByCategory.Remove(Category);
				}

				await this.DoDeleteEvents(ToDelete);
			}
		}

		/// <summary>
		/// Deletes a specified set of events.
		/// </summary>
		/// <param name="Events">Events to delete.</param>
		public Task DeleteEvents(params NotificationEvent[] Events)
		{
			foreach (NotificationEvent Event in Events)
			{
				if (Event.Type is null || Event.Category is null)
					continue;

				int TypeIndex = (int)Event.Type;

				if (TypeIndex >= 0 && TypeIndex < nrTypes)
				{
					lock (this.events)
					{
						SortedDictionary<CaseInsensitiveString, List<NotificationEvent>> ByCategory = this.events[TypeIndex];

						if (ByCategory.TryGetValue(Event.Category, out List<NotificationEvent>? List) &&
							List.Remove(Event) &&
							List.Count == 0)
						{
							ByCategory.Remove(Event.Category);
						}
					}
				}

			}

			return this.DoDeleteEvents(Events);
		}

		private async Task DoDeleteEvents(NotificationEvent[] Events)
		{
			try
			{
				await Database.StartBulk();

				try
				{
					foreach (NotificationEvent Event in Events)
					{
						try
						{
							await Database.Delete(Event);
						}
						catch (KeyNotFoundException)
						{
							// Ignore, already deleted.
						}
					}
				}
				finally
				{
					await Database.EndBulk();
				}

				await this.OnNotificationsDeleted.Raise(this, new NotificationEventsArgs(Events));
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
			}
		}

		/// <summary>
		/// Gets available notification events for a button.
		/// </summary>
		/// <param name="Type">Type of event</param>
		/// <returns>Recorded events.</returns>
		public NotificationEvent[] GetEvents(NotificationEventType Type)
		{
			int i = (int)Type;
			if (i < 0 || i >= nrTypes)
				return [];

			lock (this.events)
			{
				SortedDictionary<CaseInsensitiveString, List<NotificationEvent>> ByCategory = this.events[i];
				List<NotificationEvent> Result = [];

				foreach (List<NotificationEvent> Events in ByCategory.Values)
					Result.AddRange(Events);

				return [.. Result];
			}
		}

		/// <summary>
		/// Gets available categories for a button.
		/// </summary>
		/// <param name="Type">Type of event</param>
		/// <returns>Recorded categories.</returns>
		public CaseInsensitiveString[] GetCategories(NotificationEventType Type)
		{
			int i = (int)Type;
			if (i < 0 || i >= nrTypes)
				return [];

			lock (this.events)
			{
				SortedDictionary<CaseInsensitiveString, List<NotificationEvent>> ByCategory = this.events[i];
				List<CaseInsensitiveString> Result = [.. ByCategory.Keys];

				return [.. Result];
			}
		}

		/// <summary>
		/// Gets available notification events for a button, sorted by category.
		/// </summary>
		/// <param name="Type">Type of event</param>
		/// <returns>Recorded events.</returns>
		public SortedDictionary<CaseInsensitiveString, NotificationEvent[]> GetEventsByCategory(NotificationEventType Type)
		{
			int i = (int)Type;
			if (i < 0 || i >= nrTypes)
				return [];

			lock (this.events)
			{
				SortedDictionary<CaseInsensitiveString, List<NotificationEvent>> ByCategory = this.events[i];
				SortedDictionary<CaseInsensitiveString, NotificationEvent[]> Result = [];

				foreach (KeyValuePair<CaseInsensitiveString, List<NotificationEvent>> P in ByCategory)
					Result[P.Key] = [.. P.Value];

				return Result;
			}
		}

		/// <summary>
		/// Gets available notification events for a button, sorted by category.
		/// </summary>
		/// <param name="Type">Type of event</param>
		/// <returns>Recorded events.</returns>
		public SortedDictionary<CaseInsensitiveString, T[]> GetEventsByCategory<T>(NotificationEventType Type)
			where T : NotificationEvent
		{
			int i = (int)Type;
			if (i < 0 || i >= nrTypes)
				return [];

			lock (this.events)
			{
				SortedDictionary<CaseInsensitiveString, List<NotificationEvent>> ByCategory = this.events[i];
				SortedDictionary<CaseInsensitiveString, T[]> Result = [];

				foreach (KeyValuePair<CaseInsensitiveString, List<NotificationEvent>> P in ByCategory)
				{
					List<T>? Items = null;

					foreach (NotificationEvent Event in P.Value)
					{
						if (Event is T TypedItem)
						{
							Items ??= [];
							Items.Add(TypedItem);
						}
					}

					if (Items is not null)
						Result[P.Key] = [.. Items];
				}

				return Result;
			}
		}

		/// <summary>
		/// Tries to get available notification events.
		/// </summary>
		/// <param name="Type">Type of event</param>
		/// <param name="Category">Notification event category</param>
		/// <param name="Events">Notification events, if found.</param>
		/// <returns>If notification events where found for the given category.</returns>
		public bool TryGetNotificationEvents(NotificationEventType Type, CaseInsensitiveString Category,
			[NotNullWhen(true)] out NotificationEvent[]? Events)
		{
			int i = (int)Type;
			if (i < 0 || i >= nrTypes)
			{
				Events = null;
				return false;
			}

			lock (this.events)
			{
				SortedDictionary<CaseInsensitiveString, List<NotificationEvent>> ByCategory = this.events[i];

				if (!ByCategory.TryGetValue(Category, out List<NotificationEvent>? Events2))
				{
					Events = null;
					return false;
				}

				Events = [.. Events2];
				return true;
			}
		}

		/// <summary>
		/// Event raised when a new notification has been logged.
		/// </summary>
		public event EventHandlerAsync<NotificationEventArgs>? OnNewNotification;

		/// <summary>
		/// Event raised when notifications have been deleted.
		/// </summary>
		public event EventHandlerAsync<NotificationEventsArgs>? OnNotificationsDeleted;

		/// <summary>
		/// Number of notifications but button Contacts
		/// </summary>
		public int NrNotificationsContacts => this.Count((int)NotificationEventType.Contacts);

		/// <summary>
		/// Number of notifications but button Things
		/// </summary>
		public int NrNotificationsThings => this.Count((int)NotificationEventType.Things);

		/// <summary>
		/// Number of notifications but button Contracts
		/// </summary>
		public int NrNotificationsContracts => this.Count((int)NotificationEventType.Contracts);

		/// <summary>
		/// Number of notifications but button Wallet
		/// </summary>
		public int NrNotificationsWallet => this.Count((int)NotificationEventType.Wallet);

		private int Count(int Index)
		{
			lock (this.events)
			{
				SortedDictionary<CaseInsensitiveString, List<NotificationEvent>> Events = this.events[Index];
				int Result = 0;

				foreach (List<NotificationEvent> List in Events.Values)
					Result += List.Count;

				return Result;
			}
		}

		/// <summary>
		/// Deletes pending events that have already been resolved.
		/// </summary>
		/// <param name="Resolver">Notification event resolver, determining which events are resolved.</param>
		public async Task DeleteResolvedEvents(IEventResolver Resolver)
		{
			List<NotificationEvent>? Resolved = null;

			lock (this.events)
			{
				foreach (SortedDictionary<CaseInsensitiveString, List<NotificationEvent>> ByCategory in this.events)
				{
					foreach (KeyValuePair<CaseInsensitiveString, List<NotificationEvent>> P in ByCategory)
					{
						foreach (NotificationEvent Event in P.Value)
						{
							if (Resolver.Resolves(Event))
							{
								Resolved ??= [];
								Resolved.Add(Event);
							}
						}
					}
				}
			}

			if (Resolved is not null)
				await this.DeleteEvents([.. Resolved]);
		}

	}
}
