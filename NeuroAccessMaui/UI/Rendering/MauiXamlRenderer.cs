using SkiaSharp;
using System.Globalization;
using System.Text;
using System.Xml;
using Waher.Content.Emoji;
using Waher.Content.Markdown;
using Waher.Content.Markdown.Model;
using Waher.Content.Markdown.Model.BlockElements;
using Waher.Content.Markdown.Model.SpanElements;
using Waher.Content.Markdown.Rendering;
using Waher.Events;
using Waher.Script;
using Waher.Script.Graphs;
using Waher.Script.Operators.Matrices;

namespace NeuroAccessMaui.UI.Rendering
{
	/// <summary>
	/// Renders XAML (Maui flavour) from a Markdown document.
	/// </summary>
	/// <remarks>
	/// Modified from original in Waher.Content.Markdown.Xamarin library, with permission.
	/// </remarks>
	public class MauiXamlRenderer : Renderer
	{
		/// <summary>
		/// XML output
		/// </summary>
		public readonly XmlWriter XmlOutput;

		/// <summary>
		/// Current text-alignment.
		/// </summary>
		public Waher.Content.Markdown.Model.TextAlignment Alignment = Waher.Content.Markdown.Model.TextAlignment.Left;

		/// <summary>
		/// If text is bold
		/// </summary>
		public bool Bold = false;

		/// <summary>
		/// If text is italic
		/// </summary>
		public bool Italic = false;

		/// <summary>
		/// If text is stricken through
		/// </summary>
		public bool StrikeThrough = false;

		/// <summary>
		/// If text is underlined
		/// </summary>
		public bool Underline = false;

		/// <summary>
		/// If text is superscript
		/// </summary>
		public bool Superscript = false;

		/// <summary>
		/// If text is subscript
		/// </summary>
		public bool Subscript = false;

		/// <summary>
		/// If text is inline code.
		/// </summary>
		public bool Code = false;

		/// <summary>
		/// If rendering is inside a label.
		/// </summary>
		public bool InLabel = false;

		/// <summary>
		/// Link, if rendering a hyperlink, null otherwise.
		/// </summary>
		public string? Hyperlink = null;

		/// <summary>
		/// Renders XAML (Maui flavour) from a Markdown document.
		/// </summary>
		/// <param name="XmlSettings">XML-specific settings.</param>
		public MauiXamlRenderer(XmlWriterSettings XmlSettings)
			: base()
		{
			this.XmlOutput = XmlWriter.Create(this.Output, XmlSettings);
		}

		/// <summary>
		/// Renders XAML (Maui flavour) from a Markdown document.
		/// </summary>
		/// <param name="Output">XAML output.</param>
		/// <param name="XmlSettings">XML-specific settings.</param>
		public MauiXamlRenderer(StringBuilder Output, XmlWriterSettings XmlSettings)
			: base(Output)
		{
			this.XmlOutput = XmlWriter.Create(this.Output, XmlSettings);
		}

		/// <inheritdoc/>
		public override void Dispose()
		{
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private bool isDisposed;

		/// <summary>
		/// <see cref="IDisposable.Dispose"/>
		/// </summary>
		protected virtual void Dispose(bool disposing)
		{
			if (this.isDisposed)
				return;

			if (disposing)
			{
				base.Dispose();
				this.XmlOutput.Dispose();
			}

			this.isDisposed = true;
		}

		/// <summary>
		/// Renders a document.
		/// </summary>
		/// <param name="Document">Document to render.</param>
		/// <param name="Inclusion">If the rendered output is to be included in another document (true), or if it is a standalone document (false).</param>
		public override Task RenderDocument(MarkdownDocument Document, bool Inclusion)
		{
			this.Alignment = Waher.Content.Markdown.Model.TextAlignment.Left;
			this.Bold = false;
			this.Italic = false;
			this.StrikeThrough = false;
			this.Underline = false;
			this.Superscript = false;
			this.Subscript = false;
			this.Code = false;
			this.InLabel = false;
			this.Hyperlink = null;

			return base.RenderDocument(Document, Inclusion);
		}

		/// <summary>
		/// Renders the document header.
		/// </summary>
		public override Task RenderDocumentHeader()
		{
			this.XmlOutput.WriteStartElement("VerticalStackLayout", "http://schemas.microsoft.com/dotnet/2021/maui");
			this.XmlOutput.WriteAttributeString("xmlns", "x", null, "http://schemas.microsoft.com/winfx/2009/xaml");
			this.XmlOutput.WriteAttributeString("xmlns", "ui", null, "clr-namespace:NeuroAccessMaui.UI");
			this.XmlOutput.WriteAttributeString("Spacing", "0");

			return Task.CompletedTask;
		}

		/// <summary>
		/// Renders footnotes.
		/// </summary>
		public override async Task RenderFootnotes()
		{
			Footnote Footnote;
			int Nr;
			int Row = 0;

			this.XmlOutput.WriteStartElement("BoxView");
			this.XmlOutput.WriteAttributeString("HeightRequest", "1");
			this.XmlOutput.WriteAttributeString("BackgroundColor", "{AppThemeBinding Light={StaticResource NormalEditPlaceholderLight}, Dark={StaticResource NormalEditPlaceholderDark}}");
			this.XmlOutput.WriteAttributeString("HorizontalOptions", "FillAndExpand");
			this.XmlOutput.WriteAttributeString("Margin", SmallMargins(false, false, true, true));
			this.XmlOutput.WriteEndElement();

			this.XmlOutput.WriteStartElement("Grid");
			this.XmlOutput.WriteAttributeString("RowSpacing", "0");
			this.XmlOutput.WriteAttributeString("ColumnSpacing", "0");

			this.XmlOutput.WriteStartElement("Grid.ColumnDefinitions");

			this.XmlOutput.WriteStartElement("ColumnDefinition");
			this.XmlOutput.WriteAttributeString("Width", "Auto");
			this.XmlOutput.WriteEndElement();

			this.XmlOutput.WriteStartElement("ColumnDefinition");
			this.XmlOutput.WriteAttributeString("Width", "*");
			this.XmlOutput.WriteEndElement();

			this.XmlOutput.WriteEndElement();
			this.XmlOutput.WriteStartElement("Grid.RowDefinitions");

			foreach (string Key in this.Document.FootnoteOrder)
			{
				if ((this.Document?.TryGetFootnoteNumber(Key, out Nr) ?? false) &&
					(this.Document?.TryGetFootnote(Key, out Footnote) ?? false) &&
					Footnote.Referenced)
				{
					this.XmlOutput.WriteStartElement("RowDefinition");
					this.XmlOutput.WriteAttributeString("Height", "Auto");
					this.XmlOutput.WriteEndElement();
				}
			}

			this.XmlOutput.WriteEndElement();

			if (this.Document is not null)
			{
				foreach (string Key in this.Document.FootnoteOrder)
				{
					if ((this.Document?.TryGetFootnoteNumber(Key, out Nr) ?? false) &&
						(this.Document?.TryGetFootnote(Key, out Footnote) ?? false) &&
						Footnote.Referenced)
					{
						this.XmlOutput.WriteStartElement("ContentView");
						this.XmlOutput.WriteAttributeString("Margin", "{StaticResource SmallMargins}");
						this.XmlOutput.WriteAttributeString("Grid.Column", "0");
						this.XmlOutput.WriteAttributeString("Grid.Row", Row.ToString(CultureInfo.InvariantCulture));
						this.XmlOutput.WriteAttributeString("Scale", "0.75");
						this.XmlOutput.WriteAttributeString("TranslationY", "-5");

						this.XmlOutput.WriteStartElement("Label");
						this.XmlOutput.WriteAttributeString("Text", Nr.ToString(CultureInfo.InvariantCulture));
						this.XmlOutput.WriteEndElement();
						this.XmlOutput.WriteEndElement();

						this.XmlOutput.WriteStartElement("ContentView");
						this.XmlOutput.WriteAttributeString("Grid.Column", "1");
						this.XmlOutput.WriteAttributeString("Grid.Row", Row.ToString(CultureInfo.InvariantCulture));
						await Footnote.Render(this);
						this.XmlOutput.WriteEndElement();

						Row++;
					}
				}
			}

			this.XmlOutput.WriteEndElement();
		}

		/// <summary>
		/// Renders the document header.
		/// </summary>
		public override Task RenderDocumentFooter()
		{
			this.XmlOutput.WriteEndElement();
			this.XmlOutput.Flush();

			return Task.CompletedTask;
		}

		#region Span Elements

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override Task Render(Abbreviation Element)
		{
			return this.RenderChildren(Element);
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override Task Render(AutomaticLinkMail Element)
		{
			string? Bak = this.Hyperlink;
			this.Hyperlink = "mailto:" + Element.EMail;
			this.RenderSpan(this.Hyperlink);
			this.Hyperlink = Bak;

			return Task.CompletedTask;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override Task Render(AutomaticLinkUrl Element)
		{
			string? Bak = this.Hyperlink;
			this.Hyperlink = Element.URL;
			this.RenderSpan(Element.URL);
			this.Hyperlink = Bak;

			return Task.CompletedTask;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override async Task Render(Delete Element)
		{
			bool Bak = this.StrikeThrough;
			this.StrikeThrough = true;

			await this.RenderChildren(Element);

			this.StrikeThrough = Bak;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override Task Render(DetailsReference Element)
		{
			if (this.Document.Detail is not null)
				return this.RenderDocument(this.Document.Detail, false);
			else
				return this.Render((MetaReference)Element);
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override async Task Render(EmojiReference Element)
		{
			if (this.InLabel)
				this.RenderSpan(Element.Emoji.Unicode);
			else
			{
				IEmojiSource EmojiSource = this.Document.EmojiSource;

				if (EmojiSource is null)
					this.RenderSpan(Element.Delimiter + Element.Emoji.ShortName + Element.Delimiter);
				else if (!EmojiSource.EmojiSupported(Element.Emoji))
					this.RenderSpan(Element.Emoji.Unicode);
				else
				{
					Waher.Content.Emoji.IImageSource Source = await this.Document.EmojiSource.GetImageSource(Element.Emoji, Element.Level);
					await Multimedia.ImageContent.OutputMauiXaml(this.XmlOutput, Source);
				}
			}
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override async Task Render(Emphasize Element)
		{
			bool Bak = this.Italic;
			this.Italic = true;

			await this.RenderChildren(Element);

			this.Italic = Bak;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override async Task Render(FootnoteReference Element)
		{
			if (!(this.Document?.TryGetFootnote(Element.Key, out Footnote? Footnote) ?? false))
				Footnote = null;

			if (Element.AutoExpand && Footnote is not null)
				await this.Render(Footnote);
			else if (this.Document?.TryGetFootnoteNumber(Element.Key, out int Nr) ?? false)
			{
				bool Bak = this.Superscript;
				this.Superscript = true;

				this.RenderSpan(Nr.ToString(CultureInfo.InvariantCulture));

				this.Superscript = Bak;

				if (Footnote is not null)
					Footnote.Referenced = true;
			}
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override Task Render(HashTag Element)
		{
			this.RenderSpan(Element.Tag);
			return Task.CompletedTask;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override Task Render(HtmlEntity Element)
		{
			string s = Waher.Content.Html.HtmlEntity.EntityToCharacter(Element.Entity);
			if (!string.IsNullOrEmpty(s))
				this.RenderSpan(s);

			return Task.CompletedTask;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override Task Render(HtmlEntityUnicode Element)
		{
			this.RenderSpan(new string((char)Element.Code, 1));
			return Task.CompletedTask;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override Task Render(InlineCode Element)
		{
			bool Bak = this.Code;
			this.Code = true;

			this.RenderSpan(Element.Code);

			this.Code = Bak;

			return Task.CompletedTask;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override Task Render(InlineHTML Element)
		{
			this.XmlOutput.WriteComment(Element.HTML);
			return Task.CompletedTask;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override async Task Render(InlineScript Element)
		{
			object Result = await Element.EvaluateExpression();
			await this.RenderObject(Result, Element.AloneInParagraph, Element.Variables);
		}

		/// <summary>
		/// Generates Maui XAML from Script output.
		/// </summary>
		/// <param name="Result">Script output.</param>
		/// <param name="AloneInParagraph">If the script output is to be presented alone in a paragraph.</param>
		/// <param name="Variables">Current variables.</param>
		public async Task RenderObject(object? Result, bool AloneInParagraph, Variables Variables)
		{
			if (Result is null)
				return;

			string? s;

			if (Result is XmlDocument Xml)
				Result = await MarkdownDocument.TransformXml(Xml, Variables);
			else if (Result is IToMatrix ToMatrix)
				Result = ToMatrix.ToMatrix();

			if (this.InLabel)
			{
				s = Result?.ToString();
				if (!string.IsNullOrEmpty(s))
					this.RenderSpan(Result?.ToString() ?? string.Empty);

				return;
			}

			if (Result is Graph G)
			{
				PixelInformation Pixels = G.CreatePixels(Variables);
				byte[] Bin = Pixels.EncodeAsPng();

				s = "data:image/png;base64," + Convert.ToBase64String(Bin, 0, Bin.Length);

				await Multimedia.ImageContent.OutputMauiXaml(this.XmlOutput, new Waher.Content.Emoji.ImageSource()
				{
					Url = s,
					Width = Pixels.Width,
					Height = Pixels.Height
				});
			}
			else if (Result is SKImage Img)
			{
				using SKData Data = Img.Encode(SKEncodedImageFormat.Png, 100);
				byte[] Bin = Data.ToArray();

				s = "data:image/png;base64," + Convert.ToBase64String(Bin, 0, Bin.Length);

				await Multimedia.ImageContent.OutputMauiXaml(this.XmlOutput, new Waher.Content.Emoji.ImageSource()
				{
					Url = s,
					Width = Img.Width,
					Height = Img.Height
				});
			}
			else if (Result is MarkdownDocument Doc)
			{
				await this.RenderDocument(Doc, true);   // Does not call ProcessAsyncTasks()
				Doc.ProcessAsyncTasks();
			}
			else if (Result is MarkdownContent Markdown)
			{
				Doc = await MarkdownDocument.CreateAsync(Markdown.Markdown);
				await this.RenderDocument(Doc, true);   // Does not call ProcessAsyncTasks()
				Doc.ProcessAsyncTasks();
			}
			else if (Result is Exception ex)
			{
				ex = Log.UnnestException(ex);

				if (ex is AggregateException ex2)
				{
					foreach (Exception ex3 in ex2.InnerExceptions)
					{
						this.RenderContentView();
						this.XmlOutput.WriteStartElement("Label");
						this.XmlOutput.WriteAttributeString("LineBreakMode", "WordWrap");
						this.XmlOutput.WriteAttributeString("TextColor", "Red");
						this.XmlOutput.WriteValue(ex3.Message);
						this.XmlOutput.WriteEndElement();
						this.XmlOutput.WriteEndElement();
					}
				}
				else
				{
					if (AloneInParagraph)
						this.RenderContentView();

					this.XmlOutput.WriteStartElement("Label");
					this.XmlOutput.WriteAttributeString("LineBreakMode", "WordWrap");
					this.XmlOutput.WriteAttributeString("TextColor", "Red");
					this.XmlOutput.WriteValue(ex.Message);
					this.XmlOutput.WriteEndElement();

					if (AloneInParagraph)
						this.XmlOutput.WriteEndElement();
				}
			}
			else
			{
				if (AloneInParagraph)
					this.RenderContentView();

				this.XmlOutput.WriteStartElement("Label");
				this.XmlOutput.WriteAttributeString("LineBreakMode", "WordWrap");

				this.RenderLabelAlignment();
				this.XmlOutput.WriteValue(Result.ToString());
				this.XmlOutput.WriteEndElement();

				if (AloneInParagraph)
					this.XmlOutput.WriteEndElement();
			}
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override Task Render(InlineText Element)
		{
			this.RenderSpan(Element.Value);
			return Task.CompletedTask;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override async Task Render(Insert Element)
		{
			bool Bak = this.Underline;
			this.Underline = true;

			await this.RenderChildren(Element);

			this.Underline = Bak;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override Task Render(LineBreak Element)
		{
			this.RenderSpan(Environment.NewLine);
			return Task.CompletedTask;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override async Task Render(Link Element)
		{
			string? Bak = this.Hyperlink;
			this.Hyperlink = Element.Url;

			await this.RenderChildren(Element);

			this.Hyperlink = Bak;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override async Task Render(LinkReference Element)
		{
			Waher.Content.Markdown.Model.SpanElements.Multimedia Multimedia = this.Document.GetReference(Element.Label);

			string? Bak = this.Hyperlink;

			if (Multimedia is not null)
				this.Hyperlink = Multimedia.Items[0].Url;

			await this.RenderChildren(Element);

			this.Hyperlink = Bak;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override Task Render(MetaReference Element)
		{
			StringBuilder sb = new();
			bool FirstOnRow = true;

			if (Element.TryGetMetaData(out KeyValuePair<string, bool>[] Values))
			{
				foreach (KeyValuePair<string, bool> P in Values)
				{
					if (FirstOnRow)
						FirstOnRow = false;
					else
						sb.Append(' ');

					sb.Append(P.Key);
					if (P.Value)
					{
						sb.Append(Environment.NewLine);
						FirstOnRow = true;
					}
				}
			}

			this.RenderSpan(sb.ToString());

			return Task.CompletedTask;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override Task Render(Waher.Content.Markdown.Model.SpanElements.Multimedia Element)
		{
			IMultimediaMauiXamlRenderer Renderer = Element.MultimediaHandler<IMultimediaMauiXamlRenderer>();
			if (Renderer is null)
				return this.RenderChildren(Element);
			else
				return Renderer.RenderMauiXaml(this, Element.Items, Element.Children, Element.AloneInParagraph, Element.Document);
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override Task Render(MultimediaReference Element)
		{
			Waher.Content.Markdown.Model.SpanElements.Multimedia Multimedia = Element.Document.GetReference(Element.Label);

			if (Multimedia is not null)
			{
				IMultimediaMauiXamlRenderer Renderer = Multimedia.MultimediaHandler<IMultimediaMauiXamlRenderer>();
				if (Renderer is not null)
					return Renderer.RenderMauiXaml(this, Multimedia.Items, Element.Children, Element.AloneInParagraph, Element.Document);
			}

			return this.RenderChildren(Element);
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override async Task Render(StrikeThrough Element)
		{
			bool Bak = this.StrikeThrough;
			this.StrikeThrough = true;

			await this.RenderChildren(Element);

			this.StrikeThrough = Bak;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override async Task Render(Strong Element)
		{
			bool Bak = this.Bold;
			this.Bold = true;

			await this.RenderChildren(Element);

			this.Bold = Bak;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override async Task Render(SubScript Element)
		{
			bool Bak = this.Subscript;
			this.Subscript = true;

			await this.RenderChildren(Element);

			this.Subscript = Bak;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override async Task Render(SuperScript Element)
		{
			bool Bak = this.Superscript;
			this.Superscript = true;

			await this.RenderChildren(Element);

			this.Superscript = Bak;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override async Task Render(Underline Element)
		{
			bool Bak = this.Underline;
			this.Underline = true;

			await this.RenderChildren(Element);

			this.Underline = Bak;
		}

		#endregion

		#region Block elements
		
		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override async Task Render(BlockQuote Element)
		{
			this.XmlOutput.WriteStartElement("ContentView");
			this.XmlOutput.WriteAttributeString("Padding", SmallMargins(true, false, true, true));

			this.XmlOutput.WriteStartElement("Frame");
			this.XmlOutput.WriteAttributeString("Padding", SmallMargins(true, true, false, false));
			this.XmlOutput.WriteAttributeString("BorderColor", "{AppThemeBinding Light={StaticResource PrimaryForegroundLight}, Dark={StaticResource PrimaryForegroundDark}}");
			// TODO: Border thickness

			this.XmlOutput.WriteStartElement("VerticalStackLayout");

			await this.RenderChildren(Element);

			this.XmlOutput.WriteEndElement();
			this.XmlOutput.WriteEndElement();
			this.XmlOutput.WriteEndElement();
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override async Task Render(BulletList Element)
		{
			int Row = 0;
			bool ParagraphBullet;

			this.XmlOutput.WriteStartElement("ContentView");
			this.XmlOutput.WriteAttributeString("Padding", SmallMargins(false, false, true, true));

			this.XmlOutput.WriteStartElement("Grid");
			this.XmlOutput.WriteAttributeString("RowSpacing", "0");
			this.XmlOutput.WriteAttributeString("ColumnSpacing", "0");

			this.XmlOutput.WriteStartElement("Grid.ColumnDefinitions");

			this.XmlOutput.WriteStartElement("ColumnDefinition");
			this.XmlOutput.WriteAttributeString("Width", "Auto");
			this.XmlOutput.WriteEndElement();

			this.XmlOutput.WriteStartElement("ColumnDefinition");
			this.XmlOutput.WriteAttributeString("Width", "*");
			this.XmlOutput.WriteEndElement();

			this.XmlOutput.WriteEndElement();
			this.XmlOutput.WriteStartElement("Grid.RowDefinitions");

			foreach (MarkdownElement _ in Element.Children)
			{
				this.XmlOutput.WriteStartElement("RowDefinition");
				this.XmlOutput.WriteAttributeString("Height", "Auto");
				this.XmlOutput.WriteEndElement();
			}

			this.XmlOutput.WriteEndElement();

			foreach (MarkdownElement E in Element.Children)
			{
				if (E is UnnumberedItem Item)
				{
					ParagraphBullet = !E.InlineSpanElement || E.OutsideParagraph;
					GetMargins(E, out bool TopMargin, out bool BottomMargin);

					this.RenderContentView(SmallMargins(false, true, TopMargin, BottomMargin));

					this.XmlOutput.WriteAttributeString("Grid.Column", "0");
					this.XmlOutput.WriteAttributeString("Grid.Row", Row.ToString(CultureInfo.InvariantCulture));

					this.XmlOutput.WriteElementString("Label", "•");
					this.XmlOutput.WriteEndElement();

					this.XmlOutput.WriteStartElement("VerticalStackLayout");
					this.XmlOutput.WriteAttributeString("Grid.Column", "1");
					this.XmlOutput.WriteAttributeString("Grid.Row", Row.ToString(CultureInfo.InvariantCulture));

					if (ParagraphBullet)
						await E.Render(this);
					else
						await this.RenderLabel(Item, false);

					this.XmlOutput.WriteEndElement();
				}

				Row++;
			}

			this.XmlOutput.WriteEndElement();
			this.XmlOutput.WriteEndElement();
		}

		private static string SmallMargins(bool Left, bool Right, bool Top, bool Bottom)
		{
			StringBuilder sb = new();

			sb.Append("{StaticResource ");

			if (!(Left || Right || Top || Bottom))
				sb.Append("No");
			else
			{
				sb.Append("Small");

				if (!(Left && Right && Top && Bottom))
				{
					if (Left)
						sb.Append("Left");

					if (Right)
						sb.Append("Right");

					if (Top)
						sb.Append("Top");

					if (Bottom)
						sb.Append("Bottom");
				}
			}

			sb.Append("Margins}");

			return sb.ToString();
		}

		/// <summary>
		/// Gets margins for content.
		/// </summary>
		/// <param name="Element">Element to render.</param>
		/// <param name="TopMargin">Top margin.</param>
		/// <param name="BottomMargin">Bottom margin.</param>
		private static void GetMargins(MarkdownElement Element, out bool TopMargin, out bool BottomMargin)
		{
			if (Element.InlineSpanElement && !Element.OutsideParagraph)
			{
				TopMargin = false;
				BottomMargin = false;
			}
			else if (Element is NestedBlock NestedBlock)
			{
				bool First = true;

				TopMargin = BottomMargin = false;

				foreach (MarkdownElement E in NestedBlock.Children)
				{
					if (First)
					{
						First = false;
						GetMargins(E, out TopMargin, out BottomMargin);
					}
					else
						GetMargins(E, out bool _, out BottomMargin);
				}
			}
			else if (Element is MarkdownElementSingleChild SingleChild)
				GetMargins(SingleChild.Child, out TopMargin, out BottomMargin);
			else
			{
				TopMargin = true;
				BottomMargin = true;
			}
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override async Task Render(CenterAligned Element)
		{
			this.XmlOutput.WriteStartElement("VerticalStackLayout");

			Waher.Content.Markdown.Model.TextAlignment Bak = this.Alignment;
			this.Alignment = Waher.Content.Markdown.Model.TextAlignment.Center;

			await this.RenderChildren(Element);

			this.Alignment = Bak;
			this.XmlOutput.WriteEndElement();
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override async Task Render(CodeBlock Element)
		{
			ICodeContentMauiXamlRenderer Renderer = Element.CodeContentHandler<ICodeContentMauiXamlRenderer>();

			if (Renderer is not null)
			{
				try
				{
					if (await Renderer.RenderMauiXaml(this, Element.Rows, Element.Language, Element.Indent, Element.Document))
						return;
				}
				catch (Exception ex)
				{
					ex = Log.UnnestException(ex);

					if (ex is AggregateException ex2)
					{
						foreach (Exception ex3 in ex2.InnerExceptions)
						{
							this.RenderContentView();
							this.XmlOutput.WriteStartElement("Label");
							this.XmlOutput.WriteAttributeString("LineBreakMode", "WordWrap");
							this.XmlOutput.WriteAttributeString("TextColor", "Red");
							this.XmlOutput.WriteValue(ex3.Message);
							this.XmlOutput.WriteEndElement();
							this.XmlOutput.WriteEndElement();
						}
					}
					else
					{
						this.RenderContentView();
						this.XmlOutput.WriteStartElement("Label");
						this.XmlOutput.WriteAttributeString("LineBreakMode", "WordWrap");
						this.XmlOutput.WriteAttributeString("TextColor", "Red");
						this.XmlOutput.WriteValue(ex.Message);
						this.XmlOutput.WriteEndElement();
						this.XmlOutput.WriteEndElement();
					}
				}
			}

			this.RenderContentView();
			this.XmlOutput.WriteStartElement("VerticalStackLayout");

			int i;

			for (i = Element.Start; i <= Element.End; i++)
			{
				this.XmlOutput.WriteStartElement("Label");
				this.XmlOutput.WriteAttributeString("LineBreakMode", "NoWrap");
				this.RenderLabelAlignment();
				this.XmlOutput.WriteAttributeString("FontFamily", "SpaceGroteskRegular");
				this.XmlOutput.WriteAttributeString("Text", Element.Rows[i]);
				this.XmlOutput.WriteEndElement();
			}

			this.XmlOutput.WriteEndElement();
			this.XmlOutput.WriteEndElement();
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override Task Render(CommentBlock Element)
		{
			return Task.CompletedTask;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override async Task Render(DefinitionDescriptions Element)
		{
			MarkdownElement? Last = null;

			foreach (MarkdownElement Description in Element.Children)
				Last = Description;

			foreach (MarkdownElement Description in Element.Children)
			{
				if (Description.InlineSpanElement && !Description.OutsideParagraph)
				{
					this.RenderContentView();

					this.XmlOutput.WriteStartElement("Label");
					this.XmlOutput.WriteAttributeString("LineBreakMode", "WordWrap");
					this.RenderLabelAlignment();
					this.XmlOutput.WriteAttributeString("TextType", "Html");

					using (HtmlRenderer Renderer = new(new HtmlSettings()
					{
						XmlEntitiesOnly = true
					}, this.Document))
					{
						await Description.Render(Renderer);
						this.XmlOutput.WriteCData(Renderer.ToString());
					}

					this.XmlOutput.WriteEndElement();
					this.XmlOutput.WriteEndElement();
				}
				else
				{
					this.XmlOutput.WriteStartElement("ContentView");
					this.XmlOutput.WriteAttributeString("Padding", SmallMargins(true, false, false, Description == Last));

					this.XmlOutput.WriteStartElement("VerticalStackLayout");
					await Description.Render(this);
					this.XmlOutput.WriteEndElement();

					this.XmlOutput.WriteEndElement();
				}
			}
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override Task Render(DefinitionList Element)
		{
			return this.RenderChildren(Element);
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override async Task Render(DefinitionTerms Element)
		{
			bool Top = true;

			foreach (MarkdownElement Term in Element.Children)
			{
				this.RenderContentView(SmallMargins(true, true, Top, false));

				bool BoldBak = this.Bold;
				this.Bold = true;

				await this.RenderLabel(Term, true);

				this.Bold = BoldBak;
				this.XmlOutput.WriteEndElement();

				Top = false;
			}
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override async Task Render(DeleteBlocks Element)
		{
			this.XmlOutput.WriteStartElement("ContentView");
			this.XmlOutput.WriteAttributeString("Padding", SmallMargins(true, false, true, true));

			this.XmlOutput.WriteStartElement("Frame");
			this.XmlOutput.WriteAttributeString("Padding", SmallMargins(true, true, false, false));
			this.XmlOutput.WriteAttributeString("BorderColor", "{AppThemeBinding Light={StaticResource DeletedBorderLight}, Dark={StaticResource DeletedBorderDark}}");
			// TODO: Border thickness

			this.XmlOutput.WriteStartElement("VerticalStackLayout");

			foreach (MarkdownElement E in Element.Children)
				await E.Render(this);

			this.XmlOutput.WriteEndElement();
			this.XmlOutput.WriteEndElement();
			this.XmlOutput.WriteEndElement();
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override Task Render(Footnote Element)
		{
			return this.RenderChildren(Element);
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override async Task Render(Header Element)
		{
			this.RenderContentView();

			int Level = Math.Max(0, Math.Min(9, Element.Level));

			this.XmlOutput.WriteStartElement("Label");
			this.XmlOutput.WriteAttributeString("LineBreakMode", "WordWrap");
			this.XmlOutput.WriteAttributeString("Style", "{StaticResource Header" + Level.ToString(CultureInfo.InvariantCulture) + "}");
			this.RenderLabelAlignment();

			this.XmlOutput.WriteAttributeString("TextType", "Html");

			using (HtmlRenderer Renderer = new(new HtmlSettings()
			{
				XmlEntitiesOnly = true
			}, this.Document))
			{
				await Renderer.RenderChildren(Element);

				this.XmlOutput.WriteCData(Renderer.ToString());
			}

			this.XmlOutput.WriteEndElement();
			this.XmlOutput.WriteEndElement();
		}

		/// <summary>
		/// Writes a text-alignment attribute to a Maui label element.
		/// </summary>
		public void RenderLabelAlignment()
		{
			switch (this.Alignment)
			{
				case Waher.Content.Markdown.Model.TextAlignment.Left:
					this.XmlOutput.WriteAttributeString("HorizontalTextAlignment", "Start");
					break;

				case Waher.Content.Markdown.Model.TextAlignment.Right:
					this.XmlOutput.WriteAttributeString("HorizontalTextAlignment", "End");
					break;

				case Waher.Content.Markdown.Model.TextAlignment.Center:
					this.XmlOutput.WriteAttributeString("HorizontalTextAlignment", "Center");
					break;
			}
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override Task Render(HorizontalRule Element)
		{
			this.XmlOutput.WriteStartElement("BoxView");
			this.XmlOutput.WriteAttributeString("HeightRequest", "1");
			this.XmlOutput.WriteAttributeString("BackgroundColor", "{AppThemeBinding Light={StaticResource NormalEditPlaceholderLight}, Dark={StaticResource NormalEditPlaceholderDark}}");
			this.XmlOutput.WriteAttributeString("HorizontalOptions", "FillAndExpand");
			this.XmlOutput.WriteAttributeString("Margin", SmallMargins(false, false, true, true));
			this.XmlOutput.WriteEndElement();

			return Task.CompletedTask;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override async Task Render(HtmlBlock Element)
		{
			this.RenderContentView();

			this.XmlOutput.WriteStartElement("Label");
			this.XmlOutput.WriteAttributeString("LineBreakMode", "WordWrap");
			this.RenderLabelAlignment();
			this.XmlOutput.WriteAttributeString("TextType", "Html");

			using HtmlRenderer Renderer = new(new HtmlSettings()
			{
				XmlEntitiesOnly = true
			}, this.Document);

			await Renderer.RenderChildren(Element);

			this.XmlOutput.WriteCData(Renderer.ToString());

			this.XmlOutput.WriteEndElement();
			this.XmlOutput.WriteEndElement();
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override async Task Render(InsertBlocks Element)
		{
			this.XmlOutput.WriteStartElement("ContentView");
			this.XmlOutput.WriteAttributeString("Padding", SmallMargins(true, false, true, true));

			this.XmlOutput.WriteStartElement("Frame");
			this.XmlOutput.WriteAttributeString("Padding", SmallMargins(true, true, false, false));
			this.XmlOutput.WriteAttributeString("BorderColor", "{AppThemeBinding Light={StaticResource InsertedBorderLight}, Dark={StaticResource InsertedBorderDark}}");
			// TODO: Border thickness

			this.XmlOutput.WriteStartElement("VerticalStackLayout");

			await this.RenderChildren(Element);

			this.XmlOutput.WriteEndElement();
			this.XmlOutput.WriteEndElement();
			this.XmlOutput.WriteEndElement();
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override Task Render(InvisibleBreak Element)
		{
			return Task.CompletedTask;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override async Task Render(LeftAligned Element)
		{
			this.XmlOutput.WriteStartElement("VerticalStackLayout");

			Waher.Content.Markdown.Model.TextAlignment Bak = this.Alignment;
			this.Alignment = Waher.Content.Markdown.Model.TextAlignment.Left;

			await this.RenderChildren(Element);

			this.Alignment = Bak;
			this.XmlOutput.WriteEndElement();
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override async Task Render(MarginAligned Element)
		{
			this.XmlOutput.WriteStartElement("VerticalStackLayout");

			Waher.Content.Markdown.Model.TextAlignment Bak = this.Alignment;
			this.Alignment = Waher.Content.Markdown.Model.TextAlignment.Left;

			await this.RenderChildren(Element);

			this.Alignment = Bak;
			this.XmlOutput.WriteEndElement();
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override async Task Render(NestedBlock Element)
		{
			if (Element.HasOneChild)
				await Element.FirstChild.Render(this);
			else
			{
				HtmlSettings Settings = new()
				{
					XmlEntitiesOnly = true
				};
				HtmlRenderer? Html = null;

				try
				{
					foreach (MarkdownElement E in Element.Children)
					{
						if (E.InlineSpanElement)
						{
							Html ??= new HtmlRenderer(Settings, this.Document);
							await E.Render(Html);
						}
						else
						{
							if (Html is not null)
							{
								this.XmlOutput.WriteStartElement("Label");
								this.XmlOutput.WriteAttributeString("LineBreakMode", "WordWrap");
								this.RenderLabelAlignment();
								this.XmlOutput.WriteAttributeString("TextType", "Html");
								this.XmlOutput.WriteCData(Html.ToString());
								this.XmlOutput.WriteEndElement();

								Html.Dispose();
								Html = null;
							}

							await E.Render(this);
						}
					}

					if (Html is not null)
					{
						this.XmlOutput.WriteStartElement("Label");
						this.XmlOutput.WriteAttributeString("LineBreakMode", "WordWrap");
						this.RenderLabelAlignment();
						this.XmlOutput.WriteAttributeString("TextType", "Html");
						this.XmlOutput.WriteCData(Html.ToString());
						this.XmlOutput.WriteEndElement();
					}
				}
				finally
				{
					Html?.Dispose();
				}
			}
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override Task Render(NumberedItem Element)
		{
			return this.RenderChild(Element);
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override async Task Render(NumberedList Element)
		{
			int Expected = 0;
			int Row = 0;
			bool ParagraphBullet;

			this.XmlOutput.WriteStartElement("ContentView");
			this.XmlOutput.WriteAttributeString("Padding", SmallMargins(false, false, true, true));

			this.XmlOutput.WriteStartElement("Grid");
			this.XmlOutput.WriteAttributeString("RowSpacing", "0");
			this.XmlOutput.WriteAttributeString("ColumnSpacing", "0");

			this.XmlOutput.WriteStartElement("Grid.ColumnDefinitions");

			this.XmlOutput.WriteStartElement("ColumnDefinition");
			this.XmlOutput.WriteAttributeString("Width", "Auto");
			this.XmlOutput.WriteEndElement();

			this.XmlOutput.WriteStartElement("ColumnDefinition");
			this.XmlOutput.WriteAttributeString("Width", "*");
			this.XmlOutput.WriteEndElement();

			this.XmlOutput.WriteEndElement();
			this.XmlOutput.WriteStartElement("Grid.RowDefinitions");

			foreach (MarkdownElement _ in Element.Children)
			{
				this.XmlOutput.WriteStartElement("RowDefinition");
				this.XmlOutput.WriteAttributeString("Height", "Auto");
				this.XmlOutput.WriteEndElement();
			}

			this.XmlOutput.WriteEndElement();

			foreach (MarkdownElement E in Element.Children)
			{
				if (E is BlockElementSingleChild Item)
				{
					Expected++;

					ParagraphBullet = !E.InlineSpanElement || E.OutsideParagraph;
					GetMargins(E, out bool TopMargin, out bool BottomMargin);

					this.RenderContentView(SmallMargins(false, true, TopMargin, BottomMargin));
					this.XmlOutput.WriteAttributeString("Grid.Column", "0");
					this.XmlOutput.WriteAttributeString("Grid.Row", Row.ToString(CultureInfo.InvariantCulture));

					this.XmlOutput.WriteStartElement("Label");

					if (Item is NumberedItem NumberedItem)
						this.XmlOutput.WriteValue((Expected = NumberedItem.Number).ToString(CultureInfo.InvariantCulture));
					else
						this.XmlOutput.WriteValue(Expected.ToString(CultureInfo.InvariantCulture));

					this.XmlOutput.WriteValue(".");
					this.XmlOutput.WriteEndElement();
					this.XmlOutput.WriteEndElement();

					this.XmlOutput.WriteStartElement("VerticalStackLayout");
					this.XmlOutput.WriteAttributeString("Grid.Column", "1");
					this.XmlOutput.WriteAttributeString("Grid.Row", Row.ToString(CultureInfo.InvariantCulture));

					if (ParagraphBullet)
						await E.Render(this);
					else
						await this.RenderLabel(Item, false);

					this.XmlOutput.WriteEndElement();
				}

				Row++;
			}

			this.XmlOutput.WriteEndElement();
			this.XmlOutput.WriteEndElement();
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override async Task Render(Paragraph Element)
		{
			this.RenderContentView();
			await this.RenderLabel(Element, false);
			this.XmlOutput.WriteEndElement();
		}

		internal async Task RenderLabel(MarkdownElement Element, bool IncludeElement)
		{
			bool HasLink = !Element.ForEach((E, _) =>
			{
				return !(
					E is AutomaticLinkMail ||
					E is AutomaticLinkUrl ||
					E is Link ||
					E is LinkReference);
			}, null);

			this.XmlOutput.WriteStartElement("Label");
			this.XmlOutput.WriteAttributeString("LineBreakMode", "WordWrap");
			this.RenderLabelAlignment();

			if (HasLink)
			{
				if (this.InLabel)
				{
					if (IncludeElement)
						await Element.Render(this);
					else
						await this.RenderChildren(Element);
				}
				else
				{
					this.InLabel = true;

					this.XmlOutput.WriteStartElement("Label.FormattedText");
					this.XmlOutput.WriteStartElement("FormattedString");

					if (IncludeElement)
						await Element.Render(this);
					else
						await this.RenderChildren(Element);

					this.XmlOutput.WriteEndElement();
					this.XmlOutput.WriteEndElement();

					this.InLabel = false;
				}
			}
			else
			{
				this.XmlOutput.WriteAttributeString("TextType", "Html");

				if (this.Bold)
					this.XmlOutput.WriteAttributeString("FontAttributes", "Bold");

				using HtmlRenderer Renderer = new(new HtmlSettings()
				{
					XmlEntitiesOnly = true
				}, this.Document);

				if (IncludeElement)
					await Element.Render(Renderer);
				else
					await Renderer.RenderChildren(Element);

				this.XmlOutput.WriteCData(Renderer.ToString());
			}

			this.XmlOutput.WriteEndElement();
		}

		internal void RenderSpan(string Text)
		{
			if (!this.InLabel)
			{
				this.XmlOutput.WriteStartElement("Label");
				this.XmlOutput.WriteAttributeString("LineBreakMode", "WordWrap");
				this.RenderLabelAlignment();
				this.XmlOutput.WriteStartElement("Label.FormattedText");
				this.XmlOutput.WriteStartElement("FormattedString");
			}

			this.XmlOutput.WriteStartElement("Span");

			if (this.Superscript)
				Text = TextRenderer.ToSuperscript(Text);
			else if (this.Subscript)
				Text = TextRenderer.ToSubscript(Text);

			this.XmlOutput.WriteAttributeString("Text", Text);

			if (this.Bold && this.Italic)
				this.XmlOutput.WriteAttributeString("FontAttributes", "Italic, Bold");
			else if (this.Bold)
				this.XmlOutput.WriteAttributeString("FontAttributes", "Bold");
			else if (this.Italic)
				this.XmlOutput.WriteAttributeString("FontAttributes", "Italic");

			if (this.StrikeThrough && this.Underline)
				this.XmlOutput.WriteAttributeString("TextDecorations", "Strikethrough, Underline");
			else if (this.StrikeThrough)
				this.XmlOutput.WriteAttributeString("TextDecorations", "Strikethrough");
			else if (this.Underline)
				this.XmlOutput.WriteAttributeString("TextDecorations", "Underline");

			if (this.Code)
				this.XmlOutput.WriteAttributeString("FontFamily", "SpaceGroteskRegular");

			if (this.Hyperlink is not null)
			{
				this.XmlOutput.WriteAttributeString("TextColor", "{AppThemeBinding Light={StaticResource AccentForegroundLight}, Dark={StaticResource AccentForegroundDark}}");

				this.XmlOutput.WriteStartElement("Span.GestureRecognizers");
				this.XmlOutput.WriteStartElement("TapGestureRecognizer");
				this.XmlOutput.WriteAttributeString("Command", "{Binding HyperlinkClicked}");
				this.XmlOutput.WriteAttributeString("CommandParameter", this.Hyperlink);
				this.XmlOutput.WriteEndElement();
				this.XmlOutput.WriteEndElement();
			}

			if (!this.InLabel)
			{
				this.XmlOutput.WriteEndElement();
				this.XmlOutput.WriteEndElement();
				this.XmlOutput.WriteEndElement();
			}

			this.XmlOutput.WriteEndElement();
		}

		internal void RenderContentView()
		{
			this.RenderContentView(this.Alignment, SmallMargins(false, false, true, true), null);
		}

		internal void RenderContentView(string Margins)
		{
			this.RenderContentView(this.Alignment, Margins, null);
		}

		internal void RenderContentView(Waher.Content.Markdown.Model.TextAlignment Alignment, string? Margins)
		{
			this.RenderContentView(Alignment, Margins, null);
		}

		internal void RenderContentView(Waher.Content.Markdown.Model.TextAlignment Alignment, string? Margins, string? Style)
		{
			this.XmlOutput.WriteStartElement("ContentView");

			if (!string.IsNullOrEmpty(Margins))
				this.XmlOutput.WriteAttributeString("Padding", Margins);

			if (!string.IsNullOrEmpty(Style))
				this.XmlOutput.WriteAttributeString("Style", "{StaticResource " + Style + "}");

			switch (Alignment)
			{
				case Waher.Content.Markdown.Model.TextAlignment.Center:
					this.XmlOutput.WriteAttributeString("HorizontalOptions", "Center");
					break;

				case Waher.Content.Markdown.Model.TextAlignment.Left:
					this.XmlOutput.WriteAttributeString("HorizontalOptions", "Start");
					break;

				case Waher.Content.Markdown.Model.TextAlignment.Right:
					this.XmlOutput.WriteAttributeString("HorizontalOptions", "End");
					break;
			}
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override async Task Render(RightAligned Element)
		{
			this.XmlOutput.WriteStartElement("VerticalStackLayout");

			Waher.Content.Markdown.Model.TextAlignment Bak = this.Alignment;
			this.Alignment = Waher.Content.Markdown.Model.TextAlignment.Right;

			await this.RenderChildren(Element);

			this.Alignment = Bak;
			this.XmlOutput.WriteEndElement();
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override Task Render(Sections Element)
		{
			return this.RenderChildren(Element);
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override Task Render(SectionSeparator Element)
		{
			this.XmlOutput.WriteStartElement("BoxView");
			this.XmlOutput.WriteAttributeString("HeightRequest", "1");
			this.XmlOutput.WriteAttributeString("BackgroundColor", "{AppThemeBinding Light={StaticResource NormalEditPlaceholderLight}, Dark={StaticResource NormalEditPlaceholderDark}}");
			this.XmlOutput.WriteAttributeString("HorizontalOptions", "FillAndExpand");
			this.XmlOutput.WriteAttributeString("Margin", SmallMargins(false, false, true, true));
			this.XmlOutput.WriteEndElement();

			return Task.CompletedTask;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override async Task Render(Table Element)
		{
			int Column;
			int Row, NrRows;
			int RowNr = 0;

			this.XmlOutput.WriteStartElement("ScrollView");
			this.XmlOutput.WriteAttributeString("Orientation", "Horizontal");
			this.XmlOutput.WriteStartElement("ContentView");
			this.XmlOutput.WriteAttributeString("Padding", SmallMargins(false, false, true, true));

			this.XmlOutput.WriteStartElement("Grid");
			this.XmlOutput.WriteAttributeString("RowSpacing", "-2");
			this.XmlOutput.WriteAttributeString("ColumnSpacing", "-2");

			// TODO: Tooltip/caption

			this.XmlOutput.WriteStartElement("Grid.ColumnDefinitions");

			for (Column = 0; Column < Element.Columns; Column++)
			{
				this.XmlOutput.WriteStartElement("ColumnDefinition");
				this.XmlOutput.WriteAttributeString("Width", "Auto");
				this.XmlOutput.WriteEndElement();
			}

			this.XmlOutput.WriteEndElement();
			this.XmlOutput.WriteStartElement("Grid.RowDefinitions");

			for (Row = 0, NrRows = Element.Rows.Length + Element.Headers.Length; Row < NrRows; Row++)
			{
				this.XmlOutput.WriteStartElement("RowDefinition");
				this.XmlOutput.WriteAttributeString("Height", "Auto");
				this.XmlOutput.WriteEndElement();
			}

			this.XmlOutput.WriteEndElement();

			for (Row = 0, NrRows = Element.Headers.Length; Row < NrRows; Row++, RowNr++)
				await this.Render(Element.Headers[Row], Element.HeaderCellAlignments[Row], RowNr, true, Element);

			for (Row = 0, NrRows = Element.Rows.Length; Row < NrRows; Row++, RowNr++)
				await this.Render(Element.Rows[Row], Element.RowCellAlignments[Row], RowNr, false, Element);

			this.XmlOutput.WriteEndElement();
			this.XmlOutput.WriteEndElement();
			this.XmlOutput.WriteEndElement();

		}

		private void ClearState()
		{
			this.Alignment = Waher.Content.Markdown.Model.TextAlignment.Left;
			this.Bold = false;
			this.Italic = false;
			this.StrikeThrough = false;
			this.Underline = false;
			this.Superscript = false;
			this.Subscript = false;
			this.Code = false;
			this.InLabel = false;
			this.Hyperlink = null;
		}

		private StateBackup Backup()
		{
			return new StateBackup()
			{
				Alignment = this.Alignment,
				Bold = this.Bold,
				Italic = this.Italic,
				StrikeThrough = this.StrikeThrough,
				Underline = this.Underline,
				Superscript = this.Superscript,
				Subscript = this.Subscript,
				Code = this.Code,
				InLabel = this.InLabel,
				Hyperlink = this.Hyperlink
			};
		}

		private void Restore(StateBackup Backup)
		{
			this.Alignment = Backup.Alignment;
			this.Bold = Backup.Bold;
			this.Italic = Backup.Italic;
			this.StrikeThrough = Backup.StrikeThrough;
			this.Underline = Backup.Underline;
			this.Superscript = Backup.Superscript;
			this.Subscript = Backup.Subscript;
			this.Code = Backup.Code;
			this.InLabel = Backup.InLabel;
			this.Hyperlink = Backup.Hyperlink;
		}

		private class StateBackup
		{
			public Waher.Content.Markdown.Model.TextAlignment Alignment;
			public bool Bold;
			public bool Italic;
			public bool StrikeThrough;
			public bool Underline;
			public bool Superscript;
			public bool Subscript;
			public bool Code;
			public bool InLabel;
			public string? Hyperlink;
		}

		private async Task Render(MarkdownElement[] CurrentRow, Waher.Content.Markdown.Model.TextAlignment?[] CellAlignments,
			int RowNr, bool Bold, Table Element)
		{
			MarkdownElement E;
			Waher.Content.Markdown.Model.TextAlignment TextAlignment;
			int Column;
			int NrColumns = Element.Columns;
			int ColSpan;
			StateBackup Bak = this.Backup();

			this.ClearState();

			for (Column = 0; Column < NrColumns; Column++)
			{
				E = CurrentRow[Column];
				if (E is null)
					continue;

				TextAlignment = CellAlignments[Column] ?? Element.ColumnAlignments[Column];
				ColSpan = Column + 1;
				while (ColSpan < NrColumns && CurrentRow[ColSpan] is null)
					ColSpan++;

				ColSpan -= Column;

				this.XmlOutput.WriteStartElement("Frame");

				if ((RowNr & 1) == 0)
					this.XmlOutput.WriteAttributeString("Style", "{StaticResource TableCellEven}");
				else
					this.XmlOutput.WriteAttributeString("Style", "{StaticResource TableCellOdd}");

				this.XmlOutput.WriteAttributeString("Grid.Column", Column.ToString(CultureInfo.InvariantCulture));
				this.XmlOutput.WriteAttributeString("Grid.Row", RowNr.ToString(CultureInfo.InvariantCulture));

				if (ColSpan > 1)
					this.XmlOutput.WriteAttributeString("Grid.ColumnSpan", ColSpan.ToString(CultureInfo.InvariantCulture));

				if (E.InlineSpanElement)
				{
					this.RenderContentView(TextAlignment, null, "TableCell");

					this.Bold = Bold;
					await this.RenderLabel(E, true);
					this.Bold = false;

					this.XmlOutput.WriteEndElement();   // Paragraph
				}
				else
				{
					this.XmlOutput.WriteStartElement("ContentView");
					this.XmlOutput.WriteAttributeString("Style", null, "{StaticResource TableCell}");

					this.XmlOutput.WriteStartElement("VerticalStackLayout");
					await E.Render(this);
					this.XmlOutput.WriteEndElement();   // StackLayout

					this.XmlOutput.WriteEndElement();   // ContentView
				}

				this.XmlOutput.WriteEndElement();   // Frame
			}

			this.Restore(Bak);
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override Task Render(TaskItem Element)
		{
			return this.RenderChild(Element);
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override async Task Render(TaskList Element)
		{
			int Row = 0;
			bool ParagraphBullet;

			this.XmlOutput.WriteStartElement("ContentView");
			this.XmlOutput.WriteAttributeString("Padding", SmallMargins(false, false, true, true));

			this.XmlOutput.WriteStartElement("Grid");
			this.XmlOutput.WriteAttributeString("RowSpacing", "0");
			this.XmlOutput.WriteAttributeString("ColumnSpacing", "0");

			this.XmlOutput.WriteStartElement("Grid.ColumnDefinitions");

			this.XmlOutput.WriteStartElement("ColumnDefinition");
			this.XmlOutput.WriteAttributeString("Width", "Auto");
			this.XmlOutput.WriteEndElement();

			this.XmlOutput.WriteStartElement("ColumnDefinition");
			this.XmlOutput.WriteAttributeString("Width", "*");
			this.XmlOutput.WriteEndElement();

			this.XmlOutput.WriteEndElement();
			this.XmlOutput.WriteStartElement("Grid.RowDefinitions");

			foreach (MarkdownElement _ in Element.Children)
			{
				this.XmlOutput.WriteStartElement("RowDefinition");
				this.XmlOutput.WriteAttributeString("Height", "Auto");
				this.XmlOutput.WriteEndElement();
			}

			this.XmlOutput.WriteEndElement();

			foreach (MarkdownElement E in Element.Children)
			{
				if (E is TaskItem TaskItem)
				{
					ParagraphBullet = !E.InlineSpanElement || E.OutsideParagraph;
					GetMargins(E, out bool TopMargin, out bool BottomMargin);

					if (TaskItem.IsChecked)
					{
						this.RenderContentView(SmallMargins(false, true, TopMargin, BottomMargin));
						this.XmlOutput.WriteAttributeString("Grid.Column", "0");
						this.XmlOutput.WriteAttributeString("Grid.Row", Row.ToString(CultureInfo.InvariantCulture));

						this.XmlOutput.WriteElementString("Label", "✓");
						this.XmlOutput.WriteEndElement();
					}

					this.XmlOutput.WriteStartElement("VerticalStackLayout");
					this.XmlOutput.WriteAttributeString("Grid.Column", "1");
					this.XmlOutput.WriteAttributeString("Grid.Row", Row.ToString(CultureInfo.InvariantCulture));

					if (ParagraphBullet)
						await E.Render(this);
					else
						await this.RenderLabel(TaskItem, false);

					this.XmlOutput.WriteEndElement();
				}

				Row++;
			}

			this.XmlOutput.WriteEndElement();
			this.XmlOutput.WriteEndElement();
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public override Task Render(UnnumberedItem Element)
		{
			return this.RenderChild(Element);
		}

		#endregion

	}
}
