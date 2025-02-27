using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeuroAccessMaui.Services;
using NeuroAccessMaui.Services.UI.Photos;
using System.Collections.ObjectModel;
using Waher.Networking.XMPP.Contracts;

namespace NeuroAccessMaui.UI.Popups.Image
{


	public partial class ImagesViewModel : BasePopupViewModel
	{
		private readonly PhotosLoader photosLoader;


		public ImagesViewModel()
		{
			this.Photos = [];
			this.photosLoader = new PhotosLoader(this.Photos);
		}
		/// <summary>
		/// Holds the list of photos to display.
		/// </summary>
		public ObservableCollection<Photo> Photos { get; }

		/// <summary>
		/// Gets or sets whether a user can swipe to see the photos.
		/// </summary>
		[ObservableProperty]
		private bool isSwipeEnabled;

		/// <summary>
		/// Loads the attachments photos, if there are any.
		/// </summary>
		/// <param name="attachments">The attachments to load.</param>
		public void LoadPhotos(Attachment[] attachments)
		{
			this.photosLoader.CancelLoadPhotos();
			this.IsSwipeEnabled = false;

			_ = this.photosLoader.LoadPhotos(attachments, SignWith.LatestApprovedIdOrCurrentKeys, () =>
			{
				MainThread.BeginInvokeOnMainThread(() => this.IsSwipeEnabled = this.Photos.Count > 1);
			});
		}

		public override Task OnPop()
		{
			this.photosLoader.CancelLoadPhotos();
			return base.OnPop();
		}
		/// <summary>
		/// Cancels
		/// </summary>
		[RelayCommand]
		private async Task Cancel()
		{
			await ServiceRef.UiService.PopAsync();
		}
	}
}
