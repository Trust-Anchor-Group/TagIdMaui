using System.Text;
using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services.Contacts;
using NeuroAccessMaui.UI.Pages.Petitions.PetitionSignature;
using Waher.Networking.XMPP.Contracts;
using Waher.Networking.XMPP.Contracts.EventArguments;

namespace NeuroAccessMaui.Services.Notification.Identities
{
	/// <summary>
	/// Notification event for signature petitions.
	/// </summary>
	public class RequestSignatureNotificationEvent : IdentityNotificationEvent
	{
		/// <summary>
		/// Notification event for signature petitions.
		/// </summary>
		public RequestSignatureNotificationEvent()
			: base()
		{
		}

		/// <summary>
		/// Notification event for signature petitions.
		/// </summary>
		/// <param name="e">Event arguments.</param>
		public RequestSignatureNotificationEvent(SignaturePetitionEventArgs e)
			: base(e)
		{
		}

		/// <summary>
		/// Opens the event.
		/// </summary>
		public override async Task Open()
		{
			if (this.Identity is not null &&
				!string.IsNullOrEmpty(this.RequestorFullJid) &&
				!string.IsNullOrEmpty(this.SignatoryIdentityId) &&
				!string.IsNullOrEmpty(this.PetitionId) &&
				!string.IsNullOrEmpty(this.Purpose) &&
				this.ContentToSign is not null)
			{
				await ServiceRef.UiService.GoToAsync(nameof(PetitionSignaturePage), new PetitionSignatureNavigationArgs(
					this.Identity, this.RequestorFullJid, this.SignatoryIdentityId, this.ContentToSign, this.PetitionId, this.Purpose));
			}
		}

		/// <summary>
		/// Gets a descriptive text for the category of event.
		/// </summary>
		public override Task<string> GetDescription()
		{
			StringBuilder Result = new();

			Result.Append(ServiceRef.Localizer[nameof(AppResources.RequestSignature)]);

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
