using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeuroAccessMaui.Resources.Languages;
using NeuroAccessMaui.Services;
using NeuroAccessMaui.UI.Controls;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using Waher.Script;
using Waher.Script.Operators.Vectors;

namespace NeuroAccessMaui.UI.Pages.Main.Calculator
{
	/// <summary>
	/// The view model to bind to for when displaying the calculator.
	/// </summary>
	public partial class CalculatorViewModel : XmppViewModel
	{
		/// <summary>
		/// Creates an instance of the <see cref="CalculatorViewModel"/> class.
		/// </summary>
		/// <param name="Args">Navigation arguments.</param>
		public CalculatorViewModel(CalculatorNavigationArgs? Args)
			: base()
		{
			this.Stack = [];
			this.MemoryItems = [];

			if (Args is not null)
			{
				this.Entry = Args.Entry;
				this.ViewModel = Args.ViewModel;
				this.Property = Args.Property;

				if (this.Entry is not null)
					this.Value = this.Entry.EntryData;
				else if (this.ViewModel is not null && this.Property is not null)
					this.Value = this.ViewModel.GetValue(this.Property)?.ToString() ?? string.Empty;
				else
					this.Value = string.Empty;
			}

			this.DecimalSeparator = NumberFormatInfo.CurrentInfo.NumberDecimalSeparator;
			this.DisplayMain = true;
			this.DisplayFunctions = false;
			this.DisplayHyperbolic = false;
			this.DisplayInverse = false;
			this.DisplayEndParenthesis = false;
			this.DisplayEquals = true;
			this.Status = string.Empty;
			this.Memory = null;
			this.Entering = false;
			this.NrParentheses = 0;
			this.HasValue = !string.IsNullOrEmpty(this.Value);
			this.HasStatistics = false;
		}

		/// <inheritdoc/>
		protected override async Task OnDispose()
		{
			await this.EvaluateStack(true);

			await base.OnDispose();
		}

		#region Properties

		/// <inheritdoc/>
		protected override void OnPropertyChanged(PropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);

			switch (e.PropertyName)
			{
				case nameof(this.Value):
					this.HasValue = !string.IsNullOrEmpty(this.Value);

					if (this.Entry is not null)
						this.Entry.EntryData = this.Value ?? string.Empty;

					if (this.ViewModel is not null && this.Property is not null)
						this.ViewModel.SetValue(this.Property, this.Value);
					break;

				case nameof(this.NrParentheses):
				case nameof(this.DisplayFunctions):
				case nameof(this.DisplayHyperbolic):
				case nameof(this.DisplayInverse):
					this.CalcDisplay();
					break;
			}
		}

		/// <summary>
		/// Current entry
		/// </summary>
		[ObservableProperty]
		private string? value;

		/// <summary>
		/// Current entry
		/// </summary>
		[ObservableProperty]
		private string? status;

		/// <summary>
		/// Current entry
		/// </summary>
		[ObservableProperty]
		private bool entering;

		/// <summary>
		/// If there's a value in the input field
		/// </summary>
		[ObservableProperty]
		private bool hasValue;

		/// <summary>
		/// If there's values available for statistical computations.
		/// </summary>
		[ObservableProperty]
		private bool hasStatistics;

		/// <summary>
		/// Number of open parentheses
		/// </summary>
		[ObservableProperty]
		private int nrParentheses;

		/// <summary>
		/// Current entry
		/// </summary>
		[ObservableProperty]
		private object? memory;

		/// <summary>
		/// Entry control, if available
		/// </summary>
		[ObservableProperty]
		private CompositeEntry? entry;

		/// <summary>
		/// View model, if available
		/// </summary>
		[ObservableProperty]
		private BaseViewModel? viewModel;

		/// <summary>
		/// Property, if available.
		/// </summary>
		[ObservableProperty]
		private string? property;

		/// <summary>
		/// Current decimal separator.
		/// </summary>
		[ObservableProperty]
		private string? decimalSeparator;

		/// <summary>
		/// If main key page is to be displayed
		/// </summary>
		[ObservableProperty]
		private bool displayMain;

		/// <summary>
		/// If function key page is to be displayed
		/// </summary>
		[ObservableProperty]
		private bool displayFunctions;

		/// <summary>
		/// If hyperbolic functions are to be displayed
		/// </summary>
		[ObservableProperty]
		private bool displayHyperbolic;

		/// <summary>
		/// If inverse functions are to be displayed
		/// </summary>
		[ObservableProperty]
		private bool displayInverse;

		/// <summary>
		/// If neither hyperbolic nor inverse functions are to be displayed
		/// </summary>
		[ObservableProperty]
		private bool displayNotHyperbolicNotInverse;

		/// <summary>
		/// If hyperbolic functions, but not inverse functions are to be displayed
		/// </summary>
		[ObservableProperty]
		private bool displayHyperbolicNotInverse;

		/// <summary>
		/// If inverse functions, but not hyperbolic functions are to be displayed
		/// </summary>
		[ObservableProperty]
		private bool displayNotHyperbolicInverse;

		/// <summary>
		/// If inverse hyperbolic functions are to be displayed
		/// </summary>
		[ObservableProperty]
		private bool displayHyperbolicInverse;

		/// <summary>
		/// If the equals button should be displayed
		/// </summary>
		[ObservableProperty]
		private bool displayEquals;

		/// <summary>
		/// If the end parenthesis button should be displayed.
		/// </summary>
		[ObservableProperty]
		private bool displayEndParenthesis;

		private void CalcDisplay()
		{
			this.DisplayHyperbolicInverse = this.DisplayFunctions && this.DisplayHyperbolic && this.DisplayInverse;
			this.DisplayNotHyperbolicInverse = this.DisplayFunctions && !this.DisplayHyperbolic && this.DisplayInverse;
			this.DisplayHyperbolicNotInverse = this.DisplayFunctions && this.DisplayHyperbolic && !this.DisplayInverse;
			this.DisplayNotHyperbolicNotInverse = this.DisplayFunctions && !this.DisplayHyperbolic && !this.DisplayInverse;
			this.DisplayEquals = this.DisplayMain && this.NrParentheses == 0;
			this.DisplayEndParenthesis = this.DisplayMain && this.NrParentheses > 0;
		}

		/// <summary>
		/// Holds the contents of the calculation stack
		/// </summary>
		public ObservableCollection<StackItem> Stack { get; }

		/// <summary>
		/// Holds the contents of the memory
		/// </summary>
		public ObservableCollection<object> MemoryItems { get; }

		#endregion

		#region Commands

		/// <summary>
		/// Command executed when user wants to toggle buttons.
		/// </summary>
		[RelayCommand]
		private void Toggle()
		{
			this.DisplayMain = !this.DisplayMain;
			this.DisplayFunctions = !this.DisplayFunctions;
		}

		/// <summary>
		/// Command executed when user wants to toggle hyperbolic functions on/off.
		/// </summary>
		[RelayCommand]
		private void ToggleHyperbolic()
		{
			this.DisplayHyperbolic = !this.DisplayHyperbolic;
		}

		/// <summary>
		/// Command executed when user wants to toggle inverse functions on/off.
		/// </summary>
		[RelayCommand]
		private void ToggleInverse()
		{
			this.DisplayInverse = !this.DisplayInverse;
		}

		/// <summary>
		/// Command executed when user presses a key buttonn.
		/// </summary>
		[RelayCommand]
		private async Task KeyPress(object P)
		{
			try
			{
				string Key = P?.ToString() ?? string.Empty;

				switch (Key)
				{
					// Key entry

					case "0":
						if (!this.Entering)
							break;

						this.Value += Key;
						break;

					case "1":
					case "2":
					case "3":
					case "4":
					case "5":
					case "6":
					case "7":
					case "8":
					case "9":
						if (!this.Entering)
						{
							this.Value = string.Empty;
							this.Entering = true;
						}

						this.Value += Key;
						break;

					case ".":
						Key = NumberFormatInfo.CurrentInfo.NumberDecimalSeparator;
						this.Value += Key;
						this.Entering = true;
						break;

					// Results

					case "C":
						this.Value = string.Empty;
						this.Entering = false;
						break;

					case "CE":
						this.Value = string.Empty;
						this.Memory = null;
						this.Stack.Clear();
						this.MemoryItems.Clear();
						this.Entering = false;
						this.HasStatistics = false;

						this.OnPropertyChanged(nameof(this.StackString));
						this.OnPropertyChanged(nameof(this.MemoryString));
						break;

					case "=":
						await this.EvaluateStack();
						break;

					// Unary operators

					case "+-":
						await this.Evaluate("-x");
						break;

					case "1/x":
						await this.Evaluate("1/x");
						break;

					case "%":
						await this.Evaluate("x%");
						break;

					case "%0":
						await this.Evaluate("x‰");
						break;

					case "°":
						await this.Evaluate("x°");
						break;

					case "x2":
						await this.Evaluate("x^2");
						break;

					case "sqrt":
						await this.Evaluate("sqrt(x)");
						break;

					case "10^x":
						await this.Evaluate("10^x");
						break;

					case "2^x":
						await this.Evaluate("2^x");
						break;

					case "rad":
						await this.Evaluate("x*180/pi");
						break;

					// Binary operators

					case "+":
						await this.Evaluate("x+y", "+", OperatorPriority.Terms, false);
						break;

					case "-":
						await this.Evaluate("x-y", "−", OperatorPriority.Terms, false);
						break;

					case "*":
						await this.Evaluate("x*y", "⨉", OperatorPriority.Factors, false);
						break;

					case "/":
						await this.Evaluate("x/y", "÷", OperatorPriority.Factors, false);
						break;

					case "^":
						await this.Evaluate("x^y", "^", OperatorPriority.Powers, false);
						break;

					case "yrt":
						await this.Evaluate("x^(1/y)", "ʸ√", OperatorPriority.Powers, false);
						break;

					// Order

					case "(":

						if (this.Entering)
						{
							await this.Evaluate("x*y", "⨉", OperatorPriority.Factors, true);
							break;
						}

						if (this.Stack.Count > 0)
						{
							this.Stack[^1].StartParenthesis = true;
							this.OnPropertyChanged(nameof(this.StackString));
						}

						break;

					case ")":
						await this.Evaluate(string.Empty, "=", OperatorPriority.Parenthesis, false);
						if (this.Stack.Count > 0)
						{
							this.Stack[^1].StartParenthesis = false;
							this.OnPropertyChanged(nameof(this.StackString));
						}
						break;

					// Analytical Funcions

					case "exp":
					case "lg":
					case "log2":
					case "ln":
					case "sin":
					case "sinh":
					case "asin":
					case "asinh":
					case "cos":
					case "cosh":
					case "acos":
					case "acosh":
					case "tan":
					case "tanh":
					case "atan":
					case "atanh":
					case "sec":
					case "sech":
					case "asec":
					case "asech":
					case "csc":
					case "csch":
					case "acsc":
					case "acsch":
					case "cot":
					case "coth":
					case "acot":
					case "acoth":
						await this.Evaluate(Key + "(x)");
						break;

					// Other scalar functions

					case "abs":
					case "sign":
					case "round":
					case "ceil":
					case "floor":
						await this.Evaluate(Key + "(x)");
						break;

					case "frac":
						await this.Evaluate("x-floor(x)");
						break;

					// Statistics

					case "M+":
						await this.AddToMemory();
						break;

					case "M-":
						await this.SubtractFromMemory();
						break;

					case "MR":
						this.Value = Expression.ToString(this.Memory);
						this.Entering = false;
						break;

					case "avg":
					case "stddev":
					case "sum":
					case "prod":
					case "min":
					case "max":
						await this.EvaluateStatistics(Key + "(x)");
						break;
				}
			}
			catch (Exception ex)
			{
				this.Status = ex.Message;
			}
		}

		private async Task<object> Evaluate()
		{
			if (string.IsNullOrEmpty(this.Value))
				throw new Exception(ServiceRef.Localizer[nameof(AppResources.EnterValue)]);

			try
			{
				return await Expression.EvalAsync(this.Value);
			}
			catch (Exception)
			{
				throw new Exception(ServiceRef.Localizer[nameof(AppResources.EnterValidValue)]);
			}
		}

		private async Task Evaluate(string Script)
		{
			object x = await this.Evaluate();

			try
			{
				Variables v = [];

				v["x"] = x;

				object y = await Expression.EvalAsync(Script, v);

				this.Value = Expression.ToString(y);
				this.Entering = false;
			}
			catch (Exception)
			{
				throw new Exception(ServiceRef.Localizer[nameof(AppResources.CalculationError)]);
			}
		}

		private async Task Evaluate(string Script, string Operator, OperatorPriority Priority, bool StartParenthesis)
		{
			object x = await this.Evaluate();
			StackItem Item;
			int c = this.Stack.Count;

			// if (c > 0 && (Item = this.Stack[c - 1]).StartParenthesis)
			// 	Priority = OperatorPriority.Parenthesis;

			while (c > 0 && (Item = this.Stack[c - 1]).Priority >= Priority && !Item.StartParenthesis)
			{
				object y = x;

				this.Value = Item.Entry ?? string.Empty;
				x = await this.Evaluate();

				try
				{
					Variables v = [];

					v["x"] = x;
					v["y"] = y;

					x = await Expression.EvalAsync(Item.Script, v);

					this.Value = Expression.ToString(x);
					this.Entering = false;
				}
				catch (Exception)
				{
					throw new Exception(ServiceRef.Localizer[nameof(AppResources.CalculationError)]);
				}

				c--;
				this.Stack.RemoveAt(c);
			}

			if (!string.IsNullOrEmpty(Script))
			{
				this.Stack.Add(new StackItem()
				{
					Entry = this.Value,
					Script = Script,
					Operator = Operator,
					Priority = Priority,
					StartParenthesis = StartParenthesis
				});

				this.Value = string.Empty;
			}

			this.Entering = false;
			this.OnPropertyChanged(nameof(this.StackString));
		}

		private async Task EvaluateStatistics(string Script)
		{
			List<Waher.Script.Abstraction.Elements.IElement> Elements = [];

			foreach (object Item in this.MemoryItems)
				Elements.Add(Expression.Encapsulate(Item));

			try
			{
				Variables v = [];

				v["x"] = VectorDefinition.Encapsulate(Elements, false, null);

				object y = await Expression.EvalAsync(Script, v);

				this.Value = Expression.ToString(y);
				this.Entering = false;
			}
			catch (Exception)
			{
				throw new Exception(ServiceRef.Localizer[nameof(AppResources.CalculationError)]);
			}
		}

		/// <summary>
		/// String representation of contents on the stack.
		/// </summary>
		public string StackString
		{
			get
			{
				StringBuilder sb = new();
				bool First = true;
				int NrParantheses = 0;
				OperatorPriority PrevPriority = OperatorPriority.Equals;
				bool StartParenthesis = false;

				foreach (StackItem Item in this.Stack)
				{
					if (First)
						First = false;
					else
						sb.Append(' ');

					if (Item.Priority < PrevPriority || StartParenthesis)
					{
						NrParantheses++;
						sb.Append(" ( ");
					}

					PrevPriority = Item.Priority;
					StartParenthesis = Item.StartParenthesis;

					sb.Append(Item.Entry);
					sb.Append(' ');
					sb.Append(Item.Operator);
				}

				if (StartParenthesis)
				{
					sb.Append(" (");
					NrParantheses++;
				}

				this.NrParentheses = NrParantheses;

				while (NrParantheses > 0)
				{
					sb.Append(" )");
					NrParantheses--;
				}

				return sb.ToString();
			}
		}

		/// <summary>
		/// Evaluates the current stack.
		/// </summary>
		public Task EvaluateStack()
		{
			return this.EvaluateStack(false);
		}

		/// <summary>
		/// Evaluates the current stack.
		/// </summary>
		public async Task EvaluateStack(bool IgnoreError)
		{
			if (this.Stack.Count == 0 && string.IsNullOrEmpty(this.Value))
				return;

			if (IgnoreError)
			{
				try
				{
					await this.Evaluate(string.Empty, "=", OperatorPriority.Equals, false);
				}
				catch (Exception)
				{
					// Ignore
				}
			}
			else
				await this.Evaluate(string.Empty, "=", OperatorPriority.Equals, false);
		}

		/// <summary>
		/// String representation of contents on the statistical memory.
		/// </summary>
		public string MemoryString
		{
			get
			{
				if (this.Memory is null)
					return string.Empty;

				StringBuilder sb = new();

				sb.Append("M: ");
				sb.Append(Expression.ToString(this.Memory));
				sb.Append(" (");
				sb.Append(this.MemoryItems.Count.ToString(CultureInfo.InvariantCulture));
				sb.Append(')');

				return sb.ToString();
			}
		}

		private async Task AddToMemory()
		{
			await this.EvaluateStack();

			object x = await this.Evaluate();

			this.MemoryItems.Add(x);

			if (this.Memory is null)
				this.Memory = x;
			else
			{
				Variables v = [];

				v["M"] = this.Memory;
				v["x"] = x;

				this.Memory = await Expression.EvalAsync("M+x", v);
			}

			this.HasStatistics = true;
			this.OnPropertyChanged(nameof(this.MemoryString));
		}

		private async Task SubtractFromMemory()
		{
			await this.EvaluateStack();

			object x = await this.Evaluate();

			this.MemoryItems.Add(x);

			if (this.Memory is null)
			{
				Variables v = [];

				v["x"] = x;

				this.Memory = await Expression.EvalAsync("-x", v);
			}
			else
			{
				Variables v = [];

				v["M"] = this.Memory;
				v["x"] = x;

				this.Memory = await Expression.EvalAsync("M+x", v);
			}

			this.HasStatistics = true;
			this.OnPropertyChanged(nameof(this.MemoryString));
		}

		#endregion
	}
}
