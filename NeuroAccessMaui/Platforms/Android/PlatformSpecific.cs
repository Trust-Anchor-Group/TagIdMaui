using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
//using Android.Gms.Extensions;
using Android.Graphics;
using Android.OS;
using Android.Renderscripts;
using Android.Runtime;
using Android.Views;
using Android.Views.InputMethods;
using AndroidX.Biometric;
using AndroidX.Fragment.App;
using AndroidX.Lifecycle;
using CommunityToolkit.Mvvm.Messaging;

//using Firebase.Messaging;	// TODO: Firebase
using Java.Util.Concurrent;
using NeuroAccessMaui.Services.Push;
using Waher.Events;
using Waher.Networking.XMPP.Push;
using Rect = Android.Graphics.Rect;

namespace NeuroAccessMaui.Services
{
	/// <summary>
	/// Android implementation of platform-specific features.
	/// </summary>
	public class PlatformSpecific : IPlatformSpecific
	{
		private bool isDisposed;

		/// <summary>
		/// Android implementation of platform-specific features.
		/// </summary>
		public PlatformSpecific()
		{
			this.InitializeKeyboard();
		}

		/// <summary>
		/// <see cref="IDisposable.Dispose"/>
		/// </summary>
		public void Dispose()
		{
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// <see cref="IDisposable.Dispose"/>
		/// </summary>
		protected virtual void Dispose(bool Disposing)
		{
			if (this.isDisposed)
				return;

			if (Disposing)
			{
				protectionTimer?.Dispose();
				protectionTimer = null;
			}

			this.isDisposed = true;
		}

		private static bool screenProtected = true; // App started with screen protected.
		private static Timer? protectionTimer = null;

		/// <summary>
		/// If screen capture prohibition is supported
		/// </summary>
		public bool CanProhibitScreenCapture => true;

		/// <summary>
		/// If screen capture is prohibited or not.
		/// </summary>
		public bool ProhibitScreenCapture
		{
			get => screenProtected;
			set
			{
				try
				{
					protectionTimer?.Dispose();
					protectionTimer = null;

					if (screenProtected != value)
					{
						this.SetScreenSecurityProtection(value);
						screenProtected = value;
					}
				}
				catch (Exception ex)
				{
					Log.Exception(ex);
				}
			}
		}

		private void SetScreenSecurityProtection(bool Enabled)
		{
			MainThread.BeginInvokeOnMainThread(() =>
			{
				try
				{
					Activity? Activity = Platform.CurrentActivity;

					if (Activity is not null)
					{
						if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
						{
#pragma warning disable CA1416
							Activity.SetRecentsScreenshotEnabled(!Enabled);
#pragma warning restore CA1416
						}

						if (Enabled)
						{
							Activity.Window?.SetFlags(WindowManagerFlags.Secure, WindowManagerFlags.Secure);
							protectionTimer = new Timer(this.ProtectionTimerElapsed, null,
								Constants.Security.MaxScreenRecordingTimeSeconds * 1000, Timeout.Infinite);
						}
						else
							Activity.Window?.ClearFlags(WindowManagerFlags.Secure);
					}
				}
				catch (Exception ex)
				{
					Log.Exception(ex);
				}
			});
		}

		private void ProtectionTimerElapsed(object? P)
		{
			MainThread.BeginInvokeOnMainThread(() => this.ProhibitScreenCapture = false);
		}

		/// <summary>
		/// Gets the ID of the device
		/// </summary>
		public string? GetDeviceId()
		{
			ContentResolver? ContentResolver = Android.App.Application.Context.ContentResolver;

			if (ContentResolver is not null)
			{
				return Android.Provider.Settings.Secure.GetString(ContentResolver, Android.Provider.Settings.Secure.AndroidId);
			}
			try
			{
				App.SendAlertAsync("Unable to get AndroidID, ContentResolver was null", "text/plain").Wait();
				this.CloseApplication().Wait();
			}
			catch (Exception)
			{
				System.Environment.Exit(0);
			}

			return null;
		}


		/// <summary>
		/// Closes the application
		/// </summary>
		public Task CloseApplication()
		{
			Activity? Activity = Platform.CurrentActivity;
			Activity?.FinishAffinity();

			Java.Lang.JavaSystem.Exit(0);

			return Task.CompletedTask;
		}

		/// <summary>
		/// Shares an image in PNG format.
		/// </summary>
		/// <param name="PngFile">Binary representation (PNG format) of image.</param>
		/// <param name="Message">Message to send with image.</param>
		/// <param name="Title">Title for operation.</param>
		/// <param name="FileName">Filename of image file.</param>
		public void ShareImage(byte[] PngFile, string Message, string Title, string FileName)
		{
			Context Context = Android.App.Application.Context;
			Java.IO.File? ExternalFilesDir = Context.GetExternalFilesDir("");

			if (ExternalFilesDir is null)
				return;

			if (!Directory.Exists(ExternalFilesDir.Path))
				Directory.CreateDirectory(ExternalFilesDir.Path);

			Java.IO.File fileDir = new(ExternalFilesDir.AbsolutePath + (Java.IO.File.Separator + FileName));

			File.WriteAllBytes(fileDir.Path, PngFile);

			Intent Intent = new(Intent.ActionSend);
			Intent.PutExtra(Intent.ExtraText, Message);
			Intent.SetType(Constants.MimeTypes.Png);

			Intent.AddFlags(ActivityFlags.GrantReadUriPermission);
			Intent.AddFlags(ActivityFlags.GrantWriteUriPermission);
			Intent.PutExtra(Intent.ExtraStream, FileProvider.GetUriForFile(Context, "com.tag.IdApp.fileprovider", fileDir));

			Intent? MyIntent = Intent.CreateChooser(Intent, Title);

			if (MyIntent is not null)
			{
				MyIntent.AddFlags(ActivityFlags.NewTask);
				Context.StartActivity(MyIntent);
			}
		}


		/// <summary>
		/// Make a blurred screenshot
		/// TODO: Just make a screen shot. Use the portable CV library to blur it.
		/// </summary>
		public Task<byte[]> CaptureScreen(int blurRadius)
		{
			blurRadius = Math.Min(25, Math.Max(blurRadius, 0));

			Activity? activity = Platform.CurrentActivity;
			Android.Views.View? rootView = activity?.Window?.DecorView.RootView;

			if (rootView is null)
				return Task.FromResult<byte[]>([]);

			using Bitmap screenshot = Bitmap.CreateBitmap(rootView.Width, rootView.Height, Bitmap.Config.Argb8888!);
			Canvas canvas = new(screenshot);
			rootView.Draw(canvas);

			Bitmap? Blurred = null;

			if (activity != null && (int)Android.OS.Build.VERSION.SdkInt >= 17)
				Blurred = ToBlurred(screenshot, activity, blurRadius);
			else
				Blurred = ToLegacyBlurred(screenshot, blurRadius);

			MemoryStream Stream = new();
			Blurred.Compress(Bitmap.CompressFormat.Jpeg!, 80, Stream);
			Stream.Seek(0, SeekOrigin.Begin);

			return Task.FromResult(Stream.ToArray());
		}

		private static Bitmap ToBlurred(Bitmap originalBitmap, Activity? Activity, int radius)
		{
			// Create another bitmap that will hold the results of the filter.
			Bitmap blurredBitmap = Bitmap.CreateBitmap(originalBitmap);
			RenderScript? renderScript = RenderScript.Create(Activity);

			// Load up an instance of the specific script that we want to use.
			// An Element is similar to a C type. The second parameter, Element.U8_4,
			// tells the Allocation is made up of 4 fields of 8 unsigned bits.
			ScriptIntrinsicBlur? script = ScriptIntrinsicBlur.Create(renderScript, Android.Renderscripts.Element.U8_4(renderScript));

			// Create an Allocation for the kernel inputs.
			Allocation? input = Allocation.CreateFromBitmap(renderScript, originalBitmap, Allocation.MipmapControl.MipmapFull,
				AllocationUsage.Script);

			// Assign the input Allocation to the script.
			script?.SetInput(input);

			// Set the blur radius
			script?.SetRadius(radius);

			// Finally we need to create an output allocation to hold the output of the Renderscript.
			Allocation? output = Allocation.CreateTyped(renderScript, input?.Type);

			// Next, run the script. This will run the script over each Element in the Allocation, and copy it's
			// output to the allocation we just created for this purpose.
			script?.ForEach(output);

			// Copy the output to the blurred bitmap
			output?.CopyTo(blurredBitmap);

			// Cleanup.
			output?.Destroy();
			input?.Destroy();
			script?.Destroy();
			renderScript?.Destroy();

			return blurredBitmap;
		}

		// Source: http://incubator.quasimondo.com/processing/superfast_blur.php
		public static Bitmap ToLegacyBlurred(Bitmap source, int radius)
		{
			Bitmap.Config? config = source.GetConfig();
			config ??= Bitmap.Config.Argb8888;    // This will support transparency

			Bitmap? img = source.Copy(config!, true);

			int w = img!.Width;
			int h = img.Height;
			int wm = w - 1;
			int hm = h - 1;
			int wh = w * h;
			int div = radius + radius + 1;
			int[] r = new int[wh];
			int[] g = new int[wh];
			int[] b = new int[wh];
			int rsum, gsum, bsum, x, y, i, p, p1, p2, yp, yi, yw;
			int[] vmin = new int[Math.Max(w, h)];
			int[] vmax = new int[Math.Max(w, h)];
			int[] pix = new int[w * h];

			img.GetPixels(pix, 0, w, 0, 0, w, h);

			int[] dv = new int[256 * div];
			for (i = 0; i < 256 * div; i++)
				dv[i] = (i / div);

			yw = yi = 0;

			for (y = 0; y < h; y++)
			{
				rsum = gsum = bsum = 0;
				for (i = -radius; i <= radius; i++)
				{
					p = pix[yi + Math.Min(wm, Math.Max(i, 0))];
					rsum += (p & 0xff0000) >> 16;
					gsum += (p & 0x00ff00) >> 8;
					bsum += p & 0x0000ff;
				}
				for (x = 0; x < w; x++)
				{

					r[yi] = dv[rsum];
					g[yi] = dv[gsum];
					b[yi] = dv[bsum];

					if (y == 0)
					{
						vmin[x] = Math.Min(x + radius + 1, wm);
						vmax[x] = Math.Max(x - radius, 0);
					}

					p1 = pix[yw + vmin[x]];
					p2 = pix[yw + vmax[x]];

					rsum += ((p1 & 0xff0000) - (p2 & 0xff0000)) >> 16;
					gsum += ((p1 & 0x00ff00) - (p2 & 0x00ff00)) >> 8;
					bsum += (p1 & 0x0000ff) - (p2 & 0x0000ff);
					yi++;
				}
				yw += w;
			}

			for (x = 0; x < w; x++)
			{
				rsum = gsum = bsum = 0;
				yp = -radius * w;
				for (i = -radius; i <= radius; i++)
				{
					yi = Math.Max(0, yp) + x;
					rsum += r[yi];
					gsum += g[yi];
					bsum += b[yi];
					yp += w;
				}
				yi = x;
				for (y = 0; y < h; y++)
				{
					// Preserve alpha channel: ( 0xff000000 & pix[yi] )
					int rgb = (dv[rsum] << 16) | (dv[gsum] << 8) | dv[bsum];
					pix[yi] = ((int)(0xff000000 & pix[yi]) | rgb);
					if (x == 0)
					{
						vmin[y] = Math.Min(y + radius + 1, hm) * w;
						vmax[y] = Math.Max(y - radius, 0) * w;
					}
					p1 = x + vmin[y];
					p2 = x + vmax[y];

					rsum += r[p1] - r[p2];
					gsum += g[p1] - g[p2];
					bsum += b[p1] - b[p2];

					yi += w;
				}
			}

			img.SetPixels(pix, 0, w, 0, 0, w, h);
			return img;
		}

		/// <summary>
		/// If the device supports authenticating the user using fingerprints.
		/// </summary>
		public bool SupportsFingerprintAuthentication
		{
			get
			{
				try
				{
					if (Build.VERSION.SdkInt < BuildVersionCodes.M)
						return false;

					Context Context = Android.App.Application.Context;

					if (Context.CheckCallingOrSelfPermission(Manifest.Permission.UseBiometric) != Permission.Granted &&
						Context.CheckCallingOrSelfPermission(Manifest.Permission.UseFingerprint) != Permission.Granted)
					{
						return false;
					}

					BiometricManager Manager = BiometricManager.From(Android.App.Application.Context);
					int Level = BiometricManager.Authenticators.BiometricWeak;

					return Manager.CanAuthenticate(Level) == BiometricManager.BiometricSuccess;

					// TODO: AndroidX package conflicts arose between Maui & Xamarin.AndroidX.Biometrics package, that the
					//       Plugin.Fingerprint seems to have resolved. Using this library while Maui fixes the problem, even
					//       though the library is not used in code.

					// TODO: Consider alternative levels:
					//
					//			public interface Authenticators {
					//			        /**
					//			         * Any biometric (e.g. fingerprint, iris, or face) on the device that meets or exceeds the
					//			         * requirements for <strong>Class 3</strong> (formerly <strong>Strong</strong>), as defined
					//			         * by the Android CDD.
					//			         */
					//			        int BIOMETRIC_STRONG = 0x000F;
					//			
					//			        /**
					//			         * Any biometric (e.g. fingerprint, iris, or face) on the device that meets or exceeds the
					//			         * requirements for <strong>Class 2</strong> (formerly <strong>Weak</strong>), as defined by
					//			         * the Android CDD.
					//			         *
					//			         * <p>Note that this is a superset of {@link #BIOMETRIC_STRONG} and is defined such that
					//			         * {@code BIOMETRIC_STRONG | BIOMETRIC_WEAK == BIOMETRIC_WEAK}.
					//			         */
					//			        int BIOMETRIC_WEAK = 0x00FF;
					//			
					//			        /**
					//			         * The non-biometric credential used to secure the device (i.e. PIN, pattern, or password).
					//			         * This should typically only be used in combination with a biometric auth type, such as
					//			         * {@link #BIOMETRIC_WEAK}.
					//			         */
					//			        int DEVICE_CREDENTIAL = 1 << 15;
					//			    }
				}
				catch (Exception)
				{
					return false;
				}
			}
		}

		/// <summary>
		/// Gets the biometric method supported by the device.
		/// Currently on android, you cannot determine if the device will use fingerprint or face recognition.
		/// Unknown is returned if the device supports biometric authentication, but the method is unknown.
		/// </summary>
		/// <returns>The BiometricMethod which is preferred/supported on this device</returns>
		public BiometricMethod GetBiometricMethod()
		{
			if (Build.VERSION.SdkInt < BuildVersionCodes.M)
				return BiometricMethod.None;

			Context Context = Android.App.Application.Context;

			if (Context.CheckCallingOrSelfPermission(Manifest.Permission.UseBiometric) != Permission.Granted &&
				 Context.CheckCallingOrSelfPermission(Manifest.Permission.UseFingerprint) != Permission.Granted)
			{
				return BiometricMethod.None;
			}

			BiometricManager Manager = BiometricManager.From(Context);
			const int Level = BiometricManager.Authenticators.BiometricWeak;
			return Manager.CanAuthenticate(Level) == BiometricManager.BiometricSuccess ? BiometricMethod.Unknown : BiometricMethod.None;
		}

		/// <summary>
		/// Authenticates the user using the fingerprint sensor.
		/// </summary>
		/// <param name="Title">Title of authentication dialog.</param>
		/// <param name="Subtitle">Optional Subtitle.</param>
		/// <param name="Description">Description texst to display to user in authentication dialog.</param>
		/// <param name="Cancel">Label for Cancel button.</param>
		/// <param name="CancellationToken">Optional cancellation token, to cancel process.</param>
		/// <returns>If the user has been successfully authenticated.</returns>
		public async Task<bool> AuthenticateUserFingerprint(string Title, string? Subtitle, string Description, string Cancel,
			CancellationToken? CancellationToken)
		{
			if (!this.SupportsFingerprintAuthentication)
				return false;

			if (string.IsNullOrWhiteSpace(Title))
				throw new ArgumentException("Title cannot be empty.", nameof(Title));

			if (Platform.CurrentActivity is not FragmentActivity Activity)
				return false;

			try
			{
				BiometricPrompt.PromptInfo.Builder Builder = new();

				Builder.SetAllowedAuthenticators(BiometricManager.Authenticators.BiometricWeak);

				Builder.SetConfirmationRequired(false);
				Builder.SetTitle(Title);
				Builder.SetDescription(Description);
				Builder.SetNegativeButtonText(Cancel);

				if (!string.IsNullOrEmpty(Subtitle))
					Builder.SetSubtitle(Subtitle);

				BiometricPrompt.PromptInfo Prompt = Builder.Build();
				IExecutorService? Executor = Executors.NewSingleThreadExecutor();
				CallbackHandler Handler = new();
				CancellationTokenRegistration? Registration = null;

				BiometricPrompt Dialog = new(Activity, Executor, Handler);
				try
				{
					Registration = CancellationToken?.Register(Dialog.CancelAuthentication);
					Dialog.Authenticate(Prompt);

					return await Handler.Result;
				}
				finally
				{
					if (Registration.HasValue)
						Registration.Value.Dispose();

					// Remove the lifecycle observer that is set by the BiometricPrompt.
					// Reference: https://stackoverflow.com/a/59637670/1489968
					// Review after referenced nugets (or Maui) has been updated.

					Java.Lang.Class Class = Java.Lang.Class.FromType(Dialog.GetType());
					Java.Lang.Reflect.Field[] Fields = Class.GetDeclaredFields();
					Java.Lang.Reflect.Field? LifecycleObserver = Fields?.FirstOrDefault(f => f.Name == "mLifecycleObserver");

					if (LifecycleObserver is not null)
					{
						LifecycleObserver.Accessible = true;
						ILifecycleObserver? LastLifecycleObserver = LifecycleObserver.Get(Dialog).JavaCast<ILifecycleObserver>();
						Lifecycle? Lifecycle = Activity.Lifecycle;

						if (LastLifecycleObserver is not null && Lifecycle is not null)
							Lifecycle.RemoveObserver(LastLifecycleObserver);
					}

					Dialog?.Dispose();
				}
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				return false;
			}
		}

		private class CallbackHandler : BiometricPrompt.AuthenticationCallback, IDialogInterfaceOnClickListener
		{
			private readonly TaskCompletionSource<bool> result = new();

			public Task<bool> Result => this.result.Task;

			public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult Result)
			{
				base.OnAuthenticationSucceeded(Result);
				this.result.TrySetResult(true);
			}

			public override void OnAuthenticationError(int ErrorCode, Java.Lang.ICharSequence ErrorString)
			{
				base.OnAuthenticationError(ErrorCode, ErrorString);
				this.result.TrySetResult(false);
			}

			public override void OnAuthenticationFailed()
			{
				base.OnAuthenticationFailed();
				this.result.TrySetResult(false);
			}

			public void OnClick(IDialogInterface? Dialog, int Which)
			{
				this.result.TrySetResult(false);
			}
		}

		/// <summary>
		/// Gets a Push Notification token for the device.
		/// </summary>
		/// <returns>Token, Service used, and type of client.</returns>
		public Task<TokenInformation> GetPushNotificationToken()
		{
			Java.Lang.Object? Token = string.Empty;

			try
			{
				Token = string.Empty;   // await FirebaseMessaging.Instance.GetToken().AsAsync<Java.Lang.Object>();		// TODO: Firebase
			}
			catch (Exception ex)
			{
				Log.Exception(ex);
			}

			TokenInformation TokenInformation = new()
			{
				Token = Token?.ToString(),
				ClientType = ClientType.Android,
				Service = PushMessagingService.Firebase
			};

			return Task.FromResult(TokenInformation);
		}

		#region Keyboard

		/// <inheritdoc />
		public event EventHandler<KeyboardSizeMessage>? KeyboardShown;
		/// <inheritdoc />
		public event EventHandler<KeyboardSizeMessage>? KeyboardHidden;
		/// <summary>
		///	Fired when the keyboard size changes.
		/// </summary>>
		/// <remarks>
		///	On Android, the keyboard size is not constantly updated, but only when the keyboard is shown or hidden.
		/// </remarks>
		public event EventHandler<KeyboardSizeMessage>? KeyboardSizeChanged;

		private Activity? activity;
		private Android.Views.View? rootView;
		private int lastKeyboardHeight = 0;
		private Handler? initializeKeyboardHandler;

		/// <inheritdoc/>
		public void HideKeyboard()
		{
			if (this.activity is null || this.rootView is null)
				return;
			InputMethodManager? inputMethodManager = this.activity.GetSystemService(Context.InputMethodService) as InputMethodManager;
			inputMethodManager?.HideSoftInputFromWindow(this.rootView.WindowToken, HideSoftInputFlags.None);
			this.activity.Window?.DecorView.ClearFocus();
		}

		private void InitializeKeyboard()
		{
			this.activity = Platform.CurrentActivity;
			try
			{
				this.initializeKeyboardHandler = new Handler(Looper.MainLooper!);
				this.CheckRootView();
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
			}
		}

		private void CheckRootView()
		{
			this.activity = Platform.CurrentActivity;

			if (this.activity?.Window?.DecorView.RootView?.ViewTreeObserver is null)
			{
				this.initializeKeyboardHandler?.PostDelayed(this.CheckRootView, 100);
				return;
			}
			this.activity = Platform.CurrentActivity;
			this.rootView = this.activity!.Window!.DecorView.RootView;
			this.rootView!.ViewTreeObserver!.GlobalLayout += this.OnGlobalLayout;
		}



		private void OnGlobalLayout(object? sender, EventArgs e)
		{
			Rect r = new();
			this.rootView!.GetWindowVisibleDisplayFrame(r);

			int screenHeight = this.rootView.RootView!.Height;
			int statusBarHeight = 0;
			int actionBarHeight = 0;

			// if this succeeds, we can calculate an exact keyboard height
			Android.Views.View? contentView = this.rootView.FindViewById(Android.Views.Window.IdAndroidContent);
			if (contentView is not null)
			{
				statusBarHeight = r.Top - contentView.Top;
				actionBarHeight = r.Bottom - contentView.Bottom;
			}

			// Calculate the height of the keyboard (if the above fails, the keyboardsize will include the size of the action and status bar)
			int availableScreenHeight = screenHeight - statusBarHeight - actionBarHeight;
			int visibleHeight = r.Height();
			int keypadHeight = availableScreenHeight - visibleHeight; // height of the keyboard, but is not garanteed to be the actual keyboard height it might include other things such as the action bar.

			// Assume keyboard is shown if more than 15% of the available screen height is used.
			// This is a heuristic, and may need to be adjusted.
			// I really don't like this solution, but android doesn't provide a better way to detect the keyboard at the time of writing.
			// Checking keyboardheight > 0 is not enough, because the keyboardheight is not garanteed to be accurate on all devices and circumstances

			if (keypadHeight > availableScreenHeight * 0.15)
			{
				this.lastKeyboardHeight = keypadHeight;
				this.KeyboardSizeChanged.Raise(this, new KeyboardSizeMessage(keypadHeight));
				WeakReferenceMessenger.Default.Send(new KeyboardSizeMessage(keypadHeight));
				this.KeyboardShown.Raise(this, new KeyboardSizeMessage(keypadHeight));
			}
			else
			{
				if (this.lastKeyboardHeight == 0)
					return;

				this.lastKeyboardHeight = 0;
				this.KeyboardSizeChanged.Raise(this, new KeyboardSizeMessage(0));
				WeakReferenceMessenger.Default.Send(new KeyboardSizeMessage(0));
				this.KeyboardHidden.Raise(this, new KeyboardSizeMessage(0));
			}
		}
		#endregion

	}


}
