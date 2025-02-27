using Waher.Networking.XMPP.Push;

namespace NeuroAccessMaui.Services.Push
{
	/// <summary>
	/// Event argumens for token-based events.
	/// </summary>
	/// <param name="Source">Source of notification</param>
	/// <param name="Token">Token</param>
	/// <param name="ClientType">Client Type</param>
	public class TokenEventArgs(PushMessagingService Source, string Token, ClientType ClientType)
		: EventArgs()
	{
		/// <summary>
		/// Source of notification
		/// </summary>
		public PushMessagingService Source { get; } = Source;

		/// <summary>
		/// Token
		/// </summary>
		public string Token { get; } = Token;

		/// <summary>
		/// Client Type
		/// </summary>
		public ClientType ClientType { get; } = ClientType;
	}
}
