using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services;
using NeuroAccessMaui.Services.Contacts;
using NeuroAccessMaui.Services.Notification;
using NeuroAccessMaui.Services.Notification.Things;
using NeuroAccessMaui.UI.Pages.Things.IsFriend;
using NeuroAccessMaui.UI.Pages.Things.ViewClaimThing;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Waher.Networking.XMPP;
using Waher.Networking.XMPP.Contracts;
using Waher.Networking.XMPP.Events;
using Waher.Networking.XMPP.Provisioning;
using Waher.Persistence;
using Waher.Script.Functions.ComplexNumbers;
using Waher.Things;
using Waher.Things.SensorData;

namespace NeuroAccessMaui.UI.Pages.Things.CanRead
{
	/// <summary>
	/// The view model to bind to when displaying a thing.
	/// </summary>
	public partial class CanReadViewModel : XmppViewModel
	{
		private readonly NotificationEvent? @event;
		private readonly CanReadNavigationArgs? navigationArguments;

		/// <summary>
		/// Creates an instance of the <see cref="CanReadViewModel"/> class.
		/// </summary>
		/// <param name="Args">Navigation arguments</param>
		public CanReadViewModel(CanReadNavigationArgs? Args)
			: base()
		{
			this.navigationArguments = Args;
				
			this.Tags = [];
			this.CallerTags = [];
			this.Fields = [];
			this.RuleRanges = [];

			if (Args is not null)
			{
				this.@event = Args.Event;
				this.BareJid = Args.BareJid;
				this.FriendlyName = Args.FriendlyName;
				this.RemoteJid = Args.RemoteJid;
				this.RemoteFriendlyName = Args.RemoteFriendlyName;
				this.Key = Args.Key;
				this.ProvisioningService = Args.ProvisioningService;
				this.FieldTypes = Args.FieldTypes;
				this.NodeId = Args.NodeId;
				this.SourceId = Args.SourceId;
				this.PartitionId = Args.PartitionId;

				this.PermitMomentary = Args.FieldTypes.HasFlag(FieldType.Momentary);
				this.PermitIdentity = Args.FieldTypes.HasFlag(FieldType.Identity);
				this.PermitStatus = Args.FieldTypes.HasFlag(FieldType.Status);
				this.PermitComputed = Args.FieldTypes.HasFlag(FieldType.Computed);
				this.PermitPeak = Args.FieldTypes.HasFlag(FieldType.Peak);
				this.PermitHistorical = Args.FieldTypes.HasFlag(FieldType.Historical);

				if (this.FriendlyName == this.BareJid)
					this.FriendlyName = ServiceRef.Localizer[nameof(AppResources.NotAvailable)];

				this.RemoteFriendlyNameAvailable = this.RemoteFriendlyName != this.RemoteJid;
				if (!this.RemoteFriendlyNameAvailable)
					this.RemoteFriendlyName = ServiceRef.Localizer[nameof(AppResources.NotAvailable)];
			}
		}

		/// <inheritdoc/>
		protected override async Task OnInitialize()
		{
			await base.OnInitialize();

			if (this.navigationArguments is not null)
			{
				this.Tags.Clear();
				this.CallerTags.Clear();

				ContactInfo Thing = await ContactInfo.FindByBareJid(this.BareJid ?? string.Empty);
				if (Thing?.MetaData is not null)
				{
					foreach (Property Tag in Thing.MetaData)
						this.Tags.Add(new HumanReadableTag(Tag));
				}

				ContactInfo Caller = await ContactInfo.FindByBareJid(this.RemoteJid ?? string.Empty);
				this.CallerInContactsList = Caller is not null;
				if (Caller?.MetaData is not null)
				{
					foreach (Property Tag in Caller.MetaData)
						this.CallerTags.Add(new HumanReadableTag(Tag));
				}

				if (this.navigationArguments.UserTokens is not null && this.navigationArguments.UserTokens.Length > 0)
				{
					foreach (ProvisioningToken Token in this.navigationArguments.UserTokens)
						this.CallerTags.Add(new HumanReadableTag(new Property(ServiceRef.Localizer[nameof(AppResources.User)], Token.FriendlyName ?? Token.Token)));

					this.HasUser = true;
				}

				if (this.navigationArguments.ServiceTokens is not null && this.navigationArguments.ServiceTokens.Length > 0)
				{
					foreach (ProvisioningToken Token in this.navigationArguments.ServiceTokens)
						this.CallerTags.Add(new HumanReadableTag(new Property(ServiceRef.Localizer[nameof(AppResources.Service)], Token.FriendlyName ?? Token.Token)));

					this.HasService = true;
				}

				if (this.navigationArguments.DeviceTokens is not null && this.navigationArguments.DeviceTokens.Length > 0)
				{
					foreach (ProvisioningToken Token in this.navigationArguments.DeviceTokens)
						this.CallerTags.Add(new HumanReadableTag(new Property(ServiceRef.Localizer[nameof(AppResources.Device)], Token.FriendlyName ?? Token.Token)));

					this.HasDevice = true;
				}

				this.Fields.Clear();

				SortedDictionary<string, bool> PermittedFields = [];
				string[]? AllFields = this.navigationArguments.AllFields;

				if (AllFields is null && !string.IsNullOrEmpty(this.BareJid))
				{
					AllFields = await CanReadNotificationEvent.GetAvailableFieldNames(this.BareJid, new ThingReference(this.NodeId, this.SourceId, this.PartitionId));

					if (AllFields is not null && this.navigationArguments.Event is not null)
					{
						this.navigationArguments.Event.AllFields = AllFields;
						await Database.Update(this.navigationArguments.Event);
					}
				}

				bool AllFieldsPermitted = this.navigationArguments.Fields is null;

				if (AllFields is not null)
				{
					foreach (string Field in AllFields)
						PermittedFields[Field] = AllFieldsPermitted;
				}

				if (!AllFieldsPermitted && this.navigationArguments?.Fields is not null)
				{
					foreach (string Field in this.navigationArguments.Fields)
						PermittedFields[Field] = true;
				}

				foreach (KeyValuePair<string, bool> P in PermittedFields)
				{
					FieldReference Field = new(P.Key, P.Value);
					this.Fields.Add(Field);
				}

				this.RuleRanges.Clear();
				this.RuleRanges.Add(new RuleRangeModel(RuleRange.Caller, ServiceRef.Localizer[nameof(AppResources.CallerOnly)]));

				if (this.HasUser)
					this.RuleRanges.Add(new RuleRangeModel("User", ServiceRef.Localizer[nameof(AppResources.ToUser)]));

				if (this.HasUser)
					this.RuleRanges.Add(new RuleRangeModel("Service", ServiceRef.Localizer[nameof(AppResources.ToService)]));

				if (this.HasUser)
					this.RuleRanges.Add(new RuleRangeModel("Device", ServiceRef.Localizer[nameof(AppResources.ToDevice)]));

				this.RuleRanges.Add(new RuleRangeModel(RuleRange.Domain, ServiceRef.Localizer[nameof(AppResources.EntireDomain), XmppClient.GetDomain(this.RemoteJid)]));
				this.RuleRanges.Add(new RuleRangeModel(RuleRange.All, ServiceRef.Localizer[nameof(AppResources.Everyone)]));

				this.SelectedRuleRangeIndex = 0;
			}
		}

		#region Properties

		/// <summary>
		/// Holds a list of meta-data tags associated with a thing.
		/// </summary>
		public ObservableCollection<HumanReadableTag> Tags { get; }

		/// <summary>
		/// Holds a list of meta-data tags associated with the caller.
		/// </summary>
		public ObservableCollection<HumanReadableTag> CallerTags { get; }

		/// <summary>
		/// Holds a list of fields that will be permitted.
		/// </summary>
		public ObservableCollection<FieldReference> Fields { get; }

		/// <summary>
		/// Available Rule Ranges
		/// </summary>
		public ObservableCollection<RuleRangeModel> RuleRanges { get; }

		/// <summary>
		/// The Bare JID of the thing.
		/// </summary>
		[ObservableProperty]
		private string? bareJid;

		/// <summary>
		/// The Friendly Name of the thing.
		/// </summary>
		[ObservableProperty]
		private string? friendlyName;

		/// <summary>
		/// The Bare JID of the remote entity trying to connect to the thing.
		/// </summary>
		[ObservableProperty]
		private string? remoteJid;

		/// <summary>
		/// The Friendly Name of the remote entity
		/// </summary>
		[ObservableProperty]
		private string? remoteFriendlyName;

		/// <summary>
		/// If the Friendly Name of the remote entity exists
		/// </summary>
		[ObservableProperty]
		private bool remoteFriendlyNameAvailable;

		/// <summary>
		/// Provisioning key.
		/// </summary>
		[ObservableProperty]
		private string? key;

		/// <summary>
		/// If request has user information
		/// </summary>
		[ObservableProperty]
		private bool hasUser;

		/// <summary>
		/// If request has user information
		/// </summary>
		[ObservableProperty]
		private bool hasService;

		/// <summary>
		/// If request has user information
		/// </summary>
		[ObservableProperty]
		private bool hasDevice;

		/// <summary>
		/// Provisioning key.
		/// </summary>
		[ObservableProperty]
		private string? provisioningService;

		/// <summary>
		/// The Friendly Name of the remote entity
		/// </summary>
		[ObservableProperty]
		private bool callerInContactsList;

		/// <summary>
		/// The selected rule range index
		/// </summary>
		[ObservableProperty]
		private int selectedRuleRangeIndex;

		/// <summary>
		/// Sensor-Data FieldTypes
		/// </summary>
		[ObservableProperty]
		private FieldType fieldTypes;

		/// <summary>
		/// If Momentary values should be permitted.
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
		private bool permitMomentary;

		/// <summary>
		/// If Identity values should be permitted.
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
		private bool permitIdentity;

		/// <summary>
		/// If Status values should be permitted.
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
		private bool permitStatus;

		/// <summary>
		/// If Computed values should be permitted.
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
		private bool permitComputed;

		/// <summary>
		/// If Peak values should be permitted.
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
		private bool permitPeak;

		/// <summary>
		/// If Historical values should be permitted.
		/// </summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
		private bool permitHistorical;

		/// <summary>
		/// Node ID
		/// </summary>
		[ObservableProperty]
		private string? nodeId;

		/// <summary>
		/// Source ID
		/// </summary>
		[ObservableProperty]
		private string? sourceId;

		/// <summary>
		/// Partition ID
		/// </summary>
		[ObservableProperty]
		private string? partitionId;

		#endregion

		/// <summary>
		/// The command to bind to for processing a user click on a label
		/// </summary>
		[RelayCommand]
		private static Task Click(object obj)
		{
			if (obj is HumanReadableTag Tag)
				return ViewClaimThingViewModel.LabelClicked(Tag.Name, Tag.Value, Tag.LocalizedValue);
			else if (obj is string s)
				return ViewClaimThingViewModel.LabelClicked(string.Empty, s, s);
			else
				return Task.CompletedTask;
		}

		/// <summary>
		/// Adds the device to the contact list.
		/// </summary>
		[RelayCommand]
		private async Task AddContact()
		{
			if (!this.CallerInContactsList)
			{
				ContactInfo Info = new()
				{
					BareJid = this.RemoteJid,
					FriendlyName = (this.RemoteFriendlyNameAvailable ? this.RemoteFriendlyName : this.RemoteJid) ?? string.Empty
				};

				await Database.Insert(Info);

				this.CallerInContactsList = true;
			}
		}

		/// <summary>
		/// The command to bind to for reemoving a caller from the contact list.
		/// </summary>
		[RelayCommand]
		private async Task RemoveContact()
		{
			if (this.CallerInContactsList)
			{
				ContactInfo Info = await ContactInfo.FindByBareJid(this.RemoteJid ?? string.Empty);
				if (Info is not null)
					await Database.Delete(Info);

				this.CallerInContactsList = false;
			}
		}

		/// <summary>
		/// The command to bind to for accepting the request
		/// </summary>
		[RelayCommand(CanExecute = nameof(CanExecuteAccept))]
		private void Accept()
		{
			this.Respond(true);
		}

		/// <inheritdoc/>
		protected override void OnPropertyChanged(PropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);

			switch (e.PropertyName)
			{
				case nameof(this.IsConnected):
					this.AcceptCommand.NotifyCanExecuteChanged();
					break;
			}
		}

		private bool CanExecuteAccept()
		{
			if (!this.IsConnected)
				return false;

			if (this.PermitMomentary || this.PermitIdentity || this.PermitStatus || this.PermitComputed || this.PermitPeak || this.PermitHistorical)
				return true;

			foreach (FieldReference Field in this.Fields)
			{
				if (Field.Permitted)
					return true;
			}

			return false;
		}

		/// <summary>
		/// The command to bind to for rejecting the request
		/// </summary>
		[RelayCommand(CanExecute = nameof(this.IsConnected))]
		private void Reject()
		{
			this.Respond(false);
		}

		private void Respond(bool Accepts)
		{
			if (this.SelectedRuleRangeIndex >= 0)
			{
				RuleRangeModel Range = this.RuleRanges[this.SelectedRuleRangeIndex];
				ThingReference Thing = new(this.NodeId, this.SourceId, this.PartitionId);
				FieldType FieldTypes = (FieldType)0;

				if (this.PermitMomentary)
					FieldTypes |= FieldType.Momentary;

				if (this.PermitIdentity)
					FieldTypes |= FieldType.Identity;

				if (this.PermitStatus)
					FieldTypes |= FieldType.Status;

				if (this.PermitComputed)
					FieldTypes |= FieldType.Computed;

				if (this.PermitPeak)
					FieldTypes |= FieldType.Peak;

				if (this.PermitHistorical)
					FieldTypes |= FieldType.Historical;

				if (Range.RuleRange is RuleRange RuleRange)
				{
					ReadoutRequestResolver Resolver = new(this.BareJid ?? string.Empty, this.RemoteFriendlyName ?? string.Empty, RuleRange);

					switch (RuleRange)
					{
						case RuleRange.Caller:
						default:
							ServiceRef.XmppService.CanReadResponseCaller(this.ProvisioningService ?? ServiceRef.TagProfile.ProvisioningJid ?? string.Empty,
								this.BareJid!, this.RemoteJid!, this.Key!, Accepts, FieldTypes, this.GetFields(), Thing, this.ResponseHandler, Resolver);
							break;

						case RuleRange.Domain:
							ServiceRef.XmppService.CanReadResponseDomain(this.ProvisioningService ?? ServiceRef.TagProfile.ProvisioningJid ?? string.Empty,
								this.BareJid!, this.RemoteJid!, this.Key!, Accepts, FieldTypes, this.GetFields(), Thing, this.ResponseHandler, Resolver);
							break;

						case RuleRange.All:
							ServiceRef.XmppService.CanReadResponseAll(this.ProvisioningService ?? ServiceRef.TagProfile.ProvisioningJid ?? string.Empty,
								 this.BareJid!, this.RemoteJid!, this.Key!, Accepts, FieldTypes, this.GetFields(), Thing, this.ResponseHandler, Resolver);
							break;

					}
				}
				else if (Range.RuleRange is ProvisioningToken Token)
				{
					ReadoutRequestResolver Resolver = new(this.BareJid ?? string.Empty, this.RemoteFriendlyName ?? string.Empty, Token);

					switch (Token.Type)
					{
						case TokenType.User:
							ServiceRef.XmppService.CanReadResponseUser(this.ProvisioningService ?? ServiceRef.TagProfile.ProvisioningJid ?? string.Empty,
								this.BareJid!, this.RemoteJid!, this.Key!, Accepts, FieldTypes, this.GetFields(), Token.Token, Thing, this.ResponseHandler, Resolver);
							break;

						case TokenType.Service:
							ServiceRef.XmppService.CanReadResponseService(this.ProvisioningService ?? ServiceRef.TagProfile.ProvisioningJid ?? string.Empty,
								this.BareJid!, this.RemoteJid!, this.Key!, Accepts, FieldTypes, this.GetFields(), Token.Token, Thing, this.ResponseHandler, Resolver);
							break;

						case TokenType.Device:
							ServiceRef.XmppService.CanReadResponseDevice(this.ProvisioningService ?? ServiceRef.TagProfile.ProvisioningJid ?? string.Empty,
								this.BareJid!, this.RemoteJid!, this.Key!, Accepts, FieldTypes, this.GetFields(), Token.Token, Thing, this.ResponseHandler, Resolver);
							break;
					}
				}
			}
		}

		private string[]? GetFields()
		{
			List<string> Result = [];
			bool AllPermitted = true;

			foreach (FieldReference Field in this.Fields)
			{
				if (Field.Permitted)
					Result.Add(Field.Name);
				else
					AllPermitted = false;
			}

			if (AllPermitted)
				return null;
			else
				return [.. Result];
		}

		private async Task ResponseHandler(object? Sender, IqResultEventArgs e)
		{
			if (e.Ok)
			{
				if (this.@event is not null)
					await ServiceRef.NotificationService.DeleteEvents(this.@event);

				await ServiceRef.NotificationService.DeleteResolvedEvents((IEventResolver)e.State);

				MainThread.BeginInvokeOnMainThread(async () =>
				{
					await this.GoBack();
				});
			}
			else
			{
				MainThread.BeginInvokeOnMainThread(async () => await ServiceRef.UiService.DisplayException(e.StanzaError ??
					new Exception(ServiceRef.Localizer[nameof(AppResources.UnableToRespond)])));
			}
		}

		/// <summary>
		/// The command to bind to for ignoring the request
		/// </summary>
		[RelayCommand]
		private async Task Ignore()
		{
			if (this.@event is not null)
				await ServiceRef.NotificationService.DeleteEvents(this.@event);

			await this.GoBack();
		}

		/// <summary>
		/// The command to bind to for selecting all field types
		/// </summary>
		[RelayCommand]
		private void AllFieldTypes()
		{
			this.PermitMomentary = true;
			this.PermitIdentity = true;
			this.PermitStatus = true;
			this.PermitComputed = true;
			this.PermitPeak = true;
			this.PermitHistorical = true;
		}

		/// <summary>
		/// The command to bind to for selecting no field types
		/// </summary>
		[RelayCommand]
		private void NoFieldTypes()
		{
			this.PermitMomentary = false;
			this.PermitIdentity = false;
			this.PermitStatus = false;
			this.PermitComputed = false;
			this.PermitPeak = false;
			this.PermitHistorical = false;
		}

		/// <summary>
		/// The command to bind to for selecting all fields
		/// </summary>
		[RelayCommand]
		private void AllFields()
		{
			foreach (FieldReference Field in this.Fields)
				Field.Permitted = true;
		}

		/// <summary>
		/// The command to bind to for selecting no fields
		/// </summary>
		[RelayCommand]
		private void NoFields()
		{
			foreach (FieldReference Field in this.Fields)
				Field.Permitted = false;
		}

	}
}
