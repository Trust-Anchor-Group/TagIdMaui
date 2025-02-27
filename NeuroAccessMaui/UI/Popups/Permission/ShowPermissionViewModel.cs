using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls.Shapes;
using NeuroAccessMaui.Services;

namespace NeuroAccessMaui.UI.Popups.Permission
{
	public partial class ShowPermissionViewModel : BasePopupViewModel
	{
		#region Private Properties
		private bool hasBeenToSettings = false;

		#endregion

		#region Public Properties

		/// <summary>
		/// Gets or sets the title of the popup
		/// </summary>
		[ObservableProperty]
		private string title = string.Empty;

		/// <summary>
		/// Gets or sets the primary description of the popup
		/// </summary>
		[ObservableProperty]
		private string description = string.Empty;

		/// <summary>
		/// Gets or sets the secondary description of the popup
		/// </summary>
		[ObservableProperty]
		private string descriptionSecondary = string.Empty;

		[ObservableProperty]
		private Geometry? iconGeometry = Geometries.InfoCirclePath;

		/// <summary>
		/// Gets the size of the background for Camera Icon 
		/// </summary>
		public double CameraIconBackgroundSize => 120.0;

		/// <summary>
		/// Gets the Readius of the background for Camera Icon
		/// </summary>
		public double CameraIconBackgroundCornerRadius => this.CameraIconBackgroundSize / 2;

		/// <summary>
		/// Gets the size of the Camera Icon
		/// </summary>
		public double CameraIconSize => 60.0;

		#endregion

		#region Constructor

		public ShowPermissionViewModel(string? title, string? description, string? descriptionSecondary, Geometry? iconGeometry = null)
		{
			this.Title = title ?? string.Empty;
			this.Description = description ?? string.Empty;
			this.DescriptionSecondary = descriptionSecondary ?? string.Empty;
			this.IconGeometry = iconGeometry ?? Geometries.InfoCirclePath;
		}

		#endregion


		#region Overrides

		protected override async Task OnInitialize()
		{
			await base.OnInitialize();

			App.AppActivated += this.App_OnActivated;

		}

		protected override Task OnDispose()
		{
			App.AppActivated -= this.App_OnActivated;

			return base.OnDispose();
		}

		protected async void App_OnActivated(object? sender, EventArgs e)
		{
			try
			{
				if (this.hasBeenToSettings)
				{
					await this.Close();
				}
			}
			catch (Exception)
			{
				//Ignore no need to handle this exception, the user needs to close popup manually
			}
		}
		 #endregion

		#region Commands

		[RelayCommand]
		private async Task Close()
		{
			await ServiceRef.UiService.PopAsync();
		}

		[RelayCommand]
		private void GoToSettings()
		{
			this.hasBeenToSettings = true;
			AppInfo.Current.ShowSettingsUI();
		}
		#endregion
	}
}
