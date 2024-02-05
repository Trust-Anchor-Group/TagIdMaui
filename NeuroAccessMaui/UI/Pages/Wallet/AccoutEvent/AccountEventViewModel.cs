﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeuroAccessMaui.Services;
using NeuroAccessMaui.UI.Converters;
using Waher.Networking.XMPP;

namespace NeuroAccessMaui.UI.Pages.Wallet.AccountEvent
{
	/// <summary>
	/// The view model to bind to for when displaying the contents of an account event.
	/// </summary>
	public partial class AccountEventViewModel : XmppViewModel
	{
		/// <summary>
		/// Creates an instance of the <see cref="AccountEventViewModel"/> class.
		/// </summary>
		public AccountEventViewModel()
			: base()
		{
		}

		/// <inheritdoc/>
		protected override async Task OnInitialize()
		{
			await base.OnInitialize();

			if (ServiceRef.NavigationService.TryGetArgs(out AccountEventNavigationArgs? args))
			{
				this.Remote = args.Event?.Remote;
				this.FriendlyName = args.Event?.FriendlyName;
				this.Timestamp = args.Event?.Timestamp;
				this.TimestampStr = args.Event?.TimestampStr;
				this.Change = args.Event?.Change;
				this.ChangeColor = args.Event?.TextColor;
				this.Balance = args.Event?.Balance;
				this.Reserved = args.Event?.Reserved;
				this.Message = args.Event?.Message;
				this.HasMessage = args.Event?.HasMessage ?? false;
				this.MessageIsUri = this.HasMessage && Uri.TryCreate(this.Message, UriKind.Absolute, out _);
				this.Id = args.Event?.TransactionId.ToString();
				this.Currency = args.Event?.Currency;

				this.ChangeText = this.Change is null ? string.Empty : MoneyToString.ToString(this.Change.Value);
				this.ChangeAndCurrency = this.ChangeText + " " + this.Currency;

				this.BalanceText = this.Balance is null ? string.Empty : MoneyToString.ToString(this.Balance.Value);
				this.BalanceAndCurrency = this.BalanceText + " " + this.Currency;

				this.ReservedText = this.Reserved is null ? string.Empty : MoneyToString.ToString(this.Reserved.Value);
				this.ReservedAndCurrency = this.ReservedText + " " + this.Currency;
			}
		}

		#region Properties

		/// <summary>
		/// Change of eDaler
		/// </summary>
		[ObservableProperty]
		private decimal? change;

		/// <summary>
		/// Color of <see cref="Change"/> field.
		/// </summary>
		[ObservableProperty]
		private Color? changeColor;

		/// <summary>
		/// <see cref="Change"/> as text.
		/// </summary>
		[ObservableProperty]
		private string? changeText;

		/// <summary>
		/// <see cref="ChangeText"/> and <see cref="Currency"/>.
		/// </summary>
		[ObservableProperty]
		private string? changeAndCurrency;

		/// <summary>
		/// Balance of eDaler
		/// </summary>
		[ObservableProperty]
		private decimal? balance;

		/// <summary>
		/// <see cref="Balance"/> as text.
		/// </summary>
		[ObservableProperty]
		private string? balanceText;

		/// <summary>
		/// <see cref="BalanceText"/> and <see cref="Currency"/>.
		/// </summary>
		[ObservableProperty]
		private string? balanceAndCurrency;

		/// <summary>
		/// Reserved of eDaler
		/// </summary>
		[ObservableProperty]
		private decimal? reserved;

		/// <summary>
		/// <see cref="Reserved"/> as text.
		/// </summary>
		[ObservableProperty]
		private string? reservedText;

		/// <summary>
		/// <see cref="ReservedText"/> and <see cref="Currency"/>.
		/// </summary>
		[ObservableProperty]
		private string? reservedAndCurrency;

		/// <summary>
		/// Currency of eDaler to process
		/// </summary>
		[ObservableProperty]
		private string? currency;

		/// <summary>
		/// When code was created.
		/// </summary>
		[ObservableProperty]
		private DateTime? timestamp;

		/// <summary>
		/// When code expires
		/// </summary>
		[ObservableProperty]
		private string? timestampStr;

		/// <summary>
		/// Globally unique identifier of code
		/// </summary>
		[ObservableProperty]
		private string? id;

		/// <summary>
		/// Remote who eDaler is to be transferred
		/// </summary>
		[ObservableProperty]
		private string? remote;

		/// <summary>
		/// FriendlyName who eDaler is to be transferred
		/// </summary>
		[ObservableProperty]
		private string? friendlyName;

		/// <summary>
		/// Message to recipient
		/// </summary>
		[ObservableProperty]
		private string? message;

		/// <summary>
		/// If a message is defined
		/// </summary>
		[ObservableProperty]
		private bool hasMessage;

		/// <summary>
		/// If a message is defined
		/// </summary>
		[ObservableProperty]
		private bool messageIsUri;

		#endregion

		#region Commands

		/// <summary>
		/// Command executed when user wants to open link in message.
		/// </summary>
		[RelayCommand(CanExecute = nameof(MessageIsUri))]
		private async Task OpenMessageLink()
		{
			if (!string.IsNullOrEmpty(this.Message))
				await App.OpenUrlAsync(this.Message);
		}

		#endregion


	}
}
