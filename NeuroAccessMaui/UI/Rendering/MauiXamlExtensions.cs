using System.Text;
using System.Xml;
using Waher.Content.Markdown;
using Waher.Content.Xml;

namespace NeuroAccessMaui.UI.Rendering
{
	/// <summary>
	/// Markdown rendering extensions for Maui XAML.
	/// </summary>
	/// <remarks>
	/// Modified from original in Waher.Content.Markdown.Xamarin library, with permission.
	/// </remarks>
	public static class MauiXamlExtensions
	{
		/// <summary>
		/// Generates Maui XAML from the markdown text.
		/// </summary>
		/// <param name="Document">Markdown document.</param>
		/// <returns>Maui XAML</returns>
		public static Task<string> GenerateMauiXaml(this MarkdownDocument Document)
		{
			return Document.GenerateMauiXaml(XML.WriterSettings(false, true));
		}

		/// <summary>
		/// Generates a VerticalStackLayout from the markdown text.
		/// </summary>
		/// <param name="Document">Markdown document.</param>
		/// <returns>VerticalStackLayout</returns>
		public static async Task<VerticalStackLayout?> GenerateMaui(this MarkdownDocument Document)
		{
			using MauiRenderer Renderer = new(Document);
			await Document.RenderDocument(Renderer);
			return Renderer.Output();
		}

		/// <summary>
		/// Generates Maui XAML from the markdown text.
		/// </summary>
		/// <param name="Document">Markdown document.</param>
		/// <param name="XmlSettings">XML settings.</param>
		/// <returns>Maui XAML</returns>
		public static async Task<string> GenerateMauiXaml(this MarkdownDocument Document, XmlWriterSettings XmlSettings)
		{
			StringBuilder Output = new();
			await Document.GenerateMauiXaml(Output, XmlSettings);
			return Output.ToString();
		}

		/// <summary>
		/// Generates Maui XAML from the markdown text.
		/// </summary>
		/// <param name="Document">Markdown document.</param>
		/// <param name="Output">Maui XAML will be output here.</param>
		public static Task GenerateMauiXaml(this MarkdownDocument Document, StringBuilder Output)
		{
			return Document.GenerateMauiXaml(Output, XML.WriterSettings(false, true));
		}

		/// <summary>
		/// Generates Maui XAML from the markdown text.
		/// </summary>
		/// <param name="Document">Markdown document.</param>
		/// <param name="Output">Maui XAML will be output here.</param>
		/// <param name="XmlSettings">XML settings.</param>
		public static Task GenerateMauiXaml(this MarkdownDocument Document, StringBuilder Output, XmlWriterSettings XmlSettings)
		{
			using MauiXamlRenderer Renderer = new(Output, XmlSettings);
			return Document.RenderDocument(Renderer);
		}
	}
}
