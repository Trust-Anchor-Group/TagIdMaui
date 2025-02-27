using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NeuroAccessMaui.Services;
using NeuroAccessMaui.Services.Localization;

namespace NeuroAccessMaui.UI.Popups.Settings
{
	public partial class SelectLanguagePopupViewModel : BasePopupViewModel
	{
		// Expose the supported languages.
		public ObservableCollection<ObservableLanguage> Languages { get; }

		[ObservableProperty]
		private string? selectedLanguageName;

		public SelectLanguagePopupViewModel()
		{
			this.SelectedLanguageName = App.SelectedLanguage.Name;

			this.Languages = new ObservableCollection<ObservableLanguage>(
				App.SupportedLanguages.Select(l =>
					new ObservableLanguage(l, l.Name == this.SelectedLanguageName))
			);

		}


		protected override Task OnAppearing()
		{
			if(this.SelectedLanguageName is not null)
				WeakReferenceMessenger.Default.Send(new ScrollToLanguageMessage(this.SelectedLanguageName));

			return base.OnAppearing();
		}

		[RelayCommand(AllowConcurrentExecutions = false)]
		public async Task SelectLanguageAsync(string languageName)
		{
			// Find the language that matches the selection.
			LanguageInfo? SelectedLanguage = this.Languages.FirstOrDefault(l => l.Language.Name == languageName)?.Language;
			if (SelectedLanguage is null)
				return;

			// Update selected language property.
			this.SelectedLanguageName = SelectedLanguage.Name;

			// Update the language if a new language is selected.
			if (SelectedLanguage.Name != CultureInfo.CurrentCulture.Name)
			{
				Preferences.Set("user_selected_language", SelectedLanguage.TwoLetterISOLanguageName);
				LocalizationManager.Current.CurrentCulture = SelectedLanguage;
			}

			// Dismiss the popup via the UI service.
			await ServiceRef.UiService.PopAsync();
		}
	}
}
