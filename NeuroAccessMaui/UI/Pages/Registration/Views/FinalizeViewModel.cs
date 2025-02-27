using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services;
using NeuroAccessMaui.Services.Localization;
using NeuroAccessMaui.Services.Tag;
using NeuroAccessMaui.UI.Popups.Info;
using Waher.Content;

namespace NeuroAccessMaui.UI.Pages.Registration.Views
{
    public partial class FinalizeViewModel : BaseRegistrationViewModel
	{
		public FinalizeViewModel()
			: base(RegistrationStep.Finalize)
		{
		}

		protected override async Task OnInitialize()
		{
			await base.OnInitialize();

			LocalizationManager.Current.PropertyChanged += this.Localization_Changed;
		}

		/// <inheritdoc />
		protected override async Task OnDispose()
		{
			LocalizationManager.Current.PropertyChanged -= this.Localization_Changed;

			await base.OnDispose();
		}


		public override async Task DoAssignProperties()
		{
			await base.DoAssignProperties();
			await this.SetDomainName();
			this.NetworkID = $"{ServiceRef.TagProfile.Account}@{ServiceRef.TagProfile.Domain}";
		}

		private async void Localization_Changed(object? Sender, PropertyChangedEventArgs e)
		{
			await this.SetDomainName();
		}

		private async Task SetDomainName()
		{
			this.DomainName = ServiceRef.TagProfile.Domain!;

			try
			{
				Uri DomainInfo = new("https://" + this.DomainName + "/Agent/Account/DomainInfo");
				string AcceptLanguage = App.SelectedLanguage.TwoLetterISOLanguageName;

				if (AcceptLanguage != "en")
					AcceptLanguage += ";q=1,en;q=0.9";

				ContentResponse Result = await InternetContent.GetAsync(DomainInfo,
					new KeyValuePair<string, string>("Accept", "application/json"),
					new KeyValuePair<string, string>("Accept-Language", AcceptLanguage),
					new KeyValuePair<string, string>("Accept-Encoding", "0"));

				if (Result.HasError)
				{
					ServiceRef.LogService.LogException(Result.Error);
					return;
				}

				if (Result.Decoded is Dictionary<string, object> Response)
				{
					if (Response.TryGetValue("humanReadableName", out object? Obj) && Obj is string LocalizedName)
						this.LocalizedDomainName = LocalizedName;

					if (Response.TryGetValue("humanReadableDescription", out Obj) && Obj is string LocalizedDescription)
						this.LocalizedDomainDescription = LocalizedDescription;
				}
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
			}
		}


		/// <summary>
		/// Gets the size of the background for the checkmark.
		/// </summary>
		public double CheckmarkBackgroundSize => 120.0;

		/// <summary>
		/// Gets the size of the background for the checkmark.
		/// </summary>
		public double CheckmarkBackgroundCornerRadius => this.CheckmarkBackgroundSize / 2;
		/// <summary>
		/// Gets the size of the icon for the checkmark.
		/// </summary>
		public double CheckmarkIconSize => 60.0;

		/// <summary>
		/// The network ID of the account.
		/// </summary>
		[ObservableProperty]
		private string? networkID;

        [RelayCommand]
		private void Continue()
		{
			GoToRegistrationStep(RegistrationStep.Complete);
		}

		/// <summary>
		/// Copies Item to clipboard
		/// </summary>
		[RelayCommand]
		private async Task Copy(object Item)
		{
			try
			{
				this.SetIsBusy(true);

				if (Item is string Label)
				{
					await Clipboard.SetTextAsync(Label);
					await ServiceRef.UiService.DisplayAlert(
						ServiceRef.Localizer[nameof(AppResources.SuccessTitle)],
						ServiceRef.Localizer[nameof(AppResources.TagValueCopiedToClipboard)]);
				}
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


		[ObservableProperty]
		private string domainName = string.Empty;

		[ObservableProperty]
		private string localizedDomainName = string.Empty;

		[ObservableProperty]
		private string localizedDomainDescription = string.Empty;

		[RelayCommand]
		private async Task ServiceProviderInfo()
		{
			string title = this.LocalizedDomainName;
			string message = this.LocalizedDomainDescription;
			ShowInfoPopup infoPage = new(title, message);
			await ServiceRef.UiService.PushAsync(infoPage);
		}
    }
}
