using System.Text;
using Waher.Events;
using Waher.Events.Persistence;
using Waher.Persistence;
using Waher.Persistence.Files;
using Waher.Persistence.Serialization;
using Waher.Runtime.Inventory;

namespace NeuroAccessMaui.Services.Storage
{
	[Singleton]
	internal sealed class StorageService : IStorageService, IDisposableAsync
	{
		private readonly LinkedList<TaskCompletionSource<bool>> tasksWaiting = new();
		private readonly string dataFolder;
		private FilesProvider? databaseProvider;
		private PersistedEventLog? persistedEventLog;
		private bool? initialized = null;
		private bool started = false;

		/// <summary>
		/// Creates a new instance of the <see cref="StorageService"/> class.
		/// </summary>
		public StorageService()
		{
			string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			this.dataFolder = Path.Combine(appDataFolder, "Data");
		}

		/// <summary>
		/// Folder for database.
		/// </summary>
		public string DataFolder => this.dataFolder;

		#region LifeCycle management

		/// <inheritdoc />
		public async Task Init(CancellationToken? cancellationToken)
		{
			lock (this.tasksWaiting)
			{
				if (this.started)
					return;

				this.started = true;
			}

			try
			{
				if (Database.HasProvider)
					this.databaseProvider = Database.Provider as FilesProvider;

				if (this.databaseProvider is null)
				{
					this.databaseProvider = await this.CreateDatabaseFile();

					await this.databaseProvider.RepairIfInproperShutdown(string.Empty);
					await this.databaseProvider.Start();
				}

				if (this.databaseProvider is not null)
				{
					Database.Register(this.databaseProvider, false);
					Log.Register(this.persistedEventLog = new PersistedEventLog(90));
					this.InitDone(true);
					return;
				}
			}
			catch (Exception e1)
			{
				e1 = Log.UnnestException(e1);
				ServiceRef.LogService.LogException(e1);
			}

			//!!! test to uncomment it
			/* On iOS the UI is not initialized at this point, need to find another solution
			if (await ServiceRef.UiSerializer.DisplayAlert(ServiceRef.Localizer[nameof(AppResources.DatabaseIssue"], ServiceRef.Localizer[nameof(AppResources.DatabaseCorruptInfoText"], ServiceRef.Localizer[nameof(AppResources.RepairAndContinue"], ServiceRef.Localizer[nameof(AppResources.ContinueAnyway"]))
			*/
			//TODO: when UI is ready, show an alert that the database was reset due to unrecoverable error
			//TODO: say to close the application in a controlled manner
			{
				try
				{
					Directory.Delete(this.dataFolder, true);

					this.databaseProvider = await this.CreateDatabaseFile();

					await this.databaseProvider.RepairIfInproperShutdown(string.Empty);

					await this.databaseProvider.Start();

					if (!Database.HasProvider)
					{
						Database.Register(this.databaseProvider, false);
						Log.Register(this.persistedEventLog = new PersistedEventLog(90));
						this.InitDone(true);
						return;
					}
				}
				catch (Exception e3)
				{
					e3 = Log.UnnestException(e3);
					ServiceRef.LogService.LogException(e3);

					await App.StopAsync();
					/*
					Thread?.NewState("UI");
					await ServiceRef.UiSerializer.DisplayAlert(ServiceRef.Localizer[nameof(AppResources.DatabaseIssue"], ServiceRef.Localizer[nameof(AppResources.DatabaseRepairFailedInfoText"], ServiceRef.Localizer[nameof(AppResources.Ok"]);
					*/
				}
			}

			this.InitDone(false);
		}

		private void InitDone(bool Result)
		{
			lock (this.tasksWaiting)
			{
				this.initialized = Result;

				foreach (TaskCompletionSource<bool> Wait in this.tasksWaiting)
					Wait.TrySetResult(Result);

				this.tasksWaiting.Clear();
			}
		}

		/// <inheritdoc />
		public Task<bool> WaitInitDone()
		{
			lock (this.tasksWaiting)
			{
				if (this.initialized.HasValue)
					return Task.FromResult<bool>(this.initialized.Value);

				TaskCompletionSource<bool> Wait = new();
				this.tasksWaiting.AddLast(Wait);

				return Wait.Task;
			}
		}

		/// <inheritdoc />
		public async Task Shutdown()
		{
			lock (this.tasksWaiting)
			{
				this.initialized = null;
				this.started = false;
			}

			try
			{
				if (this.persistedEventLog is not null)
				{
					Log.Unregister(this.persistedEventLog);
					await this.persistedEventLog.DisposeAsync();
					this.persistedEventLog = null;
				}

				if (this.databaseProvider is not null)
				{
					Database.Register(new NullDatabaseProvider(), false);
					await this.databaseProvider.Flush();
					await this.databaseProvider.Stop();
					this.databaseProvider = null;
				}
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
			}
		}

		private Task<FilesProvider> CreateDatabaseFile()
		{
			FilesProvider.AsyncFileIo = false;  // Asynchronous file I/O induces a long delay during startup on mobile platforms. Why??

			return FilesProvider.CreateAsync(this.dataFolder, "Default", 8192, 10000, 8192, Encoding.UTF8,
				(int)Constants.Timeouts.Database.TotalMilliseconds, ServiceRef.CryptoService.GetCustomKey);
		}

		/// <summary>
		/// <see cref="IDisposable.Dispose"/>
		/// </summary>
		[Obsolete("Use DisposeAsync() instead.")]
		public void Dispose()
		{
			this.DisposeAsync().Wait();
		}

		/// <summary>
		/// <see cref="IDisposableAsync.DisposeAsync"/>
		/// </summary>
		public async Task DisposeAsync()
		{
			if (this.persistedEventLog is not null)
			{
				await this.persistedEventLog.DisposeAsync();
				this.persistedEventLog = null;
			}

			if (this.databaseProvider is not null)
			{
				await this.databaseProvider.DisposeAsync();
				this.databaseProvider = null;
			}
		}

		#endregion

		public async Task Insert(object obj)
		{
			await Database.Insert(obj);
			await Database.Provider.Flush();
		}

		public async Task Update(object obj)
		{
			await Database.Update(obj);
			await Database.Provider.Flush();
		}

		public Task<T> FindFirstDeleteRest<T>() where T : class
		{
			return Database.FindFirstDeleteRest<T>();
		}

		public Task<T> FindFirstIgnoreRest<T>() where T : class
		{
			return Database.FindFirstIgnoreRest<T>();
		}

		public Task Export(IDatabaseExport exportOutput)
		{
			return Database.Export(exportOutput);
		}

		/// <summary>
		/// Flags the database for repair, so that the next time the app is opened, the database will be repaired.
		/// </summary>
		public void FlagForRepair()
		{
			this.DeleteFile("Start.txt");
			this.DeleteFile("Stop.txt");
		}

		private void DeleteFile(string FileName)
		{
			try
			{
				FileName = Path.Combine(this.dataFolder, FileName);

				if (File.Exists(FileName))
					File.Delete(FileName);
			}
			catch (Exception)
			{
				// Ignore, to avoid infinite loops if event log has an inconsistency.
			}
		}
	}
}
