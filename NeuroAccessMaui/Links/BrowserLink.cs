using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services;
using Waher.Runtime.Inventory;

namespace NeuroAccessMaui.Links
{
	/// <summary>
	/// Browser link opener.
	/// </summary>
	public class BrowserLink : ILinkOpener
	{
		/// <summary>
		/// Browser link opener.
		/// </summary>
		public BrowserLink()
		{
		}

		/// <summary>
		/// How well the link opener supports a given link
		/// </summary>
		/// <param name="Link">Link that will be opened.</param>
		/// <returns>Support grade of opener for the given link.</returns>
		public Grade Supports(Uri Link)
		{
			if (Link.Scheme == "http" || Link.Scheme == "https")
				return Grade.Ok;
			else
				return Grade.NotAtAll;
		}

		/// <inheritdoc/>
		public async Task<bool> TryOpenLink(Uri Link, bool ShowErrorIfUnable)
		{
			bool canOpen = false;

			await MainThread.InvokeOnMainThreadAsync(async () =>
			{
				canOpen = await Browser.Default.OpenAsync(Link);
			});

			if (canOpen)
				return true;
			else
			{
				if (ShowErrorIfUnable)
				{
					await ServiceRef.UiService.DisplayAlert(
						ServiceRef.Localizer[nameof(AppResources.ErrorTitle)],
						ServiceRef.Localizer[nameof(AppResources.QrCodeNotUnderstood)] +
						Environment.NewLine + Environment.NewLine + Link.OriginalString);
				}

				return false;
			}
		}
	}
}
