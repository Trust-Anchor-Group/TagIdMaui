using NeuroAccessMaui.Services;
using NeuroAccessMaui.Services.UI;
using System;

namespace NeuroAccessMaui.UI.Pages.Utility.Images
{
	public partial class ImageCroppingPage
	{
		/// <summary>
		/// Initializes a new instance of <see cref="ImageCroppingPage"/>.
		/// </summary>
		public ImageCroppingPage()
		{
			this.InitializeComponent();
			// 1. Retrieve the nav args
			ImageCroppingNavigationArgs? navArgs = ServiceRef.UiService.PopLatestArgs<ImageCroppingNavigationArgs>();
			// 2. Create the ViewModel
			ImageCroppingViewModel viewModel = new ImageCroppingViewModel(navArgs);
			// 3. Assign the view model as the binding context
			this.BindingContext = viewModel;
			// 4. Set the ViewModel's reference to the ImageCropperView
			viewModel.ImageCropperView = this.ImageCropperView;
		}
	}
}
