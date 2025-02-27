namespace NeuroAccessMaui.Services.Notification
{
	/// <summary>
	/// Event argument for notification events.
	/// </summary>
	/// <param name="Event">Referenced event.</param>
	public class NotificationEventArgs(NotificationEvent Event)
		: EventArgs()
	{
		/// <summary>
		/// Referenced event.
		/// </summary>
		public NotificationEvent Event { get; } = Event;
	}
}
