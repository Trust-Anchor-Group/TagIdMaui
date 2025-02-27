using System.Security.Cryptography.X509Certificates;
using NeuroAccessMaui.Services.Contacts;
using Waher.Networking.XMPP.Provisioning;
using Waher.Networking.XMPP.Provisioning.Events;
using Waher.Persistence;
using Waher.Persistence.Attributes;

namespace NeuroAccessMaui.Services.Notification.Things
{
	/// <summary>
	/// Abstract base class of thing notification events.
	/// </summary>
	public abstract class ThingNotificationEvent : ProvisioningNotificationEvent
	{
		/// <summary>
		/// Abstract base class of thing notification events.
		/// </summary>
		public ThingNotificationEvent()
			: base()
		{
		}

		/// <summary>
		/// Abstract base class of thing notification events.
		/// </summary>
		public ThingNotificationEvent(NodeQuestionEventArgs e)
			: base(e)
		{
			this.NodeId = e.NodeId;
			this.PartitionId = e.Partition;
			this.SourceId = e.SourceId;
			this.ServiceTokens = Convert(e.ServiceTokens, TokenType.Service);
			this.DeviceTokens = Convert(e.DeviceTokens, TokenType.Device);
			this.UserTokens = Convert(e.UserTokens, TokenType.User);
			this.Category = ContactInfo.GetThingNotificationCategoryKey(this.BareJid, this.NodeId, this.SourceId, this.PartitionId);
		}

		private static ProvisioningToken[]? Convert(string[] Tokens, TokenType Type)
		{
			if (Tokens is null)
				return null;

			int i, c = Tokens.Length;
			ProvisioningToken[] Result = new ProvisioningToken[c];

			for (i = 0; i < c; i++)
				Result[i] = new ProvisioningToken(Tokens[i], Type);

			return Result;
		}

		/// <summary>
		/// Node ID of thing.
		/// </summary>
		[DefaultValueStringEmpty]
		public string? NodeId { get; set; }

		/// <summary>
		/// Source ID of thing.
		/// </summary>
		[DefaultValueStringEmpty]
		public string? SourceId { get; set; }

		/// <summary>
		/// Partition ID of thing.
		/// </summary>
		[DefaultValueStringEmpty]
		public string? PartitionId { get; set; }

		/// <summary>
		/// User tokens
		/// </summary>
		[DefaultValueNull]
		public ProvisioningToken[]? UserTokens { get; set; }

		/// <summary>
		/// Service tokens
		/// </summary>
		[DefaultValueNull]
		public ProvisioningToken[]? ServiceTokens { get; set; }

		/// <summary>
		/// Device tokens
		/// </summary>
		[DefaultValueNull]
		public ProvisioningToken[]? DeviceTokens { get; set; }

		/// <summary>
		/// Performs perparatory tasks, that will simplify opening the notification.
		/// </summary>
		public override async Task Prepare()
		{
			await base.Prepare();

			await this.Prepare(this.UserTokens);
			await this.Prepare(this.DeviceTokens);
			await this.Prepare(this.ServiceTokens);
		}

		private async Task Prepare(ProvisioningToken[]? Tokens)
		{
			if (Tokens is null)
				return;

			bool Updated = false;

			foreach (ProvisioningToken Token in Tokens)
			{
				if (!string.IsNullOrEmpty(Token.FriendlyName))
					continue;

				lock (certificatesyByToken)
				{
					if (certificatesyByToken.TryGetValue(Token.Token, out X509Certificate2? Certificate))
					{
						Token.FriendlyName = Certificate.FriendlyName;
						Token.Certificate = Certificate.RawData;
						Updated = true;
						continue;
					}
				}

				ServiceRef.XmppService.GetCertificate(Token.Token, this.CertificateResponse, Token);
			}

			if (Updated)
				await Database.Update(this);
		}

		private static readonly Dictionary<string, X509Certificate2> certificatesyByToken = [];

		private async Task CertificateResponse(object? Sender, CertificateEventArgs e)
		{
			if (e.Ok && e.State is ProvisioningToken Token)
			{
				lock (certificatesyByToken)
				{
					certificatesyByToken[Token.Token] = e.Certificate;
				}

				Token.FriendlyName = e.Certificate.FriendlyName;
				Token.Certificate = e.Certificate.RawData;

				await Database.Update(this);
			}
		}
	}
}
