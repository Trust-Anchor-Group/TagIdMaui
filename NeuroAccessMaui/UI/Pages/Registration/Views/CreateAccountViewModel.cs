using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeuroAccessMaui.Extensions;
using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services;
using NeuroAccessMaui.Services.Data;
using NeuroAccessMaui.Services.Tag;
using Waher.Networking.XMPP;
using Waher.Networking.XMPP.Contracts;
using Waher.Networking.XMPP.Contracts.EventArguments;

namespace NeuroAccessMaui.UI.Pages.Registration.Views
{
	public partial class CreateAccountViewModel : BaseRegistrationViewModel
	{
		public CreateAccountViewModel()
			: base(RegistrationStep.CreateAccount)
		{
		}

		/// <inheritdoc />
		protected override async Task OnInitialize()
		{
			await base.OnInitialize();

			ServiceRef.XmppService.ConnectionStateChanged += this.XmppService_ConnectionStateChanged;
			ServiceRef.XmppService.LegalIdentityChanged += this.XmppContracts_LegalIdentityChanged;
		}

		/// <inheritdoc />
		protected override async Task OnDispose()
		{
			ServiceRef.XmppService.ConnectionStateChanged -= this.XmppService_ConnectionStateChanged;
			ServiceRef.XmppService.LegalIdentityChanged -= this.XmppContracts_LegalIdentityChanged;

			await base.OnDispose();
		}

		/// <inheritdoc />
		public override async Task DoAssignProperties()
		{
			await base.DoAssignProperties();

			if (this.CreateIdentityCommand.CanExecute(null))
				await this.CreateIdentityCommand.ExecuteAsync(null);
			if (ServiceRef.TagProfile.Step != RegistrationStep.Complete)
				await this.CheckAndHandleIdentityApplicationAsync();
		}

		private Task XmppService_ConnectionStateChanged(object _, XmppState NewState)
		{
			if (NewState == XmppState.Connected)
			{
				MainThread.BeginInvokeOnMainThread(async () =>
				{
					await this.DoAssignProperties();
				});
			}

			return Task.CompletedTask;
		}

		private Task XmppContracts_LegalIdentityChanged(object _, LegalIdentityEventArgs e)
		{
			MainThread.BeginInvokeOnMainThread(async () =>
			{
				this.OnPropertyChanged(nameof(IsLegalIdentityCreated));
				this.CreateIdentityCommand.NotifyCanExecuteChanged();
				await this.DoAssignProperties();
			});

			return Task.CompletedTask;
		}

		protected override void OnPropertyChanged(PropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);

			switch (e.PropertyName)
			{
				case nameof(this.IsBusy):
					this.CreateIdentityCommand.NotifyCanExecuteChanged();
					break;
			}
		}

		/// <summary>
		/// If App is connected to the XMPP network.
		/// </summary>
		public static bool IsXmppConnected => ServiceRef.XmppService.State == XmppState.Connected;

		/// <summary>
		/// If App has an XMPP account defined.
		/// </summary>
		public static bool IsAccountCreated => !string.IsNullOrEmpty(ServiceRef.TagProfile.Account);

		/// <summary>
		/// If Legal ID has been created.
		/// </summary>
		public static bool IsLegalIdentityCreated
		{
			get
			{
				return ServiceRef.TagProfile.LegalIdentity is not null &&
					(ServiceRef.TagProfile.LegalIdentity.State == IdentityState.Approved ||
					ServiceRef.TagProfile.LegalIdentity.State == IdentityState.Created);
			}
		}


		/// <summary>
		/// If we can create an identity.
		/// </summary>
		public bool CanCreateIdentity => IsAccountCreated && !IsLegalIdentityCreated && IsXmppConnected;


		/// <summary>
		/// Try to create an identity.
		/// </summary>
		[RelayCommand(CanExecute = nameof(CanCreateIdentity))]
		private async Task CreateIdentity()
		{
			try
			{
				RegisterIdentityModel IdentityModel = CreateRegisterModel();
				LegalIdentityAttachment[] Photos = []; // Photos are left empty

				(bool Succeeded, LegalIdentity? AddedIdentity) = await ServiceRef.NetworkService.TryRequest(() =>
					ServiceRef.XmppService.AddLegalIdentity(IdentityModel, true, Photos));

				if (Succeeded && AddedIdentity is not null)
					await ServiceRef.TagProfile.SetLegalIdentity(AddedIdentity, true);
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
				await ServiceRef.UiService.DisplayException(ex);
			}
		}

		[RelayCommand]
		private static async Task ValidateIdentity()
		{
			await Task.CompletedTask;
		}


		private async Task CheckAndHandleIdentityApplicationAsync()
		{
			this.IsBusy = true;
			if (ServiceRef.TagProfile.LegalIdentity is LegalIdentity LegalIdentity)
			{
				if (LegalIdentity.State == IdentityState.Approved)
				{
					if (Shell.Current.CurrentState.Location.OriginalString == Constants.Pages.RegistrationPage)
						GoToRegistrationStep(RegistrationStep.DefinePassword);
				}
				else if (LegalIdentity.IsDiscarded())
				{
					await ServiceRef.TagProfile.ClearLegalIdentity();
					// TODO: Show error message
					GoToRegistrationStep(RegistrationStep.ValidatePhone);
				}
			}
			this.IsBusy = true;
		}

		private static RegisterIdentityModel CreateRegisterModel()
		{
			RegisterIdentityModel IdentityModel = new();
			string s;

			if (!string.IsNullOrWhiteSpace(s = ServiceRef.TagProfile?.PhoneNumber?.Trim() ?? string.Empty))
			{
				if (string.IsNullOrWhiteSpace(s) && (ServiceRef.TagProfile?.LegalIdentity is LegalIdentity LegalIdentity))
					s = LegalIdentity[Constants.XmppProperties.Phone];

				IdentityModel.PhoneNr = s;
			}

			if (!string.IsNullOrWhiteSpace(s = ServiceRef.TagProfile?.EMail?.Trim() ?? string.Empty))
			{
				if (string.IsNullOrWhiteSpace(s) && (ServiceRef.TagProfile?.LegalIdentity is LegalIdentity LegalIdentity))
					s = LegalIdentity[Constants.XmppProperties.EMail];

				IdentityModel.EMail = s;
			}

			if (!string.IsNullOrWhiteSpace(s = ServiceRef.TagProfile?.SelectedCountry?.Trim() ?? string.Empty))
			{
				if (string.IsNullOrWhiteSpace(s) && (ServiceRef.TagProfile?.LegalIdentity is LegalIdentity LegalIdentity))
					s = LegalIdentity[Constants.XmppProperties.Country];

				IdentityModel.CountryCode = s;
			}

			return IdentityModel;
		}
	}
}
