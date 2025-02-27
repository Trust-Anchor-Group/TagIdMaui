using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services.UI;
using NeuroAccessMaui.UI.Pages.Contracts.MyContracts.ObjectModels;
using NeuroAccessMaui.UI.Pages.Contracts.ViewContract;
using System.Text;
using Waher.Networking.XMPP.Contracts;
using Waher.Networking.XMPP.Contracts.EventArguments;

namespace NeuroAccessMaui.Services.Notification.Contracts
{
	/// <summary>
	/// Notification event for when a contract has been updated.
	/// </summary>
	public class ContractUpdatedNotificationEvent : ContractNotificationEvent
	{
		/// <summary>
		/// Notification event for when a contract has been updated.
		/// </summary>
		public ContractUpdatedNotificationEvent()
			: base()
		{
		}

		/// <summary>
		/// Notification event for when a contract has been updated.
		/// </summary>
		/// <param name="Contract">Requested contract.</param>
		/// <param name="e">Event arguments.</param>
		public ContractUpdatedNotificationEvent(Contract Contract, ContractReferenceEventArgs e)
			: base(Contract, e)
		{
		}

		/// <summary>
		/// Opens the event.
		/// </summary>
		public override async Task Open()
		{
			Contract? Contract = await this.GetContract();
			if (Contract is not null)
			{
				ViewContractNavigationArgs Args = new(Contract, false);

				await ServiceRef.UiService.GoToAsync(nameof(ViewContractPage), Args, BackMethod.Pop);
			}
		}

		/// <summary>
		/// Gets a descriptive text for the category of event.
		/// </summary>
		public override async Task<string> GetDescription()
		{
			Contract? Contract = await this.GetContract();
			if (Contract is null)
				return string.Empty;

			StringBuilder Result = new();

			Result.Append(ServiceRef.Localizer[nameof(AppResources.ContractUpdateReceived)]);

			if (Contract is not null)
			{
				Result.Append(": ");
				Result.Append(await ContractModel.GetCategory(Contract));
			}

			Result.Append('.');

			return Result.ToString();
		}
	}
}
