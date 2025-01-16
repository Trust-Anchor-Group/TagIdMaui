using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NeuroAccessMaui.Extensions;
using NeuroAccessMaui.Services;
using Waher.Content;
using Waher.Networking.XMPP.Contracts;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.Input;
using NeuroAccessMaui.UI.Pages.Contracts.MyContracts;
using System.Xml;

namespace NeuroAccessMaui.UI.Pages.Contracts.ObjectModel
{
	/// <summary>
	/// An observable object that wraps a <see cref="Waher.Networking.XMPP.Contracts.Parameter"/> object.
	/// This allows for easier binding in the UI. Must be instantiated with <see cref="CreateAsync"/>.
	/// </summary>
	public class ObservableParameter : ObservableObject
	{
		#region Constructor
		protected ObservableParameter(Parameter parameter)
		{
			this.Parameter = parameter;
		}
		#endregion

		#region Initialization
		/// <summary>
		/// Initializes the parameter in regards to a contract.
		/// </summary>
		private async Task InitializeAsync(Contract contract)
		{
			try
			{
				string UntrimmedDescription = await contract.ToPlainText(this.Parameter.Descriptions, contract.DeviceLanguage());
				this.Description = UntrimmedDescription.Trim();
			}
			catch (Exception e)
			{
				ServiceRef.LogService.LogException(e);
			}
		}

		/// <summary>
		///  Creates a new instance of <see cref="ObservableParameter"/> based on the type of the parameter.
		/// </summary>
		/// <param name="parameter">The parameter object to wrap</param>
		/// <param name="contract">The contract of which the parameter is part of</param>
		/// <returns></returns>
		public static async Task<ObservableParameter> CreateAsync(Parameter parameter, Contract contract)
		{
			ObservableParameter parameterInfo = parameter switch
			{
				BooleanParameter booleanParameter => new ObservableBooleanParameter(booleanParameter),
				DateParameter dateParameter => new ObservableDateParameter(dateParameter),
				DateTimeParameter dateTimeParameter => new ObservableDateTimeParameter(dateTimeParameter),
				NumericalParameter numericalParameter => new ObservableNumericalParameter(numericalParameter),
				StringParameter stringParameter => new ObservableStringParameter(stringParameter),
				TimeParameter timeParameter => new ObservableTimeParameter(timeParameter),
				DurationParameter durationParameter => new ObservableDurationParameter(durationParameter),
				RoleParameter roleParameter => new ObservableRoleParameter(roleParameter),
				CalcParameter calcParameter => new ObservableCalcParameter(calcParameter),
				ContractReferenceParameter contractReferenceParameter => new ObservableContractReferenceParameter(contractReferenceParameter),
				_ => new ObservableParameter(parameter)
			};

			await parameterInfo.InitializeAsync(contract);
			return parameterInfo;
		}
		#endregion

		#region Properties
		/// <summary>
		/// The wrapped parameter object
		/// </summary>
		public Parameter Parameter { get; private set; }

		/// <summary>
		/// The name of the parameter
		/// </summary>
		public string Name => this.Parameter.Name;

		/// <summary>
		/// The Guide of the parameter
		/// </summary>
		public string Guide => string.IsNullOrEmpty(this.Parameter.Guide) ? this.Name : this.Parameter.Guide;

		/// <summary>
		/// Label that determines the displayed text for the parameter.
		/// </summary>
		public string Label
		{
			get
			{
				if (string.IsNullOrEmpty(this.Description))
				{
					if (string.IsNullOrEmpty(this.Guide))
					{
						return this.Name;
					}
					return this.Guide;
				}
				return this.Description;
			}
		}

		/// <summary>
		/// The localized description of the parameter
		/// </summary>
		public string Description
		{
			get => this.description;
			private set
			{
				if (this.SetProperty(ref this.description, value))
					this.OnPropertyChanged(nameof(this.Label));
			}
		}
		private string description = string.Empty;

		/// <summary>
		/// If the parameter is avlid
		/// </summary>
		public bool IsValid
		{
			get => this.isValid;
			set => this.SetProperty(ref this.isValid, value);
		}
		private bool isValid = true;

		/// <summary>
		/// Validation error text
		/// </summary>
		public string ValidationText
		{
			get => this.validationText;
			set => this.SetProperty(ref this.validationText, value);
		}
		private string validationText = string.Empty;

		/// <summary>
		/// The value of the parameter
		/// </summary>
		private object? value;
		public object? Value
		{
			get => this.value;
			set
			{
				try
				{
					if (value is not null)
						this.Parameter.SetValue(value);
				}
				catch (Exception e)
				{
					ServiceRef.LogService.LogException(e);
				}

				this.SetProperty(ref this.@value, value);
				this.OnPropertyChanged(nameof(this.CanReadValue));
			}
		}

		/// <summary>
		/// If the parameter is transient
		/// </summary>
		public bool IsTransient => this.Parameter.Protection == ProtectionLevel.Transient;

		/// <summary>
		/// If the parameter is encrypted
		/// </summary>
		public bool IsEncrypted => this.Parameter.Protection == ProtectionLevel.Encrypted;

		/// <summary>
		/// If the parameter is protected, either encrypted or transient
		/// </summary>
		public bool IsProtected => this.IsTransient || this.IsEncrypted;

		/// <summary>
		/// If the parameter can be read, for example encrypted parameters cannot be read if you don't have the key.
		/// </summary>
		/// 
		public bool CanReadValue => this.IsProtected ? this.Parameter.ObjectValue is not null : this.Parameter.CanSerializeValue;
		#endregion

		#region Property Change Handling
		protected override void OnPropertyChanged(PropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);
		}
		#endregion
	}

	#region ObservableParameter Subclasses
	public sealed class ObservableBooleanParameter : ObservableParameter
	{
		public ObservableBooleanParameter(BooleanParameter parameter) : base(parameter)
		{
			this.Value = parameter.ObjectValue is true;
		}

		public bool BooleanValue
		{
			get => this.Value as bool? ?? false;
			set => this.Value = value;
		}
	}

	public class ObservableDateParameter : ObservableParameter
	{
		public ObservableDateParameter(DateParameter parameter) : base(parameter)
		{
			this.Value = parameter.ObjectValue as DateTime?;
		}

		public DateTime? DateValue
		{
			get => this.Value as DateTime?;
			set => this.Value = value;
		}
	}

	public class ObservableNumericalParameter : ObservableParameter
	{
		public ObservableNumericalParameter(NumericalParameter parameter) : base(parameter)
		{
			this.Value = parameter.ObjectValue is decimal decimalValue ? decimalValue : null;
		}

		public decimal? DecimalValue
		{
			get => this.Value as decimal?;
			set => this.Value = value;
		}
	}

	public class ObservableStringParameter : ObservableParameter
	{
		public ObservableStringParameter(StringParameter parameter) : base(parameter)
		{
			this.Value = parameter.ObjectValue as string ?? string.Empty;
		}

		public string StringValue
		{
			get => this.Value as string ?? string.Empty;
			set => this.Value = value;
		}
	}

	public class ObservableTimeParameter : ObservableParameter
	{
		public ObservableTimeParameter(TimeParameter parameter) : base(parameter)
		{
			this.Value = parameter.ObjectValue is TimeSpan timeSpan ? timeSpan : TimeSpan.Zero;
		}

		public TimeSpan TimeSpanValue
		{
			get => this.Value as TimeSpan? ?? TimeSpan.Zero;
			set => this.Value = value;
		}
	}

	public class ObservableDurationParameter : ObservableParameter
	{
		public ObservableDurationParameter(DurationParameter parameter) : base(parameter)
		{
			this.Value = parameter.ObjectValue is Duration duration ? duration : Duration.Zero;
		}

		public string StringValue
		{
			get => this.Value?.ToString() ?? string.Empty;
			set
			{
				if (Duration.TryParse(value, out Duration duration))
					this.Value = duration;
				else
					this.Value = null;
			}
		}

		public Duration DurationValue
		{
			get => this.Value as Duration? ?? Duration.Zero;
			set
			{
				if (Duration.TryParse(value.ToString(), out Duration duration))
					this.Value = duration;
				else
					this.Value = null;

			}
		}
	}

	public class ObservableRoleParameter : ObservableParameter
	{
		public ObservableRoleParameter(RoleParameter parameter) : base(parameter)
		{
			this.Value = parameter.ObjectValue as string ?? string.Empty;
		}

		public string RoleValue
		{
			get => this.Value as string ?? string.Empty;
			set => this.Value = value;
		}
	}

	public class ObservableCalcParameter : ObservableParameter
	{
		public ObservableCalcParameter(CalcParameter parameter) : base(parameter)
		{
			this.Value = parameter.ObjectValue;
		}

		public object? CalcValue
		{
			get => this.Value;
			set => this.Value = value;
		}

		public string CalcString
		{
			get
			{
				if (this.Value is null)
					this.Value = this.Parameter.StringValue;

				CultureInfo culture = CultureInfo.CurrentCulture;

				Console.WriteLine(this.Value.GetType().Name);

				return this.Value switch
				{
					decimal decimalValue => decimalValue.ToString("N", culture), // Number format with localization
					DateTime dateTimeValue => dateTimeValue.ToString("G", culture), // Localized date format
					TimeSpan timeSpanValue => timeSpanValue.ToString(@"hh\:mm\:ss", culture), // Time format
					string stringValue => string.IsNullOrEmpty(stringValue) ? "-" : stringValue,
					_ => this.Value.ToString() ?? string.Empty // Fallback for unknown types
				};
			}
		}
	}

	public class ObservableDateTimeParameter : ObservableParameter
	{
		public ObservableDateTimeParameter(DateTimeParameter parameter) : base(parameter)
		{
			// Extract initial value from parameter
			if (parameter.ObjectValue is DateTime dt)
				this.Value = dt;
			else
				this.Value = parameter.Min;  // or DateTime.MinValue as a fallback
		}

		/// <summary>
		/// The DateTime value of the parameter.
		/// When changed, updates the underlying parameter value.
		/// </summary>
		public DateTime? DateTimeValue
		{
			get => this.Value as DateTime?;
			set => this.Value = value;
		}

		/// <summary>
		/// Helper to get or set just the Date portion.
		/// </summary>
		public DateTime? SelectedDate
		{
			get => this.DateTimeValue?.Date;
			set
			{
				if (value.HasValue)
				{
					DateTime current = this.DateTimeValue ?? DateTime.MinValue;
					this.DateTimeValue = new DateTime(value.Value.Year, value.Value.Month, value.Value.Day, current.Hour, current.Minute, current.Second);
				}
				else
				{
					this.DateTimeValue = null;
				}
			}
		}

		/// <summary>
		/// Helper to get or set just the Time portion.
		/// </summary>
		public TimeSpan SelectedTime
		{
			get => this.DateTimeValue?.TimeOfDay ?? TimeSpan.Zero;
			set
			{
				DateTime current = this.DateTimeValue ?? DateTime.MinValue;
				this.DateTimeValue = current.Date + value;
			}
		}
	}

	public partial class ObservableContractReferenceParameter : ObservableParameter
	{
		public ObservableContractReferenceParameter(ContractReferenceParameter parameter) : base(parameter)
		{
			this.Value = parameter.ObjectValue as string ?? string.Empty;
		}

		public string? ContractReferenceValue
		{
			get => this.Value as string ?? string.Empty;
			set => this.Value = value;
		}

		[RelayCommand]
		private async Task PickContractReferenceAsync()
		{
			try
			{
				TaskCompletionSource<Contract?> taskCompletionSource = new TaskCompletionSource<Contract?>();
				MyContractsNavigationArgs args = new MyContractsNavigationArgs(ContractsListMode.Contracts, taskCompletionSource);
				await ServiceRef.UiService.GoToAsync(nameof(MyContractsPage), args);
				Contract? contract = await taskCompletionSource.Task;
				Console.WriteLine("Contract selected: " + contract?.ContractId ?? String.Empty);

				this.ContractReferenceValue = contract?.ContractId;
			}
			catch (Exception e)
			{
				ServiceRef.LogService.LogException(e);
			}

			// Open a dialog to pick a contract reference
			// and set the value of the parameter
		} 
	}

}
#endregion
