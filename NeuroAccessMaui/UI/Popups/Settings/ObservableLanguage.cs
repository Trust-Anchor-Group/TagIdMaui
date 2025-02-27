using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using NeuroAccessMaui.Services.Localization;

namespace NeuroAccessMaui.UI.Popups.Settings
{
	public partial class ObservableLanguage : ObservableObject
	{
		public LanguageInfo Language { get; }

		[ObservableProperty]
		private bool isSelected;

		public ObservableLanguage(LanguageInfo language, bool isSelected = false)
		{
			this.Language = language;
			this.IsSelected = isSelected;
		}


	}
}
