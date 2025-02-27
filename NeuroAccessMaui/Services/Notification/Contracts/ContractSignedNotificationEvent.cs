using NeuroAccessMaui.Extensions;
using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services.Contacts;
using NeuroAccessMaui.Services.UI;
using NeuroAccessMaui.Services.UI.Photos;
using NeuroAccessMaui.UI.Pages.Contracts.MyContracts.ObjectModels;
using NeuroAccessMaui.UI.Pages.Contracts.ViewContract;
using System.Text;
using System.Xml;
using Waher.Networking.XMPP.Contracts;
using Waher.Networking.XMPP.Contracts.EventArguments;

namespace NeuroAccessMaui.Services.Notification.Contracts
{
	/// <summary>
	/// Notification event for when a contract has been signed.
	/// </summary>
	public class ContractSignedNotificationEvent : ContractNotificationEvent
	{
		private LegalIdentity? identity;

		/// <summary>
		/// Notification event for when a contract has been signed.
		/// </summary>
		public ContractSignedNotificationEvent()
			: base()
		{
		}

		/// <summary>
		/// Notification event for when a contract has been signed.
		/// </summary>
		/// <param name="Contract">Requested contract.</param>
		/// <param name="e">Event arguments.</param>
		public ContractSignedNotificationEvent(Contract Contract, ContractSignedEventArgs e)
			: base(Contract, e)
		{
			this.LegalId = e.LegalId;
			this.Role = e.Role;
		}

		/// <summary>
		/// Legal ID signing the contract.
		/// </summary>
		public string? LegalId { get; set; }

		/// <summary>
		/// Role being signed
		/// </summary>
		public string? Role { get; set; }

		/// <summary>
		/// Opens the event.
		/// </summary>
		public override async Task Open()
		{
			Contract? Contract = await this.GetContract();
			ViewContractNavigationArgs Args = new(Contract, false);

			await ServiceRef.UiService.GoToAsync(nameof(ViewContractPage), Args, BackMethod.Pop);
		}

		/// <summary>
		/// Gets a descriptive text for the category of event.
		/// </summary>
		public override async Task<string> GetDescription()
		{
			Contract? Contract = await this.GetContract();
			StringBuilder Result = new();

			if (Contract is not null)
			{
				Result.Append(await ContractModel.GetCategory(Contract));
				Result.Append(": ");
			}

			if (this.identity is null)
				Result.Append(ServiceRef.Localizer[nameof(AppResources.ContractSignatureReceived)]);
			else
			{
				string FriendlyName = ContactInfo.GetFriendlyName(this.identity);
				Result.Append(ServiceRef.Localizer[nameof(AppResources.UserSignedAs), FriendlyName, this.Role ?? string.Empty]);
			}

			Result.Append('.');

			return Result.ToString();
		}

		/// <summary>
		/// XML of identity.
		/// </summary>
		public string? IdentityXml { get; set; }

		/// <summary>
		/// Gets a parsed identity.
		/// </summary>
		/// <returns>Parsed identity</returns>
		public LegalIdentity? Identity
		{
			get
			{
				if (this.identity is null && !string.IsNullOrEmpty(this.IdentityXml))
				{
					XmlDocument Doc = new()
					{
						PreserveWhitespace = true
					};
					Doc.LoadXml(this.IdentityXml);

					this.identity = LegalIdentity.Parse(Doc.DocumentElement);
				}

				return this.identity;
			}

			set
			{
				this.identity = value;

				if (value is null)
					this.IdentityXml = null;
				else
				{
					StringBuilder Xml = new();
					value.Serialize(Xml, true, true, true, true, true, true, true);
					this.IdentityXml = Xml.ToString();
				}
			}
		}

		/// <summary>
		/// Performs perparatory tasks, that will simplify opening the notification.
		/// </summary>
		public override async Task Prepare()
		{
			if (this.identity is null && !string.IsNullOrEmpty(this.LegalId))
			{
				try
				{
					this.Identity = await ServiceRef.XmppService.GetLegalIdentity(this.LegalId);
				}
				catch (Exception ex)
				{
					ServiceRef.LogService.LogException(ex,
						new KeyValuePair<string, object?>("ContractId", this.ContractId),
						new KeyValuePair<string, object?>("LegalId", this.LegalId),
						new KeyValuePair<string, object?>("Role", this.Role),
						new KeyValuePair<string, object?>(Constants.XmppProperties.Jid, ServiceRef.XmppService.BareJid));
				}
			}

			LegalIdentity? Identity = this.Identity;

			if (Identity?.Attachments is not null)
			{
				foreach (Attachment Attachment in Identity.Attachments.GetImageAttachments())
				{
					try
					{
						await PhotosLoader.LoadPhoto(Attachment);
					}
					catch (Exception ex)
					{
						ServiceRef.LogService.LogException(ex);
					}
				}
			}
		}

	}
}
