using NeuroAccessMaui.Services;
using NeuroAccessMaui.UI.Pages;
using Waher.Events;

namespace NeuroAccessMaui.UI.Popups
{
	/// <summary>
	/// Creates a base popup with view model lifecycle support.
	/// </summary>
	public partial class BasePopup
	{
		/// <summary>
		/// The requested width of the popup view.
		/// </summary>
		public virtual double ViewWidthRequest => (DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density) * (7.0 / 8.0);

		/// <summary>
		/// The maximum height of the popup view.
		/// </summary>
		public virtual double MaximumViewHeightRequest => (DeviceDisplay.MainDisplayInfo.Height / DeviceDisplay.MainDisplayInfo.Density) * (3.0 / 4.0);

		/// <summary>
		/// If true, the background will be a blurred screenshot of the current page.
		/// If false the background will be fully transparent and can be modified by derived classes.
		/// </summary>
		protected bool useDefaultBackground;


		/// <summary>
		/// Returns the current BindingContext as a BasePopupViewModel.
		/// Returns null if failed.
		/// </summary>
		public BasePopupViewModel? ViewModel
		{
			get => this.BindingContext as BasePopupViewModel;
			set => this.BindingContext = value;
		}

		protected BasePopup(bool useDefaultBackground = true)
		{
			this.useDefaultBackground = useDefaultBackground;
			this.InitializeComponent();
			this.BackgroundColor = AppColors.PrimaryBackground;

		}

		/// <summary>
		/// Called when the Page's <see cref="Element.Parent"/> property has changed.
		/// </summary>
		protected sealed override async void OnParentSet()
		{
			try
			{
				base.OnParentSet();

				if (this.BindingContext is not BaseViewModel Vm)
					return;

				if (this.Parent is null)
				{
					if (this.ViewModel is ILifeCycleView LifeCycleView)
						await LifeCycleView.DoDispose();
				}
				else
				{
					if (this.ViewModel is ILifeCycleView LifeCycleView)
						await LifeCycleView.DoInitialize();
				}
			}
			catch (Exception Ex)
			{
				Log.Exception(Ex);
			}
		}


		/// <summary>
		/// Called when the popup appears.
		/// </summary>
		protected override async void OnAppearing()
		{
			try
			{
				base.OnAppearing();

				// Call the view model's appearing logic.
				if (this.BindingContext is BaseViewModel Vm)
				{
					await Vm.DoAppearing();
				}

				if (this.useDefaultBackground)
				{
					await this.LoadScreenshotAsync();
				}
			}
			catch (Exception Ex)
			{
				ServiceRef.LogService.LogException(Ex);
			}
		}

		/// <summary>
		/// Called when the popup is disappearing.
		/// </summary>
		protected override async void OnDisappearing()
		{
			try
			{
				// Call the view model's disappearing logic.
				if (this.BindingContext is BasePopupViewModel Vm)
				{
					await Vm.DoDisappearing();
				}

				base.OnDisappearing();
			}
			catch (Exception Ex)
			{
				ServiceRef.LogService.LogException(Ex);
			}
		}

		/// <summary>
		/// A shortcut for <see cref="Services.UI.IUiService.PopAsync"/>.
		/// </summary>
		protected static async Task PopAsync()
		{
			await ServiceRef.UiService.PopAsync();
		}

		/// <summary>
		/// Called when the back button on the client is pressed.
		/// </summary>
		/// <remarks>All derived methods should call base.OnBackButtonPressed()</remarks>
		/// <returns>True, indicating the back navigation was handled manually.</returns>
		protected override bool OnBackButtonPressed()
		{
			ServiceRef.UiService.PopAsync();
			return true;
		}

		/// <summary>
		/// Called when the background is pressed.
		/// </summary>
		/// <remarks>All derived methods should call base.OnBackgroundClicked()</remarks>
		/// <returns>True, indicating the background click was handled manually.</returns>
		protected override bool OnBackgroundClicked()
		{
			ServiceRef.UiService.PopAsync();
			return true;
		}

		/// <summary>
		/// Sets the popup background to a blurred screenshot of the current page.
		/// </summary>
		private async Task LoadScreenshotAsync()
		{
			this.BackgroundImageSource = await ServiceRef.UiService.TakeBlurredScreenshotAsync();
		}
	}
}
