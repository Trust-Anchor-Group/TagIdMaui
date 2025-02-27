using System.Diagnostics.CodeAnalysis;
using Waher.Events;
using Waher.Persistence;
using Waher.Runtime.Inventory;

namespace NeuroAccessMaui.Services.Notification
{
	/// <summary>
	/// Interface for push notification services.
	/// </summary>
	[DefaultImplementation(typeof(NotificationService))]
	public interface INotificationService : ILoadableService
	{
		/// <summary>
		/// Registers a type of notification as expected.
		/// </summary>
		/// <typeparam name="T">Type of event to expect.</typeparam>
		/// <param name="Before">If event is received before this time, it is opened automatically.</param>
		void ExpectEvent<T>(DateTime Before)
			where T : NotificationEvent;

		/// <summary>
		/// Registers a new event and notifies the user.
		/// </summary>
		/// <param name="Event">Notification event.</param>
		Task NewEvent(NotificationEvent Event);

		/// <summary>
		/// Deletes events for a given button and category.
		/// </summary>
		/// <param name="Type">Type of event</param>
		/// <param name="Category">Category</param>
		Task DeleteEvents(NotificationEventType Type, CaseInsensitiveString Category);

		/// <summary>
		/// Deletes a specified set of events.
		/// </summary>
		/// <param name="Events">Events to delete.</param>
		Task DeleteEvents(params NotificationEvent[] Events);

		/// <summary>
		/// Gets available categories for a button.
		/// </summary>
		/// <param name="Type">Type of event</param>
		/// <returns>Recorded categories.</returns>
		CaseInsensitiveString[] GetCategories(NotificationEventType Type);

		/// <summary>
		/// Gets available notification events for a button.
		/// </summary>
		/// <param name="Type">Type of event</param>
		/// <returns>Recorded events.</returns>
		NotificationEvent[] GetEvents(NotificationEventType Type);

		/// <summary>
		/// Gets available notification events for a button, sorted by category.
		/// </summary>
		/// <param name="Type">Type of event</param>
		/// <returns>Recorded events.</returns>
		SortedDictionary<CaseInsensitiveString, NotificationEvent[]> GetEventsByCategory(NotificationEventType Type);

		/// <summary>
		/// Gets available notification events for a button, of a given type, sorted by category.
		/// </summary>
		/// <typeparam name="T">Type of notification event of interest.</typeparam>
		/// <param name="Type">Type of event</param>
		/// <returns>Recorded events.</returns>
		SortedDictionary<CaseInsensitiveString, T[]> GetEventsByCategory<T>(NotificationEventType Type)
			where T : NotificationEvent;

		/// <summary>
		/// Tries to get available notification events.
		/// </summary>
		/// <param name="Type">Type of event</param>
		/// <param name="Category">Notification event category</param>
		/// <param name="Events">Notification events, if found.</param>
		/// <returns>If notification events where found for the given category.</returns>
		bool TryGetNotificationEvents(NotificationEventType Type, CaseInsensitiveString Category, [NotNullWhen(true)] out NotificationEvent[]? Events);

		/// <summary>
		/// Event raised when a new notification has been logged.
		/// </summary>
		event EventHandlerAsync<NotificationEventArgs>? OnNewNotification;

		/// <summary>
		/// Event raised when notifications have been deleted.
		/// </summary>
		event EventHandlerAsync<NotificationEventsArgs>? OnNotificationsDeleted;

		/// <summary>
		/// Number of notifications but button Contacts
		/// </summary>
		int NrNotificationsContacts { get; }

		/// <summary>
		/// Number of notifications but button Things
		/// </summary>
		int NrNotificationsThings { get; }

		/// <summary>
		/// Number of notifications but button Contracts
		/// </summary>
		int NrNotificationsContracts { get; }

		/// <summary>
		/// Number of notifications but button Wallet
		/// </summary>
		int NrNotificationsWallet { get; }

		/// <summary>
		/// Deletes pending events that have already been resolved.
		/// </summary>
		/// <param name="Resolver">Notification event resolver, determining which events are resolved.</param>
		Task DeleteResolvedEvents(IEventResolver Resolver);
	}
}
