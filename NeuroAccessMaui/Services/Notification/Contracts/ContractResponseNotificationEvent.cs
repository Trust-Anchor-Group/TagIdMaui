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
	/// Notification event for contract petition responses.
	/// </summary>
	public class ContractResponseNotificationEvent : ContractNotificationEvent
	{
		/// <summary>
		/// Notification event for contract petition responses.
		/// </summary>
		public ContractResponseNotificationEvent()
			: base()
		{
		}

		/// <summary>
		/// Notification event for contract petition responses.
		/// </summary>
		/// <param name="e">Event arguments.</param>
		public ContractResponseNotificationEvent(ContractPetitionResponseEventArgs e)
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
			Contract? Contract = await this.GetContract();

			if (!this.Response || Contract is null)
			{
				await ServiceRef.UiService.DisplayAlert(ServiceRef.Localizer[nameof(AppResources.Message)],
					ServiceRef.Localizer[nameof(AppResources.PetitionToViewContractWasDenied)], ServiceRef.Localizer[nameof(AppResources.Ok)]);
			}
			else
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
			StringBuilder Result = new();

			Result.Append(ServiceRef.Localizer[nameof(AppResources.ContractResponse)]);

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
