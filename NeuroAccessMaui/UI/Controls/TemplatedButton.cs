﻿using System.Windows.Input;
using NeuroAccessMaui.Services;
using NeuroAccessMaui.UI.Core;

namespace NeuroAccessMaui.UI.Controls
{
	/// <summary>
	/// TemplatedButton represents a generalization of a Button whose appearance is defined by a <see cref="ControlTemplate"/>.
	/// </summary>
	public class TemplatedButton : ContentView, IButtonElement
	{
		/// <summary>
		/// Initializes a new instance of <see cref="TemplatedButton"/> class.
		/// </summary>
		public TemplatedButton() :
			base()
		{
			TapGestureRecognizer TapRecognizer = new();
			TapRecognizer.Tapped += this.OnTapped;
			this.GestureRecognizers.Add(TapRecognizer);
		}

		readonly WeakEventManager onClickedEventManager = new();

		/// <summary>
		/// The backing store for the <see cref="Command" /> bindable property.
		/// </summary>
		public static readonly BindableProperty CommandProperty = ButtonElement.CommandProperty;

		/// <summary>
		/// The backing store for the <see cref="CommandParameter" /> bindable property.
		/// </summary>
		public static readonly BindableProperty CommandParameterProperty = ButtonElement.CommandParameterProperty;

		/// <summary>
		/// Gets or sets the command to invoke when the button is activated. This is a bindable property.
		/// </summary>
		/// <remarks>This property is used to associate a command with an instance of a button. This property is most often set in
		/// the MVVM pattern to bind callbacks back into the ViewModel. <see cref="VisualElement.IsEnabled" /> is controlled by the
		/// <see cref="ICommand.CanExecute(object)"/> if set.</remarks>
		public ICommand? Command
		{
			get => (ICommand?)this.GetValue(CommandProperty);
			set => this.SetValue(CommandProperty, value);
		}

		/// <summary>
		/// Gets or sets the parameter to pass to the <see cref="Command"/> property.
		/// The default value is <see langword="null"/>. This is a bindable property.
		/// </summary>
		public object? CommandParameter
		{
			get => this.GetValue(CommandParameterProperty);
			set => this.SetValue(CommandParameterProperty, value);
		}

		void ICommandElement.CanExecuteChanged(object? sender, EventArgs e) => this.RefreshIsEnabledProperty();

		void IButtonElement.PropagateUpClicked() => this.onClickedEventManager.HandleEvent(this, EventArgs.Empty, nameof(Clicked));

		protected override bool IsEnabledCore => base.IsEnabledCore && CommandElement.GetCanExecute(this);

		/// <summary>
		/// Occurs when the button is clicked/tapped.
		/// </summary>
		public event EventHandler Clicked
		{
			add => this.onClickedEventManager.AddEventHandler(value);
			remove => this.onClickedEventManager.RemoveEventHandler(value);
		}

		private async void OnTapped(object? Sender, EventArgs e)
		{
			try
			{
				// Change visual state to "Pressed"
				VisualStateManager.GoToState(this, ButtonElement.PressedVisualState);

				// Initialize TaskCompletionSource to await animation
				TaskCompletionSource<bool> AnimationTask = new();

				// Start the animation
				this.Animate("Blink", new Animation()
				{
					{ 0, 0.5, new((value) => this.Scale = value, 1, 0.95, Easing.CubicOut) },
					{ 0.5, 1, new((value) => this.Scale = value, 0.95, 1, Easing.CubicOut) },
				},
				finished: (value, isCompleted) => AnimationTask.SetResult(isCompleted));

				// Await the animation's completion
				await AnimationTask.Task;

				VisualStateManager.GoToState(this, VisualStateManager.CommonStates.Normal);

				ButtonElement.ElementClicked(this, this);
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
			}
		}
	}
}
