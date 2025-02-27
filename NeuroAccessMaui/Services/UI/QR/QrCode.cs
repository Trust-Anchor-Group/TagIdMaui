using CommunityToolkit.Maui.Views;
using NeuroAccessMaui.Extensions;
using NeuroAccessMaui.Links;
using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.UI.Popups.Info;
using NeuroAccessMaui.UI.Pages.Main.QR;
using NeuroAccessMaui.UI.Pages.Main.Settings;
using SkiaSharp;
using Waher.Content.QR;
using Waher.Content.QR.Encoding;
using Waher.Runtime.Inventory;

namespace NeuroAccessMaui.Services.UI.QR
{
	/// <summary>
	/// Helper class to perform scanning of QR Codes by displaying the UI and handling async results.
	/// </summary>
	public static class QrCode
	{
		private static readonly QrEncoder encoder = new();

		/// <summary>
		/// Gets the schemas that have been unlocked in the app.
		/// </summary>
		/// <returns>Array of schemas the user can scan.</returns>
		public static string[] GetAllowedSchemas()
		{
			List<string> AllowedSchemas = [Constants.UriSchemes.TagSign];

			if (ServiceRef.TagProfile.LegalIdentity?.HasApprovedPersonalInformation() ?? false)
			{
				AllowedSchemas.Add(Constants.UriSchemes.IotId);
				AllowedSchemas.Add(Constants.UriSchemes.IotSc);
				AllowedSchemas.Add(Constants.UriSchemes.IotDisco);

				// TODO:
				// AllowedSchemas.Add(Constants.UriSchemes.IotDisco);
				// AllowedSchemas.Add(Constants.UriSchemes.EDaler);
				// AllowedSchemas.Add(Constants.UriSchemes.NeuroFeature);
				// AllowedSchemas.Add(Constants.UriSchemes.Xmpp);
			}

			return [.. AllowedSchemas];
		}

		/// <summary>
		/// Scans a QR Code, and depending on the actual result, takes different actions.
		/// This typically means navigating to an appropriate page.
		/// </summary>
		public static Task ScanQrCodeAndHandleResult()
		{
			return ScanQrCodeAndHandleResult(nameof(AppResources.QrScanCode), GetAllowedSchemas());
		}

		/// <summary>
		/// Scans a QR Code, and depending on the actual result, takes different actions.
		/// This typically means navigating to an appropriate page.
		/// </summary>
		/// <param name="QrTitle">Title to present</param>
		/// <param name="AllowedSchemas">Schemas permitted.</param>
		public static async Task ScanQrCodeAndHandleResult(string QrTitle, string[] AllowedSchemas)
		{
			string? Url = await ScanQrCode(QrTitle, AllowedSchemas);
			if (string.IsNullOrWhiteSpace(Url))
				return;

			await OpenUrl(Url);
		}

		/// <summary>
		/// Scans a QR Code, and depending on the actual result, takes different actions.
		/// This typically means navigating to an appropriate page.
		/// </summary>
		/// <param name="Url">URL to open.</param>
		/// <returns>If URL was handled.</returns>
		public static Task<bool> OpenUrl(string Url)
		{
			return OpenUrl(Url, true);
		}

		/// <summary>
		/// Scans a QR Code, and depending on the actual result, takes different actions.
		/// This typically means navigating to an appropriate page.
		/// </summary>
		/// <param name="Url">URL to open.</param>
		/// <param name="ShowErrorIfUnable">If an error message should be displayed, in case the URI could not be opened.</param>
		/// <returns>If URL was handled.</returns>
		public static async Task<bool> OpenUrl(string Url, bool ShowErrorIfUnable)
		{
			try
			{
				if (!System.Uri.TryCreate(Url, UriKind.Absolute, out Uri? Uri))
				{
					if (ShowErrorIfUnable)
					{
						await ServiceRef.UiService.DisplayAlert(
							ServiceRef.Localizer[nameof(AppResources.ErrorTitle)],
							ServiceRef.Localizer[nameof(AppResources.CodeNotRecognized)]);
					}

					return false;
				}

				ILinkOpener Opener = Types.FindBest<ILinkOpener, Uri>(Uri);

				if (Opener is null)
				{
					if (ShowErrorIfUnable)
					{
						await ServiceRef.UiService.DisplayAlert(
							ServiceRef.Localizer[nameof(AppResources.ErrorTitle)],
							ServiceRef.Localizer[nameof(AppResources.CodeNotRecognized)]);
					}

					return false;
				}

				if (!await Opener.TryOpenLink(Uri, false))
				{
					if (ShowErrorIfUnable)
					{
						await ServiceRef.UiService.DisplayAlert(
							ServiceRef.Localizer[nameof(AppResources.ErrorTitle)],
							ServiceRef.Localizer[nameof(AppResources.UnableToOpenLink)] + " " + Uri.ToString());
					}

					return false;
				}

				return true;
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);

				if (ShowErrorIfUnable)
					await ServiceRef.UiService.DisplayException(ex);

				return false;
			}
		}

		/// <summary>
		/// Navigates to the Scan QR Code Page, waits for scan to complete, and returns the result.
		/// This is seemingly simple, but performs several operations, namely:
		/// <list type="bullet">
		/// <item>
		/// <description>Display the <see cref="ScanQrCodePage"/></description>
		/// </item>
		/// <item>
		/// <description>Wait for the user to scan a QR code or enter it manually, or cancel.</description>
		/// </item>
		/// <item>
		/// <description>Navigate back to the calling page.</description>
		/// </item>
		/// </list>
		/// </summary>
		/// <param name="QrTitle">The QR page title</param>
		/// <param name="AllowedSchemas">Permitted schemas</param>
		/// <returns>Decoded string</returns>
		public static async Task<string?> ScanQrCode(string? QrTitle, string[] AllowedSchemas)
		{
			bool Permitted = await ServiceRef.PermissionService.CheckCameraPermissionAsync();

			if (!Permitted)
				return null;

			ScanQrCodeNavigationArgs NavigationArgs = new(QrTitle, AllowedSchemas);

			await ServiceRef.UiService.GoToAsync(nameof(ScanQrCodePage), NavigationArgs, BackMethod.Pop);

			if (NavigationArgs.QrCodeScanned is null)
				return null;
			else
				return await NavigationArgs.QrCodeScanned.Task;
		}

		/// <summary>
		/// Generates a QR Code png image with the specified width and height.
		/// </summary>
		/// <param name="Text">The QR Code</param>
		/// <param name="Width">Required image width.</param>
		/// <param name="Height">Required image height.</param>
		/// <returns>Binary encoding of PNG</returns>
		public static byte[] GeneratePng(string Text, int Width, int Height)
		{
			return Generate(Text, Width, Height, SKEncodedImageFormat.Png);
		}

		/// <summary>
		/// Generates a QR Code jpeg image with the specified width and height.
		/// </summary>
		/// <param name="Text">The QR Code</param>
		/// <param name="Width">Required image width.</param>
		/// <param name="Height">Required image height.</param>
		/// <returns>Binary encoding of JPG</returns>
		public static byte[] GenerateJpg(string Text, int Width, int Height)
		{
			return Generate(Text, Width, Height, SKEncodedImageFormat.Jpeg);
		}

		private static byte[] Generate(string Text, int Width, int Height, SKEncodedImageFormat Format)
		{
			QrMatrix M;
			byte[] Rgba;

			int i = Text.IndexOf(':');
			string UriScheme = i < 0 ? string.Empty : Text[..i].ToLowerInvariant();
			bool LightTheme = SettingsViewModel.CurrentDisplayMode != AppTheme.Dark;

			switch (UriScheme)
			{
				case Constants.UriSchemes.TagSign:
					M = encoder.GenerateMatrix(CorrectionLevel.H, Text);
					Rgba = M.ToRGBA(Width, Height, (LightTheme ? signatureCodeLight : signatureCodeDark).ColorFunction, true);
					break;

				case Constants.UriSchemes.IotId:
					M = encoder.GenerateMatrix(CorrectionLevel.H, Text);
					Rgba = M.ToRGBA(Width, Height, (LightTheme ? userCodeLight : userCodeDark).ColorFunction, true);
					break;

				case Constants.UriSchemes.IotSc:
					M = encoder.GenerateMatrix(CorrectionLevel.H, Text);
					Rgba = M.ToRGBA(Width, Height, (LightTheme ? contractCodeLight : contractCodeDark).ColorFunction, true);
					break;

				case Constants.UriSchemes.IotDisco:
					M = encoder.GenerateMatrix(CorrectionLevel.H, Text);
					Rgba = M.ToRGBA(Width, Height, (LightTheme ? thingsCodeLight : thingsCodeDark).ColorFunction, true);
					break;

				case Constants.UriSchemes.EDaler:
					M = encoder.GenerateMatrix(CorrectionLevel.H, Text);
					Rgba = M.ToRGBA(Width, Height, (LightTheme ? eDalerCodeLight : eDalerCodeDark).ColorFunction, true);
					break;

				case Constants.UriSchemes.NeuroFeature:
					M = encoder.GenerateMatrix(CorrectionLevel.H, Text);
					Rgba = M.ToRGBA(Width, Height, (LightTheme ? tokenCodeLight : tokenCodeDark).ColorFunction, true);
					break;

				case Constants.UriSchemes.Onboarding:
					M = encoder.GenerateMatrix(CorrectionLevel.H, Text);
					Rgba = M.ToRGBA(Width, Height, (LightTheme ? onboardingCodeLight : onboardingCodeDark).ColorFunction, true);
					break;

				case Constants.UriSchemes.Aes256:
					M = encoder.GenerateMatrix(CorrectionLevel.H, Text);
					Rgba = M.ToRGBA(Width, Height, (LightTheme ? aes256CodeLight : aes256CodeDark).ColorFunction, true);
					break;

				default:
					M = encoder.GenerateMatrix(CorrectionLevel.L, Text);
					Rgba = M.ToRGBA(Width, Height);
					break;
			}

			using SKData Unencoded = SKData.Create(new MemoryStream(Rgba));
			using SKImage Bitmap = SKImage.FromPixels(new SKImageInfo(Width, Height, SKColorType.Rgba8888), Unencoded, Width * 4);
			using SKData Encoded = Bitmap.Encode(Format, 100);

			return Encoded.ToArray();
		}

		private const string userPath = "M128 21.3335C69.1202 21.3335 21.3335 69.1202 21.3335 128C21.3335 186.88 69.1202 234.667 128 234.667C186.88 234.667 234.667 186.88 234.667 128C234.667 69.1202 186.88 21.3335 128 21.3335ZM128 53.3335C145.707 53.3335 160 67.6268 160 85.3335C160 103.04 145.707 117.333 128 117.333C110.293 117.333 96.0002 103.04 96.0002 85.3335C96.0002 67.6268 110.293 53.3335 128 53.3335ZM128 204.8C101.333 204.8 77.7602 191.147 64.0002 170.453C64.3202 149.227 106.667 137.6 128 137.6C149.227 137.6 191.68 149.227 192 170.453C178.24 191.147 154.667 204.8 128 204.8Z";
		private const string onboardingPath = "M181.523 136.57H76.6327C68.3914 136.57 61.6484 143.313 61.6484 151.554V181.523C61.6484 189.764 68.3914 196.507 76.6327 196.507H181.523C189.764 196.507 196.507 189.764 196.507 181.523V151.554C196.507 143.313 189.764 136.57 181.523 136.57ZM91.617 181.523C83.3756 181.523 76.6327 174.78 76.6327 166.538C76.6327 158.297 83.3756 151.554 91.617 151.554C99.8583 151.554 106.601 158.297 106.601 166.538C106.601 174.78 99.8583 181.523 91.617 181.523ZM181.523 61.6484H76.6327C68.3914 61.6484 61.6484 68.3914 61.6484 76.6327V106.601C61.6484 114.843 68.3914 121.586 76.6327 121.586H181.523C189.764 121.586 196.507 114.843 196.507 106.601V76.6327C196.507 68.3914 189.764 61.6484 181.523 61.6484ZM91.617 106.601C83.3756 106.601 76.6327 99.8583 76.6327 91.617C76.6327 83.3756 83.3756 76.6327 91.617 76.6327C99.8583 76.6327 106.601 83.3756 106.601 91.617C106.601 99.8583 99.8583 106.601 91.617 106.601Z";
		private const string eDalerPath = "M24.6263 16.4401C23.3663 16.4401 22.1063 16.4401 20.8463 16.4401C20.7206 16.44 20.5948 16.4401 20.4395 16.4401V51.4389H20.9223C24.8438 51.4389 28.7656 51.4428 32.6871 51.4358C33.5212 51.4344 34.3558 51.4085 35.189 51.3681C37.4651 51.2581 39.6652 50.7828 41.772 49.9128C45.1668 48.511 47.8677 46.2772 49.6567 43.0516C52.1445 38.5658 52.5325 33.7874 51.288 28.8799C50.035 23.9385 47.0312 20.3848 42.3972 18.2388C39.5603 16.9252 36.548 16.4476 33.4461 16.4435C31.2191 16.4405 28.9924 16.44 26.7654 16.44C26.0525 16.44 25.3391 16.44 24.6263 16.4401ZM29.0057 23.7511C29.0906 23.7458 29.1635 23.7367 29.2363 23.7376C29.766 23.7444 30.2963 23.7432 30.8267 23.742C32.0051 23.7392 33.1835 23.7365 34.3556 23.8212C38.7074 24.1359 42.1845 26.6075 43.0292 31.4776C43.4386 33.8372 43.3465 36.1684 42.4712 38.415C41.2914 41.4435 38.9755 43.1121 35.8589 43.7698C34.4877 44.0592 33.0964 44.0526 31.7052 44.0495C30.8097 44.0475 29.914 44.0495 28.989 44.0495V37.1036H40.4938V30.4922H29.0057V23.7514V23.7511Z";
		private const string aes256Path = "M44.2122 28.1067H42.5456V24.7733C42.5456 20.1733 38.8122 16.44 34.2122 16.44C29.6122 16.44 25.8789 20.1733 25.8789 24.7733V28.1067H24.2122C22.3789 28.1067 20.8789 29.6067 20.8789 31.44V48.1067C20.8789 49.94 22.3789 51.44 24.2122 51.44H44.2122C46.0456 51.44 47.5456 49.94 47.5456 48.1067V31.44C47.5456 29.6067 46.0456 28.1067 44.2122 28.1067ZM34.2122 43.1067C32.3789 43.1067 30.8789 41.6067 30.8789 39.7733C30.8789 37.94 32.3789 36.44 34.2122 36.44C36.0456 36.44 37.5456 37.94 37.5456 39.7733C37.5456 41.6067 36.0456 43.1067 34.2122 43.1067ZM29.2122 28.1067V24.7733C29.2122 22.0067 31.4456 19.7733 34.2122 19.7733C36.9789 19.7733 39.2122 22.0067 39.2122 24.7733V28.1067H29.2122Z";
		private const string signaturePath = "M34.1818 29.2727C32.6909 25.0364 28.6545 22 23.9091 22C17.8909 22 13 26.8909 13 32.9091C13 38.9273 17.8909 43.8182 23.9091 43.8182C28.6545 43.8182 32.6909 40.7818 34.1818 36.5455H42.0909V43.8182H49.3636V36.5455H53V29.2727H34.1818ZM23.9091 36.5455C21.9091 36.5455 20.2727 34.9091 20.2727 32.9091C20.2727 30.9091 21.9091 29.2727 23.9091 29.2727C25.9091 29.2727 27.5455 30.9091 27.5455 32.9091C27.5455 34.9091 25.9091 36.5455 23.9091 36.5455Z";
		private const string contractPath = "M34.3809 61.44C49.5687 61.44 61.8809 49.1278 61.8809 33.94C61.8809 18.7522 49.5687 6.44 34.3809 6.44C19.193 6.44 6.88086 18.7522 6.88086 33.94C6.88086 49.1278 19.193 61.44 34.3809 61.44ZM27.0428 16.44H41.0432L51.5435 26.94V47.94C51.5435 49.865 49.9684 51.44 48.0434 51.44H27.0253C25.1002 51.44 23.5427 49.865 23.5427 47.94L23.5502 35.89H22.0197V45.1152C22.0197 45.9724 21.5061 46.7396 20.7122 47.0699L19.661 47.5068C19.6621 47.5273 19.6649 47.5473 19.6678 47.5673C19.6714 47.5927 19.675 47.618 19.675 47.6442C19.675 49.032 18.5405 50.1608 17.1458 50.1608C15.7512 50.1608 14.6167 49.032 14.6167 47.6442C14.6167 46.2565 15.7512 45.1276 17.1458 45.1276C17.8867 45.1276 18.5474 45.452 19.0105 45.9584L20.0628 45.5212C20.2273 45.4526 20.3336 45.2935 20.3336 45.1149L20.3336 35.8897H18.7837C18.4346 36.8641 17.5074 37.5675 16.41 37.5675C15.0153 37.5675 13.8809 36.4386 13.8809 35.0509C13.8809 33.6631 15.0153 32.5343 16.41 32.5343C17.5077 32.5343 18.4346 33.2376 18.7837 34.212H20.3336L20.3336 24.9868C20.3336 24.8082 20.2267 24.6485 20.0618 24.58L19.0099 24.1427C18.5471 24.6491 17.8864 24.9732 17.1455 24.9732C15.7509 24.9732 14.6164 23.8443 14.6164 22.4566C14.6164 21.0688 15.7509 19.94 17.1455 19.94C18.5402 19.94 19.6747 21.0688 19.6747 22.4566C19.6747 22.4832 19.6711 22.5087 19.6675 22.5342L19.6675 22.5342C19.6646 22.5543 19.6618 22.5743 19.6607 22.5949L20.7119 23.0322C21.5064 23.3625 22.0194 24.13 22.0194 24.9869L22.0197 34.212H23.5513L23.5602 19.94C23.5602 18.015 25.1177 16.44 27.0428 16.44ZM30.5429 44.44H44.5433V40.94H30.5429V44.44ZM30.5429 37.44H44.5433V33.94H30.5429V37.44ZM39.2931 19.065V28.69H48.9184L39.2931 19.065Z";
		private const string thingsPath = "M33.5 61C48.6878 61 61 48.6878 61 33.5C61 18.3122 48.6878 6 33.5 6C18.3122 6 6 18.3122 6 33.5C6 48.6878 18.3122 61 33.5 61ZM39.02 22.146V25.292C39.02 26.157 38.312 26.865 37.447 26.865H24.864C23.998 26.865 23.291 26.157 23.291 25.292V22.146H20.144V47.312H26.484C24.565 45.881 23.29 43.601 23.29 41.021V33.156H39.021V41.021C39.021 43.6 37.762 45.881 35.828 47.312H42.166V50.458H20.146C19.3124 50.4556 18.5135 50.1234 17.9241 49.5339C17.3346 48.9445 17.0024 48.1456 17 47.312V22.146C17.0024 21.3124 17.3346 20.5135 17.9241 19.9241C18.5135 19.3346 19.3124 19.0024 20.146 19H42.167V22.146H39.02ZM32.2683 31.1223C31.9733 31.4173 31.5732 31.583 31.156 31.583C30.7388 31.583 30.3387 31.4173 30.0437 31.1223C29.7487 30.8273 29.583 30.4272 29.583 30.01C29.583 29.5928 29.7487 29.1927 30.0437 28.8977C30.3387 28.6027 30.7388 28.437 31.156 28.437C31.5732 28.437 31.9733 28.6027 32.2683 28.8977C32.5633 29.1927 32.729 29.5928 32.729 30.01C32.729 30.4272 32.5633 30.8273 32.2683 31.1223ZM44.64 30.2C44.02 30.144 43.553 29.616 43.553 28.99C43.5517 28.8177 43.5867 28.6471 43.6558 28.4892C43.7249 28.3314 43.8265 28.1899 43.954 28.074C44.0815 27.9581 44.232 27.8704 44.3957 27.8167C44.5594 27.7629 44.7326 27.7443 44.904 27.762C51.284 28.406 56.355 33.478 56.994 39.852C57.067 40.576 56.49 41.202 55.766 41.202C55.14 41.202 54.611 40.723 54.55 40.097C54.028 34.872 49.865 30.709 44.64 30.2ZM43.553 40.324V38.52V38.519C43.553 37.942 44.118 37.512 44.659 37.672C45.221 37.8529 45.7318 38.165 46.1491 38.5825C46.5665 39 46.8784 39.511 47.059 40.073C47.237 40.631 46.801 41.19 46.224 41.19H44.419C43.94 41.19 43.553 40.804 43.553 40.324ZM44.597 35.142C43.995 35.044 43.553 34.54 43.553 33.939C43.553 33.19 44.198 32.594 44.929 32.711C46.7099 33.001 48.3546 33.8436 49.6305 35.1195C50.9064 36.3954 51.749 38.0401 52.039 39.821C52.161 40.546 51.56 41.196 50.823 41.196H50.811C50.209 41.196 49.712 40.748 49.607 40.152C49.3927 38.9013 48.7958 37.7478 47.8985 36.8505C47.0012 35.9532 45.8477 35.3563 44.597 35.142Z";
		private const string tokenPath = "M37.1248 33.9083C37.1248 36.5422 34.2731 38.1878 31.9928 36.8719C30.9346 36.261 30.2824 35.1302 30.2824 33.9083C30.2824 31.2752 33.1334 29.6288 35.414 30.9455C36.4722 31.5566 37.1248 32.6862 37.1248 33.9083ZM28.6626 29.0916L19.9302 24.275C23.216 22.4293 30.2019 18.5135 31.8584 17.6126C33.5152 16.7126 34.9195 17.298 35.5497 17.6126L47.5231 24.275L38.7459 29.0916C34.5676 25.2384 30.2824 27.4855 28.6626 29.0916ZM49.3239 40.9307C49.3087 41.4554 49.018 42.6585 47.9731 43.2705C46.929 43.8825 39.2256 48.208 35.505 50.2929V40.6153C37.5758 40.1197 41.4641 37.7434 40.4562 32.1974L49.3239 27.2457V40.9307ZM18.129 27.2457L26.9972 32.1974C25.9888 37.7434 29.8769 40.1197 31.9483 40.6153V50.2929C28.2279 48.208 20.5242 43.8825 19.4802 43.2705C18.4348 42.6585 18.1441 41.4554 18.129 40.9307V27.2457ZM6 33.4999C6 48.6881 18.3119 61 33.5001 61C48.6884 61 61 48.6881 61 33.4999C61 18.3116 48.6884 6 33.5001 6C18.3119 6 6 18.3116 6 33.4999Z";

		private static readonly SKColor fgLight = SKColors.White;
		private static readonly SKColor bgLight = SKColors.Black;

		private static readonly SKColor fgDark = SKColors.LightGray;      // NOTE: You cannot switch light and dark areas; many QR decoders will
		private static readonly SKColor bgDark = SKColors.Black;          //       fail to decode such codes. You can make it less bright however.

		private static readonly CustomColoring userCodeLight = new(userPath,
			256, 256, SKColors.Red, fgLight, bgLight, fgLight,
			SKColors.DarkRed, SKColors.Red, fgLight,
			SKColors.DarkSlateGray, SKColors.SlateGray, fgLight);

		private static readonly CustomColoring userCodeDark = new(userPath,
			256, 256, SKColors.Red, fgDark, bgDark, fgDark,
			SKColors.DarkRed, SKColors.Red, fgDark,
			SKColors.DarkSlateGray, SKColors.SlateGray, fgDark);

		private static readonly CustomColoring onboardingCodeLight = new(onboardingPath,
			256, 256, SKColors.Green, fgLight, bgLight, fgLight,
			SKColors.DarkGreen, SKColors.Green, fgLight,
			SKColors.DarkSlateGray, SKColors.SlateGray, fgLight);

		private static readonly CustomColoring onboardingCodeDark = new(onboardingPath,
			256, 256, SKColors.Green, fgDark, bgDark, fgDark,
			SKColors.DarkGreen, SKColors.Green, fgDark,
			SKColors.DarkSlateGray, SKColors.SlateGray, fgDark);

		private static readonly CustomColoring eDalerCodeLight = new(eDalerPath,
			67, 67, SKColors.Goldenrod, fgLight, bgLight, fgLight,
			SKColors.SaddleBrown, SKColors.Sienna, fgLight,
			SKColors.SaddleBrown, SKColors.Sienna, fgLight);

		private static readonly CustomColoring eDalerCodeDark = new(eDalerPath,
			67, 67, SKColors.Goldenrod, fgDark, bgDark, fgDark,
			SKColors.SaddleBrown, SKColors.Sienna, fgDark,
			SKColors.SaddleBrown, SKColors.Sienna, fgDark);

		private static readonly CustomColoring aes256CodeLight = new(aes256Path,
			68, 67, SKColors.DarkSlateGray, fgLight, bgLight, fgLight,
			SKColors.DarkSlateGray, SKColors.SlateGray, fgLight,
			SKColors.DarkSlateGray, SKColors.SlateGray, fgLight);

		private static readonly CustomColoring aes256CodeDark = new(aes256Path,
			68, 67, SKColors.DarkSlateGray, fgDark, bgDark, fgDark,
			SKColors.DarkSlateGray, SKColors.SlateGray, fgDark,
			SKColors.DarkSlateGray, SKColors.SlateGray, fgDark);

		private static readonly CustomColoring signatureCodeLight = new(signaturePath,
			67, 67, SKColors.SlateGray, fgLight, bgLight, fgLight,
			SKColors.DarkSlateGray, SKColors.SlateGray, fgLight,
			SKColors.DarkSlateGray, SKColors.SlateGray, fgLight);

		private static readonly CustomColoring signatureCodeDark = new(signaturePath,
			67, 67, SKColors.SlateGray, fgDark, bgDark, fgDark,
			SKColors.DarkSlateGray, SKColors.SlateGray, fgDark,
			SKColors.DarkSlateGray, SKColors.SlateGray, fgDark);

		private static readonly CustomColoring contractCodeLight = new(contractPath,
			68, 67, SKColors.RoyalBlue, fgLight, bgLight, fgLight,
			SKColors.Navy, SKColors.RoyalBlue, fgLight,
			SKColors.Navy, SKColors.RoyalBlue, fgLight);

		private static readonly CustomColoring contractCodeDark = new(contractPath,
			68, 67, SKColors.RoyalBlue, fgDark, bgDark, fgDark,
			SKColors.Navy, SKColors.RoyalBlue, fgDark,
			SKColors.Navy, SKColors.RoyalBlue, fgDark);

		private static readonly CustomColoring thingsCodeLight = new(thingsPath,
			67, 67, SKColors.DarkOrchid, fgLight, bgLight, fgLight,
			SKColors.Purple, SKColors.DarkOrchid, fgLight,
			SKColors.Purple, SKColors.DarkOrchid, fgLight);

		private static readonly CustomColoring thingsCodeDark = new(thingsPath,
			67, 67, SKColors.DarkOrchid, fgDark, bgDark, fgDark,
			SKColors.Purple, SKColors.DarkOrchid, fgDark,
			SKColors.Purple, SKColors.DarkOrchid, fgDark);

		private static readonly CustomColoring tokenCodeLight = new(tokenPath,
			67, 67, SKColors.LightSeaGreen, fgLight, bgLight, fgLight,
			SKColors.Teal, SKColors.LightSeaGreen, fgLight,
			SKColors.Teal, SKColors.LightSeaGreen, fgLight);

		private static readonly CustomColoring tokenCodeDark = new(tokenPath,
			67, 67, SKColors.DarkSeaGreen, fgDark, bgDark, fgDark,
			SKColors.Teal, SKColors.DarkSeaGreen, fgDark,
			SKColors.Teal, SKColors.DarkSeaGreen, fgDark);
	}
}
