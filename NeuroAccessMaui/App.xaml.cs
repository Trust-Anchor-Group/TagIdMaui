using System.Globalization;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using EDaler;
using Microsoft.Maui.Controls.Internals;
using NeuroAccessMaui.Extensions;
using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services;
using NeuroAccessMaui.Services.AttachmentCache;
using NeuroAccessMaui.Services.Contracts;
using NeuroAccessMaui.Services.Crypto;
using NeuroAccessMaui.Services.EventLog;
using NeuroAccessMaui.Services.Localization;
using NeuroAccessMaui.Services.Network;
using NeuroAccessMaui.Services.Nfc;
using NeuroAccessMaui.Services.Notification;
using NeuroAccessMaui.Services.Settings;
using NeuroAccessMaui.Services.Storage;
using NeuroAccessMaui.Services.Tag;
using NeuroAccessMaui.Services.UI;
using NeuroAccessMaui.Services.UI.QR;
using NeuroAccessMaui.Services.Xmpp;
using NeuroAccessMaui.UI.Pages;
using NeuroAccessMaui.UI.Pages.Main;
using NeuroAccessMaui.UI.Popups.Password;
using NeuroFeatures;
using Waher.Content;
using Waher.Content.Images;
using Waher.Content.Markdown;
using Waher.Content.Xml;
using Waher.Events;
using Waher.Events.Persistence;
using Waher.Networking.DNS;
using Waher.Networking.XMPP;
using Waher.Networking.XMPP.Avatar;
using Waher.Networking.XMPP.Concentrator;
using Waher.Networking.XMPP.Contracts;
using Waher.Networking.XMPP.Control;
using Waher.Networking.XMPP.HTTPX;
using Waher.Networking.XMPP.Mail;
using Waher.Networking.XMPP.P2P;
using Waher.Networking.XMPP.P2P.E2E;
using Waher.Networking.XMPP.PEP;
using Waher.Networking.XMPP.Provisioning;
using Waher.Networking.XMPP.PubSub;
using Waher.Networking.XMPP.Push;
using Waher.Networking.XMPP.Sensor;
using Waher.Persistence;
using Waher.Persistence.Files;
using Waher.Persistence.Serialization;
using Waher.Runtime.Inventory;
using Waher.Runtime.Settings;
using Waher.Runtime.Text;
using Waher.Script;
using Waher.Script.Content;
using Waher.Script.Graphs;
using Waher.Security.JWS;
using Waher.Security.JWT;
using Waher.Security.LoginMonitor;
using Waher.Things;

namespace NeuroAccessMaui
{
	/// <summary>
	/// Represents an instance of the Neuro-Access app.
	/// </summary>
	public partial class App : Application, IDisposableAsync
	{
		#region Fields

		private readonly SemaphoreSlim autoSaveSemaphore = new(1, 1);
		private Timer? autoSaveTimer;
		private readonly Task<bool> initCompleted;
		private readonly SemaphoreSlim startupWorker = new(1, 1);
		private CancellationTokenSource startupCancellation;
		private bool isDisposed;

		private readonly LoginAuditor loginAuditor;

		private static App? appInstance;
		private static bool configLoaded;
		private static bool defaultInstantiated;
		private static DateTime savedStartTime = DateTime.MinValue;
		private static bool displayedPasswordPopup;
		private static int startupCounter;

		private static readonly TaskCompletionSource<bool> servicesSetup = new();
		private static readonly TaskCompletionSource<bool> defaultInstantiatedSource = new();

        /// <summary>
        /// Flag indicating if this instance is “resuming” an already-started app.
        /// 
        /// The App class is not actually a singleton. Each time Android MainActivity is destroyed and then created again, a new instance
        /// of the App class will be created, its OnStart method will be called and its OnResume method will not be called. This happens,
        /// for example, on Android when the user presses the back button and then navigates to the app again. However, the App class
        /// doesn't seem to work properly (should it?) when this happens (some chaos happens here and there), so we pretend that
        /// there is only one instance (see the references to onStartResumesApplication).
        /// </summary>
        private bool onStartResumesApplication;

		#endregion

		#region Properties

		/// <summary>
		/// Gets the current application instance.
		/// </summary>
		public static new App? Current => appInstance;

		/// <summary>
		/// Supported languages.
		/// </summary>
		public static readonly LanguageInfo[] SupportedLanguages =
		[
			new("en", "English"),
			new("sv", "svenska"),
			new("es", "español"),
			new("fr", "français"),
			new("de", "Deutsch"),
			new("da", "dansk"),
			new("no", "norsk"),
			new("fi", "suomi"),
			new("sr", "српски"),
			new("pt", "português"),
			new("ro", "română"),
			new("ru", "русский")
		];

		/// <summary>
		/// Gets the selected language.
		/// </summary>
		public static LanguageInfo SelectedLanguage
		{
			get
			{
				string? LanguageName = Preferences.Get("user_selected_language", null);
				LanguageInfo SelectedLanguage = SupportedLanguages[0];

				if (LanguageName is not null)
				{
					SelectedLanguage = SupportedLanguages.FirstOrDefault(
						el => string.Equals(el.TwoLetterISOLanguageName, LanguageName, StringComparison.OrdinalIgnoreCase),
						SelectedLanguage);
				}

				if (LanguageName is null ||
					!string.Equals(SelectedLanguage.TwoLetterISOLanguageName, LanguageName, StringComparison.OrdinalIgnoreCase))
				{
					Preferences.Set("user_selected_language", SelectedLanguage.TwoLetterISOLanguageName);
				}

				return SelectedLanguage;
			}
		}

		/// <summary>
		/// Indicates whether the application is onboarded.
		/// </summary>
		public static bool IsOnboarded => Shell.Current is not null;

		/// <summary>
		/// Define a static event to notify when the app enters the foreground.
		/// </summary>
		public static event EventHandler? AppActivated;


		#endregion

		#region Constructor

		public App() : this(false) { }

		public App(bool backgroundStart)
		{
			// Manage single-instance behavior.
			App? PreviousInstance = appInstance;
			appInstance = this;
			this.onStartResumesApplication = PreviousInstance is not null;

			if (PreviousInstance is null)
			{
				InitLocalizationResource();

				AppDomain.CurrentDomain.UnhandledException += this.CurrentDomain_UnhandledException;
				TaskScheduler.UnobservedTaskException += this.TaskScheduler_UnobservedTaskException;

				// Initialize the login auditor.
				LoginInterval[] LoginIntervals =
				[
					new(Constants.Password.FirstMaxPasswordAttempts, TimeSpan.FromHours(Constants.Password.FirstBlockInHours)),
					new(Constants.Password.SecondMaxPasswordAttempts, TimeSpan.FromHours(Constants.Password.SecondBlockInHours)),
					new(Constants.Password.ThirdMaxPasswordAttempts, TimeSpan.FromHours(Constants.Password.ThirdBlockInHours))
				];

				this.loginAuditor = new LoginAuditor(Constants.Password.LogAuditorObjectID, LoginIntervals);
				this.startupCancellation = new CancellationTokenSource();
				this.initCompleted = this.InitializeAppAsync(backgroundStart);
			}
			else
			{
				// Reuse state from the previous instance.
				this.loginAuditor = PreviousInstance.loginAuditor;
				this.autoSaveTimer = PreviousInstance.autoSaveTimer;
				this.initCompleted = PreviousInstance.initCompleted;
				this.startupCancellation = PreviousInstance.startupCancellation;
			}

			if (!backgroundStart)
			{
				this.InitializeComponent();
				AppTheme? CurrentTheme = ServiceRef.TagProfile.Theme;
				ServiceRef.TagProfile.SetTheme(CurrentTheme ?? AppTheme.Light);

				try
				{
					this.MainPage = ServiceHelper.GetService<AppShell>();
				}
				catch (Exception Ex)
				{
					this.HandleStartupException(Ex);
				}
			}
		}

		#endregion

		#region Initialization

		private static void InitLocalizationResource()
		{
			LocalizationManager.Current.PropertyChanged += (_, _) => AppResources.Culture = LocalizationManager.Current.CurrentCulture;
			LocalizationManager.Current.CurrentCulture = SelectedLanguage;
		}

		private Task<bool> InitializeAppAsync(bool backgroundStart)
		{
			TaskCompletionSource<bool> ResultTcs = new();
			Task.Run(async () => await this.InitializeAppInternalAsync(ResultTcs, backgroundStart));
			return ResultTcs.Task;
		}

		private async Task InitializeAppInternalAsync(TaskCompletionSource<bool> resultTcs, bool backgroundStart)
		{
			try
			{
				this.InitializeInstances();
				await ServiceRef.CryptoService.InitializeJwtFactory();
				await this.PerformStartupAsync(isResuming: false, backgroundStart);
				resultTcs.TrySetResult(true);
			}
			catch (Exception Ex)
			{
				Ex = Log.UnnestException(Ex);
				this.HandleStartupException(Ex);
				servicesSetup.TrySetResult(false);
				resultTcs.TrySetResult(false);
			}
		}

		private void HandleStartupException(Exception Ex)
		{
            Ex = Log.UnnestException(Ex);
			ServiceRef.LogService.SaveExceptionDump("StartPage", Ex.ToString());
			this.DisplayBootstrapErrorPage(Ex.Message, Ex.StackTrace ?? string.Empty);
			return;
		}

		/// <summary>
		/// Initializes required types and registers services.
		/// </summary>
		private void InitializeInstances()
		{
			Assembly AppAssembly = this.GetType().Assembly;

			if (!Types.IsInitialized)
			{
				Types.Initialize(
					AppAssembly,
					typeof(Database).Assembly,
					typeof(ObjectSerializer).Assembly,
					typeof(FilesProvider).Assembly,
					typeof(RuntimeSettings).Assembly,
					typeof(PersistedEvent).Assembly,
					typeof(InternetContent).Assembly,
					typeof(ImageCodec).Assembly,
					typeof(MarkdownDocument).Assembly,
					typeof(XML).Assembly,
					typeof(DnsResolver).Assembly,
					typeof(XmppClient).Assembly,
					typeof(ContractsClient).Assembly,
					typeof(NeuroFeaturesClient).Assembly,
					typeof(EDalerClient).Assembly,
					typeof(SensorClient).Assembly,
					typeof(ControlClient).Assembly,
					typeof(ConcentratorClient).Assembly,
					typeof(ProvisioningClient).Assembly,
					typeof(PubSubClient).Assembly,
					typeof(PepClient).Assembly,
					typeof(AvatarClient).Assembly,
					typeof(PushNotificationClient).Assembly,
					typeof(MailClient).Assembly,
					typeof(ThingReference).Assembly,
					typeof(JwtFactory).Assembly,
					typeof(JwsAlgorithm).Assembly,
					typeof(Expression).Assembly,
					typeof(Graph).Assembly,
					typeof(GraphEncoder).Assembly,
					typeof(XmppServerlessMessaging).Assembly,
					typeof(HttpxClient).Assembly);
			}

			// Register exceptions as alerts.
			Log.RegisterAlertExceptionType(true,
				typeof(OutOfMemoryException),
				typeof(StackOverflowException),
				typeof(AccessViolationException),
				typeof(InsufficientMemoryException));

			EndpointSecurity.SetCiphers([typeof(Edwards448Endpoint)], false);

			// Instantiate default services.
			Types.InstantiateDefault<ITagProfile>(false);
			Types.InstantiateDefault<ILogService>(false);
			Types.InstantiateDefault<IUiService>(false);
			Types.InstantiateDefault<ICryptoService>(false);
			Types.InstantiateDefault<INetworkService>(false);
			Types.InstantiateDefault<IStorageService>(false);
			Types.InstantiateDefault<ISettingsService>(false);
			Types.InstantiateDefault<IXmppService>(false);
			Types.InstantiateDefault<IAttachmentCacheService>(false);
			Types.InstantiateDefault<IContractOrchestratorService>(false);
			Types.InstantiateDefault<INfcService>(false);
			Types.InstantiateDefault<INotificationService>(false);

			defaultInstantiatedSource.TrySetResult(true);

			// Set dependency resolver.
			DependencyResolver.ResolveUsing(type =>
			{
				if (Types.GetType(type.FullName) is null)
					return null;

				try
				{
					return Types.Instantiate(true, type);
				}
				catch (Exception Ex)
				{
					ServiceRef.LogService.LogException(Ex);
					return null;
				}
			});

			servicesSetup.TrySetResult(true);
		}

        #endregion

        #region Startup / Resume

        public async Task OnBackgroundStart()
        {
            if (this.onStartResumesApplication)
            {
                this.onStartResumesApplication = false;
                await this.ResumeAsync(isBackground: true);
                return;
            }

            // Asynchronously wait up to 60 seconds.
            if (!await this.initCompleted.WaitAsync(TimeSpan.FromSeconds(60)))
                throw new Exception("Initialization did not complete in time.");
        }

        protected override async void OnStart()
        {
            if (this.onStartResumesApplication)
            {
                this.onStartResumesApplication = false;
                this.OnResume();
                return;
            }

            if (!await this.initCompleted.WaitAsync(TimeSpan.FromSeconds(60)))
                throw new Exception("Initialization did not complete in time.");
        }

        public async Task ResumeAsync(bool isBackground)
		{
			appInstance = this;
			this.startupCancellation = new CancellationTokenSource();
			await this.PerformStartupAsync(isResuming: true, backgroundStart: isBackground);
		}

		protected override async void OnResume()
		{
			await this.ResumeAsync(isBackground: false);
		}

		private async Task PerformStartupAsync(bool isResuming, bool backgroundStart)
		{
			await this.startupWorker.WaitAsync();
			try
			{
				if (++startupCounter > 1)
					return;

				CancellationToken Token = this.startupCancellation.Token;
				Token.ThrowIfCancellationRequested();

				if (!backgroundStart)
				{
					await SendErrorReportFromPreviousRunAsync();
					Token.ThrowIfCancellationRequested();
				}

				await ServiceRef.StorageService.Init(Token);
				if (!configLoaded)
				{
					await this.CreateOrRestoreConfigurationAsync();
					configLoaded = true;
				}

				Token.ThrowIfCancellationRequested();
				await ServiceRef.NetworkService.Load(isResuming, Token);
				Token.ThrowIfCancellationRequested();
				await ServiceRef.XmppService.Load(isResuming, Token);
				Token.ThrowIfCancellationRequested();

				TimeSpan InitialAutoSaveDelay = Constants.Intervals.AutoSave.Multiply(4);
				this.autoSaveTimer = new Timer(async _ => await this.AutoSaveAsync(), null, InitialAutoSaveDelay, Constants.Intervals.AutoSave);

				await ServiceRef.UiService.Load(isResuming, Token);
				await ServiceRef.AttachmentCacheService.Load(isResuming, Token);
				await ServiceRef.ContractOrchestratorService.Load(isResuming, Token);
				await ServiceRef.ThingRegistryOrchestratorService.Load(isResuming, Token);
				await ServiceRef.NeuroWalletOrchestratorService.Load(isResuming, Token);
				await ServiceRef.NotificationService.Load(isResuming, Token);

				AppShell.AppLoaded();
			}
			catch (OperationCanceledException)
			{
				Log.Notice($"{(isResuming ? "OnResume" : "Initial app")} startup was canceled.");
			}
			catch (Exception Ex)
			{
				Ex = Log.UnnestException(Ex);
				ServiceRef.LogService.SaveExceptionDump(Ex.Message, Ex.StackTrace ?? string.Empty);
				this.DisplayBootstrapErrorPage(Ex.Message, Ex.StackTrace ?? string.Empty);
			}
			finally
			{
				this.startupWorker.Release();
			}
		}

		#endregion

		#region Sleep / Shutdown

		public async Task OnBackgroundSleep() => await this.ShutdownAsync(inPanic: false);

		protected override async void OnSleep()
		{
			if (this.MainPage?.BindingContext is BaseViewModel Vm)
				await Vm.Shutdown();

			await this.ShutdownAsync(inPanic: false);
			SetStartInactivityTime();
		}

		internal static async Task StopAsync()
		{
			if (appInstance is not null)
			{
				await appInstance.ShutdownAsync(inPanic: false);
				appInstance = null;
			}

			try
			{
				await ServiceRef.PlatformSpecific.CloseApplication();
			}
			catch (Exception)
			{
				Environment.Exit(0);
			}
		}

		private async Task ShutdownAsync(bool inPanic)
		{
			this.startupCancellation.Cancel();
			await this.startupWorker.WaitAsync();

			try
			{
				if ((startupCounter < 1) || (--startupCounter > 0))
					return;

				this.StopAutoSaveTimer();

				if (inPanic)
				{
					if (ServiceRef.XmppService is not null)
						await ServiceRef.XmppService.UnloadFast();
				}
				else
				{
					List<Task> UnloadTasks = [];

					if (ServiceRef.UiService is not null)
						UnloadTasks.Add(ServiceRef.UiService.Unload());

					if (ServiceRef.ContractOrchestratorService is not null)
						UnloadTasks.Add(ServiceRef.ContractOrchestratorService.Unload());

					if (ServiceRef.XmppService is not null)
						UnloadTasks.Add(ServiceRef.XmppService.Unload());

					if (ServiceRef.NetworkService is not null)
						UnloadTasks.Add(ServiceRef.NetworkService.Unload());

					if (ServiceRef.AttachmentCacheService is not null)
						UnloadTasks.Add(ServiceRef.AttachmentCacheService.Unload());

					await Task.WhenAll(UnloadTasks);

					foreach (IEventSink Sink in Log.Sinks)
						Log.Unregister(Sink);

					if (ServiceRef.StorageService is not null)
						await ServiceRef.StorageService.Shutdown();
				}

				// Causes list of singleton instances to be cleared.
				await Log.TerminateAsync();
			}
			finally
			{
				this.startupWorker.Release();
			}
		}

		private void StopAutoSaveTimer()
		{
			if (this.autoSaveTimer is not null)
			{
				this.autoSaveTimer.Change(Timeout.Infinite, Timeout.Infinite);
				this.autoSaveTimer.Dispose();
				this.autoSaveTimer = null;
			}
		}

		#endregion

		#region AutoSave

		private async Task AutoSaveAsync()
		{
			await this.autoSaveSemaphore.WaitAsync();
			try
			{
				if (ServiceRef.TagProfile.IsDirty)
				{
					ServiceRef.TagProfile.ResetIsDirty();
					try
					{
						TagConfiguration Configuration = ServiceRef.TagProfile.ToConfiguration();
						try
						{
							if (string.IsNullOrEmpty(Configuration.ObjectId))
								await ServiceRef.StorageService.Insert(Configuration);
							else
								await ServiceRef.StorageService.Update(Configuration);
						}
						catch (KeyNotFoundException)
						{
							await ServiceRef.StorageService.Insert(Configuration);
						}
					}
					catch (Exception Ex)
					{
						ServiceRef.LogService.LogException(Ex, this.GetClassAndMethod(MethodBase.GetCurrentMethod()));
					}
				}
			}
			finally
			{
				this.autoSaveSemaphore.Release();
			}
		}

		public async Task ForceSaveAsync() => await this.AutoSaveAsync();

		#endregion

		#region Configuration

		private async Task CreateOrRestoreConfigurationAsync()
		{
			TagConfiguration? Configuration;
			try
			{
				Configuration = await ServiceRef.StorageService.FindFirstDeleteRest<TagConfiguration>();
			}
			catch (Exception FindEx)
			{
				ServiceRef.LogService.LogException(FindEx, this.GetClassAndMethod(MethodBase.GetCurrentMethod()));
				Configuration = null;
			}

			if (Configuration is null)
			{
				Configuration = new TagConfiguration();
				try
				{
					await ServiceRef.StorageService.Insert(Configuration);
				}
				catch (Exception Ex)
				{
					ServiceRef.LogService.LogException(Ex, this.GetClassAndMethod(MethodBase.GetCurrentMethod()));
				}
			}

			ServiceRef.TagProfile.FromConfiguration(Configuration);
		}

		#endregion

		#region Page Navigation

		public static Task SetRegistrationPageAsync() => SetPageAsync(Constants.Pages.RegistrationPage);

		public static Task SetMainPageAsync() => SetPageAsync(Constants.Pages.MainPage);

		public static Task SetPageAsync(string pagePath)
		{
			if (Shell.Current.CurrentState.Location?.OriginalString == pagePath)
				return Task.CompletedTask;

			return MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync(pagePath));
		}

		#endregion

		#region Error Handling

		private async void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
		{
			try
			{
				Exception Ex = Log.UnnestException(e.Exception);
				e.SetObserved();
				await this.HandleUnhandledExceptionAsync(Ex, nameof(TaskScheduler_UnobservedTaskException), shutdown: false);
			}
			catch (Exception Ex)
			{
				ServiceRef.LogService.LogException(Ex);
			}
		}

		private async void CurrentDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs e)
		{
			try
			{
				await this.HandleUnhandledExceptionAsync(e.ExceptionObject as Exception, nameof(CurrentDomain_UnhandledException), shutdown: true);
			}
			catch (Exception Ex)
			{
				ServiceRef.LogService.LogException(Ex);
			}
		}

		private async Task HandleUnhandledExceptionAsync(Exception? ex, string title, bool shutdown)
		{
			if (ex is not null)
			{
				ServiceRef.LogService.SaveExceptionDump(title, ex.ToString());
				ServiceRef.LogService.LogException(ex, this.GetClassAndMethod(MethodBase.GetCurrentMethod(), title));
			}

			if (shutdown)
				await this.ShutdownAsync(inPanic: false);

#if DEBUG
			if (!shutdown && this.MainPage is not null)
			{
				if (this.Dispatcher.IsDispatchRequired)
				{
					this.Dispatcher.Dispatch(async () =>
					{
						await this.MainPage.DisplayAlert(title, ex?.ToString(), ServiceRef.Localizer[nameof(AppResources.Ok)]);
					});
				}
				else
				{
					await this.MainPage.DisplayAlert(title, ex?.ToString(), ServiceRef.Localizer[nameof(AppResources.Ok)]);
				}
			}
#endif
		}

		private void DisplayBootstrapErrorPage(string title, string stackTrace)
		{
			this.Dispatcher.Dispatch(() => this.MainPage = new BootstrapErrorPage(title, stackTrace));
		}

		private static async Task SendErrorReportFromPreviousRunAsync()
		{
			if (ServiceRef.LogService is not null)
			{
				string StackTrace = ServiceRef.LogService.LoadExceptionDump();
				if (!string.IsNullOrWhiteSpace(StackTrace))
				{
					List<KeyValuePair<string, object?>> Tags =
						[new KeyValuePair<string, object?>(Constants.XmppProperties.Jid, ServiceRef.XmppService.BareJid)];

					KeyValuePair<string, object?>[]? AdditionalTags = ServiceRef.TagProfile.LegalIdentity?.GetTags();
					if (AdditionalTags is not null)
						Tags.AddRange(AdditionalTags);

					StringBuilder Msg = new();
					Msg.Append("Unhandled exception caused app to crash. ");
					Msg.AppendLine("Below you can find the stack trace of the corresponding exception.");
					Msg.AppendLine();
					Msg.AppendLine("```");
					Msg.AppendLine(StackTrace);
					Msg.AppendLine("```");

					Log.Alert(Msg.ToString(), [.. Tags]);

					try
					{
						await SendAlertAsync(StackTrace, "text/plain");
					}
					finally
					{
						ServiceRef.LogService.DeleteExceptionDump();
					}
				}
			}
		}

		internal static async Task SendAlertAsync(string message, string contentType)
		{
			try
			{
				using HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(30) };
				Client.DefaultRequestHeaders.Accept.Clear();
				Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

				using StringContent Content = new(message)
				{
					Headers = { ContentType = MediaTypeHeaderValue.Parse(contentType) }
				};

				await Client.PostAsync("https://lab.tagroot.io/Alert.ws", Content);
			}
			catch (Exception Ex)
			{
				Log.Exception(Ex);
			}
		}

		public static async Task EvaluateDatabaseDiffAsync(string fileName, bool includeUnchanged, bool sendAsAlert)
		{
			StringBuilder Xml = new();
			using XmlDatabaseExport Output = new(Xml, true, 256);
			await ServiceRef.StorageService.Export(Output);

			string AppDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			fileName = Path.Combine(AppDataFolder, fileName);

			string CurrentState = Xml.ToString();
			string PrevState = File.Exists(fileName) ? File.ReadAllText(fileName) : string.Empty;

			EditScript<string> Script = Difference.AnalyzeRows(PrevState, CurrentState);
			StringBuilder Markdown = new();
			Markdown.AppendLine(CultureInfo.InvariantCulture, $"Database content changes (`{fileName}`):");

			foreach (Step<string> Step in Script.Steps)
			{
				Markdown.AppendLine();

				string Prefix = Step.Operation switch
				{
					EditOperation.Insert => "+>\t",
					EditOperation.Delete => "->\t",
					_ => includeUnchanged ? ">\t" : string.Empty
				};

				if (string.IsNullOrEmpty(Prefix))
					continue;

				Markdown.AppendLine(CultureInfo.InvariantCulture, $"{Prefix}```xml");
				foreach (string Row in Step.Symbols)
					Markdown.AppendLine(CultureInfo.InvariantCulture, $"{Prefix}{Row}  ");
				Markdown.AppendLine(CultureInfo.InvariantCulture, $"{Prefix}```");
			}

			string DiffMsg = Markdown.ToString();

			if (sendAsAlert)
			{
				await SendAlertAsync(DiffMsg, "text/markdown");
			}

			File.WriteAllText(fileName, CurrentState);
			File.WriteAllText(fileName + ".diff.md", DiffMsg);
		}

		#endregion

		#region URL Handling

		public static void OpenUrlSync(string url)
		{
			MainThread.BeginInvokeOnMainThread(async () =>
			{
				if (ServiceRef.TagProfile.Step != RegistrationStep.Complete)
				{
					await ServiceRef.UiService.DisplayAlert(
						ServiceRef.Localizer[nameof(AppResources.SomethingWentWrong)],
						ServiceRef.Localizer[nameof(AppResources.NotCompletedOnboardingError)]);
					return;
				}

				await QrCode.OpenUrl(url);
			});
		}

		public static Task<bool> OpenUrlAsync(string url) => OpenUrlAsync(url, showErrorIfUnable: true);

		public static Task<bool> OpenUrlAsync(string url, bool showErrorIfUnable)
		{
			return QrCode.OpenUrl(url, showErrorIfUnable);
		}

		#endregion

		#region Authentication

		public static async Task<string?> InputPasswordAsync(AuthenticationPurpose purpose)
		{
			ITagProfile Profile = ServiceRef.TagProfile;
			if (!Profile.HasLocalPassword)
				return string.Empty;

			return await InputPasswordInternalAsync(purpose, Profile);
		}

		private static async Task<string?> InputPasswordInternalAsync(AuthenticationPurpose purpose, ITagProfile profile)
		{
			displayedPasswordPopup = true;
			try
			{
				if (!profile.HasLocalPassword)
					return string.Empty;

				CheckPasswordViewModel ViewModel = new(purpose);
				string? Result = await ServiceRef.UiService.PushAsync<CheckPasswordPopup, CheckPasswordViewModel, string>(ViewModel);
				await CheckUserBlockingAsync();
				return Result;
			}
			catch
			{
				return null;
			}
			finally
			{
				displayedPasswordPopup = false;
			}
		}

		public static Task<bool> AuthenticateUserAsync(AuthenticationPurpose purpose, bool force = false)
		{
			if (MainThread.IsMainThread)
				return AuthenticateUserOnMainThreadAsync(purpose, force);

			TaskCompletionSource<bool> Tcs = new();
			MainThread.BeginInvokeOnMainThread(async () =>
			{
				Tcs.TrySetResult(await AuthenticateUserOnMainThreadAsync(purpose, force));
			});
			return Tcs.Task;
		}

		private static async Task<bool> AuthenticateUserOnMainThreadAsync(AuthenticationPurpose purpose, bool force = false)
		{
			bool NeedToVerifyPassword = IsInactivitySafeIntervalPassed();
			if (!force && !NeedToVerifyPassword)
				return true;

			switch (ServiceRef.TagProfile.AuthenticationMethod)
			{
				case AuthenticationMethod.Fingerprint:
					if (!ServiceRef.PlatformSpecific.SupportsFingerprintAuthentication)
						ServiceRef.TagProfile.AuthenticationMethod = AuthenticationMethod.Password;

					if (await ServiceRef.PlatformSpecific.AuthenticateUserFingerprint(
						ServiceRef.Localizer[nameof(AppResources.FingerprintTitle)],
						null,
						ServiceRef.Localizer[nameof(AppResources.FingerprintDescription)],
						ServiceRef.Localizer[nameof(AppResources.Cancel)],
						null))
					{
						await UserAuthenticationSuccessfulAsync();
						return true;
					}
					goto case AuthenticationMethod.Password;

				case AuthenticationMethod.Password:
					if (!ServiceRef.TagProfile.HasLocalPassword)
						return true;

					if (displayedPasswordPopup)
						return false;

					return await InputPasswordInternalAsync(purpose, ServiceRef.TagProfile) is not null;

				default:
					return false;
			}
		}

		public static async Task CheckUserBlockingAsync()
		{
			DateTime? NextLoginTime = await appInstance!.loginAuditor.GetEarliestLoginOpportunity(Constants.Password.RemoteEndpoint, Constants.Password.Protocol);
			if (NextLoginTime.HasValue)
			{
				IUiService Ui = ServiceRef.UiService;
				string MessageAlert;
				if (NextLoginTime == DateTime.MaxValue)
					MessageAlert = ServiceRef.Localizer[nameof(AppResources.PasswordIsInvalidAplicationBlockedForever)];
				else
				{
					DateTime LocalDateTime = NextLoginTime.Value.ToLocalTime();
					if (NextLoginTime.Value.ToShortDateString() == DateTime.Today.ToShortDateString())
					{
						string DateString = LocalDateTime.ToShortTimeString();
						MessageAlert = ServiceRef.Localizer[nameof(AppResources.PasswordIsInvalidAplicationBlocked), DateString];
					}
					else if (NextLoginTime.Value.ToShortDateString() == DateTime.Today.AddDays(1).ToShortDateString())
					{
						string DateString = LocalDateTime.ToShortTimeString();
						MessageAlert = ServiceRef.Localizer[nameof(AppResources.PasswordIsInvalidAplicationBlockedTillTomorrow), DateString];
					}
					else
					{
						string DateString = LocalDateTime.ToString("yyyy-MM-dd, 'at' HH:mm", CultureInfo.InvariantCulture);
						MessageAlert = ServiceRef.Localizer[nameof(AppResources.PasswordIsInvalidAplicationBlocked), DateString];
					}
				}

				await Ui.DisplayAlert(ServiceRef.Localizer[nameof(AppResources.ErrorTitle)], MessageAlert);
				await StopAsync();
			}
		}

		public static async Task<bool> CheckPasswordAndUnblockUserAsync(string password)
		{
			if (password is null)
				return false;

			if (ServiceRef.TagProfile.ComputePasswordHash(password) == ServiceRef.TagProfile.LocalPasswordHash)
			{
				await UserAuthenticationSuccessfulAsync();
				return true;
			}
			else
			{
				await UserAuthenticationFailedAsync();
				return false;
			}
		}

		private static async Task UserAuthenticationSuccessfulAsync()
		{
			SetStartInactivityTime();
			SetCurrentPasswordCounter(0);
			await appInstance!.loginAuditor.UnblockAndReset(Constants.Password.RemoteEndpoint);
		}

		private static async Task UserAuthenticationFailedAsync()
		{
			await appInstance!.loginAuditor.ProcessLoginFailure(Constants.Password.RemoteEndpoint, Constants.Password.Protocol, DateTime.Now, Constants.Password.Reason);
			long CurrentCounter = await GetCurrentPasswordCounterAsync();
			CurrentCounter++;
			SetCurrentPasswordCounter(CurrentCounter);
		}

		private static void SetStartInactivityTime() => savedStartTime = DateTime.Now;

		private static bool IsInactivitySafeIntervalPassed() => DateTime.Now.Subtract(savedStartTime).TotalMinutes > Constants.Password.PossibleInactivityInMinutes;

		internal static async Task<long> GetCurrentPasswordCounterAsync() => await ServiceRef.SettingsService.RestoreLongState(Constants.Password.CurrentPasswordAttemptCounter);

		private static async void SetCurrentPasswordCounter(long counter) => await ServiceRef.SettingsService.SaveState(Constants.Password.CurrentPasswordAttemptCounter, counter);

		#endregion

		#region IDisposable Implementation

		public void Dispose()
		{
			this.Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// <see cref="IDisposableAsync.Dispose"/>
		/// </summary>
		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
				this.DisposeAsync().Wait();
		}

		/// <summary>
		/// <see cref="IDisposableAsync.Dispose"/>
		/// </summary>
		public virtual async Task DisposeAsync()
		{
			if (this.isDisposed)
				return;

			await this.loginAuditor.DisposeAsync();
			this.autoSaveTimer?.Dispose();
			this.initCompleted.Dispose();
			this.startupWorker.Dispose();
			this.startupCancellation.Dispose();

			this.isDisposed = true;
		}

		#endregion

		#region Helpers

		public static T Instantiate<T>()
		{
			if (!defaultInstantiated)
				defaultInstantiated = defaultInstantiatedSource.Task.Result;

			return Types.Instantiate<T>(false);
		}

		public static void RaiseAppActivated()
		{
			AppActivated?.Invoke(Current, EventArgs.Empty);
		}


		#endregion
	}
}
