using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services.Contacts;
using NeuroAccessMaui.UI.Pages.Things.CanRead;
using Waher.Networking.XMPP;
using Waher.Networking.XMPP.Provisioning;
using Waher.Networking.XMPP.Provisioning.Events;
using Waher.Networking.XMPP.Sensor;
using Waher.Persistence;
using Waher.Things;
using Waher.Things.SensorData;

namespace NeuroAccessMaui.Services.Notification.Things
{
	/// <summary>
	/// Contains information about a request to read a thing.
	/// </summary>
	public class CanReadNotificationEvent : ThingNotificationEvent
	{
		/// <summary>
		/// Contains information about a request to read a thing.
		/// </summary>
		public CanReadNotificationEvent()
			: base()
		{
		}

		/// <summary>
		/// Contains information about a request to read a thing.
		/// </summary>
		/// <param name="e">Event arguments</param>
		public CanReadNotificationEvent(CanReadEventArgs e)
			: base(e)
		{
			this.Fields = e.Fields;
			this.FieldTypes = e.FieldTypes;
		}

		/// <summary>
		/// Fields requested
		/// </summary>
		public string[]? Fields { get; set; }

		/// <summary>
		/// All fields available
		/// </summary>
		public string[]? AllFields { get; set; }

		/// <summary>
		/// Field types requested.
		/// </summary>
		public FieldType FieldTypes { get; set; }

		/// <summary>
		/// Gets a descriptive text for the event.
		/// </summary>
		public override async Task<string> GetDescription()
		{
			string ThingName = await ContactInfo.GetFriendlyName(this.BareJid, this.SourceId ?? string.Empty,
				this.PartitionId ?? string.Empty, this.NodeId ?? string.Empty);

			string RemoteName = await ContactInfo.GetFriendlyName(this.RemoteJid);

			return ServiceRef.Localizer[nameof(AppResources.ReadoutRequestText), RemoteName, ThingName];
		}

		/// <summary>
		/// Opens the event.
		/// </summary>
		public override async Task Open()
		{
			string ThingName = await ContactInfo.GetFriendlyName(this.BareJid);
			string RemoteName = await ContactInfo.GetFriendlyName(this.RemoteJid);

			await ServiceRef.UiService.GoToAsync(nameof(CanReadPage), new CanReadNavigationArgs(this, ThingName, RemoteName));
		}

		/// <summary>
		/// Performs perparatory tasks, that will simplify opening the notification.
		/// </summary>
		public override async Task Prepare()
		{
			await base.Prepare();

			string[]? Fields = await GetAvailableFieldNames(this.BareJid, new ThingReference(this.NodeId, this.SourceId, this.PartitionId));

			if (Fields is not null)
			{
				this.AllFields = Fields;
				await Database.Update(this);
			}
		}

		/// <summary>
		/// Gets available fields for a thing.
		/// </summary>
		/// <param name="BareJid">Bare JID</param>
		/// <param name="Thing">Thing reference</param>
		/// <returns>Array of available field names, or null if not able.</returns>
		public static async Task<string[]?> GetAvailableFieldNames(string BareJid, ThingReference Thing)
		{
			try
			{
				RosterItem? Item = ServiceRef.XmppService.GetRosterItem(BareJid);
				if (Item is null || !Item.HasLastPresence || !Item.LastPresence.IsOnline)
					return null;

				SortedDictionary<string, bool> Fields = [];
				SensorDataClientRequest Request = await ServiceRef.XmppService.RequestSensorReadout(Item.LastPresenceFullJid, [ Thing ], FieldType.All);
				TaskCompletionSource<bool> Done = new();

				Request.OnFieldsReceived += (sender, NewFields) =>
				{
					foreach (Field Field in NewFields)
						Fields[Field.Name] = true;

					return Task.CompletedTask;
				};

				Request.OnStateChanged += (Sender, NewState) =>
				{
					switch (NewState)
					{
						case SensorDataReadoutState.Done:
							Done.TrySetResult(true);
							break;

						case SensorDataReadoutState.Cancelled:
						case SensorDataReadoutState.Failure:
							Done.TrySetResult(false);
							break;
					}

					return Task.CompletedTask;
				};

				Task _ = Task.Delay(30000).ContinueWith((_) => Done.TrySetResult(false));

				if (await Done.Task)
				{
					string[] Fields2 = new string[Fields.Count];
					Fields.Keys.CopyTo(Fields2, 0);

					return Fields2;
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
