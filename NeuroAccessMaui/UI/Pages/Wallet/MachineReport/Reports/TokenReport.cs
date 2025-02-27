using NeuroAccessMaui.Extensions;
using NeuroAccessMaui.Services;
using NeuroFeatures;
using NeuroFeatures.EventArguments;
using System.Text;
using System.Text.RegularExpressions;
using Waher.Content;
using Waher.Content.Xml;

namespace NeuroAccessMaui.UI.Pages.Wallet.MachineReport.Reports
{
	/// <summary>
	/// Abstract base class for token reports.
	/// </summary>
	/// <param name="TokenId">ID of token being viewed.</param>
	public abstract partial class TokenReport(string TokenId)
		: IDisposable
	{
		private readonly List<string> temporaryFiles = [];
		private readonly string tokenId = TokenId;
		private MachineReportViewModel? view;
		private Timer? timer;
		private bool isDisposed;

		/// <summary>
		/// Token ID associated with the state-machine.
		/// </summary>
		public string TokenId => this.tokenId;

		/// <summary>
		/// Gets the title of report.
		/// </summary>
		/// <returns>Title</returns>
		public abstract Task<string> GetTitle();

		/// <summary>
		/// Gets the XAML for the report.
		/// </summary>
		/// <returns>String-representation of XAML of report.</returns>
		public abstract Task<string> GetReportXaml();

		/// <summary>
		/// Parses report XAML
		/// </summary>
		/// <param name="Xaml">String-representation of XAML</param>
		/// <param name="TemporaryFiles">List that will receive file names of temporary files generated.</param>
		/// <returns>Parsed XAML</returns>
		public static async Task<object?> ParseReport(string Xaml, List<string> TemporaryFiles)
		{
			StringBuilder sb = new();
			string s;
			int i = 0;
			int c = Xaml.Length;

			while (i < c)
			{
				Match M = standaloneDynamicImage.Match(Xaml, i);

				if (M.Success)
				{
					sb.Append(Xaml[i..M.Index]);
					i = M.Index + M.Length;

					// TODO: Border width

					sb.Append("<Image");

					string ContentType = M.Groups["ContentType"].Value;
					string FileExtension = InternetContent.GetFileExtension(ContentType);
					byte[] Bin = Convert.FromBase64String(M.Groups["Base64"].Value);

					string FileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + "." + FileExtension);
					using (FileStream TempFile = File.Create(FileName))
					{
						await TempFile.WriteAsync(Bin, 0, Bin.Length);
					}

					sb.Append(" Source=\"");
					sb.Append(XML.HtmlAttributeEncode(FileName));
					sb.Append("\"/>");

					TemporaryFiles.Add(FileName);
				}
				else
				{
					M = embeddedDynamicImage.Match(Xaml, i);
					if (M.Success)
					{
						sb.Append(Xaml[i..M.Index]);
						i = M.Index + M.Length;

						sb.Append("<img");

						s = M.Groups["Border"].Value;
						if (!string.IsNullOrEmpty(s))
						{
							sb.Append(" border=\"");
							sb.Append(s);
							sb.Append('"');
						}

						s = M.Groups["Width"].Value;
						if (!string.IsNullOrEmpty(s))
						{
							sb.Append(" width=\"");
							sb.Append(s);
							sb.Append('"');
						}

						s = M.Groups["Height"].Value;
						if (!string.IsNullOrEmpty(s))
						{
							sb.Append(" height=\"");
							sb.Append(s);
							sb.Append('"');
						}

						s = M.Groups["Alt"].Value;
						if (!string.IsNullOrEmpty(s))
						{
							sb.Append(" alt=\"");
							sb.Append(s);
							sb.Append('"');
						}

						string ContentType = M.Groups["ContentType"].Value;
						string FileExtension = InternetContent.GetFileExtension(ContentType);
						byte[] Bin = Convert.FromBase64String(M.Groups["Base64"].Value);

						string FileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + "." + FileExtension);
						using (FileStream TempFile = File.Create(FileName))
						{
							await TempFile.WriteAsync(Bin, 0, Bin.Length);
						}

						sb.Append(" src=\"");
						sb.Append(XML.HtmlAttributeEncode(FileName));
						sb.Append("\"/>");

						TemporaryFiles.Add(FileName);
					}
					else
					{
						sb.Append(Xaml[i..c]);
						i = c;
					}
				}
			}

			return sb.ToString().ParseXaml();
		}

		private static readonly Regex standaloneDynamicImage = EmbeddedDynamicImage();
		private static readonly Regex embeddedDynamicImage = EmbeddedDynamicImage();
		private static readonly Regex dynamicImage = DynamicImage();

		[GeneratedRegex("<Label LineBreakMode=\"WordWrap\"( HorizontalTextAlignment=\"Start\")? TextType=\"Html\"><!\\[CDATA\\[<img(\\s+((border=[\"'](?'Border'\\d+)[\"'])|(width=[\"'](?'Width'\\d+)[\"'])|(height=[\"'](?'Height'\\d+)[\"'])|(alt=[\"'](?'Alt'[^\"']*)[\"'])|(src=[\"']data:(?'ContentType'\\w+\\/\\w+);base64,(?'Base64'[A-Za-z0-9+-\\/_=]+)[\"'])))*\\s*\\/>]]><\\/Label>", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled)]
		private static partial Regex StandaloneDynamicImage();
		[GeneratedRegex("<img(\\s+((border=[\"'](?'Border'\\d+)[\"'])|(width=[\"'](?'Width'\\d+)[\"'])|(height=[\"'](?'Height'\\d+)[\"'])|(alt=[\"'](?'Alt'[^\"']*)[\"'])|(src=[\"']data:(?'ContentType'\\w+\\/\\w+);base64,(?'Base64'[A-Za-z0-9+-\\/_=]+)[\"'])))*\\s*\\/>", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled, "sv-SE")]
		private static partial Regex EmbeddedDynamicImage();
		[GeneratedRegex("<!--<img(\\s+((border=[\"'](?'Border'\\d+)[\"'])|(width=[\"'](?'Width'\\d+)[\"'])|(height=[\"'](?'Height'\\d+)[\"'])|(alt=[\"'](?'Alt'[^\"']*)[\"'])|(src=[\"']data:(?'ContentType'\\w+\\/\\w+);base64,(?'Base64'[A-Za-z0-9+-\\/_=]+)[\"'])))*\\s*\\/>-->", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled, "sv-SE")]
		private static partial Regex DynamicImage();

		/// <summary>
		/// Method called when the underlying state-machine has changed state.
		/// </summary>
		/// <param name="Sender">Sender of event.</param>
		/// <param name="e">Event arguments.</param>
		public virtual Task OnNewState(object? Sender, NewStateEventArgs e)
		{
			this.DelayedRefresh();
			return Task.CompletedTask;
		}

		/// <summary>
		/// Method called when variables in the underlying state-machine have been updated.
		/// </summary>
		/// <param name="Sender">Sender of event.</param>
		/// <param name="e">Event arguments.</param>
		public virtual Task OnVariablesUpdated(object? Sender, VariablesUpdatedEventArgs e)
		{
			this.DelayedRefresh();
			return Task.CompletedTask;
		}

		private void DelayedRefresh()
		{
			this.StopTimer();
			this.timer = new Timer(this.Action, null, 500, Timeout.Infinite);
		}

		/// <summary>
		/// Method called when it is time to execute action.
		/// </summary>
		public async void Action(object? _)
		{
			if (this.view is not null)
			{
				try
				{
					await this.GenerateReport(this.view);
				}
				catch (Exception ex)
				{
					await ServiceRef.UiService.DisplayException(ex);
				}
			}
		}

		/// <summary>
		/// Generates the report.
		/// </summary>
		/// <param name="ReportView">Report View</param>
		public async Task GenerateReport(MachineReportViewModel ReportView)
		{
			this.view = ReportView;

			string Xaml = await this.GetReportXaml();

			MainThread.BeginInvokeOnMainThread(async () =>
			{
				try
				{
					List<string> TempFiles = [];

					this.view.Report = await ParseReport(Xaml, TempFiles);

					lock (this.temporaryFiles)
					{
						this.temporaryFiles.AddRange(TempFiles);
					}
				}
				catch (Exception ex)
				{
					await ServiceRef.UiService.DisplayException(ex);
				}
			});
		}

		/// <summary>
		/// <see cref="IDisposable.Dispose"/>
		/// </summary>
		public void Dispose()
		{
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// <see cref="IDisposable.Dispose"/>
		/// </summary>
		protected virtual void Dispose(bool disposing)
		{
			if (this.isDisposed)
				return;

			if (disposing)
			{
				this.StopTimer();
				this.DeleteTemporaryFiles();
			}

			this.isDisposed = true;
		}

		/// <summary>
		/// Deletes any temporary files.
		/// </summary>
		public void DeleteTemporaryFiles()
		{
			string[] ToDelete;

			lock (this.temporaryFiles)
			{
				ToDelete = [.. this.temporaryFiles];
				this.temporaryFiles.Clear();
			}

			foreach (string FileToDelete in ToDelete)
			{
				try
				{
					File.Delete(FileToDelete);
				}
				catch (Exception ex)
				{
					ServiceRef.LogService.LogException(ex);
				}
			}
		}

		/// <summary>
		/// Stops the timer for any queued delayed refresh action.
		/// </summary>
		public void StopTimer()
		{
			this.timer?.Dispose();
			this.timer = null;
		}
	}
}
