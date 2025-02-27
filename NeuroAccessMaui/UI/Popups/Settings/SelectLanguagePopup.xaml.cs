using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Devices;
using NeuroAccessMaui.Services.Localization;
using NeuroAccessMaui.UI.Popups;
using NeuroAccessMaui.UI.Popups.Settings;

namespace NeuroAccessMaui.UI.Popups.Settings
{
	public partial class SelectLanguagePopup : BasePopup
	{

		public SelectLanguagePopup()
		{
			this.InitializeComponent();

			// Set the BindingContext to the dedicated view model.
			this.BindingContext = new SelectLanguagePopupViewModel();

			WeakReferenceMessenger.Default.Register<ScrollToLanguageMessage>(this, (r, m) =>
			{
				// Find the visual element corresponding to the language name.
				foreach (object Item in this.LanguagesContainer)
				{
					if (Item is not VisualElement { BindingContext: LanguageInfo Lang } Element)
						continue;
					if (Lang.Name != m.Value)
						continue;

					// Scroll to the element.
					MainThread.BeginInvokeOnMainThread(async void () =>
					{
						try
						{
							Console.WriteLine("TESTETESTT");
							await this.InnerScrollView.ScrollToAsync(Element, ScrollToPosition.MakeVisible, true);
						}
						catch (Exception e)
						{
							return; // Ignore, not muy importante.
						}
					});
					break;
				}
			});
		}

		protected override void OnDisappearing()
		{
			base.OnDisappearing();
			// Unregister the message handler to avoid multiple registrations.
			WeakReferenceMessenger.Default.Unregister<ScrollToLanguageMessage>(this);
		}
	}
}
