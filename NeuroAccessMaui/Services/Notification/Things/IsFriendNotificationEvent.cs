using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services.Contacts;
using NeuroAccessMaui.UI.Pages.Things.IsFriend;
using Waher.Networking.XMPP.Provisioning;
using Waher.Networking.XMPP.Provisioning.Events;

namespace NeuroAccessMaui.Services.Notification.Things
{
	/// <summary>
	/// Contains information about a request to become "friends", i.e. subscribe to presence.
	/// </summary>
	public class IsFriendNotificationEvent : ProvisioningNotificationEvent
	{
		/// <summary>
		/// Contains information about a request to become "friends", i.e. subscribe to presence.
		/// </summary>
		public IsFriendNotificationEvent()
			: base()
		{
		}

		/// <summary>
		/// Contains information about a request to become "friends", i.e. subscribe to presence.
		/// </summary>
		/// <param name="e">Event arguments</param>
		public IsFriendNotificationEvent(IsFriendEventArgs e)
			: base(e)
		{
		}

		/// <summary>
		/// Gets a descriptive text for the event.
		/// </summary>
		public override async Task<string> GetDescription()
		{
			string ThingName = await ContactInfo.GetFriendlyName(this.BareJid);
			string RemoteName = await ContactInfo.GetFriendlyName(this.RemoteJid);

			return ServiceRef.Localizer[nameof(AppResources.AccessRequestText), RemoteName, ThingName];
		}

		/// <summary>
		/// Opens the event.
		/// </summary>
		public override async Task Open()
		{
			string ThingName = await ContactInfo.GetFriendlyName(this.BareJid);
			string RemoteName = await ContactInfo.GetFriendlyName(this.RemoteJid);

			await ServiceRef.UiService.GoToAsync(nameof(IsFriendPage), new IsFriendNavigationArgs(this, ThingName, RemoteName));
		}
	}
}
