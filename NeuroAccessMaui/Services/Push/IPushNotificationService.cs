using Waher.Events;
using Waher.Networking.XMPP.Push;
using Waher.Runtime.Inventory;

namespace NeuroAccessMaui.Services.Push
{

	/// <summary>
	/// Interface for push notification services.
	/// </summary>
	[DefaultImplementation(typeof(PushNotificationService))]
	public interface IPushNotificationService
	{
		/// <summary>
		/// New token received from push notification back-end.
		/// </summary>
		/// <param name="TokenInformation">Token information.</param>
		Task NewToken(TokenInformation TokenInformation);

		/// <summary>
		/// Tries to get a token from a push notification service.
		/// </summary>
		/// <param name="Source">Source of token</param>
		/// <param name="Token">Token, if found.</param>
		/// <returns>If a token was found for the corresponding source.</returns>
		bool TryGetToken(PushMessagingService Source, out string? Token);

		/// <summary>
		/// Event raised when a new token is made available.
		/// </summary>
		event EventHandlerAsync<TokenEventArgs>? OnNewToken;

		/// <summary>
		/// Checks if the Push Notification Token is current and registered properly.
		/// </summary>
		/// <param name="TokenInformation">Non null if we got it from the OnNewToken</param>
		Task CheckPushNotificationToken(TokenInformation? TokenInformation = null);
	}
}
