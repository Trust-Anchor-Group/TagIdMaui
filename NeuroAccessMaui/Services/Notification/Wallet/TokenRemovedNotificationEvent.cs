using NeuroAccessMaui.Resources.Languages;
using NeuroFeatures;
using NeuroFeatures.EventArguments;
using System.Text;

namespace NeuroAccessMaui.Services.Notification.Wallet
{
	/// <summary>
	/// Contains information about a token that has been removed.
	/// </summary>
	public class TokenRemovedNotificationEvent : TokenNotificationEvent
	{
		/// <summary>
		/// Contains information about a token that has been removed.
		/// </summary>
		public TokenRemovedNotificationEvent()
			: base()
		{
		}

		/// <summary>
		/// Contains information about a token that has been removed.
		/// </summary>
		/// <param name="e">Event arguments.</param>
		public TokenRemovedNotificationEvent(TokenEventArgs e)
			: base(e)
		{
		}

		/// <summary>
		/// Gets a descriptive text for the event.
		/// </summary>
		public override async Task<string> GetDescription()
		{
			StringBuilder sb = new();

			sb.Append(ServiceRef.Localizer[nameof(AppResources.TokenRemoved2)]);
			sb.Append(": ");
			sb.Append(await base.GetDescription());

			return sb.ToString();
		}
	}
}
