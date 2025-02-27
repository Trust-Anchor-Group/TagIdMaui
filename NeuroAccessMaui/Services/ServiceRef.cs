using System.Diagnostics;
using System.Dynamic;
using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.Localization;
using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services.AttachmentCache;
using NeuroAccessMaui.Services.Contracts;
using NeuroAccessMaui.Services.Crypto;
using NeuroAccessMaui.Services.EventLog;
using NeuroAccessMaui.Services.Localization;
using NeuroAccessMaui.Services.Network;
using NeuroAccessMaui.Services.Nfc;
using NeuroAccessMaui.Services.Notification;
using NeuroAccessMaui.Services.AppPermissions;
using NeuroAccessMaui.Services.Push;
using NeuroAccessMaui.Services.Settings;
using NeuroAccessMaui.Services.Storage;
using NeuroAccessMaui.Services.Tag;
using NeuroAccessMaui.Services.ThingRegistries;
using NeuroAccessMaui.Services.UI;
using NeuroAccessMaui.Services.Wallet;
using NeuroAccessMaui.Services.Xmpp;
using ZXing;

namespace NeuroAccessMaui.Services
{
	/// <summary>
	/// Base class that references services in the app.
	/// </summary>
	public static class ServiceRef
	{
		private static IXmppService? xmppService;
		private static IUiService? uiService;
		private static ITagProfile? tagProfile;
		private static ILogService? logService;
		private static INetworkService? networkService;
		private static IContractOrchestratorService? contractOrchestratorService;
		private static IThingRegistryOrchestratorService? thingRegistryOrchestratorService;
		private static INeuroWalletOrchestratorService? neuroWalletOrchestratorService;
		private static IAttachmentCacheService? attachmentCacheService;
		private static ICryptoService? cryptoService;
		private static ISettingsService? settingsService;
		private static IStorageService? storageService;
		private static INfcService? nfcService;
		private static INotificationService? notificationService;
		private static IPushNotificationService? pushNotificationService;
		private static IStringLocalizer? localizer;
		private static IPlatformSpecific? platformSpecific;
		private static IBarcodeReader? barcodeReader;
		private static IPermissionService? permissionService;

		/// <summary>
		/// Service serializing and managing UI-related tasks.
		/// </summary>
		public static IUiService UiService
		{
			get
			{
				uiService ??= App.Instantiate<IUiService>();
				return uiService;
			}
		}

		/// <summary>
		/// The XMPP service for XMPP communication.
		/// </summary>
		public static IXmppService XmppService
		{
			get
			{
				xmppService ??= App.Instantiate<IXmppService>();
				return xmppService;
			}
		}

		/// <summary>
		/// TAG Profile service.
		/// </summary>
		public static ITagProfile TagProfile
		{
			get
			{
				tagProfile ??= App.Instantiate<ITagProfile>();
				return tagProfile;
			}
		}

		/// <summary>
		/// Log service.
		/// </summary>
		public static ILogService LogService
		{
			get
			{
				logService ??= App.Instantiate<ILogService>();
				return logService;
			}
		}

		/// <summary>
		/// Network service.
		/// </summary>
		public static INetworkService NetworkService
		{
			get
			{
				networkService ??= App.Instantiate<INetworkService>();
				return networkService;
			}
		}

		/// <summary>
		/// Contract orchestrator service.
		/// </summary>
		public static IContractOrchestratorService ContractOrchestratorService
		{
			get
			{
				contractOrchestratorService ??= App.Instantiate<IContractOrchestratorService>();
				return contractOrchestratorService;
			}
		}

		/// <summary>
		/// Thing Registry orchestrator service.
		/// </summary>
		public static IThingRegistryOrchestratorService ThingRegistryOrchestratorService
		{
			get
			{
				thingRegistryOrchestratorService ??= App.Instantiate<IThingRegistryOrchestratorService>();
				return thingRegistryOrchestratorService;
			}
		}

		/// <summary>
		/// Neuro Wallet orchestrator service.
		/// </summary>
		public static INeuroWalletOrchestratorService NeuroWalletOrchestratorService
		{
			get
			{
				neuroWalletOrchestratorService ??= App.Instantiate<INeuroWalletOrchestratorService>();
				return neuroWalletOrchestratorService;
			}
		}

		/// <summary>
		/// AttachmentCache service.
		/// </summary>
		public static IAttachmentCacheService AttachmentCacheService
		{
			get
			{
				attachmentCacheService ??= App.Instantiate<IAttachmentCacheService>();
				return attachmentCacheService;
			}
		}

		/// <summary>
		/// Crypto service.
		/// </summary>
		public static ICryptoService CryptoService
		{
			get
			{
				cryptoService ??= App.Instantiate<ICryptoService>();
				return cryptoService;
			}
		}

		/// <summary>
		/// Settings service.
		/// </summary>
		public static ISettingsService SettingsService
		{
			get
			{
				settingsService ??= App.Instantiate<ISettingsService>();
				return settingsService;
			}
		}

		/// <summary>
		/// Storage service.
		/// </summary>
		public static IStorageService StorageService
		{
			get
			{
				storageService ??= App.Instantiate<IStorageService>();
				return storageService;
			}
		}

		/// <summary>
		/// Near-Field Communication (NFC) service.
		/// </summary>
		public static INfcService NfcService
		{
			get
			{
				nfcService ??= App.Instantiate<INfcService>();
				return nfcService;
			}
		}

		/// <summary>
		/// Service for managing notifications for the user.
		/// </summary>
		public static INotificationService NotificationService
		{
			get
			{
				notificationService ??= App.Instantiate<INotificationService>();
				return notificationService;
			}
		}

		/// <summary>
		/// Service for managing push notifications from the network.
		/// </summary>
		public static IPushNotificationService PushNotificationService
		{
			get
			{
				pushNotificationService ??= App.Instantiate<IPushNotificationService>();
				return pushNotificationService;
			}
		}

		/// <summary>
		/// Permission Service
		/// </summary>
		public static IPermissionService PermissionService
		{
			get
			{
				permissionService ??= App.Instantiate<IPermissionService>();
				return permissionService;
			}
		}

		/// <summary>
		/// Localization service
		/// </summary>
		public static IStringLocalizer Localizer
		{
			get
			{
				localizer ??= new LocalizerReportingMissingStrings(LocalizationManager.GetStringLocalizer<AppResources>());
				return localizer;
			}
		}

		/// <summary>
		/// Localizer, that reports missing strings to the operator of the corrected broker, via the event log.
		/// </summary>
		/// <param name="Localizer">Base localizer.</param>
		private class LocalizerReportingMissingStrings(IStringLocalizer Localizer) : IStringLocalizer
		{
			private readonly IStringLocalizer localizer = Localizer;

			public LocalizedString this[string Name]
			{
				get
				{
					LocalizedString Result = this.localizer[Name];
					if (Result is not null && !Result.ResourceNotFound)
						return Result;

					StackTrace Trace = new();
					Type Caller = typeof(ServiceRef);
					int i, c = Trace.FrameCount;
					Assembly ThisAssembly = typeof(App).Assembly;

					for (i = 1; i < c; i++)
					{
						Type? T = Trace.GetFrame(i)?.GetMethod()?.DeclaringType;
						if (T is null)
							continue;

						if (T.Assembly.FullName == ThisAssembly.FullName)
						{
							if (T == typeof(LocalizerReportingMissingStrings))
								continue;

							if (T.IsConstructedGenericType)
								T = T.GetGenericTypeDefinition();

							Caller = T;
							break;
						}
					}

					LocalizeExtension.ReportMissingString(Name, Caller);

					return new LocalizedString(Name, Name, true);
				}
			}

			public LocalizedString this[string Name, params object[] Arguments]
			{
				get
				{
					LocalizedString Result = this[Name];
					if (Result.ResourceNotFound)
						return Result;

					return new LocalizedString(Name, string.Format(CultureInfo.CurrentCulture, Result.Value, Arguments));
				}
			}

			public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
			{
				return this.localizer.GetAllStrings(includeParentCultures);
			}
		}

		/// <summary>
		/// Localization service
		/// </summary>
		public static IPlatformSpecific PlatformSpecific
		{
			get
			{
				platformSpecific ??= ServiceHelper.GetService<IPlatformSpecific>();
				return platformSpecific;
			}
		}


		/// <summary>
		/// Localization service
		/// </summary>
		public static IBarcodeReader BarcodeReader
		{
			get
			{
				barcodeReader ??= ServiceHelper.GetService<IBarcodeReader>();
				return barcodeReader;
			}
		}
	}
}
