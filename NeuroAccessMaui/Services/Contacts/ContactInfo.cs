using NeuroAccessMaui.Resources.Languages;
using System.Globalization;
using System.Text;
using Waher.Networking.XMPP;
using Waher.Networking.XMPP.Contracts;
using Waher.Persistence;
using Waher.Persistence.Attributes;
using Waher.Persistence.Filters;

namespace NeuroAccessMaui.Services.Contacts
{
	/// <summary>
	/// Contains information about a contact.
	/// </summary>
	[CollectionName("ContactInformation")]
	[TypeName(TypeNameSerialization.None)]
	[Index("BareJid", "SourceId", "Partition", "NodeId")]
	[Index("IsThing", "BareJid", "SourceId", "Partition", "NodeId")]
	[Index("LegalId")]
	public class ContactInfo
	{
		private string? objectId = null;
		private CaseInsensitiveString bareJid = CaseInsensitiveString.Empty;
		private CaseInsensitiveString legalId = CaseInsensitiveString.Empty;
		private LegalIdentity? legalIdentity = null;
		private Property[]? metaData = null;
		private string friendlyName = string.Empty;
		private string alias = string.Empty;
		private string sourceId = string.Empty;
		private string partition = string.Empty;
		private string nodeId = string.Empty;
		private CaseInsensitiveString registryJid = string.Empty;
		private bool? subscribeTo = null;
		private bool? allowSubscriptionFrom = null;
		private bool? isThing = null;
		private bool? isSensor = null;
		private bool? isActuator = null;
		private bool? isConcentrator = null;
		private bool? supportsSensorEvents = null;
		private bool? owner = null;

		/// <summary>
		/// Contains information about a contact.
		/// </summary>
		public ContactInfo()
		{
		}

		/// <summary>
		/// Object ID
		/// </summary>
		[ObjectId]
		public string? ObjectId
		{
			get => this.objectId;
			set => this.objectId = value;
		}

		/// <summary>
		/// Bare JID of contact.
		/// </summary>
		public CaseInsensitiveString BareJid
		{
			get => this.bareJid;
			set => this.bareJid = value;
		}

		/// <summary>
		/// Legal ID of contact.
		/// </summary>
		public CaseInsensitiveString LegalId
		{
			get => this.legalId;
			set => this.legalId = value;
		}

		/// <summary>
		/// Legal Identity object.
		/// </summary>
		[DefaultValueNull]
		public LegalIdentity? LegalIdentity
		{
			get => this.legalIdentity;
			set => this.legalIdentity = value;
		}

		/// <summary>
		/// Friendly name.
		/// </summary>
		[DefaultValueStringEmpty]
		public string FriendlyName
		{
			get => this.friendlyName;
			set => this.friendlyName = value;
		}

		/// <summary>
		/// Alias name (user set friendly name)
		/// </summary>
		[DefaultValueStringEmpty]
		public string Alias
		{
			get => this.alias;
			set => this.alias = value;
		}

		/// <summary>
		/// Source ID
		/// </summary>
		public string SourceId
		{
			get => this.sourceId;
			set => this.sourceId = value;
		}

		/// <summary>
		/// Partition
		/// </summary>
		public string Partition
		{
			get => this.partition;
			set => this.partition = value;
		}

		/// <summary>
		/// Node ID
		/// </summary>
		public string NodeId
		{
			get => this.nodeId;
			set => this.nodeId = value;
		}

		/// <summary>
		/// Registry JID
		/// </summary>
		public CaseInsensitiveString RegistryJid
		{
			get => this.registryJid;
			set => this.registryJid = value;
		}

		/// <summary>
		/// Subscribe to this contact
		/// </summary>
		public bool? SubscribeTo
		{
			get => this.subscribeTo;
			set => this.subscribeTo = value;
		}

		/// <summary>
		/// Allow subscriptions from this contact
		/// </summary>
		public bool? AllowSubscriptionFrom
		{
			get => this.allowSubscriptionFrom;
			set => this.allowSubscriptionFrom = value;
		}

		/// <summary>
		/// The contact is a thing
		/// </summary>
		public bool? IsThing
		{
			get => this.isThing;
			set => this.isThing = value;
		}

		/// <summary>
		/// The contact is a sensor
		/// </summary>
		public bool? IsSensor
		{
			get => this.isSensor;
			set => this.isSensor = value;
		}

		/// <summary>
		/// The contact supports sensor events
		/// </summary>
		public bool? SupportsSensorEvents
		{
			get => this.supportsSensorEvents;
			set => this.supportsSensorEvents = value;
		}

		/// <summary>
		/// The contact is a actuator
		/// </summary>
		public bool? IsActuator
		{
			get => this.isActuator;
			set => this.isActuator = value;
		}

		/// <summary>
		/// The contact is a concentrator
		/// </summary>
		public bool? IsConcentrator
		{
			get => this.isConcentrator;
			set => this.isConcentrator = value;
		}

		/// <summary>
		/// If the account is registered as the owner of the thing.
		/// </summary>
		[DefaultValueNull]
		public bool? Owner
		{
			get => this.owner;
			set => this.owner = value;
		}

		/// <summary>
		/// Meta-data related to a thing.
		/// </summary>
		[DefaultValueNull]
		public Property[]? MetaData
		{
			get => this.metaData;
			set => this.metaData = value;
		}

		/// <summary>
		/// Finds information about a contact, given its Bare JID.
		/// </summary>
		/// <param name="BareJid">Bare JID</param>
		/// <returns>Contact information, if found.</returns>
		public static Task<ContactInfo> FindByBareJid(string BareJid)
		{
			return Database.FindFirstIgnoreRest<ContactInfo>(new FilterFieldEqualTo("BareJid", BareJid));
		}

		/// <summary>
		/// Finds information about a contact, given its Bare JID.
		/// </summary>
		/// <param name="BareJid">Bare JID</param>
		/// <param name="SourceId">Source ID</param>
		/// <param name="Partition">Partition</param>
		/// <param name="NodeId">Node ID</param>
		/// <returns>Contact information, if found.</returns>
		public static Task<ContactInfo> FindByBareJid(string BareJid, string SourceId, string Partition, string NodeId)
		{
			return Database.FindFirstIgnoreRest<ContactInfo>(new FilterAnd(
				new FilterFieldEqualTo("BareJid", BareJid),
				new FilterFieldEqualTo("SourceId", SourceId),
				new FilterFieldEqualTo("Partition", Partition),
				new FilterFieldEqualTo("NodeId", NodeId)));
		}

		/// <summary>
		/// Finds information about a contact, given its Legal ID.
		/// </summary>
		/// <param name="LegalId">Legal ID</param>
		/// <returns>Contact information, if found.</returns>
		public static Task<ContactInfo> FindByLegalId(string LegalId)
		{
			return Database.FindFirstIgnoreRest<ContactInfo>(new FilterFieldEqualTo("LegalId", LegalId));
		}

		/// <summary>
		/// Gets the friendly name of a remote identity (Legal ID or Bare JID).
		/// </summary>
		/// <param name="RemoteId">Remote Identity</param>
		/// <returns>Friendly name.</returns>
		public static async Task<string> GetFriendlyName(CaseInsensitiveString RemoteId)
		{
			int i = RemoteId.IndexOf('@');

			if (i < 0)
				return RemoteId;

			if (RemoteId == ServiceRef.TagProfile.LegalIdentity?.Id)
				return ServiceRef.Localizer[nameof(AppResources.Me)];

			string Account = RemoteId.Substring(0, i);
			ContactInfo Info;
			bool AccountIsGuid;

			if (AccountIsGuid = Guid.TryParse(Account, out Guid _))
			{
				Info = await FindByLegalId(RemoteId);

				if (!string.IsNullOrEmpty(Info?.FriendlyName))
					return Info.FriendlyName;

				if (Info?.LegalIdentity is not null)
					return GetFriendlyName(Info.LegalIdentity);
			}

			Info = await FindByBareJid(RemoteId);
			if (Info is not null)
			{
				if (!string.IsNullOrEmpty(Info.FriendlyName))
					return Info.FriendlyName;

				if (Info.LegalIdentity is not null)
					return GetFriendlyName(Info.LegalIdentity);

				if (Info.MetaData is not null)
				{
					string? s = GetFriendlyName(Info.MetaData);

					if (!string.IsNullOrEmpty(s))
						return s;
				}
			}

			RosterItem? Item = ServiceRef.XmppService.GetRosterItem(RemoteId);
			if (Item is not null)
				return Item.NameOrBareJid;

			lock (identityCache)
				if (identityCache.TryGetValue(RemoteId, out LegalIdentity? Id))
					if (Id is not null)
						return GetFriendlyName(Id);
					else
						AccountIsGuid = false;

			if (AccountIsGuid)
			{
				lock (identityCache)
					identityCache[RemoteId] = null;

				Task _ = Task.Run(async () =>
				{
					try
					{
						LegalIdentity Id = await ServiceRef.XmppService.GetLegalIdentity(RemoteId);

						lock (identityCache)
							identityCache[RemoteId] = Id;
					}
					catch (Exception)
					{
						// Ignore
					}
				});
			}

			return RemoteId;
		}

		private static readonly Dictionary<CaseInsensitiveString, LegalIdentity?> identityCache = [];

		/// <summary>
		/// Gets the friendly name of a remote identity (Legal ID or Bare JID).
		/// </summary>
		/// <param name="RemoteId">Remote Identity</param>
		/// <returns>Friendly name.</returns>
		public static async Task<string[]?> GetFriendlyName(string[] RemoteId)
		{
			if (RemoteId is null)
				return null;

			int i, c = RemoteId.Length;
			string[] Result = new string[c];

			for (i = 0; i < c; i++)
				Result[i] = await GetFriendlyName(RemoteId[i]);

			return Result;
		}

		/// <summary>
		/// Gets the friendly name from a set of meta-data tags.
		/// </summary>
		/// <param name="MetaData">Meta-data tags.</param>
		/// <returns>Friendly name</returns>
		public static string? GetFriendlyName(IEnumerable<Property> MetaData)
		{
			string? Apartment = null;
			string? Area = null;
			string? Building = null;
			string? City = null;
			string? Class = null;
			string? Country = null;
			string? Manufacturer = null;
			string? MeterLocation = null;
			string? MeterNumber = null;
			string? Model = null;
			string? Name = null;
			string? Region = null;
			string? Room = null;
			string? SerialNumber = null;
			string? Street = null;
			string? StreetNumber = null;
			string? Version = null;
			string? Phone = null;

			foreach (Property P in MetaData)
				switch (P.Name.ToUpper(CultureInfo.InvariantCulture))
				{
					case Constants.XmppProperties.Apartment:
						Apartment = P.Value;
						break;

					case Constants.XmppProperties.Area:
						Area = P.Value;
						break;

					case Constants.XmppProperties.Building:
						Building = P.Value;
						break;

					case Constants.XmppProperties.City:
						City = P.Value;
						break;

					case Constants.XmppProperties.Country:
						Country = P.Value;
						break;

					case Constants.XmppProperties.Region:
						Region = P.Value;
						break;

					case Constants.XmppProperties.Room:
						Room = P.Value;
						break;

					case Constants.XmppProperties.StreetName:
						Street = P.Value;
						break;

					case Constants.XmppProperties.StreetNumber:
						StreetNumber = P.Value;
						break;

					case Constants.XmppProperties.Class:
						Class = P.Value;
						break;

					case Constants.XmppProperties.Manufacturer:
						Manufacturer = P.Value;
						break;

					case Constants.XmppProperties.MeterLocation:
						MeterLocation = P.Value;
						break;

					case Constants.XmppProperties.MeterNumber:
						MeterNumber = P.Value;
						break;

					case Constants.XmppProperties.Model:
						Model = P.Value;
						break;

					case Constants.XmppProperties.Name:
						Name = P.Value;
						break;

					case Constants.XmppProperties.SerialNumber:
						SerialNumber = P.Value;
						break;

					case Constants.XmppProperties.Version:
						Version = P.Value;
						break;

					case Constants.XmppProperties.Phone:
						Phone = P.Value;
						break;
				}

			StringBuilder? sb = null;

			AppendName(ref sb, Class);
			AppendName(ref sb, Model);
			AppendName(ref sb, Version);
			AppendName(ref sb, Room);
			AppendName(ref sb, Name);
			AppendName(ref sb, Apartment);
			AppendName(ref sb, Building);
			AppendName(ref sb, Street);
			AppendName(ref sb, StreetNumber);
			AppendName(ref sb, Area);
			AppendName(ref sb, City);
			AppendName(ref sb, Region);
			AppendName(ref sb, Country);
			AppendName(ref sb, MeterNumber);
			AppendName(ref sb, MeterLocation);
			AppendName(ref sb, Manufacturer);
			AppendName(ref sb, SerialNumber);
			AppendName(ref sb, Phone);

			return sb?.ToString();
		}

		/// <summary>
		/// Gets the friendly name of a legal identity.
		/// </summary>
		/// <param name="Identity">Legal Identity</param>
		/// <returns>Friendly name</returns>
		public static string GetFriendlyName(LegalIdentity? Identity)
		{
			if (Identity is null)
				return string.Empty;

			string? FirstName = null;
			string? MiddleName = null;
			string? LastName = null;
			string? PersonalNumber = null;
			string? Domain = null;
			string? OrgDepartment = null;
			string? OrgRole = null;
			string? OrgName = null;
			bool HasName = false;
			bool HasOrg = false;
			bool HasDomain = false;

			foreach (Property P in Identity.Properties)
			{
				switch (P.Name.ToUpper(CultureInfo.InvariantCulture))
				{
					case Constants.XmppProperties.FirstName:
						FirstName = P.Value;
						HasName = true;
						break;

					case Constants.XmppProperties.MiddleNames:
						MiddleName = P.Value;
						HasName = true;
						break;

					case Constants.XmppProperties.LastNames:
						LastName = P.Value;
						HasName = true;
						break;

					case Constants.XmppProperties.PersonalNumber:
						PersonalNumber = P.Value;
						break;

					case Constants.XmppProperties.OrgName:
						OrgName = P.Value;
						HasOrg = true;
						break;

					case Constants.XmppProperties.OrgDepartment:
						OrgDepartment = P.Value;
						HasOrg = true;
						break;

					case Constants.XmppProperties.OrgRole:
						OrgRole = P.Value;
						HasOrg = true;
						break;

					case Constants.XmppProperties.Domain:
						Domain = P.Value;
						HasDomain = true;
						break;
				}
			}

			StringBuilder? sb = null;

			if (HasName)
			{
				AppendName(ref sb, FirstName);
				AppendName(ref sb, MiddleName);
				AppendName(ref sb, LastName);
			}

			if (HasOrg)
			{
				AppendName(ref sb, OrgRole);
				AppendName(ref sb, OrgDepartment);
				AppendName(ref sb, OrgName);
			}

			string Result = sb is not null ? sb.ToString() : !string.IsNullOrEmpty(PersonalNumber) ? PersonalNumber : Identity.Id;

			if (HasDomain)
				Result = Domain + " (" + Result + ")";

			return Result;
		}

		/// <summary>
		/// Gets the full name of a person.
		/// </summary>
		/// <param name="Identity">Legal identity</param>
		public static string GetFullName(LegalIdentity? Identity)
		{
			string? FirstName = null;
			string? MiddleNames = null;
			string? LastNames = null;

			if (Identity?.Properties is not null)
				foreach (Property P in Identity.Properties)
					switch (P.Name)
					{
						case Constants.XmppProperties.FirstName:
							FirstName = P.Value;
							break;

						case Constants.XmppProperties.LastNames:
							LastNames = P.Value;
							break;

						case Constants.XmppProperties.MiddleNames:
							MiddleNames = P.Value;
							break;
					}

			return GetFullName(FirstName, MiddleNames, LastNames);
		}

		/// <summary>
		/// Gets the full name of a person.
		/// </summary>
		/// <param name="FirstName">First name</param>
		/// <param name="MiddleNames">Middle name(s)</param>
		/// <param name="LastNames">Last name(s)</param>
		public static string GetFullName(string? FirstName, string? MiddleNames, string? LastNames)
		{
			StringBuilder? sb = null;

			AppendName(ref sb, FirstName);
			AppendName(ref sb, MiddleNames);
			AppendName(ref sb, LastNames);

			return sb?.ToString() ?? string.Empty;
		}

		/// <summary>
		/// Appends a value, if non-empty.
		/// </summary>
		/// <param name="sb">String-builder</param>
		/// <param name="Value">Value to add.</param>
		internal static void AppendName(ref StringBuilder? sb, string? Value)
		{
			if (!string.IsNullOrEmpty(Value))
			{
				if (sb is null)
					sb = new StringBuilder();
				else
					sb.Append(' ');

				sb.Append(Value);
			}
		}

		/// <summary>
		/// Gets the friendly name of a thing.
		/// </summary>
		/// <param name="BareJid">Bare JID of device</param>
		/// <param name="SourceId">Source ID of device.</param>
		/// <param name="PartitionId">Partition of device.</param>
		/// <param name="NodeId">Node ID of device.</param>
		/// <returns>Friendly name.</returns>
		public static async Task<string> GetFriendlyName(CaseInsensitiveString BareJid, string SourceId, string PartitionId, string NodeId)
		{
			if (string.IsNullOrEmpty(NodeId) && string.IsNullOrEmpty(PartitionId) && string.IsNullOrEmpty(SourceId))
				return await GetFriendlyName(BareJid);

			ContactInfo Thing = await FindByBareJid(BareJid, SourceId, PartitionId, NodeId);

			if (Thing is not null)
				return Thing.FriendlyName;

			string s = NodeId;

			if (!string.IsNullOrEmpty(PartitionId))
				if (string.IsNullOrEmpty(s))
					s = PartitionId;
				else
					s = ServiceRef.Localizer[nameof(AppResources.XInY), s, PartitionId];

			if (!string.IsNullOrEmpty(SourceId))
				if (string.IsNullOrEmpty(s))
					s = SourceId;
				else
					s = ServiceRef.Localizer[nameof(AppResources.XInY), s, SourceId];

			return ServiceRef.Localizer[nameof(AppResources.XOnY), s, BareJid];
		}

		/// <summary>
		/// Access to meta-data properties.
		/// </summary>
		/// <param name="PropertyName">Name of property</param>
		/// <returns>Property value.</returns>
		public string this[string PropertyName]
		{
			get
			{
				if (this.metaData is not null)
					foreach (Property P in this.metaData)
						if (string.Equals(P.Name, PropertyName, StringComparison.OrdinalIgnoreCase))
							return P.Value;

				return string.Empty;
			}
		}

		/// <summary>
		/// Category key used for thing notifications.
		/// </summary>
		public string ThingNotificationCategoryKey
		{
			get
			{
				return GetThingNotificationCategoryKey(this.bareJid, this.nodeId, this.sourceId, this.partition);
			}
		}

		/// <summary>
		/// Category key used for thing notifications.
		/// </summary>
		/// <param name="BareJid">Bare JID of device</param>
		/// <param name="NodeId">Node ID of device.</param>
		/// <param name="SourceId">Source ID of device.</param>
		/// <param name="Partition">Partition of device.</param>
		/// <returns>Key</returns>
		public static string GetThingNotificationCategoryKey(string BareJid, string? NodeId, string? SourceId, string? Partition)
		{
			StringBuilder sb = new();

			sb.Append(BareJid);
			sb.Append('|');

			if (SourceId is not null)
				sb.Append(SourceId);

			sb.Append('|');

			if (Partition is not null)
				sb.Append(Partition);

			sb.Append('|');

			if (NodeId is not null)
				sb.Append(NodeId);

			return sb.ToString();
		}
	}
}
