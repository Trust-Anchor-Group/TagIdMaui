using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services;
using NeuroAccessMaui.Services.Contacts;
using NeuroAccessMaui.UI.Pages.Things.ReadSensor.Model;
using NeuroAccessMaui.UI.Pages.Things.ViewClaimThing;
using NeuroAccessMaui.UI.Pages.Things.ViewThing;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Waher.Networking.XMPP;
using Waher.Networking.XMPP.Contracts;
using Waher.Networking.XMPP.Events;
using Waher.Networking.XMPP.PEP;
using Waher.Networking.XMPP.PEP.Events;
using Waher.Networking.XMPP.Sensor;
using Waher.Things;
using Waher.Things.SensorData;

namespace NeuroAccessMaui.UI.Pages.Things.ReadSensor
{
	/// <summary>
	/// The view model to bind to when displaying a thing.
	/// </summary>
	public partial class ReadSensorViewModel : XmppViewModel
	{
		private readonly ContactInfo? thing;
		private readonly ThingReference? thingRef;
		private SensorDataClientRequest? request;

		/// <summary>
		/// Creates an instance of the <see cref="ReadSensorViewModel"/> class.
		/// </summary>
		/// <param name="Args">Navigation arguments</param>
		public ReadSensorViewModel(ViewThingNavigationArgs? Args)
			: base()
		{
			this.thing = Args?.Thing;

			this.SensorData = [];

			if (this.thing is not null)
			{
				if (string.IsNullOrEmpty(this.thing.NodeId) && string.IsNullOrEmpty(this.thing.SourceId) && string.IsNullOrEmpty(this.thing.Partition))
					this.thingRef = null;
				else
					this.thingRef = new ThingReference(this.thing.NodeId, this.thing.SourceId, this.thing.Partition);

				if (this.thing.MetaData is not null && this.thing.MetaData.Length > 0)
				{
					this.SensorData.Add(new HeaderModel(ServiceRef.Localizer[nameof(AppResources.GeneralInformation)]));

					foreach (Property Tag in this.thing.MetaData)
						this.SensorData.Add(new HumanReadableTag(Tag));
				}

				this.SupportsSensorEvents = this.thing.SupportsSensorEvents ?? false;
			}
		}

		/// <inheritdoc/>
		protected override async Task OnInitialize()
		{
			await base.OnInitialize();

			this.CalcThingIsOnline();

			ServiceRef.XmppService.OnPresence += this.Xmpp_OnPresence;
			ServiceRef.TagProfile.Changed += this.TagProfile_Changed;

			string? FullJid = this.GetFullJid();

			if (!string.IsNullOrEmpty(FullJid))
			{
				if (this.thingRef is null || this.thingRef.IsEmpty)
					this.request = await ServiceRef.XmppService.RequestSensorReadout(FullJid, FieldType.All);
				else
					this.request = await ServiceRef.XmppService.RequestSensorReadout(FullJid, [this.thingRef], FieldType.All);

				this.request.OnStateChanged += this.Request_OnStateChanged;
				this.request.OnFieldsReceived += this.Request_OnFieldsReceived;
				this.request.OnErrorsReceived += this.Request_OnErrorsReceived;
			}

			ServiceRef.XmppService.RegisterPepHandler(typeof(SensorData), this.SensorDataPersonalEventHandler);
		}

		private Task Request_OnStateChanged(object? Sender, SensorDataReadoutState NewState)
		{
			this.Status = NewState switch
			{
				SensorDataReadoutState.Requested => ServiceRef.Localizer[nameof(AppResources.SensorDataRequested)],
				SensorDataReadoutState.Accepted => ServiceRef.Localizer[nameof(AppResources.SensorDataAccepted)],
				SensorDataReadoutState.Cancelled => ServiceRef.Localizer[nameof(AppResources.SensorDataCancelled)],
				SensorDataReadoutState.Done => ServiceRef.Localizer[nameof(AppResources.SensorDataDone)],
				SensorDataReadoutState.Failure => ServiceRef.Localizer[nameof(AppResources.SensorDataFailure)],
				SensorDataReadoutState.Receiving => ServiceRef.Localizer[nameof(AppResources.SensorDataReceiving)],
				SensorDataReadoutState.Started => ServiceRef.Localizer[nameof(AppResources.SensorDataStarted)],
				_ => string.Empty,
			};

			return Task.CompletedTask;
		}

		private static string GetFieldTypeString(FieldType Type)
		{
			if (Type.HasFlag(FieldType.Identity))
				return ServiceRef.Localizer[nameof(AppResources.SensorDataHeaderIdentity)];
			else if (Type.HasFlag(FieldType.Status))
				return ServiceRef.Localizer[nameof(AppResources.SensorDataHeaderStatus)];
			else if (Type.HasFlag(FieldType.Momentary))
				return ServiceRef.Localizer[nameof(AppResources.SensorDataHeaderMomentary)];
			else if (Type.HasFlag(FieldType.Peak))
				return ServiceRef.Localizer[nameof(AppResources.SensorDataHeaderPeak)];
			else if (Type.HasFlag(FieldType.Computed))
				return ServiceRef.Localizer[nameof(AppResources.SensorDataHeaderComputed)];
			else if (Type.HasFlag(FieldType.Historical))
				return ServiceRef.Localizer[nameof(AppResources.SensorDataHeaderHistorical)];
			else
				return ServiceRef.Localizer[nameof(AppResources.SensorDataHeaderOther)];
		}

		private Task Request_OnFieldsReceived(object? Sender, IEnumerable<Field> NewFields)
		{
			return this.NewFieldsReported(NewFields);
		}

		private Task NewFieldsReported(IEnumerable<Field> NewFields)
		{
			MainThread.BeginInvokeOnMainThread(() =>
			{
				string Category;
				HeaderModel? CategoryHeader = null;
				string FieldName;
				int CategoryIndex = 0;
				int i, j, c;
				bool IsMin;
				bool IsMax;

				foreach (Field Field in NewFields)
				{
					FieldName = Field.Name;

					if (Field.Type.HasFlag(FieldType.Historical))
					{
						IsMin = false;
						IsMax = false;

						if (FieldName.EndsWith(", Min", StringComparison.InvariantCultureIgnoreCase))
						{
							FieldName = FieldName[0..^5];
							IsMin = true;
						}
						else if (FieldName.EndsWith(", Max", StringComparison.InvariantCultureIgnoreCase))
						{
							FieldName = FieldName[0..^5];
							IsMax = true;
						}

						Category = FieldName;
					}
					else
					{
						Category = GetFieldTypeString(Field.Type);
						IsMin = IsMax = false;
					}

					if (CategoryHeader is null || CategoryHeader.Label != Category)
					{
						CategoryHeader = null;
						CategoryIndex = 0;

						foreach (object Item in this.SensorData)
						{
							if (Item is HeaderModel Header && Header.Label == Category)
							{
								CategoryHeader = Header;
								break;
							}
							else
								CategoryIndex++;
						}

						if (CategoryHeader is null)
						{
							CategoryHeader = new HeaderModel(Category);
							this.SensorData.Add(CategoryHeader);
						}
					}

					if (Field.Type.HasFlag(FieldType.Historical))
					{
						for (i = CategoryIndex + 1, c = this.SensorData.Count; i < c; i++)
						{
							object Obj = this.SensorData[i];

							if (Obj is GraphModel GraphModel)
							{
								j = string.Compare(FieldName, GraphModel.FieldName, StringComparison.OrdinalIgnoreCase);
								if (j < 0)
									continue;
								else
								{
									if (j == 0)
									{
										if (IsMin)
											GraphModel.AddMin(Field);
										else if (IsMax)
											GraphModel.AddMax(Field);
										else
											GraphModel.Add(Field);
									}
									else if (j > 0 && !IsMin && !IsMax)
										this.SensorData.Insert(i, new GraphModel(Field));

									break;
								}
							}
							else
							{
								this.SensorData.Insert(i, new GraphModel(Field));
								break;
							}
						}

						if (i >= c)
							this.SensorData.Add(new GraphModel(Field));
					}
					else
					{
						for (i = CategoryIndex + 1, c = this.SensorData.Count; i < c; i++)
						{
							object Obj = this.SensorData[i];

							if (Obj is FieldModel FieldModel)
							{
								j = string.Compare(FieldName, FieldModel.Name, StringComparison.OrdinalIgnoreCase);
								if (j < 0)
									continue;
								else
								{
									if (j == 0)
										FieldModel.Field = Field;
									else if (j > 0)
										this.SensorData.Insert(i, new FieldModel(Field));

									break;
								}
							}
							else
							{
								this.SensorData.Insert(i, new FieldModel(Field));
								break;
							}
						}

						if (i >= c)
							this.SensorData.Add(new FieldModel(Field));
					}
				}
			});

			return Task.CompletedTask;
		}

		private Task Request_OnErrorsReceived(object? Sender, IEnumerable<ThingError> NewErrors)
		{
			return this.NewErrorsReported(NewErrors);
		}

		private Task NewErrorsReported(IEnumerable<ThingError> NewErrors)
		{
			MainThread.BeginInvokeOnMainThread(() =>
			{
				string Errors = ServiceRef.Localizer[nameof(AppResources.Errors)];
				HeaderModel? CategoryHeader = null;
				int CategoryIndex = 0;
				int i, c;

				foreach (object Item in this.SensorData)
				{
					if (Item is HeaderModel Header && Header.Label == Errors)
					{
						CategoryHeader = Header;
						break;
					}
					else
						CategoryIndex++;
				}

				if (CategoryHeader is null)
				{
					CategoryHeader = new HeaderModel(Errors);
					this.SensorData.Add(CategoryHeader);
				}

				for (i = CategoryIndex + 1, c = this.SensorData.Count; i < c; i++)
				{
					object Obj = this.SensorData[i];

					if (Obj is ErrorModel ErrorModel)
						continue;
					else
					{
						foreach (ThingError Error in NewErrors)
						{
							this.SensorData.Insert(i++, new ErrorModel(Error));
							c++;
						}

						break;
					}
				}

				if (i >= c)
				{
					foreach (ThingError Error in NewErrors)
						this.SensorData.Add(new ErrorModel(Error));
				}
			});

			return Task.CompletedTask;
		}

		private async Task SensorDataPersonalEventHandler(object? Sender, PersonalEventNotificationEventArgs e)
		{
			if (e.PersonalEvent is SensorData SensorData &&
				!string.IsNullOrEmpty(this.thing?.BareJid) &&
				string.Equals(this.thing.BareJid, e.Publisher, StringComparison.OrdinalIgnoreCase) &&
				string.IsNullOrEmpty(this.thing.SourceId) &&
				string.IsNullOrEmpty(this.thing.NodeId) &&
				string.IsNullOrEmpty(this.thing.Partition))
			{
				if (SensorData.Fields is not null)
					await this.NewFieldsReported(SensorData.Fields);

				if (SensorData.Errors is not null)
					await this.NewErrorsReported(SensorData.Errors);
			}
		}

		/// <inheritdoc/>
		protected override async Task OnDispose()
		{
			ServiceRef.XmppService.UnregisterPepHandler(typeof(SensorData), this.SensorDataPersonalEventHandler);

			ServiceRef.XmppService.OnPresence -= this.Xmpp_OnPresence;
			ServiceRef.TagProfile.Changed -= this.TagProfile_Changed;

			if (this.request is not null &&
				(this.request.State == SensorDataReadoutState.Receiving ||
				this.request.State == SensorDataReadoutState.Accepted ||
				this.request.State == SensorDataReadoutState.Requested ||
				this.request.State == SensorDataReadoutState.Started))
			{
				await this.request.Cancel();
			}

			await base.OnDispose();
		}

		private Task Xmpp_OnPresence(object? Sender, PresenceEventArgs e)
		{
			this.CalcThingIsOnline();
			return Task.CompletedTask;
		}

		private void CalcThingIsOnline()
		{
			if (this.thing is null)
				this.IsThingOnline = false;
			else
			{
				RosterItem? Item = ServiceRef.XmppService.GetRosterItem(this.thing.BareJid);
				this.IsThingOnline = Item is not null && Item.HasLastPresence && Item.LastPresence.IsOnline;
			}
		}

		private string? GetFullJid()
		{
			if (this.thing is null)
				return null;
			else
			{
				RosterItem? Item = ServiceRef.XmppService.GetRosterItem(this.thing.BareJid);

				if (Item is null || !Item.HasLastPresence || !Item.LastPresence.IsOnline)
					return null;
				else
					return Item.LastPresenceFullJid;
			}
		}

		private void TagProfile_Changed(object? Sender, PropertyChangedEventArgs e)
		{
			MainThread.BeginInvokeOnMainThread(this.CalcThingIsOnline);
		}

		#region Properties

		/// <summary>
		/// Holds a list of meta-data tags associated with a thing.
		/// </summary>
		public ObservableCollection<object> SensorData { get; }

		/// <summary>
		/// Gets or sets whether the thing is in the contact.
		/// </summary>
		[ObservableProperty]
		private bool isThingOnline;

		/// <summary>
		/// Gets or sets whether the thing is a sensor
		/// </summary>
		[ObservableProperty]
		private bool supportsSensorEvents;

		/// <summary>
		/// Gets or sets whether there's a status message to display
		/// </summary>
		[ObservableProperty]
		private bool hasStatus;

		/// <summary>
		/// Gets or sets a status message.
		/// </summary>
		[ObservableProperty]
		private string? status;

		/// <inheritdoc/>
		protected override void OnPropertyChanged(PropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);

			switch (e.PropertyName)
			{
				case nameof(this.Status):
					this.HasStatus = !string.IsNullOrEmpty(this.Status);
					break;

				case nameof(this.IsConnected):
					this.CalcThingIsOnline();
					break;
			}
		}

		#endregion

		/// <summary>
		/// Command to bind to for detecting when a tag value has been clicked on.
		/// </summary>
		[RelayCommand]
		private static Task Click(object obj)
		{
			if (obj is HumanReadableTag Tag)
				return ViewClaimThingViewModel.LabelClicked(Tag.Name, Tag.Value, Tag.LocalizedValue);
			else if (obj is FieldModel Field)
				return ViewClaimThingViewModel.LabelClicked(Field.Name, Field.ValueString, Field.ValueString);
			else if (obj is ErrorModel Error)
				return ViewClaimThingViewModel.LabelClicked(string.Empty, Error.ErrorMessage, Error.ErrorMessage);
			else if (obj is string s)
				return ViewClaimThingViewModel.LabelClicked(string.Empty, s, s);
			else
				return Task.CompletedTask;
		}

	}
}
