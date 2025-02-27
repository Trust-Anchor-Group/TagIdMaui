using System.Text;
using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services.Contacts;
using NeuroAccessMaui.UI.Pages.Identity.ViewIdentity;
using Waher.Networking.XMPP.Contracts;
using Waher.Networking.XMPP.Contracts.EventArguments;

namespace NeuroAccessMaui.Services.Notification.Identities
{
	/// <summary>
	/// Notification event for identity responses.
	/// </summary>
	public class IdentityResponseNotificationEvent : IdentityNotificationEvent
	{
		/// <summary>
		/// Notification event for identity responses.
		/// </summary>
		public IdentityResponseNotificationEvent()
			: base()
		{
		}

		/// <summary>
		/// Notification event for identity responses.
		/// </summary>
		/// <param name="e">Event arguments.</param>
		public IdentityResponseNotificationEvent(LegalIdentityPetitionResponseEventArgs e)
			: base(e)
		{
			this.Response = e.Response;
		}

		/// <summary>
		/// Response
		/// </summary>
		public bool Response { get; set; }

		/// <summary>
		/// Opens the event.
		/// </summary>
		public override async Task Open()
		{
			if (!this.Response || this.Identity is null)
			{
				await ServiceRef.UiService.DisplayAlert(ServiceRef.Localizer[nameof(AppResources.Message)],
					ServiceRef.Localizer[nameof(AppResources.SignaturePetitionDenied)],
					ServiceRef.Localizer[nameof(AppResources.Ok)]);
			}
			else
				await ServiceRef.UiService.GoToAsync(nameof(ViewIdentityPage), new ViewIdentityNavigationArgs(this.Identity));
		}

		/// <summary>
		/// Gets a descriptive text for the category of event.
		/// </summary>
		public override Task<string> GetDescription()
		{
			StringBuilder Result = new();

			Result.Append(ServiceRef.Localizer[nameof(AppResources.IdentityResponse)]);

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
