using EDaler;
using EDaler.Events;
using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services.Contacts;
using NeuroAccessMaui.UI.Pages.Wallet;
using NeuroAccessMaui.UI.Pages.Wallet.EDalerReceived;
using Waher.Persistence;

namespace NeuroAccessMaui.Services.Notification.Wallet
{
	/// <summary>
	/// Contains information about an incoming chat message.
	/// </summary>
	public class BalanceNotificationEvent : WalletNotificationEvent
	{
		/// <summary>
		/// Contains information about an incoming chat message.
		/// </summary>
		public BalanceNotificationEvent()
			: base()
		{
		}

		/// <summary>
		/// Contains information about an incoming chat message.
		/// </summary>
		/// <param name="e">Event arguments</param>
		public BalanceNotificationEvent(BalanceEventArgs e)
			: base(e)
		{
			this.Amount = e.Balance.Amount;
			this.Currency = e.Balance.Currency;
			this.Event = e.Balance.Event;
			this.Reserved = e.Balance.Reserved;
			this.Timestamp = e.Balance.Timestamp;
			this.Category = e.Balance.Event?.TransactionId.ToString() ?? string.Empty;
		}

		/// <summary>
		/// Amount
		/// </summary>
		public decimal Amount { get; set; }

		/// <summary>
		/// Amount reserved
		/// </summary>
		public decimal Reserved { get; set; }

		/// <summary>
		/// Currency
		/// </summary>
		public CaseInsensitiveString? Currency { get; set; }

		/// <summary>
		/// Account event
		/// </summary>
		public AccountEvent? Event { get; set; }

		/// <summary>
		/// Gets a descriptive text for the event.
		/// </summary>
		public override async Task<string> GetDescription()
		{
			if (string.IsNullOrEmpty(this.Event?.Remote))
				return ServiceRef.Localizer[nameof(AppResources.BalanceUpdated)];
			else
				return await ContactInfo.GetFriendlyName(this.Event.Remote);
		}

		/// <summary>
		/// Opens the event.
		/// </summary>
		public override async Task Open()
		{
			if ((this.Event?.Change ?? 0) > 0)
			{
				Balance Balance = new(this.Timestamp, this.Amount, this.Reserved, this.Currency, this.Event);

				await ServiceRef.UiService.GoToAsync(nameof(EDalerReceivedPage), new EDalerBalanceNavigationArgs(Balance));
			}
			else
				await ServiceRef.NeuroWalletOrchestratorService.OpenEDalerWallet();
		}
	}
}
