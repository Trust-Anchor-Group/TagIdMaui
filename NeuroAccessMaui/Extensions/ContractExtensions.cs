using NeuroAccessMaui.UI.Rendering;
using Waher.Content.Markdown;
using Waher.Content.Markdown.Functions;
using Waher.Networking.XMPP.Contracts;
using Waher.Networking.XMPP.Contracts.HumanReadable;

namespace NeuroAccessMaui.Extensions
{
	/// <summary>
	/// Extensions for the <see cref="Contract"/> class.
	/// </summary>
	public static class ContractExtensions
	{
		/// <summary>
		/// Returns the language to use when displaying a contract on the device
		/// </summary>
		/// <param name="Contract">Contract</param>
		/// <returns>Language to use when displaying contract.</returns>
		public static string DeviceLanguage(this Contract Contract)
		{
			string[] Languages = Contract.GetLanguages();
			string Language = System.Globalization.CultureInfo.CurrentCulture.TwoLetterISOLanguageName;

			foreach (string Option in Languages)
			{
				if (string.Equals(Option.Before("-"), Language, StringComparison.OrdinalIgnoreCase))
					return Option;
			}

			return Contract.DefaultLanguage;
		}

		/// <summary>
		/// Creates a human-readable XAML document for the contract.
		/// </summary>
		/// <param name="Contract">Contract reference.</param>
		/// <param name="Language">Desired language</param>
		/// <returns>Markdown</returns>
		public static Task<string> ToMauiXaml(this Contract Contract, string Language)
		{
			return Contract.ToMauiXaml(Contract.ForHumans, Language);
		}

		/// <summary
		/// Creates a human-readable VerticalStackLayout for the Contract.
		/// </summary>
		/// <param name="Contract">Contract reference.</param>
		/// <param name="Language">Desired language</param>
		/// <returns>Markdown</returns>
		public static Task<VerticalStackLayout?> ToMaui(this Contract Contract, string Language)
		{
			return Contract.ToMaui(Contract.ForHumans, Language);
		}

		/// <summary>
		/// Selects a human-readable text, and generates a XAML document from it.
		/// </summary>
		/// <param name="Contract">Contract reference.</param>
		/// <param name="Text">Collection of texts in different languages.</param>
		/// <param name="Language">Language</param>
		/// <returns>XAML document.</returns>
		public static Task<string> ToMauiXaml(this Contract Contract, HumanReadableText[] Text, string Language)
		{
			return Contract.Select(Text, Language)?.ToMauiXaml(Contract) ?? Task.FromResult<string>(string.Empty);
		}

		/// <summary>
		/// Selects a human-readable text, and generates a VerticalStackLayout from it.
		/// </summary>
		/// <param name="Contract">Contract reference.</param>
		/// <param name="Text">Collection of texts in different languages.</param>
		/// <param name="Language">Language</param>
		/// <returns>VerticalStackLayout</returns>
		public static Task<VerticalStackLayout?> ToMaui(this Contract Contract, HumanReadableText[] Text, string Language)
		{
			return Contract.Select(Text, Language)?.ToMaui(Contract) ?? Task.FromResult<VerticalStackLayout?>(null);
		}

		/// <summary>
		/// Generates XAML for the human-readable text.
		/// </summary>
		/// <param name="Text">Human-readable text being rendered.</param>
		/// <param name="Contract">Contract, of which the human-readable text is part.</param>
		/// <returns>XAML</returns>
		public static async Task<string> ToMauiXaml(this HumanReadableText Text, Contract Contract)
		{
			MarkdownDocument Doc = await Text.GenerateMarkdownDocument(Contract);
			return await Doc.GenerateMauiXaml();
		}

		/// <summary>
		/// Generates a VerticalStackLayout for the human-readable text.
		/// </summary>
		/// <param name="Text">Human-readable text being rendered.</param>
		/// <param name="Contract">Contract, of which the human-readable text is part.</param>
		/// <returns>VerticalStackLayout</returns>
		public static async Task<VerticalStackLayout?> ToMaui(this HumanReadableText Text, Contract Contract)
		{
			MarkdownDocument Doc = await Text.GenerateMarkdownDocument(Contract);

			return await Doc.GenerateMaui();
		}

		/// <summary>
		/// Creates a human-readable XAML document for the contract.
		/// </summary>
		/// <param name="Description">Description being rendered.</param>
		/// <param name="Language">Desired language</param>
		/// <param name="Contract">Contract hosting the object.</param>
		/// <returns>Markdown</returns>
		public static Task<string> ToMauiXaml(this LocalizableDescription Description, string Language, Contract Contract)
		{
			return Contract.ToMauiXaml(Description.Descriptions, Language);
		}
	}
}
