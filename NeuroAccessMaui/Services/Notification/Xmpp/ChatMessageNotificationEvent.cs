using Microsoft.Maui.Controls.Shapes;
using NeuroAccessMaui.Services.Contacts;
using NeuroAccessMaui.Services.UI;
using NeuroAccessMaui.UI;
using NeuroAccessMaui.UI.Pages.Contacts.Chat;
using Waher.Networking.XMPP;
using Waher.Networking.XMPP.Events;

namespace NeuroAccessMaui.Services.Notification.Xmpp
{
	/// <summary>
	/// Contains information about an incoming chat message.
	/// </summary>
	public class ChatMessageNotificationEvent : XmppNotificationEvent
	{
		/// <summary>
		/// Contains information about an incoming chat message.
		/// </summary>
		public ChatMessageNotificationEvent()
			: base()
		{
		}

		/// <summary>
		/// Contains information about an incoming chat message.
		/// </summary>
		/// <param name="e">Event arguments</param>
		public ChatMessageNotificationEvent(MessageEventArgs e)
			: this(e.FromBareJID)
		{
		}

		/// <summary>
		/// Contains information about an incoming chat message.
		/// </summary>
		/// <param name="RemoteBareJid">Remote Bare JID</param>
		public ChatMessageNotificationEvent(string RemoteBareJid)
			: base()
		{
			this.Category = RemoteBareJid;
			this.BareJid = RemoteBareJid;
			this.Received = DateTime.UtcNow;
			this.Type = NotificationEventType.Contacts;
		}

		/// <summary>
		/// ID of message object being updated
		/// </summary>
		public string? ReplaceObjectId { get; set; }

		/// <summary>
		/// Gets an icon for the category of event.
		/// </summary>
		/// <returns>Icon</returns>
		public override Task<Geometry> GetCategoryIcon()
		{
			return Task.FromResult(Geometries.PersonPath);
		}

		/// <summary>
		/// Gets a descriptive text for the event.
		/// </summary>
		public override Task<string> GetDescription()
		{
			return ContactInfo.GetFriendlyName(this.BareJid);
		}

		/// <summary>
		/// Opens the event.
		/// </summary>
		public override async Task Open()
		{
			ContactInfo ContactInfo = await ContactInfo.FindByBareJid(this.BareJid);
			string LegalId = ContactInfo?.LegalId;
			string FriendlyName = await this.GetDescription();
			ChatNavigationArgs Args = new(LegalId, this.BareJid, FriendlyName);

			await ServiceRef.UiService.GoToAsync(nameof(ChatPage), Args, BackMethod.Inherited, this.BareJid);
		}
	}
}
