using NeuroAccessMaui.Extensions;
using NeuroAccessMaui.UI.Pages;
using NeuroAccessMaui.Services.AttachmentCache;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using Waher.Content;
using Waher.Content.Images;
using Waher.Content.Images.Exif;
using Waher.Events;
using Waher.Networking.XMPP.Contracts;
using Waher.Runtime.Temporary;

namespace NeuroAccessMaui.Services.UI.Photos
{
	/// <summary>
	/// This is a helper class for downloading photos via http requests.
	/// It loads photos in the background, typically photo attachments connected to a
	/// digital identity. When the photos are loaded, they are added to an <see cref="ObservableCollection{T}"/> on the main thread.
	/// This class also handles errors when trying to load photos, and internally it uses a <see cref="IAttachmentCacheService"/>.
	/// </summary>
	public class PhotosLoader(ObservableCollection<Photo> Photos) : BaseViewModel
	{
		private readonly ObservableCollection<Photo> photos = Photos;
		private readonly List<string> attachmentIds = [];
		private DateTime loadPhotosTimestamp;

		/// <summary>
		/// Creates a new instance of the <see cref="PhotosLoader"/> class.
		/// Use this constructor for when you want to load a a <b>single photo</b>.
		/// </summary>
		public PhotosLoader() : this([])
		{
		}

		/// <summary>
		/// Loads photos from the specified list of attachments.
		/// </summary>
		/// <param name="Attachments">The attachments whose files to download.</param>
		/// <param name="SignWith">How the requests are signed. For identity attachments, especially for attachments to an identity being created, <see cref="SignWith.CurrentKeys"/> should be used. For requesting attachments relating to a contract, <see cref="SignWith.LatestApprovedId"/> should be used.</param>
		/// <param name="WhenDoneAction">A callback that is called when the photo load operation is done.</param>
		/// <returns>Returns the first photo, if available, null if no photos available.</returns>
		public Task<Photo?> LoadPhotos(Attachment[] Attachments, SignWith SignWith, Action? WhenDoneAction = null)
		{
			return this.LoadPhotos(Attachments, SignWith, DateTime.UtcNow, WhenDoneAction);
		}

		/// <summary>
		/// Cancels any ongoing download of photos in progress. Also
		/// clears the collection of photos from its content.
		/// </summary>
		public void CancelLoadPhotos()
		{
			try
			{
				this.loadPhotosTimestamp = DateTime.UtcNow;
				this.attachmentIds.Clear();
				this.photos.Clear();
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
			}
		}

		/// <summary>
		/// Downloads one photo and stores in cache.
		/// </summary>
		/// <param name="Attachment">The attachment to download.</param>
		/// <param name="SignWith">How the requests are signed. For identity attachments, especially for attachments to an identity being created, <see cref="SignWith.CurrentKeys"/> should be used. For requesting attachments relating to a contract, <see cref="SignWith.LatestApprovedId"/> should be used.</param>
		/// <returns>Binary content (or null), Content-Type, Rotation</returns>
		public async Task<(byte[]?, string, int)> LoadOnePhoto(Attachment Attachment, SignWith SignWith)
		{
			try
			{
				return await this.GetPhoto(Attachment, SignWith, DateTime.UtcNow);
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
			}

			return (null, string.Empty, 0);
		}

		private async Task<Photo?> LoadPhotos(Attachment[] Attachments, SignWith SignWith, DateTime Now, Action? WhenDoneAction)
		{
			if (Attachments is null || Attachments.Length <= 0)
			{
				WhenDoneAction?.Invoke();
				return null;
			}

			List<Attachment> attachmentsList = Attachments.GetImageAttachments().ToList();
			List<string> newAttachmentIds = attachmentsList.Select(x => x.Id).ToList();

			if (this.attachmentIds.HasSameContentAs(newAttachmentIds))
			{
				WhenDoneAction?.Invoke();

				foreach (Photo Photo in this.photos)
					return Photo;

				return null;
			}

			this.attachmentIds.Clear();
			this.attachmentIds.AddRange(newAttachmentIds);

			Photo? First = null;

			foreach (Attachment attachment in attachmentsList)
			{
				if (Array.IndexOf(ImageCodec.ImageContentTypes, attachment.ContentType) < 0)
					continue;

				if (this.loadPhotosTimestamp > Now)
				{
					WhenDoneAction?.Invoke();

					foreach (Photo Photo in this.photos)
						return Photo;

					return null;
				}

				try
				{
					(byte[]? Bin, string ContentType, int Rotation) = await this.GetPhoto(attachment, SignWith, Now);

					if (Bin is null)
						continue;

					Photo Photo = new(Bin, Rotation);
					First ??= Photo;

					if (Bin is not null)
					{
						TaskCompletionSource<bool> PhotoAddedTaskSource = new();

						MainThread.BeginInvokeOnMainThread(() =>
						{
							this.photos.Add(Photo);
							PhotoAddedTaskSource.TrySetResult(true);
						});

						await PhotoAddedTaskSource.Task;
					}
				}
				catch (Exception ex)
				{
					ServiceRef.LogService.LogException(ex);
				}
			}

			WhenDoneAction?.Invoke();

			return First;
		}

		private async Task<(byte[]?, string, int)> GetPhoto(Attachment Attachment, SignWith SignWith, DateTime Now)
		{
			if (Attachment is null)
				return (null, string.Empty, 0);

			(byte[]? Bin, string ContentType) = await ServiceRef.AttachmentCacheService.TryGet(Attachment.Url);

			if (Bin is not null)
				return (Bin, ContentType, GetImageRotation(Bin));

			if (!ServiceRef.NetworkService.IsOnline || !ServiceRef.XmppService.IsOnline)
				return (null, string.Empty, 0);

			KeyValuePair<string, TemporaryFile> pair = await ServiceRef.XmppService.GetAttachment(Attachment.Url, SignWith, Constants.Timeouts.DownloadFile);

			using TemporaryFile file = pair.Value;

			if (this.loadPhotosTimestamp > Now)     // If download has been cancelled any time _during_ download, stop here.
				return (null, string.Empty, 0);

			if (pair.Value.Length > int.MaxValue)   // Too large
				return (null, string.Empty, 0);

			file.Reset();

			ContentType = pair.Key;
			Bin = new byte[file.Length];

			if (file.Length != file.Read(Bin, 0, (int)file.Length))
				return (null, string.Empty, 0);

			bool IsContact = await ServiceRef.XmppService.IsContact(Attachment.LegalId);

			await ServiceRef.AttachmentCacheService.Add(Attachment.Url, Attachment.LegalId, IsContact, Bin, ContentType);

			return (Bin, ContentType, GetImageRotation(Bin));
		}

		/// <summary>
		/// Gets the rotation angle to use, to display the image correctly in Xamarin Forms.
		/// </summary>
		/// <param name="JpegImage">Binary representation of JPEG image.</param>
		/// <returns>Rotation angle (degrees).</returns>
		public static int GetImageRotation(byte[] JpegImage)
		{
			//!!! This rotation in Xamarin is limited to Android
			if (DeviceInfo.Platform == DevicePlatform.iOS)
				return 0;

			if (JpegImage is null)
				return 0;

			if (!EXIF.TryExtractFromJPeg(JpegImage, out ExifTag[] Tags))
				return 0;

			return GetImageRotation(Tags);
		}

		/// <summary>
		/// Gets the rotation angle to use, to display the image correctly in Xamarin Forms.
		/// </summary>
		/// <param name="Tags">EXIF Tags encoded in image.</param>
		/// <returns>Rotation angle (degrees).</returns>
		public static int GetImageRotation(ExifTag[] Tags)
		{
			foreach (ExifTag Tag in Tags)
			{
				if (Tag.Name == ExifTagName.Orientation)
				{
					if (Tag.Value is ushort Orientation)
					{
						return Orientation switch
						{
							1 => 0,// Top left. Default orientation.
							2 => 0,// Top right. Horizontally reversed.
							3 => 180,// Bottom right. Rotated by 180 degrees.
							4 => 180,// Bottom left. Rotated by 180 degrees and then horizontally reversed.
							5 => -90,// Left top. Rotated by 90 degrees counterclockwise and then horizontally reversed.
							6 => 90,// Right top. Rotated by 90 degrees clockwise.
							7 => 90,// Right bottom. Rotated by 90 degrees clockwise and then horizontally reversed.
							8 => -90,// Left bottom. Rotated by 90 degrees counterclockwise.
							_ => 0,
						};
					}
				}
			}

			return 0;
		}

		/// <summary>
		/// Loads a photo attachment.
		/// </summary>
		/// <param name="Attachment">Attachment containing photo.</param>
		/// <returns>Photo, Content-Type, Rotation</returns>
		public static async Task<(byte[]?, string, int)> LoadPhoto(Attachment Attachment)
		{
			PhotosLoader Loader = new();

			(byte[]?, string, int) Image = await Loader.LoadOnePhoto(Attachment, SignWith.LatestApprovedIdOrCurrentKeys);

			return Image;
		}

		/// <summary>
		/// Tries to load a photo from a set of attachments.
		/// </summary>
		/// <param name="Attachments">Attachments</param>
		/// <param name="MaxWith">Maximum width when displaying photo.</param>
		/// <param name="MaxHeight">Maximum height when displaying photo.</param>
		/// <returns>Filename, Width, Height, if loaded, (null,0,0) if not.</returns>
		public static Task<(string?, int, int)> LoadPhotoAsTemporaryFile(Attachment[] Attachments, int MaxWith, int MaxHeight)
		{
			Attachment? Photo = null;

			foreach (Attachment Attachment in Attachments.GetImageAttachments())
			{
				if (Attachment.ContentType == Constants.MimeTypes.Png)
				{
					Photo = Attachment;
					break;
				}
				else
					Photo ??= Attachment;
			}

			if (Photo is null)
				return Task.FromResult<(string?, int, int)>((null, 0, 0));
			else
				return LoadPhotoAsTemporaryFile(Photo, MaxWith, MaxHeight);
		}

		/// <summary>
		/// Tries to load a photo from an attachments.
		/// </summary>
		/// <param name="Attachment">Attachment</param>
		/// <param name="MaxWith">Maximum width when displaying photo.</param>
		/// <param name="MaxHeight">Maximum height when displaying photo.</param>
		/// <returns>Filename, Width, Height, if loaded, (null,0,0) if not.</returns>
		public static async Task<(string?, int, int)> LoadPhotoAsTemporaryFile(Attachment Attachment, int MaxWith, int MaxHeight)
		{
			(byte[]? Data, string _, int _) = await LoadPhoto(Attachment);

			if (Data is not null)
			{
				string FileName = await GetTemporaryFile(Data);
				int Width;
				int Height;

				using (SKBitmap Bitmap = SKBitmap.Decode(Data))
				{
					Width = Bitmap.Width;
					Height = Bitmap.Height;
				}

				double ScaleWidth = ((double)MaxWith) / Width;
				double ScaleHeight = ((double)MaxHeight) / Height;
				double Scale = Math.Min(ScaleWidth, ScaleHeight);

				if (Scale < 1)
				{
					Width = (int)(Width * Scale + 0.5);
					Height = (int)(Height * Scale + 0.5);
				}

				return (FileName, Width, Height);
			}
			else
				return (null, 0, 0);
		}

		#region From Waher.Content.Markdown.Model.Multimedia.ImageContent, with permission

		/// <summary>
		/// Stores an image in binary form as a temporary file. Files will be deleted when application closes.
		/// </summary>
		/// <param name="BinaryImage">Binary image.</param>
		/// <returns>Temporary file name.</returns>
		public static Task<string> GetTemporaryFile(byte[] BinaryImage)
		{
			return GetTemporaryFile(BinaryImage, "tmp");
		}

		/// <summary>
		/// Stores an image in binary form as a temporary file. Files will be deleted when application closes.
		/// </summary>
		/// <param name="BinaryImage">Binary image.</param>
		/// <param name="FileExtension">File extension.</param>
		/// <returns>Temporary file name.</returns>
		public static async Task<string> GetTemporaryFile(byte[] BinaryImage, string FileExtension)
		{
			byte[] Digest = SHA256.HashData(BinaryImage);
			string FileName = Path.Combine(Path.GetTempPath(), "tmp" + Base64Url.Encode(Digest) + "." + FileExtension);

			if (!File.Exists(FileName))
			{
				await Waher.Runtime.IO.Files.WriteAllBytesAsync(FileName, BinaryImage);

				lock (synchObject)
				{
					if (temporaryFiles is null)
					{
						temporaryFiles = [];
						Log.Terminating += CurrentDomain_ProcessExit;
					}

					temporaryFiles[FileName] = true;
				}
			}

			return FileName;
		}

		private static Dictionary<string, bool>? temporaryFiles = null;
		private static readonly object synchObject = new();

		private static Task CurrentDomain_ProcessExit(object? sender, EventArgs e)
		{
			lock (synchObject)
			{
				if (temporaryFiles is not null)
				{
					foreach (string FileName in temporaryFiles.Keys)
					{
						try
						{
							File.Delete(FileName);
						}
						catch (Exception)
						{
							// Ignore
						}
					}

					temporaryFiles.Clear();
				}
			}

			return Task.CompletedTask;
		}

		#endregion
	}
}
