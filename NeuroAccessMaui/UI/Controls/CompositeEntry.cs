using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Waher.Events;
using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace NeuroAccessMaui.UI.Controls
{
	/// <summary>
	/// A customizable entry control that allows setting views on the left and right sides of the entry.
	/// </summary>
	public class CompositeEntry : CompositeInputView
	{
		#region Bindable Properties

		/// <summary>
		/// Bindable property for the text displayed in the entry.
		/// </summary>
		public static readonly BindableProperty EntryDataProperty = BindableProperty.Create(
			 nameof(EntryData),
			 typeof(string),
			 typeof(CompositeEntry),
			 default(string),
			 BindingMode.TwoWay);

		/// <summary>
		/// Gets or sets the text displayed in the entry.
		/// </summary>
		public string EntryData
		{
			get => (string)this.GetValue(EntryDataProperty);
			set => this.SetValue(EntryDataProperty, value);
		}

		/// <summary>
		/// Bindable property for the placeholder text displayed when the entry is empty.
		/// </summary>
		public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(
			 nameof(Placeholder),
			 typeof(string),
			 typeof(CompositeEntry),
			 default(string));

		/// <summary>
		/// Gets or sets the placeholder text displayed when the entry is empty.
		/// </summary>
		public string Placeholder
		{
			get => (string)this.GetValue(PlaceholderProperty);
			set => this.SetValue(PlaceholderProperty, value);
		}

		/// <summary>
		/// Bindable property indicating whether the entry is used for password input.
		/// </summary>
		public static readonly BindableProperty IsPasswordProperty = BindableProperty.Create(
			 nameof(IsPassword),
			 typeof(bool),
			 typeof(CompositeEntry),
			 false);

		/// <summary>
		/// Gets or sets a value indicating whether the entry is used for password input.
		/// </summary>
		public bool IsPassword
		{
			get => (bool)this.GetValue(IsPasswordProperty);
			set => this.SetValue(IsPasswordProperty, value);
		}

		/// <summary>
		/// Bindable property for the style applied to the entry.
		/// </summary>
		public static readonly BindableProperty EntryStyleProperty = BindableProperty.Create(
			 nameof(EntryStyle),
			 typeof(Style),
			 typeof(CompositeEntry));

		/// <summary>
		/// Gets or sets the style applied to the entry.
		/// </summary>
		public Style EntryStyle
		{
			get => (Style)this.GetValue(EntryStyleProperty);
			set => this.SetValue(EntryStyleProperty, value);
		}

		/// <summary>
		/// Bindable property for the keyboard type used by the entry.
		/// </summary>
		public static readonly BindableProperty KeyboardProperty = BindableProperty.Create(
			 nameof(Keyboard),
			 typeof(Keyboard),
			 typeof(CompositeEntry),
			 Keyboard.Default);

		/// <summary>
		/// Gets or sets the keyboard type used by the entry.
		/// </summary>
		public Keyboard Keyboard
		{
			get => (Keyboard)this.GetValue(KeyboardProperty);
			set => this.SetValue(KeyboardProperty, value);
		}

		/// <summary>
		/// Bindable property for the text color of the entry.
		/// </summary>
		public static readonly BindableProperty TextColorProperty = BindableProperty.Create(
			 nameof(TextColor),
			 typeof(Color),
			 typeof(CompositeEntry),
			 Colors.Black);

		/// <summary>
		/// Gets or sets the text color of the entry.
		/// </summary>
		public Color TextColor
		{
			get => (Color)this.GetValue(TextColorProperty);
			set => this.SetValue(TextColorProperty, value);
		}




		/// <summary>
		/// Bindable property indicating whether spell check is enabled.
		/// </summary>
		public static readonly BindableProperty IsSpellCheckEnabledProperty = BindableProperty.Create(
			 nameof(IsSpellCheckEnabled),
			 typeof(bool),
			 typeof(CompositeEntry),
			 true);

		/// <summary>
		/// Gets or sets a value indicating whether spell check is enabled.
		/// </summary>
		public bool IsSpellCheckEnabled
		{
			get => (bool)this.GetValue(IsSpellCheckEnabledProperty);
			set => this.SetValue(IsSpellCheckEnabledProperty, value);
		}

		/// <summary>
		/// Bindable property indicating whether text prediction is enabled.
		/// </summary>
		public static readonly BindableProperty IsTextPredictionEnabledProperty = BindableProperty.Create(
			 nameof(IsTextPredictionEnabled),
			 typeof(bool),
			 typeof(CompositeEntry),
			 true);

		/// <summary>
		/// Gets or sets a value indicating whether text prediction is enabled.
		/// </summary>
		public bool IsTextPredictionEnabled
		{
			get => (bool)this.GetValue(IsTextPredictionEnabledProperty);
			set => this.SetValue(IsTextPredictionEnabledProperty, value);
		}

		/// <summary>
		/// Bindable property indicating whether Read Only is enabled.
		/// </summary>
		public static readonly BindableProperty IsReadOnlyProperty = BindableProperty.Create(
			 nameof(IsReadOnly),
			 typeof(bool),
			 typeof(CompositeEntry),
			 false);

		/// <summary>
		/// Gets or sets a value indicating whether text prediction is enabled.
		/// </summary>
		public bool IsReadOnly
		{
			get => (bool)this.GetValue(IsReadOnlyProperty);
			set => this.SetValue(IsReadOnlyProperty, value);
		}

		// Define the new BackgroundColor property
		public static new readonly BindableProperty BackgroundColorProperty = BindableProperty.Create(
			 nameof(BackgroundColor),
			 typeof(Color),
			 typeof(CompositeEntry),
			 Colors.Transparent);

		public new Color BackgroundColor
		{
			get => (Color)this.GetValue(BackgroundColorProperty);
			set => this.SetValue(BackgroundColorProperty, value);
		}

		#endregion

		#region Fields

		private readonly Entry innerEntry;

		#endregion

		#region Events

		/// <summary>
		/// Occurs when the user finalizes the text in an entry with the return key.
		/// </summary>
		public event EventHandler? Completed;

		/// <summary>
		/// Occurs when the text of the entry changes.
		/// </summary>
		public event EventHandler<TextChangedEventArgs>? TextChanged;

		/// <summary>
		/// Occurs when the entry gains focus.
		/// </summary>
		public new event EventHandler<FocusEventArgs>? Focused;

		/// <summary>
		/// Occurs when the entry loses focus.
		/// </summary>
		public new event EventHandler<FocusEventArgs>? Unfocused;

		#endregion

		#region Constructor

		/// <summary>
		/// Initializes a new instance of the <see cref="CompositeEntry"/> class.
		/// </summary>
		public CompositeEntry()
		{
			// Initialize the inner Entry
			this.innerEntry = new Entry
			{
				VerticalOptions = LayoutOptions.Center,
				HorizontalOptions = LayoutOptions.Fill,
				BackgroundColor = Colors.Transparent
			};

			// Set up bindings for the Entry
			this.innerEntry.SetBinding(Entry.TextProperty, new Binding(nameof(this.EntryData), source: this));
			this.innerEntry.SetBinding(Entry.PlaceholderProperty, new Binding(nameof(this.Placeholder), source: this));
			this.innerEntry.SetBinding(Entry.IsPasswordProperty, new Binding(nameof(this.IsPassword), source: this));
			this.innerEntry.SetBinding(Entry.KeyboardProperty, new Binding(nameof(this.Keyboard), source: this));
			this.innerEntry.SetBinding(Entry.StyleProperty, new Binding(nameof(this.EntryStyle), source: this));
			this.innerEntry.SetBinding(Entry.TextColorProperty, new Binding(nameof(this.TextColor), source: this));
			this.innerEntry.SetBinding(InputView.IsSpellCheckEnabledProperty, new Binding(nameof(this.IsSpellCheckEnabled), source: this));
			this.innerEntry.SetBinding(InputView.IsTextPredictionEnabledProperty, new Binding(nameof(this.IsTextPredictionEnabled), source: this));
			this.innerEntry.SetBinding(Entry.IsReadOnlyProperty, new Binding(nameof(this.IsReadOnly), source: this));

			// Handle events
			this.innerEntry.Completed += this.OnEntryCompleted;
			this.innerEntry.TextChanged += this.OnEntryTextChanged;
			this.innerEntry.Focused += this.OnEntryFocused;
			this.innerEntry.Unfocused += this.OnEntryUnfocused;

			// Initialize the top label
			this.CenterView = this.innerEntry;
		}

		#endregion

		#region Property Overrides

		/// <summary>
		/// Gets or sets a user-defined value to uniquely identify the element.
		/// This is forwarded to the inner Entry control.
		/// </summary>
		public new string StyleId
		{
			get => this.innerEntry.StyleId;
			set => this.innerEntry.StyleId = value;
		}

		public new string AutomationId
		{
			get => this.innerEntry.AutomationId;
			set => this.innerEntry.AutomationId = value;
		}

		#endregion

		#region Methods

		/// <summary>
		/// Focuses the Entry.
		/// </summary>
		public new bool Focus()
		{
			return this.innerEntry.Focus();
		}

		/// <summary>
		/// Unfocuses the Entry.
		/// </summary>
		public new void Unfocus()
		{
			this.innerEntry.Unfocus();
		}

		#endregion

		#region Property Changed Callbacks

		#endregion

		#region Event Handlers

		private void OnEntryTextChanged(object? sender, TextChangedEventArgs e)
		{
			TextChanged.Raise(this, e);
		}

		/// <summary>
		/// Handles the <see cref="Entry.Completed"/> event.
		/// Executes the <see cref="Completed"/> event.
		/// </summary>
		private void OnEntryCompleted(object? sender, EventArgs e)
		{
			Completed.Raise(this, e);
		}

		/// <summary>
		/// Handles the <see cref="VisualElement.Focused"/> event.
		/// Executes the <see cref="Focused"/> if it can execute.
		/// </summary>
		private void OnEntryFocused(object? sender, FocusEventArgs e)
		{
			Focused.Raise(this, e);

		}

		/// <summary>
		/// Handles the <see cref="VisualElement.Unfocused"/> event.
		/// Executes the <see cref="Unfocused"/> if it can execute.
		/// </summary>
		private void OnEntryUnfocused(object? sender, FocusEventArgs e)
		{
			Unfocused.Raise(this, e);
		}

		#endregion
	}
}
