using Microsoft.Maui.Controls.Shapes;
using NeuroAccessMaui.UI;
using NeuroAccessMaui.UI.Pages.Contracts.MyContracts.ObjectModels;
using System.Text;
using System.Xml;
using Waher.Networking.XMPP.Contracts;
using Waher.Networking.XMPP.Contracts.EventArguments;

namespace NeuroAccessMaui.Services.Notification.Contracts
{
	/// <summary>
	/// Abstract base class of Contract notification events.
	/// </summary>
	public abstract class ContractNotificationEvent : NotificationEvent
	{
		private Contract? contract;

		/// <summary>
		/// Abstract base class of Contract notification events.
		/// </summary>
		public ContractNotificationEvent()
			: base()
		{
		}

		/// <summary>
		/// Abstract base class of Contract notification events.
		/// </summary>
		/// <param name="e">Event arguments</param>
		public ContractNotificationEvent(ContractProposalEventArgs e)
			: base()
		{
			this.ContractId = e.ContractId;
			this.Category = e.ContractId;
			this.Type = NotificationEventType.Contracts;
			this.Received = DateTime.UtcNow;
		}

		/// <summary>
		/// Abstract base class of Contract notification events.
		/// </summary>
		/// <param name="e">Event arguments</param>
		public ContractNotificationEvent(ContractPetitionResponseEventArgs e)
			: base()
		{
			this.ContractId = e.RequestedContract?.ContractId ?? string.Empty;
			this.Category = this.ContractId;
			this.Type = NotificationEventType.Contracts;
			this.Received = DateTime.UtcNow;

			this.SetContract(e.RequestedContract);
		}

		/// <summary>
		/// Abstract base class of Contract notification events.
		/// </summary>
		/// <param name="Contract">Requested contract.</param>
		/// <param name="e">Event arguments</param>
		public ContractNotificationEvent(Contract Contract, ContractPetitionEventArgs e)
			: base()
		{
			this.ContractId = Contract.ContractId;
			this.Category = e.RequestedContractId;
			this.Type = NotificationEventType.Contracts;
			this.Received = DateTime.UtcNow;

			this.SetContract(Contract);
		}

		/// <summary>
		/// Abstract base class of Contract notification events.
		/// </summary>
		/// <param name="Contract">Requested contract.</param>
		/// <param name="e">Event arguments</param>
		public ContractNotificationEvent(Contract Contract, ContractReferenceEventArgs e)
			: base()
		{
			this.ContractId = Contract.ContractId;
			this.Category = e.ContractId;
			this.Type = NotificationEventType.Contracts;
			this.Received = DateTime.UtcNow;

			this.SetContract(Contract);
		}

		/// <summary>
		/// Contract ID.
		/// </summary>
		public string? ContractId { get; set; }

		/// <summary>
		/// XML of contract.
		/// </summary>
		public string? ContractXml { get; set; }

		/// <summary>
		/// Gets a parsed contract.
		/// </summary>
		/// <returns>Parsed contract</returns>
		public async Task<Contract?> GetContract()
		{
			if (this.contract is null && !string.IsNullOrEmpty(this.ContractXml))
			{
				XmlDocument Doc = new()
				{
					PreserveWhitespace = true
				};
				Doc.LoadXml(this.ContractXml);

				ParsedContract Parsed = await Contract.Parse(Doc.DocumentElement, ServiceRef.XmppService.ContractsClient, false);

				this.contract = Parsed?.Contract;
			}

			return this.contract;
		}

		/// <summary>
		/// Sets a parsed contract.
		/// </summary>
		/// <param name="Contract">Contract</param>
		public void SetContract(Contract? Contract)
		{
			this.contract = Contract;

			if (Contract is null)
				this.ContractXml = null;
			else
			{
				StringBuilder Xml = new();
				Contract.Serialize(Xml, true, true, true, true, true, true, true);
				this.ContractXml = Xml.ToString();
			}
		}

		/// <summary>
		/// Gets an icon for the category of event.
		/// </summary>
		/// <returns>Icon</returns>
		public override Task<Geometry> GetCategoryIcon()
		{
			return Task.FromResult(Geometries.ContractPath);
		}

		/// <summary>
		/// Gets a descriptive text for the category of event.
		/// </summary>
		public override async Task<string> GetDescription()
		{
			Contract? Contract = await this.GetContract();

			if (Contract is null)
				return this.ContractId ?? string.Empty;
			else
				return await ContractModel.GetCategory(Contract) ?? string.Empty;
		}

	}
}
