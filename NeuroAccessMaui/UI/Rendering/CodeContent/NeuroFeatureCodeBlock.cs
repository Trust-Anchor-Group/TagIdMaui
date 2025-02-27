using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services;
using NeuroFeatures;
using System.Text;
using System.Xml;
using Waher.Content.Markdown;
using Waher.Content.Markdown.Model;
using Waher.Runtime.Inventory;

namespace NeuroAccessMaui.UI.Rendering.CodeContent
{
	/// <summary>
	/// Handles embedded tokens.
	/// </summary>
	public class NeuroFeatureCodeBlock : ICodeContent, ICodeContentMauiXamlRenderer
	{
		private MarkdownDocument? document;

		/// <summary>
		/// Handles embedded tokens.
		/// </summary>
		public NeuroFeatureCodeBlock()
		{
		}

		/// <summary>
		/// Markdown document.
		/// </summary>
		public MarkdownDocument? Document => this.document;

		/// <summary>
		/// If script is evaluated for this type of code block.
		/// </summary>
		public bool EvaluatesScript => false;

		/// <summary>
		/// Generates Maui XAML
		/// </summary>
		public Task<bool> RenderMauiXaml(MauiXamlRenderer Renderer, string[] Rows, string Language, int Indent, MarkdownDocument Document)
		{
			XmlWriter Output = Renderer.XmlOutput;
			Token Token;

			try
			{
				StringBuilder sb = new();

				foreach (string Row in Rows)
					sb.AppendLine(Row);

				XmlDocument Doc = new()
				{
					PreserveWhitespace = true
				};
				Doc.LoadXml(sb.ToString());

				if (!NeuroFeatures.Token.TryParse(Doc.DocumentElement, out Token))
					throw new Exception(ServiceRef.Localizer[nameof(AppResources.InvalidNeuroFeatureToken)]);
			}
			catch (Exception ex)
			{
				Output.WriteStartElement("Label");
				Output.WriteAttributeString("Text", ex.Message);
				Output.WriteAttributeString("FontFamily", "SpaceGroteskRegular");
				Output.WriteAttributeString("TextColor", "Red");
				Output.WriteAttributeString("LineBreakMode", "WordWrap");
				Output.WriteEndElement();

				return Task.FromResult(false);
			}

			Output.WriteStartElement("VerticalStackLayout");
			Output.WriteAttributeString("HorizontalOptions", "Center");

			Output.WriteStartElement("Path");
			Output.WriteAttributeString("VerticalOptions", "Center");
			Output.WriteAttributeString("HorizontalOptions", "Center");
			Output.WriteAttributeString("HeightRequest", "16");
			Output.WriteAttributeString("WidthRequest", "16");
			Output.WriteAttributeString("Aspect", "Uniform");
			Output.WriteAttributeString("Fill", "{AppThemeBinding Light={StaticResource PrimaryForegroundLight}, Dark={StaticResource PrimaryForegroundDark}}");
			Output.WriteAttributeString("Data", "{x:Static ui:Geometries.TokenPath}");
			Output.WriteEndElement();

			Output.WriteStartElement("Label");
			Output.WriteAttributeString("LineBreakMode", "WordWrap");
			Output.WriteAttributeString("FontSize", "Medium");
			Output.WriteAttributeString("HorizontalOptions", "Center");
			Output.WriteAttributeString("Text", Token.FriendlyName);
			Output.WriteEndElement();

			Output.WriteStartElement("StackLayout.GestureRecognizers");

			Output.WriteStartElement("TapGestureRecognizer");
			Output.WriteAttributeString("Command", "{Binding Path=NeuroFeatureUriClicked}");
			Output.WriteAttributeString("CommandParameter", Constants.UriSchemes.NeuroFeature + ":" + Token.ToXml());
			Output.WriteEndElement();

			Output.WriteEndElement();
			Output.WriteEndElement();

			return Task.FromResult(true);
		}

		/// <summary>
		/// Registers the Markdown document in which the construct resides.
		/// </summary>
		/// <param name="Document">Markdown document</param>
		public void Register(MarkdownDocument Document)
		{
			this.document = Document;
		}

		/// <summary>
		/// How much the module supports code of a given language (i.e. type of content)
		/// </summary>
		/// <param name="Language">Code language</param>
		/// <returns>Grade of support.</returns>
		public Grade Supports(string Language)
		{
			return string.Equals(Language, Constants.UriSchemes.NeuroFeature, StringComparison.OrdinalIgnoreCase) ? Grade.Excellent : Grade.NotAtAll;
		}
	}
}
