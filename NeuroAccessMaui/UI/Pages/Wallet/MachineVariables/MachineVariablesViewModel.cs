using CommunityToolkit.Mvvm.ComponentModel;
using NeuroAccessMaui.Services;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Waher.Script;

namespace NeuroAccessMaui.UI.Pages.Wallet.MachineVariables
{
	/// <summary>
	/// The view model to bind to for when displaying information about the current state of a state-machine.
	/// </summary>
	public partial class MachineVariablesViewModel : BaseViewModel
	{
		/// <summary>
		/// The view model to bind to for when displaying information about the current state of a state-machine.
		/// </summary>
		/// <param name="Args">Navigation arguments</param>
		public MachineVariablesViewModel(MachineVariablesNavigationArgs? Args)
			: base()
		{
			this.Variables = [];

			if (Args is not null)
			{
				this.Running = Args.Running;
				this.Ended = Args.Ended;
				this.CurrentState = Args.CurrentState;

				if (Args.Variables is not null)
				{
					foreach (Variable Variable in Args.Variables)
						this.Variables.Add(new VariableModel(Variable.Name, Variable.ValueObject));
				}
			}
		}

		/// <inheritdoc/>
		protected override async Task OnInitialize()
		{
			await base.OnInitialize();

			ServiceRef.XmppService.NeuroFeatureVariablesUpdated += this.Wallet_VariablesUpdated;
			ServiceRef.XmppService.NeuroFeatureStateUpdated += this.Wallet_StateUpdated;
		}

		/// <inheritdoc/>
		protected override Task OnDispose()
		{
			ServiceRef.XmppService.NeuroFeatureVariablesUpdated -= this.Wallet_VariablesUpdated;
			ServiceRef.XmppService.NeuroFeatureStateUpdated -= this.Wallet_StateUpdated;

			return base.OnDispose();
		}

		private Task Wallet_StateUpdated(object? Sender, NeuroFeatures.EventArguments.NewStateEventArgs e)
		{
			MainThread.BeginInvokeOnMainThread(() =>
			{
				this.CurrentState = e.NewState;
				this.Ended = string.IsNullOrEmpty(e.NewState);
				this.Running = !this.Ended;
			});

			return Task.CompletedTask;
		}

		private Task Wallet_VariablesUpdated(object? Sender, NeuroFeatures.EventArguments.VariablesUpdatedEventArgs e)
		{
			MainThread.BeginInvokeOnMainThread(() =>
			{
				foreach (Variable Variable in e.Variables)
				{
					if (this.TryGetVariableMode(Variable.Name, out VariableModel? Model))
						Model.UpdateValue(Variable.ValueObject);
					else
						this.Variables.Add(new VariableModel(Variable.Name, Variable.ValueObject));
				}
			});

			return Task.CompletedTask;
		}

		private bool TryGetVariableMode(string Name, [NotNullWhen(true)] out VariableModel? Result)
		{
			foreach (VariableModel Model in this.Variables)
			{
				if (Model.Name == Name)
				{
					Result = Model;
					return true;
				}
			}

			Result = null;
			return false;
		}

		#region Properties

		/// <summary>
		/// Current variables
		/// </summary>
		public ObservableCollection<VariableModel> Variables { get; }

		/// <summary>
		/// If the state-machine is running
		/// </summary>
		[ObservableProperty]
		private bool running;

		/// <summary>
		/// If the state-machine has ended
		/// </summary>
		[ObservableProperty]
		private bool ended;

		/// <summary>
		/// Current state of state-machine
		/// </summary>
		[ObservableProperty]
		private string? currentState;

		#endregion

	}
}
