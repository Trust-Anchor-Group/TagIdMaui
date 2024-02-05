﻿using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services.Navigation;
using NeuroAccessMaui.UI.Pages.Contracts.MyContracts.ObjectModels;
using NeuroAccessMaui.UI.Pages.Contracts.ViewContract;
using System.Text;
using Waher.Networking.XMPP.Contracts;

namespace NeuroAccessMaui.Services.Notification.Contracts
{
	/// <summary>
	/// Notification event for contract proposals.
	/// </summary>
	public class ContractProposalNotificationEvent : ContractNotificationEvent
	{
		/// <summary>
		/// Notification event for contract proposals.
		/// </summary>
		public ContractProposalNotificationEvent()
			: base()
		{
		}

		/// <summary>
		/// Notification event for contract proposals.
		/// </summary>
		/// <param name="e">Event arguments.</param>
		public ContractProposalNotificationEvent(ContractProposalEventArgs e)
			: base(e)
		{
			this.Role = e.Role;
			this.Message = e.MessageText;
		}

		/// <summary>
		/// Role
		/// </summary>
		public string? Role { get; set; }

		/// <summary>
		/// Message
		/// </summary>
		public string? Message { get; set; }

		/// <summary>
		/// Opens the event.
		/// </summary>
		public override async Task Open()
		{
			Contract? Contract = await this.GetContract();

			if (Contract is not null)
			{
				ViewContractNavigationArgs Args = new(Contract, false, this.Role, this.Message);

				await ServiceRef.NavigationService.GoToAsync(nameof(ViewContractPage), Args, BackMethod.Pop);
			}
		}

		/// <summary>
		/// Gets a descriptive text for the category of event.
		/// </summary>
		public override async Task<string> GetDescription()
		{
			Contract? Contract = await this.GetContract();
			StringBuilder Result = new();

			Result.Append(ServiceRef.Localizer[nameof(AppResources.ContractProposal)]);
			Result.Append(", ");
			Result.Append(this.Role);

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
