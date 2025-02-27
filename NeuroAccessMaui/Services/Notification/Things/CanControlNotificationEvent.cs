using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services.Contacts;
using NeuroAccessMaui.UI.Pages.Things.CanControl;
using Waher.Networking.XMPP;
using Waher.Networking.XMPP.DataForms;
using Waher.Networking.XMPP.Provisioning;
using Waher.Networking.XMPP.Provisioning.Events;
using Waher.Persistence;
using Waher.Things;

namespace NeuroAccessMaui.Services.Notification.Things
{
	/// <summary>
	/// Contains information about a request to read a thing.
	/// </summary>
	public class CanControlNotificationEvent : ThingNotificationEvent
	{
		/// <summary>
		/// Contains information about a request to read a thing.
		/// </summary>
		public CanControlNotificationEvent()
			: base()
		{
		}

		/// <summary>
		/// Contains information about a request to read a thing.
		/// </summary>
		/// <param name="e">Event arguments</param>
		public CanControlNotificationEvent(CanControlEventArgs e)
			: base(e)
		{
			this.Parameters = e.Parameters;
		}

		/// <summary>
		/// Parameters requested
		/// </summary>
		public string[]? Parameters { get; set; }

		/// <summary>
		/// All parameters available
		/// </summary>
		public string[]? AllParameters { get; set; }

		/// <summary>
		/// Gets a descriptive text for the event.
		/// </summary>
		public override async Task<string> GetDescription()
		{
			string ThingName = await ContactInfo.GetFriendlyName(this.BareJid, this.SourceId ?? string.Empty,
				this.PartitionId ?? string.Empty, this.NodeId ?? string.Empty);

			string RemoteName = await ContactInfo.GetFriendlyName(this.RemoteJid);

			return ServiceRef.Localizer[nameof(AppResources.ControlRequestText), RemoteName, ThingName];
		}

		/// <summary>
		/// Opens the event.
		/// </summary>
		public override async Task Open()
		{
			string ThingName = await ContactInfo.GetFriendlyName(this.BareJid);
			string RemoteName = await ContactInfo.GetFriendlyName(this.RemoteJid);

			await ServiceRef.UiService.GoToAsync(nameof(CanControlPage), new CanControlNavigationArgs(this, ThingName, RemoteName));
		}

		/// <summary>
		/// Performs perparatory tasks, that will simplify opening the notification.
		/// </summary>
		public override async Task Prepare()
		{
			await base.Prepare();

			string[]? Parameters = await GetAvailableParameterNames(this.BareJid,
				new ThingReference(this.NodeId, this.SourceId, this.PartitionId));

			if (Parameters is not null)
			{
				this.AllParameters = Parameters;
				await Database.Update(this);
			}
		}

		/// <summary>
		/// Gets available fields for a thing.
		/// </summary>
		/// <param name="BareJid">Bare JID</param>
		/// <param name="Thing">Thing reference</param>
		/// <returns>Array of available field names, or null if not able.</returns>
		public static async Task<string[]?> GetAvailableParameterNames(string BareJid, ThingReference Thing)
		{
			try
			{
				RosterItem? Item = ServiceRef.XmppService.GetRosterItem(BareJid);
				if (Item is null || !Item.HasLastPresence || !Item.LastPresence.IsOnline)
					return null;

				TaskCompletionSource<DataForm?> FormTask = new();

				ServiceRef.XmppService.GetControlForm(Item.LastPresenceFullJid,
					System.Globalization.CultureInfo.CurrentCulture.TwoLetterISOLanguageName,
					(sender, e) =>
					{
						if (e.Ok)
							FormTask.TrySetResult(e.Form);
						else
							FormTask.TrySetResult(null);

						return Task.CompletedTask;
					}, null, [ Thing ]);

				Task _ = Task.Delay(30000).ContinueWith((_) => FormTask.TrySetResult(null));

				DataForm? Form = await FormTask.Task;

				if (Form is not null)
				{
					SortedDictionary<string, bool> Parameters = [];

					foreach (Field Field in Form.Fields)
					{
						if (!Field.ReadOnly && !string.IsNullOrEmpty(Field.Var))
							Parameters[Field.Var] = true;
					}

					string[] Parameters2 = new string[Parameters.Count];
					Parameters.Keys.CopyTo(Parameters2, 0);

					return Parameters2;
				}
				else
					return null;
			}
			catch (Exception)
			{
				return null;
			}
		}

	}
}
