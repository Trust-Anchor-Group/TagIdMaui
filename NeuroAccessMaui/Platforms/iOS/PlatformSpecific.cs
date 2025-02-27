using Foundation;
using LocalAuthentication;
using NeuroAccessMaui.Services.Push;
using ObjCRuntime;
using System.Diagnostics.CodeAnalysis;
using UIKit;
using Waher.Events;
using Waher.Networking.XMPP.Push;
using Security;
using System.Text;
using CommunityToolkit.Mvvm.Messaging;
namespace NeuroAccessMaui.Services
{
	/// <summary>
	/// iOS implementation of platform-specific features.
	/// </summary>
	public class PlatformSpecific : IPlatformSpecific
	{
		private LAContext? localAuthenticationContext;
		private bool isDisposed;

		/// <summary>
		/// iOS implementation of platform-specific features.
		/// </summary>
		public PlatformSpecific()
		{
			NSNotificationCenter.DefaultCenter.AddObserver(UIKeyboard.WillShowNotification, this.OnKeyboardWillShow);
			NSNotificationCenter.DefaultCenter.AddObserver(UIKeyboard.WillHideNotification, this.OnKeyboardWillHide);
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

			NSNotificationCenter.DefaultCenter.RemoveObserver(UIKeyboard.WillShowNotification);
			NSNotificationCenter.DefaultCenter.RemoveObserver(UIKeyboard.WillHideNotification);

			if (Disposing)
				this.DisposeLocalAuthenticationContext();

			this.isDisposed = true;
		}

		/// <summary>
		/// If screen capture prohibition is supported
		/// </summary>
		public bool CanProhibitScreenCapture => false;

		/// <summary>
		/// If screen capture is prohibited or not.
		/// </summary>
		public bool ProhibitScreenCapture // iOS doesn't support screen protection
		{
			get => false;
			set => _ = value; // ignore the value
		}

		/// <summary>
		/// Gets A persistent ID of the device
		/// Fetches the device ID from the keychain, or creates a new one if it doesn't exist.
		/// Errors results in an alert and the application closing.
		/// </summary>
		public string? GetDeviceId()
		{
			try
			{
				string ServiceName = AppInfo.PackageName;
				const string AccountName = "DeviceIdentifier"; //Basically the key

				// Define the search criteria for the SecRecord
				SecRecord searchRecord = new(SecKind.GenericPassword)
				{
					Service = ServiceName,
					Account = AccountName
				};

				// Try to retrieve the existing device identifier from the Keychain
				SecRecord? existingRecord = SecKeyChain.QueryAsRecord(searchRecord, out SecStatusCode resultCode);
				if (resultCode == SecStatusCode.Success && existingRecord is not null && existingRecord?.ValueData is not null)
				{
					// If the record exists, return the identifier
					return existingRecord.ValueData.ToString(NSStringEncoding.UTF8);
				}
				else if (resultCode == SecStatusCode.ItemNotFound)
				{
					// No existing record found, create a new device identifier
					string identifier = UIDevice.CurrentDevice.IdentifierForVendor.ToString();

					// Define the SecRecord for storing the new identifier
					SecRecord newRecord = new(SecKind.GenericPassword)
					{
						Service = ServiceName,
						Account = AccountName,
						Label = "Persistent Device Identifier for Vendor",
						ValueData = NSData.FromString(identifier),
						Accessible = SecAccessible.WhenUnlockedThisDeviceOnly,
						Synchronizable = false
					};

					// Sanity check: Remove any existing record, which should not exist
					SecKeyChain.Remove(newRecord);

					// Add the new item to the Keychain
					SecStatusCode addResult = SecKeyChain.Add(newRecord);
					if (addResult == SecStatusCode.Success)
						return identifier; // Return the newly stored identifier

					throw new Exception($"Unable to store device identifier in Keychain - Code: {addResult} - Description: {SecStatusCodeExtensions.GetStatusDescription(addResult)}");
				}
				else
					throw new Exception($"Unable to retrieve device identifier from Keychain - Code: {resultCode} - Description: {SecStatusCodeExtensions.GetStatusDescription(resultCode)}");
			}
			catch (Exception ex)
			{
				try
				{
					///TODO: Show a message to the user
					///TODO: The problem is that the app has not loaded the UI yet, so we can't show an alert.

					StringBuilder msg = new();

					msg.Append(ex.Message);
					msg.AppendLine();
					msg.AppendLine("```");
					msg.AppendLine(ex.StackTrace);
					msg.AppendLine("```");

					App.SendAlertAsync(msg.ToString(), "text/plain").Wait();
					this.CloseApplication().Wait();
				}
				catch (Exception)
				{
					Environment.Exit(0);
				}
			}
			return null;
		}

		/// <summary>
		/// Closes the application
		/// </summary>
		public Task CloseApplication()
		{
			if (this.localAuthenticationContext is not null)
			{
				if (this.localAuthenticationContext.RespondsToSelector(new Selector("invalidate")))
					this.localAuthenticationContext.Invalidate();

				this.localAuthenticationContext.Dispose();
				this.localAuthenticationContext = null;
			}

			Environment.Exit(0);
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
			UIImage? ImageObject = UIImage.LoadFromData(NSData.FromArray(PngFile));
			UIWindow? KeyWindow = UIApplication.SharedApplication?.KeyWindow;

			if ((ImageObject is null) || (KeyWindow is null))
				return;

			NSString MessageObject = new(Message);
			NSObject[] Items = [MessageObject, ImageObject];
			UIActivityViewController activityController = new(Items, null);

			UIViewController? topController = KeyWindow.RootViewController;

			if (topController is not null)
			{
				while (topController.PresentedViewController is not null)
					topController = topController.PresentedViewController;

				topController.PresentViewController(activityController, true, () => { });
			}
		}


		/// <summary>
		/// Make a blurred screenshot
		/// TODO: Just make a screen shot. Use the portable CV library to blur it.
		/// </summary>
		public Task<byte[]> CaptureScreen(int blurRadius = 25)
		{
			blurRadius = Math.Min(25, Math.Max(blurRadius, 0));
			UIImage? capture;

			using UIBlurEffect blurEffect = UIBlurEffect.FromStyle(UIBlurEffectStyle.Regular);
			using UIVisualEffectView blurWindow = new(blurEffect);

			blurWindow.Frame = UIScreen.MainScreen.Bounds;
			blurWindow.Alpha = Math.Min(1.0f, (1.0f / 25.0f) * blurRadius);

			UIView? subview = UIScreen.MainScreen.SnapshotView(true);
			//capture = UIScreen.MainScreen.Capture();
			//var subview = new UIImageView(capture);
			subview?.AddSubview(blurWindow);
			capture = subview?.Capture(true);
			blurWindow.RemoveFromSuperview();
			subview?.Dispose();

			//!!! capture.AsJPEG(.8f).AsStream();
			return Task.FromResult(Array.Empty<byte>());
		}

		/// <summary>
		/// If the device supports authenticating the user using fingerprints.
		/// </summary>
		public bool SupportsFingerprintAuthentication
		{
			get
			{
				if (!this.HasLocalAuthenticationContext)
					return false;

				try
				{
					if (!this.localAuthenticationContext.CanEvaluatePolicy(LAPolicy.DeviceOwnerAuthenticationWithBiometrics, out _))
						return false;

					return true;
				}
				catch (Exception ex)
				{
					ServiceRef.LogService.LogException(ex);
					return false;
				}
			}
		}

		/// <summary>
		/// Gets the biometric method supported by the device.
		/// Can return Face, Fingerprint, Unknown, or None.
		/// </summary>
		/// <returns>The BiometricMethod which is preferred/supported on this device</returns>
		public BiometricMethod GetBiometricMethod()
		{
			if (!this.HasLocalAuthenticationContext)
				return BiometricMethod.None;

			try
			{
				if (!this.localAuthenticationContext.CanEvaluatePolicy(LAPolicy.DeviceOwnerAuthenticationWithBiometrics, out _))
					return BiometricMethod.None;

				return this.localAuthenticationContext.BiometryType switch
				{
					LABiometryType.FaceId => BiometricMethod.FaceId,
					LABiometryType.TouchId => BiometricMethod.TouchId,
					_ => BiometricMethod.Unknown
				};
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				return BiometricMethod.Unknown;
			}
		}

		[MemberNotNullWhen(true, nameof(localAuthenticationContext))]
		private bool HasLocalAuthenticationContext
		{
			get
			{
				try
				{
					if (this.localAuthenticationContext is null)
					{
						NSProcessInfo ProcessInfo = new();
						NSOperatingSystemVersion MinVersion = new(10, 12, 0);
						if (!ProcessInfo.IsOperatingSystemAtLeastVersion(MinVersion))
							return false;

						if (!UIDevice.CurrentDevice.CheckSystemVersion(8, 0))
							return false;

						if (Class.GetHandle(typeof(LAContext)) == IntPtr.Zero)
							return false;

						this.localAuthenticationContext = new LAContext();
					}

					return true;
				}
				catch (Exception ex)
				{
					ServiceRef.LogService.LogException(ex);
					return false;
				}
			}
		}

		private void DisposeLocalAuthenticationContext()
		{
			if (this.localAuthenticationContext is not null)
			{
				if (this.localAuthenticationContext.RespondsToSelector(new Selector("invalidate")))
					this.localAuthenticationContext.Invalidate();

				this.localAuthenticationContext.Dispose();
				this.localAuthenticationContext = null;
			}
		}

		/// <summary>
		/// Authenticates the user using the fingerprint sensor.
		/// </summary>
		/// <param name="Title">Title of authentication dialog.</param>
		/// <param name="Subtitle">Optional Subtitle.</param>
		/// <param name="Description">Description texst to display to user in authentication dialog.</param>
		/// <param name="Cancel">Label for Cancel button.</param>
		/// <param name="RequireConfirmation">If user confirmation is required.</param>
		/// <param name="CancellationToken">Optional cancellation token, to cancel process.</param>
		/// <returns>If the user has been successfully authenticated.</returns>
		public async Task<bool> AuthenticateUserFingerprint(string Title, string? Subtitle, string Description, string Cancel,
			CancellationToken? CancellationToken)
		{
			if (!this.HasLocalAuthenticationContext)
				return false;

			CancellationTokenRegistration? Registration = null;

			try
			{
				if (this.localAuthenticationContext.RespondsToSelector(new Selector("localizedFallbackTitle")))
					this.localAuthenticationContext.LocalizedFallbackTitle = Title;

				if (this.localAuthenticationContext.RespondsToSelector(new Selector("localizedCancelTitle")))
					this.localAuthenticationContext.LocalizedCancelTitle = Cancel;

				Registration = CancellationToken?.Register(this.DisposeLocalAuthenticationContext);

				(bool Success, NSError _) = await this.localAuthenticationContext.EvaluatePolicyAsync(LAPolicy.DeviceOwnerAuthenticationWithBiometrics, Description);

				this.DisposeLocalAuthenticationContext();

				return Success;
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				return false;
			}
			finally
			{
				if (Registration.HasValue)
					Registration.Value.Dispose();
			}
		}

		/// <summary>
		/// Gets a Push Notification token for the device.
		/// </summary>
		/// <returns>Token, Service used, and type of client.</returns>
		public async Task<TokenInformation> GetPushNotificationToken()
		{
			string Token = string.Empty;

			try
			{
				//Token = Firebase.CloudMessaging.Messaging.SharedInstance.FcmToken ?? string.Empty;
			}
			catch (Exception ex)
			{
				Log.Exception(ex);
			}

			TokenInformation TokenInformation = new()
			{
				Token = Token,
				ClientType = ClientType.iOS,
				Service = PushMessagingService.Firebase
			};

			return await Task.FromResult(TokenInformation);
		}

		#region Keyboard
		public event EventHandler<KeyboardSizeMessage>? KeyboardShown;
		public event EventHandler<KeyboardSizeMessage>? KeyboardHidden;
		public event EventHandler<KeyboardSizeMessage>? KeyboardSizeChanged;

		/// <summary>
		/// Force hide the keyboard
		/// </summary>
		public void HideKeyboard()
		{
			AppDelegate.GetKeyWindow()?.EndEditing(true);
		}

		private void OnKeyboardWillShow(NSNotification notification)
		{
			CoreGraphics.CGRect keyboardFrame = UIKeyboard.FrameEndFromNotification(notification);
			float keyboardHeight = (float)keyboardFrame.Height;
			KeyboardShown.Raise(this, new KeyboardSizeMessage(keyboardHeight));
			KeyboardSizeChanged.Raise(this, new KeyboardSizeMessage(keyboardHeight));
			WeakReferenceMessenger.Default.Send(new KeyboardSizeMessage(keyboardHeight));
		}

		private void OnKeyboardWillHide(NSNotification notification)
		{
			float keyboardHeight = 0;
			KeyboardShown.Raise(this, new KeyboardSizeMessage(keyboardHeight));
			KeyboardSizeChanged.Raise(this, new KeyboardSizeMessage(keyboardHeight));
			WeakReferenceMessenger.Default.Send(new KeyboardSizeMessage(keyboardHeight));
		}


		#endregion

	}
}
