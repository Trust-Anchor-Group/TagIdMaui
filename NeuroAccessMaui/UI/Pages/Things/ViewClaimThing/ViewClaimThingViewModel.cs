using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services;
using NeuroAccessMaui.Services.Contacts;
using NeuroAccessMaui.UI.Pages.Contacts.Chat;
using NeuroAccessMaui.UI.Pages.Identity.ViewIdentity;
using NeuroAccessMaui.UI.Pages.Things.ViewThing;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.RegularExpressions;
using Waher.Networking.DNS;
using Waher.Networking.DNS.ResourceRecords;
using Waher.Networking.XMPP;
using Waher.Networking.XMPP.Contracts;
using Waher.Networking.XMPP.Provisioning;
using Waher.Networking.XMPP.Provisioning.Events;
using Waher.Persistence;

namespace NeuroAccessMaui.UI.Pages.Things.ViewClaimThing
{
	/// <summary>
	/// The view model to bind to for when displaying thing claim information.
	/// </summary>
	public partial class ViewClaimThingViewModel : XmppViewModel
	{
		/// <summary>
		/// Creates an instance of the <see cref="ViewClaimThingViewModel"/> class.
		/// </summary>
		/// <param name="Args">Navigation arguments</param>
		public ViewClaimThingViewModel(ViewClaimThingNavigationArgs? Args)
			: base()
		{
			this.Uri = Args?.Uri;
			this.Tags = [];

			if (this.Uri is not null)
			{
				if (ServiceRef.XmppService.TryDecodeIoTDiscoClaimURI(this.Uri, out MetaDataTag[]? Tags))
				{
					this.RegistryJid = null;

					foreach (MetaDataTag Tag in Tags)
					{
						this.Tags.Add(new HumanReadableTag(Tag));

						if (string.Equals(Tag.Name, "R", StringComparison.OrdinalIgnoreCase))
							this.RegistryJid = Tag.StringValue;
					}

					if (string.IsNullOrEmpty(this.RegistryJid))
						this.RegistryJid = ServiceRef.XmppService.RegistryServiceJid;
				}
			}
		}

		/// <inheritdoc/>
		protected override Task XmppService_ConnectionStateChanged(object? Sender, XmppState NewState)
		{
			MainThread.BeginInvokeOnMainThread(() =>
			{
				this.SetConnectionStateAndText(NewState);
				this.ClaimThingCommand.NotifyCanExecuteChanged();
			});

			return Task.CompletedTask;
		}

		#region Properties

		/// <summary>
		/// iotdisco URI to process
		/// </summary>
		[ObservableProperty]
		private string? uri;

		/// <summary>
		/// Holds a list of meta-data tags associated with a thing.
		/// </summary>
		public ObservableCollection<HumanReadableTag> Tags { get; }

		/// <summary>
		/// Gets or sets whether a user can claim a thing.
		/// </summary>
		[ObservableProperty]
		private bool makePublic;

		/// <summary>
		/// JID of registry the thing uses.
		/// </summary>
		[ObservableProperty]
		private string? registryJid;

		#endregion

		/// <summary>
		/// Command to bind to for detecting when a tag value has been clicked on.
		/// </summary>
		[RelayCommand]
		private static Task Click(object obj)
		{
			if (obj is HumanReadableTag Tag)
				return LabelClicked(Tag.Name, Tag.Value, Tag.LocalizedValue);
			else if (obj is string s)
				return LabelClicked(string.Empty, s, s);
			else
				return Task.CompletedTask;
		}

		/// <summary>
		/// Processes the click of a localized meta-data label.
		/// </summary>
		/// <param name="Name">Tag name</param>
		/// <param name="Value">Tag value</param>
		/// <param name="LocalizedValue">Localized tag value</param>
		public static async Task LabelClicked(string Name, string Value, string LocalizedValue)
		{
			try
			{
				switch (Name)
				{
					case "MAN":
						if (System.Uri.TryCreate("https://" + Value, UriKind.Absolute, out Uri? Uri) && await Launcher.TryOpenAsync(Uri))
							return;
						break;

					case "PURL":
						if (System.Uri.TryCreate(Value, UriKind.Absolute, out Uri) && await Launcher.TryOpenAsync(Uri))
							return;
						break;

					case "R":
						SRV SRV;

						try
						{
							SRV = await DnsResolver.LookupServiceEndpoint(Value, "xmpp-server", "tcp");
						}
						catch
						{
							break;
						}

						if (System.Uri.TryCreate("https://" + SRV.TargetHost, UriKind.Absolute, out Uri) && await Launcher.TryOpenAsync(Uri))
							return;
						break;

					default:
						if ((Value.StartsWith("http://", StringComparison.CurrentCultureIgnoreCase) ||
							Value.StartsWith("https://", StringComparison.CurrentCultureIgnoreCase)) &&
							System.Uri.TryCreate(Value, UriKind.Absolute, out Uri) &&
							await Launcher.TryOpenAsync(Uri))
						{
							return;
						}
						else
						{
							Match M = XmppClient.BareJidRegEx.Match(Value);

							if (M.Success && M.Index == 0 && M.Length == Value.Length)
							{
								ContactInfo Info = await ContactInfo.FindByBareJid(Value);
								if (Info is not null)
								{
									await ServiceRef.UiService.GoToAsync(nameof(ChatPage), new ChatNavigationArgs(Info));
									return;
								}

								int i = Value.IndexOf('@');
								if (i > 0 && Guid.TryParse(Value[..i], out _))
								{
									if (ServiceRef.UiService.CurrentPage is not ViewIdentityPage)
									{
										Info = await ContactInfo.FindByLegalId(Value);
										if (Info?.LegalIdentity is not null)
										{
											await ServiceRef.UiService.GoToAsync(nameof(ViewIdentityPage),
												new ViewIdentityNavigationArgs(Info.LegalIdentity));
											return;
										}
									}
								}
								else
								{
									string FriendlyName = await ContactInfo.GetFriendlyName(Value);
									await ServiceRef.UiService.GoToAsync(nameof(ChatPage), new ChatNavigationArgs(string.Empty, Value, FriendlyName));
									return;
								}
							}
						}
						break;
				}

				await Clipboard.SetTextAsync(LocalizedValue);
				await ServiceRef.UiService.DisplayAlert(ServiceRef.Localizer[nameof(AppResources.SuccessTitle)],
					ServiceRef.Localizer[nameof(AppResources.TagValueCopiedToClipboard)]);
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				await ServiceRef.UiService.DisplayException(ex);
			}
		}

		/// <summary>
		/// Get Friendly name of thing
		/// </summary>
		public static string? GetFriendlyName(IEnumerable<HumanReadableTag> Tags)
		{
			return GetFriendlyName(ToProperties(Tags));
		}

		/// <summary>
		/// Get Friendly name of thing
		/// </summary>
		public static string? GetFriendlyName(IEnumerable<MetaDataTag> Tags)
		{
			return GetFriendlyName(ToProperties(Tags));
		}

		/// <summary>
		/// Get Friendly name of thing
		/// </summary>
		public static string? GetFriendlyName(IEnumerable<Property> Tags)
		{
			return ContactInfo.GetFriendlyName(Tags);
		}

		/// <summary>
		/// Gets or sets whether a user can claim a thing.
		/// </summary>
		public bool CanClaimThing
		{
			get { return this.IsConnected && ServiceRef.XmppService.IsOnline; }
		}

		/// <inheritdoc/>
		protected override void OnPropertyChanged(PropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);

			switch (e.PropertyName)
			{
				case nameof(this.IsConnected):
					this.ClaimThingCommand.NotifyCanExecuteChanged();
					break;
			}
		}

		/// <summary>
		/// The command to bind to for claiming a thing
		/// </summary>
		[RelayCommand(CanExecute = nameof(CanClaimThing))]
		private async Task ClaimThing()
		{
			try
			{
				if (string.IsNullOrEmpty(this.Uri))
					return;

				if (!await App.AuthenticateUserAsync(AuthenticationPurpose.ClaimThing, true))
					return;

				(bool Succeeded, NodeResultEventArgs? e) = await ServiceRef.NetworkService.TryRequest(() =>
					ServiceRef.XmppService.ClaimThing(this.Uri, this.MakePublic));

				if (!Succeeded || e is null)
					return;

				if (e.Ok)
				{
					string? FriendlyName = GetFriendlyName(this.Tags);
					RosterItem? Item = ServiceRef.XmppService.GetRosterItem(e.JID);
					if (Item is null)
						ServiceRef.XmppService.AddRosterItem(new RosterItem(e.JID, FriendlyName));

					//Remove Key Tag from the list of tags
					foreach (HumanReadableTag Tag in this.Tags)
					{
						if (string.Equals(Tag.Name, Constants.XmppProperties.Key, StringComparison.OrdinalIgnoreCase))
						{
							this.Tags.Remove(Tag);
							break;
						}
					}

					ContactInfo Info = await ContactInfo.FindByBareJid(e.JID, e.Node.SourceId, e.Node.Partition, e.Node.NodeId);
					if (Info is null)
					{
						Info = new ContactInfo()
						{
							BareJid = e.JID,
							LegalId = string.Empty,
							LegalIdentity = null,
							FriendlyName = FriendlyName ?? string.Empty,
							IsThing = true,
							Owner = true,
							SourceId = e.Node.SourceId,
							Partition = e.Node.Partition,
							NodeId = e.Node.NodeId,
							MetaData = ToProperties(this.Tags),
							RegistryJid = this.RegistryJid
						};

						await Database.Insert(Info);
					}
					else
					{
						Info.FriendlyName = FriendlyName ?? string.Empty;

						await Database.Update(Info);
					}

					await Database.Provider.Flush();

					ServiceRef.XmppService.RequestPresenceSubscription(Info.BareJid);

					await ServiceRef.UiService.GoToAsync(nameof(ViewThingPage), new ViewThingNavigationArgs(Info, []), Services.UI.BackMethod.Pop2);
				}
				else
				{
					string Msg = e.ErrorText;
					if (string.IsNullOrEmpty(Msg))
						Msg = ServiceRef.Localizer[nameof(AppResources.UnableToClaimThing)];

					await ServiceRef.UiService.DisplayAlert(ServiceRef.Localizer[nameof(AppResources.ErrorTitle)], Msg);
				}
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				await ServiceRef.UiService.DisplayException(ex);
			}
		}

		/// <summary>
		/// Converts an enumerable set of <see cref="HumanReadableTag"/> to an enumerable set of <see cref="Property"/>.
		/// </summary>
		/// <param name="Tags">Enumerable set of <see cref="HumanReadableTag"/></param>
		/// <returns>Enumerable set of <see cref="Property"/></returns>
		public static Property[] ToProperties(IEnumerable<HumanReadableTag> Tags)
		{
			List<Property> Result = [];

			foreach (HumanReadableTag Tag in Tags)
				Result.Add(new Property(Tag.Name, Tag.Value));

			return [.. Result];
		}

		/// <summary>
		/// Converts an enumerable set of <see cref="MetaDataTag"/> to an enumerable set of <see cref="Property"/>.
		/// </summary>
		/// <param name="Tags">Enumerable set of <see cref="MetaDataTag"/></param>
		/// <returns>Enumerable set of <see cref="Property"/></returns>
		public static Property[] ToProperties(IEnumerable<MetaDataTag> Tags)
		{
			List<Property> Result = [];

			foreach (MetaDataTag Tag in Tags)
				Result.Add(new Property(Tag.Name, Tag.StringValue));

			return [.. Result];
		}

	}
}
