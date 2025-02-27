using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services;
using NeuroAccessMaui.Services.Contacts;
using NeuroAccessMaui.Services.Localization;
using NeuroAccessMaui.Services.Notification;
using NeuroAccessMaui.Services.UI;
using NeuroAccessMaui.UI.Pages.Contacts.Chat;
using NeuroAccessMaui.UI.Pages.Contracts.MyContracts.ObjectModels;
using NeuroAccessMaui.UI.Pages.Main.XmppForm;
using NeuroAccessMaui.UI.Pages.Things.MyThings;
using NeuroAccessMaui.UI.Pages.Things.ReadSensor;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using Waher.Networking.XMPP;
using Waher.Networking.XMPP.Concentrator;
using Waher.Networking.XMPP.Contracts;
using Waher.Networking.XMPP.Control;
using Waher.Networking.XMPP.DataForms;
using Waher.Networking.XMPP.Events;
using Waher.Networking.XMPP.Sensor;
using Waher.Networking.XMPP.ServiceDiscovery;
using Waher.Persistence;
using Waher.Things;

namespace NeuroAccessMaui.UI.Pages.Things.ViewThing
{
	/// <summary>
	/// The view model to bind to when displaying a thing.
	/// </summary>
	public partial class ViewThingViewModel : QrXmppViewModel
	{
		private readonly Dictionary<string, PresenceEventArgs> presences = new(StringComparer.InvariantCultureIgnoreCase);
		private readonly ViewThingNavigationArgs? navigationArguments;
		private readonly ContactInfo? thing;

		/// <summary>
		/// Creates an instance of the <see cref="ViewThingViewModel"/> class.
		/// </summary>
		/// <param name="Args">Navigation arguments</param>
		public ViewThingViewModel(ViewThingNavigationArgs? Args)
			: base()
		{
			this.navigationArguments = Args;
			this.thing = Args?.Thing;

			this.Tags = [];
			this.Notifications = [];

			if (this.thing?.MetaData is not null)
			{
				foreach (Property Tag in this.thing.MetaData)
					this.Tags.Add(new HumanReadableTag(Tag));
			}

			this.InContacts = !string.IsNullOrEmpty(this.thing?.ObjectId);
			this.IsOwner = this.thing?.Owner ?? false;
			this.IsSensor = this.thing?.IsSensor ?? false;
			this.IsActuator = this.thing?.IsActuator ?? false;
			this.IsConcentrator = this.thing?.IsConcentrator ?? false;
			this.IsNodeInConcentrator = !string.IsNullOrEmpty(this.thing?.NodeId) || !string.IsNullOrEmpty(this.thing?.SourceId) || !string.IsNullOrEmpty(this.thing?.Partition);
			this.SupportsSensorEvents = this.thing?.SupportsSensorEvents ?? false;

			this.InContactsAndNotOwner = this.InContacts && !this.IsOwner;
			this.NotInContacts = !this.InContacts;
			this.IsConnectedAndOwner = this.IsConnected && this.IsOwner;
			this.IsConnectedAndSensor = this.IsConnected && this.IsSensor;
			this.IsConnectedAndActuator = this.IsConnected && this.IsActuator;
			this.IsConnectedAndNotConcentrator = this.IsConnected && !this.IsConcentrator;
		}

		/// <inheritdoc/>
		protected override async Task OnInitialize()
		{
			await base.OnInitialize();

			if (this.navigationArguments?.Events is not null)
			{
				this.Notifications.Clear();

				int c = 0;

				foreach (NotificationEvent Event in this.navigationArguments.Events)
				{
					this.Notifications.Add(new EventModel(Event.Received,
						await Event.GetCategoryIcon(),
						await Event.GetDescription(),
						Event));

					if (Event.Type == NotificationEventType.Contacts)
						c++;
				}

				this.NrPendingChatMessages = c;
				this.HasPendingChatMessages = c > 0;
			}

			this.HasNotifications = this.Notifications.Count > 0;

			await this.CalcThingIsOnline();

			ServiceRef.XmppService.OnPresence += this.Xmpp_OnPresence;
			ServiceRef.XmppService.OnRosterItemAdded += this.Xmpp_OnRosterItemAdded;
			ServiceRef.XmppService.OnRosterItemUpdated += this.Xmpp_OnRosterItemUpdated;
			ServiceRef.XmppService.OnRosterItemRemoved += this.Xmpp_OnRosterItemRemoved;
			ServiceRef.TagProfile.Changed += this.TagProfile_Changed;
			ServiceRef.NotificationService.OnNewNotification += this.NotificationService_OnNewNotification;
			ServiceRef.NotificationService.OnNotificationsDeleted += this.NotificationService_OnNotificationsDeleted;

			if (this.IsConnected && this.IsThingOnline)
				await this.CheckCapabilities();

			this.GenerateQrCode(this.Link);
		}


		protected override Task XmppService_ConnectionStateChanged(object? _, XmppState NewState)
		{
			base.XmppService_ConnectionStateChanged(_, NewState);

			MainThread.BeginInvokeOnMainThread(async () => await this.CalcThingIsOnline());

			return Task.CompletedTask;
		}

		private async Task CheckCapabilities()
		{
			if (this.InContacts &&
				this.thing is not null &&
				(!this.thing.IsSensor.HasValue ||
				!this.thing.IsActuator.HasValue ||
				!this.thing.IsConcentrator.HasValue ||
				!this.thing.SupportsSensorEvents.HasValue))
			{
				string? FullJid = this.GetFullJid();

				if (!string.IsNullOrEmpty(FullJid))
				{
					try
					{
						ServiceDiscoveryEventArgs e = await ServiceRef.XmppService.SendServiceDiscoveryRequest(FullJid);
						if (!this.InContacts)
							return;

						this.thing.IsSensor = e.HasAnyFeature(SensorClient.NamespacesSensorData);
						this.thing.SupportsSensorEvents = e.HasAnyFeature(SensorClient.NamespacesSensorEvents);
						this.thing.IsActuator = e.HasAnyFeature(ControlClient.NamespacesControl);
						this.thing.IsConcentrator = e.HasAnyFeature(ConcentratorServer.NamespacesConcentrator);

						if (this.InContacts && !string.IsNullOrEmpty(this.thing.ObjectId))
							await Database.Update(this.thing);

						MainThread.BeginInvokeOnMainThread(() =>
						{
							this.IsSensor = this.thing.IsSensor ?? false;
							this.IsActuator = this.thing.IsActuator ?? false;
							this.IsConcentrator = this.thing.IsConcentrator ?? false;
							this.SupportsSensorEvents = this.thing.SupportsSensorEvents ?? false;
						});
					}
					catch (Exception ex)
					{
						ServiceRef.LogService.LogException(ex);
					}
				}
			}
		}

		/// <inheritdoc/>
		protected override async Task OnDispose()
		{
			ServiceRef.XmppService.OnPresence -= this.Xmpp_OnPresence;
			ServiceRef.XmppService.OnRosterItemAdded -= this.Xmpp_OnRosterItemAdded;
			ServiceRef.XmppService.OnRosterItemUpdated -= this.Xmpp_OnRosterItemUpdated;
			ServiceRef.XmppService.OnRosterItemRemoved -= this.Xmpp_OnRosterItemRemoved;
			ServiceRef.TagProfile.Changed -= this.TagProfile_Changed;
			ServiceRef.NotificationService.OnNewNotification -= this.NotificationService_OnNewNotification;
			ServiceRef.NotificationService.OnNotificationsDeleted -= this.NotificationService_OnNotificationsDeleted;

			await base.OnDispose();
		}

		private async Task Xmpp_OnPresence(object? Sender, PresenceEventArgs e)
		{
			switch (e.Type)
			{
				case PresenceType.Available:
					this.presences[e.FromBareJID] = e;

					if (!this.InContacts && string.Equals(e.FromBareJID, this.thing?.BareJid, StringComparison.OrdinalIgnoreCase))
					{
						if (string.IsNullOrEmpty(this.thing?.ObjectId))
							await Database.Insert(this.thing);

						MainThread.BeginInvokeOnMainThread(() =>
						{
							this.InContacts = true;
						});
					}

					await this.CalcThingIsOnline();

					break;

				case PresenceType.Unavailable:
					this.presences.Remove(e.FromBareJID);
					break;
			}
		}

		private async Task CalcThingIsOnline()
		{

			if (this.thing is null)
				MainThread.BeginInvokeOnMainThread(() => this.IsThingOnline = false);
			else
			{
				try
				{
					await MainThread.InvokeOnMainThreadAsync(() => this.IsThingOnline = this.IsOnline(this.thing.BareJid));
					if (this.IsThingOnline)
						await this.CheckCapabilities();
				}
				catch (Exception ex)
				{
					ServiceRef.LogService.LogException(ex);
					await ServiceRef.UiService.DisplayException(ex);
				}

			}
		}

		async partial void OnInContactsChanged(bool value)
		{
			await this.CalcThingIsOnline();
		}
		private bool IsOnline(string BareJid)
		{
			if (this.presences.TryGetValue(BareJid, out PresenceEventArgs? e))
				return e.IsOnline;
			try
			{
				RosterItem? Item = ServiceRef.XmppService?.GetRosterItem(BareJid);
				if (Item is not null && Item.HasLastPresence)
					return Item.LastPresence.IsOnline;
			}
			catch (Exception)
			{
				//ignore
			}

			return false;
		}

		private string? GetFullJid()
		{
			if (this.thing is null)
				return null;
			else
			{
				if (this.presences.TryGetValue(this.thing.BareJid, out PresenceEventArgs? e))
					return (e?.IsOnline == true) ? e.From : null;

				try
				{
					RosterItem? Item = ServiceRef.XmppService.GetRosterItem(this.thing.BareJid);

					if (Item is null || !Item.HasLastPresence || !Item.LastPresence.IsOnline)
						return null;
					else
						return Item.LastPresenceFullJid;
				}
				catch (Exception)
				{
					return null;
				}

			}
		}

		private void TagProfile_Changed(object? Sender, PropertyChangedEventArgs e)
		{
			MainThread.BeginInvokeOnMainThread(async () => await this.CalcThingIsOnline());
		}

		#region Properties

		/// <summary>
		/// Holds a list of meta-data tags associated with a thing.
		/// </summary>
		public ObservableCollection<HumanReadableTag> Tags { get; }

		/// <summary>
		/// Holds a list of notifications.
		/// </summary>
		public ObservableCollection<EventModel> Notifications { get; }

		/// <summary>
		/// Gets or sets whether the thing is in the contact list.
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(RemoveFromListCommand))]
		[NotifyCanExecuteChangedFor(nameof(AddToListCommand))]
		private bool inContacts;

		/// <summary>
		/// Gets or sets whether the thing is in the contact list.
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(RemoveFromListCommand))]
		[NotifyCanExecuteChangedFor(nameof(AddToListCommand))]
		private bool notInContacts;


		protected override void OnPropertyChanged(PropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);
			MainThread.BeginInvokeOnMainThread(() =>
			{

				switch (e.PropertyName)
				{
					case nameof(this.IsBusy):
						this.ReadSensorCommand.NotifyCanExecuteChanged();
						this.ControlActuatorCommand.NotifyCanExecuteChanged();
						this.ChatCommand.NotifyCanExecuteChanged();
						break;
				}

				//This looks a bit cursed
				switch (e.PropertyName)
				{
					case nameof(this.InContacts):
					case nameof(this.IsOwner):
						this.InContactsAndNotOwner = this.InContacts && !this.IsOwner;
						this.NotInContacts = !this.InContacts;
						break;
				}

				switch (e.PropertyName)
				{
					case nameof(this.IsConnected):
					case nameof(this.IsOwner):
						this.IsConnectedAndOwner = this.IsConnected && this.IsOwner;
						break;
				}

				switch (e.PropertyName)
				{
					case nameof(this.IsConnected):
					case nameof(this.IsSensor):
						this.IsConnectedAndSensor = this.IsConnected && this.IsSensor;
						break;
				}

				switch (e.PropertyName)
				{
					case nameof(this.IsConnected):
					case nameof(this.IsActuator):
						this.IsConnectedAndActuator = this.IsConnected && this.IsActuator;
						break;
				}

				switch (e.PropertyName)
				{
					case nameof(this.IsConnected):
					case nameof(this.IsConcentrator):
						this.IsConnectedAndNotConcentrator = this.IsConnected && !this.IsConcentrator;
						break;
				}
			});
		}

		public string? FriendlyName => this.thing?.FriendlyName ?? this.thing?.BareJid ?? string.Empty;

		/// <summary>
		/// Gets or sets whether the thing is in the contact.
		/// </summary>
		[ObservableProperty]
		private bool isOwner;

		/// <summary>
		/// If the device is in the contact list, but the user is not the owner.
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(RemoveFromListCommand))]
		[NotifyCanExecuteChangedFor(nameof(AddToListCommand))]
		private bool inContactsAndNotOwner;

		/// <summary>
		/// If the user is the owner, and the app is connected.
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(DeleteRulesCommand))]
		[NotifyCanExecuteChangedFor(nameof(DisownThingCommand))]
		private bool isConnectedAndOwner;

		/// <summary>
		/// If the app is connected, and the device is a sensor.
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(ReadSensorCommand))]
		private bool isConnectedAndSensor;

		/// <summary>
		/// If the app is connected, and the device is an actuator.
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(ControlActuatorCommand))]
		private bool isConnectedAndActuator;

		/// <summary>
		/// If the app is connected, and the device is not in a concentrator.
		/// </summary>
		[ObservableProperty]
		private bool isConnectedAndNotConcentrator;

		/// <summary>
		/// Gets or sets whether the thing is in the contact.
		/// </summary>
		[ObservableProperty]
		private bool isThingOnline;

		/// <summary>
		/// Gets or sets whether the thing is a sensor
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(ReadSensorCommand))]
		private bool isSensor;

		/// <summary>
		/// Gets or sets whether the thing is an actuator
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(ControlActuatorCommand))]
		private bool isActuator;

		/// <summary>
		/// Gets or sets whether the thing is a concentrator
		/// </summary>
		[ObservableProperty]
		private bool isConcentrator;

		/// <summary>
		/// Gets or sets whether the thing is a concentrator
		/// </summary>
		[ObservableProperty]
		private bool isNodeInConcentrator;

		/// <summary>
		/// Gets or sets whether the thing is a sensor
		/// </summary>
		[ObservableProperty]
		private bool supportsSensorEvents;
		/// <summary>
		/// Gets or sets whether the thing is a sensor
		/// </summary>
		[ObservableProperty]
		private bool hasNotifications;

		/// <summary>
		/// Gets or sets whether the identity is in the contact.
		/// </summary>
		[ObservableProperty]
		private bool hasPendingChatMessages;

		/// <summary>
		/// Gets or sets whether the identity is in the contact.
		/// </summary>
		[ObservableProperty]
		private int nrPendingChatMessages;

		#endregion

		/// <summary>
		/// Command to bind to for detecting when a tag value has been clicked on.
		/// </summary>
		[RelayCommand]
		private static Task Click(object obj)
		{
			if (obj is HumanReadableTag Tag)
				return ViewClaimThing.ViewClaimThingViewModel.LabelClicked(Tag.Name, Tag.Value, Tag.LocalizedValue);
			else if (obj is string s)
				return ViewClaimThing.ViewClaimThingViewModel.LabelClicked(string.Empty, s, s);
			else
				return Task.CompletedTask;
		}

		/// <summary>
		/// Copies Item to clipboard
		/// </summary>
		[RelayCommand]
		private async Task CopyQr(object Item)
		{
			try
			{
				this.SetIsBusy(true);

				await Clipboard.SetTextAsync(this.Link);
				await ServiceRef.UiService.DisplayAlert(
					ServiceRef.Localizer[nameof(AppResources.SuccessTitle)],
					ServiceRef.Localizer[nameof(AppResources.SuccessTitle)]);
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				await ServiceRef.UiService.DisplayException(ex);
			}
			finally
			{
				this.SetIsBusy(false);
			}
		}

		/// <summary>
		/// The command to bind to for clearing rules for the thing.
		/// </summary>
		[RelayCommand(CanExecute = nameof(IsConnectedAndOwner))]
		private async Task DeleteRules()
		{
			if (this.thing is null)
				return;

			try
			{
				if (!await ServiceRef.UiService.DisplayAlert(
					ServiceRef.Localizer[nameof(AppResources.Question)], ServiceRef.Localizer[nameof(AppResources.DeleteRulesQuestion)],
					ServiceRef.Localizer[nameof(AppResources.Yes)], ServiceRef.Localizer[nameof(AppResources.Cancel)]))
				{
					return;
				}

				if (!await App.AuthenticateUserAsync(AuthenticationPurpose.DeleteRules, true))
					return;

				TaskCompletionSource<bool> Result = new();

				ServiceRef.XmppService.DeleteDeviceRules(this.thing.RegistryJid, this.thing.BareJid, this.thing.NodeId,
					this.thing.SourceId, this.thing.Partition, (sender, e) =>
				{
					if (e.Ok)
						Result.TrySetResult(true);
					else if (e.StanzaError is not null)
						Result.TrySetException(e.StanzaError);
					else
						Result.TrySetResult(false);

					return Task.CompletedTask;
				}, null);


				if (!await Result.Task)
					return;

				await ServiceRef.UiService.DisplayAlert(ServiceRef.Localizer[nameof(AppResources.SuccessTitle)],
					ServiceRef.Localizer[nameof(AppResources.RulesDeleted)]);
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				await ServiceRef.UiService.DisplayException(ex);
			}
		}

		/// <summary>
		/// The command to bind to for disowning a thing
		/// </summary>
		[RelayCommand(CanExecute = nameof(IsConnectedAndOwner))]
		private async Task DisownThing()
		{
			if (this.thing is null)
				return;

			try
			{
				if (!await ServiceRef.UiService.DisplayAlert(
					ServiceRef.Localizer[nameof(AppResources.Question)], ServiceRef.Localizer[nameof(AppResources.DisownThingQuestion)],
					ServiceRef.Localizer[nameof(AppResources.Yes)], ServiceRef.Localizer[nameof(AppResources.Cancel)]))
				{
					return;
				}

				if (!await App.AuthenticateUserAsync(AuthenticationPurpose.DisownThing, true))
					return;

				(bool Succeeded, bool Done) = await ServiceRef.NetworkService.TryRequest(() =>
					ServiceRef.XmppService.Disown(this.thing.RegistryJid, this.thing.BareJid, this.thing.SourceId, this.thing.Partition, this.thing.NodeId));

				if (!Succeeded)
					return;

				if (this.InContacts)
				{
					if (!string.IsNullOrEmpty(this.thing?.ObjectId))
					{
						await Database.Delete(this.thing);
						await Database.Provider.Flush();

						this.thing.ObjectId = null;
					}

					if (this.thing is not null)
						this.thing.ObjectId = null;

					this.InContacts = false;
				}

				await ServiceRef.UiService.DisplayAlert(ServiceRef.Localizer[nameof(AppResources.SuccessTitle)],
					ServiceRef.Localizer[nameof(AppResources.ThingDisowned)]);
				await this.GoBack();
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				await ServiceRef.UiService.DisplayException(ex);
			}
		}

		/// <summary>
		/// The command to bind to for adding a thing to the list
		/// </summary>
		[RelayCommand(CanExecute = nameof(NotInContacts))]
		private async Task AddToList()
		{
			if (this.thing is null)
				return;
			try
			{
				if (!await App.AuthenticateUserAsync(AuthenticationPurpose.AddToListOfThings))
					return;

				RosterItem? Item = ServiceRef.XmppService.GetRosterItem(this.thing.BareJid);
				if (Item is null || Item.State == SubscriptionState.None || Item.State == SubscriptionState.From)
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
					ServiceRef.XmppService.RequestPresenceSubscription(this.thing.BareJid);
					await ServiceRef.UiService.DisplayAlert(ServiceRef.Localizer[nameof(AppResources.SuccessTitle)], ServiceRef.Localizer[nameof(AppResources.ARequestHasBeenSentToTheOwner)]);
					MainThread.BeginInvokeOnMainThread(() => this.NotInContacts = false);
				}
				await MainThread.InvokeOnMainThreadAsync(async () =>
				{
					if (!this.InContacts)
					{
						if (string.IsNullOrEmpty(this.thing.ObjectId))
							await Database.Insert(this.thing);

						this.InContacts = true;
					}

					await this.CalcThingIsOnline();
				});

			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				await ServiceRef.UiService.DisplayException(ex);
			}
		}

		/// <summary>
		/// The command to bind to for removing a thing from the list
		/// </summary>
		[RelayCommand(CanExecute = nameof(InContactsAndNotOwner))]
		private async Task RemoveFromList()
		{
			if (this.thing is null)
				return;

			try
			{
				if (!await App.AuthenticateUserAsync(AuthenticationPurpose.RemoveFromListOfThings))
					return;

				if (this.InContacts)
				{
					if (!string.IsNullOrEmpty(this.thing.ObjectId))
					{
						await Database.Delete(this.thing);
						this.thing.ObjectId = null;
					}

					ServiceRef.XmppService.RequestPresenceUnsubscription(this.thing.BareJid);

					if (ServiceRef.XmppService.GetRosterItem(this.thing.BareJid) is not null)
						ServiceRef.XmppService.RemoveRosterItem(this.thing.BareJid);

					await this.GoBack();
				}
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				await ServiceRef.UiService.DisplayException(ex);
			}
		}
		private bool CanReadSensor => !this.IsBusy && this.IsConnectedAndSensor;

		/// <summary>
		/// The command to bind to for reading a sensor
		/// </summary>
		[RelayCommand(CanExecute = nameof(CanReadSensor))]
		private async Task ReadSensor()
		{
			await MainThread.InvokeOnMainThreadAsync(() => this.SetIsBusy(true));

			if (this.thing is null)
				return;

			ViewThingNavigationArgs Args = new(this.thing, MyThingsViewModel.GetNotificationEvents(this.thing) ?? []);

			await ServiceRef.UiService.GoToAsync(nameof(ReadSensorPage), Args, BackMethod.Pop);

			await MainThread.InvokeOnMainThreadAsync(() => this.SetIsBusy(false));

		}

		private bool CanControlActuator => !this.IsBusy && this.IsConnectedAndActuator;

		/// <summary>
		/// The command to bind to for controlling an actuator
		/// </summary>
		[RelayCommand(CanExecute = nameof(CanControlActuator))]
		private async Task ControlActuator()
		{
			await MainThread.InvokeOnMainThreadAsync(() => this.SetIsBusy(true));

			if (this.thing is null)
				return;

			try
			{
				string? FullJid = this.GetFullJid();
				if (string.IsNullOrEmpty(FullJid))
					return;

				LanguageInfo SelectedLanguage = App.SelectedLanguage;

				if (string.IsNullOrEmpty(this.thing.NodeId) && string.IsNullOrEmpty(this.thing.SourceId) && string.IsNullOrEmpty(this.thing.Partition))
					ServiceRef.XmppService.GetControlForm(FullJid, SelectedLanguage.Name, this.ControlFormCallback, null);
				else
				{
					ThingReference ThingRef = new(this.thing.NodeId, this.thing.SourceId, this.thing.Partition);
					ServiceRef.XmppService.GetControlForm(FullJid, SelectedLanguage.Name, this.ControlFormCallback, null, ThingRef);
				}
			}
			catch (Exception ex)
			{
				await ServiceRef.UiService.DisplayException(ex);
			}
		}

		private Task ControlFormCallback(object? Sender, DataFormEventArgs e)
		{
			if (e.Ok)
			{
				MainThread.BeginInvokeOnMainThread(async () =>
				{
					await ServiceRef.UiService.GoToAsync(nameof(XmppFormPage), new XmppFormNavigationArgs(e.Form));
				});
			}
			else
			{
				ServiceRef.UiService.DisplayException(e.StanzaError ?? new Exception("Unable to get control form."));
			}
			MainThread.BeginInvokeOnMainThread(() => this.SetIsBusy(false));
			return Task.CompletedTask;
		}


		private bool CanChat => !this.IsBusy && this.IsConnectedAndNotConcentrator;

		/// <summary>
		/// The command to bind to for chatting with a thing
		/// </summary>
		[RelayCommand(CanExecute = nameof(CanChat))]
		private async Task Chat()
		{
			await MainThread.InvokeOnMainThreadAsync(() => this.SetIsBusy(true));

			if (this.thing is null)
				return;

			try
			{
				string LegalId = this.thing.LegalId;
				string FriendlyName = this.thing.FriendlyName;
				ChatNavigationArgs Args = new(LegalId, this.thing.BareJid, FriendlyName);

				await ServiceRef.UiService.GoToAsync(nameof(ChatPage), Args, BackMethod.Inherited, this.thing.BareJid);
			}
			catch (Exception ex)
			{
				await ServiceRef.UiService.DisplayException(ex);
			}
			finally
			{
				await MainThread.InvokeOnMainThreadAsync(() => this.SetIsBusy(false));

			}
		}

		private Task NotificationService_OnNotificationsDeleted(object? Sender, NotificationEventsArgs e)
		{
			if (this.thing is not null)
			{
				MainThread.BeginInvokeOnMainThread(() =>
				{
					bool IsNode = this.IsNodeInConcentrator;
					string Key = this.thing.ThingNotificationCategoryKey;
					int NrChatMessagesRemoved = 0;

					foreach (NotificationEvent Event in e.Events)
					{
						switch (Event.Type)
						{
							case NotificationEventType.Contacts:
								if (IsNode)
									continue;

								if (Event.Category != this.thing.BareJid)
									continue;
								break;

							case NotificationEventType.Things:
								if (Event.Category != Key)
									continue;
								break;

							default:
								continue;
						}

						int i = 0;

						foreach (EventModel Model in this.Notifications)
						{
							if (Model.Event.ObjectId == Event.ObjectId)
							{
								this.Notifications.RemoveAt(i);

								if (Event.Type == NotificationEventType.Contacts)
									NrChatMessagesRemoved++;

								break;
							}

							i++;
						}
					}

					this.NrPendingChatMessages -= NrChatMessagesRemoved;
					this.HasNotifications = this.Notifications.Count > 0;
					this.HasPendingChatMessages = this.NrPendingChatMessages > 0;
				});
			}

			return Task.CompletedTask;
		}

		private Task NotificationService_OnNewNotification(object? Sender, NotificationEventArgs e)
		{
			MainThread.BeginInvokeOnMainThread(async () =>
			{
				try
				{
					await this.CalcThingIsOnline();

					switch (e.Event.Type)
					{
						case NotificationEventType.Contacts:
							if (this.IsNodeInConcentrator)
								return;

							if (e.Event.Category != this.thing?.BareJid)
								return;
							break;

						case NotificationEventType.Things:
							if (e.Event.Category != this.thing?.ThingNotificationCategoryKey)
								return;
							break;

						default:
							return;
					}

					this.Notifications.Add(new EventModel(e.Event.Received,
						await e.Event.GetCategoryIcon(),
						await e.Event.GetDescription(),
						e.Event));

					this.HasNotifications = true;

					if (e.Event.Type == NotificationEventType.Contacts)
					{
						this.NrPendingChatMessages++;
						this.HasPendingChatMessages = true;
					}
				}
				catch (Exception ex)
				{
					ServiceRef.LogService.LogException(ex);
				}
			});

			return Task.CompletedTask;
		}

		private Task Xmpp_OnRosterItemRemoved(object? Sender, RosterItem Item)
		{
			this.presences.Remove(Item.BareJid);
			MainThread.BeginInvokeOnMainThread(async () => await this.CalcThingIsOnline());
			return Task.CompletedTask;
		}

		private Task Xmpp_OnRosterItemUpdated(object? Sender, RosterItem Item)
		{
			MainThread.BeginInvokeOnMainThread(async () => await this.CalcThingIsOnline());
			return Task.CompletedTask;
		}

		private Task Xmpp_OnRosterItemAdded(object? Sender, RosterItem Item)
		{
			MainThread.BeginInvokeOnMainThread(async () => await this.CalcThingIsOnline());
			return Task.CompletedTask;
		}

		#region ILinkableView

		/// <summary>
		/// If the current view is linkable.
		/// </summary>
		public override bool IsLinkable => true;

		/// <summary>
		/// If App links should be encoded with the link.
		/// </summary>
		public override bool EncodeAppLinks => true;

		/// <summary>
		/// Link to the current view
		/// </summary>
		public override string Link
		{
			get
			{
				StringBuilder sb = new();
				bool HasJid = false;
				bool HasSourceId = false;
				bool HasPartition = false;
				bool HasNodeId = false;
				bool HasRegistry = false;

				sb.Append("iotdisco:");

				if (this.thing is not null)
				{
					if (this.thing.MetaData is not null)
					{
						foreach (Property P in this.thing.MetaData)
						{

							switch (P.Name.ToUpper(CultureInfo.InvariantCulture))
							{
								case Constants.XmppProperties.Altitude:
								case Constants.XmppProperties.Latitude:
								case Constants.XmppProperties.Longitude:
								case Constants.XmppProperties.Version:
									sb.Append('#');
									break;

								case Constants.XmppProperties.Jid:
									HasJid = true;
									break;

								case Constants.XmppProperties.SourceId:
									HasSourceId = true;
									break;

								case Constants.XmppProperties.Partition:
									HasPartition = true;
									break;

								case Constants.XmppProperties.NodeId:
									HasNodeId = true;
									break;

								case Constants.XmppProperties.Registry:
									HasRegistry = true;
									break;
							}
							sb.Append(Uri.EscapeDataString(P.Name));
							sb.Append('=');
							sb.Append(Uri.EscapeDataString(P.Value));
							sb.Append(';');

						}
					}

					if (!HasJid)
					{
						sb.Append("JID=");
						sb.Append(Uri.EscapeDataString(this.thing.BareJid));
					}

					if (!HasSourceId && !string.IsNullOrEmpty(this.thing.SourceId))
					{
						sb.Append(";SID=");
						sb.Append(Uri.EscapeDataString(this.thing.SourceId));
					}

					if (!HasPartition && !string.IsNullOrEmpty(this.thing.Partition))
					{
						sb.Append(";PT=");
						sb.Append(Uri.EscapeDataString(this.thing.Partition));
					}

					if (!HasNodeId && !string.IsNullOrEmpty(this.thing.NodeId))
					{
						sb.Append(";NID=");
						sb.Append(Uri.EscapeDataString(this.thing.NodeId));
					}

					if (!HasRegistry && !string.IsNullOrEmpty(this.thing.RegistryJid))
					{
						sb.Append(";R=");
						sb.Append(Uri.EscapeDataString(this.thing.RegistryJid));
					}
				}

				return sb.ToString();
			}
		}

		/// <summary>
		/// Title of the current view
		/// </summary>
		public override Task<string> Title => Task.FromResult(this.thing?.FriendlyName ?? string.Empty);

		/// <summary>
		/// If linkable view has media associated with link.
		/// </summary>
		public override bool HasMedia => false;

		/// <summary>
		/// Encoded media, if available.
		/// </summary>
		public override byte[]? Media => null;

		/// <summary>
		/// Content-Type of associated media.
		/// </summary>
		public override string? MediaContentType => null;

		#endregion
	}
}
