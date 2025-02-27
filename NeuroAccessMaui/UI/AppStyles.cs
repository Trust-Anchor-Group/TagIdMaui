using System.Text;
using NeuroAccessMaui.Services;
using Waher.Events;

namespace NeuroAccessMaui.UI
{
	/// <summary>
	/// Static class that gives access to app-specific styles
	/// </summary>
	public static class AppStyles
	{
		private static readonly SortedDictionary<string, bool> missingStyles = [];
		private static Timer? timer = null;

		private static double? smallSpacing;
		private static ControlTemplate? radioButtonTemplate;
		private static Thickness? smallBottomMargins;
		private static Thickness? smallTopMargins;
		private static Thickness? smallLeftMargins;
		private static Thickness? smallRightMargins;
		private static Style? sectionTitleLabelStyle;
		private static Style? keyLabel;
		private static Style? valueLabel;
		private static Style? formattedValueLabel;
		private static Style? clickableValueLabel;
		private static Style? infoLabelStyle;
		private static Style? filledTextButton;
		private static Style? frameSet;
		private static Style? frameSubSet;
		private static Style? clickableFrameSubSet;
		private static Style? regularCompositeEntry;
		private static Style? regularCompositeEntryBorder;
		private static Style? regularCompositeDatePicker;
		private static Style? unicodeCharacterButton;
		private static Style? imageOnlyButton;
		private static Style? transparentImageButton;
		private static Style? sendFrame;
		private static Style? receiveFrame;
		private static Style? requiredFieldMarker;
		private static Style? requiredFieldMarkerSpan;
		private static Style? roundedBorder;


		static AppStyles()
		{
			Log.Terminating += Log_Terminating;
		}

		private static Task Log_Terminating(object? sender, EventArgs e)
		{
			timer?.Dispose();
			timer = null;

			return Task.CompletedTask;
		}

		/// <summary>
		/// Tries to get an embedded resource.
		/// </summary>
		/// <typeparam name="T">Type of resource.</typeparam>
		/// <param name="Name">Name of resource.</param>
		/// <returns>Typed resource, if found.</returns>
		public static T? TryGetResource<T>(string Name)
		{
			if ((Application.Current?.Resources.TryGetValue(Name, out object Value) ?? false) && Value is T TypedValue)
				return TypedValue;
			else
			{
				lock (missingStyles)
				{
					timer?.Dispose();
					timer = null;

					missingStyles[Name] = true;

					timer = new Timer(LogAlert, null, 1000, Timeout.Infinite);
				}

				return default;
			}
		}

		private static void LogAlert(object? _)
		{
			StringBuilder sb = new();

			sb.AppendLine("Missing styles:");
			sb.AppendLine();

			lock (missingStyles)
			{
				foreach (string Key in missingStyles.Keys)
				{
					sb.Append("* ");
					sb.AppendLine(Key);
				}

				missingStyles.Clear();
			}

			ServiceRef.LogService.LogAlert(sb.ToString());
		}

		public static double SmallSpacing
		{
			get
			{
				smallSpacing ??= TryGetResource<double?>("SmallSpacing");
				return smallSpacing ?? 0;
			}
		}

		/// <summary>
		/// Template for radio buttons
		/// </summary>
		public static ControlTemplate RadioButtonTemplate
		{
			get
			{
				radioButtonTemplate ??= TryGetResource<ControlTemplate>("RadioButtonTemplate");
				return radioButtonTemplate!;
			}
		}

		/// <summary>
		/// Bottom-only small margins
		/// </summary>
		public static Thickness SmallBottomMargins
		{
			get
			{
				smallBottomMargins ??= TryGetResource<Thickness>("SmallBottomMargins");
				return smallBottomMargins.Value;
			}
		}

		/// <summary>
		/// Top-only small margins
		/// </summary>
		public static Thickness SmallTopMargins
		{
			get
			{
				smallTopMargins ??= TryGetResource<Thickness>("SmallTopMargins");
				return smallTopMargins.Value;
			}
		}

		/// <summary>
		/// Left-only small margins
		/// </summary>
		public static Thickness SmallLeftMargins
		{
			get
			{
				smallLeftMargins ??= TryGetResource<Thickness>("SmallLeftMargins");
				return smallLeftMargins.Value;
			}
		}

		/// <summary>
		/// Right-only small margins
		/// </summary>
		public static Thickness SmallRightMargins
		{
			get
			{
				smallRightMargins ??= TryGetResource<Thickness>("SmallRightMargins");
				return smallRightMargins.Value;
			}
		}

		/// <summary>
		/// Style of section title labels
		/// </summary>
		public static Style SectionTitleLabel
		{
			get
			{
				sectionTitleLabelStyle ??= TryGetResource<Style>("SectionTitleLabel");
				return sectionTitleLabelStyle!;
			}
		}

		/// <summary>
		/// Style of key labels
		/// </summary>
		public static Style KeyLabel
		{
			get
			{
				keyLabel ??= TryGetResource<Style>("KeyLabel");
				return keyLabel!;
			}
		}

		/// <summary>
		/// Style of value labels
		/// </summary>
		public static Style ValueLabel
		{
			get
			{
				valueLabel ??= TryGetResource<Style>("ValueLabel");
				return valueLabel!;
			}
		}

		/// <summary>
		/// Style of formatted value labels
		/// </summary>
		public static Style FormattedValueLabel
		{
			get
			{
				formattedValueLabel ??= TryGetResource<Style>("FormattedValueLabel");
				return formattedValueLabel!;
			}
		}

		/// <summary>
		/// Style of clickable value labels
		/// </summary>
		public static Style ClickableValueLabel
		{
			get
			{
				clickableValueLabel ??= TryGetResource<Style>("ClickableValueLabel");
				return clickableValueLabel!;
			}
		}

		/// <summary>
		/// Style of information labels
		/// </summary>
		public static Style InfoLabel
		{
			get
			{
				infoLabelStyle ??= TryGetResource<Style>("InfoLabel");
				return infoLabelStyle!;
			}
		}

		/// <summary>
		/// Style for filled text buttons.
		/// </summary>
		public static Style FilledTextButton
		{
			get
			{
				filledTextButton ??= TryGetResource<Style>("FilledTextButton");   // TODO: Remove NoRoundedCorners
				return filledTextButton!;
			}
		}

		/// <summary>
		/// Style for frame sets.
		/// </summary>
		public static Style FrameSet
		{
			get
			{
				frameSet ??= TryGetResource<Style>("FrameSet");
				return frameSet!;
			}
		}

		/// <summary>
		/// Style for frame subsets.
		/// </summary>
		public static Style FrameSubSet
		{
			get
			{
				frameSubSet ??= TryGetResource<Style>("FrameSubSet");
				return frameSubSet!;
			}
		}

		/// <summary>
		/// Style for clickable frame subsets.
		/// </summary>
		public static Style ClickableFrameSubSet
		{
			get
			{
				clickableFrameSubSet ??= TryGetResource<Style>("ClickableFrameSubSet");
				return clickableFrameSubSet!;
			}
		}

		/// <summary>
		/// Style for borders in a regular composte entry control.
		/// </summary>
		public static Style RegularCompositeEntry
		{
			get
			{
				regularCompositeEntry ??= TryGetResource<Style>("RegularCompositeEntry"); // TODO: Remove NoRoundedCorners
				return regularCompositeEntry!;
			}
		}

		/// <summary>
		/// Style for borders in a regular composte entry control.
		/// </summary>
		public static Style RegularCompositeEntryBorder
		{
			get
			{
				regularCompositeEntryBorder ??= TryGetResource<Style>("RegularCompositeEntryBorder");  // TODO: Remove NoRoundedCorners
				return regularCompositeEntryBorder!;
			}
		}

		/// <summary>
		/// Style for CompositeDatePicker controls.
		/// </summary>
		public static Style RegularCompositeDatePicker
		{
			get
			{
				regularCompositeDatePicker ??= TryGetResource<Style>("RegularCompositeDatePicker");
				return regularCompositeDatePicker!;
			}
		}

		/// <summary>
		/// Style for buttons containing a single Unicode character.
		/// </summary>
		public static Style UnicodeCharacterButton
		{
			get
			{
				unicodeCharacterButton ??= TryGetResource<Style>("UnicodeCharacterButtonNoRoundedCorners");  // TODO: Remove NoRoundedCorners
				return unicodeCharacterButton!;
			}
		}

		/// <summary>
		/// Style for buttons containing only an image.
		/// </summary>
		public static Style ImageOnlyButton
		{
			get
			{
				imageOnlyButton ??= TryGetResource<Style>("ImageOnlyButton");
				return imageOnlyButton!;
			}
		}

		/// <summary>
		/// Style for transparent image buttons.
		/// </summary>
		public static Style TransparentImageButton
		{
			get
			{
				transparentImageButton ??= TryGetResource<Style>("TransparentImageButton");
				return transparentImageButton!;
			}
		}

		/// <summary>
		/// Style for send frames
		/// </summary>
		public static Style SendFrame
		{
			get
			{
				sendFrame ??= TryGetResource<Style>("SendFrame");
				return sendFrame!;
			}
		}

		/// <summary>
		/// Style for receive frames
		/// </summary>
		public static Style ReceiveFrame
		{
			get
			{
				receiveFrame ??= TryGetResource<Style>("ReceiveFrame");
				return receiveFrame!;
			}
		}

		/// <summary>
		/// Style for required field marker labels
		/// </summary>
		public static Style RequiredFieldMarker
		{
			get
			{
				requiredFieldMarker ??= TryGetResource<Style>("RequiredFieldMarker");
				return requiredFieldMarker!;
			}
		}

		/// <summary>
		/// Style for required field marker spans
		/// </summary>
		public static Style RequiredFieldMarkerSpan
		{
			get
			{
				requiredFieldMarkerSpan ??= TryGetResource<Style>("RequiredFieldMarkerSpan");
				return requiredFieldMarkerSpan!;
			}
		}

		public static Style RoundedBorder
		{
			get
			{
				roundedBorder ??= TryGetResource<Style>("RoundedBorder");
				return roundedBorder!;
			}
		}
	}
}
