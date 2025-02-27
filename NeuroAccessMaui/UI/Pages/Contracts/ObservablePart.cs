using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeuroAccessMaui.Extensions;
using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services;
using NeuroAccessMaui.Services.Contacts;
using NeuroAccessMaui.Services.Notification;
using NeuroAccessMaui.Services.Notification.Identities;
using NeuroAccessMaui.UI.Pages.Identity.ViewIdentity;
using NeuroAccessMaui.UI.Pages.Signatures.ClientSignature;
using Waher.Networking.XMPP.Contracts;
using Waher.Networking.XMPP.Contracts.EventArguments;
using Waher.Persistence;
using Waher.Persistence.Filters;

namespace NeuroAccessMaui.UI.Pages.Contracts.ObjectModel
{
	public partial class ObservablePart : ObservableObject, IDisposable
	{
		public ObservablePart(Part part)
		{
			this.Part = part;
			ServiceRef.XmppService.PetitionedIdentityResponseReceived += this.XmppService_OnPetitionedIdentityResponseReceived;
		}

		public ObservablePart(Part part, ClientSignature signature)
		{
			this.Part = part;
			ServiceRef.XmppService.PetitionedIdentityResponseReceived += this.XmppService_OnPetitionedIdentityResponseReceived;
			this.Signature = signature;

		}
		/// <summary>
		/// Initializes the part, setting properties which needs to be set asynchronosly
		/// </summary>
		public async Task InitializeAsync(bool sendPetition = true)
		{
			try
			{
				// Set Friendly name before anything can go wrong
				this.FriendlyName = await this.GetFriendlyNameAsync();
				if (sendPetition)
					await this.PetitionIdentityAsync();
				else
				{
					try
					{
						LegalIdentity Identity = await ServiceRef.XmppService.GetLegalIdentity(this.LegalId);
						this.identity = Identity;

						string FriendlyName = await this.GetFriendlyNameAsync();

						MainThread.BeginInvokeOnMainThread(() =>
						{
							this.OnPropertyChanged(nameof(this.HasIdentity));
							this.FriendlyName = FriendlyName;
						});
					}
					catch (Exception)
					{
						//Ignore, OK if forbidden or network error
					}
				}
			}
			catch (Exception E)
			{
				ServiceRef.LogService.LogException(E);
			}
		}

		private async Task XmppService_OnPetitionedIdentityResponseReceived(object? Sender, LegalIdentityPetitionResponseEventArgs e)
		{
			if (e.RequestedIdentity is not null && e.RequestedIdentity.Id == this.LegalId)
			{
				this.identity = e.RequestedIdentity;
				string FriendlyName = await this.GetFriendlyNameAsync();

				MainThread.BeginInvokeOnMainThread(() =>
				{
					this.FriendlyName = FriendlyName;
					this.HasSentPetition = false;
					this.OnPropertyChanged(nameof(this.HasIdentity));
					this.OnPropertyChanged(nameof(this.HasSentPetition));
				});
			}
		}

		private async Task PetitionIdentityAsync()
		{
			this.identity = await ServiceRef.ContractOrchestratorService.TryGetLegalIdentity(this.LegalId,
							ServiceRef.Localizer[nameof(AppResources.ForInclusionInContract)]);

			string FriendlyName = await this.GetFriendlyNameAsync();

			MainThread.BeginInvokeOnMainThread(() =>
			{

				if (this.identity is null)
					this.HasSentPetition = true;
				this.OnPropertyChanged(nameof(this.HasIdentity));
				this.OnPropertyChanged(nameof(this.HasSentPetition));
				this.FriendlyName = FriendlyName;
			});

		}

		private async Task<string> GetFriendlyNameAsync()
		{
			try
			{
				if (this.IsMe)
					return ServiceRef.Localizer[nameof(AppResources.Me)];

				if (this.Part.LegalId == ServiceRef.TagProfile.TrustProviderId && !string.IsNullOrEmpty(ServiceRef.TagProfile.Domain))
					return ServiceRef.TagProfile.Domain;

				ContactInfo Info = await Database.FindFirstIgnoreRest<ContactInfo>(new FilterFieldEqualTo("LegalId", this.Part.LegalId));
				if (Info is not null && !string.IsNullOrEmpty(Info.FriendlyName))
					return Info.FriendlyName;

				if (this.identity is not null)
					return ContactInfo.GetFriendlyName(this.identity);

				if (Info is not null && !string.IsNullOrEmpty(Info.BareJid))
					return Info.BareJid;

				if (this.Signature is not null)
					return this.Signature.BareJid;
			}
			catch (Exception E)
			{
				ServiceRef.LogService.LogException(E);
				//log and use fallback
			}

			return this.LegalId;
		}
		#region Properties
		private LegalIdentity? identity;

		/// <summary>
		/// The wrapped Part object
		/// </summary>
		public Part Part { get; }

		private ClientSignature? signature = null;
		public ClientSignature? Signature
		{
			get => this.signature;
			set
			{
				this.SetProperty(ref this.signature, value);
				this.OnPropertyChanged(nameof(this.HasSigned));
				this.OpenSignatureCommand.NotifyCanExecuteChanged();
			}
		}

		public string LegalId => this.Part.LegalId;


		public string Role => this.Part.Role;

		/// <summary>
		/// The friendly name for the part
		/// Has to be initialized with <see cref="InitializeAsync"/>
		/// </summary>
		public string? FriendlyName
		{
			get => this.friendlyName;
			private set
			{
				this.SetProperty(ref this.friendlyName, value);
			}
		}
		private string? friendlyName = string.Empty;

		public bool HasSentPetition
		{
			get => this.hasSentPetition && !this.HasIdentity;
			private set
			{
				this.SetProperty(ref this.hasSentPetition, value);
			}
		}
		private bool hasSentPetition = false;

		public bool IsMe => this.Part.LegalId == ServiceRef.TagProfile.LegalIdentity?.Id;
		public bool IsThirdParty => !this.IsMe;

		public bool HasIdentity => this.identity is not null;

		public bool HasSigned => this.Signature is not null;

		#endregion


		#region Commands
		[RelayCommand(AllowConcurrentExecutions = false)]
		private async Task OpenIdentityAsync()
		{
			if (this.identity is null)
			{
				try
				{
					await this.PetitionIdentityAsync();
					await ServiceRef.UiService.DisplayAlert(ServiceRef.Localizer[nameof(AppResources.Petition)], ServiceRef.Localizer[nameof(AppResources.PetitionIdentitySent)], ServiceRef.Localizer[nameof(AppResources.Ok)]);
				}
				catch (Exception)
				{
					await ServiceRef.UiService.DisplayAlert(ServiceRef.Localizer[nameof(AppResources.Error)], ServiceRef.Localizer[nameof(AppResources.SomethingWentWrong)], ServiceRef.Localizer[nameof(AppResources.Ok)]);
				}
			}
			else
			{
				await ServiceRef.UiService.GoToAsync(nameof(ViewIdentityPage), new ViewIdentityNavigationArgs(this.identity), Services.UI.BackMethod.Pop);
			}
		}

		[RelayCommand(AllowConcurrentExecutions = false, CanExecute = nameof(HasSigned))]
		private async Task OpenSignatureAsync()
		{
			if (this.Signature is not null)
			{
				await ServiceRef.UiService.GoToAsync(nameof(ClientSignaturePage),
					new ClientSignatureNavigationArgs(this.Signature, this.identity),
					Services.UI.BackMethod.Pop);
			}
		}
		#endregion

		private bool disposed = false;

		#region IDisposable Support
		public void Dispose()
		{
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (this.disposed)
				return;

			if (disposing)
			{
				// Unsubscribe from the event to prevent memory leaks
				ServiceRef.XmppService.PetitionedIdentityResponseReceived -= this.XmppService_OnPetitionedIdentityResponseReceived;
			}

			this.disposed = true;
		}
		#endregion
	}
}
