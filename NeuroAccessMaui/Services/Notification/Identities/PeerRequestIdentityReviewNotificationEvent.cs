using System.Text;
using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services.Contacts;
using NeuroAccessMaui.UI.Pages.Identity.ViewIdentity;
using Waher.Networking.XMPP.Contracts;
using Waher.Networking.XMPP.Contracts.EventArguments;

namespace NeuroAccessMaui.Services.Notification.Identities
{
	/// <summary>
	/// Notification event for peer reviews of identities.
	/// </summary>
	public class PeerRequestIdentityReviewNotificationEvent : IdentityNotificationEvent
	{
		/// <summary>
		/// Notification event for peer reviews of identities.
		/// </summary>
		public PeerRequestIdentityReviewNotificationEvent()
			: base()
		{
		}

		/// <summary>
		/// Notification event for contract proposals.
		/// </summary>
		/// <param name="e">Event arguments.</param>
		public PeerRequestIdentityReviewNotificationEvent(SignaturePetitionEventArgs e)
			: base(e)
		{
		}

		/// <summary>
		/// Opens the event.
		/// </summary>
		public override async Task Open()
		{
			if (this.Identity is not null)
			{
				await ServiceRef.UiService.GoToAsync(nameof(ViewIdentityPage), new ViewIdentityNavigationArgs(this.Identity,
					this.RequestorFullJid, this.SignatoryIdentityId, this.PetitionId, this.Purpose, this.ContentToSign));
			}
		}

		/// <summary>
		/// Gets a descriptive text for the category of event.
		/// </summary>
		public override Task<string> GetDescription()
		{
			StringBuilder Result = new();

			Result.Append(ServiceRef.Localizer[nameof(AppResources.IdentityReviewRequest)]);

			if (this.Identity is not null)
			{
				Result.Append(": ");
				Result.Append(ContactInfo.GetFriendlyName(this.Identity));
			}

			Result.Append('.');

			return Task.FromResult(Result.ToString());
		}

	}
}
