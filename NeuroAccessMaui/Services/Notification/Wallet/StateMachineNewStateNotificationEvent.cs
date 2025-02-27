using NeuroAccessMaui.Resources.Languages;
using NeuroFeatures;
using NeuroFeatures.EventArguments;
using System.Text;

namespace NeuroAccessMaui.Services.Notification.Wallet
{
	/// <summary>
	/// Contains information about a change in a state-machine associated with a token.
	/// </summary>
	public class StateMachineNewStateNotificationEvent : TokenNotificationEvent
	{
		/// <summary>
		/// Contains information about a change in a state-machine associated with a token.
		/// </summary>
		public StateMachineNewStateNotificationEvent()
			: base()
		{
		}

		/// <summary>
		/// Contains information about a change in a state-machine associated with a token.
		/// </summary>
		/// <param name="e">Event arguments.</param>
		public StateMachineNewStateNotificationEvent(NewStateEventArgs e)
			: base(e)
		{
			this.NewState = e.NewState;
		}

		/// <summary>
		/// New state of state-machine.
		/// </summary>
		public string? NewState { get; set; }

		/// <summary>
		/// Gets a descriptive text for the event.
		/// </summary>
		public override async Task<string> GetDescription()
		{
			StringBuilder sb = new();

			sb.Append(ServiceRef.Localizer[nameof(AppResources.StateChangedTo), this.NewState ?? string.Empty]);
			sb.Append(": ");
			sb.Append(await base.GetDescription());

			return sb.ToString();
		}
	}
}
