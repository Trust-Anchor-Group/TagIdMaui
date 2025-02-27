using NeuroAccessMaui.Services.Contacts;
using NeuroFeatures.Events;

namespace NeuroAccessMaui.UI.Pages.Wallet.TokenEvents.Events
{
	/// <summary>
	/// Represents a token donation event.
	/// </summary>
	/// <param name="Event">Token event</param>
	public class DonatedItem(Donated Event) : OwnershipEventItem(Event)
	{
		private readonly Donated @event = Event;
		private string? donorFriendlyName;

		/// <summary>
		/// Type of event.
		/// </summary>
		public override EventType Type => EventType.Donated;

		/// <summary>
		/// Donor
		/// </summary>
		public string Donor => this.@event.Donor;

		/// <summary>
		/// Donor (Friendly Name)
		/// </summary>
		public string DonorFriendlyName => this.donorFriendlyName ?? string.Empty;

		/// <summary>
		/// Binds properties
		/// </summary>
		public override async Task DoBind()
		{
			await base.DoBind();

			this.donorFriendlyName = await ContactInfo.GetFriendlyName(this.Donor);
		}
	}
}
