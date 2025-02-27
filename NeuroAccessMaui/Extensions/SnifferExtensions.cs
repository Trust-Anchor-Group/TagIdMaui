using System.Text;
using Waher.Networking.Sniffers;

namespace NeuroAccessMaui.Extensions
{
	/// <summary>
	/// Extensions for the <see cref="ISniffer"/> implementation.
	/// </summary>
	public static class SnifferExtensions
	{
		/// <summary>
		/// Converts the latest Xmpp communication that the sniffer holds to plain text.
		/// </summary>
		/// <param name="Sniffer">The sniffer whose contents to get.</param>
		/// <returns>The xmpp communication in plain text.</returns>
		public static async Task<string> SnifferToTextAsync(this InMemorySniffer Sniffer)
		{
			if (Sniffer is null)
				return string.Empty;

			StringBuilder sb = new();

			await using StringWriter Writer = new(sb);
			await using TextWriterSniffer Output = new(Writer, BinaryPresentationMethod.ByteCount);

			await Sniffer.FlushAsync();

			Sniffer.Replay(Output);

			await Output.FlushAsync();

			return sb.ToString();
		}
	}
}
