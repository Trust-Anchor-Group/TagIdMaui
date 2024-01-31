﻿using EDaler;
using EDaler.Uris;
using NeuroAccessMaui.UI.Pages.Contacts.MyContacts;
using NeuroAccessMaui.UI.Pages.Contracts.MyContracts;
using NeuroAccessMaui.UI.Pages.Contracts.ViewContract;
using NeuroAccessMaui.UI.Pages.Identity.ViewIdentity;
using NeuroAccessMaui.UI.Pages.Things.MyThings;
using NeuroFeatures;
using SkiaSharp;
using System.Security.Cryptography;
using System.Text;
using System.Timers;
using System.Windows.Input;
using System.Xml;
using Waher.Content;
using Waher.Content.Html;
using Waher.Content.Markdown;
using Waher.Content.Xml;
using Waher.Networking.XMPP;
using Waher.Networking.XMPP.Contracts;
using Waher.Networking.XMPP.HttpFileUpload;
using Waher.Persistence;
using Waher.Persistence.Filters;
using System.Globalization;
using NeuroAccessMaui.Services;
using NeuroAccessMaui.Services.Notification;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using NeuroAccessMaui.Resources.Languages;
using System.ComponentModel;

namespace NeuroAccessMaui.UI.Pages.Contacts.Chat
{
	/// <summary>
	/// The view model to bind to when displaying the list of contacts.
	/// </summary>
	public class ChatViewModel : XmppViewModel, IChatView, ILinkableView
	{
		private TaskCompletionSource<bool> waitUntilBound = new();

		/// <summary>
		/// Creates an instance of the <see cref="ChatViewModel"/> class.
		/// </summary>
		protected internal ChatViewModel()
			: base()
		{
			this.ExpandButtons = new Command(_ => this.IsButtonExpanded = !this.IsButtonExpanded);
			this.SendCommand = new Command(async _ => await this.ExecuteSendMessage(), _ => this.CanExecuteSendMessage());
			this.PauseResumeCommand = new Command(async _ => await this.ExecutePauseResume(), _ => this.CanExecutePauseResume());
			this.CancelCommand = new Command(async _ => await this.ExecuteCancelMessage(), _ => this.CanExecuteCancelMessage());
			this.LoadMoreMessages = new Command(async _ => await this.ExecuteLoadMessagesAsync(), _ => this.CanExecuteLoadMoreMessages());
			this.RecordAudio = new Command(async _ => await this.ExecuteRecordAudio(), _ => this.CanExecuteRecordAudio());
			this.TakePhoto = new Command(async _ => await this.ExecuteTakePhoto(), _ => this.CanExecuteTakePhoto());
			this.EmbedFile = new Command(async _ => await this.ExecuteEmbedFile(), _ => this.CanExecuteEmbedFile());
			this.EmbedId = new Command(async _ => await this.ExecuteEmbedId(), _ => this.CanExecuteEmbedId());
			this.EmbedContract = new Command(async _ => await this.ExecuteEmbedContract(), _ => this.CanExecuteEmbedContract());
			this.EmbedMoney = new Command(async _ => await this.ExecuteEmbedMoney(), _ => this.CanExecuteEmbedMoney());
			this.EmbedToken = new Command(async _ => await this.ExecuteEmbedToken(), _ => this.CanExecuteEmbedToken());
			this.EmbedThing = new Command(async _ => await this.ExecuteEmbedThing(), _ => this.CanExecuteEmbedThing());

			this.MessageSelected = new Command(async Parameter => await this.ExecuteMessageSelected(Parameter));
		}

		/// <inheritdoc/>
		protected override async Task OnInitialize()
		{
			await base.OnInitialize();

			if (ServiceRef.NavigationService.TryGetArgs(out ChatNavigationArgs? args, this.UniqueId))
			{
				this.LegalId = args.LegalId;
				this.BareJid = args.BareJid;
				this.FriendlyName = args.FriendlyName;
			}
			else
			{
				this.LegalId = string.Empty;
				this.BareJid = string.Empty;
				this.FriendlyName = string.Empty;
			}

			await this.ExecuteLoadMessagesAsync(false);

			this.EvaluateAllCommands();
			this.waitUntilBound.TrySetResult(true);

			await ServiceRef.NotificationService.DeleteEvents(NotificationEventType.Contacts, this.BareJid);
		}

		/// <inheritdoc/>
		protected override async Task OnDispose()
		{
			await base.OnDispose();

			this.waitUntilBound = new TaskCompletionSource<bool>();
		}

		private void EvaluateAllCommands()
		{
			this.EvaluateCommands(this.SendCommand, this.CancelCommand, this.LoadMoreMessages, this.PauseResumeCommand, this.RecordAudio, this.TakePhoto, this.EmbedFile,
				this.EmbedId, this.EmbedContract, this.EmbedMoney, this.EmbedToken, this.EmbedThing);
		}

		/// <inheritdoc/>
		protected override Task XmppService_ConnectionStateChanged(object? Sender, XmppState NewState)
		{
			base.XmppService_ConnectionStateChanged(Sender, NewState);
			MainThread.BeginInvokeOnMainThread(() => this.EvaluateAllCommands());

			return Task.CompletedTask;
		}

		/// <summary>
		/// Set the views unique ID
		/// </summary>
		[ObservableProperty]
		private string? uniqueId;

		/// <summary>
		/// Bare JID of remote party
		/// </summary>
		[ObservableProperty]
		private string? bareJid;

		/// <summary>
		/// Bare JID of remote party
		/// </summary>
		[ObservableProperty]
		private string? legalId;

		/// <summary>
		/// Friendly name of remote party
		/// </summary>
		[ObservableProperty]
		private string? friendlyName;

		/// <summary>
		/// Current Markdown input.
		/// </summary>
		[ObservableProperty]
		private string MarkdownInput;

		protected override void OnPropertyChanged(PropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);

			switch (e.PropertyName)
			{
				case nameof(this.MarkdownInput):
					this.IsWriting = !string.IsNullOrEmpty(this.MarkdownInput);
					this.EvaluateAllCommands();

					if (!string.IsNullOrEmpty(this.MarkdownInput))
						MessagingCenter.Send<object>(this, Constants.MessagingCenter.ChatEditorFocus);
					break;

				case nameof(this.MessageId):
					this.IsWriting = !string.IsNullOrEmpty(value);
					this.EvaluateAllCommands();
					break;
			}
		}

		/// <summary>
		/// Current Markdown input.
		/// </summary>
		[ObservableProperty]
		private string messageId;

		/// <summary>
		/// Current Markdown input.
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(LoadMoreMessagesCommand))]
		private bool existsMoreMessages;

		/// <summary>
		/// <see cref="IsWriting"/>
		/// </summary>
		public static readonly BindableProperty IsWritingProperty =
			BindableProperty.Create(nameof(IsWriting), typeof(bool), typeof(ChatViewModel), default(bool));

		/// <summary>
		/// If the user is writing markdown.
		/// </summary>
		public bool IsWriting
		{
			get => (bool)this.GetValue(IsWritingProperty);
			set
			{
				this.SetValue(IsWritingProperty, value);
				this.IsButtonExpanded = false;
			}
		}

		/// <summary>
		/// <see cref="IsRecordingAudio"/>
		/// </summary>
		public static readonly BindableProperty IsRecordingAudioProperty =
			BindableProperty.Create(nameof(IsRecordingAudio), typeof(bool), typeof(ChatViewModel), default(bool));

		/// <summary>
		/// If the user is recording an audio message
		/// </summary>
		public bool IsRecordingAudio
		{
			get => (bool)this.GetValue(IsRecordingAudioProperty);
			set
			{
				this.SetValue(IsRecordingAudioProperty, value);
				this.IsRecordingPaused = audioRecorder.Value.IsPaused;
				this.IsWriting = value;

				if (audioRecorderTimer is null)
				{
					audioRecorderTimer = new Timer(100);
					audioRecorderTimer.Elapsed += this.OnAudioRecorderTimer;
					audioRecorderTimer.AutoReset = true;
				}

				audioRecorderTimer.Enabled = value;

				this.OnPropertyChanged(nameof(this.RecordingTime));
				this.EvaluateAllCommands();
			}
		}

		/// <summary>
		/// If the audio recording is paused
		/// </summary>
		[ObservableProperty]
		private bool isRecordingPaused;

		/// <summary>
		/// If the audio recording is paused
		/// </summary>
		public static string RecordingTime
		{
			get
			{
				double Milliseconds = audioRecorder.Value.TotalAudioTimeout.TotalMilliseconds - audioRecorder.Value.RecordingTime.TotalMilliseconds;
				return (Milliseconds > 0) ? string.Format(CultureInfo.CurrentCulture, "{0:F0}s left", Math.Ceiling(Milliseconds / 1000.0)) : "TIMEOUT";
			}
		}

		/// <summary>
		/// Holds the list of chat messages to display.
		/// </summary>
		public ObservableCollection<ChatMessage> Messages { get; } = [];

		/// <summary>
		/// External message has been received
		/// </summary>
		/// <param name="Message">Message</param>
		public async Task MessageAddedAsync(ChatMessage Message)
		{
			try
			{
				await Message.GenerateXaml(this);
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				return;
			}

			MainThread.BeginInvokeOnMainThread(() =>
			{
				try
				{
					int i = 0;

					for (; i < this.Messages.Count; i++)
					{
						ChatMessage Item = this.Messages[i];

						if (Item.Created <= Message.Created)
							break;
					}

					if (i >= this.Messages.Count)
						this.Messages.Add(Message);
					else if (this.Messages[i].ObjectId != Message.ObjectId)
						this.Messages.Insert(i, Message);

					this.EnsureFirstMessageIsEmpty();
				}
				catch (Exception ex)
				{
					ServiceRef.LogService.LogException(ex);
				}
			});
		}

		/// <summary>
		/// External message has been updated
		/// </summary>
		/// <param name="Message">Message</param>
		public async Task MessageUpdatedAsync(ChatMessage Message)
		{
			try
			{
				await Message.GenerateXaml(this);
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				return;
			}

			MainThread.BeginInvokeOnMainThread(() =>
			{
				try
				{
					for (int i = 0; i < this.Messages.Count; i++)
					{
						ChatMessage Item = this.Messages[i];

						if (Item.ObjectId == Message.ObjectId)
						{
							this.Messages[i] = Message;
							break;
						}

						this.EnsureFirstMessageIsEmpty();
					}
				}
				catch (Exception ex)
				{
					ServiceRef.LogService.LogException(ex);
				}
			});
		}

		private async Task ExecuteLoadMessagesAsync(bool LoadMore = true)
		{
			IEnumerable<ChatMessage>? Messages = null;
			int c = Constants.BatchSizes.MessageBatchSize;

			try
			{
				this.ExistsMoreMessages = false;

				DateTime LastTime = LoadMore ? this.Messages[^1].Created : DateTime.MaxValue;

				Messages = await Database.Find<ChatMessage>(0, Constants.BatchSizes.MessageBatchSize,
					new FilterAnd(
						new FilterFieldEqualTo("RemoteBareJid", this.BareJid),
						new FilterFieldLesserThan("Created", LastTime)), "-Created");

				foreach (ChatMessage Message in Messages)
				{
					await Message.GenerateXaml(this);
					c--;
				}
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				this.ExistsMoreMessages = false;
				return;
			}

			MainThread.BeginInvokeOnMainThread(() =>
			{
				try
				{
					this.MergeObservableCollections(LoadMore, Messages.ToList());
					this.ExistsMoreMessages = c <= 0;
					this.EnsureFirstMessageIsEmpty();
				}
				catch (Exception ex)
				{
					ServiceRef.LogService.LogException(ex);
					this.ExistsMoreMessages = false;
				}
			});
		}

		private void MergeObservableCollections(bool LoadMore, List<ChatMessage> NewMessages)
		{
			if (LoadMore || (this.Messages.Count == 0))
			{
				this.Messages.AddRange(NewMessages);
				return;
			}

			List<ChatMessage> RemoveItems = this.Messages.Where(oel => NewMessages.All(nel => nel.UniqueName != oel.UniqueName)).ToList();
			this.Messages.RemoveRange(RemoveItems);

			for (int i = 0; i < NewMessages.Count; i++)
			{
				ChatMessage Item = NewMessages[i];

				if (i >= this.Messages.Count)
					this.Messages.Add(Item);
				else if (this.Messages[i].UniqueName != Item.UniqueName)
					this.Messages.Insert(i, Item);
			}
		}

		private void EnsureFirstMessageIsEmpty()
		{

/* Unmerged change from project 'NeuroAccessMaui (net8.0-ios)'
Before:
			if (this.Messages.Count > 0 && this.Messages[0].MessageType != Services.Messages.MessageType.Empty)
After:
			if (this.Messages.Count > 0 && this.Messages[0].MessageType != MessageType.Empty)
*/
			if (this.Messages.Count > 0 && this.Messages[0].MessageType != Chat.MessageType.Empty)
			{
				this.Messages.Insert(0, ChatMessage.Empty);
			}
		}

		/// <summary>
		/// <see cref="IsButtonExpanded"/>
		/// </summary>
		public static readonly BindableProperty IsButtonExpandedProperty =
			BindableProperty.Create(nameof(IsButtonExpanded), typeof(bool), typeof(ChatViewModel), default(bool));

		/// <summary>
		/// If the button is expanded
		/// </summary>
		public bool IsButtonExpanded
		{
			get => (bool)this.GetValue(IsButtonExpandedProperty);
			set => this.SetValue(IsButtonExpandedProperty, value);
		}

		/// <summary>
		/// Command to expand the buttons
		/// </summary>
		public ICommand ExpandButtons { get; }

		/// <summary>
		/// The command to bind to for sending user input
		/// </summary>
		public ICommand SendCommand { get; }

		private bool CanExecuteSendMessage()
		{
			return this.IsConnected && (!string.IsNullOrEmpty(this.MarkdownInput) || this.IsRecordingAudio);
		}

		private async Task ExecuteSendMessage()
		{
			if (this.IsRecordingAudio)
			{
				try
				{
					await audioRecorder.Value.StopRecording();
					string audioPath = await this.audioRecorderTask;

					if (audioPath is not null)
					{
						await this.EmbedMedia(audioPath, true);
					}
				}
				catch (Exception ex)
				{
					ServiceRef.LogService.LogException(ex);
				}
				finally
				{
					this.IsRecordingAudio = false;
				}
			}
			else
			{
				await this.ExecuteSendMessage(this.MessageId, this.MarkdownInput);
				await this.ExecuteCancelMessage();
			}
		}

		private Task ExecuteSendMessage(string ReplaceObjectId, string MarkdownInput)
		{
			return ExecuteSendMessage(ReplaceObjectId, MarkdownInput, this.BareJid, this);
		}

		/// <summary>
		/// Sends a Markdown-formatted chat message
		/// </summary>
		/// <param name="ReplaceObjectId">ID of message being updated, or the empty string.</param>
		/// <param name="MarkdownInput">Markdown input.</param>
		/// <param name="BareJid">Bare JID of recipient.</param>
		/// <param name="ServiceReferences">Service references.</param>
		public static async Task ExecuteSendMessage(string ReplaceObjectId, string MarkdownInput, string BareJid, IServiceReferences ServiceReferences)
		{
			try
			{
				if (string.IsNullOrEmpty(MarkdownInput))
					return;

				MarkdownSettings Settings = new()
				{
					AllowScriptTag = false,
					EmbedEmojis = false,    // TODO: Emojis
					AudioAutoplay = false,
					AudioControls = false,
					ParseMetaData = false,
					VideoAutoplay = false,
					VideoControls = false
				};

				MarkdownDocument Doc = await MarkdownDocument.CreateAsync(MarkdownInput, Settings);

				ChatMessage Message = new()
				{
					Created = DateTime.UtcNow,
					RemoteBareJid = BareJid,
					RemoteObjectId = string.Empty,

/* Unmerged change from project 'NeuroAccessMaui (net8.0-ios)'
Before:
					MessageType = Services.Messages.MessageType.Sent,
After:
					MessageType = MessageType.Sent,
*/
					MessageType = Chat.MessageType.Sent,
					Html = HtmlDocument.GetBody(await Doc.GenerateHTML()),
					PlainText = (await Doc.GeneratePlainText()).Trim(),
					Markdown = MarkdownInput
				};

				StringBuilder Xml = new();

				Xml.Append("<content xmlns=\"urn:xmpp:content\" type=\"text/markdown\">");
				Xml.Append(XML.Encode(MarkdownInput));
				Xml.Append("</content><html xmlns='http://jabber.org/protocol/xhtml-im'><body xmlns='http://www.w3.org/1999/xhtml'>");

				HtmlDocument HtmlDoc = new("<root>" + Message.Html + "</root>");

				foreach (HtmlNode N in (HtmlDoc.Body ?? HtmlDoc.Root).Children)
					N.Export(Xml);

				Xml.Append("</body></html>");

				if (!string.IsNullOrEmpty(ReplaceObjectId))
				{
					Xml.Append("<replace id='");
					Xml.Append(ReplaceObjectId);
					Xml.Append("' xmlns='urn:xmpp:message-correct:0'/>");
				}

				if (string.IsNullOrEmpty(ReplaceObjectId))
				{
					await Database.Insert(Message);

					if (ServiceReferences is ChatViewModel ChatViewModel)
						await ChatViewModel.MessageAddedAsync(Message);
				}
				else
				{
					ChatMessage Old = await Database.TryLoadObject<ChatMessage>(ReplaceObjectId);

					if (Old is null)
					{
						ReplaceObjectId = null;
						await Database.Insert(Message);

						if (ServiceReferences is ChatViewModel ChatViewModel)
							await ChatViewModel.MessageAddedAsync(Message);
					}
					else
					{
						Old.Updated = Message.Created;
						Old.Html = Message.Html;
						Old.PlainText = Message.PlainText;
						Old.Markdown = Message.Markdown;

						await Database.Update(Old);

						Message = Old;

						if (ServiceReferences is ChatViewModel ChatViewModel)
							await ChatViewModel.MessageUpdatedAsync(Message);
					}
				}

				ServiceReferences.XmppService.SendMessage(QoSLevel.Unacknowledged, Waher.Networking.XMPP.MessageType.Chat, Message.ObjectId,
					BareJid, Xml.ToString(), Message.PlainText, string.Empty, string.Empty, string.Empty, string.Empty, null, null);
			}
			catch (Exception ex)
			{
				await ServiceReferences.UiSerializer.DisplayAlert(ex);
			}
		}

		/// <summary>
		/// The command to bind for pausing/resuming the audio recording
		/// </summary>
		public ICommand PauseResumeCommand { get; }

		private bool CanExecutePauseResume()
		{
			return this.IsRecordingAudio && audioRecorder.Value.IsRecording;
		}

		/// <summary>
		/// The command to bind to for sending user input
		/// </summary>
		public ICommand CancelCommand { get; }

		private bool CanExecuteCancelMessage()
		{
			return this.IsConnected && (!string.IsNullOrEmpty(this.MarkdownInput) || this.IsRecordingAudio);
		}

		private Task ExecuteCancelMessage()
		{
			if (this.IsRecordingAudio)
			{
				try
				{
					return audioRecorder.Value.StopRecording();
				}
				catch (Exception ex)
				{
					ServiceRef.LogService.LogException(ex);
				}
				finally
				{
					this.IsRecordingAudio = false;
				}
			}
			else
			{
				this.MarkdownInput = string.Empty;
				this.MessageId = string.Empty;
			}

			return Task.CompletedTask;
		}

		/// <summary>
		/// The command to bind to for loading more messages.
		/// </summary>
		public ICommand LoadMoreMessages { get; }

		private bool CanExecuteLoadMoreMessages()
		{
			return this.ExistsMoreMessages && this.Messages.Count > 0;
		}

		private static Timer audioRecorderTimer;

		private static readonly Lazy<AudioRecorderService> audioRecorder = new(() => {
			return new AudioRecorderService()
			{
				StopRecordingOnSilence = false,
				StopRecordingAfterTimeout = true,
				TotalAudioTimeout = TimeSpan.FromSeconds(60)
			};
		}, System.Threading.LazyThreadSafetyMode.PublicationOnly);

		private Task<string> audioRecorderTask = null;

		/// <summary>
		/// Command to take and send a audio record
		/// </summary>
		public ICommand RecordAudio { get; }

		private bool CanExecuteRecordAudio()
		{
			return this.IsConnected && !this.IsWriting && ServiceRef.XmppService.FileUploadIsSupported;
		}

		/// <summary>
		/// Command to take and send a photo
		/// </summary>
		public ICommand TakePhoto { get; }


		private bool CanExecuteTakePhoto()
		{
			return this.IsConnected && !this.IsWriting && ServiceRef.XmppService.FileUploadIsSupported;
		}

		private async Task ExecutePauseResume()
		{
			if (audioRecorder.Value.IsPaused)
			{
				await audioRecorder.Value.Resume();
			}
			else
			{
				await audioRecorder.Value.Pause();
			}

			this.IsRecordingPaused = audioRecorder.Value.IsPaused;
		}

		private void OnAudioRecorderTimer(Object source, System.Timers.ElapsedEventArgs e)
		{
			this.OnPropertyChanged(nameof(this.RecordingTime));
			this.IsRecordingPaused = audioRecorder.Value.IsPaused;
		}

		private async Task ExecuteRecordAudio()
		{
			if (!ServiceRef.XmppService.FileUploadIsSupported)
			{
				await ServiceRef.UiSerializer.DisplayAlert(ServiceRef.Localizer[nameof(AppResources.TakePhoto)],
					ServiceRef.Localizer[nameof(AppResources.ServerDoesNotSupportFileUpload)]);
				return;
			}

			try
			{
				PermissionStatus Status = await Permissions.RequestAsync<Permissions.Microphone>();

				if (Status == PermissionStatus.Granted)
				{
					this.audioRecorderTask = await audioRecorder.Value.StartRecording();
					this.IsRecordingAudio = true;
				}
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
			}
		}

		private async Task ExecuteTakePhoto()
		{
			if (!ServiceRef.XmppService.FileUploadIsSupported)
			{
				await ServiceRef.UiSerializer.DisplayAlert(ServiceRef.Localizer[nameof(AppResources.TakePhoto)],
					ServiceRef.Localizer[nameof(AppResources.ServerDoesNotSupportFileUpload)]);
				return;
			}

			if (Device.RuntimePlatform == Device.iOS)
			{
				MediaFile capturedPhoto;

				try
				{
					capturedPhoto = await CrossMedia.Current.TakePhotoAsync(new StoreCameraMediaOptions()
					{
						CompressionQuality = 80,
						RotateImage = false
					});
				}
				catch (Exception ex)
				{
					await ServiceRef.UiSerializer.DisplayAlert(ServiceRef.Localizer[nameof(AppResources.TakePhoto)],
						ServiceRef.Localizer[nameof(AppResources.TakingAPhotoIsNotSupported)] + ": " + ex.Message);
					return;
				}

				if (capturedPhoto is not null)
				{
					try
					{
						await this.EmbedMedia(capturedPhoto.Path, true);
					}
					catch (Exception ex)
					{
						await ServiceRef.UiSerializer.DisplayAlert(ex);
					}
				}
			}
			else
			{
				FileResult capturedPhoto;

				try
				{
					capturedPhoto = await MediaPicker.CapturePhotoAsync();
					if (capturedPhoto is null)
						return;
				}
				catch (Exception ex)
				{
					await ServiceRef.UiSerializer.DisplayAlert(ServiceRef.Localizer[nameof(AppResources.TakePhoto)],
						ServiceRef.Localizer[nameof(AppResources.TakingAPhotoIsNotSupported)] + ": " + ex.Message);
					return;
				}

				if (capturedPhoto is not null)
				{
					try
					{
						await this.EmbedMedia(capturedPhoto.FullPath, true);
					}
					catch (Exception ex)
					{
						await ServiceRef.UiSerializer.DisplayAlert(ex);
					}
				}
			}
		}

		private async Task EmbedMedia(string FilePath, bool DeleteFile)
		{
			try
			{
				byte[] Bin = File.ReadAllBytes(FilePath);
				if (!InternetContent.TryGetContentType(Path.GetExtension(FilePath), out string ContentType))
					ContentType = "application/octet-stream";

				if (Bin.Length > this.TagProfile.HttpFileUploadMaxSize)
				{
					await ServiceRef.UiSerializer.DisplayAlert(ServiceRef.Localizer[nameof(AppResources.ErrorTitle)],
						ServiceRef.Localizer[nameof(AppResources.PhotoIsTooLarge)]);
					return;
				}

				// Taking or picking photos switches to another app, so ID app has to reconnect again after.
				if (!await ServiceRef.XmppService.WaitForConnectedState(Constants.Timeouts.XmppConnect))
				{
					await ServiceRef.UiSerializer.DisplayAlert(ServiceRef.Localizer[nameof(AppResources.ErrorTitle)],
						string.Format(ServiceRef.Localizer[nameof(AppResources.UnableToConnectTo)], this.TagProfile.Domain));
					return;
				}

				string FileName = Path.GetFileName(FilePath);

				// Encrypting image

				using RandomNumberGenerator Rnd = RandomNumberGenerator.Create();
				byte[] Key = new byte[16];
				byte[] IV = new byte[16];

				Rnd.GetBytes(Key);
				Rnd.GetBytes(IV);

				Aes Aes = Aes.Create();
				Aes.BlockSize = 128;
				Aes.KeySize = 256;
				Aes.Mode = CipherMode.CBC;
				Aes.Padding = PaddingMode.PKCS7;

				using ICryptoTransform Transform = Aes.CreateEncryptor(Key, IV);
				Bin = Transform.TransformFinalBlock(Bin, 0, Bin.Length);

				// Preparing File upload service that content uploaded next is encrypted, and can be stored in encrypted storage.

				StringBuilder Xml = new();

				Xml.Append("<prepare xmlns='http://waher.se/Schema/EncryptedStorage.xsd' filename='");
				Xml.Append(XML.Encode(FileName));
				Xml.Append("' size='");
				Xml.Append(Bin.Length.ToString(CultureInfo.InvariantCulture));
				Xml.Append("' content-type='application/octet-stream'/>");

				await ServiceRef.XmppService.IqSetAsync(this.TagProfile.HttpFileUploadJid, Xml.ToString());
				// Empty response expected. Errors cause an exception to be raised.

				// Requesting upload slot

				HttpFileUploadEventArgs Slot = await ServiceRef.XmppService.RequestUploadSlotAsync(
					FileName, "application/octet-stream", Bin.Length);

				if (!Slot.Ok)
					throw Slot.StanzaError ?? new Exception(Slot.ErrorText);

				// Uploading encrypted image

				await Slot.PUT(Bin, "application/octet-stream", (int)Constants.Timeouts.UploadFile.TotalMilliseconds);

				// Generating Markdown message to send to recipient

				StringBuilder Markdown = new();

				Markdown.Append("![");
				Markdown.Append(MarkdownDocument.Encode(FileName));
				Markdown.Append("](");
				Markdown.Append(Constants.UriSchemes.Aes256);
				Markdown.Append(':');
				Markdown.Append(Convert.ToBase64String(Key));
				Markdown.Append(':');
				Markdown.Append(Convert.ToBase64String(IV));
				Markdown.Append(':');
				Markdown.Append(ContentType);
				Markdown.Append(':');
				Markdown.Append(Slot.GetUrl);

				SKImageInfo ImageInfo = SKBitmap.DecodeBounds(Bin);
				if (!ImageInfo.IsEmpty)
				{
					Markdown.Append(' ');
					Markdown.Append(ImageInfo.Width.ToString(CultureInfo.InvariantCulture));
					Markdown.Append(' ');
					Markdown.Append(ImageInfo.Height.ToString(CultureInfo.InvariantCulture));
				}

				Markdown.Append(')');

				await this.ExecuteSendMessage(string.Empty, Markdown.ToString());

				// TODO: End-to-End encryption, or using Elliptic Curves of recipient together with sender to deduce shared secret.

				if (DeleteFile)
					File.Delete(FilePath);
			}
			catch (Exception ex)
			{
				await ServiceRef.UiSerializer.DisplayAlert(ServiceRef.Localizer[nameof(AppResources.ErrorTitle)], ex.Message);
				ServiceRef.LogService.LogException(ex);
				return;
			}
		}

		/// <summary>
		/// Command to embed a file
		/// </summary>
		public ICommand EmbedFile { get; }

		private bool CanExecuteEmbedFile()
		{
			return this.IsConnected && !this.IsWriting && ServiceRef.XmppService.FileUploadIsSupported;
		}

		private async Task ExecuteEmbedFile()
		{
			if (!ServiceRef.XmppService.FileUploadIsSupported)
			{
				await ServiceRef.UiSerializer.DisplayAlert(ServiceRef.Localizer[nameof(AppResources.PickPhoto)], ServiceRef.Localizer[nameof(AppResources.SelectingAPhotoIsNotSupported)]);
				return;
			}

			FileResult pickedPhoto = await MediaPicker.PickPhotoAsync();

			if (pickedPhoto is not null)
				await this.EmbedMedia(pickedPhoto.FullPath, false);
		}

		/// <summary>
		/// Command to embed a reference to a legal ID
		/// </summary>
		public ICommand EmbedId { get; }

		private bool CanExecuteEmbedId()
		{
			return this.IsConnected && !this.IsWriting;
		}

		private async Task ExecuteEmbedId()
		{
			TaskCompletionSource<ContactInfoModel> SelectedContact = new();
			ContactListNavigationArgs Args = new(ServiceRef.Localizer[nameof(AppResources.SelectContactToPay)], SelectedContact)
			{
				CanScanQrCode = true
			};

			await ServiceRef.NavigationService.GoToAsync(nameof(MyContactsPage), Args, BackMethod.Pop);

			ContactInfoModel Contact = await SelectedContact.Task;
			if (Contact is null)
				return;

			await this.waitUntilBound.Task;     // Wait until view is bound again.

			if (Contact.LegalIdentity is not null)
			{
				StringBuilder Markdown = new();

				Markdown.Append("```");
				Markdown.AppendLine(Constants.UriSchemes.IotId);

				Contact.LegalIdentity.Serialize(Markdown, true, true, true, true, true, true, true);

				Markdown.AppendLine();
				Markdown.AppendLine("```");

				await this.ExecuteSendMessage(string.Empty, Markdown.ToString());
				return;
			}

			if (!string.IsNullOrEmpty(Contact.LegalId))
			{
				await this.ExecuteSendMessage(string.Empty, "![" + MarkdownDocument.Encode(Contact.FriendlyName) + "](" + ContractsClient.LegalIdUriString(Contact.LegalId) + ")");
				return;
			}

			if (!string.IsNullOrEmpty(Contact.BareJid))
			{
				await this.ExecuteSendMessage(string.Empty, "![" + MarkdownDocument.Encode(Contact.FriendlyName) + "](xmpp:" + Contact.BareJid + "?subscribe)");
				return;
			}
		}

		/// <summary>
		/// Command to embed a reference to a smart contract
		/// </summary>
		public ICommand EmbedContract { get; }

		private bool CanExecuteEmbedContract()
		{
			return this.IsConnected && !this.IsWriting;
		}

		private async Task ExecuteEmbedContract()
		{
			TaskCompletionSource<Contract> SelectedContract = new();
			MyContractsNavigationArgs Args = new(ContractsListMode.Contracts, SelectedContract);

			await ServiceRef.NavigationService.GoToAsync(nameof(MyContractsPage), Args, BackMethod.Pop);

			Contract Contract = await SelectedContract.Task;
			if (Contract is null)
				return;

			await this.waitUntilBound.Task;     // Wait until view is bound again.

			StringBuilder Markdown = new();

			Markdown.Append("```");
			Markdown.AppendLine(Constants.UriSchemes.IotSc);

			Contract.Serialize(Markdown, true, true, true, true, true, true, true);

			Markdown.AppendLine();
			Markdown.AppendLine("```");

			await this.ExecuteSendMessage(string.Empty, Markdown.ToString());
		}

		/// <summary>
		/// Command to embed a payment
		/// </summary>
		public ICommand EmbedMoney { get; }

		private bool CanExecuteEmbedMoney()
		{
			return this.IsConnected && !this.IsWriting;
		}

		private async Task ExecuteEmbedMoney()
		{
			StringBuilder sb = new();

			sb.Append("edaler:");

			if (!string.IsNullOrEmpty(this.LegalId))
			{
				sb.Append("ti=");
				sb.Append(this.LegalId);
			}
			else if (!string.IsNullOrEmpty(this.BareJid))
			{
				sb.Append("t=");
				sb.Append(this.BareJid);
			}
			else
				return;

			Balance CurrentBalance = await ServiceRef.XmppService.GetEDalerBalance();

			sb.Append(";cu=");
			sb.Append(CurrentBalance.Currency);

			if (!EDalerUri.TryParse(sb.ToString(), out EDalerUri Parsed))
				return;

			TaskCompletionSource<string> UriToSend = new();
			EDalerUriNavigationArgs Args = new(Parsed, this.FriendlyName, UriToSend);

			await ServiceRef.NavigationService.GoToAsync(nameof(SendPaymentPage), Args, BackMethod.Pop);

			string Uri = await UriToSend.Task;
			if (string.IsNullOrEmpty(Uri) || !EDalerUri.TryParse(Uri, out Parsed))
				return;

			await this.waitUntilBound.Task;     // Wait until view is bound again.

			sb.Clear();
			sb.Append(MoneyToString.ToString(Parsed.Amount));

			if (Parsed.AmountExtra.HasValue)
			{
				sb.Append(" (+");
				sb.Append(MoneyToString.ToString(Parsed.AmountExtra.Value));
				sb.Append(")");
			}

			sb.Append(" ");
			sb.Append(Parsed.Currency);

			await this.ExecuteSendMessage(string.Empty, "![" + sb.ToString() + "](" + Uri + ")");
		}

		/// <summary>
		/// Command to embed a token reference
		/// </summary>
		public ICommand EmbedToken { get; }

		private bool CanExecuteEmbedToken()
		{
			return this.IsConnected && !this.IsWriting;
		}

		private async Task ExecuteEmbedToken()
		{
			MyTokensNavigationArgs Args = new();

			await ServiceRef.NavigationService.GoToAsync(nameof(MyTokensPage), Args, BackMethod.Pop);

			TokenItem Selected = await Args.TokenItemProvider.Task;

			if (Selected is null)
				return;

			StringBuilder Markdown = new();

			Markdown.AppendLine("```nfeat");

			Selected.Token.Serialize(Markdown);

			Markdown.AppendLine();
			Markdown.AppendLine("```");

			await this.ExecuteSendMessage(string.Empty, Markdown.ToString());
			return;

		}

		/// <summary>
		/// Command to embed a reference to a thing
		/// </summary>
		public ICommand EmbedThing { get; }

		private bool CanExecuteEmbedThing()
		{
			return this.IsConnected && !this.IsWriting;
		}

		private async Task ExecuteEmbedThing()
		{
			TaskCompletionSource<ContactInfoModel> ThingToShare = new();
			MyThingsNavigationArgs Args = new(ThingToShare);

			await ServiceRef.NavigationService.GoToAsync(nameof(MyThingsPage), Args, BackMethod.Pop);

			ContactInfoModel Thing = await ThingToShare.Task;
			if (Thing is null)
				return;

			await this.waitUntilBound.Task;     // Wait until view is bound again.

			StringBuilder sb = new();

			sb.Append("![");
			sb.Append(MarkdownDocument.Encode(Thing.FriendlyName));
			sb.Append("](iotdisco:JID=");
			sb.Append(Thing.BareJid);

			if (!string.IsNullOrEmpty(Thing.SourceId))
			{
				sb.Append(";SID=");
				sb.Append(Thing.SourceId);
			}

			if (!string.IsNullOrEmpty(Thing.Partition))
			{
				sb.Append(";PT=");
				sb.Append(Thing.Partition);
			}

			if (!string.IsNullOrEmpty(Thing.NodeId))
			{
				sb.Append(";NID=");
				sb.Append(Thing.NodeId);
			}

			sb.Append(")");

			await this.ExecuteSendMessage(string.Empty, sb.ToString());
		}

		/// <summary>
		/// Command executed when a message has been selected (or deselected) in the list view.
		/// </summary>
		public ICommand MessageSelected { get; }

		private Task ExecuteMessageSelected(object Parameter)
		{
			if (Parameter is ChatMessage Message)
			{
				if (Message.ParsedXaml is View View)
				{
					AudioPlayerControl AudioPlayer = View.Descendants().OfType<AudioPlayerControl>().FirstOrDefault();
					if (AudioPlayer is not null)
					{
						return Task.CompletedTask;
					}
				}

				switch (Message.MessageType)
				{

/* Unmerged change from project 'NeuroAccessMaui (net8.0-ios)'
Before:
					case Services.Messages.MessageType.Sent:
After:
					case MessageType.Sent:
*/
					case Chat.MessageType.Sent:
						this.MessageId = Message.ObjectId;
						this.MarkdownInput = Message.Markdown;
						break;


/* Unmerged change from project 'NeuroAccessMaui (net8.0-ios)'
Before:
					case Services.Messages.MessageType.Received:
After:
					case MessageType.Received:
*/
					case Chat.MessageType.Received:
						string s = Message.Markdown;
						if (string.IsNullOrEmpty(s))
							s = MarkdownDocument.Encode(Message.PlainText);

						string[] Rows = s.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

						StringBuilder Quote = new();

						foreach (string Row in Rows)
						{
							Quote.Append("> ");
							Quote.AppendLine(Row);
						}

						Quote.AppendLine();

						this.MessageId = string.Empty;
						this.MarkdownInput = Quote.ToString();
						break;
				}
			}

			this.EvaluateAllCommands();

			return Task.CompletedTask;
		}

		/// <summary>
		/// Called when a Multi-media URI link using the XMPP URI scheme.
		/// </summary>
		/// <param name="Message">Message containing the URI.</param>
		/// <param name="Uri">URI</param>
		/// <param name="Scheme">URI Scheme</param>
		public async Task ExecuteUriClicked(ChatMessage Message, string Uri, UriScheme Scheme)
		{
			try
			{
				if (Scheme == UriScheme.Xmpp)
					await ProcessXmppUri(Uri, ServiceRef.XmppService, this.TagProfile);
				else
				{
					int i = Uri.IndexOf(':');
					if (i < 0)
						return;

					string s = Uri[(i + 1)..].Trim();
					if (s.StartsWith('<') && s.EndsWith('>'))  // XML
					{
						XmlDocument Doc = new()
						{
							PreserveWhitespace = true
						};
						Doc.LoadXml(s);

						switch (Scheme)
						{
							case UriScheme.IotId:
								LegalIdentity Id = LegalIdentity.Parse(Doc.DocumentElement);
								ViewIdentityNavigationArgs ViewIdentityArgs = new(Id);

								await ServiceRef.NavigationService.GoToAsync(nameof(ViewIdentityPage), ViewIdentityArgs, BackMethod.Pop);
								break;

							case UriScheme.IotSc:
								ParsedContract ParsedContract = await Contract.Parse(Doc.DocumentElement, ServiceRef.XmppService.ContractsClient);
								ViewContractNavigationArgs ViewContractArgs = new(ParsedContract.Contract, false);

								await ServiceRef.NavigationService.GoToAsync(nameof(ViewContractPage), ViewContractArgs, BackMethod.Pop);
								break;

							case UriScheme.NeuroFeature:
								if (!Token.TryParse(Doc.DocumentElement, out Token ParsedToken))
									throw new Exception(ServiceRef.Localizer[nameof(AppResources.InvalidNeuroFeatureToken)]);

								if (!ServiceRef.NotificationService.TryGetNotificationEvents(NotificationEventType.Wallet, ParsedToken.TokenId, out NotificationEvent[] Events))
									Events = [];

								TokenDetailsNavigationArgs Args = new(new TokenItem(ParsedToken, this, Events));

								await ServiceRef.NavigationService.GoToAsync(nameof(TokenDetailsPage), Args, BackMethod.Pop);
								break;

							default:
								return;
						}
					}
					else
						await QrCode.OpenUrl(Uri);
				}
			}
			catch (Exception ex)
			{
				await ServiceRef.UiSerializer.DisplayAlert(ex);
			}
		}

		/// <summary>
		/// Processes an XMPP URI
		/// </summary>
		/// <param name="Uri">XMPP URI</param>
		/// <returns>If URI could be processed.</returns>
		public static async Task<bool> ProcessXmppUri(string Uri)
		{
			int i = Uri.IndexOf(':');
			if (i < 0)
				return false;

			string Jid = Uri[(i + 1)..].TrimStart();
			string Command;

			i = Jid.IndexOf('?');
			if (i < 0)
				Command = "subscribe";
			else
			{
				Command = Jid[(i + 1)..].TrimStart();
				Jid = Jid[..i].TrimEnd();
			}

			Jid = System.Web.HttpUtility.UrlDecode(Jid);
			Jid = XmppClient.GetBareJID(Jid);

			switch (Command.ToLower(CultureInfo.InvariantCulture))
			{
				case "subscribe":
					SubscribeToPopupPage SubscribeToPopupPage = new(Jid);

					await Rg.Plugins.Popup.Services.PopupNavigation.Instance.PushAsync(SubscribeToPopupPage);
					bool? SubscribeTo = await SubscribeToPopupPage.Result;

					if (SubscribeTo.HasValue && SubscribeTo.Value)
					{
						string IdXml;

						if (ServiceRef.TagProfile.LegalIdentity is null)
							IdXml = string.Empty;
						else
						{
							StringBuilder Xml = new();
							ServiceRef.TagProfile.LegalIdentity.Serialize(Xml, true, true, true, true, true, true, true);
							IdXml = Xml.ToString();
						}

						ServiceRef.XmppService.RequestPresenceSubscription(Jid, IdXml);
					}
					return true;

				case "unsubscribe":
					// TODO
					return false;

				case "remove":
					ServiceRef.XmppService.GetRosterItem(Jid);
					// TODO
					return false;

				default:
					return false;
			}
		}

		#region ILinkableView

		/// <summary>
		/// If the current view is linkable.
		/// </summary>
		public bool IsLinkable => true;

		/// <summary>
		/// If App links should be encoded with the link.
		/// </summary>
		public bool EncodeAppLinks => true;

		/// <summary>
		/// Link to the current view
		/// </summary>
		public string Link => Constants.UriSchemes.Xmpp + ":" + this.BareJid;

		/// <summary>
		/// Title of the current view
		/// </summary>
		public Task<string> Title => Task.FromResult<string>(this.FriendlyName);

		/// <summary>
		/// If linkable view has media associated with link.
		/// </summary>
		public bool HasMedia => false;

		/// <summary>
		/// Encoded media, if available.
		/// </summary>
		public byte[] Media => null;

		/// <summary>
		/// Content-Type of associated media.
		/// </summary>
		public string MediaContentType => null;

		#endregion

	}
}
