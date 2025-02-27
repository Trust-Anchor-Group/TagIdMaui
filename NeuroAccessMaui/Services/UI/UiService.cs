//#define PROFILING

using IdApp.Cv;
using IdApp.Cv.ColorModels;
using Mopups.Pages;
using Mopups.Services;
using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services.UI.Tasks;
using NeuroAccessMaui.UI.Pages;
using NeuroAccessMaui.UI.Popups;
using SkiaSharp;
using Svg.Skia;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Waher.Events;
using Waher.Runtime.Inventory;

#if PROFILING
using Waher.Runtime.Profiling;
#endif

namespace NeuroAccessMaui.Services.UI
{
	/// <inheritdoc/>
	[Singleton]
	public class UiService : LoadableService, IUiService
	{
		private readonly Dictionary<string, NavigationArgs> navigationArgsMap = [];
		private readonly ConcurrentQueue<UiTask> taskQueue = new();
		private readonly Stack<PopupPage?> popupStack = new();
		private NavigationArgs? latestArguments = null;
		private bool isExecutingUiTasks = false;
		private bool isNavigating = false;

		/// <summary>
		/// Creates a new instance of the <see cref="UiService"/> class.
		/// </summary>
		public UiService()
		{
		}

		#region UI-tasks

		private void AddTask(UiTask Task)
		{
			this.taskQueue.Enqueue(Task);

			if (!this.isExecutingUiTasks)
			{
				this.isExecutingUiTasks = true;

				MainThread.BeginInvokeOnMainThread(async () =>
				{
					await this.ProcessAllTasks();
				});
			}
		}

		private async Task ProcessAllTasks()
		{
			try
			{
				do
				{
					if (this.taskQueue.TryDequeue(out UiTask? Task))
						await Task.Execute();
				}
				while (!this.taskQueue.IsEmpty);
			}
			finally
			{
				this.isExecutingUiTasks = false;
			}
		}

		#endregion

		#region DisplayAlert

		/// <inheritdoc/>
		public Task<bool> DisplayAlert(string Title, string Message, string? Accept = null, string? Cancel = null)
		{
			DisplayAlert Task = new(Title, Message, Accept, Cancel);
			this.AddTask(Task);
			return Task.CompletionSource.Task;
		}

		/// <inheritdoc/>
		public Task DisplayException(Exception Exception, string? Title = null)
		{
			Exception = Log.UnnestException(Exception);

			StringBuilder sb = new();

			if (Exception is not null)
			{
				sb.AppendLine(Exception.Message);

				while (Exception.InnerException is not null)
				{
					Exception = Exception.InnerException;
					sb.AppendLine(Exception.Message);
				}
			}
			else
				sb.AppendLine(ServiceRef.Localizer[nameof(AppResources.ErrorTitle)]);

			return this.DisplayAlert(
				Title ?? ServiceRef.Localizer[nameof(AppResources.ErrorTitle)], sb.ToString(),
				ServiceRef.Localizer[nameof(AppResources.Ok)]);
		}

		#endregion

		#region DisplayPrompt

		/// <inheritdoc/>
		public Task<string?> DisplayPrompt(string Title, string Message, string? Accept = null, string? Cancel = null)
		{
			DisplayPrompt Task = new(Title, Message, Accept, Cancel);
			this.AddTask(Task);
			return Task.CompletionSource.Task;
		}

		#endregion

		#region Screen shots

		/// <summary>
		/// Takes a blurred screen shot
		/// </summary>
		/// <returns>Screenshot, if able to create a screen shot.</returns>
		public async Task<ImageSource?> TakeBlurredScreenshotAsync()
		{
			try
			{
#if PROFILING
				Profiler Profiler = new("Blur", ProfilerThreadType.Sequential);

				Profiler.Start();
				Profiler.NewState("Capture");
#endif
				IScreenshotResult? Screen = await Screenshot.CaptureAsync();
				if (Screen is null)
					return null;

#if PROFILING
				Profiler.NewState("PNG");
#endif
				//Read screenshot
				using Stream PngStream = await Screen.OpenReadAsync(ScreenshotFormat.Png, 20);

#if PROFILING
				Profiler.NewState("SKBitmap");
#endif
				// Original SKBitmap from PNG stream
				SKBitmap OriginalBitmap = SKBitmap.FromImage(SKImage.FromEncodedData(PngStream));

#if PROFILING
				Profiler.NewState("Scale");
#endif
				// Desired width and height for the downscaled image
				int DesiredWidth = OriginalBitmap.Width / 4;   //Reduce the width by a quarter
				int DesiredHeight = OriginalBitmap.Height / 4; //Reduce the height by a quarter

				// Create an SKImageInfo with the desired width, height, and color type of the original
				SKImageInfo resizedInfo = new(DesiredWidth, DesiredHeight, SKColorType.Gray8);

				// Create a new SKBitmap for the downscaled image
				SKBitmap resizedBitmap = OriginalBitmap.Resize(resizedInfo, SKFilterQuality.Medium);

#if PROFILING
				Profiler.NewState("Prepare");
#endif
				//Blur image
				IMatrix RezisedMatrix = Bitmaps.FromBitmap(resizedBitmap);
				IMatrix GreyChannelMatrix = RezisedMatrix.GrayScale();

#if PROFILING
				Profiler.NewState("Blur 5x5");
#endif
				IMatrix NewMatrix = IdApp.Cv.Transformations.Convolutions.ConvolutionOperations.Blur(GreyChannelMatrix, 5);

#if PROFILING
				Profiler.NewState("Blur2");
#endif
				NewMatrix = IdApp.Cv.Transformations.Convolutions.ConvolutionOperations.Blur(NewMatrix, 5);

#if PROFILING
				Profiler.NewState("Encode");
#endif
				// Continue with the blurring and encoding to PNG as before
				byte[] Blurred = Bitmaps.EncodeAsPng(NewMatrix);
				ImageSource BlurredScreen = ImageSource.FromStream(() => new MemoryStream(Blurred));

#if PROFILING
				Profiler.Stop();

				string TimingUml = Profiler.ExportPlantUml(TimeUnit.MilliSeconds);

				await App.SendAlert("```uml\r\n" + TimingUml + "\r\n```", "text/markdown");
#endif
				return BlurredScreen;
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				return null;
			}
		}

		/// <summary>
		/// Takes a screen-shot.
		/// </summary>
		/// <returns>Screen shot, if able.</returns>
		public async Task<ImageSource?> TakeScreenshotAsync()
		{
			try
			{
				IScreenshotResult? Result = await Screenshot.CaptureAsync();
				if (Result is null)
					return null;

				// Read the stream into a memory stream or byte array
				using Stream Stream = await Result.OpenReadAsync();
				MemoryStream MemoryStream = new();
				await Stream.CopyToAsync(MemoryStream);
				byte[] Bytes = MemoryStream.ToArray();

				// Return a new MemoryStream based on the byte array for each invocation
				return ImageSource.FromStream(() => new MemoryStream(Bytes));
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				return null;
			}
		}

		#endregion

		#region Navigation



		/// <inheritdoc/>
		public Page CurrentPage => Shell.Current.CurrentPage;

		/// <inheritdoc/>
		public override Task Load(bool IsResuming, CancellationToken CancellationToken)
		{
			if (this.BeginLoad(IsResuming, CancellationToken))
			{
				try
				{
					Application? Application = Application.Current;

					if (Application is not null)
					{
						Application.PropertyChanging += this.OnApplicationPropertyChanging;
						Application.PropertyChanged += this.OnApplicationPropertyChanged;
					}

					this.SubscribeToShellNavigatingIfNecessary(Application);

					this.EndLoad(true);
				}
				catch (Exception ex)
				{
					ServiceRef.LogService.LogException(ex);
					this.EndLoad(false);
				}
			}


			return Task.CompletedTask;
		}

		/// <inheritdoc/>
		public override Task Unload()
		{
			if (this.BeginUnload())
			{
				try
				{
					Application? Application = Application.Current;
					if (Application is not null)
					{
						this.UnsubscribeFromShellNavigatingIfNecessary(Application);
						Application.PropertyChanged -= this.OnApplicationPropertyChanged;
						Application.PropertyChanging -= this.OnApplicationPropertyChanging;
					}
				}
				catch (Exception ex)
				{
					ServiceRef.LogService.LogException(ex);
				}

				this.EndUnload();
			}

			return Task.CompletedTask;
		}


		/// <inheritdoc/>
		public Task GoToAsync(string Route, BackMethod BackMethod = BackMethod.Inherited, string? UniqueId = null)
		{
			// No args navigation will create a default navigation arguments
			return this.GoToAsync<NavigationArgs>(Route, null, BackMethod, UniqueId);
		}

		/// <inheritdoc/>
		public async Task GoToAsync<TArgs>(string Route, TArgs? Args, BackMethod BackMethod = BackMethod.Inherited, string? UniqueId = null) where TArgs : NavigationArgs, new()
		{
			await MainThread.InvokeOnMainThreadAsync(async () =>
			{
				ServiceRef.PlatformSpecific.HideKeyboard();

				// Get the parent's navigation arguments
				NavigationArgs? ParentArgs = this.GetCurrentNavigationArgs();

				// Create a default navigation arguments if Args are null
				NavigationArgs NavigationArgs = Args ?? new();
		
				NavigationArgs.SetBackArguments(ParentArgs, BackMethod, UniqueId);
				this.PushArgs(Route, NavigationArgs);

				if (!string.IsNullOrEmpty(UniqueId))
					Route += "?UniqueId=" + UniqueId;

				try
				{
					this.isNavigating = true;
					await Shell.Current.GoToAsync(Route, NavigationArgs.Animated);
				}
				catch (Exception ex)
				{
					ex = Log.UnnestException(ex);
					ServiceRef.LogService.LogException(ex);
					string ExtraInfo = Environment.NewLine + ex.Message;

					await ServiceRef.UiService.DisplayAlert(
						ServiceRef.Localizer[nameof(AppResources.ErrorTitle)],
						ServiceRef.Localizer[nameof(AppResources.FailedToNavigateToPage), Route, ExtraInfo]);
				}
				finally
				{
					this.isNavigating = false;
					NavigationArgs.NavigationCompletionSource.TrySetResult(true);
				}
			});
		}

		/// <inheritdoc/>
		public async Task GoBackAsync(bool Animate = true)
		{
			try
			{
				NavigationArgs? NavigationArgs = this.GetCurrentNavigationArgs();

				if (NavigationArgs is not null) // the main view?
				{
					string BackRoute = NavigationArgs.GetBackRoute();

					this.isNavigating = true;
					await Shell.Current.GoToAsync(BackRoute, Animate);
				}
				else
				{
					ShellNavigationState State = Shell.Current.CurrentState;
					if (Uri.TryCreate(State.Location, "..", out Uri? BackLocation))
						await Shell.Current.GoToAsync(BackLocation);
					else
						await Shell.Current.GoToAsync(Constants.Pages.MainPage);
				}
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);

				await ServiceRef.UiService.DisplayAlert(
					ServiceRef.Localizer[nameof(AppResources.ErrorTitle)],
					ServiceRef.Localizer[nameof(AppResources.FailedToClosePage)]);
			}
			finally
			{
				this.isNavigating = false;
			}
		}

		/// <summary>
		/// Pops the latests navigation arguments. Can only be used once to get the navigation arguments. Called by constructors to find
		/// associated navigation arguments for a page being constructed.
		/// </summary>
		/// <returns>Latest navigation arguments, or null if not found.</returns>
		public TArgs? PopLatestArgs<TArgs>()
			where TArgs : NavigationArgs, new()
		{
			if (this.latestArguments is TArgs Result)
			{
				this.latestArguments = null;
				return Result;
			}
			else
				return null;
		}

		/// <summary>
		/// Returns the page's arguments from the (one-level) deep navigation stack.
		/// </summary>
		/// <param name="UniqueId">View's unique ID.</param>
		/// <returns>View's navigation arguments, or null if not found.</returns>
		public TArgs? TryGetArgs<TArgs>(string? UniqueId = null)
			where TArgs : NavigationArgs, new()
		{
			if (this.TryGetArgs(out TArgs? Result, UniqueId))
				return Result;
			else
				return null;
		}

		/// <summary>
		/// Returns the page's arguments from the (one-level) deep navigation stack.
		/// </summary>
		/// <param name="Args">View's navigation arguments.</param>
		/// <param name="UniqueId">View's unique ID.</param>
		public bool TryGetArgs<TArgs>([NotNullWhen(true)] out TArgs? Args, string? UniqueId = null)
			where TArgs : NavigationArgs, new()
		{
			NavigationArgs? NavigationArgs = null;

			if (this.CurrentPage is Page Page)
			{
				NavigationArgs = this.TryGetArgs(Page.GetType().Name, UniqueId);
				string Route = Routing.GetRoute(Page);
				NavigationArgs ??= this.TryGetArgs(Route, UniqueId);

				if ((NavigationArgs is null) && (UniqueId is null) &&
					(Page is BaseContentPage BasePage) && (BasePage.UniqueId is not null))
				{
					return this.TryGetArgs(out Args, BasePage.UniqueId);
				}
			}

			if (NavigationArgs is TArgs TArgsArgs)
				Args = TArgsArgs;
			else
				Args = null;

			return (Args is not null);
		}

		private NavigationArgs? GetCurrentNavigationArgs()
		{
			this.TryGetArgs(out NavigationArgs? Args);
			return Args;
		}

		private void OnApplicationPropertyChanged(object? Sender, System.ComponentModel.PropertyChangedEventArgs Args)
		{
			if (Args.PropertyName == nameof(Application.MainPage))
				this.SubscribeToShellNavigatingIfNecessary((Application?)Sender);
		}

		private void OnApplicationPropertyChanging(object? Sender, PropertyChangingEventArgs Args)
		{
			if (Args.PropertyName == nameof(Application.MainPage))
				this.UnsubscribeFromShellNavigatingIfNecessary((Application?)Sender);
		}

		private void SubscribeToShellNavigatingIfNecessary(Application? Application)
		{
			if (Application?.MainPage is Shell Shell)
				Shell.Navigating += this.Shell_Navigating;
		}

		private void UnsubscribeFromShellNavigatingIfNecessary(Application? Application)
		{
			if (Application?.MainPage is Shell Shell)
				Shell.Navigating -= this.Shell_Navigating;
		}

		private void Shell_Navigating(object? Sender, ShellNavigatingEventArgs e)
		{
			try
			{
				if ((e.Source == ShellNavigationSource.Pop) && e.CanCancel && !this.isNavigating)
				{
					e.Cancel();

					MainThread.BeginInvokeOnMainThread(async () =>
					{
						await this.GoBackAsync();
					});
				}
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
			}
		}

		private static bool TryGetPageName(string Route, [NotNullWhen(true)] out string? PageName)
		{
			PageName = null;

			if (!string.IsNullOrWhiteSpace(Route))
			{
				PageName = Route.TrimStart('.', '/');
				return !string.IsNullOrWhiteSpace(PageName);
			}

			return false;
		}

		private void PushArgs(string Route, NavigationArgs Args)
		{
			this.latestArguments = Args;

			if (TryGetPageName(Route, out string? PageName))
			{
				if (Args is not null)
				{
					string? UniqueId = Args.UniqueId;

					if (!string.IsNullOrEmpty(UniqueId))
						PageName += "?UniqueId=" + UniqueId;

					this.navigationArgsMap[PageName] = Args;
				}
				else
					this.navigationArgsMap.Remove(PageName);
			}
		}

		private NavigationArgs? TryGetArgs(string Route, string? UniqueId)
		{
			if (!string.IsNullOrEmpty(UniqueId))
				Route += "?UniqueId=" + UniqueId;

			if (TryGetPageName(Route, out string? PageName) &&
				this.navigationArgsMap.TryGetValue(PageName, out NavigationArgs? Args))
			{
				return Args;
			}

			return null;
		}

		#endregion

		#region Popups

		/// <summary>
		/// The current stack of popup pages.
		/// </summary>
		public ReadOnlyCollection<PopupPage?> PopupStack => new(this.popupStack.ToList());

		/// <inheritdoc/>
		public async Task PushAsync<TPage, TViewModel>() where TPage : BasePopup, new() where TViewModel : BasePopupViewModel, new()
		{
			TPage Page = new();
			TViewModel ViewModel = new();
			Page.ViewModel = ViewModel;

			this.popupStack.Push(Page);
			await MopupService.Instance.PushAsync(Page);
			await ViewModel.Popped;
		}

		/// <inheritdoc/>
		public async Task PushAsync<TPage, TViewModel>(TPage page, TViewModel viewModel) where TPage : BasePopup where TViewModel : BasePopupViewModel
		{
			page.ViewModel = viewModel;

			this.popupStack.Push(page);
			await MopupService.Instance.PushAsync(page);
			await page.ViewModel.Popped;
		}

		/// <inheritdoc/>
		public async Task PushAsync<TPage, TViewModel>(TPage page) where TPage : BasePopup where TViewModel : BasePopupViewModel, new()
		{
			TViewModel ViewModel = new();
			page.ViewModel = ViewModel;

			this.popupStack.Push(page);
			await MopupService.Instance.PushAsync(page);
			await ViewModel.Popped;
		}

		/// <inheritdoc/>
		public async Task PushAsync<TPage, TViewModel>(TViewModel viewModel) where TPage : BasePopup, new() where TViewModel : BasePopupViewModel
		{
			TPage Page = new()
			{
				ViewModel = viewModel
			};

			this.popupStack.Push(Page);
			await MopupService.Instance.PushAsync(Page);
			await viewModel.Popped;
		}

		/// <inheritdoc/>
		public async Task PushAsync<TPage>() where TPage : BasePopup, new()
		{
			TPage Page = new();
			Page.BindingContext ??= new BasePopupViewModel();

			this.popupStack.Push(Page);
			await MopupService.Instance.PushAsync(Page);
			if(Page.ViewModel is not null)
				await Page.ViewModel.Popped;
		}

		/// <inheritdoc/>
		public async Task PushAsync<TPage>(TPage page) where TPage : BasePopup
		{
			page.ViewModel ??= new BasePopupViewModel();

			this.popupStack.Push(page);
			await MopupService.Instance.PushAsync(page);
			await page.ViewModel.Popped;
		}

		/// <inheritdoc/>
		public async Task<TReturn?> PushAsync<TPage, TViewModel, TReturn>() where TPage : BasePopup, new() where TViewModel : ReturningPopupViewModel<TReturn>, new()
		{
			TPage Page = new();
			TViewModel ViewModel = new();
			Page.ViewModel = ViewModel;

			this.popupStack.Push(Page);
			await MopupService.Instance.PushAsync(Page);
			return await ViewModel.Result;
		}

		/// <inheritdoc/>
		public async Task<TReturn?> PushAsync<TPage, TViewModel, TReturn>(TPage page, TViewModel viewModel) where TPage : BasePopup where TViewModel : ReturningPopupViewModel<TReturn>
		{
			page.ViewModel = viewModel;

			this.popupStack.Push(page);
			await MopupService.Instance.PushAsync(page);
			return await viewModel.Result;
		}

		/// <inheritdoc/>
		public async Task<TReturn?> PushAsync<TPage, TViewModel, TReturn>(TPage page) where TPage : BasePopup
			where TViewModel : ReturningPopupViewModel<TReturn>, new()
		{
			TViewModel ViewModel = new();
			page.ViewModel = ViewModel;

			this.popupStack.Push(page);
			await MopupService.Instance.PushAsync(page);
			return await ViewModel.Result;
		}

		/// <inheritdoc/>
		public async Task<TReturn?> PushAsync<TPage, TViewModel, TReturn>(TViewModel viewModel) where TPage : BasePopup, new() where TViewModel : ReturningPopupViewModel<TReturn>
		{
			TPage Page = new()
			{
				ViewModel = viewModel
			};

			this.popupStack.Push(Page);
			await MopupService.Instance.PushAsync(Page);

			return await viewModel.Result;
		}

		/// <inheritdoc/>
		public async Task PopAsync()
		{
			if (this.popupStack.Count == 0)
				return;
			try
			{
				object? ViewModel = this.popupStack.Pop()?.BindingContext;
				if (ViewModel is BasePopupViewModel BaseVm)
					await BaseVm.OnPopInternal();

				await MopupService.Instance.PopAsync();
			}
			catch (Exception Ex)
			{
				ServiceRef.LogService.LogException(Ex);
			}
		}

		#endregion

		#region Image
		/// <inheritdoc/>  
		public async Task<ImageSource?> ConvertSvgUriToImageSource(string svgUri)
		{
			try
			{
				//Fetch image
				using HttpClient httpClient = new();
				using HttpResponseMessage response = await httpClient.GetAsync(svgUri);
				if (!response.IsSuccessStatusCode)
					return null;

				// Load SVG image
				byte[] contentBytes = await response.Content.ReadAsByteArrayAsync();
				SKSvg svg = new();
				using (MemoryStream stream = new(contentBytes))
				{
					svg.Load(stream);
				}

				//Check that the svg was parsed correct
				if (svg.Picture is null)
					return null;

				using (MemoryStream stream = new())
				{
					if (svg.Picture.ToImage(stream, SKColor.Parse("#00FFFFFF"), SKEncodedImageFormat.Png, 100, 1, 1, SKColorType.Rgba8888, SKAlphaType.Premul, SKColorSpace.CreateSrgb()))
						return ImageSource.FromStream(() => new MemoryStream(stream.ToArray()));
					return null;
				}
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				return null;
			}

		}
		#endregion
	}
}
