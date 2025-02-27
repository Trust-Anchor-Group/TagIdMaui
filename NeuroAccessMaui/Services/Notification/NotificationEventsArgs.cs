namespace NeuroAccessMaui.Services.Notification
{
	/// <summary>
	/// Event argument for notification events.
	/// </summary>
	/// <param name="Events">Referenced event.</param>
	public class NotificationEventsArgs(NotificationEvent[] Events) : EventArgs()
	{
		/// <summary>
		/// Referenced events.
		/// </summary>
		public NotificationEvent[] Events { get; } = Events;
	}
}
