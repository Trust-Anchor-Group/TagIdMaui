using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeuroAccessMaui.UI.Controls;
using System.Threading.Tasks;
using Waher.Content.QR.Serialization;

namespace NeuroAccessMaui.UI.Pages.Utility.Images
{
	/// <summary>
	/// The ViewModel for <see cref="ImageCroppingPage"/>.
	/// Handles cropping commands and navigation logic.
	/// </summary>
	public partial class ImageCroppingViewModel : BaseViewModel
	{
		/// <summary>
		/// The navigation arguments passed when the page was opened.
		/// </summary>
		private readonly ImageCroppingNavigationArgs? args;

		/// <summary>
		/// A direct reference to the ImageCropperView control,
		/// so we can call PerformCrop.
		/// </summary>
		[ObservableProperty]
		private ImageCropperView? imageCropperView;

		/// <summary>
		/// Initializes a new instance of the <see cref="ImageCroppingViewModel"/> class.
		/// </summary>
		/// <param name="args">The navigation args containing the source image, TCS, etc.</param>
		public ImageCroppingViewModel(ImageCroppingNavigationArgs? args)
		{
			this.args = args;
		}

		/// <summary>
		/// Called when the page appears.
		/// Assigns the incoming source to the cropper control.
		/// </summary>
		/// <returns>A task representing the asynchronous operation.</returns>
		protected override Task OnAppearing()
		{
			base.OnAppearing();

			if (this.args?.Source is not null && this.ImageCropperView is not null)
			{
				// Assign the incoming source to the cropper control.
				this.ImageCropperView.ImageSource = this.args.Source;
			}

			if (this.args?.OutputResolution != null && this.ImageCropperView is not null)
			{
				this.ImageCropperView.OutputMaxResolution = this.args.OutputResolution.Value;
			}

			if (this.args?.CropMode != null && this.ImageCropperView is not null)
			{
				this.ImageCropperView.CropMode = this.args.CropMode;
			}

			return Task.CompletedTask;
		}

		/// <summary>
		/// Command to perform the actual crop and return the result.
		/// </summary>
		/// <returns>A task representing the asynchronous crop operation.</returns>
		[RelayCommand]
		private async Task CropAsync()
		{
			if (this.ImageCropperView is null || this.args is null)
				return;
				
			//Run the crop operation on a background thread.
			await Task.Run(() => 
			{
				byte[]? croppedResult = this.ImageCropperView.PerformCrop();
				this.args.CompletionSource?.TrySetResult(croppedResult);
			});
			// Then pop navigation.
			await this.GoBack();
		}

		/// <summary>
		/// Command to cancel cropping and return a null result.
		/// </summary>
		/// <returns>A task representing the asynchronous cancel operation.</returns>
		[RelayCommand]
		private async Task CancelAsync()
		{
			this.args?.CompletionSource?.SetResult(null);
			await this.GoBack();
		}

		/// <summary>
		/// Command to rotate the image by 90 degrees.
		/// </summary>
		/// <returns>A task representing the asynchronous rotate operation.</returns>
		[RelayCommand]
		private Task RotateAsync()
		{
			if (this.ImageCropperView is not null)
			{
				// Increment the rotation angle by 90 degrees.
				double newRotation = this.ImageCropperView.RotationAngle + 90;
				// Normalize the rotation angle to stay within 0-359 degrees.
				this.ImageCropperView.RotationAngle = newRotation % 360;
			}
			return Task.CompletedTask;
		}
	}
}
