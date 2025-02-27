using NeuroAccessMaui.Services.Contacts;
using NeuroAccessMaui.Services.UI.Photos;
using System.Globalization;
using System.Text;
using System.Xml;
using Waher.Content.Markdown;
using Waher.Content.Markdown.Model;
using Waher.Networking.XMPP.Contracts;
using Waher.Runtime.Inventory;

namespace NeuroAccessMaui.UI.Rendering.CodeContent
{
	/// <summary>
	/// Handles embedded Legal IDs.
	/// </summary>
	public class IoTIdCodeBlock : ICodeContent, ICodeContentMauiXamlRenderer
	{
		private MarkdownDocument? document;

		/// <summary>
		/// Handles embedded Legal IDs.
		/// </summary>
		public IoTIdCodeBlock()
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
		public async Task<bool> RenderMauiXaml(MauiXamlRenderer Renderer, string[] Rows, string Language, int Indent, MarkdownDocument Document)
		{
			XmlWriter Output = Renderer.XmlOutput;
			LegalIdentity Identity;

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

				Identity = LegalIdentity.Parse(Doc.DocumentElement);
			}
			catch (Exception ex)
			{
				Output.WriteStartElement("Label");
				Output.WriteAttributeString("Text", ex.Message);
				Output.WriteAttributeString("FontFamily", "SpaceGroteskRegular");
				Output.WriteAttributeString("TextColor", "Red");
				Output.WriteAttributeString("LineBreakMode", "WordWrap");
				Output.WriteEndElement();

				return false;
			}

			Output.WriteStartElement("VerticalStackLayout");
			Output.WriteAttributeString("HorizontalOptions", "Center");

			bool ImageShown = false;

			if (Identity.Attachments is not null)
			{
				(string? FileName, int Width, int Height) = await PhotosLoader.LoadPhotoAsTemporaryFile(Identity.Attachments,
					Constants.QrCode.DefaultImageWidth, Constants.QrCode.DefaultImageHeight);

				if (!string.IsNullOrEmpty(FileName))
				{
					Output.WriteStartElement("Image");
					Output.WriteAttributeString("Source", FileName);
					Output.WriteAttributeString("WidthRequest", Width.ToString(CultureInfo.InvariantCulture));
					Output.WriteAttributeString("HeightRequest", Height.ToString(CultureInfo.InvariantCulture));
					Output.WriteEndElement();

					ImageShown = true;
				}
			}

			if (!ImageShown)
			{
				Output.WriteStartElement("Path");
				Output.WriteAttributeString("VerticalOptions", "Center");
				Output.WriteAttributeString("HorizontalOptions", "Center");
				Output.WriteAttributeString("HeightRequest", "16");
				Output.WriteAttributeString("WidthRequest", "16");
				Output.WriteAttributeString("Aspect", "Uniform");
				Output.WriteAttributeString("Fill", "{AppThemeBinding Light={StaticResource PrimaryForegroundLight}, Dark={StaticResource PrimaryForegroundDark}}");
				Output.WriteAttributeString("Data", "{x:Static ui:Geometries.PersonPath}");
				Output.WriteEndElement();
			}

			Output.WriteStartElement("Label");
			Output.WriteAttributeString("LineBreakMode", "WordWrap");
			Output.WriteAttributeString("FontSize", "Medium");
			Output.WriteAttributeString("HorizontalOptions", "Center");
			Output.WriteAttributeString("Text", ContactInfo.GetFriendlyName(Identity));
			Output.WriteEndElement();

			Output.WriteStartElement("VerticalStackLayout.GestureRecognizers");

			StringBuilder Xml = new();
			Identity.Serialize(Xml, true, true, true, true, true, true, true);

			Output.WriteStartElement("TapGestureRecognizer");
			Output.WriteAttributeString("Command", "{Binding Path=IotIdUriClicked}");
			Output.WriteAttributeString("CommandParameter", Constants.UriSchemes.IotId + ":" + Xml.ToString());
			Output.WriteEndElement();

			Output.WriteEndElement();
			Output.WriteEndElement();

			return true;
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
			return string.Equals(Language, Constants.UriSchemes.IotId, StringComparison.OrdinalIgnoreCase) ? Grade.Excellent : Grade.NotAtAll;
		}
	}
}
