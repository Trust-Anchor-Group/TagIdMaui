using NeuroAccessMaui.UI.Pages.Wallet.TokenEvents.Events;
using NeuroFeatures.Events;
using System.Text;
using System.Windows.Input;
using Waher.Content;
using Waher.Networking.XMPP.HttpFileUpload;
using NeuroAccessMaui.Services;
using NeuroAccessMaui.Services.Contacts;
using NeuroAccessMaui.UI.Pages.Contacts.Chat;
using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services.UI;

namespace NeuroAccessMaui.UI.Pages.Wallet.TokenEvents
{
	/// <summary>
	/// Types of events
	/// </summary>
	public enum EventType
	{
		/// <summary>
		/// Creation of token
		/// </summary>
		Created,

		/// <summary>
		/// Destruction of token
		/// </summary>
		Destroyed,

		/// <summary>
		/// Token transferred to new owner
		/// </summary>
		Transferred,

		/// <summary>
		/// Token donated to new owner
		/// </summary>
		Donated,

		/// <summary>
		/// Text note made by owner at the time.
		/// </summary>
		NoteText,

		/// <summary>
		/// XML note made by owner at the time.
		/// </summary>
		NoteXml,

		/// <summary>
		/// Text note made by an external source at the time.
		/// </summary>
		ExternalNoteText,

		/// <summary>
		/// XML note made by an external source at the time.
		/// </summary>
		ExternalNoteXml
	}

	/// <summary>
	/// Represents a token event.
	/// </summary>
	public abstract class EventItem
	{
		private readonly TokenEvent @event;

		/// <summary>
		/// Represents a token event.
		/// </summary>
		/// <param name="Event">Token event</param>
		public EventItem(TokenEvent Event)
		{
			this.@event = Event;

			this.ViewIdCommand = new Command(async P => await ViewId((string)P));
			this.ViewContractCommand = new Command(async P => await ViewContract((string)P));
			this.ViewSourceCommand = new Command(async P => await this.ViewSource((string)P));
			this.CopyToClipboardCommand = new Command(async P => await CopyToClipboard((string)P));
			this.ViewXmlInBrowserCommand = new Command(async P => await ViewXmlInBrowser((string)P));
		}

		/// <summary>
		/// Token ID
		/// </summary>
		public string TokenId => this.@event.TokenId;

		/// <summary>
		/// Personal
		/// </summary>
		public bool Personal => this.@event.Personal;

		/// <summary>
		/// Timestamp
		/// </summary>
		public DateTime Timestamp => this.@event.Timestamp;

		/// <summary>
		/// When event expires
		/// </summary>
		public DateTime Expires => this.@event.Expires;

		/// <summary>
		/// Required Archiving time (after event expires)
		/// </summary>
		public Duration? ArchiveRequired => this.@event.ArchiveRequired;

		/// <summary>
		/// Optional Archiving time (after required archiving time elapses).
		/// </summary>
		public Duration? ArchiveOptional => this.@event.ArchiveOptional;

		/// <summary>
		/// Type of event.
		/// </summary>
		public abstract EventType Type { get; }

		/// <summary>
		/// Command executed when the user wants to view an ID
		/// </summary>
		public ICommand ViewIdCommand { get; }

		/// <summary>
		/// Command executed when the user wants to view a smart contract
		/// </summary>
		public ICommand ViewContractCommand { get; }

		/// <summary>
		/// Command executed when the user wants to view the source of an external note
		/// </summary>
		public ICommand ViewSourceCommand { get; }

		/// <summary>
		/// Command executed when the user wants to copy a text note to the clipboard
		/// </summary>
		public ICommand CopyToClipboardCommand { get; }

		/// <summary>
		/// Command executed when the user wants to view an XML note in the browser
		/// </summary>
		public ICommand ViewXmlInBrowserCommand { get; }

		/// <summary>
		/// Creates an Event Item view model for a token event.
		/// </summary>
		/// <param name="Event">Token event.</param>
		/// <returns>View model of token event.</returns>
		public static EventItem Create(TokenEvent Event)
		{
			if (Event is Created Created)
				return new CreatedItem(Created);
			else if (Event is Destroyed Destroyed)
				return new DestroyedItem(Destroyed);
			else if (Event is NoteText NoteText)
				return new NoteTextItem(NoteText);
			else if (Event is NoteXml NoteXml)
				return new NoteXmlItem(NoteXml);
			else if (Event is ExternalNoteText ExternalNoteText)
				return new ExternalNoteTextItem(ExternalNoteText);
			else if (Event is ExternalNoteXml ExternalNoteXml)
				return new ExternalNoteXmlItem(ExternalNoteXml);
			else if (Event is Transferred Transferred)
				return new TransferredItem(Transferred);
			else if (Event is Donated Donated)
				return new DonatedItem(Donated);
			else
			{
				return new NoteTextItem(new NoteText()
				{
					ArchiveOptional = Event.ArchiveOptional,
					ArchiveRequired = Event.ArchiveRequired,
					Expires = Event.Expires,
					Personal = Event.Personal,
					Timestamp = Event.Timestamp,
					TokenId = Event.TokenId,
					Note = ServiceRef.Localizer[nameof(AppResources.UnrecognizedEventType)] + " " + Event.GetType().FullName
				});
			}
		}

		/// <summary>
		/// Binds properties
		/// </summary>
		public virtual Task DoBind()
		{
			return Task.CompletedTask;
		}

		/// <summary>
		/// Shows a Legal Identity to the suer.
		/// </summary>
		/// <param name="IdentityId">Identity ID</param>
		public static Task ViewId(string IdentityId)
		{
			return ServiceRef.ContractOrchestratorService.OpenLegalIdentity(IdentityId, ServiceRef.Localizer[nameof(AppResources.PurposeReviewToken)]);
		}

		/// <summary>
		/// Shows a Smart Contract to the user.
		/// </summary>
		/// <param name="ContractId">Contract ID</param>
		public static Task ViewContract(string ContractId)
		{
			return ServiceRef.ContractOrchestratorService.OpenContract(ContractId, ServiceRef.Localizer[nameof(AppResources.PurposeReviewToken)], null);
		}

		/// <summary>
		/// Copies text to the clipboard.
		/// </summary>
		/// <param name="Text">Text to copy.</param>
		public static async Task CopyToClipboard(string Text)
		{
			await Clipboard.SetTextAsync(Text);
			await ServiceRef.UiService.DisplayAlert(ServiceRef.Localizer[nameof(AppResources.SuccessTitle)],
				ServiceRef.Localizer[nameof(AppResources.NoteCopiedToClipboard)]);
		}

		/// <summary>
		/// Displays XML in a browser.
		/// </summary>
		/// <param name="Xml">XML to display.</param>
		public static async Task ViewXmlInBrowser(string Xml)
		{
			try
			{
				byte[] Bin = Encoding.UTF8.GetBytes(Xml);
				HttpFileUploadEventArgs e = await ServiceRef.XmppService.RequestUploadSlotAsync("Note.xml", "text/xml; charset=utf-8", Bin.Length);

				if (e.Ok)
				{
					await e.PUT(Bin, "text/xml", (int)Constants.Timeouts.UploadFile.TotalMilliseconds);
					await App.OpenUrlAsync(e.GetUrl);
				}
				else
					await ServiceRef.UiService.DisplayException(e.StanzaError ?? new Exception(e.ErrorText));
			}
			catch (Exception ex)
			{
				await ServiceRef.UiService.DisplayException(ex);
			}
		}

		/// <summary>
		/// Displays the source of an external note.
		/// </summary>
		/// <param name="Source">Source of external note.</param>
		public async Task ViewSource(string Source)
		{
			try
			{
				int i = Source.IndexOf('@');
				if (i < 0)
				{
					if (Source.IndexOf(':') < 0)
						Source = "https://" + Source;
				}
				else
				{
					string Account = Source[..i];

					if (Guid.TryParse(Account, out _))
						Source = Constants.UriSchemes.IotId + ":" + Source;
					else
					{
						ContactInfo Contact = await ContactInfo.FindByBareJid(Source);
						ChatNavigationArgs Args = new(Contact?.LegalId, Contact?.BareJid ?? Source, Contact?.FriendlyName ?? Source);

						await ServiceRef.UiService.GoToAsync(nameof(ChatPage), Args, BackMethod.Inherited, Contact?.BareJid ?? Source);
						return;
					}
				}

				await App.OpenUrlAsync(Source);
			}
			catch (Exception ex)
			{
				await ServiceRef.UiService.DisplayException(ex);
			}
		}
	}
}
