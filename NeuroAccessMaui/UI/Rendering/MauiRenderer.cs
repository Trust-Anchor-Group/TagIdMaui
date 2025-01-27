using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using CommunityToolkit.Maui.Markup;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using SkiaSharp;
using Waher.Content;
using Waher.Content.Emoji;
using Waher.Content.Markdown;
using Waher.Content.Markdown.Model;
using Waher.Content.Markdown.Model.BlockElements;
using Waher.Content.Markdown.Model.Multimedia;
using Waher.Content.Markdown.Model.SpanElements;
using Waher.Content.Markdown.Rendering;
using Waher.Content.Xml;
using Waher.Events;
using Waher.Script;
using Waher.Script.Constants;
using Waher.Script.Functions.Analytic;
using Waher.Script.Graphs;
using Waher.Script.Operators.Matrices;
using Color = Microsoft.Maui.Graphics.Color;
using ImageSource = Waher.Content.Emoji.ImageSource;
using MarkdownContent = Waher.Content.Markdown.MarkdownContent;
using Microsoft.Maui.Graphics.Text;

namespace NeuroAccessMaui.UI.Rendering
{
    /// <summary>
    /// Represents global rendering options for <see cref="MauiRenderer"/>.
    /// You can add more properties to control styling, spacing, etc.
    /// </summary>
    public class MauiRendererOptions
    {
        /// <summary>
        /// Spacing between elements in the vertical stack layout.
        /// </summary>
        public double StackLayoutSpacing { get; set; } = 10.0;

        /// <summary>
        /// Default margin to apply around content (if desired).
        /// </summary>
        public Thickness DefaultMargins { get; set; } = new Thickness(5);

        /// <summary>
        /// A Style for table cells (can be extended to hold more styles).
        /// </summary>
        public Style? DefaultTableCellStyle { get; set; } = null;
    }

    /// <summary>
    /// Encapsulates the current text/formatting state for the renderer.
    /// This allows nested formatting to push/pop state instead of
    /// copying booleans all over the place.
    /// </summary>
    public sealed class MauiRenderState
    {
        /// <summary>
        /// Current text alignment.
        /// </summary>
        public Waher.Content.Markdown.Model.TextAlignment Alignment = Waher.Content.Markdown.Model.TextAlignment.Left;

        /// <summary>
        /// Indicates if text is bold.
        /// </summary>
        public bool Bold = false;

        /// <summary>
        /// Indicates if text is italic.
        /// </summary>
        public bool Italic = false;

        /// <summary>
        /// Indicates if text is stricken through.
        /// </summary>
        public bool StrikeThrough = false;

        /// <summary>
        /// Indicates if text is underlined.
        /// </summary>
        public bool Underline = false;

        /// <summary>
        /// Indicates if text is superscript.
        /// </summary>
        public bool Superscript = false;

        /// <summary>
        /// Indicates if text is subscript.
        /// </summary>
        public bool Subscript = false;

        /// <summary>
        /// Indicates if text is inline code.
        /// </summary>
        public bool Code = false;

        /// <summary>
        /// Indicates if rendering is inside a label.
        /// </summary>
        public bool InLabel = false;

        /// <summary>
        /// Current hyperlink, if any.
        /// </summary>
        public string? Hyperlink = null;

        /// <summary>
        /// Creates a shallow copy of the current state, for easy push/pop.
        /// </summary>
        public MauiRenderState Clone()
        {
            return (MauiRenderState)this.MemberwiseClone();
        }
    }

    /// <summary>
    /// Renders MAUI objects from a Markdown document, applying recommended maintainable patterns.
    /// </summary>
    public class MauiRenderer : IRenderer
    {
        // ------------------------------------------------------------------------
        // Fields & Properties
        // ------------------------------------------------------------------------

        /// <summary>
        /// Reference to the current Markdown document being processed.
        /// </summary>
        public MarkdownDocument Document;

        /// <summary>
        /// Options controlling basic layout and styling.
        /// </summary>
        private readonly MauiRendererOptions rendererOptions;

        /// <summary>
        /// The top-level layout that gathers rendered content.
        /// </summary>
        private readonly VerticalStackLayout mainStackLayout;

        /// <summary>
        /// The current element (ContentView or Layout) being populated.
        /// </summary>
        private View currentElement;

        /// <summary>
        /// Stack of formatting states, so nested formatting can push/pop.
        /// </summary>
        private readonly Stack<MauiRenderState> stateStack = new();

        /// <summary>
        /// Easy access to the current top of the state stack.
        /// </summary>
        private MauiRenderState CurrentState => this.stateStack.Peek();

        /// <summary>
        /// Initializes a new instance of the <see cref="MauiRenderer"/> class.
        /// </summary>
        /// <param name="DocumentParameter">The <see cref="MarkdownDocument"/> to render.</param>
        /// <param name="OptionsParameter">Optional rendering customization.</param>
        public MauiRenderer(MarkdownDocument DocumentParameter, MauiRendererOptions? OptionsParameter = null)
        {
            this.Document = DocumentParameter;
            this.rendererOptions = OptionsParameter ?? new MauiRendererOptions();

            // Initialize stack with a default state
            this.stateStack.Push(new MauiRenderState());

            // Create the top-level stack layout
            this.mainStackLayout = new VerticalStackLayout
            {
                Spacing = this.rendererOptions.StackLayoutSpacing
            };

            // The renderer starts with a dummy current element (will be replaced).
            this.currentElement = new ContentView();
        }

        /// <summary>
        /// Retrieves the final <see cref="VerticalStackLayout"/> containing all rendered views.
        /// </summary>
        /// <returns>The rendered content or null if no children.</returns>
        public VerticalStackLayout? Output()
        {
            return this.mainStackLayout.Children.Count == 0 ? null : this.mainStackLayout;
        }

        // ------------------------------------------------------------------------
        // IRenderer Implementation: RenderDocument
        // ------------------------------------------------------------------------

        /// <summary>
        /// Main entry point for rendering a Markdown document.
        /// </summary>
        /// <param name="DocumentParameter">Document to render.</param>
        /// <param name="InclusionParameter">If true, skip certain "standalone" steps like HEAD/BODY.</param>
        public Task RenderDocument(MarkdownDocument DocumentParameter, bool InclusionParameter)
        {
            // Clear and reset top-level state
            this.ResetState();
            return this.RenderDocumentEntry(DocumentParameter, InclusionParameter);
        }

        /// <summary>
        /// Internal call to actually iterate over the document's elements.
        /// </summary>
        /// <param name="DocumentParameter">Document to render.</param>
        /// <param name="InclusionParameter">If rendering is for inclusion in another doc (skips HEAD/BODY).</param>
        public virtual async Task RenderDocumentEntry(MarkdownDocument DocumentParameter, bool InclusionParameter)
        {
            MarkdownDocument DocumentBackup = this.Document;
            this.Document = DocumentParameter;

            if (!InclusionParameter && this.Document.TryGetMetaData("BODYONLY", out KeyValuePair<string, bool>[] Values))
            {
                if (CommonTypes.TryParse(Values[0].Key, out bool IsBodyOnly) && IsBodyOnly)
                    InclusionParameter = true;
            }

            if (!InclusionParameter)
                await this.RenderDocumentHeader();

            foreach (MarkdownElement ChildElement in this.Document.Elements)
            {
                this.currentElement = new ContentView();
                await ChildElement.Render(this);
                this.mainStackLayout.Add(this.currentElement);
            }

            if (this.NeedsToDisplayFootnotes())
            {
                await this.RenderFootnotes();
                this.mainStackLayout.Add(this.currentElement);
            }

            this.Document = DocumentBackup;
        }

        /// <summary>
        /// Renders the document header (stub here).
        /// </summary>
        public Task RenderDocumentHeader()
        {
            // No HEAD/BODY steps in this example
            return Task.CompletedTask;
        }

        // ------------------------------------------------------------------------
        // IRenderer Implementation: Blocks & Helpers
        // ------------------------------------------------------------------------

        /// <summary>
        /// Renders footnotes at the bottom if any are referenced.
        /// </summary>
        public async Task RenderFootnotes()
        {
            Footnote CurrentFootnote;
            int FootnoteNumber;
            int RowIndex = 0;

            BoxView BoxViewLocal = new()
				{
                HeightRequest = 1,
                BackgroundColor = AppColors.NormalEditPlaceholder,
                HorizontalOptions = LayoutOptions.Fill,
                Margin = AppStyles.SmallTopMargins + AppStyles.SmallBottomMargins
				};
            this.mainStackLayout.Add(BoxViewLocal);

            Grid FootnoteGrid = new()
				{
                RowSpacing = 0,
                ColumnSpacing = 0,
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star)
                }
            };

            // Create row definitions for all referenced footnotes
            foreach (string Key in this.Document.FootnoteOrder)
            {
                if ((this.Document?.TryGetFootnoteNumber(Key, out FootnoteNumber) ?? false)
                    && (this.Document?.TryGetFootnote(Key, out CurrentFootnote) ?? false)
                    && CurrentFootnote.Referenced)
                {
                    FootnoteGrid.AddRowDefinition(new RowDefinition { Height = GridLength.Auto });
                }
            }

            // Populate footnote rows
            if (this.Document is not null)
            {
                foreach (string Key in this.Document.FootnoteOrder)
                {
                    if ((this.Document?.TryGetFootnoteNumber(Key, out FootnoteNumber) ?? false)
                        && (this.Document?.TryGetFootnote(Key, out CurrentFootnote) ?? false)
                        && CurrentFootnote.Referenced)
                    {
                        ContentView FootnoteNumberView = new()
								{
                            Margin = AppStyles.SmallMargins,
                            Scale = 0.75,
                            TranslationY = -5,
                            Content = new Label
                            {
                                Text = FootnoteNumber.ToString(CultureInfo.InvariantCulture)
                            }
                        };

                        FootnoteGrid.Add(FootnoteNumberView, 0, RowIndex);

                        ContentView FootnoteContentView = new();
                        this.currentElement = FootnoteContentView;
                        await this.Render(CurrentFootnote);
                        FootnoteGrid.Add(FootnoteContentView, 1, RowIndex);

                        RowIndex++;
                    }
                }
            }

            this.currentElement = FootnoteGrid;
        }

        // ------------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------------

        /// <summary>
        /// Checks if any footnotes are marked as referenced and thus need to be displayed.
        /// </summary>
        private bool NeedsToDisplayFootnotes()
        {
            if (this.Document.Footnotes is null)
                return false;

            foreach (string Key in this.Document.Footnotes)
            {
                if (this.Document.TryGetFootnote(Key, out Footnote? FootnoteLocal) && FootnoteLocal.Referenced)
                    return true;
            }
            return false;
        }

        // ------------------------------------------------------------------------
        // Child Rendering Helpers
        // ------------------------------------------------------------------------

        /// <summary>
        /// Renders the children of a block element that has multiple children.
        /// </summary>
        /// <param name="ElementParameter">The element containing children to render.</param>
        public async Task RenderChildren(MarkdownElementChildren ElementParameter)
        {
            if (this.CurrentState.InLabel && ElementParameter.Children is not null)
            {
                // If we are in a label, child inline elements can just be appended
                foreach (MarkdownElement ChildElement in ElementParameter.Children)
                    await ChildElement.Render(this);
            }
            else
            {
                // Otherwise, each child is put into a content container in a vertical layout.
                ContentView BackupContentView = (ContentView)this.currentElement;

                VerticalStackLayout ChildrenContainer = new()
                {
                    Spacing = 8
                };

                BackupContentView.Content = ChildrenContainer;

                if (ElementParameter.Children is not null)
                {
                    foreach (MarkdownElement ChildElement in ElementParameter.Children)
                    {
                        ContentView ChildContentView = new ContentView();
                        this.currentElement = ChildContentView;
                        await ChildElement.Render(this);
                        ChildrenContainer.Add(ChildContentView);
                    }
                }

                this.currentElement = BackupContentView;
            }
        }

        /// <summary>
        /// Renders the children of a generic MarkdownElement.
        /// </summary>
        /// <param name="ElementParameter">The element containing children.</param>
        public async Task RenderChildren(MarkdownElement ElementParameter)
        {
            IEnumerable<MarkdownElement> ChildrenEnumerable = ElementParameter.Children;

            if (this.CurrentState.InLabel && ChildrenEnumerable is not null)
            {
                foreach (MarkdownElement ChildElement in ChildrenEnumerable)
                    await ChildElement.Render(this);
            }
            else
            {
                ContentView BackupContentView = (ContentView)this.currentElement;

                VerticalStackLayout ChildrenContainer = new();
                BackupContentView.Content = ChildrenContainer;

                if (ChildrenEnumerable is not null)
                {
                    foreach (MarkdownElement ChildElement in ChildrenEnumerable)
                    {
                        ContentView ChildContentView = new ContentView();
                        this.currentElement = ChildContentView;
                        await ChildElement.Render(this);
                        ChildrenContainer.Add(ChildContentView);
                    }
                }
                this.currentElement = BackupContentView;
            }
        }

        /// <summary>
        /// Renders a single child (common in elements with a single subelement).
        /// </summary>
        /// <param name="ElementParameter">The parent element whose child is to be rendered.</param>
        public Task RenderChild(MarkdownElementSingleChild ElementParameter)
        {
            if (ElementParameter.Child is null)
                return Task.CompletedTask;

            return ElementParameter.Child.Render(this);
        }

        /// <summary>
        /// Renders a label with special detection if inline links exist.
        /// </summary>
        /// <param name="ElementParameter">The element or sub-element to be rendered in a label.</param>
        /// <param name="IncludeElement">If true, process the entire element; otherwise just its children.</param>
        internal async Task RenderLabel(MarkdownElement ElementParameter, bool IncludeElement)
        {
            ContentView BackupContentView = (ContentView)this.currentElement;

            // Check if the element or its descendants have links
            bool HasLink = !ElementParameter.ForEach((Descendant, _) =>
            {
                return !(Descendant is AutomaticLinkMail
                            || Descendant is AutomaticLinkUrl
                            || Descendant is Link
                            || Descendant is LinkReference);
            }, null);

            Label LabelControl = new Label
            {
                LineBreakMode = LineBreakMode.WordWrap,
                HorizontalTextAlignment = this.MapAlignment(this.CurrentState.Alignment)
            };
            this.currentElement = LabelControl;

            if (HasLink)
            {
                // If we have links, we often need to set inLabel = true for correct span rendering.
                this.PushState();
                this.CurrentState.InLabel = true;

                if (IncludeElement)
                    await ElementParameter.Render(this);
                else
                    await this.RenderChildren(ElementParameter);

                this.PopState();
            }
            else
            {
                // We can safely render as HTML text in a single label.
                LabelControl.TextType = TextType.Html;
                if (this.CurrentState.Bold)
                    LabelControl.FontAttributes = FontAttributes.Bold;

                using (HtmlRenderer HtmlRendererLocal = new(new HtmlSettings() { XmlEntitiesOnly = true }, this.Document))
                {
                    if (IncludeElement)
                        await ElementParameter.Render(HtmlRendererLocal);
                    else
                        await HtmlRendererLocal.RenderChildren(ElementParameter);

                    LabelControl.Text = HtmlRendererLocal.ToString();
                }
            }

            BackupContentView.Content = LabelControl;
            this.currentElement = BackupContentView;
        }

        /// <summary>
        /// Renders inline text or "span" content inside a label. Applies current formatting (bold, italic, etc.).
        /// </summary>
        /// <param name="TextParameter">The text to render inline.</param>
        internal Task RenderSpan(string TextParameter)
        {
            Label LabelControl;

            // If we are not in a label, create one
            if (!this.CurrentState.InLabel)
            {
                LabelControl = new Label
                {
                    LineBreakMode = LineBreakMode.WordWrap,
                };

                ContentView CurrentContentView = (ContentView)this.currentElement;
                CurrentContentView.Content = LabelControl;
                this.currentElement = CurrentContentView;
            }
            else
            {
                // Otherwise, cast the existing currentElement to a Label
                LabelControl = (Label)this.currentElement;
            }

            FormattedString Formatted = LabelControl.FormattedText ?? new FormattedString();

            // Create a new Span for the text
            Span SpanControl = new Span();
            Formatted.Spans.Add(SpanControl);
            LabelControl.FormattedText = Formatted;

            // Convert text if subscript/superscript
            if (this.CurrentState.Superscript)
                TextParameter = TextRenderer.ToSuperscript(TextParameter);
            else if (this.CurrentState.Subscript)
                TextParameter = TextRenderer.ToSubscript(TextParameter);

            SpanControl.Text = TextParameter;

            // Font attributes
            if (this.CurrentState.Bold && this.CurrentState.Italic)
                SpanControl.FontAttributes = FontAttributes.Bold | FontAttributes.Italic;
            else if (this.CurrentState.Bold)
                SpanControl.FontAttributes = FontAttributes.Bold;
            else if (this.CurrentState.Italic)
                SpanControl.FontAttributes = FontAttributes.Italic;

            // Decorations
            if (this.CurrentState.StrikeThrough && this.CurrentState.Underline)
                SpanControl.TextDecorations = TextDecorations.Underline | TextDecorations.Strikethrough;
            else if (this.CurrentState.StrikeThrough)
                SpanControl.TextDecorations = TextDecorations.Strikethrough;
            else if (this.CurrentState.Underline)
                SpanControl.TextDecorations = TextDecorations.Underline;

            // Inline code
            if (this.CurrentState.Code)
                SpanControl.FontFamily = "SpaceGroteskRegular";

            // Hyperlink
            if (!string.IsNullOrEmpty(this.CurrentState.Hyperlink))
            {
                SpanControl.TextColor = AppColors.BlueLink;
                SpanControl.GestureRecognizers.Add(
                     new TapGestureRecognizer
                     {
                         CommandParameter = this.CurrentState.Hyperlink,
                         Command = new Command(async Param =>
                         {
                             string UrlString = Param as string ?? string.Empty;
                             await App.OpenUrlAsync(UrlString);
                         })
                     }
                );
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Creates a new ContentView with a given margin and alignment style.
        /// </summary>
        /// <param name="MarginsParameter">The margin to apply.</param>
        internal void RenderContentView(Thickness MarginsParameter)
        {
            this.RenderContentView(this.CurrentState.Alignment, MarginsParameter, null);
        }

        /// <summary>
        /// Creates a new ContentView with an explicit alignment, margin, and style.
        /// </summary>
        /// <param name="AlignmentParameter">The text alignment for the content view.</param>
        /// <param name="MarginsParameter">The margin to apply.</param>
        /// <param name="BoxStyleParameter">Optional style to apply.</param>
        internal void RenderContentView(Waher.Content.Markdown.Model.TextAlignment AlignmentParameter,
                                        Thickness MarginsParameter,
                                        Style? BoxStyleParameter)
        {
            ContentView NewContentView = new ContentView();
            if (MarginsParameter != default)
                NewContentView.Padding = MarginsParameter;

            if (BoxStyleParameter is not null)
                NewContentView.Style = BoxStyleParameter;

            switch (AlignmentParameter)
            {
                case Waher.Content.Markdown.Model.TextAlignment.Center:
                    NewContentView.HorizontalOptions = LayoutOptions.Center;
                    break;
                case Waher.Content.Markdown.Model.TextAlignment.Left:
                    NewContentView.HorizontalOptions = LayoutOptions.Start;
                    break;
                case Waher.Content.Markdown.Model.TextAlignment.Right:
                    NewContentView.HorizontalOptions = LayoutOptions.End;
                    break;
            }
            this.currentElement = NewContentView;
        }

        /// <summary>
        /// Maps the custom markdown alignment to Maui Label alignment.
        /// </summary>
        /// <param name="AlignParameter">The <see cref="Waher.Content.Markdown.Model.TextAlignment"/> to map.</param>
        private Microsoft.Maui.TextAlignment MapAlignment(Waher.Content.Markdown.Model.TextAlignment AlignParameter)
        {
            return AlignParameter switch
            {
                Waher.Content.Markdown.Model.TextAlignment.Left => Microsoft.Maui.TextAlignment.Start,
                Waher.Content.Markdown.Model.TextAlignment.Right => Microsoft.Maui.TextAlignment.End,
                Waher.Content.Markdown.Model.TextAlignment.Center => Microsoft.Maui.TextAlignment.Center,
                _ => Microsoft.Maui.TextAlignment.Start
            };
        }

        // ------------------------------------------------------------------------
        // Push/Pop State
        // ------------------------------------------------------------------------
        private void ResetState()
        {
            this.stateStack.Clear();
            this.stateStack.Push(new MauiRenderState());
        }

        private void PushState()
        {
            this.stateStack.Push(this.CurrentState.Clone());
        }

        private void PopState()
        {
            if (this.stateStack.Count > 1)
                this.stateStack.Pop();
        }

        // ------------------------------------------------------------------------
        // Handling Evaluated Objects (Inline Scripts, etc.)
        // ------------------------------------------------------------------------

        /// <summary>
        /// Renders an object that might result from an inline script or other runtime evaluation.
        /// </summary>
        /// <param name="ResultParameter">The evaluated object to render.</param>
        /// <param name="AloneInParagraphParameter">Indicates if the object is alone in its paragraph.</param>
        /// <param name="VariablesParameter">The script variables.</param>
        public async Task RenderObject(object? ResultParameter, bool AloneInParagraphParameter, Variables VariablesParameter)
        {
            ContentView BackupContentView = (ContentView)this.currentElement;

            if (ResultParameter is null)
                return;

            if (ResultParameter is XmlDocument XmlDoc)
                ResultParameter = await MarkdownDocument.TransformXml(XmlDoc, VariablesParameter);
            else if (ResultParameter is IToMatrix MatrixConverter)
                ResultParameter = MatrixConverter.ToMatrix();

            if (this.CurrentState.InLabel)
            {
                string? InlineText = ResultParameter?.ToString();
                if (!string.IsNullOrEmpty(InlineText))
                    await this.RenderSpan(InlineText);
                return;
            }

            if (ResultParameter is Graph GraphObj)
            {
                PixelInformation Pixels = GraphObj.CreatePixels(VariablesParameter);
                byte[] Bytes = Pixels.EncodeAsPng();

                string DataUri = "data:image/png;base64," + Convert.ToBase64String(Bytes, 0, Bytes.Length);

                await this.OutputMaui(new Waher.Content.Emoji.ImageSource
                {
                    Url = DataUri,
                    Width = Pixels.Width,
                    Height = Pixels.Height
                });
            }
            else if (ResultParameter is SKImage SkiaImage)
            {
                using SKData ImageData = SkiaImage.Encode(SKEncodedImageFormat.Png, 100);
                byte[] Bytes = ImageData.ToArray();

                string DataUri = "data:image/png;base64," + Convert.ToBase64String(Bytes, 0, Bytes.Length);

                await this.OutputMaui(new Waher.Content.Emoji.ImageSource
                {
                    Url = DataUri,
                    Width = SkiaImage.Width,
                    Height = SkiaImage.Height
                });
            }
            else if (ResultParameter is MarkdownDocument MarkdownDoc)
            {
                await this.RenderDocument(MarkdownDoc, true);
                MarkdownDoc.ProcessAsyncTasks();
            }
            else if (ResultParameter is MarkdownContent MarkdownContentLocal)
            {
                MarkdownDocument NewDoc = await MarkdownDocument.CreateAsync(MarkdownContentLocal.Markdown);
                await this.RenderDocument(NewDoc, true);
                NewDoc.ProcessAsyncTasks();
            }
            else if (ResultParameter is Exception ExParameter)
            {
                ExParameter = Log.UnnestException(ExParameter);

                if (ExParameter is AggregateException AggEx)
                {
                    VerticalStackLayout ExceptionContainer = new();

                    foreach (Exception InnerEx in AggEx.InnerExceptions)
                    {
                        Label ErrorLabel = new Label
                        {
                            LineBreakMode = LineBreakMode.WordWrap,
                            TextColor = AppColors.Alert,
                            Text = InnerEx.Message
                        };
                        ExceptionContainer.Add(ErrorLabel);
                    }
                    BackupContentView.Content = ExceptionContainer;
                }
                else
                {
                    Label ErrorLabel = new Label
                    {
                        LineBreakMode = LineBreakMode.WordWrap,
                        TextColor = AppColors.Alert,
                        Text = ExParameter.Message
                    };
                    BackupContentView.Content = ErrorLabel;
                }
            }
            else
            {
                Label SimpleLabel = new Label
                {
                    LineBreakMode = LineBreakMode.WordWrap,
                    HorizontalTextAlignment = this.MapAlignment(this.CurrentState.Alignment),
                    Text = ResultParameter.ToString()
                };
                BackupContentView.Content = SimpleLabel;
            }
        }

        // ------------------------------------------------------------------------
        // Output Images, Multimedia
        // ------------------------------------------------------------------------

        /// <summary>
        /// Checks a Data URI image, ensuring it contains a decodable image.
        /// </summary>
        /// <param name="SourceParameter">The image source to check.</param>
        public static async Task<Waher.Content.Emoji.IImageSource> CheckDataUri(Waher.Content.Emoji.IImageSource SourceParameter)
        {
            string UrlLocal = SourceParameter.Url;
            int Base64Index;

            if (UrlLocal.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                && (Base64Index = UrlLocal.IndexOf("base64,", StringComparison.OrdinalIgnoreCase)) > 0)
            {
                int? ImageWidth = SourceParameter.Width;
                int? ImageHeight = SourceParameter.Height;
                byte[] DataBytes = Convert.FromBase64String(UrlLocal.Substring(Base64Index + 7));

                using SKBitmap BitmapLocal = SKBitmap.Decode(DataBytes);
                ImageWidth = BitmapLocal.Width;
                ImageHeight = BitmapLocal.Height;

                UrlLocal = await ImageContent.GetTemporaryFile(DataBytes);

                return new ImageSource
                {
                    Url = UrlLocal,
                    Width = ImageWidth,
                    Height = ImageHeight
                };
            }
            else
                return SourceParameter;
        }

        private async Task OutputMaui(Waher.Content.Emoji.IImageSource SourceParameter)
        {
            SourceParameter = await CheckDataUri(SourceParameter);

            Image MauiImage = new Image
            {
                Source = SourceParameter.Url
            };

            ScrollView ScrollContainer = new ScrollView
            {
                Orientation = ScrollOrientation.Horizontal,
                Content = MauiImage
            };

            if (SourceParameter.Width.HasValue)
                ScrollContainer.WidthRequest = SourceParameter.Width.Value;

            if (SourceParameter.Height.HasValue)
                ScrollContainer.HeightRequest = SourceParameter.Height.Value;

            ContentView CurrentContentView = (ContentView)this.currentElement;
            CurrentContentView.Content = ScrollContainer;
        }

        private async Task RenderMaui(Waher.Content.Markdown.Model.SpanElements.Multimedia ElementParameter)
        {
            ContentView BackupContentView = (ContentView)this.currentElement;
            VerticalStackLayout MultimediaContainer = new VerticalStackLayout();

            foreach (MultimediaItem Item in ElementParameter.Items)
            {
                ContentView ImageContainer = new ContentView();
                this.currentElement = ImageContainer;
                await this.OutputMaui(new ImageSource
                {
                    Url = ElementParameter.Document.CheckURL(Item.Url, null),
                    Width = Item.Width,
                    Height = Item.Height
                });
                MultimediaContainer.Add(ImageContainer);
            }

            BackupContentView.Content = MultimediaContainer;
            this.currentElement = BackupContentView;
        }

        // ------------------------------------------------------------------------
        // Interface Methods: SPAN ELEMENTS
        // (We just ensure we have a method for each interface member.)
        // ------------------------------------------------------------------------

        /// <inheritdoc/>
        public async Task Render(Abbreviation ElementParameter)
        {
            // Typically just treat it as normal inline or children
            await this.RenderChildren(ElementParameter);
        }

        /// <inheritdoc/>
        public Task Render(AutomaticLinkMail ElementParameter)
        {
            string? PreviousHyperlink = this.CurrentState.Hyperlink;
            this.CurrentState.Hyperlink = "mailto:" + ElementParameter.EMail;
            this.RenderSpan(this.CurrentState.Hyperlink);
            this.CurrentState.Hyperlink = PreviousHyperlink;
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task Render(AutomaticLinkUrl ElementParameter)
        {
            string? PreviousHyperlink = this.CurrentState.Hyperlink;
            this.CurrentState.Hyperlink = ElementParameter.URL;
            this.RenderSpan(ElementParameter.URL);
            this.CurrentState.Hyperlink = PreviousHyperlink;
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task Render(Delete ElementParameter)
        {
            this.PushState();
            this.CurrentState.StrikeThrough = true;
            await this.RenderChildren(ElementParameter);
            this.PopState();
        }

        /// <inheritdoc/>
        public Task Render(DetailsReference ElementParameter)
        {
            // Implementation is up to you; the old code did minimal handling
            // For now, we can do similarly or call your 'RenderDocument(this.Document.Detail,...)' if needed
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task Render(EmojiReference ElementParameter)
        {
            ContentView BackupContentView = (ContentView)this.currentElement;

            if (this.CurrentState.InLabel)
            {
                await this.RenderSpan(ElementParameter.Emoji.Unicode);
            }
            else
            {
                IEmojiSource? EmojiSrc = this.Document.EmojiSource;
                if (EmojiSrc is null)
                {
                    await this.RenderSpan(ElementParameter.Delimiter + ElementParameter.Emoji.ShortName + ElementParameter.Delimiter);
                }
                else if (!EmojiSrc.EmojiSupported(ElementParameter.Emoji))
                {
                    await this.RenderSpan(ElementParameter.Emoji.Unicode);
                }
                else
                {
                    Waher.Content.Emoji.IImageSource FinalSource =
                        await EmojiSrc.GetImageSource(ElementParameter.Emoji, ElementParameter.Level);
                    await this.OutputMaui(FinalSource);
                }
            }

            this.currentElement = BackupContentView;
        }

        /// <inheritdoc/>
        public async Task Render(Emphasize ElementParameter)
        {
            this.PushState();
            this.CurrentState.Italic = true;
            await this.RenderChildren(ElementParameter);
            this.PopState();
        }

        /// <inheritdoc/>
        public async Task Render(FootnoteReference ElementParameter)
        {
            ContentView BackupContentView = (ContentView)this.currentElement;

            if (!(this.Document?.TryGetFootnote(ElementParameter.Key, out Footnote? RenderFootnote) ?? false))
                RenderFootnote = null;

            if (ElementParameter.AutoExpand && RenderFootnote is not null)
            {
                await this.Render(RenderFootnote);
            }
            else if (this.Document?.TryGetFootnoteNumber(ElementParameter.Key, out int FootnoteNum) ?? false)
            {
                this.PushState();
                this.CurrentState.Superscript = true;
                await this.RenderSpan(FootnoteNum.ToString(CultureInfo.InvariantCulture));
                this.PopState();

                if (RenderFootnote is not null)
                    RenderFootnote.Referenced = true;
            }

            this.currentElement = BackupContentView;
        }

        /// <inheritdoc/>
        public Task Render(HashTag ElementParameter)
        {
            ContentView BackupContentView = (ContentView)this.currentElement;
            ContentView InnerContentView = new ContentView();
            this.currentElement = InnerContentView;

            Border TagBorder = new Border
            {
                Background = Color.FromArgb("FFFAFAD2"),
                Content = InnerContentView
            };

            this.RenderSpan(ElementParameter.Tag);

            BackupContentView.Content = TagBorder;
            this.currentElement = BackupContentView;
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task Render(HtmlEntity ElementParameter)
        {
            string Translated = Waher.Content.Html.HtmlEntity.EntityToCharacter(ElementParameter.Entity);
            if (!string.IsNullOrEmpty(Translated))
            {
                this.RenderSpan(Translated);
            }
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task Render(HtmlEntityUnicode ElementParameter)
        {
            this.RenderSpan(new string((char)ElementParameter.Code, 1));
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task Render(InlineCode ElementParameter)
        {
            this.PushState();
            this.CurrentState.Code = true;
            this.RenderSpan(ElementParameter.Code);
            this.PopState();
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task Render(InlineHTML ElementParameter)
        {
            // Display raw HTML in a label
            if (this.currentElement is Label LabelLocal)
            {
                LabelLocal.TextType = TextType.Html;
                LabelLocal.Text = $"<--- {ElementParameter.HTML} --->";
            }
            else
            {
                ContentView ContentViewLocal = (ContentView)this.currentElement;
                Label NewLabel = new Label
                {
                    TextType = TextType.Html,
                    Text = $"<--- {ElementParameter.HTML} --->",
                };
                ContentViewLocal.Content = NewLabel;
            }
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task Render(InlineScript ElementParameter)
        {
            object ResultObj = await ElementParameter.EvaluateExpression();
            await this.RenderObject(ResultObj, ElementParameter.AloneInParagraph, ElementParameter.Variables);
        }

        /// <inheritdoc/>
        public Task Render(InlineText ElementParameter)
        {
            return this.RenderSpan(ElementParameter.Value);
        }

        /// <inheritdoc/>
        public async Task Render(Insert ElementParameter)
        {
            this.PushState();
            this.CurrentState.Underline = true;
            await this.RenderChildren(ElementParameter);
            this.PopState();
        }

        /// <inheritdoc/>
        public Task Render(LineBreak ElementParameter)
        {
            this.RenderSpan(Environment.NewLine);
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task Render(Link ElementParameter)
        {
            string? PreviousHyperlink = this.CurrentState.Hyperlink;
            this.CurrentState.Hyperlink = ElementParameter.Url;
            await this.RenderChildren(ElementParameter);
            this.CurrentState.Hyperlink = PreviousHyperlink;
        }

        /// <inheritdoc/>
        public async Task Render(LinkReference ElementParameter)
        {
            Waher.Content.Markdown.Model.SpanElements.Multimedia? MmLocal = this.Document.GetReference(ElementParameter.Label);

            string? PreviousHyperlink = this.CurrentState.Hyperlink;
            if (MmLocal is not null)
                this.CurrentState.Hyperlink = MmLocal.Items[0].Url;

            await this.RenderChildren(ElementParameter);
            this.CurrentState.Hyperlink = PreviousHyperlink;
        }

        /// <inheritdoc/>
        public Task Render(MetaReference ElementParameter)
        {
            StringBuilder StringBuilderLocal = new();
            bool FirstOnRow = true;

            if (ElementParameter.TryGetMetaData(out KeyValuePair<string, bool>[] ValuesLocal))
            {
                foreach (KeyValuePair<string, bool> Pair in ValuesLocal)
                {
                    if (FirstOnRow)
                        FirstOnRow = false;
                    else
                        StringBuilderLocal.Append(' ');

                    StringBuilderLocal.Append(Pair.Key);
                    if (Pair.Value)
                    {
                        StringBuilderLocal.Append(Environment.NewLine);
                        FirstOnRow = true;
                    }
                }
            }
            this.RenderSpan(StringBuilderLocal.ToString());
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task Render(Waher.Content.Markdown.Model.SpanElements.Multimedia ElementParameter)
        {
            // If there's a specialized renderer, use it. Otherwise, treat as children or images
            IMultimediaMauiXamlRenderer? Handler = ElementParameter.MultimediaHandler<IMultimediaMauiXamlRenderer>();
            if (Handler is null)
                await this.RenderChildren(ElementParameter);
            else
                await this.RenderMaui(ElementParameter);
        }

        /// <inheritdoc/>
        public async Task Render(MultimediaReference ElementParameter)
        {
            Waher.Content.Markdown.Model.SpanElements.Multimedia? MRef = ElementParameter.Document.GetReference(ElementParameter.Label);
            if (MRef is not null)
            {
                IMultimediaMauiXamlRenderer? Renderer = MRef.MultimediaHandler<IMultimediaMauiXamlRenderer>();
                if (Renderer is not null)
                {
                    await this.RenderMaui(MRef);
                    return;
                }
            }
            await this.RenderChildren(ElementParameter);
        }

        /// <inheritdoc/>
        public async Task Render(StrikeThrough ElementParameter)
        {
            this.PushState();
            this.CurrentState.StrikeThrough = true;
            await this.RenderChildren(ElementParameter);
            this.PopState();
        }

        /// <inheritdoc/>
        public async Task Render(Strong ElementParameter)
        {
            this.PushState();
            this.CurrentState.Bold = true;
            await this.RenderChildren(ElementParameter);
            this.PopState();
        }

        /// <inheritdoc/>
        public async Task Render(SubScript ElementParameter)
        {
            this.PushState();
            this.CurrentState.Subscript = true;
            await this.RenderChildren(ElementParameter);
            this.PopState();
        }

        /// <inheritdoc/>
        public async Task Render(SuperScript ElementParameter)
        {
            this.PushState();
            this.CurrentState.Superscript = true;
            await this.RenderChildren(ElementParameter);
            this.PopState();
        }

        /// <inheritdoc/>
        public async Task Render(Underline ElementParameter)
        {
            this.PushState();
            this.CurrentState.Underline = true;
            await this.RenderChildren(ElementParameter);
            this.PopState();
        }

        // ------------------------------------------------------------------------
        // Interface Methods: BLOCK ELEMENTS
        // ------------------------------------------------------------------------

        /// <inheritdoc/>
        public async Task Render(BlockQuote ElementParameter)
        {
            ContentView BackupContentView = (ContentView)this.currentElement;

            Border QuoteBorder = new Border
            {
                Padding = AppStyles.SmallMargins,
                Stroke = new SolidColorBrush { Color = AppColors.PrimaryForeground },
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 2 }
            };
            BackupContentView.Content = QuoteBorder;

            ContentView QuoteContentView = new ContentView();
            this.currentElement = QuoteContentView;
            await this.RenderChildren(ElementParameter);

            QuoteBorder.Content = QuoteContentView;
            this.currentElement = BackupContentView;
        }

        /// <inheritdoc/>
        public async Task Render(BulletList ElementParameter)
        {
            ContentView BackupContentView = (ContentView)this.currentElement;

            int RowIndex = 0;
            Grid BulletGrid = new Grid
            {
                RowSpacing = 0,
                ColumnSpacing = 0,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Star }
                },
            };

            foreach (MarkdownElement _ in ElementParameter.Children)
                BulletGrid.AddRowDefinition(new RowDefinition { Height = GridLength.Auto });

            foreach (MarkdownElement ChildElement in ElementParameter.Children)
            {
                if (ChildElement is UnnumberedItem Unnumbered)
                {
                    this.RenderContentView(AppStyles.SmallRightMargins);
                    ContentView BulletView = (ContentView)this.currentElement;
                    BulletView.Column(0);
                    BulletView.Row(RowIndex);
                    BulletView.Content = new Label { Text = "•" };
                    BulletGrid.Add(BulletView);

                    ContentView ContainerView = new();
                    ContainerView.Column(1);
                    ContainerView.Row(RowIndex);
                    this.currentElement = ContainerView;

                    bool ParagraphBullet = !ChildElement.InlineSpanElement || ChildElement.OutsideParagraph;
                    if (ParagraphBullet)
                        await ChildElement.Render(this);
                    else
                        await this.RenderLabel(Unnumbered, false);

                    BulletGrid.Add(ContainerView);
                }
                RowIndex++;
            }

            BackupContentView.Content = BulletGrid;
            this.currentElement = BackupContentView;
        }

        /// <inheritdoc/>
        public async Task Render(CenterAligned ElementParameter)
        {
            this.PushState();
            this.CurrentState.Alignment = Waher.Content.Markdown.Model.TextAlignment.Center;
            await this.RenderChildren(ElementParameter);
            this.PopState();
        }

        /// <inheritdoc/>
        public async Task Render(CodeBlock ElementParameter)
        {
            ContentView BackupContentView = (ContentView)this.currentElement;
            VerticalStackLayout CodeBlockContainer = new VerticalStackLayout();

            StringBuilder OutputBuilder = new();
            MauiXamlRenderer XamlRendererLocal = new(OutputBuilder, XML.WriterSettings(false, true));
            ICodeContentMauiXamlRenderer? CodeRenderer = ElementParameter.CodeContentHandler<ICodeContentMauiXamlRenderer>();
            if (CodeRenderer is not null)
            {
                try
                {
                    if (await CodeRenderer.RenderMauiXaml(
                        XamlRendererLocal,
                        ElementParameter.Rows,
                        ElementParameter.Language,
                        ElementParameter.Indent,
                        ElementParameter.Document))
                    {
                        // If specialized rendering succeeded, we can stop here
                        return;
                    }
                }
                catch (Exception ExParameter)
                {
                    ExParameter = Log.UnnestException(ExParameter);
                    if (ExParameter is AggregateException AggEx)
                    {
                        foreach (Exception NestedEx in AggEx.InnerExceptions)
                        {
                            Label ErrorLabel = new Label
                            {
                                LineBreakMode = LineBreakMode.WordWrap,
                                TextColor = AppColors.Alert,
                                Text = NestedEx.Message
                            };
                            CodeBlockContainer.Add(ErrorLabel);
                        }
                    }
                    else
                    {
                        Label ErrorLabel = new Label
                        {
                            LineBreakMode = LineBreakMode.WordWrap,
                            TextColor = AppColors.Alert,
                            Text = ExParameter.Message
                        };
                        CodeBlockContainer.Add(ErrorLabel);
                    }
                }
            }

            // Fallback: just plain text lines
            for (int LineIndex = ElementParameter.Start; LineIndex <= ElementParameter.End; LineIndex++)
            {
                Label CodeLineLabel = new Label
                {
                    LineBreakMode = LineBreakMode.NoWrap,
                    HorizontalTextAlignment = this.MapAlignment(this.CurrentState.Alignment),
                    FontFamily = "SpaceGroteskRegular",
                    Text = ElementParameter.Rows[LineIndex]
                };
                CodeBlockContainer.Add(CodeLineLabel);
            }

            BackupContentView.Content = CodeBlockContainer;
            this.currentElement = BackupContentView;
        }

        /// <inheritdoc/>
        public Task Render(CommentBlock ElementParameter)
        {
            // Not rendered in most cases
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task Render(DefinitionDescriptions ElementParameter)
        {
            // If you have special logic for definitions, copy from old code
            // Otherwise, you can do the default:
            return this.RenderChildren(ElementParameter);
        }

        /// <inheritdoc/>
        public Task Render(DefinitionList ElementParameter)
        {
            return this.RenderChildren(ElementParameter);
        }

        /// <inheritdoc/>
        public Task Render(DefinitionTerms ElementParameter)
        {
            // If you have special logic for terms, copy from old code
            return this.RenderChildren(ElementParameter);
        }

        /// <inheritdoc/>
        public async Task Render(DeleteBlocks ElementParameter)
        {
            ContentView BackupContentView = (ContentView)this.currentElement;

            Border DeletedBorder = new Border
            {
                Padding = AppStyles.SmallMargins,
                Stroke = new SolidColorBrush { Color = AppColors.DeletedBorder },
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 2 }
            };
            BackupContentView.Content = DeletedBorder;

            ContentView DeletedContent = new ContentView();
            this.currentElement = DeletedContent;
            await this.RenderChildren(ElementParameter);

            DeletedBorder.Content = DeletedContent;
            this.currentElement = BackupContentView;
        }

        /// <inheritdoc/>
        public async Task Render(Footnote ElementParameter)
        {
            // Render footnote content
            await this.RenderChildren(ElementParameter);
        }

        /// <inheritdoc/>
        public async Task Render(Header ElementParameter)
        {
            // We already did this above, but let's keep the method:
            int HeaderLevel = Math.Max(0, Math.Min(9, ElementParameter.Level));
            Label HeaderLabel = new Label
            {
                LineBreakMode = LineBreakMode.WordWrap,
                HorizontalTextAlignment = this.MapAlignment(this.CurrentState.Alignment),
                TextType = TextType.Html,
                Style = AppStyles.GetHeaderStyle(HeaderLevel)
            };

            using (HtmlRenderer HtmlRendererLocal = new(new HtmlSettings() { XmlEntitiesOnly = true }, this.Document))
            {
                await HtmlRendererLocal.RenderChildren(ElementParameter);
                HeaderLabel.Text = HtmlRendererLocal.ToString();
            }

            ContentView HeaderView = (ContentView)this.currentElement;
            HeaderView.Content = HeaderLabel;
            this.currentElement = HeaderView;
        }

        /// <inheritdoc/>
        public Task Render(HorizontalRule ElementParameter)
        {
            BoxView HrBoxView = new BoxView
            {
                HeightRequest = 1,
                BackgroundColor = AppColors.NormalEditPlaceholder,
                HorizontalOptions = LayoutOptions.Fill,
                Margin = AppStyles.SmallTopMargins + AppStyles.SmallBottomMargins
            };

            ContentView HrContentView = (ContentView)this.currentElement;
            HrContentView.Content = HrBoxView;
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task Render(HtmlBlock ElementParameter)
        {
            ContentView BackupContentView = (ContentView)this.currentElement;
            BackupContentView.Margin = AppStyles.SmallTopMargins + AppStyles.SmallBottomMargins;

            Label HtmlLabel = new Label
            {
                LineBreakMode = LineBreakMode.WordWrap,
                HorizontalTextAlignment = this.MapAlignment(this.CurrentState.Alignment),
                TextType = TextType.Html,
            };

            using HtmlRenderer HtmlRendererLocal = new(new HtmlSettings() { XmlEntitiesOnly = true }, this.Document);
            await HtmlRendererLocal.RenderChildren(ElementParameter);
            HtmlLabel.Text = HtmlRendererLocal.ToString();

            BackupContentView.Content = HtmlLabel;
            this.currentElement = BackupContentView;
        }

        /// <inheritdoc/>
        public async Task Render(InsertBlocks ElementParameter)
        {
            ContentView BackupContentView = (ContentView)this.currentElement;

            Border InsertBorder = new Border
            {
                Padding = AppStyles.SmallMargins,
                Stroke = new SolidColorBrush { Color = AppColors.InsertedBorder },
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 2 }
            };
            BackupContentView.Content = InsertBorder;

            ContentView InsertContentView = new ContentView();
            this.currentElement = InsertContentView;
            await this.RenderChildren(ElementParameter);

            InsertBorder.Content = InsertContentView;
            this.currentElement = BackupContentView;
        }

        /// <inheritdoc/>
        public Task Render(InvisibleBreak ElementParameter)
        {
            // Usually a no-op
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task Render(LeftAligned ElementParameter)
        {
            this.PushState();
            this.CurrentState.Alignment = Waher.Content.Markdown.Model.TextAlignment.Left;
            await this.RenderChildren(ElementParameter);
            this.PopState();
        }

        /// <inheritdoc/>
        public async Task Render(MarginAligned ElementParameter)
        {
            this.PushState();
            this.CurrentState.Alignment = Waher.Content.Markdown.Model.TextAlignment.Left;
            await this.RenderChildren(ElementParameter);
            this.PopState();
        }

        /// <inheritdoc/>
        public async Task Render(NestedBlock ElementParameter)
        {
            ContentView BackupContentView = (ContentView)this.currentElement;

            if (ElementParameter.HasOneChild)
            {
                await ElementParameter.FirstChild.Render(this);
            }
            else
            {
                HtmlSettings SettingsLocal = new() { XmlEntitiesOnly = true };
                HtmlRenderer? HtmlLocal = null;
                VerticalStackLayout NestedContainer = new VerticalStackLayout();

                try
                {
                    foreach (MarkdownElement ChildElement in ElementParameter.Children)
                    {
                        if (ChildElement.InlineSpanElement)
                        {
                            HtmlLocal ??= new HtmlRenderer(SettingsLocal, this.Document);
                            await ChildElement.Render(HtmlLocal);
                        }
                        else
                        {
                            if (HtmlLocal is not null)
                            {
                                Label HtmlLabel = new Label
                                {
                                    LineBreakMode = LineBreakMode.WordWrap,
                                    HorizontalTextAlignment = this.MapAlignment(this.CurrentState.Alignment),
                                    TextType = TextType.Html,
                                    Text = HtmlLocal.ToString()
                                };
                                HtmlLocal.Dispose();
                                HtmlLocal = null;
                                NestedContainer.Add(HtmlLabel);
                            }

                            ContentView ChildContentView = new ContentView();
                            this.currentElement = ChildContentView;
                            await ChildElement.Render(this);
                            NestedContainer.Add(ChildContentView);
                        }
                    }

                    if (HtmlLocal is not null)
                    {
                        Label FinalHtmlLabel = new Label
                        {
                            LineBreakMode = LineBreakMode.WordWrap,
                            HorizontalTextAlignment = this.MapAlignment(this.CurrentState.Alignment),
                            TextType = TextType.Html,
                            Text = HtmlLocal.ToString()
                        };
                        NestedContainer.Add(FinalHtmlLabel);
                    }
                }
                finally
                {
                    HtmlLocal?.Dispose();
                }
                BackupContentView.Content = NestedContainer;
                this.currentElement = BackupContentView;
            }
        }

        /// <inheritdoc/>
        public Task Render(NumberedItem ElementParameter)
        {
            // Typically just calls RenderChild
            return this.RenderChild(ElementParameter);
        }

        /// <inheritdoc/>
        public async Task Render(NumberedList ElementParameter)
        {
            ContentView BackupContentView = (ContentView)this.currentElement;

            int ExpectedNumber = 0;
            int RowIndex = 0;
            Grid NumberedGrid = new Grid
            {
                RowSpacing = 0,
                ColumnSpacing = 0,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Star }
                }
            };

            foreach (MarkdownElement _ in ElementParameter.Children)
                NumberedGrid.AddRowDefinition(new RowDefinition { Height = GridLength.Auto });

            foreach (MarkdownElement ChildElement in ElementParameter.Children)
            {
                if (ChildElement is BlockElementSingleChild SingleChildElement)
                {
                    ExpectedNumber++;

                    this.RenderContentView(AppStyles.SmallRightMargins);
                    ContentView NumberView = (ContentView)this.currentElement;
                    NumberView.Column(0);
                    NumberView.Row(RowIndex);

                    Label NumberLabel = new Label();
                    if (SingleChildElement is NumberedItem NumberedItemElement)
                        NumberLabel.Text = (ExpectedNumber = NumberedItemElement.Number).ToString(CultureInfo.InvariantCulture) + ".";
                    else
                        NumberLabel.Text = ExpectedNumber.ToString(CultureInfo.InvariantCulture) + ".";

                    NumberView.Content = NumberLabel;
                    NumberedGrid.Add(NumberView);

                    ContentView ContainerView = new ContentView();
                    ContainerView.Column(1);
                    ContainerView.Row(RowIndex);
                    this.currentElement = ContainerView;

                    bool IsBulletParagraph = !ChildElement.InlineSpanElement || ChildElement.OutsideParagraph;
                    if (IsBulletParagraph)
                        await ChildElement.Render(this);
                    else
                        await this.RenderLabel(SingleChildElement, false);

                    NumberedGrid.Add(ContainerView);
                }
                RowIndex++;
            }

            BackupContentView.Content = NumberedGrid;
            this.currentElement = BackupContentView;
        }

        /// <inheritdoc/>
        public async Task Render(Paragraph ElementParameter)
        {
            await this.RenderLabel(ElementParameter, false);
        }

        /// <inheritdoc/>
        public async Task Render(RightAligned ElementParameter)
        {
            this.PushState();
            this.CurrentState.Alignment = Waher.Content.Markdown.Model.TextAlignment.Right;
            await this.RenderChildren(ElementParameter);
            this.PopState();
        }

        /// <inheritdoc/>
        public async Task Render(Sections ElementParameter)
        {
            await this.RenderChildren(ElementParameter);
        }

        /// <inheritdoc/>
        public Task Render(SectionSeparator ElementParameter)
        {
            ContentView BackupContentView = (ContentView)this.currentElement;
            Rectangle SeparatorRect = new()
				{
                Fill = Brush.Black,
                HeightRequest = 1,
                Aspect = Stretch.Fill
            };
            BackupContentView.Margin = AppStyles.SmallTopMargins + AppStyles.SmallBottomMargins;
            BackupContentView.Content = SeparatorRect;
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task Render(Table ElementParameter)
        {
            // Copy your old Table rendering or adapt as needed
            // For brevity, we’ll do a minimal approach or copy from existing logic
            await Task.Run(() => { /* Implement table logic here */ });
        }

        /// <inheritdoc/>
        public Task Render(TaskItem ElementParameter)
        {
            return this.RenderChild(ElementParameter);
        }

        /// <inheritdoc/>
        public async Task Render(TaskList ElementParameter)
        {
            // Copy your old TaskList logic or adapt
            await Task.Run(() => { /* Implement task list logic here */ });
        }

        /// <inheritdoc/>
        public Task Render(UnnumberedItem ElementParameter)
        {
            return this.RenderChild(ElementParameter);
        }

        // ------------------------------------------------------------------------
        // IDisposable
        // ------------------------------------------------------------------------
        private bool isDisposed;

        /// <summary>
        /// Disposes the renderer.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Cleans up native resources if needed.
        /// </summary>
        /// <param name="Disposing">Indicates if disposing is in progress.</param>
        protected virtual void Dispose(bool Disposing)
        {
            if (this.isDisposed)
                return;

            if (Disposing)
            {
                // Dispose of anything that needs disposing
            }
            this.isDisposed = true;
        }
    }
}
