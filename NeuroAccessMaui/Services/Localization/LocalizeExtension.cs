using Microsoft.Extensions.Localization;
using NeuroAccessMaui.Resources.Languages;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Waher.Events;

namespace NeuroAccessMaui.Services.Localization
{
	/// <summary>
	/// Access to localized strings.
	/// </summary>
	[ContentProperty(nameof(Path))]
	public class LocalizeExtension : IMarkupExtension<BindingBase>, INotifyPropertyChanged, IDisposable
	{
		private static readonly Dictionary<Type, SortedDictionary<string, bool>> missingStrings = [];
		private static Timer? timer = null;

		private IStringLocalizer? localizer;
		private bool isDisposed;

		static LocalizeExtension()
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
		/// Localizer instance reference.
		/// </summary>
		public IStringLocalizer Localizer
		{
			get
			{
				this.localizer ??= LocalizationManager.GetStringLocalizer(this.StringResource);
				return this.localizer;
			}
		}

		public string Path { get; set; } = ".";
		public BindingMode Mode { get; set; } = BindingMode.OneWay;
		public IValueConverter? Converter { get; set; } = null;
		public string? ConverterParameter { get; set; } = null;
		public string? StringFormat { get; set; } = null;
		public Type? StringResource { get; set; } = null;

		public object ProvideValue(IServiceProvider ServiceProvider)
		{
			return (this as IMarkupExtension<BindingBase>).ProvideValue(ServiceProvider);
		}

		BindingBase IMarkupExtension<BindingBase>.ProvideValue(IServiceProvider ServiceProvider)
		{
			Type ResourcesType = this.StringResource ?? typeof(AppResources);

			if (ResourcesType.GetRuntimeProperties().FirstOrDefault(pi => pi.Name == this.Path) is null)
			{
				ReportMissingString(this.Path, ResourcesType);
				return new Binding("Localizer[STRINGNOTDEFINED]", this.Mode, this.Converter, this.ConverterParameter, this.StringFormat, this);
			}

			return new Binding($"Localizer[{this.Path}]", this.Mode, this.Converter, this.ConverterParameter, this.StringFormat, this);
		}

		/// <summary>
		/// Reports a missing string
		/// </summary>
		/// <param name="StringId">String ID without localized value.</param>
		/// <param name="Type">Type referencing string.</param>
		public static void ReportMissingString(string StringId, Type Type)
		{
			lock (missingStrings)
			{
				timer?.Dispose();
				timer = null;

				if (!missingStrings.TryGetValue(Type, out SortedDictionary<string, bool>? PerId))
				{
					PerId = [];
					missingStrings[Type] = PerId;
				}

				PerId[StringId] = true;

				timer = new Timer(LogAlert, null, 1000, Timeout.Infinite);
			}
		}

		private static void LogAlert(object? _)
		{
			StringBuilder sb = new();

			sb.AppendLine("Missing localized strings:");
			sb.AppendLine();

			lock (missingStrings)
			{
				foreach (KeyValuePair<Type, SortedDictionary<string, bool>> P in missingStrings)
				{
					sb.AppendLine();
					sb.AppendLine(P.Key.FullName);
					sb.AppendLine(new string('-', (P.Key.FullName?.Length ?? 0) + 3));
					sb.AppendLine();

					foreach (string Key in P.Value.Keys)
					{
						sb.Append("* ");
						sb.AppendLine(Key);
					}
				}

				missingStrings.Clear();
			}

			ServiceRef.LogService.LogAlert(sb.ToString());
		}

		public LocalizeExtension()
		{
			LocalizationManager.CurrentCultureChanged += this.OnCurrentCultureChanged;
		}

		~LocalizeExtension()
		{
			this.Dispose(false);
		}

		private void OnCurrentCultureChanged(object? sender, CultureInfo culture)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Localizer)));
		}

		/// <summary>
		/// <see cref="IDisposable.Dispose"/>
		/// </summary>
		public void Dispose()
		{
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// <see cref="IDisposable.Dispose"/>
		/// </summary>
		protected virtual void Dispose(bool disposing)
		{
			if (this.isDisposed)
				return;

			LocalizationManager.CurrentCultureChanged -= this.OnCurrentCultureChanged;

			this.isDisposed = true;
		}

		public event PropertyChangedEventHandler? PropertyChanged;
	}
}
