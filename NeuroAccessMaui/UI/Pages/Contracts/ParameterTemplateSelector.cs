

namespace NeuroAccessMaui.UI.Pages.Contracts.ObjectModel
{

	/// <summary>
	/// Selects a template based on the type of the parameter.
	/// </summary>
	public class ParameterTemplateSelector : DataTemplateSelector
	{
		public DataTemplate? BooleanTemplate { get; set; }
		public DataTemplate? DateTemplate { get; set; }
		public DataTemplate? DateTimeTemplate { get; set; }
		public DataTemplate? DurationTemplate { get; set; }
		public DataTemplate? StringTemplate { get; set; }
		public DataTemplate? NumericalTemplate { get; set; }
		public DataTemplate? TimeTemplate { get; set; }
		public DataTemplate? CalcTemplate { get; set; }
		public DataTemplate? ContractReferenceTemplate { get; set; }
		public DataTemplate? ProtectedTemplate { get; set; }
		public DataTemplate? DefaultTemplate { get; set; }

		// Add other templates if needed

		protected override DataTemplate? OnSelectTemplate(object item, BindableObject container)
		{
			ObservableParameter? parameter = item as ObservableParameter;
			if (parameter is not null && this.ProtectedTemplate is not null)
			{
				if (parameter.IsProtected && !parameter.CanReadValue)
				{
					return this.ProtectedTemplate ?? this.DefaultTemplate;
				}
			}

			return item switch
			{
				ObservableBooleanParameter => this.BooleanTemplate ?? this.DefaultTemplate,
				ObservableDateParameter => this.DateTemplate ?? this.DefaultTemplate,
				ObservableDateTimeParameter => this.DateTimeTemplate ?? this.DefaultTemplate,
				ObservableDurationParameter => this.DurationTemplate ?? this.DefaultTemplate,
				ObservableStringParameter => this.StringTemplate ?? this.DefaultTemplate,
				ObservableNumericalParameter => this.NumericalTemplate ?? this.DefaultTemplate,
				ObservableTimeParameter => this.TimeTemplate ?? this.DefaultTemplate,
				ObservableCalcParameter => this.CalcTemplate ?? this.DefaultTemplate,
				ObservableContractReferenceParameter => this.ContractReferenceTemplate ?? this.DefaultTemplate,
				_ => this.DefaultTemplate
			};
		}
	}
}
