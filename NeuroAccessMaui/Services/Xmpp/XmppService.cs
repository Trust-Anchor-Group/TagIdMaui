//#define DEBUG_XMPP_REMOTE
//#define DEBUG_LOG_REMOTE
//#define DEBUG_DB_REMOTE

using CommunityToolkit.Mvvm.Messaging;
using EDaler;
using EDaler.Events;
using EDaler.Uris;
using Mopups.Services;
using NeuroAccessMaui.Extensions;
using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services.Contacts;
using NeuroAccessMaui.Services.Contracts;
using NeuroAccessMaui.Services.Notification.Things;
using NeuroAccessMaui.Services.Notification.Xmpp;
using NeuroAccessMaui.Services.Push;
using NeuroAccessMaui.Services.Tag;
using NeuroAccessMaui.Services.UI.Photos;
using NeuroAccessMaui.Services.Wallet;
using NeuroAccessMaui.UI.Pages.Contacts.Chat;
using NeuroAccessMaui.UI.Pages.Registration;
using NeuroAccessMaui.UI.Popups.Xmpp.ReportOrBlock;
using NeuroAccessMaui.UI.Popups.Xmpp.ReportType;
using NeuroAccessMaui.UI.Popups.Xmpp.SubscribeTo;
using NeuroAccessMaui.UI.Popups.Xmpp.SubscriptionRequest;
using NeuroFeatures;
using NeuroFeatures.EventArguments;
using NeuroFeatures.Events;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Xml;
using Waher.Content;
using Waher.Content.Html;
using Waher.Content.Markdown;
using Waher.Content.Xml;
using Waher.Events;
using Waher.Events.Filter;
using Waher.Events.XMPP;
using Waher.Networking;
using Waher.Networking.Sniffers;
using Waher.Networking.XMPP;
using Waher.Networking.XMPP.Abuse;
using Waher.Networking.XMPP.Concentrator;
using Waher.Networking.XMPP.Contracts;
using Waher.Networking.XMPP.Contracts.EventArguments;
using Waher.Networking.XMPP.Control;
using Waher.Networking.XMPP.DataForms;
using Waher.Networking.XMPP.Events;
using Waher.Networking.XMPP.HttpFileUpload;
using Waher.Networking.XMPP.HTTPX;
using Waher.Networking.XMPP.PEP;
using Waher.Networking.XMPP.PEP.Events;
using Waher.Networking.XMPP.Provisioning;
using Waher.Networking.XMPP.Provisioning.Events;
using Waher.Networking.XMPP.Provisioning.SearchOperators;
using Waher.Networking.XMPP.PubSub;
using Waher.Networking.XMPP.Push;
using Waher.Networking.XMPP.Sensor;
using Waher.Networking.XMPP.ServiceDiscovery;
using Waher.Networking.XMPP.StanzaErrors;
using Waher.Persistence;
using Waher.Persistence.Filters;
using Waher.Runtime.Inventory;
using Waher.Runtime.Settings;
using Waher.Runtime.Temporary;
using Waher.Security.JWT;
using Waher.Things;
using Waher.Things.SensorData;

namespace NeuroAccessMaui.Services.Xmpp
{
	/// <summary>
	/// XMPP Service, maintaining XMPP connections and XMPP Extensions.
	///
	/// Note: By duplicating event handlers on the service, event handlers continue to work, even if app
	/// goes to sleep, and new clients are created when awoken again.
	/// </summary>
	[Singleton]
	internal sealed class XmppService : LoadableService, IXmppService, IDisposableAsync
	{
		//private bool isDisposed;
		private XmppClient? xmppClient;
		private ContractsClient? contractsClient;
		private HttpFileUploadClient? fileUploadClient;
		private ThingRegistryClient? thingRegistryClient;
		private ProvisioningClient? provisioningClient;
		private ControlClient? controlClient;
		private SensorClient? sensorClient;
		private ConcentratorClient? concentratorClient;
		private EDalerClient? eDalerClient;
		private NeuroFeaturesClient? neuroFeaturesClient;
		private PushNotificationClient? pushNotificationClient;
		private AbuseClient? abuseClient;
		private PepClient? pepClient;
		private HttpxClient? httpxClient;
		private Timer? reconnectTimer;
		private string? domainName;
		private string? accountName;
		private string? passwordHash;
		private string? passwordHashMethod;
		private bool xmppConnected = false;
		private DateTime xmppLastStateChange = DateTime.MinValue;
		private readonly InMemorySniffer? sniffer = new(250);
		private bool isCreatingClient;
		private EventFilter? xmppFilteredEventSink;
		private string? token = null;
		private DateTime tokenCreated = DateTime.MinValue;
#if DEBUG_XMPP_REMOTE || DEBUG_LOG_REMOTE || DEBUG_DB_REMOTE
		private const string debugRecipient = "";     // TODO: Set JID of recipient of debug messages.
#endif
#if DEBUG_XMPP_REMOTE || DEBUG_DB_REMOTE
		private RemoteSniffer? debugSniffer = null;
#endif
#if DEBUG_LOG_REMOTE
		private EventFilter? debugEventSink = null;
#endif

		#region Creation / Destruction

		public XmppService()
		{
		}

		private async Task CreateXmppClient()
		{
			if (this.isCreatingClient)
				return;

			try
			{
				this.isCreatingClient = true;

				if (!this.XmppParametersCurrent() || this.XmppStale())
				{
					if (this.xmppClient is not null)
						await this.DestroyXmppClient();

					this.domainName = ServiceRef.TagProfile.Domain;
					this.accountName = ServiceRef.TagProfile.Account;
					this.passwordHash = ServiceRef.TagProfile.XmppPasswordHash;
					this.passwordHashMethod = ServiceRef.TagProfile.XmppPasswordHashMethod;

					string? HostName;
					int PortNumber;
					bool IsIpAddress;

					if (ServiceRef.TagProfile.DefaultXmppConnectivity)
					{
						HostName = this.domainName;
						PortNumber = XmppCredentials.DefaultPort;
						IsIpAddress = false;
					}
					else
					{
						(HostName, PortNumber, IsIpAddress) = await ServiceRef.NetworkService.LookupXmppHostnameAndPort(this.domainName!);

						if (HostName == this.domainName && PortNumber == XmppCredentials.DefaultPort)
						{
							ServiceRef.TagProfile.SetDomain(this.domainName, true, ServiceRef.TagProfile.ApiKey ?? string.Empty,
								ServiceRef.TagProfile.ApiSecret ?? string.Empty);
						}
					}

					this.xmppLastStateChange = DateTime.Now;
					this.xmppConnected = false;

					Assembly AppAssembly = App.Current!.GetType().Assembly;

					if (string.IsNullOrEmpty(this.passwordHashMethod))
					{
						this.xmppClient = new XmppClient(HostName, PortNumber, this.accountName, this.passwordHash,
							Constants.LanguageCodes.Default, AppAssembly, this.sniffer);
					}
					else
					{
						this.xmppClient = new XmppClient(HostName, PortNumber, this.accountName, this.passwordHash, this.passwordHashMethod,
							Constants.LanguageCodes.Default, AppAssembly, this.sniffer);
					}

#if DEBUG_XMPP_REMOTE || DEBUG_LOG_REMOTE || DEBUG_DB_REMOTE
					if (!string.IsNullOrEmpty(debugRecipient))
					{
#endif
#if DEBUG_XMPP_REMOTE || DEBUG_DB_REMOTE
						this.debugSniffer = new RemoteSniffer(debugRecipient, DateTime.MaxValue, this.xmppClient, this.xmppClient,
							ConcentratorServer.NamespaceConcentratorCurrent);
#endif
#if DEBUG_XMPP_REMOTE
						this.xmppClient.Add(this.debugSniffer);
#endif
#if DEBUG_LOG_REMOTE
						if (this.debugEventSink is not null)
						{
							Log.Unregister(this.debugEventSink);
							this.debugEventSink?.Dispose();
							this.debugEventSink = null;
						}

						this.debugEventSink = new EventFilter("Debug Event Filter",
							new XmppEventSink("Debug Event Sink", this.xmppClient, debugRecipient, false),
							EventType.Informational, (Event) =>
							{
								if (this.xmppClient is null || this.xmppClient.State != XmppState.Connected)
									return false;

								return string.IsNullOrEmpty(Event.StackTrace) || !Event.StackTrace.Contains("XmppEventSink");
							});

						Log.Register(this.debugEventSink);
#endif
#if DEBUG_DB_REMOTE
						if (!Ledger.HasProvider)
						{
							XmlFileLedger XmlFileLedger = new(new RemoteLedgerWriter());
							Ledger.Register(XmlFileLedger);

							await XmlFileLedger.Start();

							Ledger.StartListeningToDatabaseEvents();
						}
#endif
#if DEBUG_XMPP_REMOTE || DEBUG_LOG_REMOTE || DEBUG_DB_REMOTE
					}
#endif

					this.xmppClient.RequestRosterOnStartup = false;
					this.xmppClient.TrustServer = !IsIpAddress;
					this.xmppClient.AllowCramMD5 = false;
					this.xmppClient.AllowDigestMD5 = false;
					this.xmppClient.AllowPlain = false;
					this.xmppClient.AllowEncryption = true;
					this.xmppClient.AllowScramSHA1 = true;
					this.xmppClient.AllowScramSHA256 = true;
					this.xmppClient.AllowQuickLogin = true;

					this.xmppClient.RequestRosterOnStartup = true;
					this.xmppClient.OnStateChanged += this.XmppClient_StateChanged;
					this.xmppClient.OnConnectionError += this.XmppClient_ConnectionError;
					this.xmppClient.OnError += this.XmppClient_Error;
					this.xmppClient.OnChatMessage += this.XmppClient_OnChatMessage;
					this.xmppClient.OnNormalMessage += this.XmppClient_OnNormalMessage;
					this.xmppClient.OnPresenceSubscribe += this.XmppClient_OnPresenceSubscribe;
					this.xmppClient.OnPresenceUnsubscribed += this.XmppClient_OnPresenceUnsubscribed;
					this.xmppClient.OnRosterItemAdded += this.XmppClient_OnRosterItemAdded;
					this.xmppClient.OnRosterItemUpdated += this.XmppClient_OnRosterItemUpdated;
					this.xmppClient.OnRosterItemRemoved += this.XmppClient_OnRosterItemRemoved;
					this.xmppClient.OnPresence += this.XmppClient_OnPresence;

					this.xmppClient.RegisterMessageHandler("Delivered", ContractsClient.NamespaceOnboarding, this.TransferIdDelivered, true);
					this.xmppClient.RegisterMessageHandler("clientMessage", ContractsClient.NamespaceLegalIdentitiesCurrent, this.ClientMessage, true);

					this.xmppFilteredEventSink = new EventFilter("XMPP Event Filter",
						new XmppEventSink("XMPP Event Sink", this.xmppClient, ServiceRef.TagProfile.LogJid, false),
						EventType.Error);

					// Add extensions before connecting

					this.abuseClient = new AbuseClient(this.xmppClient);

					if (!string.IsNullOrWhiteSpace(ServiceRef.TagProfile.LegalJid))
					{
						this.contractsClient = new ContractsClient(this.xmppClient, ServiceRef.TagProfile.LegalJid);
						this.RegisterContractsEventHandlers();

						if (!await this.contractsClient.LoadKeys(false))
						{
							if (ServiceRef.TagProfile.IsCompleteOrWaitingForValidation())
							{
								Log.Alert("Regeneration of keys not permitted at this time.",
									string.Empty, string.Empty, string.Empty, EventLevel.Major, string.Empty, string.Empty, Environment.StackTrace);

								throw new Exception("Regeneration of keys not permitted at this time.");
							}

							await this.GenerateNewKeys();
						}
					}

					if (!string.IsNullOrWhiteSpace(ServiceRef.TagProfile.HttpFileUploadJid) && (ServiceRef.TagProfile.HttpFileUploadMaxSize > 0))
						this.fileUploadClient = new HttpFileUploadClient(this.xmppClient, ServiceRef.TagProfile.HttpFileUploadJid, ServiceRef.TagProfile.HttpFileUploadMaxSize);

					if (!string.IsNullOrWhiteSpace(ServiceRef.TagProfile.RegistryJid))
						this.thingRegistryClient = new ThingRegistryClient(this.xmppClient, ServiceRef.TagProfile.RegistryJid);

					if (!string.IsNullOrWhiteSpace(ServiceRef.TagProfile.ProvisioningJid))
					{
						this.provisioningClient = new ProvisioningClient(this.xmppClient, ServiceRef.TagProfile.ProvisioningJid)
						{
							ManagePresenceSubscriptionRequests = false
						};

						this.provisioningClient.CanControlQuestion += this.ProvisioningClient_CanControlQuestion;
						this.provisioningClient.CanReadQuestion += this.ProvisioningClient_CanReadQuestion;
						this.provisioningClient.IsFriendQuestion += this.ProvisioningClient_IsFriendQuestion;
					}

					if (!string.IsNullOrWhiteSpace(ServiceRef.TagProfile.EDalerJid))
					{
						this.eDalerClient = new EDalerClient(this.xmppClient, this.contractsClient, ServiceRef.TagProfile.EDalerJid);
						this.RegisterEDalerEventHandlers(this.eDalerClient);
					}

					if (!string.IsNullOrWhiteSpace(ServiceRef.TagProfile.NeuroFeaturesJid))
					{
						this.neuroFeaturesClient = new NeuroFeaturesClient(this.xmppClient, this.contractsClient, ServiceRef.TagProfile.NeuroFeaturesJid);
						this.RegisterNeuroFeatureEventHandlers(this.neuroFeaturesClient);
					}

					if (ServiceRef.TagProfile.SupportsPushNotification)
						this.pushNotificationClient = new PushNotificationClient(this.xmppClient);

					this.sensorClient = new SensorClient(this.xmppClient);
					this.controlClient = new ControlClient(this.xmppClient);
					this.concentratorClient = new ConcentratorClient(this.xmppClient);

					this.pepClient = new PepClient(this.xmppClient);
					this.ReregisterPepEventHandlers(this.pepClient);

					this.httpxClient = new HttpxClient(this.xmppClient, 8192);
					Types.SetModuleParameter("XMPP", this.xmppClient);      // Makes the XMPP Client the default XMPP client, when resolving HTTP over XMPP requests.

					this.IsLoggedOut = false;
					await this.xmppClient.Connect(IsIpAddress ? string.Empty : this.domainName);
					this.RecreateReconnectTimer();

					// Await connected state during registration or user initiated log in, but not otherwise.
					if (!ServiceRef.TagProfile.IsCompleteOrWaitingForValidation())
					{
						if (!await this.WaitForConnectedState(Constants.Timeouts.XmppConnect))
						{
							ServiceRef.LogService.LogWarning("Connection to XMPP server failed.",
								new KeyValuePair<string, object?>("Domain", this.domainName ?? string.Empty),
								new KeyValuePair<string, object?>("Account", this.accountName ?? string.Empty),
								new KeyValuePair<string, object?>("Timeout", Constants.Timeouts.XmppConnect));
						}
					}
				}
			}
			finally
			{
				this.isCreatingClient = false;
			}
		}

#if DEBUG_DB_REMOTE
		private class RemoteLedgerWriter()
			: TextWriter(CultureInfo.CurrentCulture)
		{
			private readonly StringBuilder sb = new();

			public override Encoding Encoding => Encoding.Unicode;
			public override void Flush() => this.FlushAsync().Wait();
			public override Task FlushAsync(CancellationToken cancellationToken) => this.FlushAsync();

			public override async Task FlushAsync()
			{
				try
				{
					string s = this.sb.ToString();
					string s2 = s.TrimStart();
					if (string.IsNullOrEmpty(s2))
						return;

					if (ServiceRef.XmppService is not XmppService Service)
						return;

					RemoteSniffer? Sniffer = Service.debugSniffer;
					if (Sniffer is null)
						return;

					this.sb.Clear();

					int i = s2.IndexOf('<');
					if (i > 0)
						s2 = s2[i..];

					string[] Rows = s.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

					if (s2.StartsWith("<New", StringComparison.OrdinalIgnoreCase))
					{
						foreach (string Row in Rows)
							await Sniffer.TransmitText(Row);
					}
					else if (s2.StartsWith("<Update", StringComparison.OrdinalIgnoreCase))
					{
						foreach (string Row in Rows)
							await Sniffer.ReceiveText(Row);
					}
					else if (s2.StartsWith("<Delete", StringComparison.OrdinalIgnoreCase))
					{
						foreach (string Row in Rows)
							await Sniffer.Error(Row);
					}
					else if (s2.StartsWith("<Clear", StringComparison.OrdinalIgnoreCase))
					{
						foreach (string Row in Rows)
							await Sniffer.Warning(Row);
					}
					else
					{
						foreach (string Row in Rows)
							await Sniffer.Information(Row);
					}
				}
				catch (Exception)
				{
					// Ignore
				}
			}

			public override void Write(char value) => this.sb.Append(value);
			public override void Write(char[]? buffer) => this.sb.Append(buffer);
			public override void Write(char[] buffer, int index, int count) => this.sb.Append(new string(buffer, index, count));
			public override void Write(ReadOnlySpan<char> buffer) => this.sb.Append(new string(buffer));
			public override void Write(bool value) => this.sb.Append(value);
			public override void Write(int value) => this.sb.Append(value);
			public override void Write(uint value) => this.sb.Append(value);
			public override void Write(long value) => this.sb.Append(value);
			public override void Write(ulong value) => this.sb.Append(value);
			public override void Write(float value) => this.sb.Append(value);
			public override void Write(double value) => this.sb.Append(value);
			public override void Write(decimal value) => this.sb.Append(value);
			public override void Write(string? value) => this.sb.Append(value);
			public override void Write(object? value) => this.sb.Append(value);
			public override void Write(StringBuilder? value) => this.sb.Append(value);
			public override void Write([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, object? arg0) => this.sb.Append(string.Format(this.FormatProvider, format, arg0));
			public override void Write([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, object? arg0, object? arg1) => this.sb.Append(string.Format(this.FormatProvider, format, arg0, arg1));
			public override void Write([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, object? arg0, object? arg1, object? arg2) => this.sb.Append(string.Format(this.FormatProvider, format, arg0, arg1, arg2));
			public override void Write([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, params object?[] arg) => this.sb.Append(string.Format(this.FormatProvider, format, arg));
			public override Task WriteAsync(char value) { this.sb.Append(value); return Task.CompletedTask; }
			public override Task WriteAsync(string? value) { this.sb.Append(value); return Task.CompletedTask; }
			public override Task WriteAsync(StringBuilder? value, CancellationToken cancellationToken = default) { this.sb.Append(value); return Task.CompletedTask; }
			public override Task WriteAsync(char[] buffer, int index, int count) { this.sb.Append(new string(buffer, index, count)); return Task.CompletedTask; }
			public override Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default) { this.sb.Append(buffer); return Task.CompletedTask; }
			public override void WriteLine() => this.sb.AppendLine(string.Empty);
			public override void WriteLine(char value) => this.sb.AppendLine(value.ToString());
			public override void WriteLine(char[]? buffer) => this.sb.AppendLine(new string(buffer));
			public override void WriteLine(char[] buffer, int index, int count) => this.sb.AppendLine(new string(buffer, index, count));
			public override void WriteLine(ReadOnlySpan<char> buffer) => this.sb.AppendLine(new string(buffer));
			public override void WriteLine(bool value) => this.sb.AppendLine(value.ToString());
			public override void WriteLine(int value) => this.sb.AppendLine(value.ToString(CultureInfo.CurrentCulture));
			public override void WriteLine(uint value) => this.sb.AppendLine(value.ToString(CultureInfo.CurrentCulture));
			public override void WriteLine(long value) => this.sb.AppendLine(value.ToString(CultureInfo.CurrentCulture));
			public override void WriteLine(ulong value) => this.sb.AppendLine(value.ToString(CultureInfo.CurrentCulture));
			public override void WriteLine(float value) => this.sb.AppendLine(value.ToString(CultureInfo.CurrentCulture));
			public override void WriteLine(double value) => this.sb.AppendLine(value.ToString(CultureInfo.CurrentCulture));
			public override void WriteLine(decimal value) => this.sb.AppendLine(value.ToString(CultureInfo.CurrentCulture));
			public override void WriteLine(string? value) => this.sb.AppendLine(value?.ToString());
			public override void WriteLine(StringBuilder? value) => this.sb.AppendLine(value?.ToString());
			public override void WriteLine(object? value) => this.sb.AppendLine(value?.ToString());
			public override void WriteLine([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, object? arg0) => this.sb.AppendLine(string.Format(this.FormatProvider, format, arg0));
			public override void WriteLine([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, object? arg0, object? arg1) => this.sb.AppendLine(string.Format(this.FormatProvider, format, arg0, arg1));
			public override void WriteLine([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, object? arg0, object? arg1, object? arg2) => this.sb.AppendLine(string.Format(this.FormatProvider, format, arg0, arg1, arg2));
			public override void WriteLine([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, params object?[] arg) => this.sb.AppendLine(string.Format(this.FormatProvider, format, arg));
			public override Task WriteLineAsync(char value) { this.sb.AppendLine(value.ToString()); return Task.CompletedTask; }
			public override Task WriteLineAsync(string? value) { this.sb.AppendLine(value); return Task.CompletedTask; }
			public override Task WriteLineAsync(StringBuilder? value, CancellationToken cancellationToken = default) { this.sb.AppendLine(value?.ToString()); return Task.CompletedTask; }
			public override Task WriteLineAsync(char[] buffer, int index, int count) { this.sb.AppendLine(new string(buffer, index, count)); return Task.CompletedTask; }
			public override Task WriteLineAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default) { this.sb.AppendLine(new string(buffer.Span)); return Task.CompletedTask; }
			public override Task WriteLineAsync() { this.sb.AppendLine(string.Empty); return Task.CompletedTask; }
		}
#endif

		private async Task DestroyXmppClient()
		{
			this.reconnectTimer?.Dispose();
			this.reconnectTimer = null;

			await this.OnConnectionStateChanged(XmppState.Offline);

			if (this.xmppFilteredEventSink is not null)
			{
				ServiceRef.LogService.RemoveListener(this.xmppFilteredEventSink);
				await this.xmppFilteredEventSink.SecondarySink.DisposeAsync();
				await this.xmppFilteredEventSink.DisposeAsync();
				this.xmppFilteredEventSink = null;
			}

			this.contractsClient?.Dispose();
			this.contractsClient = null;

			this.fileUploadClient?.Dispose();
			this.fileUploadClient = null;

			this.thingRegistryClient?.Dispose();
			this.thingRegistryClient = null;

			this.provisioningClient?.Dispose();
			this.provisioningClient = null;

			this.eDalerClient?.Dispose();
			this.eDalerClient = null;

			this.neuroFeaturesClient?.Dispose();
			this.neuroFeaturesClient = null;

			this.pushNotificationClient?.Dispose();
			this.pushNotificationClient = null;

			this.sensorClient?.Dispose();
			this.sensorClient = null;

			this.controlClient?.Dispose();
			this.controlClient = null;

			this.concentratorClient?.Dispose();
			this.concentratorClient = null;

			this.pepClient?.Dispose();
			this.pepClient = null;

			this.abuseClient?.Dispose();
			this.abuseClient = null;

#if DEBUG_DB_REMOTE
			this.debugSniffer = null;
#endif
#if DEBUG_LOG_REMOTE
			if (this.debugEventSink is not null)
			{
				Log.Unregister(this.debugEventSink);
				this.debugEventSink?.Dispose();
				this.debugEventSink = null;
			}
#endif
			if (this.xmppClient is not null)
			{
				await this.xmppClient.DisposeAsync();
				this.xmppClient = null;
			}
		}

		private bool XmppStale()
		{
			return this.xmppClient is null ||
				this.xmppClient.State == XmppState.Offline ||
				this.xmppClient.State == XmppState.Error ||
				(this.xmppClient.State != XmppState.Connected && (DateTime.Now - this.xmppLastStateChange).TotalSeconds >= 10);
		}

		private bool XmppParametersCurrent()
		{
			if (this.xmppClient is null)
				return false;

			if (this.domainName != ServiceRef.TagProfile.Domain)
				return false;

			if (this.accountName != ServiceRef.TagProfile.Account)
				return false;

			if (this.passwordHash != ServiceRef.TagProfile.XmppPasswordHash)
				return false;

			if (this.passwordHashMethod != ServiceRef.TagProfile.XmppPasswordHashMethod)
				return false;

			if (this.contractsClient?.ComponentAddress != ServiceRef.TagProfile.LegalJid)
				return false;

			if (this.fileUploadClient?.FileUploadJid != ServiceRef.TagProfile.HttpFileUploadJid)
				return false;

			if (this.thingRegistryClient?.ThingRegistryAddress != ServiceRef.TagProfile.RegistryJid)
				return false;

			if (this.provisioningClient?.ProvisioningServerAddress != ServiceRef.TagProfile.ProvisioningJid)
				return false;

			if (this.eDalerClient?.ComponentAddress != ServiceRef.TagProfile.EDalerJid)
				return false;

			if (this.neuroFeaturesClient?.ComponentAddress != ServiceRef.TagProfile.NeuroFeaturesJid)
				return false;

			if ((this.pushNotificationClient is null) ^ !ServiceRef.TagProfile.SupportsPushNotification)
				return false;

			return true;
		}

		private void RecreateReconnectTimer()
		{
			this.reconnectTimer?.Dispose();
			this.reconnectTimer = new Timer(this.ReconnectTimer_Tick, null, Constants.Intervals.Reconnect, Constants.Intervals.Reconnect);
		}

		/// <summary>
		/// <see cref="IDisposable.Dispose"/>
		/// </summary>
		[Obsolete("Use the DisposeAsync method.")]
		public void Dispose()
		{
			this.DisposeAsync().Wait();
		}

		/// <summary>
		/// <see cref="IDisposableAsync.DisposeAsync"/>
		/// </summary>
		public async Task DisposeAsync()
		{
			this.reconnectTimer?.Dispose();
			this.reconnectTimer = null;

			if (this.xmppFilteredEventSink is not null)
			{
				ServiceRef.LogService.RemoveListener(this.xmppFilteredEventSink);
				await this.xmppFilteredEventSink.SecondarySink.DisposeAsync();
				await this.xmppFilteredEventSink.DisposeAsync();
				this.xmppFilteredEventSink = null;
			}

			this.contractsClient?.Dispose();
			this.contractsClient = null;

			this.fileUploadClient?.Dispose();
			this.fileUploadClient = null;

			this.thingRegistryClient?.Dispose();
			this.thingRegistryClient = null;

			this.provisioningClient?.Dispose();
			this.provisioningClient = null;

			this.eDalerClient?.Dispose();
			this.eDalerClient = null;

			this.neuroFeaturesClient?.Dispose();
			this.neuroFeaturesClient = null;

			this.pushNotificationClient?.Dispose();
			this.pushNotificationClient = null;

			this.sensorClient?.Dispose();
			this.sensorClient = null;

			this.controlClient?.Dispose();
			this.controlClient = null;

			this.concentratorClient?.Dispose();
			this.concentratorClient = null;

			this.pepClient?.Dispose();
			this.pepClient = null;

			this.abuseClient?.Dispose();
			this.abuseClient = null;

			if (this.xmppClient is not null)
			{
				await this.xmppClient.DisposeAsync();
				this.xmppClient = null;
			}

			/*
			this.Dispose(true);
			GC.SuppressFinalize(this);
			*/
		}

		/*
		/// <summary>
		/// <see cref="IDisposable.Dispose"/>
		/// </summary>
		protected virtual void Dispose(bool disposing)
		{
			if (this.isDisposed)
			{
				return;
			}

			if (disposing)
			{
				this.abuseClient.Dispose();
				this.contractsClient.Dispose();
				this.fileUploadClient.Dispose();
				this.httpxClient.Dispose();
				this.reconnectTimer.Dispose();
				this.sniffer.Dispose();
				this.xmppClient.Dispose();
				this.xmppFilteredEventSink.SecondarySink.Dispose();
				this.xmppFilteredEventSink.Dispose();
			}

			this.isDisposed = true;
		}
		*/
		#endregion

		#region Lifecycle

		public async Task<bool> WaitForConnectedState(TimeSpan Timeout)
		{
			if (this.xmppClient is null)
			{
				DateTime Start = DateTime.Now;

				while (this.xmppClient is null && DateTime.Now - Start < Timeout)
					await Task.Delay(1000);

				if (this.xmppClient is null)
					return false;

				Timeout -= DateTime.Now - Start;
			}

			if (this.xmppClient.State == XmppState.Connected)
				return true;

			if (Timeout < TimeSpan.Zero)
				return false;

			int i = await this.xmppClient.WaitStateAsync((int)Timeout.TotalMilliseconds, XmppState.Connected);
			return i >= 0;
		}

		public override async Task Load(bool IsResuming, CancellationToken CancellationToken)
		{
			if (this.BeginLoad(IsResuming, CancellationToken))
			{
				try
				{
					ServiceRef.TagProfile.StepChanged += this.TagProfile_StepChanged;
					ServiceRef.TagProfile.Changed += this.TagProfile_Changed;

					if (ServiceRef.TagProfile.ShouldCreateClient() && !this.XmppParametersCurrent())
						await this.CreateXmppClient();

					if ((this.xmppClient is not null) &&
						this.xmppClient.State == XmppState.Connected &&
						ServiceRef.TagProfile.IsCompleteOrWaitingForValidation())
					{
						// Don't await this one, just fire and forget, to improve startup time.
						_ = this.xmppClient.SetPresenceAsync(Availability.Online);
					}

					this.EndLoad(true);
				}
				catch (Exception ex)
				{
					ex = Log.UnnestException(ex);
					ServiceRef.LogService.LogException(ex, this.GetClassAndMethod(MethodBase.GetCurrentMethod()));
					this.EndLoad(false);
				}
			}
		}

		public override Task Unload()
		{
			return this.Unload(false);
		}

		public Task UnloadFast()
		{
			return this.Unload(true);
		}

		private async Task Unload(bool fast)
		{
			if (this.BeginUnload())
			{
				try
				{
					ServiceRef.TagProfile.StepChanged -= this.TagProfile_StepChanged;
					ServiceRef.TagProfile.Changed -= this.TagProfile_Changed;

					this.reconnectTimer?.Dispose();
					this.reconnectTimer = null;

					if (this.xmppClient is not null)
					{
						this.xmppClient.CheckConnection = false;

						if (!fast)
						{
							try
							{
								await Task.WhenAny(
									this.xmppClient.SetPresenceAsync(Availability.Offline),
									Task.Delay(1000)    // Wait at most 1000 ms.
								);
							}
							catch (Exception)
							{
								// Ignore
							}
						}
					}

					await this.DestroyXmppClient();
				}
				catch (Exception ex)
				{
					ServiceRef.LogService.LogException(ex, this.GetClassAndMethod(MethodBase.GetCurrentMethod()));
				}

				this.EndUnload();
			}
		}

		private void TagProfile_StepChanged(object? Sender, EventArgs e)
		{
			if (!this.IsLoaded)
				return;

			Task ExecutionTask = Task.Run(async () =>
			{
				try
				{
					bool CreateXmppClient = ServiceRef.TagProfile.ShouldCreateClient();

					if (CreateXmppClient && !this.XmppParametersCurrent())
						await this.CreateXmppClient();
					else if (!CreateXmppClient)
						await this.DestroyXmppClient();
				}
				catch (Exception ex)
				{
					ServiceRef.LogService.LogException(ex);
				}
			});
		}

		private void TagProfile_Changed(object? Sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(ITagProfile.Account))
				this.TagProfile_StepChanged(Sender, new EventArgs());
		}

		private Task XmppClient_Error(object? _, Exception e)
		{
			this.LatestError = e.Message;
			return Task.CompletedTask;
		}

		private Task XmppClient_ConnectionError(object? _, Exception e)
		{
			if (e is ObjectDisposedException)
				this.LatestConnectionError = ServiceRef.Localizer[nameof(AppResources.UnableToConnect)];
			else
				this.LatestConnectionError = e.Message;

			return Task.CompletedTask;
		}

		private async Task XmppClient_StateChanged(object? Sender, XmppState NewState)
		{
			this.xmppLastStateChange = DateTime.Now;

			switch (NewState)
			{
				case XmppState.Connecting:
					this.LatestError = string.Empty;
					this.LatestConnectionError = string.Empty;
					break;

				case XmppState.Connected:
					this.LatestError = string.Empty;
					this.LatestConnectionError = string.Empty;

					this.xmppConnected = true;

					this.RecreateReconnectTimer();

					if (string.IsNullOrEmpty(ServiceRef.TagProfile.XmppPasswordHashMethod))
					{
						ServiceRef.TagProfile.SetAccount(ServiceRef.TagProfile.Account ?? string.Empty,
							this.xmppClient?.PasswordHash ?? string.Empty,
							this.xmppClient?.PasswordHashMethod ?? string.Empty);
					}

					if (ServiceRef.TagProfile.NeedsUpdating() && await this.DiscoverServices())
					{
						if (this.contractsClient is null && !string.IsNullOrWhiteSpace(ServiceRef.TagProfile.LegalJid))
						{
							this.contractsClient = new ContractsClient(this.xmppClient, ServiceRef.TagProfile.LegalJid);
							this.RegisterContractsEventHandlers();

							if (!await this.contractsClient.LoadKeys(false))
							{
								this.contractsClient.Dispose();
								this.contractsClient = null;
							}
						}

						if (this.fileUploadClient is null && !string.IsNullOrWhiteSpace(ServiceRef.TagProfile.HttpFileUploadJid) && (ServiceRef.TagProfile.HttpFileUploadMaxSize > 0))
							this.fileUploadClient = new HttpFileUploadClient(this.xmppClient, ServiceRef.TagProfile.HttpFileUploadJid, ServiceRef.TagProfile.HttpFileUploadMaxSize);

						if (this.thingRegistryClient is null && !string.IsNullOrWhiteSpace(ServiceRef.TagProfile.RegistryJid))
							this.thingRegistryClient = new ThingRegistryClient(this.xmppClient, ServiceRef.TagProfile.RegistryJid);

						if (this.provisioningClient is null && !string.IsNullOrWhiteSpace(ServiceRef.TagProfile.RegistryJid))
						{
							this.provisioningClient = new ProvisioningClient(this.xmppClient, ServiceRef.TagProfile.ProvisioningJid)
							{
								ManagePresenceSubscriptionRequests = false
							};

							this.provisioningClient.CanControlQuestion += this.ProvisioningClient_CanControlQuestion;
							this.provisioningClient.CanReadQuestion += this.ProvisioningClient_CanReadQuestion;
							this.provisioningClient.IsFriendQuestion += this.ProvisioningClient_IsFriendQuestion;
						}

						if (this.eDalerClient is null && !string.IsNullOrWhiteSpace(ServiceRef.TagProfile.EDalerJid))
						{
							this.eDalerClient = new EDalerClient(this.xmppClient, this.contractsClient, ServiceRef.TagProfile.EDalerJid);
							this.RegisterEDalerEventHandlers(this.eDalerClient);
						}

						if (this.neuroFeaturesClient is null && !string.IsNullOrWhiteSpace(ServiceRef.TagProfile.NeuroFeaturesJid))
						{
							this.neuroFeaturesClient = new NeuroFeaturesClient(this.xmppClient, this.contractsClient, ServiceRef.TagProfile.NeuroFeaturesJid);
							this.RegisterNeuroFeatureEventHandlers(this.neuroFeaturesClient);
						}

						if (this.pushNotificationClient is null && ServiceRef.TagProfile.SupportsPushNotification)
							this.pushNotificationClient = new PushNotificationClient(this.xmppClient);
					}

					ServiceRef.LogService.AddListener(this.xmppFilteredEventSink!);

					await ServiceRef.PushNotificationService.CheckPushNotificationToken();
					break;

				case XmppState.Offline:
				case XmppState.Error:
					if (this.xmppConnected && !this.IsUnloading)
					{
						this.xmppConnected = false;

						if (this.xmppClient is not null && !this.xmppClient.Disposed)
							await this.xmppClient.Reconnect();
					}
					break;
			}

			await this.OnConnectionStateChanged(NewState);
		}

		/// <summary>
		/// An event that triggers whenever the connection state to the XMPP server changes.
		/// </summary>
		public event EventHandlerAsync<XmppState>? ConnectionStateChanged;

		private async Task OnConnectionStateChanged(XmppState NewState)
		{
			await this.ConnectionStateChanged.Raise(this, NewState);
		}

		#endregion

		#region State

		public bool IsLoggedOut { get; private set; }
		public bool IsOnline => (this.xmppClient is not null) && this.xmppClient.State == XmppState.Connected;
		public XmppState State => this.xmppClient?.State ?? XmppState.Offline;
		public string BareJid => this.xmppClient?.BareJID ?? string.Empty;

		public string? LatestError { get; private set; }
		public string? LatestConnectionError { get; private set; }

		#endregion

		#region Connections

		private enum ConnectOperation
		{
			Connect,
			ConnectAndCreateAccount,
			ConnectToAccount
		}

		public Task<(bool Succeeded, string? ErrorMessage, string[]? Alternatives)> TryConnect(string domain, bool isIpAddress, string hostName, int portNumber,
			string languageCode, Assembly applicationAssembly, Func<XmppClient, Task> connectedFunc)
		{
			return this.TryConnectInner(domain, isIpAddress, hostName, portNumber, string.Empty, string.Empty, string.Empty, languageCode,
				string.Empty, string.Empty, applicationAssembly, connectedFunc, ConnectOperation.Connect);
		}

		public Task<(bool Succeeded, string? ErrorMessage, string[]? Alternatives)> TryConnectAndCreateAccount(string domain, bool isIpAddress, string hostName,
			int portNumber, string userName, string password, string languageCode, string ApiKey, string ApiSecret,
			Assembly applicationAssembly, Func<XmppClient, Task> connectedFunc)
		{
			return this.TryConnectInner(domain, isIpAddress, hostName, portNumber, userName, password, string.Empty, languageCode,
				ApiKey, ApiSecret, applicationAssembly, connectedFunc, ConnectOperation.ConnectAndCreateAccount);
		}

		public Task<(bool Succeeded, string? ErrorMessage, string[]? Alternatives)> TryConnectAndConnectToAccount(string domain, bool isIpAddress, string hostName,
			int portNumber, string userName, string password, string passwordMethod, string languageCode, Assembly applicationAssembly,
			Func<XmppClient, Task> connectedFunc)
		{
			return this.TryConnectInner(domain, isIpAddress, hostName, portNumber, userName, password, passwordMethod, languageCode,
				string.Empty, string.Empty, applicationAssembly, connectedFunc, ConnectOperation.ConnectToAccount);
		}

		private async Task<(bool Succeeded, string? ErrorMessage, string[]? Alternatives)> TryConnectInner(string Domain, bool IsIpAddress, string HostName,
			int PortNumber, string UserName, string Password, string PasswordMethod, string LanguageCode, string ApiKey, string ApiSecret,
			Assembly ApplicationAssembly, Func<XmppClient, Task> ConnectedFunc, ConnectOperation Operation)
		{
			TaskCompletionSource<bool> Connected = new();
			bool Succeeded;
			string? ErrorMessage = null;
			bool StreamNegotiation = false;
			bool StreamOpened = false;
			bool StartingEncryption = false;
			bool Authenticating = false;
			bool Registering = false;
			bool IsTimeout = false;
			string? ConnectionError = null;
			string[]? Alternatives = null;

			Task OnConnectionError(object _, Exception e)
			{
				if (e is ObjectDisposedException)
					ConnectionError = ServiceRef.Localizer[nameof(AppResources.UnableToConnect)];
				else if (e is ConflictException ConflictInfo)
					Alternatives = ConflictInfo.Alternatives;
				else
					ConnectionError = e.Message;

				Connected.TrySetResult(false);
				return Task.CompletedTask;
			}

			async Task OnStateChanged(object _, XmppState newState)
			{
				switch (newState)
				{
					case XmppState.StreamNegotiation:
						StreamNegotiation = true;
						break;

					case XmppState.StreamOpened:
						StreamOpened = true;
						break;

					case XmppState.StartingEncryption:
						StartingEncryption = true;
						break;

					case XmppState.Authenticating:
						Authenticating = true;

						if (Operation == ConnectOperation.Connect)
							Connected.TrySetResult(true);

						break;

					case XmppState.Registering:
						Registering = true;
						break;

					case XmppState.Connected:
						Connected.TrySetResult(true);
						break;

					case XmppState.Offline:
						Connected.TrySetResult(false);
						break;

					case XmppState.Error:
						// When State = Error, wait for the OnConnectionError event to arrive also, as it holds more/direct information.
						// Just in case it never would - set state error and result.
						await Task.Delay(Constants.Timeouts.XmppConnect);
						Connected.TrySetResult(false);
						break;
				}
			}

			XmppClient? Client = null;
			try
			{
				if (string.IsNullOrEmpty(PasswordMethod))
					Client = new XmppClient(HostName, PortNumber, UserName, Password, LanguageCode, ApplicationAssembly, this.sniffer);
				else
					Client = new XmppClient(HostName, PortNumber, UserName, Password, PasswordMethod, LanguageCode, ApplicationAssembly, this.sniffer);

				if (Operation == ConnectOperation.ConnectAndCreateAccount)
				{
					if (!string.IsNullOrEmpty(ApiKey) && !string.IsNullOrEmpty(ApiSecret))
						Client.AllowRegistration(ApiKey, ApiSecret);
					else
						Client.AllowRegistration();
				}

				Client.TrustServer = !IsIpAddress;
				Client.AllowCramMD5 = false;
				Client.AllowDigestMD5 = false;
				Client.AllowPlain = false;
				Client.AllowEncryption = true;
				Client.AllowScramSHA1 = true;
				Client.AllowScramSHA256 = true;
				Client.AllowQuickLogin = true;

				Client.OnConnectionError += OnConnectionError;
				Client.OnStateChanged += OnStateChanged;

				await Client.Connect(IsIpAddress ? string.Empty : Domain);

				void TimerCallback(object? _)
				{
					IsTimeout = true;
					Connected.TrySetResult(false);
				}

				using Timer _ = new(TimerCallback, null, (int)Constants.Timeouts.XmppConnect.TotalMilliseconds, Timeout.Infinite);
				Succeeded = await Connected.Task;

				if (Succeeded && (ConnectedFunc is not null))
					await ConnectedFunc(Client);

				Client.OnStateChanged -= OnStateChanged;
				Client.OnConnectionError -= OnConnectionError;
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex, new KeyValuePair<string, object?>(nameof(ConnectOperation), Operation.ToString()));
				Succeeded = false;
				ErrorMessage = ServiceRef.Localizer[nameof(AppResources.UnableToConnectTo), Domain];
			}
			finally
			{
				if (Client is not null)
				{
					await Client.DisposeAsync();
					Client = null;
				}
			}

			if (!Succeeded && string.IsNullOrEmpty(ErrorMessage))
			{
				if (this.sniffer is not null)
					System.Diagnostics.Debug.WriteLine(await this.sniffer.SnifferToTextAsync(), "Sniffer");

				if (!StreamNegotiation || IsTimeout)
					ErrorMessage = ServiceRef.Localizer[nameof(AppResources.CantConnectTo), Domain];
				else if (!StreamOpened)
					ErrorMessage = ServiceRef.Localizer[nameof(AppResources.DomainIsNotAValidOperator), Domain];
				else if (!StartingEncryption)
					ErrorMessage = ServiceRef.Localizer[nameof(AppResources.DomainDoesNotFollowEncryptionPolicy), Domain];
				else if (!Authenticating)
					ErrorMessage = ServiceRef.Localizer[nameof(AppResources.UnableToAuthenticateWith), Domain];
				else if (!Registering)
				{
					if (!string.IsNullOrWhiteSpace(ConnectionError))
						ErrorMessage = ConnectionError;
					else
						ErrorMessage = ServiceRef.Localizer[nameof(AppResources.OperatorDoesNotSupportRegisteringNewAccounts), Domain];
				}
				else if (Operation == ConnectOperation.ConnectAndCreateAccount)
					ErrorMessage = ServiceRef.Localizer[nameof(AppResources.UsernameNameAlreadyTaken), this.accountName ?? string.Empty];
				else if (Operation == ConnectOperation.ConnectToAccount)
					ErrorMessage = ServiceRef.Localizer[nameof(AppResources.InvalidUsernameOrPassword), this.accountName ?? string.Empty];
				else
					ErrorMessage = ServiceRef.Localizer[nameof(AppResources.UnableToConnectTo), Domain];
			}

			return (Succeeded, ErrorMessage, Alternatives);
		}

		private void ReconnectTimer_Tick(object? _)
		{
			if (this.xmppClient is null)
				return;

			if (!ServiceRef.NetworkService.IsOnline)
				return;

			if (this.XmppStale())
			{
				this.xmppLastStateChange = DateTime.Now;

				if (!this.xmppClient.Disposed)
					this.xmppClient.Reconnect();
			}
		}

		#endregion

		#region Password

		/// <summary>
		/// Changes the password of the account.
		/// </summary>
		/// <param name="NewPassword">New password</param>
		/// <returns>If change was successful.</returns>
		public Task<bool> ChangePassword(string NewPassword)
		{
			TaskCompletionSource<bool> PasswordChanged = new();

			this.XmppClient.ChangePassword(NewPassword, (sender, e) =>
			{
				PasswordChanged.TrySetResult(e.Ok);
				return Task.CompletedTask;
			}, null);

			return PasswordChanged.Task;
		}

		#endregion

		#region Components & Services

		/// <summary>
		/// Performs a Service Discovery on a remote entity.
		/// </summary>
		/// <param name="FullJid">Full JID of entity.</param>
		/// <returns>Service Discovery response.</returns>
		public Task<ServiceDiscoveryEventArgs> SendServiceDiscoveryRequest(string FullJid)
		{
			TaskCompletionSource<ServiceDiscoveryEventArgs> Result = new();

			this.XmppClient.SendServiceDiscoveryRequest(FullJid, (_, e) =>
			{
				Result.TrySetResult(e);
				return Task.CompletedTask;
			}, null);

			return Result.Task;
		}

		/// <summary>
		/// Run this method to discover services for any given XMPP server.
		/// </summary>
		/// <param name="Client">The client to use. Can be <c>null</c>, in which case the default is used.</param>
		/// <returns>If TAG services were found.</returns>
		public async Task<bool> DiscoverServices(XmppClient? Client = null)
		{
			Client ??= this.xmppClient;

			if (Client is null)
				return false;

			ServiceItemsDiscoveryEventArgs response;

			try
			{
				response = await Client.ServiceItemsDiscoveryAsync(null, string.Empty, string.Empty);
			}
			catch (Exception ex)
			{
				if (this.sniffer is not null)
				{
					string CommsDump = await this.sniffer.SnifferToTextAsync();
					ServiceRef.LogService.LogException(ex, new KeyValuePair<string, object?>("Sniffer", CommsDump));
				}

				return false;
			}

			List<Task> Tasks = [];
			object SynchObject = new();

			Tasks.Add(CheckFeatures(Client, SynchObject));

			foreach (Item Item in response.Items)
				Tasks.Add(CheckComponent(Client, Item, SynchObject));

			await Task.WhenAll([.. Tasks]);

			if (string.IsNullOrWhiteSpace(ServiceRef.TagProfile.LegalJid))
				return false;

			if (string.IsNullOrWhiteSpace(ServiceRef.TagProfile.HttpFileUploadJid) || (ServiceRef.TagProfile.HttpFileUploadMaxSize <= 0))
				return false;

			if (string.IsNullOrWhiteSpace(ServiceRef.TagProfile.LogJid))
				return false;

			if (string.IsNullOrWhiteSpace(ServiceRef.TagProfile.EDalerJid))
				return false;

			if (string.IsNullOrWhiteSpace(ServiceRef.TagProfile.NeuroFeaturesJid))
				return false;

			if (!ServiceRef.TagProfile.SupportsPushNotification)
				return false;

			return true;
		}

		private static async Task CheckFeatures(XmppClient Client, object SynchObject)
		{
			ServiceDiscoveryEventArgs e = await Client.ServiceDiscoveryAsync(string.Empty);

			lock (SynchObject)
			{
				ServiceRef.TagProfile.SupportsPushNotification = e.HasFeature(PushNotificationClient.MessagePushNamespace);
			}
		}

		private static async Task CheckComponent(XmppClient Client, Item Item, object SynchObject)
		{
			ServiceDiscoveryEventArgs itemResponse = await Client.ServiceDiscoveryAsync(null, Item.JID, Item.Node);

			lock (SynchObject)
			{
				if (itemResponse.HasAnyFeature(ContractsClient.NamespacesLegalIdentities))
					ServiceRef.TagProfile.LegalJid = Item.JID;

				if (itemResponse.HasAnyFeature(ThingRegistryClient.NamespacesDiscovery))
					ServiceRef.TagProfile.RegistryJid = Item.JID;

				if (itemResponse.HasAnyFeature(ProvisioningClient.NamespacesProvisioningDevice) &&
					itemResponse.HasAnyFeature(ProvisioningClient.NamespacesProvisioningOwner) &&
					itemResponse.HasAnyFeature(ProvisioningClient.NamespacesProvisioningToken))
				{
					ServiceRef.TagProfile.ProvisioningJid = Item.JID;
				}

				if (itemResponse.HasFeature(HttpFileUploadClient.Namespace))
				{
					long maxSize = HttpFileUploadClient.FindMaxFileSize(Client, itemResponse) ?? 0;
					ServiceRef.TagProfile.SetFileUploadParameters(Item.JID, maxSize);
				}

				if (itemResponse.HasFeature(XmppEventSink.NamespaceEventLogging))
					ServiceRef.TagProfile.LogJid = Item.JID;

				if (itemResponse.HasFeature(XmppEventSink.NamespaceEventLogging))
					ServiceRef.TagProfile.LogJid = Item.JID;

				if (itemResponse.HasFeature(EDalerClient.NamespaceEDaler))
					ServiceRef.TagProfile.EDalerJid = Item.JID;

				if (itemResponse.HasFeature(NeuroFeaturesClient.NamespaceNeuroFeatures))
					ServiceRef.TagProfile.NeuroFeaturesJid = Item.JID;
			}
		}

		#endregion

		#region Transfer

		private async Task TransferIdDelivered(object? Sender, MessageEventArgs e)
		{
			if (e.From != Constants.Domains.OnboardingDomain)
			{
				return;
			}

			string Code = XML.Attribute(e.Content, "code");
			bool Deleted = XML.Attribute(e.Content, "deleted", false);

			if (!Deleted)
				return;

			string CodesGenerated = await RuntimeSettings.GetAsync(Constants.Settings.TransferIdCodeSent, string.Empty);
			string[] Codes = CodesGenerated.Split(CommonTypes.CRLF, StringSplitOptions.RemoveEmptyEntries);

			if (Array.IndexOf<string>(Codes, Code) < 0)
				return;

			await this.DestroyXmppClient();

			this.domainName = string.Empty;
			this.accountName = string.Empty;
			this.passwordHash = string.Empty;
			this.passwordHashMethod = string.Empty;
			this.xmppConnected = false;

			ServiceRef.TagProfile.ClearAll();
			if (App.Current is not null)
				await App.Current.ForceSaveAsync();
			await RuntimeSettings.SetAsync(Constants.Settings.TransferIdCodeSent, string.Empty);
			await Database.Provider.Flush();
			WeakReferenceMessenger.Default.Send(new RegistrationPageMessage(ServiceRef.TagProfile.Step));
			await App.SetRegistrationPageAsync();
		}

		/// <summary>
		/// Registers a Transfer ID Code
		/// </summary>
		/// <param name="Code">Transfer Code</param>
		public async Task AddTransferCode(string Code)
		{
			string CodesGenerated = await RuntimeSettings.GetAsync(Constants.Settings.TransferIdCodeSent, string.Empty);

			if (string.IsNullOrEmpty(CodesGenerated))
				CodesGenerated = Code;
			else
				CodesGenerated += "\r\n" + Code;

			await RuntimeSettings.SetAsync(Constants.Settings.TransferIdCodeSent, CodesGenerated);
			await Database.Provider.Flush();
		}

		#endregion

		#region Presence Subscriptions

		private async Task XmppClient_OnPresenceSubscribe(object? Sender, PresenceEventArgs e)
		{
			LegalIdentity? RemoteIdentity = null;
			string FriendlyName = string.IsNullOrWhiteSpace(e.NickName) ? e.FromBareJID : e.NickName;
			string? PhotoUrl = null;
			int PhotoWidth = 0;
			int PhotoHeight = 0;

			foreach (XmlNode N in e.Presence.ChildNodes)
			{
				if (N is XmlElement E && E.LocalName == "identity" && E.NamespaceURI == ContractsClient.NamespaceLegalIdentitiesCurrent)
				{
					RemoteIdentity = LegalIdentity.Parse(E);
					if (RemoteIdentity is not null)
					{
						FriendlyName = ContactInfo.GetFriendlyName(RemoteIdentity);

						IdentityStatus Status = await this.ContractsClient.ValidateAsync(RemoteIdentity);
						if (Status != IdentityStatus.Valid)
						{
							await e.Decline();

							Log.Warning("Invalid ID received. Presence subscription declined.", e.FromBareJID, RemoteIdentity.Id, "IdValidationError",
								new KeyValuePair<string, object?>("Recipient JID", this.BareJid),
								new KeyValuePair<string, object?>("Sender JID", e.FromBareJID),
								new KeyValuePair<string, object?>("Legal ID", RemoteIdentity.Id),
								new KeyValuePair<string, object?>("Validation", Status));
							return;
						}

						break;
					}
				}
			}

			ContactInfo Info = await ContactInfo.FindByBareJid(e.FromBareJID);
			if ((Info is not null) && Info.AllowSubscriptionFrom.HasValue)
			{
				if (Info.AllowSubscriptionFrom.Value)
					await e.Accept();
				else
					await e.Decline();

				if (Info.FriendlyName != FriendlyName || ((RemoteIdentity is not null) && Info.LegalId != RemoteIdentity.Id))
				{
					if (RemoteIdentity is not null)
					{
						Info.LegalId = RemoteIdentity.Id;
						Info.LegalIdentity = RemoteIdentity;
					}

					Info.FriendlyName = FriendlyName;
					await Database.Update(Info);
				}

				return;
			}

			if ((RemoteIdentity is not null) && (RemoteIdentity.Attachments is not null))
			{
				(PhotoUrl, PhotoWidth, PhotoHeight) = await PhotosLoader.LoadPhotoAsTemporaryFile(RemoteIdentity.Attachments,
					Constants.QrCode.DefaultImageWidth, Constants.QrCode.DefaultImageHeight);
			}

			SubscriptionRequestViewModel SubscriptionRequestViewModel = new(e.FromBareJID, FriendlyName, PhotoUrl, PhotoWidth, PhotoHeight);
			SubscriptionRequestPopup SubscriptionRequestPopup = new(SubscriptionRequestViewModel);

			await MopupService.Instance.PushAsync(SubscriptionRequestPopup);
			PresenceRequestAction Action = await SubscriptionRequestViewModel.Result;

			switch (Action)
			{
				case PresenceRequestAction.Accept:
					await e.Accept();

					if (Info is null)
					{
						Info = new ContactInfo()
						{
							AllowSubscriptionFrom = true,
							BareJid = e.FromBareJID,
							FriendlyName = string.IsNullOrWhiteSpace(e.NickName) ? e.FromBareJID : e.NickName,
							IsThing = false
						};

						await Database.Insert(Info);
					}
					else if (!Info.AllowSubscriptionFrom.HasValue || !Info.AllowSubscriptionFrom.Value)
					{
						Info.AllowSubscriptionFrom = true;
						await Database.Update(Info);
					}

					RosterItem? Item = this.XmppClient[e.FromBareJID];

					if (Item is null || (Item.State != SubscriptionState.Both && Item.State != SubscriptionState.To))
					{
						SubscribeToViewModel SubscribeToViewModel = new(e.FromBareJID);
						SubscribeToPopup SubscribeToPopup = new(SubscribeToViewModel);

						await MopupService.Instance.PushAsync(SubscribeToPopup);
						bool? SubscribeTo = await SubscribeToViewModel.Result;

						if (SubscribeTo.HasValue && SubscribeTo.Value)
						{
							string IdXml;

							if (ServiceRef.TagProfile.LegalIdentity is null)
								IdXml = string.Empty;
							else
							{
								StringBuilder Xml = new();
								ServiceRef.TagProfile.LegalIdentity.Serialize(Xml, true, true, true, true, true, true, true);
								IdXml = Xml.ToString();
							}

							await e.Client.RequestPresenceSubscription(e.FromBareJID, IdXml);
						}
					}
					break;

				case PresenceRequestAction.Reject:
					await e.Decline();

					if (this.abuseClient is null)
						break;

					ReportOrBlockViewModel ReportOrBlockViewModel = new(e.FromBareJID);
					ReportOrBlockPopup ReportOrBlockPopup = new(ReportOrBlockViewModel);

					await MopupService.Instance.PushAsync(ReportOrBlockPopup);
					ReportOrBlockAction ReportOrBlock = await ReportOrBlockViewModel.Result;

					if (ReportOrBlock == ReportOrBlockAction.Block || ReportOrBlock == ReportOrBlockAction.Report)
					{
						if (Info is null)
						{
							Info = new ContactInfo()
							{
								AllowSubscriptionFrom = false,
								BareJid = e.FromBareJID,
								FriendlyName = string.IsNullOrWhiteSpace(e.NickName) ? e.FromBareJID : e.NickName,
								IsThing = false
							};

							await Database.Insert(Info);
						}
						else if (!Info.AllowSubscriptionFrom.HasValue || Info.AllowSubscriptionFrom.Value)
						{
							Info.AllowSubscriptionFrom = false;
							await Database.Update(Info);
						}

						if (ReportOrBlock == ReportOrBlockAction.Report)
						{
							ReportTypeViewModel ReportTypeViewModel = new(e.FromBareJID);
							ReportTypePopup ReportTypePopup = new(ReportTypeViewModel);

							await MopupService.Instance.PushAsync(ReportOrBlockPopup);
							ReportingReason? ReportType = await ReportTypeViewModel.Result;

							if (ReportType.HasValue)
							{
								TaskCompletionSource<bool> Result = new();

								await this.abuseClient.BlockJID(e.FromBareJID, ReportType.Value, (sender2, e2) =>
								{
									Result.TrySetResult(e.Ok);
									return Task.CompletedTask;
								}, null);

								await Result.Task;
							}
						}
					}
					break;

				case PresenceRequestAction.Ignore:
				default:
					break;
			}
		}

		private async Task XmppClient_OnPresenceUnsubscribed(object? Sender, PresenceEventArgs e)
		{
			ContactInfo ContactInfo = await ContactInfo.FindByBareJid(e.FromBareJID);
			if ((ContactInfo is not null) && ContactInfo.AllowSubscriptionFrom.HasValue && ContactInfo.AllowSubscriptionFrom.Value)
			{
				ContactInfo.AllowSubscriptionFrom = null;
				await Database.Update(ContactInfo);
			}
		}

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
		public Task<XmlElement> IqSetAsync(string To, string Xml)
		{
			return this.XmppClient.IqSetAsync(To, Xml);
		}

		/// <summary>
		/// Reference to XMPP Client. If no such client is available, an exception is thrown.
		/// </summary>
		private XmppClient XmppClient
		{
			get
			{
				if (this.xmppClient is null)
					throw new Exception("Not connected to XMPP network.");

				return this.xmppClient;
			}
		}

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
		public void SendMessage(QoSLevel QoS, Waher.Networking.XMPP.MessageType Type, string Id, string To, string CustomXml, string Body,
			string Subject, string Language, string ThreadId, string ParentThreadId, EventHandlerAsync<DeliveryEventArgs>? DeliveryCallback, object? State)
		{
			this.ContractsClient.LocalE2eEndpoint.SendMessage(this.XmppClient, E2ETransmission.NormalIfNotE2E,
				QoS, Type, Id, To, CustomXml, Body, Subject, Language, ThreadId, ParentThreadId, DeliveryCallback, State);
		}

		private Task XmppClient_OnNormalMessage(object? Sender, MessageEventArgs e)
		{
			Log.Warning("Unhandled message received.", e.To, e.From,
				new KeyValuePair<string, object?>("Stanza", e.Message.OuterXml));

			return Task.CompletedTask;
		}

		private async Task XmppClient_OnChatMessage(object? Sender, MessageEventArgs e)
		{
			string RemoteBareJid = e.FromBareJID;

			foreach (XmlNode N in e.Message.ChildNodes)
			{
				if (N is XmlElement E &&
					E.LocalName == "qlRef" &&
					E.NamespaceURI == XmppClient.NamespaceQuickLogin &&
					RemoteBareJid.IndexOf('@') < 0 &&
					RemoteBareJid.IndexOf('/') < 0)
				{
					LegalIdentity? RemoteIdentity = null;

					foreach (XmlNode N2 in E.ChildNodes)
					{
						if (N2 is XmlElement E2 &&
							E2.LocalName == "identity" &&
							E2.NamespaceURI == ContractsClient.NamespaceLegalIdentitiesCurrent)
						{
							RemoteIdentity = LegalIdentity.Parse(E2);
							break;
						}
					}

					if (RemoteIdentity is not null)
					{
						IdentityStatus Status = await this.ValidateIdentity(RemoteIdentity);
						if (Status != IdentityStatus.Valid)
						{
							Log.Warning("Message rejected because the embedded legal identity was not valid.",
								new KeyValuePair<string, object?>("Identity", RemoteIdentity.Id),
								new KeyValuePair<string, object?>("From", RemoteBareJid),
								new KeyValuePair<string, object?>("Status", Status));
							return;
						}

						string Jid = RemoteIdentity["JID"];

						if (string.IsNullOrEmpty(Jid))
						{
							Log.Warning("Message rejected because the embedded legal identity lacked JID.",
								new KeyValuePair<string, object?>("Identity", RemoteIdentity.Id),
								new KeyValuePair<string, object?>("From", RemoteBareJid),
								new KeyValuePair<string, object?>("Status", Status));
							return;
						}

						if (!string.Equals(XML.Attribute(E, "bareJid", string.Empty), Jid, StringComparison.OrdinalIgnoreCase))
						{
							Log.Warning("Message rejected because the embedded legal identity had a different JID compared to the JID of the quick-login reference.",
								new KeyValuePair<string, object?>("Identity", RemoteIdentity.Id),
								new KeyValuePair<string, object?>("From", RemoteBareJid),
								new KeyValuePair<string, object?>("Status", Status));
							return;
						}

						RemoteBareJid = Jid;
					}
				}
			}

			ContactInfo ContactInfo = await ContactInfo.FindByBareJid(RemoteBareJid);
			string FriendlyName = ContactInfo?.FriendlyName ?? RemoteBareJid;
			string? ReplaceObjectId = null;

			ChatMessage Message = new()
			{
				Created = DateTime.UtcNow,
				RemoteBareJid = RemoteBareJid,
				RemoteObjectId = e.Id,
				MessageType = NeuroAccessMaui.UI.Pages.Contacts.Chat.MessageType.Received,
				Html = string.Empty,
				PlainText = e.Body,
				Markdown = string.Empty
			};

			foreach (XmlNode N in e.Message.ChildNodes)
			{
				if (N is XmlElement E)
				{
					switch (N.LocalName)
					{
						case "content":
							if (E.NamespaceURI == "urn:xmpp:content")
							{
								string Type = XML.Attribute(E, "type");

								switch (Type)
								{
									case "text/markdown":
										Message.Markdown = E.InnerText;
										break;

									case "text/plain":
										Message.PlainText = E.InnerText;
										break;

									case "text/html":
										Message.Html = E.InnerText;
										break;
								}
							}
							break;

						case "html":
							if (E.NamespaceURI == "http://jabber.org/protocol/xhtml-im")
							{
								string Html = E.InnerXml;

								int i = Html.IndexOf("<body", StringComparison.OrdinalIgnoreCase);
								if (i >= 0)
								{
									i = Html.IndexOf('>', i + 5);
									if (i >= 0)
										Html = Html[(i + 1)..].TrimStart();

									i = Html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
									if (i >= 0)
										Html = Html[..i].TrimEnd();
								}

								Message.Html = Html;
							}
							break;

						case "replace":
							if (E.NamespaceURI == "urn:xmpp:message-correct:0")
								ReplaceObjectId = XML.Attribute(E, "id");
							break;

						case "delay":
							if (E.NamespaceURI == PubSubClient.NamespaceDelayedDelivery &&
								E.HasAttribute("stamp") &&
								XML.TryParse(E.GetAttribute("stamp"), out DateTime Timestamp2))
							{
								Message.Created = Timestamp2.ToUniversalTime();
							}
							break;
					}
				}
			}

			if (!string.IsNullOrEmpty(Message.Markdown))
			{
				try
				{
					MarkdownSettings Settings = new()
					{
						AllowScriptTag = false,
						EmbedEmojis = false,    // TODO: Emojis
						AudioAutoplay = false,
						AudioControls = false,
						ParseMetaData = false,
						VideoAutoplay = false,
						VideoControls = false
					};

					MarkdownDocument Doc = await MarkdownDocument.CreateAsync(Message.Markdown, Settings);

					if (string.IsNullOrEmpty(Message.PlainText))
						Message.PlainText = (await Doc.GeneratePlainText()).Trim();

					if (string.IsNullOrEmpty(Message.Html))
						Message.Html = HtmlDocument.GetBody(await Doc.GenerateHTML());
				}
				catch (Exception ex)
				{
					ServiceRef.LogService.LogException(ex);
					Message.Markdown = string.Empty;
				}
			}

			if (string.IsNullOrEmpty(ReplaceObjectId))
				await Database.Insert(Message);
			else
			{
				ChatMessage Old = await Database.FindFirstIgnoreRest<ChatMessage>(new FilterAnd(
					new FilterFieldEqualTo("RemoteBareJid", RemoteBareJid),
					new FilterFieldEqualTo("RemoteObjectId", ReplaceObjectId)));

				if (Old is null)
				{
					ReplaceObjectId = null;
					await Database.Insert(Message);
				}
				else
				{
					Old.Updated = Message.Created;
					Old.Html = Message.Html;
					Old.PlainText = Message.PlainText;
					Old.Markdown = Message.Markdown;

					await Database.Update(Old);

					Message = Old;
				}
			}

			MainThread.BeginInvokeOnMainThread(async () =>
			{
				if (ServiceRef.UiService.CurrentPage is ChatPage &&
					ServiceRef.UiService.CurrentPage.BindingContext is ChatViewModel ChatViewModel &&
					string.Equals(ChatViewModel.BareJid, RemoteBareJid, StringComparison.OrdinalIgnoreCase))
				{
					if (string.IsNullOrEmpty(ReplaceObjectId))
						await ChatViewModel.MessageAddedAsync(Message);
					else
						await ChatViewModel.MessageUpdatedAsync(Message);
				}
				else
				{
					await ServiceRef.NotificationService.NewEvent(new ChatMessageNotificationEvent(e)
					{
						ReplaceObjectId = ReplaceObjectId,
						BareJid = RemoteBareJid,
						Category = RemoteBareJid
					});
				}
			});
		}

		private Task ClientMessage(object? Sender, MessageEventArgs e)
		{
			string Code = XML.Attribute(e.Content, "code");
			string Type = XML.Attribute(e.Content, "type");
			string Message = e.Body;

			if (!string.IsNullOrEmpty(Code))
			{
				try
				{
					string LocalizedMessage = ServiceRef.Localizer["ClientMessage" + Code];

					if (!string.IsNullOrEmpty(LocalizedMessage))
						Message = LocalizedMessage;
				}
				catch (Exception)
				{
					// Ignore
				}
			}

			MainThread.BeginInvokeOnMainThread(async () =>
			{
				switch (Type.ToUpperInvariant())
				{
					case "NONE":
					default:
						await ServiceRef.UiService.DisplayAlert(
							ServiceRef.Localizer[nameof(AppResources.Information)], Message,
							ServiceRef.Localizer[nameof(AppResources.Ok)]);
						break;

					case "CLIENT":
					case "SERVER":
					case "SERVICE":
						await ServiceRef.UiService.DisplayAlert(
							ServiceRef.Localizer[nameof(AppResources.ErrorTitle)], Message,
							ServiceRef.Localizer[nameof(AppResources.Ok)]);
						break;

				}
			});

			return Task.CompletedTask;
		}

		#endregion

		#region Presence

		private async Task XmppClient_OnPresence(object? Sender, PresenceEventArgs e)
		{
			await this.OnPresence.Raise(this, e);
		}

		/// <summary>
		/// Event raised when a new presence stanza has been received.
		/// </summary>
		public event EventHandlerAsync<PresenceEventArgs>? OnPresence;

		/// <summary>
		/// Requests subscription of presence information from a contact.
		/// </summary>
		/// <param name="BareJid">Bare JID of contact.</param>
		public void RequestPresenceSubscription(string BareJid)
		{
			this.XmppClient.RequestPresenceSubscription(BareJid);
		}

		/// <summary>
		/// Requests subscription of presence information from a contact.
		/// </summary>
		/// <param name="BareJid">Bare JID of contact.</param>
		/// <param name="CustomXml">Custom XML to include in the subscription request.</param>
		public void RequestPresenceSubscription(string BareJid, string CustomXml)
		{
			this.XmppClient.RequestPresenceSubscription(BareJid, CustomXml);
		}

		/// <summary>
		/// Requests unssubscription of presence information from a contact.
		/// </summary>
		/// <param name="BareJid">Bare JID of contact.</param>
		public void RequestPresenceUnsubscription(string BareJid)
		{
			this.XmppClient.RequestPresenceUnsubscription(BareJid);
		}

		/// <summary>
		/// Requests a previous presence subscription request revoked.
		/// </summary>
		/// <param name="BareJid">Bare JID of contact.</param>
		public void RequestRevokePresenceSubscription(string BareJid)
		{
			this.XmppClient.RequestRevokePresenceSubscription(BareJid);
		}

		#endregion

		#region Roster

		/// <summary>
		/// Items in the roster.
		/// </summary>
		public RosterItem[] Roster => this.xmppClient?.Roster ?? [];

		/// <summary>
		/// Gets a roster item.
		/// </summary>
		/// <param name="BareJid">Bare JID of roster item.</param>
		/// <returns>Roster item, if found, or null, if not available.</returns>
		public RosterItem? GetRosterItem(string BareJid)
		{
			return this.XmppClient?.GetRosterItem(BareJid);
		}

		/// <summary>
		/// Adds an item to the roster. If an item with the same Bare JID is found in the roster, that item is updated.
		/// </summary>
		/// <param name="Item">Item to add.</param>
		public void AddRosterItem(RosterItem Item)
		{
			this.XmppClient.AddRosterItem(Item);
		}

		/// <summary>
		/// Removes an item from the roster.
		/// </summary>
		/// <param name="BareJid">Bare JID of the roster item.</param>
		public void RemoveRosterItem(string BareJid)
		{
			this.XmppClient.RemoveRosterItem(BareJid);
		}

		private async Task XmppClient_OnRosterItemAdded(object? Sender, RosterItem Item)
		{
			await this.OnRosterItemAdded.Raise(this, Item);
		}

		/// <summary>
		/// Event raised when a roster item has been added to the roster.
		/// </summary>
		public event EventHandlerAsync<RosterItem>? OnRosterItemAdded;

		private async Task XmppClient_OnRosterItemUpdated(object? Sender, RosterItem Item)
		{
			await this.OnRosterItemUpdated.Raise(this, Item);
		}

		/// <summary>
		/// Event raised when a roster item has been updated in the roster.
		/// </summary>
		public event EventHandlerAsync<RosterItem>? OnRosterItemUpdated;

		private async Task XmppClient_OnRosterItemRemoved(object? Sender, RosterItem Item)
		{
			await this.OnRosterItemRemoved.Raise(this, Item);
		}

		/// <summary>
		/// Event raised when a roster item has been removed from the roster.
		/// </summary>
		public event EventHandlerAsync<RosterItem>? OnRosterItemRemoved;

		#endregion

		#region Push Notification

		/// <summary>
		/// If push notification is supported.
		/// </summary>
		public bool SupportsPushNotification => this.pushNotificationClient is not null;


		/// <summary>
		/// Reference to the Push-notification client.
		/// </summary>
		private PushNotificationClient PushNotificationClient
		{
			get
			{
				if (this.pushNotificationClient is null)
					throw new InvalidOperationException(ServiceRef.Localizer[nameof(AppResources.PushNotificationServiceNotFound)]);

				return this.pushNotificationClient;
			}
		}

		/// <summary>
		/// Registers a new token.
		/// </summary>
		/// <param name="TokenInformation">Token information.</param>
		/// <returns>If token could be registered.</returns>
		public async Task<bool> NewPushNotificationToken(TokenInformation TokenInformation)
		{
			// TODO: Check if started

			if (this.pushNotificationClient is null || !this.IsOnline || string.IsNullOrEmpty(TokenInformation.Token))
				return false;
			else
			{
				await this.ReportNewPushNotificationToken(TokenInformation.Token, TokenInformation.Service, TokenInformation.ClientType);

				return true;
			}
		}

		/// <summary>
		/// Reports a new push-notification token to the broker.
		/// </summary>
		/// <param name="Token">Token</param>
		/// <param name="Service">Service used</param>
		/// <param name="ClientType">Client type.</param>
		public Task ReportNewPushNotificationToken(string Token, PushMessagingService Service, ClientType ClientType)
		{
			return this.PushNotificationClient.NewTokenAsync(Token, Service, ClientType);
		}

		/// <summary>
		/// Clears configured push notification rules in the broker.
		/// </summary>
		public Task ClearPushNotificationRules()
		{
			return this.PushNotificationClient.ClearRulesAsync();
		}

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
		public Task AddPushNotificationRule(Waher.Networking.XMPP.MessageType MessageType, string LocalName, string Namespace,
			string Channel, string MessageVariable, string PatternMatchingScript, string ContentScript)
		{
			return this.PushNotificationClient.AddRuleAsync(MessageType, LocalName, Namespace, Channel, MessageVariable,
				PatternMatchingScript, ContentScript);
		}

		#endregion

		#region Tokens

		/// <summary>
		/// Gets a token for use with APIs that are either distributed or use different
		/// protocols, when the client needs to authenticate itself using the current
		/// XMPP connection.
		/// </summary>
		/// <param name="Seconds">Number of seconds for which the token should be valid.</param>
		/// <returns>Token, if able to get a token, or null otherwise.</returns>
		public async Task<string?> GetApiToken(int Seconds)
		{
			DateTime Now = DateTime.UtcNow;

			if (!string.IsNullOrEmpty(this.token) && Now.Subtract(this.tokenCreated).TotalSeconds < Seconds - 10)
				return this.token;

			if (!this.IsOnline)
			{
				if (!await this.WaitForConnectedState(TimeSpan.FromSeconds(20)))
					return this.token;
			}

			if (this.httpxClient is null)
				throw new Exception("Not connected to XMPP network.");

			this.token = await this.httpxClient.GetJwtTokenAsync(Seconds);
			this.tokenCreated = Now;

			return this.token;
		}

		/// <summary>
		/// Performs an HTTP POST to a protected API on the server, over the current XMPP connection,
		/// authenticating the client using the credentials already provided over XMPP.
		/// </summary>
		/// <param name="LocalResource">Local Resource on the server to POST to.</param>
		/// <param name="Data">Data to post. This will be encoded using encoders in the type inventory.</param>
		/// <param name="Headers">Headers to provide in the POST.</param>
		/// <returns>Decoded response from the resource.</returns>
		/// <exception cref="Exception">Any communication error will be handle by raising the corresponding exception.</exception>
		public async Task<object> PostToProtectedApi(string LocalResource, object Data, params KeyValuePair<string, string>[] Headers)
		{
			StringBuilder Url = new();

			if (this.IsOnline)
				Url.Append("httpx://");
			else if (!string.IsNullOrEmpty(this.token))     // Token needs to be retrieved regularly when connected, if protected APIs are to be used when disconnected or during connection.
			{
				Url.Append("https://");

				KeyValuePair<string, string> Authorization = new("Authorization", "Bearer " + this.token);

				if (Headers is null)
					Headers = [Authorization];
				else
				{
					int c = Headers.Length;

					Array.Resize(ref Headers, c + 1);
					Headers[c] = Authorization;
				}
			}
			else
				throw new IOException("No connection and no token available for call to protect API.");

			Url.Append(ServiceRef.TagProfile.Domain);
			Url.Append(LocalResource);

			ContentResponse Response = await InternetContent.PostAsync(new Uri(Url.ToString()), Data, Headers);
			Response.AssertOk();

			return Response.Decoded;
		}

		#endregion

		#region HTTP File Upload

		/// <summary>
		/// Reference to HTTP File Upload client, with a check that one is created.
		/// Note: Do not make public. Reference only from inside the XmppService class.
		/// </summary>
		private HttpFileUploadClient FileUploadClient
		{
			get
			{
				if (this.fileUploadClient is null)
					throw new InvalidOperationException(ServiceRef.Localizer[nameof(AppResources.FileUploadServiceNotFound)]);

				return this.fileUploadClient;
			}
		}

		/// <summary>
		/// Returns <c>true</c> if file upload is supported, <c>false</c> otherwise.
		/// </summary>
		public bool FileUploadIsSupported
		{
			get
			{
				try
				{
					return ServiceRef.TagProfile.FileUploadIsSupported &&
						this.fileUploadClient is not null &&
						this.fileUploadClient.HasSupport;
				}
				catch (Exception ex)
				{
					ServiceRef.LogService.LogException(ex);
					return false;
				}
			}
		}

		/// <summary>
		/// Uploads a file to the upload component.
		/// </summary>
		/// <param name="FileName">Name of file.</param>
		/// <param name="ContentType">Internet content type.</param>
		/// <param name="ContentSize">Size of content.</param>
		public Task<HttpFileUploadEventArgs> RequestUploadSlotAsync(string FileName, string ContentType, long ContentSize)
		{
			return this.FileUploadClient.RequestUploadSlotAsync(FileName, ContentType, ContentSize);
		}


		#endregion

		#region Personal Eventing Protocol (PEP)

		private readonly LinkedList<KeyValuePair<Type, EventHandlerAsync<PersonalEventNotificationEventArgs>>> pepHandlers = new();

		/// <summary>
		/// Reference to Personal Eventing Protocol (PEP) client, with a check that one is created.
		/// Note: Do not make public. Reference only from inside the XmppService class.
		/// </summary>
		private PepClient PepClient
		{
			get
			{
				if (this.pepClient is null)
					throw new InvalidOperationException(ServiceRef.Localizer[nameof(AppResources.PepServiceNotFound)]);

				return this.pepClient;
			}
		}

		/// <summary>
		/// Registers an event handler of a specific type of personal events.
		/// </summary>
		/// <param name="PersonalEventType">Type of personal event.</param>
		/// <param name="Handler">Event handler.</param>
		public void RegisterPepHandler(Type PersonalEventType, EventHandlerAsync<PersonalEventNotificationEventArgs> Handler)
		{
			lock (this.pepHandlers)
			{
				this.pepHandlers.AddLast(new KeyValuePair<Type, EventHandlerAsync<PersonalEventNotificationEventArgs>>(PersonalEventType, Handler));
			}

			this.PepClient.RegisterHandler(PersonalEventType, Handler);
		}

		/// <summary>
		/// Unregisters an event handler of a specific type of personal events.
		/// </summary>
		/// <param name="PersonalEventType">Type of personal event.</param>
		/// <param name="Handler">Event handler.</param>
		/// <returns>If the event handler was found and removed.</returns>
		public bool UnregisterPepHandler(Type PersonalEventType, EventHandlerAsync<PersonalEventNotificationEventArgs> Handler)
		{
			lock (this.pepHandlers)
			{
				LinkedListNode<KeyValuePair<Type, EventHandlerAsync<PersonalEventNotificationEventArgs>>>? Node = this.pepHandlers.First;

				while (Node is not null)
				{
					if (Node.Value.Key == PersonalEventType &&
						(Node.Value.Value.Target?.Equals(Handler.Target) ?? Handler.Target is null) &&
						Node.Value.Value.Method.Equals(Handler.Method))
					{
						this.pepHandlers.Remove(Node);
						break;
					}

					Node = Node.Next;
				}
			}

			return this.PepClient.UnregisterHandler(PersonalEventType, Handler);
		}

		private void ReregisterPepEventHandlers(PepClient PepClient)
		{
			lock (this.pepHandlers)
			{
				foreach (KeyValuePair<Type, EventHandlerAsync<PersonalEventNotificationEventArgs>> P in this.pepHandlers)
					PepClient.RegisterHandler(P.Key, P.Value);
			}
		}

		#endregion

		#region Thing Registries & Discovery

		/// <summary>
		/// Reference to thing registry client, with a check that one is created.
		/// Note: Do not make public. Reference only from inside the XmppService class.
		/// </summary>
		private ThingRegistryClient ThingRegistryClient
		{
			get
			{
				if (this.thingRegistryClient is null)
					throw new InvalidOperationException(ServiceRef.Localizer[nameof(AppResources.ThingRegistryServiceNotFound)]);

				return this.thingRegistryClient;
			}
		}

		/// <summary>
		/// JID of thing registry service.
		/// </summary>
		public string RegistryServiceJid => this.ThingRegistryClient.ThingRegistryAddress;

		/// <summary>
		/// Checks if a URI is a claim URI.
		/// </summary>
		/// <param name="DiscoUri">IoTDisco URI</param>
		/// <returns>If <paramref name="DiscoUri"/> is a claim URI.</returns>
		public bool IsIoTDiscoClaimURI(string DiscoUri)
		{
			return ThingRegistryClient.IsIoTDiscoClaimURI(DiscoUri);
		}

		/// <summary>
		/// Checks if a URI is a search URI.
		/// </summary>
		/// <param name="DiscoUri">IoTDisco URI</param>
		/// <returns>If <paramref name="DiscoUri"/> is a search URI.</returns>
		public bool IsIoTDiscoSearchURI(string DiscoUri)
		{
			return ThingRegistryClient.IsIoTDiscoSearchURI(DiscoUri);
		}

		/// <summary>
		/// Checks if a URI is a direct reference URI.
		/// </summary>
		/// <param name="DiscoUri">IoTDisco URI</param>
		/// <returns>If <paramref name="DiscoUri"/> is a direct reference URI.</returns>
		public bool IsIoTDiscoDirectURI(string DiscoUri)
		{
			return ThingRegistryClient.IsIoTDiscoDirectURI(DiscoUri);
		}

		/// <summary>
		/// Tries to decode an IoTDisco Claim URI (subset of all possible IoTDisco URIs).
		/// </summary>
		/// <param name="DiscoUri">IoTDisco URI</param>
		/// <param name="Tags">Decoded meta data tags.</param>
		/// <returns>If DiscoUri was successfully decoded.</returns>
		public bool TryDecodeIoTDiscoClaimURI(string DiscoUri, [NotNullWhen(true)] out MetaDataTag[]? Tags)
		{
			return ThingRegistryClient.TryDecodeIoTDiscoClaimURI(DiscoUri, out Tags);
		}

		/// <summary>
		/// Tries to decode an IoTDisco Search URI (subset of all possible IoTDisco URIs).
		/// </summary>
		/// <param name="DiscoUri">IoTDisco URI</param>
		/// <param name="Operators">Search operators.</param>
		/// <param name="RegistryJid">Registry Service JID</param>
		/// <returns>If the URI could be parsed.</returns>
		public bool TryDecodeIoTDiscoSearchURI(string DiscoUri, [NotNullWhen(true)] out SearchOperator[]? Operators,
			out string? RegistryJid)
		{
			RegistryJid = null;
			Operators = null;
			if (!ThingRegistryClient.TryDecodeIoTDiscoURI(DiscoUri, out IEnumerable<SearchOperator> Operators2))
				return false;

			List<SearchOperator> List = [];

			foreach (SearchOperator Operator in Operators2)
			{
				if (Operator.Name.Equals("R", StringComparison.OrdinalIgnoreCase))
				{
					if (!string.IsNullOrEmpty(RegistryJid))
						return false;

					if (Operator is not StringTagEqualTo StrEqOp)
						return false;

					RegistryJid = StrEqOp.Value;
				}
				else
					List.Add(Operator);
			}

			Operators = [.. List];

			return true;
		}

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
		public bool TryDecodeIoTDiscoDirectURI(string DiscoUri, [NotNullWhen(true)] out string? Jid, out string? SourceId, out string? NodeId,
			out string? PartitionId, [NotNullWhen(true)] out MetaDataTag[]? Tags)
		{
			Jid = null;
			SourceId = null;
			NodeId = null;
			PartitionId = null;

			if (!ThingRegistryClient.TryDecodeIoTDiscoURI(DiscoUri, out IEnumerable<SearchOperator> Operators2))
			{
				Tags = null;
				return false;
			}

			List<MetaDataTag> TagsFound = [];

			foreach (SearchOperator Operator in Operators2)
			{
				if (Operator is StringTagEqualTo S)
				{
					switch (S.Name.ToUpper(CultureInfo.InvariantCulture))
					{
						case Constants.XmppProperties.Jid:
							Jid = S.Value;
							break;

						case Constants.XmppProperties.SourceId:
							SourceId = S.Value;
							break;

						case Constants.XmppProperties.NodeId:
							NodeId = S.Value;
							break;

						case Constants.XmppProperties.Partition:
							PartitionId = S.Value;
							break;

						default:
							TagsFound.Add(new MetaDataStringTag(S.Name, S.Value));
							break;
					}
				}
				else if (Operator is NumericTagEqualTo N)
					TagsFound.Add(new MetaDataNumericTag(N.Name, N.Value));
				else
				{
					Tags = null;
					return false;
				}
			}

			Tags = [.. TagsFound];

			return !string.IsNullOrEmpty(Jid);
		}

		/// <summary>
		/// Claims a think in accordance with parameters defined in a iotdisco claim URI.
		/// </summary>
		/// <param name="DiscoUri">IoTDisco URI</param>
		/// <param name="MakePublic">If the device should be public in the thing registry.</param>
		/// <returns>Information about the thing, or error if unable.</returns>
		public Task<NodeResultEventArgs> ClaimThing(string DiscoUri, bool MakePublic)
		{
			if (!this.TryDecodeIoTDiscoClaimURI(DiscoUri, out MetaDataTag[]? Tags))
				throw new ArgumentException(ServiceRef.Localizer[nameof(AppResources.InvalidIoTDiscoClaimUri)], nameof(DiscoUri));

			TaskCompletionSource<NodeResultEventArgs> Result = new();

			this.ThingRegistryClient.Mine(MakePublic, Tags, (sender, e) =>
			{
				Result.TrySetResult(e);
				return Task.CompletedTask;
			}, null);

			return Result.Task;
		}

		/// <summary>
		/// Disowns a thing
		/// </summary>
		/// <param name="RegistryJid">Registry JID</param>
		/// <param name="ThingJid">Thing JID</param>
		/// <param name="SourceId">Source ID</param>
		/// <param name="Partition">Partition</param>
		/// <param name="NodeId">Node ID</param>
		/// <returns>If the thing was disowned</returns>
		public Task<bool> Disown(string RegistryJid, string ThingJid, string SourceId, string Partition, string NodeId)
		{
			TaskCompletionSource<bool> Result = new();

			this.ThingRegistryClient.Disown(RegistryJid, ThingJid, NodeId, SourceId, Partition, (sender, e) =>
			{
				Result.TrySetResult(e.Ok);
				return Task.CompletedTask;
			}, null);

			return Result.Task;
		}

		/// <summary>
		/// Searches for devices in accordance with settings in a iotdisco-URI.
		/// </summary>
		/// <param name="Offset">Start offset of list</param>
		/// <param name="MaxCount">Maximum number of items in response.</param>
		/// <param name="DiscoUri">iotdisco URI.</param>
		/// <returns>Devices found, Registry JID, and if more devices are available.</returns>
		public async Task<(SearchResultThing[], string?, bool)> Search(int Offset, int MaxCount, string DiscoUri)
		{
			if (!this.TryDecodeIoTDiscoSearchURI(DiscoUri, out SearchOperator[]? Operators, out string? RegistryJid))
				return (Array.Empty<SearchResultThing>(), RegistryJid, false);

			(SearchResultThing[] Things, bool More) = await this.Search(Offset, MaxCount, RegistryJid, Operators);

			return (Things, RegistryJid, More);
		}

		/// <summary>
		/// Searches for devices in accordance with settings in a iotdisco-URI.
		/// </summary>
		/// <param name="Offset">Start offset of list</param>
		/// <param name="MaxCount">Maximum number of items in response.</param>
		/// <param name="RegistryJid">Registry Service JID</param>
		/// <param name="Operators">Search operators.</param>
		/// <returns>Devices found, and if more devices are available.</returns>
		public Task<(SearchResultThing[], bool)> Search(int Offset, int MaxCount, string? RegistryJid, params SearchOperator[] Operators)
		{
			TaskCompletionSource<(SearchResultThing[], bool)> Result = new();

			this.ThingRegistryClient.Search(RegistryJid ?? ServiceRef.TagProfile.RegistryJid, Offset, MaxCount, Operators, (sender, e) =>
			{
				if (e.Ok)
					Result.TrySetResult((e.Things, e.More));
				else
					Result.TrySetException(e.StanzaError ?? new Exception("Unable to perform search."));

				return Task.CompletedTask;
			}, null);

			return Result.Task;
		}

		/// <summary>
		/// Searches for all devices in accordance with settings in a iotdisco-URI.
		/// </summary>
		/// <param name="DiscoUri">iotdisco URI.</param>
		/// <returns>Complete list of devices in registry matching the search operators, and the JID of the registry service.</returns>
		public async Task<(SearchResultThing[], string?)> SearchAll(string DiscoUri)
		{
			if (!this.TryDecodeIoTDiscoSearchURI(DiscoUri, out SearchOperator[]? Operators, out string? RegistryJid))
				return (Array.Empty<SearchResultThing>(), RegistryJid);

			SearchResultThing[] Things = await this.SearchAll(RegistryJid ?? ServiceRef.TagProfile.RegistryJid, Operators);

			return (Things, RegistryJid);
		}

		/// <summary>
		/// Searches for all devices in accordance with settings in a iotdisco-URI.
		/// </summary>
		/// <param name="RegistryJid">Registry Service JID</param>
		/// <param name="Operators">Search operators.</param>
		/// <returns>Complete list of devices in registry matching the search operators.</returns>
		public async Task<SearchResultThing[]> SearchAll(string? RegistryJid, params SearchOperator[] Operators)
		{
			(SearchResultThing[] Things, bool More) = await this.Search(0, Constants.BatchSizes.DeviceBatchSize, RegistryJid, Operators);
			if (!More)
				return Things;

			List<SearchResultThing> Result = [];
			int Offset = Things.Length;

			Result.AddRange(Things);

			while (More)
			{
				(Things, More) = await this.Search(Offset, Constants.BatchSizes.DeviceBatchSize, RegistryJid, Operators);
				Result.AddRange(Things);
				Offset += Things.Length;
			}

			return [.. Result];
		}

		#endregion

		#region Legal Identities

		/// <summary>
		/// Generates new keys
		/// </summary>
		public async Task GenerateNewKeys()
		{
			await this.ContractsClient.GenerateNewKeys();

			if (this.ContractsClient.Client.State == XmppState.Connected)
				await this.ContractsClient.Client.SetPresenceAsync(Availability.Online);
		}

		/// <summary>
		/// Gets important attributes for a successful ID Application.
		/// </summary>
		/// <returns>ID Application attributes.</returns>
		public async Task<IdApplicationAttributesEventArgs> GetIdApplicationAttributes()
		{
			return await this.ContractsClient.GetIdApplicationAttributesAsync();
		}

		/// <summary>
		/// Adds a legal identity.
		/// </summary>
		/// <param name="Model">The model holding all the values needed.</param>
		/// <param name="GenerateNewKeys">If new keys should be generated.</param>
		/// <param name="Attachments">The physical attachments to upload.</param>
		/// <returns>Legal Identity</returns>
		public async Task<LegalIdentity> AddLegalIdentity(RegisterIdentityModel Model, bool GenerateNewKeys,
			params LegalIdentityAttachment[] Attachments)
		{
			if (GenerateNewKeys)
				await this.GenerateNewKeys();

			LegalIdentity Identity = await this.ContractsClient.ApplyAsync(Model.ToProperties(ServiceRef.XmppService));

			foreach (LegalIdentityAttachment Attachment in Attachments)
			{
				HttpFileUploadEventArgs e2 = await ServiceRef.XmppService.RequestUploadSlotAsync(
					Path.GetFileName(Attachment.FileName!)!, Attachment.ContentType!, Attachment.ContentLength);

				if (!e2.Ok)
					throw e2.StanzaError ?? new Exception(e2.ErrorText);

				await e2.PUT(Attachment.Data, Attachment.ContentType, (int)Constants.Timeouts.UploadFile.TotalMilliseconds);
				byte[] Signature = await this.ContractsClient.SignAsync(Attachment.Data, SignWith.CurrentKeys);

				Identity = await this.ContractsClient.AddLegalIdAttachmentAsync(Identity.Id, e2.GetUrl, Signature);
			}

			await this.ContractsClient.ReadyForApprovalAsync(Identity.Id);

			return Identity;
		}

		/// <summary>
		/// Returns a list of legal identities.
		/// </summary>
		/// <param name="client">The Xmpp client instance. Can be null, in that case the default one is used.</param>
		/// <returns>Legal Identities</returns>
		public async Task<LegalIdentity[]> GetLegalIdentities(XmppClient? client = null)
		{
			if (client is null)
				return await this.ContractsClient.GetLegalIdentitiesAsync();
			else
			{
				using ContractsClient cc = new(client, ServiceRef.TagProfile.LegalJid);  // No need to load keys for this operation.
				return await cc.GetLegalIdentitiesAsync();
			}
		}

		/// <summary>
		/// Gets a specific legal identity.
		/// </summary>
		/// <param name="legalIdentityId">The id of the legal identity to retrieve.</param>
		/// <returns>Legal identity object</returns>
		public async Task<LegalIdentity> GetLegalIdentity(CaseInsensitiveString legalIdentityId)
		{
			ContactInfo Info = await ContactInfo.FindByLegalId(legalIdentityId);

			if (Info is not null && Info.LegalIdentity is not null)
				return Info.LegalIdentity;

			return await this.ContractsClient.GetLegalIdentityAsync(legalIdentityId);
		}

		/// <summary>
		/// Checks if a legal identity is in the contacts list.
		/// </summary>
		/// <param name="legalIdentityId">The id of the legal identity to retrieve.</param>
		/// <returns>If the legal identity is in the contacts list.</returns>
		public async Task<bool> IsContact(CaseInsensitiveString legalIdentityId)
		{
			ContactInfo Info = await ContactInfo.FindByLegalId(legalIdentityId);
			return (Info is not null && Info.LegalIdentity is not null);
		}

		/// <summary>
		/// Checks if the client has access to the private keys of the specified legal identity.
		/// </summary>
		/// <param name="LegalIdentityId">The id of the legal identity.</param>
		/// <returns>If private keys are available.</returns>
		public Task<bool> HasPrivateKey(CaseInsensitiveString LegalIdentityId)
		{
			return this.ContractsClient.HasPrivateKey(LegalIdentityId);
		}

		/// <summary>
		/// Marks the legal identity as obsolete.
		/// </summary>
		/// <param name="legalIdentityId">The id to mark as obsolete.</param>
		/// <returns>Legal Identity</returns>
		public Task<LegalIdentity> ObsoleteLegalIdentity(CaseInsensitiveString legalIdentityId)
		{
			return this.ContractsClient.ObsoleteLegalIdentityAsync(legalIdentityId);
		}

		/// <summary>
		/// Marks the legal identity as compromised.
		/// </summary>
		/// <param name="legalIdentityId">The legal id to mark as compromised.</param>
		/// <returns>Legal Identity</returns>
		public Task<LegalIdentity> CompromiseLegalIdentity(CaseInsensitiveString legalIdentityId)
		{
			return this.ContractsClient.CompromisedLegalIdentityAsync(legalIdentityId);
		}

		/// <summary>
		/// Petitions a legal identity.
		/// </summary>
		/// <param name="LegalId">The id of the legal identity.</param>
		/// <param name="PetitionId">The petition id.</param>
		/// <param name="Purpose">The purpose of the petitioning.</param>
		public async Task PetitionIdentity(CaseInsensitiveString LegalId, string PetitionId, string Purpose)
		{
			if (ServiceRef.TagProfile.LegalIdentity is null)
				throw new Exception("No Legal Identity registered.");

			await this.ContractsClient.AuthorizeAccessToIdAsync(ServiceRef.TagProfile.LegalIdentity.Id, LegalId, true);

			this.StartPetition(PetitionId);
			await this.ContractsClient.PetitionIdentityAsync(LegalId, PetitionId, Purpose);
		}

		private void StartPetition(string PetitionId)
		{
			lock (this.currentPetitions)
			{
				this.currentPetitions[PetitionId] = true;
			}
		}

		private bool EndPetition(string PetitionId)
		{
			lock (this.currentPetitions)
			{
				return this.currentPetitions.Remove(PetitionId);
			}
		}

		/// <summary>
		/// Sends a response to a petitioning identity request.
		/// </summary>
		/// <param name="LegalId">The id of the legal identity.</param>
		/// <param name="PetitionId">The petition id.</param>
		/// <param name="RequestorFullJid">The full Jid of the requestor.</param>
		/// <param name="Response">If the petition is accepted (true) or rejected (false).</param>
		public Task SendPetitionIdentityResponse(CaseInsensitiveString LegalId, string PetitionId, string RequestorFullJid, bool Response)
		{
			return this.ContractsClient.PetitionIdentityResponseAsync(LegalId, PetitionId, RequestorFullJid, Response);
		}

		/// <summary>
		/// An event that fires when a legal identity changes.
		/// </summary>
		public event EventHandlerAsync<LegalIdentityEventArgs>? LegalIdentityChanged;

		/// <summary>
		/// An event that fires when an ID Application has changed.
		/// </summary>
		public event EventHandlerAsync<LegalIdentityEventArgs>? IdentityApplicationChanged;

		private async Task ContractsClient_IdentityUpdated(object? Sender, LegalIdentityEventArgs e)
		{
			try
			{
				if (ServiceRef.TagProfile.LegalIdentity is not null && ServiceRef.TagProfile.LegalIdentity.Id == e.Identity.Id)
				{
					if (ServiceRef.TagProfile.LegalIdentity.Created > e.Identity.Created)
						return;

					await ServiceRef.TagProfile.SetLegalIdentity(e.Identity, true);
					await this.LegalIdentityChanged.Raise(this, e);

					if (e.Identity.IsDiscarded() && Shell.Current.CurrentState.Location.OriginalString != Constants.Pages.RegistrationPage)
					{
						MainThread.BeginInvokeOnMainThread(async () =>
						{
							try
							{
								await ServiceRef.TagProfile.ClearLegalIdentity();
								ServiceRef.TagProfile.GoToStep(RegistrationStep.ValidatePhone, true);
								await Shell.Current.GoToAsync(Constants.Pages.RegistrationPage);
							}
							catch (Exception ex)
							{
								ServiceRef.LogService.LogException(ex);
								await App.StopAsync();
							}
						});
					}
				}
				else if (ServiceRef.TagProfile.IdentityApplication is not null && ServiceRef.TagProfile.IdentityApplication.Id == e.Identity.Id)
				{
					if (ServiceRef.TagProfile.IdentityApplication.Created > e.Identity.Created)
						return;

					if (e.Identity.IsDiscarded())
					{
						await ServiceRef.TagProfile.SetIdentityApplication(null, true);
						await this.IdentityApplicationChanged.Raise(this, e);
					}
					else if (e.Identity.IsApproved())
					{
						LegalIdentity? ToObsolete = ServiceRef.TagProfile.LegalIdentity;

						await ServiceRef.TagProfile.SetLegalIdentity(e.Identity, true);
						await ServiceRef.TagProfile.SetIdentityApplication(null, false);

						await this.LegalIdentityChanged.Raise(this, e);
						await this.IdentityApplicationChanged.Raise(this, e);

						if (ToObsolete is not null && !ToObsolete.IsDiscarded())
							await this.ObsoleteLegalIdentity(ToObsolete.Id);
					}
					else
					{
						await ServiceRef.TagProfile.SetIdentityApplication(e.Identity, false);
						await this.IdentityApplicationChanged.Raise(this, e);
					}
				}
				else if (ServiceRef.TagProfile.LegalIdentity is null)
				{
					if (e.Identity.IsDiscarded())
						return;

					await ServiceRef.TagProfile.SetLegalIdentity(e.Identity, true);
					await this.LegalIdentityChanged.Raise(this, e);
				}
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				await ServiceRef.UiService.DisplayException(ex);
			}
		}

		/// <summary>
		/// An event that fires when a petition for an identity is received.
		/// </summary>
		public event EventHandlerAsync<LegalIdentityPetitionEventArgs>? PetitionForIdentityReceived;

		private async Task ContractsClient_PetitionForIdentityReceived(object? Sender, LegalIdentityPetitionEventArgs e)
		{
			await this.PetitionForIdentityReceived.Raise(this, e);
		}

		/// <summary>
		/// An event that fires when a petitioned identity response is received.
		/// </summary>
		public event EventHandlerAsync<LegalIdentityPetitionResponseEventArgs>? PetitionedIdentityResponseReceived;

		private async Task ContractsClient_PetitionedIdentityResponseReceived(object? Sender, LegalIdentityPetitionResponseEventArgs e)
		{
			try
			{
				this.EndPetition(e.PetitionId);
				await this.PetitionedIdentityResponseReceived.Raise(this, e);
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				await ServiceRef.UiService.DisplayException(ex);
			}
		}

		/// <summary>
		/// Exports Keys to XML.
		/// </summary>
		/// <param name="Output">XML output.</param>
		public Task ExportSigningKeys(XmlWriter Output)
		{
			return this.ContractsClient.ExportKeys(Output);
		}

		/// <summary>
		/// Imports keys
		/// </summary>
		/// <param name="Xml">XML Definition of keys.</param>
		/// <returns>If keys could be loaded into the client.</returns>
		public Task<bool> ImportSigningKeys(XmlElement Xml)
		{
			return this.ContractsClient.ImportKeys(Xml);
		}

		/// <summary>
		/// Validates a legal identity.
		/// </summary>
		/// <param name="Identity">Legal Identity</param>
		/// <returns>The validity of the identity.</returns>
		public Task<IdentityStatus> ValidateIdentity(LegalIdentity Identity)
		{
			return this.ContractsClient.ValidateAsync(Identity, true);
		}

		#endregion

		#region Smart Contracts

		private readonly Dictionary<CaseInsensitiveString, DateTime> lastContractEvent = [];

		/// <summary>
		/// Reference to contracts client, with a check that one is created.
		/// Note: Do not make public. Reference only from inside the XmppService class.
		/// </summary>
		public ContractsClient ContractsClient
		{
			get
			{
				if (this.contractsClient is null)
					throw new InvalidOperationException(ServiceRef.Localizer[nameof(AppResources.LegalServiceNotFound)]);

				return this.contractsClient;
			}
		}

		private void RegisterContractsEventHandlers()
		{
			this.ContractsClient.EnableE2eEncryption(true, false);

			this.ContractsClient.IdentityUpdated += this.ContractsClient_IdentityUpdated;
			this.ContractsClient.PetitionForIdentityReceived += this.ContractsClient_PetitionForIdentityReceived;
			this.ContractsClient.PetitionedIdentityResponseReceived += this.ContractsClient_PetitionedIdentityResponseReceived;
			this.ContractsClient.PetitionForContractReceived += this.ContractsClient_PetitionForContractReceived;
			this.ContractsClient.PetitionedContractResponseReceived += this.ContractsClient_PetitionedContractResponseReceived;
			this.ContractsClient.PetitionForSignatureReceived += this.ContractsClient_PetitionForSignatureReceived;
			this.ContractsClient.PetitionedSignatureResponseReceived += this.ContractsClient_PetitionedSignatureResponseReceived;
			this.ContractsClient.PetitionForPeerReviewIDReceived += this.ContractsClient_PetitionForPeerReviewIdReceived;
			this.ContractsClient.PetitionedPeerReviewIDResponseReceived += this.ContractsClient_PetitionedPeerReviewIdResponseReceived;
			this.ContractsClient.PetitionClientUrlReceived += this.ContractsClient_PetitionClientUrlReceived;
			this.ContractsClient.ContractProposalReceived += this.ContractsClient_ContractProposalReceived;
			this.ContractsClient.ContractUpdated += this.ContractsClient_ContractUpdated;
			this.ContractsClient.ContractSigned += this.ContractsClient_ContractSigned;
		}

		/// <summary>
		/// Gets the contract with the specified id.
		/// </summary>
		/// <param name="ContractId">The contract id.</param>
		/// <returns>Smart Contract</returns>
		public Task<Contract> GetContract(CaseInsensitiveString ContractId)
		{
			return this.ContractsClient.GetContractAsync(ContractId);
		}

		/// <summary>
		/// Gets created contracts.
		/// </summary>
		/// <returns>Created contracts.</returns>
		public async Task<string[]> GetCreatedContractReferences()
		{
			List<string> Result = [];
			string[] ContractIds;
			int Offset = 0;
			int Nr;

			do
			{
				ContractIds = await this.ContractsClient.GetCreatedContractReferencesAsync(Offset, 20);
				Result.AddRange(ContractIds);
				Nr = ContractIds.Length;
				Offset += Nr;
			}
			while (Nr == 20);

			return [.. Result];
		}

		/// <summary>
		/// Gets signed contracts.
		/// </summary>
		/// <returns>Signed contracts.</returns>
		public async Task<string[]> GetSignedContractReferences()
		{
			List<string> Result = [];
			string[] ContractIds;
			int Offset = 0;
			int Nr;

			do
			{
				ContractIds = await this.ContractsClient.GetSignedContractReferencesAsync(Offset, 20);
				Result.AddRange(ContractIds);
				Nr = ContractIds.Length;
				Offset += Nr;
			}
			while (Nr == 20);

			return [.. Result];
		}

		/// <summary>
		/// Signs a given contract.
		/// </summary>
		/// <param name="Contract">The contract to sign.</param>
		/// <param name="Role">The role of the signer.</param>
		/// <param name="Transferable">Whether the contract is transferable or not.</param>
		/// <returns>Smart Contract</returns>
		public async Task<Contract> SignContract(Contract Contract, string Role, bool Transferable)
		{
			if (Contract.ForMachinesNamespace == Constants.ContractMachineNames.PaymentInstructionsNamespace && (
				Contract.ForMachinesLocalName == Constants.ContractMachineNames.BuyEDaler ||
				Contract.ForMachinesLocalName == Constants.ContractMachineNames.SellEDaler))
			{
				lock (this.currentTransactions)
				{
					string TransactionId = Contract.ContractId;
					string Currency = Contract["Currency"]?.ToString() ?? string.Empty;

					this.currentTransactions[Contract.ContractId] = new PaymentTransaction(TransactionId, Currency);
				}
			}

			Contract Result = await this.ContractsClient.SignContractAsync(Contract, Role, Transferable);
			await UpdateContractReference(Result);
			return Result;
		}

		/// <summary>
		/// Obsoletes a contract.
		/// </summary>
		/// <param name="ContractId">The id of the contract to obsolete.</param>
		/// <returns>Smart Contract</returns>
		public async Task<Contract> ObsoleteContract(CaseInsensitiveString ContractId)
		{
			Contract Result = await this.ContractsClient.ObsoleteContractAsync(ContractId);
			await UpdateContractReference(Result);
			return Result;
		}

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
		public async Task<Contract> CreateContract(
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
			bool CanActAsTemplate)
		{
			Contract Result = await this.ContractsClient.CreateContractAsync(TemplateId, Parts, Parameters, Visibility, PartsMode, Duration, ArchiveRequired, ArchiveOptional, SignAfter, SignBefore, CanActAsTemplate);
			await UpdateContractReference(Result);
			return Result;
		}

		/// <summary>
		/// Deletes a contract.
		/// </summary>
		/// <param name="ContractId">The id of the contract to delete.</param>
		/// <returns>Smart Contract</returns>
		public async Task<Contract> DeleteContract(CaseInsensitiveString ContractId)
		{
			Contract Contract = await this.ContractsClient.DeleteContractAsync(ContractId);
			await Database.FindDelete<ContractReference>(new FilterFieldEqualTo("ContractId", Contract.ContractId));
			return Contract;
		}

		/// <summary>
		/// Petitions a contract with the specified id and purpose.
		/// </summary>
		/// <param name="ContractId">The contract id.</param>
		/// <param name="PetitionId">The petition id.</param>
		/// <param name="Purpose">The purpose.</param>
		public Task PetitionContract(CaseInsensitiveString ContractId, string PetitionId, string Purpose)
		{
			this.StartPetition(PetitionId);
			return this.ContractsClient.PetitionContractAsync(ContractId, PetitionId, Purpose);
		}

		/// <summary>
		/// Sends a response to a petitioning contract request.
		/// </summary>
		/// <param name="ContractId">The id of the contract.</param>
		/// <param name="PetitionId">The petition id.</param>
		/// <param name="RequestorFullJid">The full Jid of the requestor.</param>
		/// <param name="Response">If the petition is accepted (true) or rejected (false).</param>
		public Task SendPetitionContractResponse(CaseInsensitiveString ContractId, string PetitionId, string RequestorFullJid, bool Response)
		{
			return this.ContractsClient.PetitionContractResponseAsync(ContractId, PetitionId, RequestorFullJid, Response);
		}

		/// <summary>
		/// An event that fires when a petition for a contract is received.
		/// </summary>
		public event EventHandlerAsync<ContractPetitionEventArgs>? PetitionForContractReceived;

		private async Task ContractsClient_PetitionForContractReceived(object? Sender, ContractPetitionEventArgs e)
		{
			await this.PetitionForContractReceived.Raise(this, e);
		}

		/// <summary>
		/// An event that fires when a petitioned contract response is received.
		/// </summary>
		public event EventHandlerAsync<ContractPetitionResponseEventArgs>? PetitionedContractResponseReceived;

		private async Task ContractsClient_PetitionedContractResponseReceived(object? Sender, ContractPetitionResponseEventArgs e)
		{
			try
			{
				this.EndPetition(e.PetitionId);
				await this.PetitionedContractResponseReceived.Raise(this, e);
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				await ServiceRef.UiService.DisplayException(ex);
			}
		}

		/// <summary>
		/// Gets the timestamp of the last event received for a given contract ID.
		/// </summary>
		/// <param name="ContractId">Contract ID</param>
		/// <returns>Timestamp</returns>
		public DateTime GetTimeOfLastContractEvent(CaseInsensitiveString ContractId)
		{
			lock (this.lastContractEvent)
			{
				if (this.lastContractEvent.TryGetValue(ContractId, out DateTime TP))
					return TP;
				else
					return DateTime.MinValue;
			}
		}

		private static async Task UpdateContractReference(Contract Contract)
		{
			ContractReference Ref = await Database.FindFirstDeleteRest<ContractReference>(
				new FilterFieldEqualTo("ContractId", Contract.ContractId));

			if (Ref is null)
			{
				Ref = new ContractReference()
				{
					ContractId = Contract.ContractId
				};

				await Ref.SetContract(Contract);
				await Database.Insert(Ref);
			}
			else
			{
				await Ref.SetContract(Contract);
				await Database.Update(Ref);
			}

			ServiceRef.TagProfile.CheckContractReference(Ref);
		}

		/// <summary>
		/// Event raised when a contract proposal has been received.
		/// </summary>
		public event EventHandlerAsync<ContractProposalEventArgs>? ContractProposalReceived;

		private async Task ContractsClient_ContractProposalReceived(object? Sender, ContractProposalEventArgs e)
		{
			await this.ContractProposalReceived.Raise(this, e);
		}

		/// <summary>
		/// Event raised when contract was updated.
		/// </summary>
		public event EventHandlerAsync<ContractReferenceEventArgs>? ContractUpdated;

		private async Task ContractsClient_ContractUpdated(object? Sender, ContractReferenceEventArgs e)
		{
			await this.ContractUpdatedOrSigned(e);
			await this.ContractUpdated.Raise(this, e);
		}

		private Task ContractUpdatedOrSigned(ContractReferenceEventArgs e)
		{
			lock (this.lastContractEvent)
			{
				this.lastContractEvent[e.ContractId] = DateTime.Now;
			}

			return Task.CompletedTask;
		}

		/// <summary>
		/// Event raised when contract was signed.
		/// </summary>
		public event EventHandlerAsync<ContractSignedEventArgs>? ContractSigned;

		private async Task ContractsClient_ContractSigned(object? Sender, ContractSignedEventArgs e)
		{
			await this.ContractUpdatedOrSigned(e);
			await this.ContractSigned.Raise(this, e);
		}

		/// <summary>
		/// Sends a contract proposal to a recipient.
		/// </summary>
		/// <param name="Contract">Proposed contract.</param>
		/// <param name="Role">Proposed role of recipient.</param>
		/// <param name="To">Recipient Address (Bare or Full JID).</param>
		/// <param name="Message">Optional message included in message.</param>
		public Task SendContractProposal(Contract Contract, string Role, string To, string Message)
		{
			return this.ContractsClient.SendContractProposal(Contract, Role, To, Message);
		}

		#endregion

		#region Attachments

		/// <summary>
		/// Gets an attachment for a contract.
		/// </summary>
		/// <param name="Url">The url of the attachment.</param>
		/// <param name="Timeout">Max timeout allowed when retrieving an attachment.</param>
		/// <param name="SignWith">How the request is signed. For identity attachments, especially for attachments to an identity being created, <see cref="SignWith.CurrentKeys"/> should be used. For requesting attachments relating to a contract, <see cref="SignWith.LatestApprovedId"/> should be used.</param>
		/// <returns>Content-Type, and attachment file.</returns>
		public Task<KeyValuePair<string, TemporaryFile>> GetAttachment(string Url, SignWith SignWith, TimeSpan Timeout)
		{
			return this.ContractsClient.GetAttachmentAsync(Url, SignWith, (int)Timeout.TotalMilliseconds);
		}

		#endregion

		#region Peer Review

		/// <summary>
		/// Sends a petition to a third-party to review a legal identity.
		/// </summary>
		/// <param name="LegalId">The legal id to petition.</param>
		/// <param name="Identity">The legal id to peer review.</param>
		/// <param name="PetitionId">The petition id.</param>
		/// <param name="Purpose">The purpose.</param>
		public async Task PetitionPeerReviewId(CaseInsensitiveString LegalId, LegalIdentity Identity, string PetitionId, string Purpose)
		{
			await this.ContractsClient.AuthorizeAccessToIdAsync(Identity.Id, LegalId, true);

			this.StartPetition(PetitionId);
			await this.ContractsClient.PetitionPeerReviewIDAsync(LegalId, Identity, PetitionId, Purpose);
		}

		/// <summary>
		/// Adds an attachment for the peer review.
		/// </summary>
		/// <param name="Identity">The identity to which the attachment should be added.</param>
		/// <param name="ReviewerLegalIdentity">The identity of the reviewer.</param>
		/// <param name="PeerSignature">The raw signature data.</param>
		/// <returns>Legal Identity</returns>
		public Task<LegalIdentity> AddPeerReviewIdAttachment(LegalIdentity Identity, LegalIdentity ReviewerLegalIdentity, byte[] PeerSignature)
		{
			return this.ContractsClient.AddPeerReviewIDAttachment(Identity, ReviewerLegalIdentity, PeerSignature);
		}

		/// <summary>
		/// An event that fires when a petition for peer review is received.
		/// </summary>
		public event EventHandlerAsync<SignaturePetitionEventArgs>? PetitionForPeerReviewIdReceived;

		private async Task ContractsClient_PetitionForPeerReviewIdReceived(object? Sender, SignaturePetitionEventArgs e)
		{
			await this.PetitionForPeerReviewIdReceived.Raise(this, e);
		}

		/// <summary>
		/// An event that fires when a petitioned peer review response is received.
		/// </summary>
		public event EventHandlerAsync<SignaturePetitionResponseEventArgs>? PetitionedPeerReviewIdResponseReceived;

		private async Task ContractsClient_PetitionedPeerReviewIdResponseReceived(object? Sender, SignaturePetitionResponseEventArgs e)
		{
			try
			{
				this.EndPetition(e.PetitionId);
				await this.PetitionedPeerReviewIdResponseReceived.Raise(this, e);
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				await ServiceRef.UiService.DisplayException(ex);
			}
		}

		/// <summary>
		/// Gets available service providers for buying eDaler.
		/// </summary>
		/// <returns>Available service providers for peer review of identity applications.</returns>
		public async Task<ServiceProviderWithLegalId[]> GetServiceProvidersForPeerReviewAsync()
		{
			return await this.ContractsClient.GetPeerReviewIdServiceProvidersAsync();
		}

		/// <summary>
		/// Selects a peer-review service as default, for the account, when sending a peer-review request to the
		/// Legal Identity of the Trust Provider hosting the account.
		/// </summary>
		/// <param name="ServiceId">Service ID</param>
		/// <param name="ServiceProvider">Service Provider</param>
		public async Task SelectPeerReviewService(string ServiceId, string ServiceProvider)
		{
			await this.ContractsClient.SelectPeerReviewServiceAsync(ServiceProvider, ServiceId);
		}

		private readonly Dictionary<string, bool> currentPetitions = [];

		private async Task ContractsClient_PetitionClientUrlReceived(object? Sender, PetitionClientUrlEventArgs e)
		{
			lock (this.currentPetitions)
			{
				if (!this.currentPetitions.ContainsKey(e.PetitionId))
				{
					ServiceRef.LogService.LogWarning("Client URL message for a petition is ignored. Petition ID not recognized.",
						new KeyValuePair<string, object?>("PetitionId", e.PetitionId),
						new KeyValuePair<string, object?>("ClientUrl", e.ClientUrl));
					return;
				}
			}

			await App.OpenUrlAsync(e.ClientUrl);
		}

		#endregion

		#region Signatures

		/// <summary>
		/// Signs binary data with the corresponding private key.
		/// </summary>
		/// <param name="data">The data to sign.</param>
		/// <param name="signWith">What keys that can be used to sign the data.</param>
		/// <returns>Signature</returns>
		public Task<byte[]> Sign(byte[] data, SignWith signWith)
		{
			return this.ContractsClient.SignAsync(data, signWith);
		}

		/// <summary>Validates a signature of binary data.</summary>
		/// <param name="legalIdentity">Legal identity used to create the signature.</param>
		/// <param name="data">Binary data to sign-</param>
		/// <param name="signature">Digital signature of data</param>
		/// <returns>
		/// true = Signature is valid.
		/// false = Signature is invalid.
		/// null = Client key algorithm is unknown, and veracity of signature could not be established.
		/// </returns>
		public bool? ValidateSignature(LegalIdentity legalIdentity, byte[] data, byte[] signature)
		{
			return this.ContractsClient.ValidateSignature(legalIdentity, data, signature);
		}

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
		public Task SendPetitionSignatureResponse(CaseInsensitiveString LegalId, byte[] Content, byte[] Signature, string PetitionId, string RequestorFullJid, bool Response)
		{
			return this.ContractsClient.PetitionSignatureResponseAsync(LegalId, Content, Signature, PetitionId, RequestorFullJid, Response);
		}

		/// <summary>
		/// An event that fires when a petition for a signature is received.
		/// </summary>
		public event EventHandlerAsync<SignaturePetitionEventArgs>? PetitionForSignatureReceived;

		private async Task ContractsClient_PetitionForSignatureReceived(object? Sender, SignaturePetitionEventArgs e)
		{
			await this.PetitionForSignatureReceived.Raise(this, e);
		}

		/// <summary>
		/// Event raised when a response to a signature petition has been received.
		/// </summary>
		public event EventHandlerAsync<SignaturePetitionResponseEventArgs>? SignaturePetitionResponseReceived;

		private async Task ContractsClient_PetitionedSignatureResponseReceived(object? Sender, SignaturePetitionResponseEventArgs e)
		{
			try
			{
				this.EndPetition(e.PetitionId);
				await this.SignaturePetitionResponseReceived.Raise(this, e);
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				await ServiceRef.UiService.DisplayException(ex);
			}
		}

		#endregion

		#region Provisioning

		/// <summary>
		/// Access to provisioning client, for authorization control, with a check that one is created.
		/// Note: Do not make public. Reference only from inside the XmppService class.
		/// </summary>
		private ProvisioningClient ProvisioningClient
		{
			get
			{
				if (this.provisioningClient is null)
					throw new InvalidOperationException(ServiceRef.Localizer[nameof(AppResources.ProvisioningServiceNotFound)]);

				return this.provisioningClient;
			}
		}

		private async Task ProvisioningClient_IsFriendQuestion(object? Sender, IsFriendEventArgs e)
		{
			if (e.From.IndexOfAny(clientChars) < 0)
				await ServiceRef.NotificationService.NewEvent(new IsFriendNotificationEvent(e));
		}

		private async Task ProvisioningClient_CanReadQuestion(object? Sender, CanReadEventArgs e)
		{
			if (e.From.IndexOfAny(clientChars) < 0)
				await ServiceRef.NotificationService.NewEvent(new CanReadNotificationEvent(e));
		}

		private async Task ProvisioningClient_CanControlQuestion(object? Sender, CanControlEventArgs e)
		{
			if (e.From.IndexOfAny(clientChars) < 0)
				await ServiceRef.NotificationService.NewEvent(new CanControlNotificationEvent(e));
		}

		private static readonly char[] clientChars = ['@', '/'];

		/// <summary>
		/// JID of provisioning service.
		/// </summary>
		public string ProvisioningServiceJid => this.ProvisioningClient.ProvisioningServerAddress;

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
		public void IsFriendResponse(string ProvisioningServiceJID, string JID, string RemoteJID, string Key, bool IsFriend,
			RuleRange Range, EventHandlerAsync<IqResultEventArgs> Callback, object? State)
		{
			this.ProvisioningClient.IsFriendResponse(ProvisioningServiceJID, JID, RemoteJID, Key, IsFriend, Range, Callback, State);
		}

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
		public void CanControlResponseAll(string ProvisioningServiceJID, string JID, string RemoteJID, string Key, bool CanControl,
			string[]? ParameterNames, IThingReference Node, EventHandlerAsync<IqResultEventArgs> Callback, object? State)
		{
			this.ProvisioningClient.CanControlResponseAll(ProvisioningServiceJID, JID, RemoteJID, Key, CanControl, ParameterNames,
				Node, Callback, State);
		}

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
		public void CanControlResponseCaller(string ProvisioningServiceJID, string JID, string RemoteJID, string Key,
			bool CanControl, string[]? ParameterNames, IThingReference Node, EventHandlerAsync<IqResultEventArgs> Callback, object? State)
		{
			this.ProvisioningClient.CanControlResponseCaller(ProvisioningServiceJID, JID, RemoteJID, Key, CanControl,
				ParameterNames, Node, Callback, State);
		}

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
		public void CanControlResponseDomain(string ProvisioningServiceJID, string JID, string RemoteJID, string Key,
			bool CanControl, string[]? ParameterNames, IThingReference Node, EventHandlerAsync<IqResultEventArgs> Callback, object? State)
		{
			this.ProvisioningClient.CanControlResponseDomain(ProvisioningServiceJID, JID, RemoteJID, Key, CanControl,
				ParameterNames, Node, Callback, State);
		}

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
		public void CanControlResponseDevice(string ProvisioningServiceJID, string JID, string RemoteJID, string Key,
			bool CanControl, string[]? ParameterNames, string Token, IThingReference Node, EventHandlerAsync<IqResultEventArgs> Callback,
			object? State)
		{
			this.ProvisioningClient.CanControlResponseDevice(ProvisioningServiceJID, JID, RemoteJID, Key, CanControl,
				ParameterNames, Token, Node, Callback, State);
		}

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
		public void CanControlResponseService(string ProvisioningServiceJID, string JID, string RemoteJID, string Key,
			bool CanControl, string[]? ParameterNames, string Token, IThingReference Node, EventHandlerAsync<IqResultEventArgs> Callback,
			object? State)
		{
			this.ProvisioningClient.CanControlResponseService(ProvisioningServiceJID, JID, RemoteJID, Key, CanControl,
				ParameterNames, Token, Node, Callback, State);
		}

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
		public void CanControlResponseUser(string ProvisioningServiceJID, string JID, string RemoteJID, string Key,
			bool CanControl, string[]? ParameterNames, string Token, IThingReference Node, EventHandlerAsync<IqResultEventArgs> Callback,
			object? State)
		{
			this.ProvisioningClient.CanControlResponseUser(ProvisioningServiceJID, JID, RemoteJID, Key, CanControl,
				ParameterNames, Token, Node, Callback, State);
		}

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
		public void CanReadResponseAll(string ProvisioningServiceJID, string JID, string RemoteJID, string Key, bool CanRead,
			FieldType FieldTypes, string[]? FieldNames, IThingReference Node, EventHandlerAsync<IqResultEventArgs> Callback, object? State)
		{
			this.ProvisioningClient.CanReadResponseAll(ProvisioningServiceJID, JID, RemoteJID, Key, CanRead, FieldTypes, FieldNames,
				Node, Callback, State);
		}

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
		public void CanReadResponseCaller(string ProvisioningServiceJID, string JID, string RemoteJID, string Key,
			bool CanRead, FieldType FieldTypes, string[]? FieldNames, IThingReference Node, EventHandlerAsync<IqResultEventArgs> Callback, object? State)
		{
			this.ProvisioningClient.CanReadResponseCaller(ProvisioningServiceJID, JID, RemoteJID, Key, CanRead,
				FieldTypes, FieldNames, Node, Callback, State);
		}

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
		public void CanReadResponseDomain(string ProvisioningServiceJID, string JID, string RemoteJID, string Key,
			bool CanRead, FieldType FieldTypes, string[]? FieldNames, IThingReference Node, EventHandlerAsync<IqResultEventArgs> Callback, object? State)
		{
			this.ProvisioningClient.CanReadResponseDomain(ProvisioningServiceJID, JID, RemoteJID, Key, CanRead,
				FieldTypes, FieldNames, Node, Callback, State);
		}

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
		public void CanReadResponseDevice(string ProvisioningServiceJID, string JID, string RemoteJID, string Key,
			bool CanRead, FieldType FieldTypes, string[]? FieldNames, string Token, IThingReference Node, EventHandlerAsync<IqResultEventArgs> Callback,
			object? State)
		{
			this.ProvisioningClient.CanReadResponseDevice(ProvisioningServiceJID, JID, RemoteJID, Key, CanRead,
				FieldTypes, FieldNames, Token, Node, Callback, State);
		}

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
		public void CanReadResponseService(string ProvisioningServiceJID, string JID, string RemoteJID, string Key,
			bool CanRead, FieldType FieldTypes, string[]? FieldNames, string Token, IThingReference Node, EventHandlerAsync<IqResultEventArgs> Callback,
			object? State)
		{
			this.ProvisioningClient.CanReadResponseService(ProvisioningServiceJID, JID, RemoteJID, Key, CanRead,
				FieldTypes, FieldNames, Token, Node, Callback, State);
		}

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
		public void CanReadResponseUser(string ProvisioningServiceJID, string JID, string RemoteJID, string Key,
			bool CanRead, FieldType FieldTypes, string[]? FieldNames, string Token, IThingReference Node, EventHandlerAsync<IqResultEventArgs> Callback,
			object? State)
		{
			this.ProvisioningClient.CanReadResponseUser(ProvisioningServiceJID, JID, RemoteJID, Key, CanRead,
				FieldTypes, FieldNames, Token, Node, Callback, State);
		}

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
		public void DeleteDeviceRules(string ServiceJID, string DeviceJID, string NodeId, string SourceId, string Partition,
			EventHandlerAsync<IqResultEventArgs> Callback, object? State)
		{
			this.ProvisioningClient.DeleteDeviceRules(ServiceJID, DeviceJID, NodeId, SourceId, Partition, Callback, State);
		}

		#endregion

		#region IoT

		/// <summary>
		/// Access to sensor client, for sensor data readout and subscription, with a check that one is created.
		/// Note: Do not make public. Reference only from inside the XmppService class.
		/// </summary>
		private SensorClient SensorClient
		{
			get
			{
				if (this.sensorClient is null)
					throw new InvalidOperationException(ServiceRef.Localizer[nameof(AppResources.SensorServiceNotFound)]);

				return this.sensorClient;
			}
		}

		/// <summary>
		/// Access to control client, for access to actuators, with a check that one is created.
		/// Note: Do not make public. Reference only from inside the XmppService class.
		/// </summary>
		private ControlClient ControlClient
		{
			get
			{
				if (this.controlClient is null)
					throw new InvalidOperationException(ServiceRef.Localizer[nameof(AppResources.ControlServiceNotFound)]);

				return this.controlClient;
			}
		}

		/// <summary>
		/// Access to concentrator client, for administrative purposes of concentrators, with a check that one is created.
		/// Note: Do not make public. Reference only from inside the XmppService class.
		/// </summary>
		private ConcentratorClient ConcentratorClient
		{
			get
			{
				if (this.concentratorClient is null)
					throw new InvalidOperationException(ServiceRef.Localizer[nameof(AppResources.ConcentratorServiceNotFound)]);

				return this.concentratorClient;
			}
		}

		/// <summary>
		/// Gets a (partial) list of my devices.
		/// </summary>
		/// <param name="Offset">Start offset of list</param>
		/// <param name="MaxCount">Maximum number of items in response.</param>
		/// <returns>Found devices, and if there are more devices available.</returns>
		public Task<(SearchResultThing[], bool)> GetMyDevices(int Offset, int MaxCount)
		{
			TaskCompletionSource<(SearchResultThing[], bool)> Result = new();

			this.ProvisioningClient.GetDevices(Offset, MaxCount, (sender, e) =>
			{
				if (e.Ok)
					Result.TrySetResult((e.Things, e.More));
				else
					Result.TrySetException(e.StanzaError ?? new Exception(ServiceRef.Localizer[nameof(AppResources.UnableToGetListOfMyDevices)]));

				return Task.CompletedTask;
			}, null);

			return Result.Task;
		}

		/// <summary>
		/// Gets the full list of my devices.
		/// </summary>
		/// <returns>Complete list of my devices.</returns>
		public async Task<SearchResultThing[]> GetAllMyDevices()
		{
			(SearchResultThing[] Things, bool More) = await this.GetMyDevices(0, Constants.BatchSizes.DeviceBatchSize);
			if (!More)
				return Things;

			List<SearchResultThing> Result = [];
			int Offset = Things.Length;

			Result.AddRange(Things);

			while (More)
			{
				(Things, More) = await this.GetMyDevices(Offset, Constants.BatchSizes.DeviceBatchSize);
				Result.AddRange(Things);
				Offset += Things.Length;
			}

			return [.. Result];
		}

		/// <summary>
		/// Gets the certificate the corresponds to a token. This certificate can be used
		/// to identify services, devices or users. Tokens are challenged to make sure they
		/// correspond to the holder of the private part of the corresponding certificate.
		/// </summary>
		/// <param name="Token">Token corresponding to the requested certificate.</param>
		/// <param name="Callback">Callback method called, when certificate is available.</param>
		/// <param name="State">State object that will be passed on to the callback method.</param>
		public void GetCertificate(string Token, EventHandlerAsync<CertificateEventArgs> Callback, object? State)
		{
			this.ProvisioningClient.GetCertificate(Token, Callback, State);
		}

		/// <summary>
		/// Gets a control form from an actuator.
		/// </summary>
		/// <param name="To">Address of actuator.</param>
		/// <param name="Language">Language</param>
		/// <param name="Callback">Method to call when response is returned.</param>
		/// <param name="State">State object.</param>
		/// <param name="Nodes">Node references</param>
		public void GetControlForm(string To, string Language, EventHandlerAsync<DataFormEventArgs> Callback, object? State,
			params ThingReference[] Nodes)
		{
			this.ControlClient.GetForm(To, Language, Callback, State, Nodes);
		}

		/// <summary>
		/// Requests a sensor data readout.
		/// </summary>
		/// <param name="Destination">JID of sensor to read.</param>
		/// <param name="Types">Field Types to read.</param>
		/// <returns>Request object maintaining the current status of the request.</returns>
		public Task<SensorDataClientRequest> RequestSensorReadout(string Destination, FieldType Types)
		{
			return this.SensorClient.RequestReadout(Destination, Types);
		}

		/// <summary>
		/// Requests a sensor data readout.
		/// </summary>
		/// <param name="Destination">JID of sensor to read.</param>
		/// <param name="Nodes">Array of nodes to read. Can be null or empty, if reading a sensor that is not a concentrator.</param>
		/// <param name="Types">Field Types to read.</param>
		/// <returns>Request object maintaining the current status of the request.</returns>
		public Task<SensorDataClientRequest> RequestSensorReadout(string Destination, ThingReference[] Nodes, FieldType Types)
		{
			return this.SensorClient.RequestReadout(Destination, Nodes, Types);
		}

		#endregion

		#region e-Daler

		private readonly Dictionary<string, Wallet.Transaction> currentTransactions = [];
		private Balance? lastBalance = null;
		private DateTime lastEDalerEvent = DateTime.MinValue;

		/// <summary>
		/// Reference to the e-Daler client implementing the e-Daler XMPP extension
		/// Note: Do not make public. Reference only from inside the XmppService class.
		/// </summary>
		private EDalerClient EDalerClient
		{
			get
			{
				if (this.eDalerClient is null)
					throw new InvalidOperationException(ServiceRef.Localizer[nameof(AppResources.EDalerServiceNotFound)]);

				return this.eDalerClient;
			}
		}

		private void RegisterEDalerEventHandlers(EDalerClient Client)
		{
			Client.BalanceUpdated += this.EDalerClient_BalanceUpdated;
			Client.BuyEDalerOptionsClientUrlReceived += this.NeuroWallet_BuyEDalerOptionsClientUrlReceived;
			Client.BuyEDalerOptionsCompleted += this.NeuroWallet_BuyEDalerOptionsCompleted;
			Client.BuyEDalerOptionsError += this.NeuroWallet_BuyEDalerOptionsError;
			Client.BuyEDalerClientUrlReceived += this.NeuroWallet_BuyEDalerClientUrlReceived;
			Client.BuyEDalerCompleted += this.NeuroWallet_BuyEDalerCompleted;
			Client.BuyEDalerError += this.NeuroWallet_BuyEDalerError;
			Client.SellEDalerOptionsClientUrlReceived += this.NeuroWallet_SellEDalerOptionsClientUrlReceived;
			Client.SellEDalerOptionsCompleted += this.NeuroWallet_SellEDalerOptionsCompleted;
			Client.SellEDalerOptionsError += this.NeuroWallet_SellEDalerOptionsError;
			Client.SellEDalerClientUrlReceived += this.NeuroWallet_SellEDalerClientUrlReceived;
			Client.SellEDalerCompleted += this.NeuroWallet_SellEDalerCompleted;
			Client.SellEDalerError += this.NeuroWallet_SellEDalerError;
		}

		private async Task EDalerClient_BalanceUpdated(object? _, BalanceEventArgs e)
		{
			this.lastBalance = e.Balance;
			this.lastEDalerEvent = DateTime.Now;

			await this.EDalerBalanceUpdated.Raise(this, e);
		}

		/// <summary>
		/// Event raised when balance has been updated
		/// </summary>
		public event EventHandlerAsync<BalanceEventArgs>? EDalerBalanceUpdated;

		/// <summary>
		/// Last reported balance
		/// </summary>
		public Balance? LastEDalerBalance => this.lastBalance;

		/// <summary>
		/// Timepoint of last event.
		/// </summary>
		public DateTime LastEDalerEvent => this.lastEDalerEvent;

		/// <summary>
		/// Tries to parse an eDaler URI.
		/// </summary>
		/// <param name="Uri">URI string.</param>
		/// <param name="Parsed">Parsed eDaler URI, if successful.</param>
		/// <param name="Reason">Error message, if not able to parse URI.</param>
		/// <returns>If URI string could be parsed.</returns>
		public bool TryParseEDalerUri(string Uri, out EDalerUri Parsed, out string Reason)
		{
			return EDalerUri.TryParse(Uri, out Parsed, out Reason);
		}

		/// <summary>
		/// Tries to decrypt an encrypted private message.
		/// </summary>
		/// <param name="EncryptedMessage">Encrypted message.</param>
		/// <param name="PublicKey">Public key used.</param>
		/// <param name="TransactionId">ID of transaction containing the encrypted message.</param>
		/// <param name="RemoteEndpoint">Remote endpoint</param>
		/// <returns>Decrypted string, if successful, or null, if not.</returns>
		public async Task<string> TryDecryptMessage(byte[] EncryptedMessage, byte[] PublicKey, Guid TransactionId, string RemoteEndpoint)
		{
			try
			{
				return await this.EDalerClient.DecryptMessage(EncryptedMessage, PublicKey, TransactionId, RemoteEndpoint);
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				return string.Empty;
			}
		}

		/// <summary>
		/// Sends an eDaler URI to the eDaler service.
		/// </summary>
		/// <param name="Uri">eDaler URI</param>
		/// <returns>Transaction object containing information about the processed URI.</returns>
		public Task<EDaler.Transaction> SendEDalerUri(string Uri)
		{
			return this.EDalerClient.SendEDalerUriAsync(Uri);
		}

		/// <summary>
		/// Gets account events available for the wallet.
		/// </summary>
		/// <param name="MaxCount">Maximum number of events to return.</param>
		/// <returns>Events found, and if more events are available.</returns>
		public Task<(AccountEvent[], bool)> GetEDalerAccountEvents(int MaxCount)
		{
			return this.EDalerClient.GetAccountEventsAsync(MaxCount);
		}

		/// <summary>
		/// Gets account events available for the wallet.
		/// </summary>
		/// <param name="MaxCount">Maximum number of events to return.</param>
		/// <param name="From">From what point in time events should be returned.</param>
		/// <returns>Events found, and if more events are available.</returns>
		public Task<(AccountEvent[], bool)> GetEDalerAccountEvents(int MaxCount, DateTime From)
		{
			return this.EDalerClient.GetAccountEventsAsync(MaxCount, From);
		}

		/// <summary>
		/// Gets the current account balance.
		/// </summary>
		/// <returns>Current account balance.</returns>
		public Task<Balance> GetEDalerBalance()
		{
			return this.EDalerClient.GetBalanceAsync();
		}

		/// <summary>
		/// Gets pending payments
		/// </summary>
		/// <returns>(Total amount, currency, items)</returns>
		public Task<(decimal, string, PendingPayment[])> GetPendingEDalerPayments()
		{
			return this.EDalerClient.GetPendingPayments();
		}

		/// <summary>
		/// Creates a full payment URI.
		/// </summary>
		/// <param name="ToBareJid">To whom the payment is to be made.</param>
		/// <param name="Amount">Amount of eDaler to pay.</param>
		/// <param name="AmountExtra">Any amount extra of eDaler to pay.</param>
		/// <param name="Currency">Currency to pay.</param>
		/// <param name="ValidNrDays">For how many days the URI should be valid.</param>
		/// <returns>Signed payment URI.</returns>
		public Task<string> CreateFullEDalerPaymentUri(string ToBareJid, decimal Amount, decimal? AmountExtra, string Currency, int ValidNrDays)
		{
			this.lastEDalerEvent = DateTime.Now;
			return this.EDalerClient.CreateFullPaymentUri(ToBareJid, Amount, AmountExtra, Currency, ValidNrDays);
		}

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
		public Task<string> CreateFullEDalerPaymentUri(string ToBareJid, decimal Amount, decimal? AmountExtra, string Currency, int ValidNrDays, string Message)
		{
			this.lastEDalerEvent = DateTime.Now;
			return this.EDalerClient.CreateFullPaymentUri(ToBareJid, Amount, AmountExtra, Currency, ValidNrDays, Message);
		}

		/// <summary>
		/// Creates a full payment URI.
		/// </summary>
		/// <param name="To">To whom the payment is to be made.</param>
		/// <param name="Amount">Amount of eDaler to pay.</param>
		/// <param name="AmountExtra">Any amount extra of eDaler to pay.</param>
		/// <param name="Currency">Currency to pay.</param>
		/// <param name="ValidNrDays">For how many days the URI should be valid.</param>
		/// <returns>Signed payment URI.</returns>
		public Task<string> CreateFullEDalerPaymentUri(LegalIdentity To, decimal Amount, decimal? AmountExtra, string Currency, int ValidNrDays)
		{
			this.lastEDalerEvent = DateTime.Now;
			return this.EDalerClient.CreateFullPaymentUri(To, Amount, AmountExtra, Currency, ValidNrDays);
		}

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
		public Task<string> CreateFullEDalerPaymentUri(LegalIdentity To, decimal Amount, decimal? AmountExtra, string Currency, int ValidNrDays, string PrivateMessage)
		{
			this.lastEDalerEvent = DateTime.Now;
			return this.EDalerClient.CreateFullPaymentUri(To, Amount, AmountExtra, Currency, ValidNrDays, PrivateMessage);
		}

		/// <summary>
		/// Creates an incomplete PayMe-URI.
		/// </summary>
		/// <param name="BareJid">To whom the payment is to be made.</param>
		/// <param name="Amount">Amount of eDaler to pay.</param>
		/// <param name="AmountExtra">Any amount extra of eDaler to pay.</param>
		/// <param name="Currency">Currency to pay.</param>
		/// <param name="Message">Message to be sent to recipient (not encrypted).</param>
		/// <returns>Incomplete PayMe-URI.</returns>
		public string CreateIncompleteEDalerPayMeUri(string BareJid, decimal? Amount, decimal? AmountExtra, string Currency, string Message)
		{
			return this.EDalerClient.CreateIncompletePayMeUri(BareJid, Amount, AmountExtra, Currency, Message);
		}

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
		public string CreateIncompleteEDalerPayMeUri(LegalIdentity To, decimal? Amount, decimal? AmountExtra, string Currency, string PrivateMessage)
		{
			return this.EDalerClient.CreateIncompletePayMeUri(To, Amount, AmountExtra, Currency, PrivateMessage);
		}

		/// <summary>
		/// Gets available service providers for buying eDaler.
		/// </summary>
		/// <returns>Available service providers.</returns>
		public async Task<IBuyEDalerServiceProvider[]> GetServiceProvidersForBuyingEDalerAsync()
		{
			return await this.EDalerClient.GetServiceProvidersForBuyingEDalerAsync();
		}

		/// <summary>
		/// Initiates the process of getting available options for buying of eDaler using a service provider that
		/// uses a smart contract.
		/// </summary>
		/// <param name="ServiceId">Service ID</param>
		/// <param name="ServiceProvider">Service Provider</param>
		/// <returns>Transaction ID</returns>
		public async Task<OptionsTransaction> InitiateBuyEDalerGetOptions(string ServiceId, string ServiceProvider)
		{
			string TransactionId = Guid.NewGuid().ToString();
			string SuccessUrl = GenerateNeuroAccessUrl(
				new KeyValuePair<string, object?>("cmd", "beos"),
				new KeyValuePair<string, object?>("tid", TransactionId),
				new KeyValuePair<string, object?>(JwtClaims.ClientId, ServiceRef.CryptoService.DeviceID),
				new KeyValuePair<string, object?>(JwtClaims.Issuer, ServiceRef.CryptoService.DeviceID),
				new KeyValuePair<string, object?>(JwtClaims.Subject, ServiceRef.XmppService.BareJid),
				new KeyValuePair<string, object?>(JwtClaims.ExpirationTime, (int)DateTime.UtcNow.AddHours(1).Subtract(JSON.UnixEpoch).TotalSeconds));
			string FailureUrl = GenerateNeuroAccessUrl(
				new KeyValuePair<string, object?>("cmd", "beof"),
				new KeyValuePair<string, object?>("tid", TransactionId),
				new KeyValuePair<string, object?>(JwtClaims.ClientId, ServiceRef.CryptoService.DeviceID),
				new KeyValuePair<string, object?>(JwtClaims.Issuer, ServiceRef.CryptoService.DeviceID),
				new KeyValuePair<string, object?>(JwtClaims.Subject, ServiceRef.XmppService.BareJid),
				new KeyValuePair<string, object?>(JwtClaims.ExpirationTime, (int)DateTime.UtcNow.AddHours(1).Subtract(JSON.UnixEpoch).TotalSeconds));
			string CancelUrl = GenerateNeuroAccessUrl(
				new KeyValuePair<string, object?>("cmd", "beoc"),
				new KeyValuePair<string, object?>("tid", TransactionId),
				new KeyValuePair<string, object?>(JwtClaims.ClientId, ServiceRef.CryptoService.DeviceID),
				new KeyValuePair<string, object?>(JwtClaims.Issuer, ServiceRef.CryptoService.DeviceID),
				new KeyValuePair<string, object?>(JwtClaims.Subject, ServiceRef.XmppService.BareJid),
				new KeyValuePair<string, object?>(JwtClaims.ExpirationTime, (int)DateTime.UtcNow.AddHours(1).Subtract(JSON.UnixEpoch).TotalSeconds));

			TransactionId = await this.EDalerClient.InitiateGetOptionsBuyEDalerAsync(ServiceId, ServiceProvider, TransactionId, SuccessUrl, FailureUrl, CancelUrl);
			OptionsTransaction Result = new(TransactionId);

			lock (this.currentTransactions)
			{
				this.currentTransactions[TransactionId] = Result;
			}

			return Result;
		}

		private async Task NeuroWallet_BuyEDalerOptionsClientUrlReceived(object? Sender, BuyEDalerClientUrlEventArgs e)
		{
			lock (this.currentTransactions)
			{
				if (!this.currentTransactions.ContainsKey(e.TransactionId))
				{
					ServiceRef.LogService.LogWarning("Client URL message for getting options for buying eDaler ignored. Transaction ID not recognized.",
						new KeyValuePair<string, object?>("TransactionId", e.TransactionId),
						new KeyValuePair<string, object?>("ClientUrl", e.ClientUrl));
					return;
				}
			}

			await Wallet.Transaction.OpenUrl(e.ClientUrl);
		}

		private Task NeuroWallet_BuyEDalerOptionsCompleted(object? _, PaymentOptionsEventArgs e)
		{
			this.BuyEDalerGetOptionsCompleted(e.TransactionId, e.Options);
			return Task.CompletedTask;
		}

		/// <summary>
		/// Registers an initiated getting of payment options for buying eDaler as completed.
		/// </summary>
		/// <param name="TransactionId">Transaction ID</param>
		/// <param name="Options">Available options.</param>
		public void BuyEDalerGetOptionsCompleted(string TransactionId, IDictionary<CaseInsensitiveString, object>[] Options)
		{
			Wallet.Transaction? Transaction;

			lock (this.currentTransactions)
			{
				if (!this.currentTransactions.TryGetValue(TransactionId, out Transaction))
					return;

				this.currentTransactions.Remove(TransactionId);
			}

			if (Transaction is OptionsTransaction OptionsTransaction)
				OptionsTransaction.Completed(Options);
		}

		private Task NeuroWallet_BuyEDalerOptionsError(object? _, PaymentErrorEventArgs e)
		{
			this.BuyEDalerGetOptionsFailed(e.TransactionId, e.Message);
			return Task.CompletedTask;
		}

		/// <summary>
		/// Registers an initiated getting of payment options for buying eDaler as failed.
		/// </summary>
		/// <param name="TransactionId">Transaction ID</param>
		/// <param name="Message">Error message.</param>
		public void BuyEDalerGetOptionsFailed(string TransactionId, string Message)
		{
			Wallet.Transaction? Transaction;

			lock (this.currentTransactions)
			{
				if (!this.currentTransactions.TryGetValue(TransactionId, out Transaction))
					return;

				this.currentTransactions.Remove(TransactionId);
			}

			Transaction.ErrorReported(Message);
		}

		/// <summary>
		/// Initiates the buying of eDaler using a service provider that does not use a smart contract.
		/// </summary>
		/// <param name="ServiceId">Service ID</param>
		/// <param name="ServiceProvider">Service Provider</param>
		/// <param name="Amount">Amount</param>
		/// <param name="Currency">Currency</param>
		/// <returns>Transaction ID</returns>
		public async Task<PaymentTransaction> InitiateBuyEDaler(string ServiceId, string ServiceProvider, decimal Amount, string Currency)
		{
			string TransactionId = Guid.NewGuid().ToString();
			string SuccessUrl = GenerateNeuroAccessUrl(
				new KeyValuePair<string, object?>("cmd", "bes"),
				new KeyValuePair<string, object?>("tid", TransactionId),
				new KeyValuePair<string, object?>("amt", Amount),
				new KeyValuePair<string, object?>("cur", Currency),
				new KeyValuePair<string, object?>(JwtClaims.ClientId, ServiceRef.CryptoService.DeviceID),
				new KeyValuePair<string, object?>(JwtClaims.Issuer, ServiceRef.CryptoService.DeviceID),
				new KeyValuePair<string, object?>(JwtClaims.Subject, ServiceRef.XmppService.BareJid),
				new KeyValuePair<string, object?>(JwtClaims.ExpirationTime, (int)DateTime.UtcNow.AddHours(1).Subtract(JSON.UnixEpoch).TotalSeconds));
			string FailureUrl = GenerateNeuroAccessUrl(
				new KeyValuePair<string, object?>("cmd", "bef"),
				new KeyValuePair<string, object?>("tid", TransactionId),
				new KeyValuePair<string, object?>(JwtClaims.ClientId, ServiceRef.CryptoService.DeviceID),
				new KeyValuePair<string, object?>(JwtClaims.Issuer, ServiceRef.CryptoService.DeviceID),
				new KeyValuePair<string, object?>(JwtClaims.Subject, ServiceRef.XmppService.BareJid),
				new KeyValuePair<string, object?>(JwtClaims.ExpirationTime, (int)DateTime.UtcNow.AddHours(1).Subtract(JSON.UnixEpoch).TotalSeconds));
			string CancelUrl = GenerateNeuroAccessUrl(
				new KeyValuePair<string, object?>("cmd", "bec"),
				new KeyValuePair<string, object?>("tid", TransactionId),
				new KeyValuePair<string, object?>(JwtClaims.ClientId, ServiceRef.CryptoService.DeviceID),
				new KeyValuePair<string, object?>(JwtClaims.Issuer, ServiceRef.CryptoService.DeviceID),
				new KeyValuePair<string, object?>(JwtClaims.Subject, ServiceRef.XmppService.BareJid),
				new KeyValuePair<string, object?>(JwtClaims.ExpirationTime, (int)DateTime.UtcNow.AddHours(1).Subtract(JSON.UnixEpoch).TotalSeconds));

			TransactionId = await this.EDalerClient.InitiateBuyEDalerAsync(ServiceId, ServiceProvider, Amount, Currency, TransactionId, SuccessUrl, FailureUrl, CancelUrl);
			PaymentTransaction Result = new(TransactionId, Currency);

			lock (this.currentTransactions)
			{
				this.currentTransactions[TransactionId] = Result;
			}

			return Result;
		}

		private static string GenerateNeuroAccessUrl(params KeyValuePair<string, object?>[] Claims)
		{
			string Token = ServiceRef.CryptoService.GenerateJwtToken(Claims);
			return Constants.UriSchemes.NeuroAccess + ":" + Token;
		}

		private async Task NeuroWallet_BuyEDalerClientUrlReceived(object? Sender, BuyEDalerClientUrlEventArgs e)
		{
			lock (this.currentTransactions)
			{
				if (!this.currentTransactions.ContainsKey(e.TransactionId))
				{
					ServiceRef.LogService.LogWarning("Client URL message for buying eDaler ignored. Transaction ID not recognized.",
						new KeyValuePair<string, object?>("TransactionId", e.TransactionId),
						new KeyValuePair<string, object?>("ClientUrl", e.ClientUrl));
					return;
				}
			}

			await Wallet.Transaction.OpenUrl(e.ClientUrl);
		}

		private Task NeuroWallet_BuyEDalerCompleted(object? _, PaymentCompletedEventArgs e)
		{
			this.BuyEDalerCompleted(e.TransactionId, e.Amount, e.Currency);
			return Task.CompletedTask;
		}

		/// <summary>
		/// Registers an initiated payment as completed.
		/// </summary>
		/// <param name="TransactionId">Transaction ID</param>
		/// <param name="Amount">Amount</param>
		/// <param name="Currency">Currency</param>
		public void BuyEDalerCompleted(string TransactionId, decimal Amount, string Currency)
		{
			Wallet.Transaction? Transaction;

			lock (this.currentTransactions)
			{
				if (!this.currentTransactions.TryGetValue(TransactionId, out Transaction))
					return;

				this.currentTransactions.Remove(TransactionId);
			}

			if (Transaction is PaymentTransaction PaymentTransaction)
				PaymentTransaction.Completed(Amount, Currency);
		}

		private Task NeuroWallet_BuyEDalerError(object? _, PaymentErrorEventArgs e)
		{
			this.BuyEDalerFailed(e.TransactionId, e.Message);
			return Task.CompletedTask;
		}

		/// <summary>
		/// Registers an initiated payment as failed.
		/// </summary>
		/// <param name="TransactionId">Transaction ID</param>
		/// <param name="Message">Error message.</param>
		public void BuyEDalerFailed(string TransactionId, string Message)
		{
			Wallet.Transaction? Transaction;

			lock (this.currentTransactions)
			{
				if (!this.currentTransactions.TryGetValue(TransactionId, out Transaction))
					return;

				this.currentTransactions.Remove(TransactionId);
			}

			Transaction.ErrorReported(Message);
		}

		/// <summary>
		/// Gets available service providers for selling eDaler.
		/// </summary>
		/// <returns>Available service providers.</returns>
		public async Task<ISellEDalerServiceProvider[]> GetServiceProvidersForSellingEDalerAsync()
		{
			return await this.EDalerClient.GetServiceProvidersForSellingEDalerAsync();
		}

		/// <summary>
		/// Initiates the process of getting available options for selling of eDaler using a service provider that
		/// uses a smart contract.
		/// </summary>
		/// <param name="ServiceId">Service ID</param>
		/// <param name="ServiceProvider">Service Provider</param>
		/// <returns>Transaction ID</returns>
		public async Task<OptionsTransaction> InitiateSellEDalerGetOptions(string ServiceId, string ServiceProvider)
		{
			string TransactionId = Guid.NewGuid().ToString();
			string SuccessUrl = GenerateNeuroAccessUrl(
				new KeyValuePair<string, object?>("cmd", "seos"),
				new KeyValuePair<string, object?>("tid", TransactionId),
				new KeyValuePair<string, object?>(JwtClaims.ClientId, ServiceRef.CryptoService.DeviceID),
				new KeyValuePair<string, object?>(JwtClaims.Issuer, ServiceRef.CryptoService.DeviceID),
				new KeyValuePair<string, object?>(JwtClaims.Subject, ServiceRef.XmppService.BareJid),
				new KeyValuePair<string, object?>(JwtClaims.ExpirationTime, (int)DateTime.UtcNow.AddHours(1).Subtract(JSON.UnixEpoch).TotalSeconds));
			string FailureUrl = GenerateNeuroAccessUrl(
				new KeyValuePair<string, object?>("cmd", "seof"),
				new KeyValuePair<string, object?>("tid", TransactionId),
				new KeyValuePair<string, object?>(JwtClaims.ClientId, ServiceRef.CryptoService.DeviceID),
				new KeyValuePair<string, object?>(JwtClaims.Issuer, ServiceRef.CryptoService.DeviceID),
				new KeyValuePair<string, object?>(JwtClaims.Subject, ServiceRef.XmppService.BareJid),
				new KeyValuePair<string, object?>(JwtClaims.ExpirationTime, (int)DateTime.UtcNow.AddHours(1).Subtract(JSON.UnixEpoch).TotalSeconds));
			string CancelUrl = GenerateNeuroAccessUrl(
				new KeyValuePair<string, object?>("cmd", "seoc"),
				new KeyValuePair<string, object?>("tid", TransactionId),
				new KeyValuePair<string, object?>(JwtClaims.ClientId, ServiceRef.CryptoService.DeviceID),
				new KeyValuePair<string, object?>(JwtClaims.Issuer, ServiceRef.CryptoService.DeviceID),
				new KeyValuePair<string, object?>(JwtClaims.Subject, ServiceRef.XmppService.BareJid),
				new KeyValuePair<string, object?>(JwtClaims.ExpirationTime, (int)DateTime.UtcNow.AddHours(1).Subtract(JSON.UnixEpoch).TotalSeconds));

			TransactionId = await this.EDalerClient.InitiateGetOptionsSellEDalerAsync(ServiceId, ServiceProvider, TransactionId, SuccessUrl, FailureUrl, CancelUrl);
			OptionsTransaction Result = new(TransactionId);

			lock (this.currentTransactions)
			{
				this.currentTransactions[TransactionId] = Result;
			}

			return Result;
		}

		private async Task NeuroWallet_SellEDalerOptionsClientUrlReceived(object? Sender, SellEDalerClientUrlEventArgs e)
		{
			lock (this.currentTransactions)
			{
				if (!this.currentTransactions.ContainsKey(e.TransactionId))
				{
					ServiceRef.LogService.LogWarning("Client URL message for getting options for selling eDaler ignored. Transaction ID not recognized.",
						new KeyValuePair<string, object?>("TransactionId", e.TransactionId),
						new KeyValuePair<string, object?>("ClientUrl", e.ClientUrl));
					return;
				}
			}

			await Wallet.Transaction.OpenUrl(e.ClientUrl);
		}

		private Task NeuroWallet_SellEDalerOptionsCompleted(object? _, PaymentOptionsEventArgs e)
		{
			this.SellEDalerGetOptionsCompleted(e.TransactionId, e.Options);
			return Task.CompletedTask;
		}

		/// <summary>
		/// Registers an initiated getting of payment options for selling eDaler as completed.
		/// </summary>
		/// <param name="TransactionId">Transaction ID</param>
		/// <param name="Options">Available options.</param>
		public void SellEDalerGetOptionsCompleted(string TransactionId, IDictionary<CaseInsensitiveString, object>[] Options)
		{
			Wallet.Transaction? Transaction;

			lock (this.currentTransactions)
			{
				if (!this.currentTransactions.TryGetValue(TransactionId, out Transaction))
					return;

				this.currentTransactions.Remove(TransactionId);
			}

			if (Transaction is OptionsTransaction OptionsTransaction)
				OptionsTransaction.Completed(Options);
		}

		private Task NeuroWallet_SellEDalerOptionsError(object? _, PaymentErrorEventArgs e)
		{
			this.SellEDalerGetOptionsFailed(e.TransactionId, e.Message);
			return Task.CompletedTask;
		}

		/// <summary>
		/// Registers an initiated getting of payment options for selling eDaler as failed.
		/// </summary>
		/// <param name="TransactionId">Transaction ID</param>
		/// <param name="Message">Error message.</param>
		public void SellEDalerGetOptionsFailed(string TransactionId, string Message)
		{
			Wallet.Transaction? Transaction;

			lock (this.currentTransactions)
			{
				if (!this.currentTransactions.TryGetValue(TransactionId, out Transaction))
					return;

				this.currentTransactions.Remove(TransactionId);
			}

			Transaction.ErrorReported(Message);
		}

		/// <summary>
		/// Initiates the selling of eDaler using a service provider that does not use a smart contract.
		/// </summary>
		/// <param name="ServiceId">Service ID</param>
		/// <param name="ServiceProvider">Service Provider</param>
		/// <param name="Amount">Amount</param>
		/// <param name="Currency">Currency</param>
		/// <returns>Transaction ID</returns>
		public async Task<PaymentTransaction> InitiateSellEDaler(string ServiceId, string ServiceProvider, decimal Amount, string Currency)
		{
			string TransactionId = Guid.NewGuid().ToString();
			string SuccessUrl = GenerateNeuroAccessUrl(
				new KeyValuePair<string, object?>("cmd", "ses"),
				new KeyValuePair<string, object?>("tid", TransactionId),
				new KeyValuePair<string, object?>("amt", Amount),
				new KeyValuePair<string, object?>("cur", Currency),
				new KeyValuePair<string, object?>(JwtClaims.ClientId, ServiceRef.CryptoService.DeviceID),
				new KeyValuePair<string, object?>(JwtClaims.Issuer, ServiceRef.CryptoService.DeviceID),
				new KeyValuePair<string, object?>(JwtClaims.Subject, ServiceRef.XmppService.BareJid),
				new KeyValuePair<string, object?>(JwtClaims.ExpirationTime, (int)DateTime.UtcNow.AddHours(1).Subtract(JSON.UnixEpoch).TotalSeconds));
			string FailureUrl = GenerateNeuroAccessUrl(
				new KeyValuePair<string, object?>("cmd", "sef"),
				new KeyValuePair<string, object?>("tid", TransactionId),
				new KeyValuePair<string, object?>(JwtClaims.ClientId, ServiceRef.CryptoService.DeviceID),
				new KeyValuePair<string, object?>(JwtClaims.Issuer, ServiceRef.CryptoService.DeviceID),
				new KeyValuePair<string, object?>(JwtClaims.Subject, ServiceRef.XmppService.BareJid),
				new KeyValuePair<string, object?>(JwtClaims.ExpirationTime, (int)DateTime.UtcNow.AddHours(1).Subtract(JSON.UnixEpoch).TotalSeconds));
			string CancelUrl = GenerateNeuroAccessUrl(
				new KeyValuePair<string, object?>("cmd", "sec"),
				new KeyValuePair<string, object?>("tid", TransactionId),
				new KeyValuePair<string, object?>(JwtClaims.ClientId, ServiceRef.CryptoService.DeviceID),
				new KeyValuePair<string, object?>(JwtClaims.Issuer, ServiceRef.CryptoService.DeviceID),
				new KeyValuePair<string, object?>(JwtClaims.Subject, ServiceRef.XmppService.BareJid),
				new KeyValuePair<string, object?>(JwtClaims.ExpirationTime, (int)DateTime.UtcNow.AddHours(1).Subtract(JSON.UnixEpoch).TotalSeconds));

			TransactionId = await this.EDalerClient.InitiateSellEDalerAsync(ServiceId, ServiceProvider, Amount, Currency, TransactionId, SuccessUrl, FailureUrl, CancelUrl);
			PaymentTransaction Result = new(TransactionId, Currency);

			lock (this.currentTransactions)
			{
				this.currentTransactions[TransactionId] = Result;
			}

			return Result;
		}

		private async Task NeuroWallet_SellEDalerClientUrlReceived(object? Sender, SellEDalerClientUrlEventArgs e)
		{
			lock (this.currentTransactions)
			{
				if (!this.currentTransactions.ContainsKey(e.TransactionId))
				{
					ServiceRef.LogService.LogWarning("Client URL message ignored. Transaction ID not recognized.",
						new KeyValuePair<string, object?>("TransactionId", e.TransactionId),
						new KeyValuePair<string, object?>("ClientUrl", e.ClientUrl));
					return;
				}
			}

			await Wallet.Transaction.OpenUrl(e.ClientUrl);
		}

		private Task NeuroWallet_SellEDalerError(object? _, PaymentErrorEventArgs e)
		{
			this.SellEDalerFailed(e.TransactionId, e.Message);
			return Task.CompletedTask;
		}

		/// <summary>
		/// Registers an initiated payment as failed.
		/// </summary>
		/// <param name="TransactionId">Transaction ID</param>
		/// <param name="Message">Error message.</param>
		public void SellEDalerFailed(string TransactionId, string Message)
		{
			Wallet.Transaction? Transaction;

			lock (this.currentTransactions)
			{
				if (!this.currentTransactions.TryGetValue(TransactionId, out Transaction))
					return;

				this.currentTransactions.Remove(TransactionId);
			}

			Transaction.ErrorReported(Message);
		}

		private Task NeuroWallet_SellEDalerCompleted(object? _, PaymentCompletedEventArgs e)
		{
			this.SellEDalerCompleted(e.TransactionId, e.Amount, e.Currency);
			return Task.CompletedTask;
		}

		/// <summary>
		/// Registers an initiated payment as completed.
		/// </summary>
		/// <param name="TransactionId">Transaction ID</param>
		/// <param name="Amount">Amount</param>
		/// <param name="Currency">Currency</param>
		public void SellEDalerCompleted(string TransactionId, decimal Amount, string Currency)
		{
			Wallet.Transaction? Transaction;

			lock (this.currentTransactions)
			{
				if (!this.currentTransactions.TryGetValue(TransactionId, out Transaction))
					return;

				this.currentTransactions.Remove(TransactionId);
			}

			if (Transaction is PaymentTransaction PaymentTransaction)
				PaymentTransaction.Completed(Amount, Currency);
		}

		#endregion

		#region Neuro-Features

		private DateTime lastTokenEvent = DateTime.MinValue;

		/// <summary>
		/// Reference to the Neuro-Features client implementing the Neuro-Features XMPP extension
		/// </summary>
		private NeuroFeaturesClient NeuroFeaturesClient
		{
			get
			{
				if (this.neuroFeaturesClient is null)
					throw new InvalidOperationException(ServiceRef.Localizer[nameof(AppResources.NeuroFeaturesServiceNotFound)]);

				return this.neuroFeaturesClient;
			}
		}

		private void RegisterNeuroFeatureEventHandlers(NeuroFeaturesClient Client)
		{
			Client.TokenAdded += this.NeuroFeaturesClient_TokenAdded;
			Client.TokenRemoved += this.NeuroFeaturesClient_TokenRemoved;

			Client.StateUpdated += this.NeuroFeaturesClient_StateUpdated;
			Client.VariablesUpdated += this.NeuroFeaturesClient_VariablesUpdated;
		}

		/// <summary>
		/// Timepoint of last event.
		/// </summary>
		public DateTime LastNeuroFeatureEvent => this.lastTokenEvent;

		private async Task NeuroFeaturesClient_TokenRemoved(object _, NeuroFeatures.EventArguments.TokenEventArgs e)
		{
			this.lastTokenEvent = DateTime.Now;

			await this.NeuroFeatureRemoved.Raise(this, e);
		}

		/// <summary>
		/// Event raised when a token has been removed from the wallet.
		/// </summary>
		public event EventHandlerAsync<NeuroFeatures.EventArguments.TokenEventArgs>? NeuroFeatureRemoved;

		private async Task NeuroFeaturesClient_TokenAdded(object _, NeuroFeatures.EventArguments.TokenEventArgs e)
		{
			this.lastTokenEvent = DateTime.Now;

			await this.NeuroFeatureAdded.Raise(this, e);
		}

		/// <summary>
		/// Event raised when a token has been added to the wallet.
		/// </summary>
		public event EventHandlerAsync<NeuroFeatures.EventArguments.TokenEventArgs>? NeuroFeatureAdded;

		private async Task NeuroFeaturesClient_VariablesUpdated(object? _, VariablesUpdatedEventArgs e)
		{
			await this.NeuroFeatureVariablesUpdated.Raise(this, e);
		}

		/// <summary>
		/// Event raised when variables have been updated in a state-machine.
		/// </summary>
		public event EventHandlerAsync<VariablesUpdatedEventArgs>? NeuroFeatureVariablesUpdated;

		private async Task NeuroFeaturesClient_StateUpdated(object? _, NewStateEventArgs e)
		{
			await this.NeuroFeatureStateUpdated.Raise(this, e);
		}

		/// <summary>
		/// Event raised when a state-machine has received a new state.
		/// </summary>
		public event EventHandlerAsync<NewStateEventArgs>? NeuroFeatureStateUpdated;

		/// <summary>
		/// Gets available tokens
		/// </summary>
		/// <returns>Response with tokens.</returns>
		public Task<TokensEventArgs> GetNeuroFeatures()
		{
			return this.GetNeuroFeatures(0, int.MaxValue);
		}

		/// <summary>
		/// Gets a section of available tokens
		/// </summary>
		/// <param name="Offset">Start offset of list</param>
		/// <param name="MaxCount">Maximum number of items in response.</param>
		/// <returns>Response with tokens.</returns>
		public Task<TokensEventArgs> GetNeuroFeatures(int Offset, int MaxCount)
		{
			return this.NeuroFeaturesClient.GetTokensAsync(Offset, MaxCount);
		}

		/// <summary>
		/// Gets references to available tokens
		/// </summary>
		/// <returns>Response with tokens.</returns>
		public Task<string[]> GetNeuroFeatureReferences()
		{
			return this.GetNeuroFeatureReferences(0, int.MaxValue);
		}

		/// <summary>
		/// Gets references to a section of available tokens
		/// </summary>
		/// <param name="Offset">Start offset of list</param>
		/// <param name="MaxCount">Maximum number of items in response.</param>
		/// <returns>Response with tokens.</returns>
		public Task<string[]> GetNeuroFeatureReferences(int Offset, int MaxCount)
		{
			return this.NeuroFeaturesClient.GetTokenReferencesAsync(Offset, MaxCount);
		}

		/// <summary>
		/// Gets the value totals of tokens available in the wallet, grouped and ordered by currency.
		/// </summary>
		/// <returns>Response with tokens.</returns>
		public Task<TokenTotalsEventArgs> GetNeuroFeatureTotals()
		{
			return this.NeuroFeaturesClient.GetTotalsAsync();
		}

		/// <summary>
		/// Gets tokens created by a smart contract
		/// </summary>
		/// <param name="ContractId">Contract ID</param>
		/// <returns>Response with tokens.</returns>
		public Task<TokensEventArgs> GetNeuroFeaturesForContract(string ContractId)
		{
			return this.NeuroFeaturesClient.GetContractTokensAsync(ContractId);
		}

		/// <summary>
		/// Gets tokens created by a smart contract
		/// </summary>
		/// <param name="ContractId">Contract ID</param>
		/// <param name="Offset">Start offset of list</param>
		/// <param name="MaxCount">Maximum number of items in response.</param>
		/// <returns>Response with tokens.</returns>
		public Task<TokensEventArgs> GetNeuroFeaturesForContract(string ContractId, int Offset, int MaxCount)
		{
			return this.NeuroFeaturesClient.GetContractTokensAsync(ContractId, Offset, MaxCount);
		}

		/// <summary>
		/// Gets token references created by a smart contract
		/// </summary>
		/// <param name="ContractId">Contract ID</param>
		/// <returns>Response with tokens.</returns>
		public Task<string[]> GetNeuroFeatureReferencesForContract(string ContractId)
		{
			return this.NeuroFeaturesClient.GetContractTokenReferencesAsync(ContractId);
		}

		/// <summary>
		/// Gets token references created by a smart contract
		/// </summary>
		/// <param name="ContractId">Contract ID</param>
		/// <param name="Offset">Start offset of list</param>
		/// <param name="MaxCount">Maximum number of items in response.</param>
		/// <returns>Response with tokens.</returns>
		public Task<string[]> GetNeuroFeatureReferencesForContract(string ContractId, int Offset, int MaxCount)
		{
			return this.NeuroFeaturesClient.GetContractTokenReferencesAsync(ContractId, Offset, MaxCount);
		}

		/// <summary>
		/// Gets a specific token.
		/// </summary>
		/// <param name="TokenId">Token ID</param>
		/// <returns>Token</returns>
		public Task<Token> GetNeuroFeature(string TokenId)
		{
			return this.NeuroFeaturesClient.GetTokenAsync(TokenId);
		}

		/// <summary>
		/// Gets events relating to a specific token.
		/// </summary>
		/// <param name="TokenId">Token ID</param>
		/// <returns>Token events.</returns>
		public Task<TokenEvent[]> GetNeuroFeatureEvents(string TokenId)
		{
			return this.GetNeuroFeatureEvents(TokenId, 0, int.MaxValue);
		}

		/// <summary>
		/// Gets events relating to a specific token.
		/// </summary>
		/// <param name="TokenId">Token ID</param>
		/// <param name="Offset">Offset </param>
		/// <param name="MaxCount">Maximum number of events to return.</param>
		/// <returns>Token events.</returns>
		public Task<TokenEvent[]> GetNeuroFeatureEvents(string TokenId, int Offset, int MaxCount)
		{
			return this.NeuroFeaturesClient.GetEventsAsync(TokenId, Offset, MaxCount);
		}

		/// <summary>
		/// Adds a text note on a token.
		/// </summary>
		/// <param name="TokenId">Token ID</param>
		/// <param name="TextNote">Text Note</param>
		public Task AddNeuroFeatureTextNote(string TokenId, string TextNote)
		{
			return this.AddNeuroFeatureTextNote(TokenId, TextNote, false);
		}

		/// <summary>
		/// Adds a text note on a token.
		/// </summary>
		/// <param name="TokenId">Token ID</param>
		/// <param name="TextNote">Text Note</param>
		/// <param name="Personal">If the text note contains personal information. (default=false).
		///
		/// Note: Personal notes are deleted when ownership of token is transferred.</param>
		public Task AddNeuroFeatureTextNote(string TokenId, string TextNote, bool Personal)
		{
			this.lastTokenEvent = DateTime.Now;

			return this.NeuroFeaturesClient.AddTextNoteAsync(TokenId, TextNote, Personal);
		}

		/// <summary>
		/// Adds a xml note on a token.
		/// </summary>
		/// <param name="TokenId">Token ID</param>
		/// <param name="XmlNote">Xml Note</param>
		public Task AddNeuroFeatureXmlNote(string TokenId, string XmlNote)
		{
			return this.AddNeuroFeatureXmlNote(TokenId, XmlNote, false);
		}

		/// <summary>
		/// Adds a xml note on a token.
		/// </summary>
		/// <param name="TokenId">Token ID</param>
		/// <param name="XmlNote">Xml Note</param>
		/// <param name="Personal">If the xml note contains personal information. (default=false).
		///
		/// Note: Personal notes are deleted when ownership of token is transferred.</param>
		public Task AddNeuroFeatureXmlNote(string TokenId, string XmlNote, bool Personal)
		{
			this.lastTokenEvent = DateTime.Now;

			return this.NeuroFeaturesClient.AddXmlNoteAsync(TokenId, XmlNote, Personal);
		}

		/// <summary>
		/// Gets token creation attributes from the broker.
		/// </summary>
		/// <returns>Token creation attributes.</returns>
		public Task<CreationAttributesEventArgs> GetNeuroFeatureCreationAttributes()
		{
			return this.NeuroFeaturesClient.GetCreationAttributesAsync();
		}

		/// <summary>
		/// Generates a XAML report for a state diagram corresponding to the token.
		/// </summary>
		/// <returns>String-representation of XAML of report.</returns>
		public async Task<string> GenerateNeuroFeatureStateDiagramReport(string TokenId)
		{
			ReportEventArgs e = await this.NeuroFeaturesClient.GenerateStateDiagramAsync(TokenId, ReportFormat.Markdown);
			if (!e.Ok)
				throw e.StanzaError ?? new Exception(ServiceRef.Localizer[nameof(AppResources.UnableToGetStateDiagram)]);

			return await e.ReportText.MarkdownToXaml();
		}

		/// <summary>
		/// Generates a XAML report for a timing diagram corresponding to the token.
		/// </summary>
		/// <returns>String-representation of XAML of report.</returns>
		public async Task<string> GenerateNeuroFeatureProfilingReport(string TokenId)
		{
			ReportEventArgs e = await this.NeuroFeaturesClient.GenerateProfilingReportAsync(TokenId, ReportFormat.Markdown);
			if (!e.Ok)
				throw e.StanzaError ?? new Exception(ServiceRef.Localizer[nameof(AppResources.UnableToGetProfiling)]);

			return await e.ReportText.MarkdownToXaml();
		}

		/// <summary>
		/// Generates a XAML present report for a token.
		/// </summary>
		/// <returns>String-representation of XAML of report.</returns>
		public async Task<string> GenerateNeuroFeaturePresentReport(string TokenId)
		{
			ReportEventArgs e = await this.NeuroFeaturesClient.GeneratePresentReportAsync(TokenId, ReportFormat.Markdown);
			if (!e.Ok)
				throw e.StanzaError ?? new Exception(ServiceRef.Localizer[nameof(AppResources.UnableToGetPresent)]);

			return await e.ReportText.MarkdownToXaml();
		}

		/// <summary>
		/// Generates a XAML history report for a token.
		/// </summary>
		/// <returns>String-representation of XAML of report.</returns>
		public async Task<string> GenerateNeuroFeatureHistoryReport(string TokenId)
		{
			ReportEventArgs e = await this.NeuroFeaturesClient.GenerateHistoryReportAsync(TokenId, ReportFormat.Markdown);
			if (!e.Ok)
				throw e.StanzaError ?? new Exception(ServiceRef.Localizer[nameof(AppResources.UnableToGetHistory)]);

			return await e.ReportText.MarkdownToXaml();
		}

		/// <summary>
		/// Gets the current state of a Neuro-Feature token.
		/// </summary>
		/// <param name="TokenId">Token ID</param>
		/// <returns>Current state</returns>
		public Task<CurrentStateEventArgs> GetNeuroFeatureCurrentState(string TokenId)
		{
			return this.NeuroFeaturesClient.GetCurrentStateAsync(TokenId);
		}

		#endregion

		#region Private XML

		/// <summary>
		/// Saves Private XML to the server. Private XML are separated by
		/// Local Name and Namespace of the root element. Only one document
		/// per fully qualified name. When saving private XML, the XML overwrites
		/// any existing XML having the same local name and namespace.
		/// </summary>
		/// <param name="Xml">XML to save.</param>
		public Task SavePrivateXml(string Xml)
		{
			return this.xmppClient?.SetPrivateXmlElementAsync(Xml)
				?? throw new Exception("Not connected to XMPP network.");
		}

		/// <summary>
		/// Saves Private XML to the server. Private XML are separated by
		/// Local Name and Namespace of the root element. Only one document
		/// per fully qualified name. When saving private XML, the XML overwrites
		/// any existing XML having the same local name and namespace.
		/// </summary>
		/// <param name="Xml">XML to save.</param>
		public Task SavePrivateXml(XmlElement Xml)
		{
			return this.xmppClient?.SetPrivateXmlElementAsync(Xml)
				?? throw new Exception("Not connected to XMPP network.");
		}

		/// <summary>
		/// Loads private XML previously stored, given the local name and
		/// namespace of the XML.
		/// </summary>
		/// <param name="LocalName">Local Name</param>
		/// <param name="Namespace">Namespace</param>
		/// <returns>XML Element, if found.</returns>
		public async Task<XmlElement?> LoadPrivateXml(string LocalName, string Namespace)
		{
			return await this.XmppClient.GetPrivateXmlElementAsync(LocalName, Namespace);
		}

		/// <summary>
		/// Deletes private XML previously saved to the account.
		/// </summary>
		/// <param name="LocalName">Local Name</param>
		/// <param name="Namespace">Namespace</param>
		public Task DeletePrivateXml(string LocalName, string Namespace)
		{
			StringBuilder Xml = new();

			Xml.Append('<');
			Xml.Append(XML.Encode(LocalName));
			Xml.Append(" xmlns='");
			Xml.Append(XML.Encode(Namespace));
			Xml.Append("'/>");
			return this.SavePrivateXml(Xml.ToString());
		}

		#endregion

	}
}
