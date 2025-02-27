using System.ComponentModel;
using System.Text;
using NeuroAccessMaui.Extensions;
using NeuroAccessMaui.Services;
using NeuroAccessMaui.Services.Contacts;
using NeuroAccessMaui.Services.Notification;
using Waher.Content.Markdown;
using Waher.Networking.XMPP.Contracts;
using Waher.Networking.XMPP.Contracts.HumanReadable;
using Waher.Networking.XMPP.Contracts.HumanReadable.BlockElements;
using Waher.Networking.XMPP.Contracts.HumanReadable.InlineElements;

namespace NeuroAccessMaui.UI.Pages.Contracts.MyContracts.ObjectModels
{
	/// <summary>
	/// The data model for a contract.
	/// </summary>
	public class ContractModel : IUniqueItem, INotifyPropertyChanged
	{
		private readonly string contractId;
		private readonly string category;
		private readonly string name;
		private readonly DateTime timestamp;
		private readonly Contract contract;
		private NotificationEvent[] events;

		private ContractModel(string ContractId, DateTime Timestamp, Contract Contract, string Category, string Name, NotificationEvent[] Events)
		{
			this.contract = Contract;
			this.contractId = ContractId;
			this.timestamp = Timestamp;
			this.category = Category;
			this.name = Name;
			this.events = Events;
		}

		/// <summary>
		/// Creates an instance of the <see cref="ContractModel"/> class.
		/// </summary>
		/// <param name="ContractId">The contract id.</param>
		/// <param name="Timestamp">The timestamp to show with the contract reference.</param>
		/// <param name="Contract">Contract</param>
		/// <param name="Events">Notification events associated with contract.</param>
		public static async Task<ContractModel> Create(string ContractId, DateTime Timestamp, Contract Contract, NotificationEvent[] Events)
		{
			string Category = await GetCategory(Contract) ?? Contract.ForMachinesNamespace + "#" + Contract.ForMachinesLocalName;
			string Name = await GetName(Contract);

			if (string.IsNullOrEmpty(Name))
				Name = Contract.ContractId;

			return new ContractModel(ContractId, Timestamp, Contract, Category, Name, Events);
		}

		/// <summary>
		/// Gets a displayable name for a contract.
		/// </summary>
		/// <param name="Contract">Contract</param>
		/// <returns>Displayable Name</returns>
		public static async Task<string> GetName(Contract? Contract)
		{
			if (Contract?.Parts is null)
				return string.Empty;

			Dictionary<string, ClientSignature> Signatures = [];
			StringBuilder? sb = null;

			if (Contract.ClientSignatures is not null)
			{
				foreach (ClientSignature Signature in Contract.ClientSignatures)
					Signatures[Signature.LegalId] = Signature;
			}

			foreach (Part Part in Contract.Parts)
			{
				if (Part.LegalId == ServiceRef.TagProfile.LegalJid ||
					 (Signatures.TryGetValue(Part.LegalId, out ClientSignature? PartSignature) &&
					 string.Equals(PartSignature.BareJid, ServiceRef.XmppService.BareJid, StringComparison.OrdinalIgnoreCase)))
				{
					continue;   // Self
				}

				string FriendlyName = await ContactInfo.GetFriendlyName(Part.LegalId);

				if (sb is null)
					sb = new StringBuilder(FriendlyName);
				else
				{
					sb.Append(", ");
					sb.Append(FriendlyName);
				}
			}

			return sb?.ToString() ?? string.Empty;
		}

		/// <summary>
		/// Gets the category of a contract
		/// </summary>
		/// <param name="Contract">Contract</param>
		/// <returns>Contract Category</returns>
		public static async Task<string?> GetCategory(Contract Contract)
		{
			HumanReadableText[] Localizations = Contract.ForHumans;
			string Language = Contract.DeviceLanguage();

			foreach (HumanReadableText Localization in Localizations)
			{
				if (!string.Equals(Localization.Language, Language, StringComparison.OrdinalIgnoreCase))
					continue;

				foreach (BlockElement Block in Localization.Body)
				{
					if (Block is Section Section)
					{
						MarkdownOutput Markdown = new();

						foreach (InlineElement Item in Section.Header)
							await Item.GenerateMarkdown(Markdown, 1, 0, new Waher.Networking.XMPP.Contracts.HumanReadable.MarkdownSettings(Contract, MarkdownType.ForRendering));

						MarkdownDocument Doc = await MarkdownDocument.CreateAsync(Markdown.ToString());

						return (await Doc.GeneratePlainText()).Trim();
					}
				}
			}

			return null;
		}

		/// <summary>
		/// The contract id.
		/// </summary>
		public string ContractId => this.contractId;

		/// <inheritdoc/>
		public string UniqueName => this.ContractId;

		/// <summary>
		/// The created timestamp of the contract.
		/// </summary>
		public DateTime Timestamp => this.timestamp;

		/// <summary>
		/// A reference to the contract.
		/// </summary>
		public Contract Contract => this.contract;

		/// <summary>
		/// Displayable name for the contract.
		/// </summary>
		public string Name => this.name;

		/// <summary>
		/// Name, or category if no name.
		/// </summary>
		public string NameOrCategory => string.IsNullOrEmpty(this.name) ? this.category : this.name;

		/// <summary>
		/// Displayable category for the contract.
		/// </summary>
		public string Category => this.category;

		/// <summary>
		/// If the contract has associated notification events.
		/// </summary>
		public bool HasEvents => this.NrEvents > 0;

		/// <summary>
		/// Number of notification events associated with the contract.
		/// </summary>
		public int NrEvents => this.events?.Length ?? 0;

		/// <summary>
		/// Notification events.
		/// </summary>
		public NotificationEvent[] Events => this.events;

		/// <summary>
		/// Called when a property has changed.
		/// </summary>
		/// <param name="PropertyName">Name of property</param>
		public void OnPropertyChanged(string PropertyName)
		{
			try
			{
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
			}
		}

		/// <summary>
		/// Occurs when a property value changes.
		/// </summary>
		public event PropertyChangedEventHandler? PropertyChanged;

		/// <summary>
		/// Method called when notifications for the item have been updated.
		/// </summary>
		/// <param name="Events">Updated set of events.</param>
		public void NotificationsUpdated(NotificationEvent[] Events)
		{
			this.events = Events;

			this.OnPropertyChanged(nameof(this.Events));
			this.OnPropertyChanged(nameof(this.HasEvents));
			this.OnPropertyChanged(nameof(this.NrEvents));
		}

		/// <summary>
		/// Adds a notification event.
		/// </summary>
		/// <param name="Event">Notification event.</param>
		/// <returns>If event was added (true), or if it was ignored because the event was already in the list of events (false).</returns>
		public bool AddEvent(NotificationEvent Event)
		{
			if (this.events is null)
				this.NotificationsUpdated([ Event ]);
			else
			{
				foreach (NotificationEvent Event2 in this.events)
				{
					if (Event2.ObjectId == Event.ObjectId)
						return false;
				}

				int c = this.events.Length;
				NotificationEvent[] NewArray = new NotificationEvent[c + 1];
				Array.Copy(this.events, 0, NewArray, 0, c);
				NewArray[c] = Event;

				this.NotificationsUpdated(NewArray);
			}

			return true;
		}

		/// <summary>
		/// Removes a notification event.
		/// </summary>
		/// <param name="Event">Notification event.</param>
		/// <returns>If the event was found and removed.</returns>
		public bool RemoveEvent(NotificationEvent Event)
		{
			if (this.events is not null)
			{
				int i, c = this.events.Length;

				for (i = 0; i < c; i++)
				{
					NotificationEvent Event2 = this.events[i];

					if (Event2.ObjectId == Event.ObjectId)
					{
						NotificationEvent[] NewArray = new NotificationEvent[c - 1];

						if (i > 0)
							Array.Copy(this.events, 0, NewArray, 0, i);

						if (i < c - 1)
							Array.Copy(this.events, i + 1, NewArray, i, c - i - 1);

						this.NotificationsUpdated(NewArray);

						return true;
					}
				}
			}

			return false;
		}
	}
}
