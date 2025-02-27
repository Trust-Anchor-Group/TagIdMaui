using CommunityToolkit.Maui.Markup;
using Microsoft.Maui.Controls;
using SkiaSharp;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;
using Waher.Content;
using Waher.Content.Xml;
using Waher.Content.Emoji;
using Waher.Content.Markdown;
using Waher.Content.Markdown.Model;
using Waher.Content.Markdown.Model.BlockElements;
using Waher.Content.Markdown.Model.SpanElements;
using Waher.Content.Markdown.Rendering;
using Waher.Content.Markdown.Model.Multimedia;
using Waher.Events;
using Waher.Script;
using Waher.Script.Constants;
using Waher.Script.Graphs;
using Waher.Script.Operators.Matrices;
using Microsoft.Maui.Graphics.Text;
using Waher.Script.Functions.Analytic;
using ImageSource = Waher.Content.Emoji.ImageSource;
using MarkdownContent = Waher.Content.Markdown.MarkdownContent;
using Svg;
using Microsoft.Maui.Controls.Shapes;

namespace NeuroAccessMaui.UI.Rendering
{
	/// <summary>
	/// Renders MAUI objects in a verticalStackLayout from a Markdown document.
	/// </summary>
	/// <remarks>
	/// Modified from original in Waher.Content.Markdown.Xamarin library, with permission.
	/// This class is a generic markdown renderer, modified to render MAUI objects instead
	/// of XAML. Currently only tested to support a subset of the markdown language used in contracts.
	/// </remarks>
	public class MauiRenderer : IRenderer
	{
		#region Fields & properties

		/// <summary>
		/// Reference to Markdown document being processed.
		/// </summary>
		public MarkdownDocument Document;

		/// <summary>
		/// The VerticalStackLayout Being created
		/// </summary>
		private VerticalStackLayout mainStackLayout;

		/// <summary>
		/// The current element being processed
		/// </summary>
		private View currentElement;

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
		/// Current text-alignment.
		/// </summary>
		public Waher.Content.Markdown.Model.TextAlignment Alignment = Waher.Content.Markdown.Model.TextAlignment.Left;

		#endregion

		#region Entry & Exit
		/// <summary>
		/// Initializes a new instance of the <see cref="MauiRenderer"/> class.
		/// </summary>
		/// <param name="Document">The <see cref="MarkdownDocument"/> to render.</param>
		public MauiRenderer(MarkdownDocument Document)
		{
			this.Document = Document;
			this.mainStackLayout = new VerticalStackLayout();
			this.mainStackLayout.Spacing = 10;
			this.currentElement = new ContentView();
		}

		/// <summary>
		/// Retrieves the final <see cref="VerticalStackLayout"/> containing all rendered views.
		/// </summary>
		/// <returns>The rendered content or null if no children.</returns>
		public VerticalStackLayout? Output()
		{
			if (this.mainStackLayout.Children.Count == 0) return null;
			return this.mainStackLayout;
		}
		#endregion

		#region Render Document

		/// <summary>
		/// Sets up default state and calls main render function.
		/// </summary>
		/// <param name="Document">Document to render.</param>
		/// <param name="Inclusion">If the rendered output is to be included in another document (true), or if it is a standalone document (false).</param>
		public Task RenderDocument(MarkdownDocument Document, bool Inclusion)
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

			return this.RenderDocumentEntry(Document, Inclusion);
		}

		/// <summary>
		/// Renders a document.
		/// </summary>
		/// <param name="Document">Document to render.</param>
		/// <param name="Inclusion">If the rendered output is to be included in another document (true), or if it is a standalone document (false).</param>
		public virtual async Task RenderDocumentEntry(MarkdownDocument Document, bool Inclusion)
		{
			MarkdownDocument DocBak = this.Document;

			this.Document = Document;

			if (!Inclusion && this.Document.TryGetMetaData("BODYONLY", out KeyValuePair<string, bool>[] Values))
			{
				if (CommonTypes.TryParse(Values[0].Key, out bool B) && B)
					Inclusion = true;
			}

			if (!Inclusion)
				await this.RenderDocumentHeader();

			foreach (MarkdownElement E in this.Document.Elements)
			{
				this.currentElement = new ContentView();

				await E.Render(this);

				this.mainStackLayout.Add(this.currentElement);
			}

			if (this.NeedsToDisplayFootnotes())
			{
				await this.RenderFootnotes();
				this.mainStackLayout.Add(this.currentElement);
			}

			this.Document = DocBak;
		}

		/// <summary>
		/// Renders the document header.
		/// </summary>
		public Task RenderDocumentHeader()
		{
			return Task.CompletedTask;
		}

		/// <summary>
		/// Renders footnotes.
		/// </summary>
		public async Task RenderFootnotes()
		{
			Footnote CurrentFootnote;
			int FootnoteNumber;
			int RowIndex = 0;

			ContentView SeparatorContentView = new ContentView
			{
				Content = new Rectangle
				{
					Fill = Brush.Black,
					HeightRequest = 1,
					Aspect = Stretch.Fill
				}
			};

			this.mainStackLayout.Add(SeparatorContentView);

			Grid FootnoteGrid = new Grid
			{
				RowSpacing = 0,
				ColumnSpacing = 0,
				ColumnDefinitions =
				{
					new ColumnDefinition(GridLength.Auto),
					new ColumnDefinition(GridLength.Star)
				} 
			};

			foreach (string Key in this.Document.FootnoteOrder)
			{
				if ((this.Document?.TryGetFootnoteNumber(Key, out FootnoteNumber) ?? false) &&
					(this.Document?.TryGetFootnote(Key, out CurrentFootnote) ?? false) &&
					CurrentFootnote.Referenced)
				{
					FootnoteGrid.AddRowDefinition(new RowDefinition { Height = GridLength.Auto });
				}
			}

			if (this.Document is not null)
			{
				foreach (string Key in this.Document.FootnoteOrder)
				{
					if ((this.Document?.TryGetFootnoteNumber(Key, out FootnoteNumber) ?? false) &&
						(this.Document?.TryGetFootnote(Key, out CurrentFootnote) ?? false) &&
						CurrentFootnote.Referenced)
					{
						ContentView FootnoteNumberView = new ContentView
						{
							Margin = AppStyles.SmallMargins,
							Scale = 0.75,
							TranslationY = -5,
							Content = new Label
							{
								Text = FootnoteNumber.ToString(CultureInfo.InvariantCulture)
							},
						};

						FootnoteGrid.Add(FootnoteNumberView, 0, RowIndex);

						ContentView FootnoteContentView = new ContentView();
						this.currentElement = FootnoteContentView;
						await this.Render(CurrentFootnote);

						FootnoteGrid.Add(FootnoteContentView, 1, RowIndex);

						RowIndex++;
					}
				}
			}

			this.currentElement = FootnoteGrid;
		}
		#endregion

		#region Children Render Helpers

		/// <summary>
		/// Renders the children of <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element being rendered</param>
		public async Task RenderChildren(MarkdownElementChildren Element)
		{
			if (this.InLabel && !(Element.Children is null))
			{
				foreach (MarkdownElement E in Element.Children)
				{
					await E.Render(this);
				}
			}
			else
			{
				ContentView Bakup = (ContentView)this.currentElement;

				VerticalStackLayout ChildrenContainer = new();
				ChildrenContainer.Spacing = 8; // Same size as small margins
				Bakup.Content = ChildrenContainer;

				if (!(Element.Children is null))
				{
					foreach (MarkdownElement E in Element.Children)
					{
						ContentView ChildContentView = new ContentView();
						this.currentElement = ChildContentView;
						await E.Render(this);
						ChildrenContainer.Add(ChildContentView);
					}
				}
				this.currentElement = Bakup; 
			} 
		}

		/// <summary>
		/// Renders the children of <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element being rendered</param>
		public async Task RenderChildren(MarkdownElement Element)
		{
			IEnumerable<MarkdownElement> Children = Element.Children;

			if (this.InLabel && !(Children is null))
			{
				foreach (MarkdownElement E in Children)
				{
					await E.Render(this);
				}
			}
			else
			{
				ContentView Bakup = (ContentView)this.currentElement;

				VerticalStackLayout ChildrenContainer = new();
				Bakup.Content = ChildrenContainer;

				if (!(Children is null))
				{
					foreach (MarkdownElement E in Children)
					{
						ContentView ChildContentView = new ContentView();
						this.currentElement = ChildContentView;
						await E.Render(this);
						ChildrenContainer.Add(ChildContentView);
					}
				}

				this.currentElement = Bakup;
			}
		}

		/// <summary>
		/// Renders the child of <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element being rendered</param>
		public Task RenderChild(MarkdownElementSingleChild Element)
		{
			if (Element.Child is null)
				return Task.CompletedTask;

			Element.Child.Render(this);

			return Task.CompletedTask;
		}

		#endregion

		#region Base Element Renderers

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public async Task Render(Footnote Element)
		{
			await this.RenderChildren(Element);
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public async Task Render(Abbreviation Element)
		{
		   await this.RenderChildren(Element);
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public async Task Render(Delete Element)
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
		public async Task Render(Emphasize Element)
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
		public Task Render(UnnumberedItem Element)
		{
			this.RenderChild(Element);

			return Task.CompletedTask;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public async Task Render(Underline Element)
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
		public async Task Render(TaskList Element)
		{
			ContentView Bakup = (ContentView)this.currentElement;

			int RowIndex = 0;
			bool ParagraphBullet;

			Grid TaskListGrid = new Grid
			{
				RowSpacing = 0,
				ColumnSpacing = 0,
				ColumnDefinitions =
				{
					new ColumnDefinition { Width = GridLength.Auto },
					new ColumnDefinition { Width = GridLength.Star },
				},
			};

			foreach (MarkdownElement _ in Element.Children)
			{
				TaskListGrid.AddRowDefinition(new RowDefinition { Height = GridLength.Auto });
			}

			foreach (MarkdownElement E in Element.Children)
			{
				if (E is TaskItem TaskItem)
				{
					ParagraphBullet = !E.InlineSpanElement || E.OutsideParagraph;

					if (TaskItem.IsChecked)
					{
						this.RenderContentView(AppStyles.SmallRightMargins);

						ContentView CheckmarkContentView = (ContentView)this.currentElement;

						CheckmarkContentView.Column(0);
						CheckmarkContentView.Row(RowIndex);

						CheckmarkContentView.Content = (new Label { Text = "✓" });

						TaskListGrid.Add(CheckmarkContentView);
					}

					ContentView TaskContentView = new ContentView();
					TaskContentView.Column(1);
					TaskContentView.Row(RowIndex);

					this.currentElement = TaskContentView;

					if (ParagraphBullet)
						await E.Render(this);
					else
						await this.RenderLabel(TaskItem, false);

					TaskListGrid.Add(TaskContentView);
				}

				RowIndex++;
			}

			Bakup.Content = TaskListGrid;
			this.currentElement = Bakup;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public Task Render(TaskItem Element)
		{
			this.RenderChild(Element);

			return Task.CompletedTask;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public async Task Render(Table Element)
		{
			ContentView Bakup = (ContentView)this.currentElement;

			int Column;
			int RowIndex, NrRows;
			int RowNr = 0;

			Grid TableGrid = new Grid
			{
				RowSpacing = -2,
				ColumnSpacing = -2,
			};

			// TODO: Tooltip/caption

			for (Column = 0; Column < Element.Columns; Column++)
			{
				TableGrid.AddColumnDefinition(new ColumnDefinition { Width = GridLength.Auto });
			}

			for (RowIndex = 0, NrRows = Element.Rows.Length + Element.Headers.Length; RowIndex < NrRows; RowIndex++)
			{
				TableGrid.AddRowDefinition(new RowDefinition { Height = GridLength.Auto });
			}

			ScrollView TableScrollView = new ScrollView
			{
				Orientation = ScrollOrientation.Horizontal,
				Content = TableGrid
			};

			for (RowIndex = 0, NrRows = Element.Headers.Length; RowIndex < NrRows; RowIndex++, RowNr++)
			{
				this.currentElement = TableGrid;
				await this.Render(Element.Headers[RowIndex], Element.HeaderCellAlignments[RowIndex], RowNr, true, Element);
			}

			for (RowIndex = 0, NrRows = Element.Rows.Length; RowIndex < NrRows; RowIndex++, RowNr++)
			{
				this.currentElement = TableGrid;
				await this.Render(Element.Rows[RowIndex], Element.RowCellAlignments[RowIndex], RowNr, false, Element);
			}

			Bakup.Content = TableScrollView;
			this.currentElement = Bakup;
		}

		/// <summary>
		/// Renders cell content for a table.
		/// </summary>
		/// <param name="CurrentRow"></param>
		/// <param name="CellAlignments"></param>
		/// <param name="RowNr"></param>
		/// <param name="Bold"></param>
		/// <param name="Element"></param>
		/// <returns></returns>
		private async Task Render(MarkdownElement[] CurrentRow, Waher.Content.Markdown.Model.TextAlignment?[] CellAlignments,
			int RowNr, bool Bold, Table Element)
		{
			MarkdownElement E;
			Waher.Content.Markdown.Model.TextAlignment TextAlignment;
			int Column;
			int NrColumns = Element.Columns;
			int ColSpan;
			StateBackup Bak = this.Backup();

			Grid Bakup = (Grid)this.currentElement;

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

				Frame Frame = new Frame();

				if ((RowNr & 1) == 0)
					Frame.Style = AppStyles.TableCellEven;

				else
					Frame.Style = AppStyles.TableCellOdd;

				Frame.Column(Column);
				Frame.Row(RowNr);

				if (ColSpan > 1)
					Frame.ColumnSpan(ColSpan);

				if (E.InlineSpanElement)
				{
					this.RenderContentView(TextAlignment, new Thickness(0, 0, 0, 0), AppStyles.TableCell);

					ContentView Cv = (ContentView)this.currentElement;
					this.currentElement = Cv;

					this.Bold = Bold;
					await this.RenderLabel(E, true);

					Frame.Content = Cv;
					this.Bold = false;
				}
				else
				{
					ContentView Cv = new ContentView
					{
						Style = AppStyles.TableCell
					};
					this.currentElement = Cv;

					await E.Render(this);

					Frame.Content = Cv;
				}
				Bakup.Add(Frame);
			}
			this.Restore(Bak);

			this.currentElement = Bakup;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public async Task Render(SuperScript Element)
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
		public async Task Render(SubScript Element)
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
		public async Task Render(Strong Element)
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
		public async Task Render(StrikeThrough Element)
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
		public Task Render(SectionSeparator Element)
		{
			ContentView Bakup = (ContentView)this.currentElement;

			Rectangle Separator = new Rectangle
			{
				Fill = Brush.Black,
				HeightRequest = 1,
				Aspect = Stretch.Fill
			};

			Bakup.Margin = AppStyles.SmallTopMargins + AppStyles.SmallBottomMargins;
			Bakup.Content = Separator;

			return Task.CompletedTask;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public async Task Render(Sections Element)
		{
			await this.RenderChildren(Element);
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public async Task Render(RightAligned Element)
		{
			Waher.Content.Markdown.Model.TextAlignment Bak = this.Alignment;
			this.Alignment = Waher.Content.Markdown.Model.TextAlignment.Right;

			await this.RenderChildren(Element);

			this.Alignment = Bak; 
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public async Task Render(Paragraph Element)
		{
			await this.RenderLabel(Element, false);
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public async Task Render(NumberedList Element)
		{
			ContentView Bakup = (ContentView)this.currentElement;

			int Expected = 0;
			int RowIndex = 0;
			bool ParagraphBullet;

			Grid ListGrid = new Grid
			{
				RowSpacing = 0,
				ColumnSpacing = 0,
				ColumnDefinitions =
				{
					new ColumnDefinition { Width = GridLength.Auto },
					new ColumnDefinition { Width = GridLength.Star }
				}
			};

			foreach (MarkdownElement _ in Element.Children)
			{
				ListGrid.AddRowDefinition(new RowDefinition { Height = GridLength.Auto });
			}

			foreach (MarkdownElement E in Element.Children)
			{
				if (E is BlockElementSingleChild Item)
				{
					Expected++;

					ParagraphBullet = !E.InlineSpanElement || E.OutsideParagraph;

					this.RenderContentView(AppStyles.SmallRightMargins);
					ContentView InnerContentView = (ContentView)this.currentElement;
					InnerContentView.Column(0);
					InnerContentView.Row(RowIndex);

					Label Label = new Label();

					if (Item is NumberedItem NumberedItem)
						Label.Text = (Expected = NumberedItem.Number).ToString(CultureInfo.InvariantCulture) + ".";
					else
						Label.Text = Expected.ToString(CultureInfo.InvariantCulture) + ".";

					InnerContentView.Content = Label;
					ListGrid.Add(InnerContentView);

					ContentView ItemContentView = new ContentView();
					ItemContentView.Column(1);
					ItemContentView.Row(RowIndex);
					this.currentElement = ItemContentView;

					if (ParagraphBullet)
					{
						await E.Render(this);
					}
					else
					{
						await this.RenderLabel(Item, false);
					}

					ListGrid.Add(ItemContentView);
				}

				RowIndex++;
			}

			Bakup.Content = ListGrid;
			this.currentElement = Bakup;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public Task Render(NumberedItem Element)
		{
			this.RenderChild(Element);

			return Task.CompletedTask;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public async Task Render(NestedBlock Element)
		{
			ContentView Bakup = (ContentView)this.currentElement;

			if (Element.HasOneChild)
			{
				await Element.FirstChild.Render(this);
			}
			else
			{
				HtmlSettings Settings = new()
				{
					XmlEntitiesOnly = true
				};
				HtmlRenderer? Html = null;

				VerticalStackLayout OuterVerticalStackLayout = new VerticalStackLayout();

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
								Label HTMLLabel = new Label
								{
									LineBreakMode = LineBreakMode.WordWrap,
									HorizontalTextAlignment = this.LabelAlignment(),
									TextType = TextType.Html,
									Text = Html.ToString()
								};

								Html.Dispose();
								Html = null;

								OuterVerticalStackLayout.Add(HTMLLabel);
							}

							ContentView InnerContentView = new ContentView();
							this.currentElement = InnerContentView;
							await E.Render(this);
							OuterVerticalStackLayout.Add(InnerContentView);
						}
					}

					if (Html is not null)
					{
						Label HTMLLabel = new Label
						{
							LineBreakMode = LineBreakMode.WordWrap,
							HorizontalTextAlignment = this.LabelAlignment(),
							TextType = TextType.Html,
							Text = Html.ToString()
						};
						OuterVerticalStackLayout.Add(HTMLLabel);
					}
				}
				finally
				{
					Html?.Dispose();
				}

				Bakup.Content = OuterVerticalStackLayout;
				this.currentElement = Bakup;
			}
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public async Task Render(MultimediaReference Element)
		{
			Waher.Content.Markdown.Model.SpanElements.Multimedia Multimedia = Element.Document.GetReference(Element.Label);

			if (Multimedia is not null)
			{
				// TODO
				IMultimediaMauiXamlRenderer Renderer = Multimedia.MultimediaHandler<IMultimediaMauiXamlRenderer>();
				if (Renderer is not null)
				{
					await this.RenderMaui(Multimedia);
					return;
				}
			} 
			await this.RenderChildren(Element);
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public async Task Render(Waher.Content.Markdown.Model.SpanElements.Multimedia Element)
		{
			IMultimediaMauiXamlRenderer Renderer = Element.MultimediaHandler<IMultimediaMauiXamlRenderer>();
			if (Renderer is null)
				await this.RenderChildren(Element);
			else
				await this.RenderMaui(Element);
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public Task Render(MetaReference Element)
		{
			StringBuilder StringBuilder = new();
			bool FirstOnRow = true;

			if (Element.TryGetMetaData(out KeyValuePair<string, bool>[] Values))
			{
				foreach (KeyValuePair<string, bool> P in Values)
				{
					if (FirstOnRow)
						FirstOnRow = false;
					else
						StringBuilder.Append(' ');

					StringBuilder.Append(P.Key);
					if (P.Value)
					{
						StringBuilder.Append(Environment.NewLine);
						FirstOnRow = true;
					}
				}
			}

			this.RenderSpan(StringBuilder.ToString());

			return Task.CompletedTask;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public async Task Render(MarginAligned Element)
		{
			Waher.Content.Markdown.Model.TextAlignment Bak = this.Alignment;
			this.Alignment = Waher.Content.Markdown.Model.TextAlignment.Left;

			await this.RenderChildren(Element);

			this.Alignment = Bak; 
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public async Task Render(LinkReference Element)
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
		public async Task Render(Link Element)
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
		public Task Render(LineBreak Element)
		{
			this.RenderSpan(Environment.NewLine);

			return Task.CompletedTask;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public async Task Render(LeftAligned Element)
		{
			Waher.Content.Markdown.Model.TextAlignment Bak = this.Alignment;
			this.Alignment = Waher.Content.Markdown.Model.TextAlignment.Left;

			await this.RenderChildren(Element);

			this.Alignment = Bak; 
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public Task Render(InvisibleBreak Element)
		{
			//TODO
			return Task.CompletedTask;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public async Task Render(InsertBlocks Element)
		{
			ContentView Bakup = (ContentView)this.currentElement;

			Border BlockBorder = new Border
			{
				Padding = AppStyles.SmallMargins,
				Stroke = new SolidColorBrush
				{
					Color = AppColors.InsertedBorder,
				},
				StrokeThickness = 1,
				StrokeShape = new RoundRectangle
				{
					CornerRadius = 2
				}
			};

			Bakup.Content = BlockBorder;

			ContentView InnerContentView = new ContentView();
			this.currentElement = InnerContentView;

			await this.RenderChildren(Element);

			BlockBorder.Content = InnerContentView;
			this.currentElement = Bakup;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public async Task Render(Insert Element)
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
		public Task Render(InlineText Element)
		{
			this.RenderSpan(Element.Value);

			return Task.CompletedTask;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public async Task Render(InlineScript Element)
		{
			object Result = await Element.EvaluateExpression();
			await this.RenderObject(Result, Element.AloneInParagraph, Element.Variables);
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public Task Render(InlineHTML Element)
		{
			if (this.currentElement is Label ExistingLabel)
			{
				ExistingLabel.TextType = TextType.Html;
				ExistingLabel.Text = $"<--- {Element.HTML} --->";
			}
			else
			{
				ContentView Bakup = (ContentView)this.currentElement;

				Label NewLabel = new Label
				{
					TextType = TextType.Html,
					Text = $"<--- {Element.HTML} --->",
				};

				Bakup.Content = NewLabel;
			}

			return Task.CompletedTask;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public Task Render(InlineCode Element)
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
		public Task Render(HtmlEntityUnicode Element)
		{
			this.RenderSpan(new string((char)Element.Code, 1));

			return Task.CompletedTask;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public Task Render(HtmlEntity Element)
		{
			string S = Waher.Content.Html.HtmlEntity.EntityToCharacter(Element.Entity);
			if (!string.IsNullOrEmpty(S))
			{
				this.RenderSpan(S);
			}

			return Task.CompletedTask;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public async Task Render(HtmlBlock Element)
		{
			ContentView Bakup = (ContentView)this.currentElement;

			Thickness Margins = AppStyles.SmallTopMargins + AppStyles.SmallBottomMargins;
			Bakup.Margin = Margins;

			Label HTMLTextLabel = new Label
			{
				LineBreakMode = LineBreakMode.WordWrap,
				HorizontalTextAlignment = this.LabelAlignment(),
				TextType = TextType.Html,
			};

			using HtmlRenderer HTMLRenderer = new(new HtmlSettings()
			{
				XmlEntitiesOnly = true
			}, this.Document);

			await HTMLRenderer.RenderChildren(Element);

			HTMLTextLabel.Text = HTMLRenderer.ToString();

			Bakup.Content = HTMLTextLabel;
			this.currentElement = Bakup;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public Task Render(HorizontalRule Element)
		{
			ContentView CurrentContentView = (ContentView)this.currentElement;

			CurrentContentView.Content = new Rectangle
			{
				Fill = Brush.Black,
				HeightRequest = 1,
				Aspect = Stretch.Fill,
			};

			return Task.CompletedTask;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public async Task Render(Header Element)
		{
			int Level = Math.Max(0, Math.Min(9, Element.Level));

			Label HeaderLabel = new Label
			{
				LineBreakMode = LineBreakMode.WordWrap,
				HorizontalTextAlignment = this.LabelAlignment(),
				TextType = TextType.Html
			};

			using (HtmlRenderer Renderer = new(new HtmlSettings()
			{
				XmlEntitiesOnly = true
			}, this.Document))
			{
				await Renderer.RenderChildren(Element);

				HeaderLabel.Text = Renderer.ToString();
			}

			HeaderLabel.Style = AppStyles.GetHeaderStyle(Level);

			ContentView Bakup = (ContentView)this.currentElement;
			Bakup.Content = HeaderLabel;
			this.currentElement = Bakup;
			
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public Task Render(HashTag Element)
		{
			ContentView Bakup = (ContentView)this.currentElement;

			ContentView InnerContentView = new();
			this.currentElement = InnerContentView;

			Border ContentBorder = new Border
			{
				Background = Color.FromArgb("FFFAFAD2"),
				Content = InnerContentView
			};

			this.RenderSpan(Element.Tag);

			Bakup.Content = ContentBorder;
			return Task.CompletedTask;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public async Task Render(FootnoteReference Element)
		{
			if (!(this.Document?.TryGetFootnote(Element.Key, out Footnote? Footnote) ?? false))
				Footnote = null;

			if (Element.AutoExpand && Footnote is not null)
				await this.Render(Footnote);
			else if (this.Document?.TryGetFootnoteNumber(Element.Key, out int Nr) ?? false)
			{
				bool Bak = this.Superscript;
				this.Superscript = true;

				await this.RenderSpan(Nr.ToString(CultureInfo.InvariantCulture));

				this.Superscript = Bak; 

				if (Footnote is not null)
					Footnote.Referenced = true;
			}
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public async Task Render(EmojiReference Element)
		{
			if (this.InLabel)
				await this.RenderSpan(Element.Emoji.Unicode);
			else
			{
				IEmojiSource EmojiSource = this.Document.EmojiSource;

				if (EmojiSource is null)
					await this.RenderSpan(Element.Delimiter + Element.Emoji.ShortName + Element.Delimiter);
				else if (!EmojiSource.EmojiSupported(Element.Emoji))
					await this.RenderSpan(Element.Emoji.Unicode);
				else
				{
					Waher.Content.Emoji.IImageSource Source = await this.Document.EmojiSource.GetImageSource(Element.Emoji, Element.Level);
					await this.OutputImage(Source);
				}
			}
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public Task Render(DetailsReference Element)
		{
			if (this.Document.Detail is not null)
				this.RenderDocument(this.Document.Detail, false);
			else
				this.Render((MetaReference)Element);

			return Task.CompletedTask;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public async Task Render(DeleteBlocks Element)
		{
			ContentView Bakup = (ContentView)this.currentElement;

			Border Border = new Border
			{
				Padding = AppStyles.SmallMargins,
				Stroke = new SolidColorBrush
				{
					Color = AppColors.DeletedBorder,
				},
				StrokeThickness = 1,
				StrokeShape = new RoundRectangle
				{
					CornerRadius = 2
				}
			};

			Bakup.Content = Border;

			ContentView InnerContentView = new ContentView();
			this.currentElement = InnerContentView;

			await this.RenderChildren(Element);

			Border.Content = InnerContentView;
			this.currentElement = Bakup;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public async Task Render(DefinitionTerms Element)
		{
			bool Top = true;

			ContentView Bakup = (ContentView)this.currentElement;

			foreach (MarkdownElement Term in Element.Children)
			{
				Thickness Margins = AppStyles.SmallLeftMargins + AppStyles.SmallRightMargins + AppStyles.SmallBottomMargins;
				if (Top)
					Margins += AppStyles.SmallTopMargins;

				this.RenderContentView(Margins);
				ContentView InnerContentView = (ContentView)this.currentElement;
				this.currentElement = InnerContentView;

				bool BoldBak = this.Bold;
				this.Bold = true;

				await this.RenderLabel(Term, true);
				Bakup.Content = InnerContentView;
				this.Bold = BoldBak;

				Top = false;
			}

			this.currentElement = Bakup;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public async Task Render(DefinitionList Element)
		{
			await this.RenderChildren(Element);
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public async Task Render(DefinitionDescriptions Element)
		{
			ContentView Bakup = (ContentView)this.currentElement;

			MarkdownElement? Last = null;

			foreach (MarkdownElement Description in Element.Children)
				Last = Description;

			foreach (MarkdownElement Description in Element.Children)
			{
				if (Description.InlineSpanElement && !Description.OutsideParagraph)
				{
					Bakup.Margin = AppStyles.SmallTopMargins + AppStyles.SmallBottomMargins;

					Label HTMLLabel = new Label
					{
						LineBreakMode = LineBreakMode.WordWrap,
						HorizontalTextAlignment = this.LabelAlignment(),
						TextType = TextType.Html
					};

					using (HtmlRenderer Renderer = new(new HtmlSettings()
					{
						XmlEntitiesOnly = true
					}, this.Document))
					{
						await Description.Render(Renderer);
						HTMLLabel.Text = Renderer.ToString();
					}

					Bakup.Content = HTMLLabel;
				}
				else
				{
					Bakup.Padding = AppStyles.SmallLeftMargins;

					if (Description == Last)
						Bakup.Padding += AppStyles.SmallBottomMargins;

					ContentView InnerCV = new();
					this.currentElement = InnerCV;
					await Description.Render(this); //TODO?

					//No idea why a VSL is used for just one element but that is the case in the original renderer
					VerticalStackLayout Vsl = new VerticalStackLayout();
					Vsl.Add(InnerCV);
					Bakup.Content = Vsl;
				}
			}
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public async Task Render(CodeBlock Element)
		{
			ContentView Bakup = (ContentView)this.currentElement;
			VerticalStackLayout BlockStackLayout = new();

			StringBuilder Output = new();
			MauiXamlRenderer Rend = new(Output, XML.WriterSettings(false, true));
			ICodeContentMauiXamlRenderer Renderer = Element.CodeContentHandler<ICodeContentMauiXamlRenderer>();

			if (Renderer is not null)
			{
				try
				{
					// TODO ?
					if (await Renderer.RenderMauiXaml(Rend, Element.Rows, Element.Language, Element.Indent, Element.Document))
						return;
				}
				catch (Exception Ex)
				{
					Ex = Log.UnnestException(Ex);

					if (Ex is AggregateException Ex2)
					{
						foreach (Exception Ex3 in Ex2.InnerExceptions)
						{
							Label ExceptionLabel = new Label
							{
								LineBreakMode = LineBreakMode.WordWrap,
								TextColor = AppColors.Alert,
								Text = Ex3.Message
							};

							BlockStackLayout.Add(ExceptionLabel);
						}
					}
					else
					{
						Label ExceptionLabel = new Label
						{
							LineBreakMode = LineBreakMode.WordWrap,
							TextColor = AppColors.Alert,
							Text = Ex.Message
						};

						BlockStackLayout.Add(ExceptionLabel);
					}
				}
			}

			for (int i = Element.Start; i <= Element.End; i++)
			{
				Label ContentLabel = new Label
				{
					LineBreakMode = LineBreakMode.NoWrap,
					HorizontalTextAlignment = this.LabelAlignment(),
					FontFamily = "SpaceGroteskRegular",
					Text = Element.Rows[i]
				};

				BlockStackLayout.Add(ContentLabel);
			}

			Bakup.Content = BlockStackLayout;
			this.currentElement = Bakup;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public Task Render(CommentBlock Element)
		{
			//TODO
			return Task.CompletedTask;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public async Task Render(BulletList Element)
		{
			ContentView Bakup = (ContentView)this.currentElement;

			int Row = 0;
			bool ParagraphBullet;

			Grid ListGrid = new Grid
			{
				RowSpacing = 0,
				ColumnSpacing = 0,
				ColumnDefinitions =
				{
					new ColumnDefinition { Width = GridLength.Auto },
					new ColumnDefinition { Width = GridLength.Star }
				},
			};

			foreach (MarkdownElement _ in Element.Children)
			{
				ListGrid.AddRowDefinition(new RowDefinition { Height = GridLength.Auto });
			}

			foreach (MarkdownElement E in Element.Children)
			{
				if (E is UnnumberedItem Item)
				{
					ParagraphBullet = !E.InlineSpanElement || E.OutsideParagraph;

					this.RenderContentView(AppStyles.SmallRightMargins);

					ContentView StarContentView = (ContentView)this.currentElement;
					StarContentView.Column(0);
					StarContentView.Row(Row);

					Label Lbl = new Label { Text = "•" };
					StarContentView.Content = Lbl;
					ListGrid.Add(StarContentView);

					ContentView VslContainer = new ContentView();
					VslContainer.Column(1);
					VslContainer.Row(Row);
					this.currentElement = VslContainer;

					if (ParagraphBullet)
						await E.Render(this);
					else
						await this.RenderLabel(Item, false);

					ListGrid.Add(VslContainer);
				}

				Row++;
			}

			Bakup.Content = ListGrid;
			this.currentElement = Bakup;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public async Task Render(CenterAligned Element)
		{
			Waher.Content.Markdown.Model.TextAlignment Bak = this.Alignment;
			this.Alignment = Waher.Content.Markdown.Model.TextAlignment.Center;

			await this.RenderChildren(Element);

			this.Alignment = Bak; 
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public async Task Render(BlockQuote Element)
		{
			ContentView Bakup = (ContentView)this.currentElement;

			Border BlockQuoteBorder = new Border
			{
				Padding = AppStyles.SmallMargins,
				Stroke = new SolidColorBrush
				{
					Color = AppColors.PrimaryForeground,
				},
				StrokeThickness = 1,
				StrokeShape = new RoundRectangle
				{
					CornerRadius = 2
				}
			};

			Bakup.Content = BlockQuoteBorder;

			ContentView BlockQuoteContentView = new ContentView();
			this.currentElement = BlockQuoteContentView;

			await this.RenderChildren(Element);

			BlockQuoteBorder.Content = BlockQuoteContentView;
			this.currentElement = Bakup;
		}

		/// <summary>
		/// Renders <paramref name="Element"/>.
		/// </summary>
		/// <param name="Element">Element to render</param>
		public Task Render(AutomaticLinkUrl Element)
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
		public Task Render(AutomaticLinkMail Element)
		{
			string? Bak = this.Hyperlink;
			this.Hyperlink = "mailto:" + Element.EMail;
			this.RenderSpan(this.Hyperlink);
			this.Hyperlink = Bak; 

			return Task.CompletedTask;
		}

		#endregion

		#region Label & Span

		/// <summary>
		/// Handles rendering of labels, in or outside of ContentViews
		/// </summary>
		/// <param name="Element"></param>
		/// <param name="IncludeElement"></param>
		/// <returns></returns>
		internal async Task RenderLabel(MarkdownElement Element, bool IncludeElement)
		{
			ContentView Bakup = (ContentView)this.currentElement;

			bool HasLink = !Element.ForEach((E, _) =>
			{
				return !(
					E is AutomaticLinkMail ||
					E is AutomaticLinkUrl ||
					E is Link ||
					E is LinkReference);
			}, null);

			Label ParentLabel = new Label
			{
				LineBreakMode = LineBreakMode.WordWrap,
				HorizontalTextAlignment = this.LabelAlignment()
			};

			this.currentElement = ParentLabel;

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

					if (IncludeElement)
						await Element.Render(this);
					else
						await this.RenderChildren(Element);

					this.InLabel = false;
				}
			}
			else
			{
				ParentLabel.TextType = TextType.Html;

				if (this.Bold)
					ParentLabel.FontAttributes = FontAttributes.Bold;

				using HtmlRenderer Renderer = new(new HtmlSettings()
				{
					XmlEntitiesOnly = true
				}, this.Document);

				if (IncludeElement)
					await Element.Render(Renderer);
				else
					await Renderer.RenderChildren(Element);

				ParentLabel.Text = Renderer.ToString();
			}

			Bakup.Content = ParentLabel;
			this.currentElement = Bakup;
		}

		/// <summary>
		/// Renders individual spans inside of formattedstring property of labels
		/// </summary>
		/// <param name="Text"></param>
		/// <returns></returns>
		internal Task RenderSpan(string Text)
		{
			Label MainLabel;
			FormattedString FormattedString;

			if (!this.InLabel)
			{
				MainLabel = new Label
				{
					LineBreakMode = LineBreakMode.WordWrap,
				};

				ContentView Cv = (ContentView)this.currentElement;
				Cv.Content = MainLabel;
				this.currentElement = Cv;
			}
			else
			{
				MainLabel = (Label)this.currentElement;
				this.currentElement = MainLabel;
			}

			if (MainLabel.FormattedText == null)
			{
				FormattedString = new();
			}
			else
			{
				FormattedString = MainLabel.FormattedText;
			}

			Span MainSpan = new Span();
			FormattedString.Spans.Add(MainSpan);
			MainLabel.FormattedText = FormattedString;

			if (this.Superscript)
				Text = TextRenderer.ToSuperscript(Text);
			else if (this.Subscript)
				Text = TextRenderer.ToSubscript(Text);

			MainSpan.Text = Text;

			if (this.Bold && this.Italic)
				MainSpan.FontAttributes = FontAttributes.Bold | FontAttributes.Italic;
			else if (this.Bold)
				MainSpan.FontAttributes = FontAttributes.Bold;
			else if (this.Italic)
				MainSpan.FontAttributes = FontAttributes.Italic;

			if (this.StrikeThrough && this.Underline)
				MainSpan.TextDecorations = TextDecorations.Underline | TextDecorations.Strikethrough;
			else if (this.StrikeThrough)
				MainSpan.TextDecorations = TextDecorations.Strikethrough;
			else if (this.Underline)
				MainSpan.TextDecorations = TextDecorations.Underline;

			if (this.Code)
				MainSpan.FontFamily = "SpaceGroteskRegular";

			if (this.Hyperlink is not null)
			{
				MainSpan.TextColor = AppColors.BlueLink;

				MainSpan.GestureRecognizers.Add(new TapGestureRecognizer { CommandParameter = this.Hyperlink, Command = new Command(async Parameter => await App.OpenUrlAsync(Parameter as string ?? string.Empty)) });
			}

			return Task.CompletedTask;
		}

		#endregion

		#region Dispose
		/// <inheritdoc/>
		public void Dispose()
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

			}
			this.isDisposed = true;
		}

		#endregion

		#region Logic Helpers
		/// <summary>
		/// Cheks if any footnotes are referenced in the document.
		/// </summary>
		private bool NeedsToDisplayFootnotes()
		{
			IEnumerable<Footnote> DocumentFootnotes = this.GetFootnotes(this.Document.Footnotes);

			if (DocumentFootnotes is null)
				return false;

			foreach (Footnote Footnote in DocumentFootnotes)
			{
				if (Footnote.Referenced)
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Helper function that gets footnotes from the document.
		/// </summary>
		/// <param name="keys"></param>
		/// <returns></returns>
		private IEnumerable<Footnote> GetFootnotes(string[] keys)
		{
			foreach(string Key in keys)
			{
				Footnote Footnote;
				this.Document.TryGetFootnote(Key, out Footnote);
				yield return Footnote;
			}
		}

		/// <summary>
		/// Checks if the image source is a data uri and if so, converts it to a local file.
		/// </summary>
		/// <param name="Source"></param>
		/// <returns></returns>
		private async Task OutputImage(Waher.Content.Emoji.IImageSource Source)
		{
			Source = await CheckDataUri(Source);

			Image Image = new Image
			{
				Source = Source.Url,
			};

			if (Source.Width.HasValue)
				Image.WidthRequest = Source.Width.Value;

			if (Source.Height.HasValue)
				Image.HeightRequest = Source.Height.Value;

			ScrollView ScrollView = new ScrollView
			{
				Orientation = ScrollOrientation.Horizontal,
				Content = Image
			};

			ContentView Cv = (ContentView)this.currentElement;
			Cv.Content = ScrollView;
		}

		/// <summary>
		/// Helper function to determine label text alignment
		/// </summary>
		/// <returns>
		/// Text alignment
		/// </returns>
		public Microsoft.Maui.TextAlignment LabelAlignment()
		{
			switch (this.Alignment)
			{
				case Waher.Content.Markdown.Model.TextAlignment.Left:
					return Microsoft.Maui.TextAlignment.Start;

				case Waher.Content.Markdown.Model.TextAlignment.Right:
					return Microsoft.Maui.TextAlignment.End;

				case Waher.Content.Markdown.Model.TextAlignment.Center:
					return Microsoft.Maui.TextAlignment.Center;

				default:
					return Microsoft.Maui.TextAlignment.Center;
			}
		}

		/// <summary>
		/// Helper function to generate a new ContentView with the specified alignment, margins, and style.
		/// </summary>
		/// <param name="Alignment"></param>
		/// <param name="Margins"></param>
		/// <param name="BoxStyle"></param>
		internal void RenderContentView(Waher.Content.Markdown.Model.TextAlignment Alignment, Thickness Margins, Style? BoxStyle)
		{
			ContentView ContentView = new ContentView();

			if (!Margins.Equals(Thickness.Zero))
				ContentView.Padding = Margins;

			if (BoxStyle is not null)
				ContentView.Style = BoxStyle;

			switch (Alignment)
			{
				case Waher.Content.Markdown.Model.TextAlignment.Center:
					ContentView.HorizontalOptions = LayoutOptions.Center;
					break;

				case Waher.Content.Markdown.Model.TextAlignment.Left:
					ContentView.HorizontalOptions = LayoutOptions.Start;
					break;

				case Waher.Content.Markdown.Model.TextAlignment.Right:
					ContentView.HorizontalOptions = LayoutOptions.End;
					break;
			}
			
			this.currentElement = ContentView;
		}

		/// <summary>
		/// Helper function to generate a new ContentView with the specified margins.
		/// </summary>
		/// <param name="Margins"></param>
		internal void RenderContentView(Thickness Margins)
		{
			this.RenderContentView(this.Alignment, Margins, null);
		}

		/// <summary>
		/// Renders an object such as images, graphs, exceptions, or other documents.
		/// </summary>
		/// <param name="Result"></param>
		/// <param name="AloneInParagraph"></param>
		/// <param name="Variables"></param>
		/// <returns></returns>
		public async Task RenderObject(object? Result, bool AloneInParagraph, Variables Variables)
		{
			ContentView Bakup = (ContentView)this.currentElement;

			if (Result is null)
				return;

			string? S;

			if (Result is XmlDocument Xml)
				Result = await MarkdownDocument.TransformXml(Xml, Variables);
			else if (Result is IToMatrix ToMatrix)
				Result = ToMatrix.ToMatrix();

			if (this.InLabel)
			{
				S = Result?.ToString();
				if (!string.IsNullOrEmpty(S))
					await this.RenderSpan(Result?.ToString() ?? string.Empty); //TODO

				return;
			}

			if (Result is Graph G)
			{
				PixelInformation Pixels = G.CreatePixels(Variables);
				byte[] Bin = Pixels.EncodeAsPng();

				S = "data:image/png;base64," + Convert.ToBase64String(Bin, 0, Bin.Length);

				await this.OutputMaui(new Waher.Content.Emoji.ImageSource()
				{
					Url = S,
					Width = Pixels.Width,
					Height = Pixels.Height
				});
			}
			else if (Result is SKImage Img)
			{
				using SKData Data = Img.Encode(SKEncodedImageFormat.Png, 100);
				byte[] Bin = Data.ToArray();

				S = "data:image/png;base64," + Convert.ToBase64String(Bin, 0, Bin.Length);

				await this.OutputMaui(new Waher.Content.Emoji.ImageSource()
				{
					Url = S,
					Width = Img.Width,
					Height = Img.Height
				});
			}
			else if (Result is MarkdownDocument Doc)
			{
				//TODO maybe
				await this.RenderDocument(Doc, true);   // Does not call ProcessAsyncTasks()
				Doc.ProcessAsyncTasks();
			}
			else if (Result is MarkdownContent Markdown)
			{
				Doc = await MarkdownDocument.CreateAsync(Markdown.Markdown);
				await this.RenderDocument(Doc, true);   // Does not call ProcessAsyncTasks()
				Doc.ProcessAsyncTasks();
			}
			else if (Result is Exception Ex)
			{
				Ex = Log.UnnestException(Ex);

				if (Ex is AggregateException Ex2)
				{
					VerticalStackLayout Vsl = new();

					foreach (Exception Ex3 in Ex2.InnerExceptions)
					{
						Label Label = new Label
						{
							LineBreakMode = LineBreakMode.WordWrap,
							TextColor = AppColors.Alert,
							Text = Ex3.Message
						};

						Vsl.Add(Label);
					}
					Bakup.Content = Vsl;
				}
				else
				{
					Label Label = new Label
					{
						LineBreakMode = LineBreakMode.WordWrap,
						TextColor = AppColors.Alert,
						Text = Ex.Message
					};

					Bakup.Content = Label;
				}
			}
			else
			{
				Label Label = new Label
				{
					LineBreakMode = LineBreakMode.WordWrap,
					HorizontalTextAlignment = this.LabelAlignment(),
					Text = Result.ToString()
				};

				Bakup.Content = Label;
			}
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
		/// Outputs an image to Maui object
		/// </summary>
		/// <param name="Source">Image source.</param>
		private async Task OutputMaui(Waher.Content.Emoji.IImageSource Source)
		{
			Source = await CheckDataUri(Source);

			Image Image = new Image
			{
				Source = Source.Url,
			};

			ScrollView Sv = new ScrollView
			{
				Orientation = ScrollOrientation.Horizontal,
				Content = Image
			};

			if (Source.Width.HasValue)
				Sv.WidthRequest = Source.Width.Value;

			if (Source.Height.HasValue)
				Sv.HeightRequest = Source.Height.Value;

			ContentView Cv = (ContentView)this.currentElement;

			Cv.Content = Sv;
		}

		/// <summary>
		/// Entry Render function for Multimedia Elements
		/// </summary>
		/// <param name="Element"></param>
		/// <returns></returns>
		private async Task RenderMaui(Waher.Content.Markdown.Model.SpanElements.Multimedia Element)
		{
			ContentView Bakup = (ContentView)this.currentElement;
			VerticalStackLayout MauiStackLayout = new VerticalStackLayout();

			foreach (MultimediaItem Item in Element.Items)
			{
				ContentView ElementContentView = new();
				this.currentElement = ElementContentView;
				await this.OutputMaui(new Waher.Content.Emoji.ImageSource()
				{
					Url = Element.Document.CheckURL(Item.Url, null),
					Width = Item.Width,
					Height = Item.Height,
				});
				MauiStackLayout.Add(ElementContentView);
			}
			Bakup.Content = MauiStackLayout;
			this.currentElement = Bakup;
		}

		/// <summary>
		/// Checks a Data URI image, that it contains a decodable image.
		/// </summary>
		/// <param name="Source">Image source.</param>
		public static async Task<Waher.Content.Emoji.IImageSource> CheckDataUri(Waher.Content.Emoji.IImageSource Source)
		{
			string Url = Source.Url;
			int i;

			if (Url.StartsWith("data:", StringComparison.CurrentCultureIgnoreCase) && (i = Url.IndexOf("base64,")) > 0)
			{
				int? Width = Source.Width;
				int? Height = Source.Height;
				byte[] Data = Convert.FromBase64String(Url.Substring(i + 7));
				using (SKBitmap Bitmap = SKBitmap.Decode(Data))
				{
					Width = Bitmap.Width;
					Height = Bitmap.Height;
				}

				Url = await ImageContent.GetTemporaryFile(Data);

				return new ImageSource()
				{
					Url = Url,
					Width = Width,
					Height = Height
				};
			}
			else
				return Source;
		}
		#endregion

		#region State Management

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

		#endregion
	}
}
