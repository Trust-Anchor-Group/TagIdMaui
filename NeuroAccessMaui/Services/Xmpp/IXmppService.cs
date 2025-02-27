using EDaler;
using EDaler.Events;
using EDaler.Uris;
using NeuroAccessMaui.Services.Push;
using NeuroAccessMaui.Services.Wallet;
using NeuroAccessMaui.UI.Pages.Registration;
using NeuroFeatures;
using NeuroFeatures.EventArguments;
using NeuroFeatures.Events;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Xml;
using Waher.Content;
using Waher.Events;
using Waher.Networking;
using Waher.Networking.XMPP;
using Waher.Networking.XMPP.Contracts;
using Waher.Networking.XMPP.Contracts.EventArguments;
using Waher.Networking.XMPP.DataForms;
using Waher.Networking.XMPP.Events;
using Waher.Networking.XMPP.HttpFileUpload;
using Waher.Networking.XMPP.PEP.Events;
using Waher.Networking.XMPP.Provisioning;
using Waher.Networking.XMPP.Provisioning.Events;
using Waher.Networking.XMPP.Provisioning.SearchOperators;
using Waher.Networking.XMPP.Push;
using Waher.Networking.XMPP.Sensor;
using Waher.Networking.XMPP.ServiceDiscovery;
using Waher.Persistence;
using Waher.Runtime.Inventory;
using Waher.Runtime.Temporary;
using Waher.Things;
using Waher.Things.SensorData;

namespace NeuroAccessMaui.Services.Xmpp
{
	/// <summary>
	/// Represents an abstraction of a connection to an XMPP Server.
	/// </summary>
	[DefaultImplementation(typeof(XmppService))]
	public interface IXmppService : ILoadableService
	{
		#region Lifecycle

		/// <summary>
		/// Can be used to <c>await</c> the server's connection state, i.e. skipping all intermediate states but <see cref="XmppState.Connected"/>.
		/// </summary>
		/// <param name="timeout">Maximum timeout before giving up.</param>
		/// <returns>If connected</returns>
		Task<bool> WaitForConnectedState(TimeSpan timeout);

		/// <summary>
		/// Perform a shutdown in critical situations. Attempts to shut down XMPP connection as fast as possible.
		/// </summary>
		Task UnloadFast();

		/// <summary>
		/// An event that triggers whenever the connection state to the XMPP server changes.
		/// </summary>
		event EventHandlerAsync<XmppState> ConnectionStateChanged;

		#endregion

		#region State

		/// <summary>
		/// Determines whether the connection to the XMPP server is live or not.
		/// </summary>
		bool IsOnline { get; }

		/// <summary>
		/// The current state of the connection to the XMPP server.
		/// </summary>
		XmppState State { get; }

		/// <summary>
		/// The Bare Jid of the current connection, or <c>null</c>.
		/// </summary>
		string BareJid { get; }

		/// <summary>
		/// The latest generic xmpp error, if any.
		/// </summary>
		string? LatestError { get; }

		/// <summary>
		/// The latest generic xmpp connection error, if any.
		/// </summary>
		string? LatestConnectionError { get; }

		#endregion

		#region Connections

		/// <summary>
		/// To be used during the very first phase of the startup/registration procedure. Tries to connect (and then disconnect) to the specified server.
		/// </summary>
		/// <param name="domain">The server's domain name.</param>
		/// <param name="isIpAddress">If the domain is provided as an IP address.</param>
		/// <param name="hostName">The server's host name.</param>
		/// <param name="portNumber">The xmpp port.</param>
		/// <param name="languageCode">Language code to use for communication.</param>
		/// <param name="appAssembly">The current app's main assembly.</param>
		/// <param name="connectedFunc">A callback to use if and when connected.</param>
		/// <returns>If connected. If not, any error message.</returns>
		Task<(bool Succeeded, string? ErrorMessage, string[]? Alternatives)> TryConnect(string domain, bool isIpAddress, string hostName, int portNumber,
				string languageCode, Assembly appAssembly, Func<XmppClient, Task> connectedFunc);

		/// <summary>
		/// To be used during the second phase of the startup/registration procedure. Tries to connect (and then disconnect) to the specified server, while also creating an account.
		/// </summary>
		/// <param name="domain">The server's domain name.</param>
		/// <param name="isIpAddress">If the domain is provided as an IP address.</param>
		/// <param name="hostName">The server's host name.</param>
		/// <param name="portNumber">The xmpp port.</param>
		/// <param name="userName">The user name of the account to create.</param>
		/// <param name="password">The password to use.</param>
		/// <param name="languageCode">Language code to use for communication.</param>
		/// <param name="ApiKey">API Key used when creating account.</param>
		/// <param name="ApiSecret">API Secret used when creating account.</param>
		/// <param name="appAssembly">The current app's main assembly.</param>
		/// <param name="connectedFunc">A callback to use if and when connected.</param>
		/// <returns>If connected. If not, any error message.</returns>
		Task<(bool Succeeded, string? ErrorMessage, string[]? Alternatives)> TryConnectAndCreateAccount(string domain, bool isIpAddress, string hostName,
			int portNumber, string userName, string password, string languageCode, string ApiKey, string ApiSecret,
			Assembly appAssembly, Func<XmppClient, Task> connectedFunc);

		/// <summary>
		/// To be used during the second phase of the startup/registration procedure. Tries to connect (and then disconnect) to the specified server, while also connecting to an existing account.
		/// </summary>
		/// <param name="domain">The server's domain name.</param>
		/// <param name="isIpAddress">If the domain is provided as an IP address.</param>
		/// <param name="hostName">The server's host name.</param>
		/// <param name="portNumber">The xmpp port.</param>
		/// <param name="userName">The user name of the account to create.</param>
		/// <param name="password">The password to use.</param>
		/// <param name="passwordMethod">The password hash method to use. Empty string signifies an unhashed password.</param>
		/// <param name="languageCode">Language code to use for communication.</param>
		/// <param name="appAssembly">The current app's main assembly.</param>
		/// <param name="connectedFunc">A callback to use if and when connected.</param>
		/// <returns>If connected. If not, any error message.</returns>
		Task<(bool Succeeded, string? ErrorMessage, string[]? Alternatives)> TryConnectAndConnectToAccount(string domain, bool isIpAddress, string hostName,
			int portNumber, string userName, string password, string passwordMethod, string languageCode, Assembly appAssembly,
			Func<XmppClient, Task> connectedFunc);

		#endregion

		#region Password

		/// <summary>
		/// Changes the password of the account.
		/// </summary>
		/// <param name="NewPassword">New password</param>
		/// <returns>If change was successful.</returns>
		Task<bool> ChangePassword(string NewPassword);

		#endregion

		#region Components & Services

		/// <summary>
		/// Performs a Service Discovery on a remote entity.
		/// </summary>
		/// <param name="FullJid">Full JID of entity.</param>
		/// <returns>Service Discovery response.</returns>
		Task<ServiceDiscoveryEventArgs> SendServiceDiscoveryRequest(string FullJid);

		/// <summary>
		/// Run this method to discover services for any given XMPP server.
		/// </summary>
		/// <param name="Client">The client to use. Can be <c>null</c>, in which case the default is used.</param>
		/// <returns>If TAG services were found.</returns>
		Task<bool> DiscoverServices(XmppClient? Client = null);

		#endregion

		#region Transfer

		/// <summary>
		/// Registers a Transfer ID Code
		/// </summary>
		/// <param name="Code">Transfer Code</param>
		Task AddTransferCode(string Code);

		#endregion

		#region IQ Stanzas (Information Query)

		/// <summary>
		/// Performs an asynchronous IQ Set request/response operation.
		/// </summary>
		/// <param name="To">Destination address</param>
		/// <param name="Xml">XML to embed into the request.</param>
		/// <returns>Response XML element.</returns>
		/// <exception cref="TimeoutException">If a timeout occurred.</exception>
		/// <exception cref="XmppException">If an IQ error is returned.</exception>
		Task<XmlElement> IqSetAsync(string To, string Xml);

		#endregion

		#region Messages

		/// <summary>
		/// Sends a message
		/// </summary>
		/// <param name="QoS">Quality of Service level of message.</param>
		/// <param name="Type">Type of message to send.</param>
		/// <param name="Id">Message ID</param>
		/// <param name="To">Destination address</param>
		/// <param name="CustomXml">Custom XML</param>
		/// <param name="Body">Body text of chat message.</param>
		/// <param name="Subject">Subject</param>
		/// <param name="Language">Language used.</param>
		/// <param name="ThreadId">Thread ID</param>
		/// <param name="ParentThreadId">Parent Thread ID</param>
		/// <param name="DeliveryCallback">Callback to call when message has been sent, or failed to be sent.</param>
		/// <param name="State">State object to pass on to the callback method.</param>
		public void SendMessage(QoSLevel QoS, MessageType Type, string Id, string To, string CustomXml,
			string Body, string Subject, string Language, string ThreadId, string ParentThreadId,
			EventHandlerAsync<DeliveryEventArgs>? DeliveryCallback, object? State);

		#endregion

		#region Presence

		/// <summary>
		/// Event raised when a new presence stanza has been received.
		/// </summary>
		event EventHandlerAsync<PresenceEventArgs> OnPresence;

		/// <summary>
		/// Requests subscription of presence information from a contact.
		/// </summary>
		/// <param name="BareJid">Bare JID of contact.</param>
		void RequestPresenceSubscription(string BareJid);

		/// <summary>
		/// Requests subscription of presence information from a contact.
		/// </summary>
		/// <param name="BareJid">Bare JID of contact.</param>
		/// <param name="CustomXml">Custom XML to include in the subscription request.</param>
		void RequestPresenceSubscription(string BareJid, string CustomXml);

		/// <summary>
		/// Requests unssubscription of presence information from a contact.
		/// </summary>
		/// <param name="BareJid">Bare JID of contact.</param>
		void RequestPresenceUnsubscription(string BareJid);

		/// <summary>
		/// Requests a previous presence subscription request revoked.
		/// </summary>
		/// <param name="BareJid">Bare JID of contact.</param>
		void RequestRevokePresenceSubscription(string BareJid);

		#endregion

		#region Roster

		/// <summary>
		/// Items in the roster.
		/// </summary>
		RosterItem[] Roster { get; }

		/// <summary>
		/// Gets a roster item.
		/// </summary>
		/// <param name="BareJid">Bare JID of roster item.</param>
		/// <returns>Roster item, if found, or null, if not available.</returns>
		RosterItem? GetRosterItem(string BareJid);

		/// <summary>
		/// Adds an item to the roster. If an item with the same Bare JID is found in the roster, that item is updated.
		/// </summary>
		/// <param name="Item">Item to add.</param>
		void AddRosterItem(RosterItem Item);

		/// <summary>
		/// Removes an item from the roster.
		/// </summary>
		/// <param name="BareJid">Bare JID of the roster item.</param>
		void RemoveRosterItem(string BareJid);

		/// <summary>
		/// Event raised when a roster item has been added to the roster.
		/// </summary>
		event EventHandlerAsync<RosterItem> OnRosterItemAdded;

		/// <summary>
		/// Event raised when a roster item has been updated in the roster.
		/// </summary>
		event EventHandlerAsync<RosterItem> OnRosterItemUpdated;

		/// <summary>
		/// Event raised when a roster item has been removed from the roster.
		/// </summary>
		event EventHandlerAsync<RosterItem> OnRosterItemRemoved;

		#endregion

		#region Push Notification

		/// <summary>
		/// If push notification is supported.
		/// </summary>
		bool SupportsPushNotification { get; }

		/// <summary>
		/// Registers a new token.
		/// </summary>
		/// <param name="TokenInformation">Token Information</param>
		/// <returns>If token could be registered.</returns>
		Task<bool> NewPushNotificationToken(TokenInformation TokenInformation);

		/// <summary>
		/// Reports a new push-notification token to the broker.
		/// </summary>
		/// <param name="Token">Token</param>
		/// <param name="Service">Service used</param>
		/// <param name="ClientType">Client type.</param>
		Task ReportNewPushNotificationToken(string Token, PushMessagingService Service, ClientType ClientType);

		/// <summary>
		/// Clears configured push notification rules in the broker.
		/// </summary>
		Task ClearPushNotificationRules();

		/// <summary>
		/// Adds a push-notification rule in the broker.
		/// </summary>
		/// <param name="MessageType">Type of message</param>
		/// <param name="LocalName">Local name of content element</param>
		/// <param name="Namespace">Namespace of content element</param>
		/// <param name="Channel">Push-notification channel</param>
		/// <param name="MessageVariable">Variable to receive message stanza</param>
		/// <param name="PatternMatchingScript">Pattern matching script</param>
		/// <param name="ContentScript">Content script</param>
		Task AddPushNotificationRule(MessageType MessageType, string LocalName, string Namespace, string Channel,
			string MessageVariable, string PatternMatchingScript, string ContentScript);

		#endregion

		#region Tokens

		/// <summary>
		/// Gets a token for use with APIs that are either distributed or use different
		/// protocols, when the client needs to authenticate itself using the current
		/// XMPP connection.
		/// </summary>
		/// <param name="Seconds">Number of seconds for which the token should be valid.</param>
		/// <returns>Token, if able to get a token, or null otherwise.</returns>
		Task<string?> GetApiToken(int Seconds);

		/// <summary>
		/// Performs an HTTP POST to a protected API on the server, over the current XMPP connection,
		/// authenticating the client using the credentials already provided over XMPP.
		/// </summary>
		/// <param name="LocalResource">Local Resource on the server to POST to.</param>
		/// <param name="Data">Data to post. This will be encoded using encoders in the type inventory.</param>
		/// <param name="Headers">Headers to provide in the POST.</param>
		/// <returns>Decoded response from the resource.</returns>
		/// <exception cref="Exception">Any communication error will be handle by raising the corresponding exception.</exception>
		Task<object> PostToProtectedApi(string LocalResource, object Data, params KeyValuePair<string, string>[] Headers);

		#endregion

		#region HTTP File Upload

		/// <summary>
		/// Returns <c>true</c> if file upload is supported, <c>false</c> otherwise.
		/// </summary>
		bool FileUploadIsSupported { get; }

		/// <summary>
		/// Uploads a file to the upload component.
		/// </summary>
		/// <param name="FileName">Name of file.</param>
		/// <param name="ContentType">Internet content type.</param>
		/// <param name="ContentSize">Size of content.</param>
		Task<HttpFileUploadEventArgs> RequestUploadSlotAsync(string FileName, string ContentType, long ContentSize);

		#endregion

		#region Personal Eventing Protocol (PEP)

		/// <summary>
		/// Registers an event handler of a specific type of personal events.
		/// </summary>
		/// <param name="PersonalEventType">Type of personal event.</param>
		/// <param name="Handler">Event handler.</param>
		void RegisterPepHandler(Type PersonalEventType, EventHandlerAsync<PersonalEventNotificationEventArgs> Handler);

		/// <summary>
		/// Unregisters an event handler of a specific type of personal events.
		/// </summary>
		/// <param name="PersonalEventType">Type of personal event.</param>
		/// <param name="Handler">Event handler.</param>
		/// <returns>If the event handler was found and removed.</returns>
		bool UnregisterPepHandler(Type PersonalEventType, EventHandlerAsync<PersonalEventNotificationEventArgs> Handler);

		#endregion

		#region Thing Registries & Discovery

		/// <summary>
		/// JID of thing registry service.
		/// </summary>
		string RegistryServiceJid { get; }

		/// <summary>
		/// Checks if a URI is a claim URI.
		/// </summary>
		/// <param name="DiscoUri">IoTDisco URI</param>
		/// <returns>If <paramref name="DiscoUri"/> is a claim URI.</returns>
		bool IsIoTDiscoClaimURI(string DiscoUri);

		/// <summary>
		/// Checks if a URI is a search URI.
		/// </summary>
		/// <param name="DiscoUri">IoTDisco URI</param>
		/// <returns>If <paramref name="DiscoUri"/> is a search URI.</returns>
		bool IsIoTDiscoSearchURI(string DiscoUri);

		/// <summary>
		/// Checks if a URI is a direct reference URI.
		/// </summary>
		/// <param name="DiscoUri">IoTDisco URI</param>
		/// <returns>If <paramref name="DiscoUri"/> is a direct reference URI.</returns>
		bool IsIoTDiscoDirectURI(string DiscoUri);

		/// <summary>
		/// Tries to decode an IoTDisco Claim URI (subset of all possible IoTDisco URIs).
		/// </summary>
		/// <param name="DiscoUri">IoTDisco URI</param>
		/// <param name="Tags">Decoded meta data tags.</param>
		/// <returns>If DiscoUri was successfully decoded.</returns>
		bool TryDecodeIoTDiscoClaimURI(string DiscoUri, [NotNullWhen(true)] out MetaDataTag[]? Tags);

		/// <summary>
		/// Tries to decode an IoTDisco Search URI (subset of all possible IoTDisco URIs).
		/// </summary>
		/// <param name="DiscoUri">IoTDisco URI</param>
		/// <param name="Operators">Search operators.</param>
		/// <param name="RegistryJid">Registry Service JID</param>
		/// <returns>If the URI could be parsed.</returns>
		bool TryDecodeIoTDiscoSearchURI(string DiscoUri, [NotNullWhen(true)] out SearchOperator[]? Operators, out string? RegistryJid);

		/// <summary>
		/// Tries to decode an IoTDisco Direct Reference URI (subset of all possible IoTDisco URIs).
		/// </summary>
		/// <param name="DiscoUri">IoTDisco URI</param>
		/// <param name="Jid">JID of device</param>
		/// <param name="SourceId">Optional Source ID of device, or null if none.</param>
		/// <param name="NodeId">Optional Node ID of device, or null if none.</param>
		/// <param name="PartitionId">Optional Partition ID of device, or null if none.</param>
		/// <param name="Tags">Decoded meta data tags.</param>
		/// <returns>If the URI could be parsed.</returns>
		bool TryDecodeIoTDiscoDirectURI(string DiscoUri, [NotNullWhen(true)] out string? Jid, out string? SourceId, out string? NodeId,
			out string? PartitionId, [NotNullWhen(true)] out MetaDataTag[]? Tags);

		/// <summary>
		/// Claims a think in accordance with parameters defined in a iotdisco claim URI.
		/// </summary>
		/// <param name="DiscoUri">IoTDisco URI</param>
		/// <param name="MakePublic">If the device should be public in the thing registry.</param>
		/// <returns>Information about the thing, or error if unable.</returns>
		Task<NodeResultEventArgs> ClaimThing(string DiscoUri, bool MakePublic);

		/// <summary>
		/// Disowns a thing
		/// </summary>
		/// <param name="RegistryJid">Registry JID</param>
		/// <param name="ThingJid">Thing JID</param>
		/// <param name="SourceId">Source ID</param>
		/// <param name="Partition">Partition</param>
		/// <param name="NodeId">Node ID</param>
		/// <returns>If the thing was disowned</returns>
		Task<bool> Disown(string RegistryJid, string ThingJid, string SourceId, string Partition, string NodeId);

		/// <summary>
		/// Searches for devices in accordance with settings in a iotdisco-URI.
		/// </summary>
		/// <param name="Offset">Start offset of list</param>
		/// <param name="MaxCount">Maximum number of items in response.</param>
		/// <param name="DiscoUri">iotdisco URI.</param>
		/// <returns>Devices found, Registry JID, and if more devices are available.</returns>
		Task<(SearchResultThing[], string?, bool)> Search(int Offset, int MaxCount, string DiscoUri);

		/// <summary>
		/// Searches for devices in accordance with settings in a iotdisco-URI.
		/// </summary>
		/// <param name="Offset">Start offset of list</param>
		/// <param name="MaxCount">Maximum number of items in response.</param>
		/// <param name="RegistryJid">Registry Service JID</param>
		/// <param name="Operators">Search operators.</param>
		/// <returns>Devices found, and if more devices are available.</returns>
		Task<(SearchResultThing[], bool)> Search(int Offset, int MaxCount, string? RegistryJid, params SearchOperator[] Operators);

		/// <summary>
		/// Searches for all devices in accordance with settings in a iotdisco-URI.
		/// </summary>
		/// <param name="DiscoUri">iotdisco URI.</param>
		/// <returns>Complete list of devices in registry matching the search operators, and the JID of the registry service.</returns>
		Task<(SearchResultThing[], string?)> SearchAll(string DiscoUri);

		/// <summary>
		/// Searches for all devices in accordance with settings in a iotdisco-URI.
		/// </summary>
		/// <param name="RegistryJid">Registry Service JID</param>
		/// <param name="Operators">Search operators.</param>
		/// <returns>Complete list of devices in registry matching the search operators.</returns>
		Task<SearchResultThing[]> SearchAll(string RegistryJid, params SearchOperator[] Operators);

		#endregion

		#region Legal Identities

		/// <summary>
		/// Generates new keys
		/// </summary>
		Task GenerateNewKeys();

		/// <summary>
		/// Gets important attributes for a successful ID Application.
		/// </summary>
		/// <returns>ID Application attributes.</returns>
		Task<IdApplicationAttributesEventArgs> GetIdApplicationAttributes();

		/// <summary>
		/// Adds a legal identity.
		/// </summary>
		/// <param name="Model">The model holding all the values needed.</param>
		/// <param name="GenerateNewKeys">If new keys should be generated.</param>
		/// <param name="Attachments">The physical attachments to upload.</param>
		/// <returns>Legal Identity</returns>
		Task<LegalIdentity> AddLegalIdentity(RegisterIdentityModel Model, bool GenerateNewKeys, params LegalIdentityAttachment[] Attachments);

		/// <summary>
		/// Returns a list of legal identities.
		/// </summary>
		/// <param name="client">The Xmpp client instance. Can be null, in that case the default one is used.</param>
		/// <returns>Legal Identities</returns>
		Task<LegalIdentity[]> GetLegalIdentities(XmppClient? client = null);

		/// <summary>
		/// Gets a specific legal identity.
		/// </summary>
		/// <param name="legalIdentityId">The id of the legal identity to retrieve.</param>
		/// <returns>Legal identity object</returns>
		Task<LegalIdentity> GetLegalIdentity(CaseInsensitiveString legalIdentityId);

		/// <summary>
		/// Checks if a legal identity is in the contacts list.
		/// </summary>
		/// <param name="legalIdentityId">The id of the legal identity to retrieve.</param>
		/// <returns>If the legal identity is in the contacts list.</returns>
		Task<bool> IsContact(CaseInsensitiveString legalIdentityId);

		/// <summary>
		/// Checks if the client has access to the private keys of the specified legal identity.
		/// </summary>
		/// <param name="legalIdentityId">The id of the legal identity.</param>
		/// <returns>If private keys are available.</returns>
		Task<bool> HasPrivateKey(CaseInsensitiveString legalIdentityId);

		/// <summary>
		/// Marks the legal identity as obsolete.
		/// </summary>
		/// <param name="legalIdentityId">The id to mark as obsolete.</param>
		/// <returns>Legal Identity</returns>
		Task<LegalIdentity> ObsoleteLegalIdentity(CaseInsensitiveString legalIdentityId);

		/// <summary>
		/// Marks the legal identity as compromised.
		/// </summary>
		/// <param name="legalIdentityId">The legal id to mark as compromised.</param>
		/// <returns>Legal Identity</returns>
		Task<LegalIdentity> CompromiseLegalIdentity(CaseInsensitiveString legalIdentityId);

		/// <summary>
		/// Petitions a legal identity.
		/// </summary>
		/// <param name="LegalId">The id of the legal identity.</param>
		/// <param name="PetitionId">The petition id.</param>
		/// <param name="Purpose">The purpose of the petitioning.</param>
		Task PetitionIdentity(CaseInsensitiveString LegalId, string PetitionId, string Purpose);

		/// <summary>
		/// Sends a response to a petitioning identity request.
		/// </summary>
		/// <param name="LegalId">The id of the legal identity.</param>
		/// <param name="PetitionId">The petition id.</param>
		/// <param name="RequestorFullJid">The full Jid of the requestor.</param>
		/// <param name="Response">If the petition is accepted (true) or rejected (false).</param>
		Task SendPetitionIdentityResponse(CaseInsensitiveString LegalId, string PetitionId, string RequestorFullJid, bool Response);

		/// <summary>
		/// An event that fires when a legal identity changes.
		/// </summary>
		event EventHandlerAsync<LegalIdentityEventArgs> LegalIdentityChanged;

		/// <summary>
		/// An event that fires when an ID Application has changed.
		/// </summary>
		event EventHandlerAsync<LegalIdentityEventArgs> IdentityApplicationChanged;

		/// <summary>
		/// An event that fires when a petition for an identity is received.
		/// </summary>
		event EventHandlerAsync<LegalIdentityPetitionEventArgs> PetitionForIdentityReceived;

		/// <summary>
		/// An event that fires when a petitioned identity response is received.
		/// </summary>
		event EventHandlerAsync<LegalIdentityPetitionResponseEventArgs> PetitionedIdentityResponseReceived;

		/// <summary>
		/// Exports Keys to XML.
		/// </summary>
		/// <param name="Output">XML output.</param>
		Task ExportSigningKeys(XmlWriter Output);

		/// <summary>
		/// Imports keys
		/// </summary>
		/// <param name="Xml">XML Definition of keys.</param>
		/// <returns>If keys could be loaded into the client.</returns>
		Task<bool> ImportSigningKeys(XmlElement Xml);

		/// <summary>
		/// Validates a legal identity.
		/// </summary>
		/// <param name="Identity">Legal Identity</param>
		/// <returns>The validity of the identity.</returns>
		Task<IdentityStatus> ValidateIdentity(LegalIdentity Identity);

		#endregion

		#region Smart Contracts

		/// <summary>
		/// Refrence to the underlying contracts client.
		/// </summary>
		ContractsClient ContractsClient { get; }

		/// <summary>
		/// Gets the contract with the specified id.
		/// </summary>
		/// <param name="ContractId">The contract id.</param>
		/// <returns>Smart Contract</returns>
		Task<Contract> GetContract(CaseInsensitiveString ContractId);

		/// <summary>
		/// Gets references to created contracts.
		/// </summary>
		/// <returns>Created contracts.</returns>
		Task<string[]> GetCreatedContractReferences();

		/// <summary>
		/// Gets references to signed contracts.
		/// </summary>
		/// <returns>Signed contracts.</returns>
		Task<string[]> GetSignedContractReferences();

		/// <summary>
		/// Signs a given contract.
		/// </summary>
		/// <param name="Contract">The contract to sign.</param>
		/// <param name="Role">The role of the signer.</param>
		/// <param name="Transferable">Whether the contract is transferable or not.</param>
		/// <returns>Smart Contract</returns>
		Task<Contract> SignContract(Contract Contract, string Role, bool Transferable);

		/// <summary>
		/// Obsoletes a contract.
		/// </summary>
		/// <param name="ContractId">The id of the contract to obsolete.</param>
		/// <returns>Smart Contract</returns>
		Task<Contract> ObsoleteContract(CaseInsensitiveString ContractId);

		/// <summary>
		/// Creates a new contract.
		/// </summary>
		/// <param name="TemplateId">The id of the contract template to use.</param>
		/// <param name="Parts">The individual contract parts.</param>
		/// <param name="Parameters">Contract parameters.</param>
		/// <param name="Visibility">The contract's visibility.</param>
		/// <param name="PartsMode">The contract's parts.</param>
		/// <param name="Duration">Duration of the contract.</param>
		/// <param name="ArchiveRequired">Required duration for contract archival.</param>
		/// <param name="ArchiveOptional">Optional duration for contract archival.</param>
		/// <param name="SignAfter">Timestamp of when the contract can be signed at the earliest.</param>
		/// <param name="SignBefore">Timestamp of when the contract can be signed at the latest.</param>
		/// <param name="CanActAsTemplate">Can this contract act as a template itself?</param>
		/// <returns>Smart Contract</returns>
		Task<Contract> CreateContract(
			CaseInsensitiveString TemplateId,
			Part[] Parts,
			Parameter[] Parameters,
			ContractVisibility Visibility,
			ContractParts PartsMode,
			Duration Duration,
			Duration ArchiveRequired,
			Duration ArchiveOptional,
			DateTime? SignAfter,
			DateTime? SignBefore,
			bool CanActAsTemplate);

		/// <summary>
		/// Deletes a contract.
		/// </summary>
		/// <param name="ContractId">The id of the contract to delete.</param>
		/// <returns>Smart Contract</returns>
		Task<Contract> DeleteContract(CaseInsensitiveString ContractId);

		/// <summary>
		/// Petitions a contract with the specified id and purpose.
		/// </summary>
		/// <param name="ContractId">The contract id.</param>
		/// <param name="PetitionId">The petition id.</param>
		/// <param name="Purpose">The purpose.</param>
		Task PetitionContract(CaseInsensitiveString ContractId, string PetitionId, string Purpose);

		/// <summary>
		/// Sends a response to a petitioning contract request.
		/// </summary>
		/// <param name="ContractId">The id of the contract.</param>
		/// <param name="PetitionId">The petition id.</param>
		/// <param name="RequestorFullJid">The full Jid of the requestor.</param>
		/// <param name="Response">If the petition is accepted (true) or rejected (false).</param>
		Task SendPetitionContractResponse(CaseInsensitiveString ContractId, string PetitionId, string RequestorFullJid, bool Response);

		/// <summary>
		/// Event raised when a contract proposal has been received.
		/// </summary>
		event EventHandlerAsync<ContractProposalEventArgs> ContractProposalReceived;

		/// <summary>
		/// Event raised when contract was updated.
		/// </summary>
		event EventHandlerAsync<ContractReferenceEventArgs> ContractUpdated;

		/// <summary>
		/// Event raised when contract was signed.
		/// </summary>
		event EventHandlerAsync<ContractSignedEventArgs> ContractSigned;

		/// <summary>
		/// An event that fires when a petition for a contract is received.
		/// </summary>
		event EventHandlerAsync<ContractPetitionEventArgs> PetitionForContractReceived;

		/// <summary>
		/// An event that fires when a petitioned contract response is received.
		/// </summary>
		event EventHandlerAsync<ContractPetitionResponseEventArgs> PetitionedContractResponseReceived;

		/// <summary>
		/// Gets the timestamp of the last event received for a given contract ID.
		/// </summary>
		/// <param name="ContractId">Contract ID</param>
		/// <returns>Timestamp</returns>
		DateTime GetTimeOfLastContractEvent(CaseInsensitiveString ContractId);

		/// <summary>
		/// Sends a contract proposal to a recipient.
		/// </summary>
		/// <param name="Contract">Proposed contract.</param>
		/// <param name="Role">Proposed role of recipient.</param>
		/// <param name="To">Recipient Address (Bare or Full JID).</param>
		/// <param name="Message">Optional message included in message.</param>
		Task SendContractProposal(Contract Contract, string Role, string To, string Message);

		#endregion

		#region Attachments

		/// <summary>
		/// Gets an attachment for a contract.
		/// </summary>
		/// <param name="Url">The url of the attachment.</param>
		/// <param name="Timeout">Max timeout allowed when retrieving an attachment.</param>
		/// <param name="SignWith">How the request is signed. For identity attachments, especially for attachments to an identity being created, <see cref="SignWith.CurrentKeys"/> should be used. For requesting attachments relating to a contract, <see cref="SignWith.LatestApprovedId"/> should be used.</param>
		/// <returns>Content-Type, and attachment file.</returns>
		Task<KeyValuePair<string, TemporaryFile>> GetAttachment(string Url, SignWith SignWith, TimeSpan Timeout);

		#endregion

		#region Peer Review

		/// <summary>
		/// Sends a petition to a third-party to review a legal identity.
		/// </summary>
		/// <param name="LegalId">The legal id to petition.</param>
		/// <param name="Identity">The legal id to peer review.</param>
		/// <param name="PetitionId">The petition id.</param>
		/// <param name="Purpose">The purpose.</param>
		Task PetitionPeerReviewId(CaseInsensitiveString LegalId, LegalIdentity Identity, string PetitionId, string Purpose);

		/// <summary>
		/// Adds an attachment for the peer review.
		/// </summary>
		/// <param name="Identity">The identity to which the attachment should be added.</param>
		/// <param name="ReviewerLegalIdentity">The identity of the reviewer.</param>
		/// <param name="PeerSignature">The raw signature data.</param>
		/// <returns>Legal Identity</returns>
		Task<LegalIdentity> AddPeerReviewIdAttachment(LegalIdentity Identity, LegalIdentity ReviewerLegalIdentity, byte[] PeerSignature);

		/// <summary>
		/// An event that fires when a petition for peer review is received.
		/// </summary>
		event EventHandlerAsync<SignaturePetitionEventArgs> PetitionForPeerReviewIdReceived;

		/// <summary>
		/// An event that fires when a petitioned peer review response is received.
		/// </summary>
		event EventHandlerAsync<SignaturePetitionResponseEventArgs> PetitionedPeerReviewIdResponseReceived;

		/// <summary>
		/// Gets available service providers for buying eDaler.
		/// </summary>
		/// <returns>Available service providers for peer review of identity applications.</returns>
		Task<ServiceProviderWithLegalId[]> GetServiceProvidersForPeerReviewAsync();

		/// <summary>
		/// Selects a peer-review service as default, for the account, when sending a peer-review request to the
		/// Legal Identity of the Trust Provider hosting the account.
		/// </summary>
		/// <param name="ServiceId">Service ID</param>
		/// <param name="ServiceProvider">Service Provider</param>
		Task SelectPeerReviewService(string ServiceId, string ServiceProvider);

		#endregion

		#region Signatures

		/// <summary>
		/// Signs binary data with the corresponding private key.
		/// </summary>
		/// <param name="data">The data to sign.</param>
		/// <param name="signWith">What keys that can be used to sign the data.</param>
		/// <returns>Signature</returns>
		Task<byte[]> Sign(byte[] data, SignWith signWith);

		/// <summary>Validates a signature of binary data.</summary>
		/// <param name="legalIdentity">Legal identity used to create the signature.</param>
		/// <param name="data">Binary data to sign-</param>
		/// <param name="signature">Digital signature of data</param>
		/// <returns>
		/// true = Signature is valid.
		/// false = Signature is invalid.
		/// null = Client key algorithm is unknown, and veracity of signature could not be established.
		/// </returns>
		bool? ValidateSignature(LegalIdentity legalIdentity, byte[] data, byte[] signature);

		/// <summary>
		/// Sends a response to a petitioning signature request.
		/// </summary>
		/// <param name="LegalId">Legal Identity petitioned.</param>
		/// <param name="Content">Content to be signed.</param>
		/// <param name="Signature">Digital signature of content, made by the legal identity.</param>
		/// <param name="PetitionId">A petition identifier. This identifier will follow the petition, and can be used
		/// to identify the petition request.</param>
		/// <param name="RequestorFullJid">Full JID of requestor.</param>
		/// <param name="Response">If the petition is accepted (true) or rejected (false).</param>
		Task SendPetitionSignatureResponse(CaseInsensitiveString LegalId, byte[] Content, byte[] Signature, string PetitionId, string RequestorFullJid, bool Response);

		/// <summary>
		/// An event that fires when a petition for a signature is received.
		/// </summary>
		event EventHandlerAsync<SignaturePetitionEventArgs> PetitionForSignatureReceived;

		/// <summary>
		/// Event raised when a response to a signature petition has been received.
		/// </summary>
		event EventHandlerAsync<SignaturePetitionResponseEventArgs> SignaturePetitionResponseReceived;

		#endregion

		#region Provisioning

		/// <summary>
		/// JID of provisioning service.
		/// </summary>
		string ProvisioningServiceJid { get; }

		/// <summary>
		/// Sends a response to a previous "Is Friend" question.
		/// </summary>
		/// <param name="ProvisioningServiceJID">Provisioning service JID.</param>
		/// <param name="JID">JID of device asking the question.</param>
		/// <param name="RemoteJID">JID of caller.</param>
		/// <param name="Key">Key corresponding to request.</param>
		/// <param name="IsFriend">If the response is yes or no.</param>
		/// <param name="Range">The range of the response.</param>
		/// <param name="Callback">Optional callback method to call, when response to request has been received.</param>
		/// <param name="State">State object to pass on to callback method.</param>
		void IsFriendResponse(string ProvisioningServiceJID, string JID, string RemoteJID, string Key, bool IsFriend,
			RuleRange Range, EventHandlerAsync<IqResultEventArgs> Callback, object? State);

		/// <summary>
		/// Sends a response to a previous "Can Control" question, for all future requests.
		/// </summary>
		/// <param name="ProvisioningServiceJID">Provisioning service JID.</param>
		/// <param name="JID">JID of device asking the question.</param>
		/// <param name="RemoteJID">JID of caller.</param>
		/// <param name="Key">Key corresponding to request.</param>
		/// <param name="CanControl">If the caller is allowed to control the device.</param>
		/// <param name="ParameterNames">Parameter names allowed</param>
		/// <param name="Node">Optional node reference. Can be null or Waher.Things.ThingReference.Empty.</param>
		/// <param name="Callback">Optional callback method to call, when response to request has been received.</param>
		/// <param name="State">State object to pass on to callback method.</param>
		void CanControlResponseAll(string ProvisioningServiceJID, string JID, string RemoteJID, string Key, bool CanControl,
			string[]? ParameterNames, IThingReference Node, EventHandlerAsync<IqResultEventArgs> Callback, object? State);

		/// <summary>
		/// Sends a response to a previous "Can Control" question, based on the JID of the caller.
		/// </summary>
		/// <param name="ProvisioningServiceJID">Provisioning service JID.</param>
		/// <param name="JID">JID of device asking the question.</param>
		/// <param name="RemoteJID">JID of caller.</param>
		/// <param name="Key">Key corresponding to request.</param>
		/// <param name="CanControl">If the caller is allowed to control the device.</param>
		/// <param name="ParameterNames">Parameter names allowed</param>
		/// <param name="Node">Optional node reference. Can be null or Waher.Things.ThingReference.Empty.</param>
		/// <param name="Callback">Optional callback method to call, when response to request has been received.</param>
		/// <param name="State">State object to pass on to callback method.</param>
		void CanControlResponseCaller(string ProvisioningServiceJID, string JID, string RemoteJID, string Key,
			bool CanControl, string[]? ParameterNames, IThingReference Node, EventHandlerAsync<IqResultEventArgs> Callback, object? State);

		/// <summary>
		/// Sends a response to a previous "Can Control" question, based on the domain of the caller.
		/// </summary>
		/// <param name="ProvisioningServiceJID">Provisioning service JID.</param>
		/// <param name="JID">JID of device asking the question.</param>
		/// <param name="RemoteJID">JID of caller.</param>
		/// <param name="Key">Key corresponding to request.</param>
		/// <param name="CanControl">If the caller is allowed to control the device.</param>
		/// <param name="ParameterNames">Parameter names allowed</param>
		/// <param name="Node">Optional node reference. Can be null or Waher.Things.ThingReference.Empty.</param>
		/// <param name="Callback">Optional callback method to call, when response to request has been received.</param>
		/// <param name="State">State object to pass on to callback method.</param>
		void CanControlResponseDomain(string ProvisioningServiceJID, string JID, string RemoteJID, string Key,
			bool CanControl, string[]? ParameterNames, IThingReference Node, EventHandlerAsync<IqResultEventArgs> Callback, object? State);

		/// <summary>
		/// Sends a response to a previous "Can Control" question, based on a device token.
		/// </summary>
		/// <param name="ProvisioningServiceJID">Provisioning service JID.</param>
		/// <param name="JID">JID of device asking the question.</param>
		/// <param name="RemoteJID">JID of caller.</param>
		/// <param name="Key">Key corresponding to request.</param>
		/// <param name="CanControl">If the caller is allowed to control the device.</param>
		/// <param name="ParameterNames">Parameter names allowed</param>
		/// <param name="Token">Token.</param>
		/// <param name="Node">Optional node reference. Can be null or Waher.Things.ThingReference.Empty.</param>
		/// <param name="Callback">Optional callback method to call, when response to request has been received.</param>
		/// <param name="State">State object to pass on to callback method.</param>
		void CanControlResponseDevice(string ProvisioningServiceJID, string JID, string RemoteJID, string Key,
			bool CanControl, string[]? ParameterNames, string Token, IThingReference Node, EventHandlerAsync<IqResultEventArgs> Callback,
			object? State);

		/// <summary>
		/// Sends a response to a previous "Can Control" question, based on a service token.
		/// </summary>
		/// <param name="ProvisioningServiceJID">Provisioning service JID.</param>
		/// <param name="JID">JID of device asking the question.</param>
		/// <param name="RemoteJID">JID of caller.</param>
		/// <param name="Key">Key corresponding to request.</param>
		/// <param name="CanControl">If the caller is allowed to control the device.</param>
		/// <param name="ParameterNames">Parameter names allowed</param>
		/// <param name="Token">Token.</param>
		/// <param name="Node">Optional node reference. Can be null or Waher.Things.ThingReference.Empty.</param>
		/// <param name="Callback">Optional callback method to call, when response to request has been received.</param>
		/// <param name="State">State object to pass on to callback method.</param>
		void CanControlResponseService(string ProvisioningServiceJID, string JID, string RemoteJID, string Key,
			bool CanControl, string[]? ParameterNames, string Token, IThingReference Node, EventHandlerAsync<IqResultEventArgs> Callback,
			object? State);

		/// <summary>
		/// Sends a response to a previous "Can Control" question, based on a user token.
		/// </summary>
		/// <param name="ProvisioningServiceJID">Provisioning service JID.</param>
		/// <param name="JID">JID of device asking the question.</param>
		/// <param name="RemoteJID">JID of caller.</param>
		/// <param name="Key">Key corresponding to request.</param>
		/// <param name="CanControl">If the caller is allowed to control the device.</param>
		/// <param name="ParameterNames">Parameter names allowed</param>
		/// <param name="Token">Token.</param>
		/// <param name="Node">Optional node reference. Can be null or Waher.Things.ThingReference.Empty.</param>
		/// <param name="Callback">Optional callback method to call, when response to request has been received.</param>
		/// <param name="State">State object to pass on to callback method.</param>
		void CanControlResponseUser(string ProvisioningServiceJID, string JID, string RemoteJID, string Key,
			bool CanControl, string[]? ParameterNames, string Token, IThingReference Node, EventHandlerAsync<IqResultEventArgs> Callback,
			object? State);

		/// <summary>
		/// Sends a response to a previous "Can Read" question, for all future requests.
		/// </summary>
		/// <param name="ProvisioningServiceJID">Provisioning service JID.</param>
		/// <param name="JID">JID of device asking the question.</param>
		/// <param name="RemoteJID">JID of caller.</param>
		/// <param name="Key">Key corresponding to request.</param>
		/// <param name="CanRead">If the caller is allowed to control the device.</param>
		/// <param name="FieldTypes">Field types allowed.</param>
		/// <param name="FieldNames">Field names allowed</param>
		/// <param name="Node">Optional node reference. Can be null or Waher.Things.ThingReference.Empty.</param>
		/// <param name="Callback">Optional callback method to call, when response to request has been received.</param>
		/// <param name="State">State object to pass on to callback method.</param>
		void CanReadResponseAll(string ProvisioningServiceJID, string JID, string RemoteJID, string Key, bool CanRead,
			FieldType FieldTypes, string[]? FieldNames, IThingReference Node, EventHandlerAsync<IqResultEventArgs> Callback, object? State);

		/// <summary>
		/// Sends a response to a previous "Can Read" question, based on the JID of the caller.
		/// </summary>
		/// <param name="ProvisioningServiceJID">Provisioning service JID.</param>
		/// <param name="JID">JID of device asking the question.</param>
		/// <param name="RemoteJID">JID of caller.</param>
		/// <param name="Key">Key corresponding to request.</param>
		/// <param name="CanRead">If the caller is allowed to control the device.</param>
		/// <param name="FieldTypes">Field types allowed.</param>
		/// <param name="FieldNames">Field names allowed</param>
		/// <param name="Node">Optional node reference. Can be null or Waher.Things.ThingReference.Empty.</param>
		/// <param name="Callback">Optional callback method to call, when response to request has been received.</param>
		/// <param name="State">State object to pass on to callback method.</param>
		void CanReadResponseCaller(string ProvisioningServiceJID, string JID, string RemoteJID, string Key, bool CanRead,
			FieldType FieldTypes, string[]? FieldNames, IThingReference Node, EventHandlerAsync<IqResultEventArgs> Callback, object? State);

		/// <summary>
		/// Sends a response to a previous "Can Read" question, based on the domain of the caller.
		/// </summary>
		/// <param name="ProvisioningServiceJID">Provisioning service JID.</param>
		/// <param name="JID">JID of device asking the question.</param>
		/// <param name="RemoteJID">JID of caller.</param>
		/// <param name="Key">Key corresponding to request.</param>
		/// <param name="CanRead">If the caller is allowed to control the device.</param>
		/// <param name="FieldTypes">Field types allowed.</param>
		/// <param name="FieldNames">Field names allowed</param>
		/// <param name="Node">Optional node reference. Can be null or Waher.Things.ThingReference.Empty.</param>
		/// <param name="Callback">Optional callback method to call, when response to request has been received.</param>
		/// <param name="State">State object to pass on to callback method.</param>
		void CanReadResponseDomain(string ProvisioningServiceJID, string JID, string RemoteJID, string Key, bool CanRead,
			FieldType FieldTypes, string[]? FieldNames, IThingReference Node, EventHandlerAsync<IqResultEventArgs> Callback, object? State);

		/// <summary>
		/// Sends a response to a previous "Can Read" question, based on a device token.
		/// </summary>
		/// <param name="ProvisioningServiceJID">Provisioning service JID.</param>
		/// <param name="JID">JID of device asking the question.</param>
		/// <param name="RemoteJID">JID of caller.</param>
		/// <param name="Key">Key corresponding to request.</param>
		/// <param name="CanRead">If the caller is allowed to control the device.</param>
		/// <param name="FieldTypes">Field types allowed.</param>
		/// <param name="FieldNames">Field names allowed</param>
		/// <param name="Token">Token.</param>
		/// <param name="Node">Optional node reference. Can be null or Waher.Things.ThingReference.Empty.</param>
		/// <param name="Callback">Optional callback method to call, when response to request has been received.</param>
		/// <param name="State">State object to pass on to callback method.</param>
		void CanReadResponseDevice(string ProvisioningServiceJID, string JID, string RemoteJID, string Key,
			bool CanRead, FieldType FieldTypes, string[]? FieldNames, string Token, IThingReference Node, EventHandlerAsync<IqResultEventArgs> Callback,
			object? State);

		/// <summary>
		/// Sends a response to a previous "Can Read" question, based on a service token.
		/// </summary>
		/// <param name="ProvisioningServiceJID">Provisioning service JID.</param>
		/// <param name="JID">JID of device asking the question.</param>
		/// <param name="RemoteJID">JID of caller.</param>
		/// <param name="Key">Key corresponding to request.</param>
		/// <param name="CanRead">If the caller is allowed to control the device.</param>
		/// <param name="FieldTypes">Field types allowed.</param>
		/// <param name="FieldNames">Field names allowed</param>
		/// <param name="Token">Token.</param>
		/// <param name="Node">Optional node reference. Can be null or Waher.Things.ThingReference.Empty.</param>
		/// <param name="Callback">Optional callback method to call, when response to request has been received.</param>
		/// <param name="State">State object to pass on to callback method.</param>
		void CanReadResponseService(string ProvisioningServiceJID, string JID, string RemoteJID, string Key,
			bool CanRead, FieldType FieldTypes, string[]? FieldNames, string Token, IThingReference Node, EventHandlerAsync<IqResultEventArgs> Callback,
			object? State);

		/// <summary>
		/// Sends a response to a previous "Can Read" question, based on a user token.
		/// </summary>
		/// <param name="ProvisioningServiceJID">Provisioning service JID.</param>
		/// <param name="JID">JID of device asking the question.</param>
		/// <param name="RemoteJID">JID of caller.</param>
		/// <param name="Key">Key corresponding to request.</param>
		/// <param name="CanRead">If the caller is allowed to control the device.</param>
		/// <param name="FieldTypes">Field types allowed.</param>
		/// <param name="FieldNames">Field names allowed</param>
		/// <param name="Token">Token.</param>
		/// <param name="Node">Optional node reference. Can be null or Waher.Things.ThingReference.Empty.</param>
		/// <param name="Callback">Optional callback method to call, when response to request has been received.</param>
		/// <param name="State">State object to pass on to callback method.</param>
		void CanReadResponseUser(string ProvisioningServiceJID, string JID, string RemoteJID, string Key,
			bool CanRead, FieldType FieldTypes, string[]? FieldNames, string Token, IThingReference Node, EventHandlerAsync<IqResultEventArgs> Callback,
			object? State);

		/// <summary>
		/// Deletes the rules of a device.
		/// </summary>
		/// <param name="ServiceJID">JID of provisioning service.</param>
		/// <param name="DeviceJID">Bare JID of device whose rules are to be deleted. If null, all owned devices will get their rules deleted.</param>
		/// <param name="NodeId">Optional Node ID of device.</param>
		/// <param name="SourceId">Optional Source ID of device.</param>
		/// <param name="Partition">Optional Partition of device.</param>
		/// <param name="Callback">Method to call when response is returned.</param>
		/// <param name="State">State object to pass on to callback method.</param>
		void DeleteDeviceRules(string ServiceJID, string DeviceJID, string NodeId, string SourceId, string Partition,
			EventHandlerAsync<IqResultEventArgs> Callback, object? State);

		/// <summary>
		/// Gets the certificate the corresponds to a token. This certificate can be used
		/// to identify services, devices or users. Tokens are challenged to make sure they
		/// correspond to the holder of the private part of the corresponding certificate.
		/// </summary>
		/// <param name="Token">Token corresponding to the requested certificate.</param>
		/// <param name="Callback">Callback method called, when certificate is available.</param>
		/// <param name="State">State object that will be passed on to the callback method.</param>
		void GetCertificate(string Token, EventHandlerAsync<CertificateEventArgs> Callback, object? State);

		#endregion

		#region IoT

		/// <summary>
		/// Gets a (partial) list of my devices.
		/// </summary>
		/// <param name="Offset">Start offset of list</param>
		/// <param name="MaxCount">Maximum number of items in response.</param>
		/// <returns>Found devices, and if there are more devices available.</returns>
		Task<(SearchResultThing[], bool)> GetMyDevices(int Offset, int MaxCount);

		/// <summary>
		/// Gets the full list of my devices.
		/// </summary>
		/// <returns>Complete list of my devices.</returns>
		Task<SearchResultThing[]> GetAllMyDevices();

		/// <summary>
		/// Gets a control form from an actuator.
		/// </summary>
		/// <param name="To">Address of actuator.</param>
		/// <param name="Language">Language</param>
		/// <param name="Callback">Method to call when response is returned.</param>
		/// <param name="State">State object.</param>
		/// <param name="Nodes">Node references</param>
		void GetControlForm(string To, string Language, EventHandlerAsync<DataFormEventArgs> Callback, object? State,
			params ThingReference[] Nodes);

		/// <summary>
		/// Requests a sensor data readout.
		/// </summary>
		/// <param name="Destination">JID of sensor to read.</param>
		/// <param name="Types">Field Types to read.</param>
		/// <returns>Request object maintaining the current status of the request.</returns>
		Task<SensorDataClientRequest> RequestSensorReadout(string Destination, FieldType Types);

		/// <summary>
		/// Requests a sensor data readout.
		/// </summary>
		/// <param name="Destination">JID of sensor to read.</param>
		/// <param name="Nodes">Array of nodes to read. Can be null or empty, if reading a sensor that is not a concentrator.</param>
		/// <param name="Types">Field Types to read.</param>
		/// <returns>Request object maintaining the current status of the request.</returns>
		Task<SensorDataClientRequest> RequestSensorReadout(string Destination, ThingReference[] Nodes, FieldType Types);

		#endregion

		#region e-Daler

		/// <summary>
		/// Tries to parse an eDaler URI.
		/// </summary>
		/// <param name="Uri">URI string.</param>
		/// <param name="Parsed">Parsed eDaler URI, if successful.</param>
		/// <param name="Reason">Error message, if not able to parse URI.</param>
		/// <returns>If URI string could be parsed.</returns>
		bool TryParseEDalerUri(string Uri, out EDalerUri Parsed, out string Reason);

		/// <summary>
		/// Tries to decrypt an encrypted private message.
		/// </summary>
		/// <param name="EncryptedMessage">Encrypted message.</param>
		/// <param name="PublicKey">Public key used.</param>
		/// <param name="TransactionId">ID of transaction containing the encrypted message.</param>
		/// <param name="RemoteEndpoint">Remote endpoint</param>
		/// <returns>Decrypted string, if successful, or null, if not.</returns>
		Task<string> TryDecryptMessage(byte[] EncryptedMessage, byte[] PublicKey, Guid TransactionId, string RemoteEndpoint);

		/// <summary>
		/// Sends an eDaler URI to the eDaler service.
		/// </summary>
		/// <param name="Uri">eDaler URI</param>
		/// <returns>Transaction object containing information about the processed URI.</returns>
		Task<EDaler.Transaction> SendEDalerUri(string Uri);

		/// <summary>
		/// Event raised when balance has been updated.
		/// </summary>
		event EventHandlerAsync<BalanceEventArgs> EDalerBalanceUpdated;

		/// <summary>
		/// Gets account events available for the wallet.
		/// </summary>
		/// <param name="MaxCount">Maximum number of events to return.</param>
		/// <returns>Events found, and if more events are available.</returns>
		Task<(AccountEvent[], bool)> GetEDalerAccountEvents(int MaxCount);

		/// <summary>
		/// Gets account events available for the wallet.
		/// </summary>
		/// <param name="MaxCount">Maximum number of events to return.</param>
		/// <param name="From">From what point in time events should be returned.</param>
		/// <returns>Events found, and if more events are available.</returns>
		Task<(AccountEvent[], bool)> GetEDalerAccountEvents(int MaxCount, DateTime From);

		/// <summary>
		/// Gets the current account balance.
		/// </summary>
		/// <returns>Current account balance.</returns>
		Task<Balance> GetEDalerBalance();

		/// <summary>
		/// Gets pending payments
		/// </summary>
		/// <returns>(Total amount, currency, items)</returns>
		Task<(decimal, string, PendingPayment[])> GetPendingEDalerPayments();

		/// <summary>
		/// Creates a full payment URI.
		/// </summary>
		/// <param name="ToBareJid">To whom the payment is to be made.</param>
		/// <param name="Amount">Amount of eDaler to pay.</param>
		/// <param name="AmountExtra">Any amount extra of eDaler to pay.</param>
		/// <param name="Currency">Currency to pay.</param>
		/// <param name="ValidNrDays">For how many days the URI should be valid.</param>
		/// <returns>Signed payment URI.</returns>
		Task<string> CreateFullEDalerPaymentUri(string ToBareJid, decimal Amount, decimal? AmountExtra, string Currency, int ValidNrDays);

		/// <summary>
		/// Creates a full payment URI.
		/// </summary>
		/// <param name="ToBareJid">To whom the payment is to be made.</param>
		/// <param name="Amount">Amount of eDaler to pay.</param>
		/// <param name="AmountExtra">Any amount extra of eDaler to pay.</param>
		/// <param name="Currency">Currency to pay.</param>
		/// <param name="ValidNrDays">For how many days the URI should be valid.</param>
		/// <param name="Message">Unencrypted message to send to recipient.</param>
		/// <returns>Signed payment URI.</returns>
		Task<string> CreateFullEDalerPaymentUri(string ToBareJid, decimal Amount, decimal? AmountExtra, string Currency, int ValidNrDays, string Message);

		/// <summary>
		/// Creates a full payment URI.
		/// </summary>
		/// <param name="To">To whom the payment is to be made.</param>
		/// <param name="Amount">Amount of eDaler to pay.</param>
		/// <param name="AmountExtra">Any amount extra of eDaler to pay.</param>
		/// <param name="Currency">Currency to pay.</param>
		/// <param name="ValidNrDays">For how many days the URI should be valid.</param>
		/// <returns>Signed payment URI.</returns>
		Task<string> CreateFullEDalerPaymentUri(LegalIdentity To, decimal Amount, decimal? AmountExtra, string Currency, int ValidNrDays);

		/// <summary>
		/// Creates a full payment URI.
		/// </summary>
		/// <param name="To">To whom the payment is to be made.</param>
		/// <param name="Amount">Amount of eDaler to pay.</param>
		/// <param name="AmountExtra">Any amount extra of eDaler to pay.</param>
		/// <param name="Currency">Currency to pay.</param>
		/// <param name="ValidNrDays">For how many days the URI should be valid.</param>
		/// <param name="PrivateMessage">Message to be sent to recipient. Message will be end-to-end encrypted.</param>
		/// <returns>Signed payment URI.</returns>
		Task<string> CreateFullEDalerPaymentUri(LegalIdentity To, decimal Amount, decimal? AmountExtra, string Currency, int ValidNrDays, string PrivateMessage);

		/// <summary>
		/// Creates an incomplete PayMe-URI.
		/// </summary>
		/// <param name="BareJid">To whom the payment is to be made.</param>
		/// <param name="Amount">Amount of eDaler to pay.</param>
		/// <param name="AmountExtra">Any amount extra of eDaler to pay.</param>
		/// <param name="Currency">Currency to pay.</param>
		/// <param name="Message">Message to be sent to recipient (not encrypted).</param>
		/// <returns>Incomplete PayMe-URI.</returns>
		string CreateIncompleteEDalerPayMeUri(string BareJid, decimal? Amount, decimal? AmountExtra, string Currency, string Message);

		/// <summary>
		/// Creates an incomplete PayMe-URI.
		/// </summary>
		/// <param name="To">To whom the payment is to be made.</param>
		/// <param name="Amount">Amount of eDaler to pay.</param>
		/// <param name="AmountExtra">Any amount extra of eDaler to pay.</param>
		/// <param name="Currency">Currency to pay.</param>
		/// <param name="PrivateMessage">Message to be sent to recipient. Message will be end-to-end encrypted in payment.
		/// But the message will be unencrypted in the incomplete PeyMe URI.</param>
		/// <returns>Incomplete PayMe-URI.</returns>
		string CreateIncompleteEDalerPayMeUri(LegalIdentity To, decimal? Amount, decimal? AmountExtra, string Currency, string PrivateMessage);

		/// <summary>
		/// Last reported balance
		/// </summary>
		Balance? LastEDalerBalance { get; }

		/// <summary>
		/// Timepoint of last eDaler event.
		/// </summary>
		DateTime LastEDalerEvent { get; }

		/// <summary>
		/// Gets available service providers for buying eDaler.
		/// </summary>
		/// <returns>Available service providers.</returns>
		Task<IBuyEDalerServiceProvider[]> GetServiceProvidersForBuyingEDalerAsync();

		/// <summary>
		/// Initiates the process of getting available options for buying of eDaler using a service provider that
		/// uses a smart contract.
		/// </summary>
		/// <param name="ServiceId">Service ID</param>
		/// <param name="ServiceProvider">Service Provider</param>
		/// <returns>Transaction ID</returns>
		Task<OptionsTransaction> InitiateBuyEDalerGetOptions(string ServiceId, string ServiceProvider);

		/// <summary>
		/// Registers an initiated getting of payment options for buying eDaler as completed.
		/// </summary>
		/// <param name="TransactionId">Transaction ID</param>
		/// <param name="Options">Available options.</param>
		void BuyEDalerGetOptionsCompleted(string TransactionId, IDictionary<CaseInsensitiveString, object>[] Options);

		/// <summary>
		/// Registers an initiated getting of payment options for buying eDaler as failed.
		/// </summary>
		/// <param name="TransactionId">Transaction ID</param>
		/// <param name="Message">Error message.</param>
		void BuyEDalerGetOptionsFailed(string TransactionId, string Message);

		/// <summary>
		/// Initiates the buying of eDaler using a service provider that does not use a smart contract.
		/// </summary>
		/// <param name="ServiceId">Service ID</param>
		/// <param name="ServiceProvider">Service Provider</param>
		/// <param name="Amount">Amount</param>
		/// <param name="Currency">Currency</param>
		/// <returns>Transaction ID</returns>
		Task<PaymentTransaction> InitiateBuyEDaler(string ServiceId, string ServiceProvider, decimal Amount, string Currency);

		/// <summary>
		/// Initiates the process of getting available options for selling of eDaler using a service provider that
		/// uses a smart contract.
		/// </summary>
		/// <param name="ServiceId">Service ID</param>
		/// <param name="ServiceProvider">Service Provider</param>
		/// <returns>Transaction ID</returns>
		Task<OptionsTransaction> InitiateSellEDalerGetOptions(string ServiceId, string ServiceProvider);

		/// <summary>
		/// Registers an initiated getting of payment options for selling eDaler as completed.
		/// </summary>
		/// <param name="TransactionId">Transaction ID</param>
		/// <param name="Options">Available options.</param>
		void SellEDalerGetOptionsCompleted(string TransactionId, IDictionary<CaseInsensitiveString, object>[] Options);

		/// <summary>
		/// Registers an initiated getting of payment options for selling eDaler as failed.
		/// </summary>
		/// <param name="TransactionId">Transaction ID</param>
		/// <param name="Message">Error message.</param>
		void SellEDalerGetOptionsFailed(string TransactionId, string Message);

		/// <summary>
		/// Registers an initiated payment as completed.
		/// </summary>
		/// <param name="TransactionId">Transaction ID</param>
		/// <param name="Amount">Amount</param>
		/// <param name="Currency">Currency</param>
		void BuyEDalerCompleted(string TransactionId, decimal Amount, string Currency);

		/// <summary>
		/// Registers an initiated payment as failed.
		/// </summary>
		/// <param name="TransactionId">Transaction ID</param>
		/// <param name="Message">Error message.</param>
		void BuyEDalerFailed(string TransactionId, string Message);

		/// <summary>
		/// Gets available service providers for selling eDaler.
		/// </summary>
		/// <returns>Available service providers.</returns>
		Task<ISellEDalerServiceProvider[]> GetServiceProvidersForSellingEDalerAsync();

		/// <summary>
		/// Initiates the selling of eDaler using a service provider that does not use a smart contract.
		/// </summary>
		/// <param name="ServiceId">Service ID</param>
		/// <param name="ServiceProvider">Service Provider</param>
		/// <param name="Amount">Amount</param>
		/// <param name="Currency">Currency</param>
		/// <returns>Transaction ID</returns>
		Task<PaymentTransaction> InitiateSellEDaler(string ServiceId, string ServiceProvider, decimal Amount, string Currency);

		/// <summary>
		/// Registers an initiated payment as completed.
		/// </summary>
		/// <param name="TransactionId">Transaction ID</param>
		/// <param name="Amount">Amount</param>
		/// <param name="Currency">Currency</param>
		void SellEDalerCompleted(string TransactionId, decimal Amount, string Currency);

		/// <summary>
		/// Registers an initiated payment as failed.
		/// </summary>
		/// <param name="TransactionId">Transaction ID</param>
		/// <param name="Message">Error message.</param>
		void SellEDalerFailed(string TransactionId, string Message);

		#endregion

		#region Neuro-Features

		/// <summary>
		/// Event raised when a token has been removed from the wallet.
		/// </summary>
		event EventHandlerAsync<NeuroFeatures.EventArguments.TokenEventArgs> NeuroFeatureRemoved;

		/// <summary>
		/// Event raised when a token has been added to the wallet.
		/// </summary>
		event EventHandlerAsync<NeuroFeatures.EventArguments.TokenEventArgs> NeuroFeatureAdded;

		/// <summary>
		/// Event raised when variables have been updated in a state-machine.
		/// </summary>
		event EventHandlerAsync<VariablesUpdatedEventArgs> NeuroFeatureVariablesUpdated;

		/// <summary>
		/// Event raised when a state-machine has received a new state.
		/// </summary>
		event EventHandlerAsync<NewStateEventArgs>? NeuroFeatureStateUpdated;

		/// <summary>
		/// Timepoint of last Neuro-Feature token event.
		/// </summary>
		DateTime LastNeuroFeatureEvent { get; }

		/// <summary>
		/// Gets available tokens
		/// </summary>
		/// <returns>Response with tokens.</returns>
		Task<TokensEventArgs> GetNeuroFeatures();

		/// <summary>
		/// Gets a section of available tokens
		/// </summary>
		/// <returns>Response with tokens.</returns>
		/// <param name="Offset">Start offset of list</param>
		/// <param name="MaxCount">Maximum number of items in response.</param>
		Task<TokensEventArgs> GetNeuroFeatures(int Offset, int MaxCount);

		/// <summary>
		/// Gets references to available tokens
		/// </summary>
		/// <returns>Response with tokens.</returns>
		Task<string[]> GetNeuroFeatureReferences();

		/// <summary>
		/// Gets references to a section of available tokens
		/// </summary>
		/// <param name="Offset">Start offset of list</param>
		/// <param name="MaxCount">Maximum number of items in response.</param>
		/// <returns>Response with tokens.</returns>
		Task<string[]> GetNeuroFeatureReferences(int Offset, int MaxCount);

		/// <summary>
		/// Gets the value totals of tokens available in the wallet, grouped and ordered by currency.
		/// </summary>
		/// <returns>Response with tokens.</returns>
		Task<TokenTotalsEventArgs> GetNeuroFeatureTotals();

		/// <summary>
		/// Gets tokens created by a smart contract
		/// </summary>
		/// <param name="ContractId">Contract ID</param>
		/// <returns>Response with tokens.</returns>
		Task<TokensEventArgs> GetNeuroFeaturesForContract(string ContractId);

		/// <summary>
		/// Gets tokens created by a smart contract
		/// </summary>
		/// <param name="ContractId">Contract ID</param>
		/// <param name="Offset">Start offset of list</param>
		/// <param name="MaxCount">Maximum number of items in response.</param>
		/// <returns>Response with tokens.</returns>
		Task<TokensEventArgs> GetNeuroFeaturesForContract(string ContractId, int Offset, int MaxCount);

		/// <summary>
		/// Gets token references created by a smart contract
		/// </summary>
		/// <param name="ContractId">Contract ID</param>
		/// <returns>Response with tokens.</returns>
		Task<string[]> GetNeuroFeatureReferencesForContract(string ContractId);

		/// <summary>
		/// Gets token references created by a smart contract
		/// </summary>
		/// <param name="ContractId">Contract ID</param>
		/// <param name="Offset">Start offset of list</param>
		/// <param name="MaxCount">Maximum number of items in response.</param>
		/// <returns>Response with tokens.</returns>
		Task<string[]> GetNeuroFeatureReferencesForContract(string ContractId, int Offset, int MaxCount);

		/// <summary>
		/// Gets a specific token.
		/// </summary>
		/// <param name="TokenId">Token ID</param>
		/// <returns>Token</returns>
		Task<Token> GetNeuroFeature(string TokenId);

		/// <summary>
		/// Gets events relating to a specific token.
		/// </summary>
		/// <param name="TokenId">Token ID</param>
		/// <returns>Token events.</returns>
		Task<TokenEvent[]> GetNeuroFeatureEvents(string TokenId);

		/// <summary>
		/// Gets events relating to a specific token.
		/// </summary>
		/// <param name="TokenId">Token ID</param>
		/// <param name="Offset">Offset </param>
		/// <param name="MaxCount">Maximum number of events to return.</param>
		/// <returns>Token events.</returns>
		Task<TokenEvent[]> GetNeuroFeatureEvents(string TokenId, int Offset, int MaxCount);

		/// <summary>
		/// Adds a text note on a token.
		/// </summary>
		/// <param name="TokenId">Token ID</param>
		/// <param name="TextNote">Text Note</param>
		Task AddNeuroFeatureTextNote(string TokenId, string TextNote);

		/// <summary>
		/// Adds a text note on a token.
		/// </summary>
		/// <param name="TokenId">Token ID</param>
		/// <param name="TextNote">Text Note</param>
		/// <param name="Personal">If the text note contains personal information. (default=false).
		/// 
		/// Note: Personal notes are deleted when ownership of token is transferred.</param>
		Task AddNeuroFeatureTextNote(string TokenId, string TextNote, bool Personal);

		/// <summary>
		/// Adds a xml note on a token.
		/// </summary>
		/// <param name="TokenId">Token ID</param>
		/// <param name="XmlNote">Xml Note</param>
		Task AddNeuroFeatureXmlNote(string TokenId, string XmlNote);

		/// <summary>
		/// Adds a xml note on a token.
		/// </summary>
		/// <param name="TokenId">Token ID</param>
		/// <param name="XmlNote">Xml Note</param>
		/// <param name="Personal">If the xml note contains personal information. (default=false).
		/// 
		/// Note: Personal notes are deleted when ownership of token is transferred.</param>
		Task AddNeuroFeatureXmlNote(string TokenId, string XmlNote, bool Personal);

		/// <summary>
		/// Gets token creation attributes from the broker.
		/// </summary>
		/// <returns>Token creation attributes.</returns>
		Task<CreationAttributesEventArgs> GetNeuroFeatureCreationAttributes();

		/// <summary>
		/// Generates a XAML report for a state diagram corresponding to the token.
		/// </summary>
		/// <returns>String-representation of XAML of report.</returns>
		Task<string> GenerateNeuroFeatureStateDiagramReport(string TokenId);

		/// <summary>
		/// Generates a XAML report for a timing diagram corresponding to the token.
		/// </summary>
		/// <returns>String-representation of XAML of report.</returns>
		Task<string> GenerateNeuroFeatureProfilingReport(string TokenId);

		/// <summary>
		/// Generates a XAML present report for a token.
		/// </summary>
		/// <returns>String-representation of XAML of report.</returns>
		Task<string> GenerateNeuroFeaturePresentReport(string TokenId);

		/// <summary>
		/// Generates a XAML history report for a token.
		/// </summary>
		/// <returns>String-representation of XAML of report.</returns>
		Task<string> GenerateNeuroFeatureHistoryReport(string TokenId);

		/// <summary>
		/// Gets the current state of a Neuro-Feature token.
		/// </summary>
		/// <param name="TokenId">Token ID</param>
		/// <returns>Current state</returns>
		Task<CurrentStateEventArgs> GetNeuroFeatureCurrentState(string TokenId);

		#endregion

		#region Private XML

		/// <summary>
		/// Saves Private XML to the server. Private XML are separated by
		/// Local Name and Namespace of the root element. Only one document
		/// per fully qualified name. When saving private XML, the XML overwrites
		/// any existing XML having the same local name and namespace.
		/// </summary>
		/// <param name="Xml">XML to save.</param>
		Task SavePrivateXml(string Xml);

		/// <summary>
		/// Saves Private XML to the server. Private XML are separated by
		/// Local Name and Namespace of the root element. Only one document
		/// per fully qualified name. When saving private XML, the XML overwrites
		/// any existing XML having the same local name and namespace.
		/// </summary>
		/// <param name="Xml">XML to save.</param>
		Task SavePrivateXml(XmlElement Xml);

		/// <summary>
		/// Loads private XML previously stored, given the local name and
		/// namespace of the XML.
		/// </summary>
		/// <param name="LocalName">Local Name</param>
		/// <param name="Namespace">Namespace</param>
		/// <returns>XML Element, if found.</returns>
		Task<XmlElement?> LoadPrivateXml(string LocalName, string Namespace);

		/// <summary>
		/// Deletes private XML previously saved to the account.
		/// </summary>
		/// <param name="LocalName">Local Name</param>
		/// <param name="Namespace">Namespace</param>
		Task DeletePrivateXml(string LocalName, string Namespace);

		#endregion
	}
}
