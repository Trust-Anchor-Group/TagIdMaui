using System.Text;
using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services.Contacts;
using NeuroAccessMaui.UI.Pages.Petitions.PetitionIdentity;
using Waher.Networking.XMPP.Contracts;
using Waher.Networking.XMPP.Contracts.EventArguments;

namespace NeuroAccessMaui.Services.Notification.Identities
{
	/// <summary>
	/// Notification event for identity petitions.
	/// </summary>
	public class RequestIdentityNotificationEvent : IdentityNotificationEvent
	{
		/// <summary>
		/// Notification event for identity petitions.
		/// </summary>
		public RequestIdentityNotificationEvent()
			: base()
		{
		}

		/// <summary>
		/// Notification event for identity petitions.
		/// </summary>
		/// <param name="e">Event arguments.</param>
		public RequestIdentityNotificationEvent(LegalIdentityPetitionEventArgs e)
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
				await ServiceRef.UiService.GoToAsync(nameof(PetitionIdentityPage), new PetitionIdentityNavigationArgs(
					this.Identity, this.RequestorFullJid, this.SignatoryIdentityId, this.PetitionId, this.Purpose));
			}
		}

		/// <summary>
		/// Gets a descriptive text for the category of event.
		/// </summary>
		public override Task<string> GetDescription()
		{
			StringBuilder Result = new();

			Result.Append(ServiceRef.Localizer[nameof(AppResources.RequestToAccessIdentity)]);

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
