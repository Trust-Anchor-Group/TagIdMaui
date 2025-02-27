using NeuroAccessMaui.UI.Controls.Extended;
using NeuroAccessMaui.UI.Converters;
using Waher.Events;

namespace NeuroAccessMaui.UI.Controls
{
	/// <summary>
	///  Extended DatePicker for nullable values with text placeholder
	/// </summary>
	public class CompositeDatePicker : CompositeInputView
	{
		/// <summary>
		/// The font property
		/// </summary>
		public static readonly BindableProperty FontProperty =
			 BindableProperty.Create(nameof(Font), typeof(Microsoft.Maui.Font), typeof(CompositePicker), new Microsoft.Maui.Font());

		/// <summary>
		/// Gets or sets the Font
		/// </summary>
		public Microsoft.Maui.Font Font
		{
			get { return (Microsoft.Maui.Font)this.GetValue(FontProperty); }
			set { this.SetValue(FontProperty, value); }
		}

		/// <summary>
		/// The XAlign property
		/// </summary>
		public static readonly BindableProperty XAlignProperty =
			 BindableProperty.Create(nameof(XAlign), typeof(TextAlignment), typeof(CompositePicker),
			 TextAlignment.Start);

		/// <summary>
		/// Gets or sets the X alignment of the text
		/// </summary>
		public TextAlignment XAlign
		{
			get { return (TextAlignment)this.GetValue(XAlignProperty); }
			set { this.SetValue(XAlignProperty, value); }
		}

		/// <summary>
		/// The HasBorder property
		/// </summary>
		public static readonly BindableProperty HasBorderProperty =
			 BindableProperty.Create(nameof(HasBorder), typeof(bool), typeof(CompositePicker), true);

		/// <summary>
		/// Gets or sets if the border should be shown or not
		/// </summary>
		public bool HasBorder
		{
			get { return (bool)this.GetValue(HasBorderProperty); }
			set { this.SetValue(HasBorderProperty, value); }
		}

		/// <summary>
		/// The Placeholder property
		/// </summary>
		public static readonly BindableProperty PlaceholderProperty =
			 BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(CompositePicker), string.Empty, BindingMode.OneWay);

		/// <summary>
		/// Get or sets the PlaceHolder
		/// </summary>
		public string Placeholder
		{
			get { return (string)this.GetValue(PlaceholderProperty); }
			set { this.SetValue(PlaceholderProperty, value); }
		}

		/// <summary>
		/// The PlaceholderTextColor property
		/// </summary>
		public static readonly BindableProperty PlaceholderTextColorProperty =
			 BindableProperty.Create(nameof(PlaceholderTextColor), typeof(Color), typeof(CompositePicker), Colors.Transparent);

		/// <summary>
		/// Sets color for placeholder text
		/// </summary>
		public Color PlaceholderTextColor
		{
			get { return (Color)this.GetValue(PlaceholderTextColorProperty); }
			set { this.SetValue(PlaceholderTextColorProperty, value); }
		}

		/// <summary>
		/// The TextColor property
		/// </summary>
		public static readonly BindableProperty TextColorProperty =
			BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(CompositePicker), Colors.Black);

		/// <summary>
		/// Gets or sets the TextColor
		/// </summary>
		public Color TextColor
		{
			get => (Color)this.GetValue(TextColorProperty);
			set => this.SetValue(TextColorProperty, value);
		}

		/// <summary>
		/// Bindable property for the style applied to the picker
		/// </summary>
		public static readonly BindableProperty PickerStyleProperty =
			BindableProperty.Create(nameof(PickerStyle), typeof(Style), typeof(CompositePicker), null);

		public Style PickerStyle
		{
			get => (Style)this.GetValue(PickerStyleProperty);
			set => this.SetValue(PickerStyleProperty, value);
		}

		/// <summary>
		/// The NullableDate property
		/// </summary>
		public static readonly BindableProperty NullableDateProperty =
			 BindableProperty.Create(nameof(NullableDate), typeof(DateTime?), typeof(CompositeDatePicker), null, BindingMode.TwoWay,
			 propertyChanged: DatePropertyChanged);

		/// <summary>
		/// The MinimumDate property
		/// </summary>
		public static readonly BindableProperty MinimumDateProperty =
			BindableProperty.Create(nameof(MinimumDate), typeof(DateTime), typeof(CompositeDatePicker), DateTime.MinValue);

		/// <summary>
		/// The MaximumDate property
		/// </summary>
		public static readonly BindableProperty MaximumDateProperty =
			BindableProperty.Create(nameof(MaximumDate), typeof(DateTime), typeof(CompositeDatePicker), DateTime.MaxValue);

		/// <summary>
		/// The Date property
		/// </summary>
		public static readonly BindableProperty DateProperty =
			BindableProperty.Create(nameof(Date), typeof(DateTime), typeof(CompositeDatePicker), DateTime.Now, propertyChanged: DatePropertyChanged);

		/// <summary>
		/// Gets or sets the MinimumDate
		/// </summary>
		public DateTime MinimumDate
		{
			get => (DateTime)this.GetValue(MinimumDateProperty);
			set => this.SetValue(MinimumDateProperty, value);
		}

		/// <summary>
		/// Gets or sets the MaximumDate
		/// </summary>
		public DateTime MaximumDate
		{
			get => (DateTime)this.GetValue(MaximumDateProperty);
			set => this.SetValue(MaximumDateProperty, value);
		}

		/// <summary>
		/// Gets or sets the Date
		/// </summary>
		public DateTime Date
		{
			get => (DateTime)this.GetValue(DateProperty);
			set => this.SetValue(DateProperty, value);
		}

		/// <summary>
		/// Get or sets the NullableDate
		/// </summary>
		public DateTime? NullableDate
		{
			get { return (DateTime?)this.GetValue(NullableDateProperty); }
			set
			{
				if (value != this.NullableDate)
				{
					this.SetValue(NullableDateProperty, value);
					this.UpdateDate();
				}
			}
		}

		/// <summary>
		/// Event sent when the date is changed.
		/// </summary>
		public event EventHandler<NullableDateChangedEventArgs>? NullableDateSelected;

		private readonly DatePicker picker;

		/// <summary>
		/// Creates a new instance of the <see cref="CompositeDatePicker"/> class.
		/// </summary>
		public CompositeDatePicker()
		{
			this.picker = new DatePicker();

			this.SetDefaultDate();

			this.picker.SetBinding(DatePicker.DateProperty, new Binding(nameof(this.NullableDate), BindingMode.TwoWay, new NullableDateTimeConverter(), source: this));
			this.picker.SetBinding(DatePicker.MinimumDateProperty, new Binding(nameof(this.MinimumDate), source: this));
			this.picker.SetBinding(DatePicker.MaximumDateProperty, new Binding(nameof(this.MaximumDate), source: this));
			this.picker.SetBinding(DatePicker.TextColorProperty, new Binding(nameof(this.TextColor), source: this));
			this.picker.SetBinding(DatePicker.StyleProperty, new Binding(nameof(this.PickerStyle), source: this));
			this.CenterView = this.picker;
		}

		static void DatePropertyChanged(BindableObject bindable, object oldValue, object newValue)
		{
			CompositeDatePicker Picker = (CompositeDatePicker)bindable;
			EventHandler<NullableDateChangedEventArgs>? selected = Picker.NullableDateSelected;

			selected.Raise(Picker, new NullableDateChangedEventArgs((DateTime?)oldValue, (DateTime?)newValue));
		}

		private bool isDefaultDateSet = false;

		/// <inheritdoc/>
		protected override void OnPropertyChanged(string? propertyName = null)
		{
			base.OnPropertyChanged(propertyName);

			if (propertyName == IsFocusedProperty.PropertyName)
				// we don't know if the date picker was closed by the Ok or Cancel button,
				// so we presuppose it was an Ok.
				if (!this.IsFocused)
					this.OnPropertyChanged(nameof(this.picker.DateProperty));

			if (propertyName == nameof(this.picker.DateProperty) && !this.isDefaultDateSet)
				this.NullableDate = this.picker.Date;

			if (propertyName == NullableDateProperty.PropertyName)
				if (this.NullableDate.HasValue)
					this.picker.Date = this.NullableDate.Value;
		}

		private void UpdateDate()
		{
			if (this.NullableDate.HasValue)
				this.picker.Date = this.NullableDate.Value;
			else
			{
				this.isDefaultDateSet = true;
				this.SetDefaultDate();
				this.isDefaultDateSet = false;
			}
		}

		private void SetDefaultDate()
		{
			DateTime now = DateTime.Now;
			this.picker.Date = new DateTime(now.Year, now.Month, now.Day);
		}
	}
}
