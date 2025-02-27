using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeuroAccessMaui.UI.Pages.Wallet.MyWallet.ObjectModels;
using NeuroAccessMaui.Services;
using NeuroAccessMaui.Services.Notification;
using NeuroFeatures;
using NeuroFeatures.EventArguments;
using System.Collections.ObjectModel;
using Waher.Persistence;

namespace NeuroAccessMaui.UI.Pages.Wallet.MyTokens
{
	/// <summary>
	/// The view model to bind to for when displaying my tokens.
	/// </summary>
	/// <param name="Args">Navigation arguments</param>
	public partial class MyTokensViewModel(MyTokensNavigationArgs? Args) : XmppViewModel()
	{
		private readonly MyTokensNavigationArgs? navigationArgs = Args;

		/// <inheritdoc/>
		protected override async Task OnInitialize()
		{
			await base.OnInitialize();

			try
			{
				TokensEventArgs e = await ServiceRef.XmppService.GetNeuroFeatures(0, Constants.BatchSizes.TokenBatchSize);
				SortedDictionary<CaseInsensitiveString, NotificationEvent[]> EventsByCateogy = ServiceRef.NotificationService.GetEventsByCategory(NotificationEventType.Wallet);

				MainThread.BeginInvokeOnMainThread(() =>
				{
					if (e.Ok)
					{
						this.Tokens.Clear();

						if (e.Tokens is not null)
						{
							foreach (Token Token in e.Tokens)
							{
								if (!EventsByCateogy.TryGetValue(Token.TokenId, out NotificationEvent[]? Events))
									Events = [];

								this.Tokens.Add(new TokenItem(Token, this.navigationArgs?.TokenItemProvider, Events));
							}

							this.HasTokens = true;
							this.HasMoreTokens = e.Tokens.Length == Constants.BatchSizes.TokenBatchSize;
						}
						else
							this.HasTokens = false;
					}
					else
						this.HasTokens = false;
				});

				ServiceRef.XmppService.NeuroFeatureAdded += this.Wallet_TokenAdded;
				ServiceRef.XmppService.NeuroFeatureRemoved += this.Wallet_TokenRemoved;
			}
			catch (Exception ex)
			{
				ServiceRef.LogService.LogException(ex);
			}
		}

		/// <inheritdoc/>
		protected override Task OnDispose()
		{
			ServiceRef.XmppService.NeuroFeatureAdded -= this.Wallet_TokenAdded;
			ServiceRef.XmppService.NeuroFeatureRemoved -= this.Wallet_TokenRemoved;

			if (this.navigationArgs?.TokenItemProvider is TaskCompletionSource<TokenItem?> TaskSource)
				TaskSource.TrySetResult(null);

			return base.OnDispose();
		}

		private Task Wallet_TokenAdded(object? Sender, TokenEventArgs e)
		{
			if (!ServiceRef.NotificationService.TryGetNotificationEvents(NotificationEventType.Wallet, e.Token.TokenId, out NotificationEvent[]? Events))
				Events = [];

			MainThread.BeginInvokeOnMainThread(() =>
			{
				TokenItem Item = new(e.Token, this.navigationArgs?.TokenItemProvider, Events);

				if (this.Tokens.Count == 0)
					this.Tokens.Add(Item);
				else
					this.Tokens.Insert(0, Item);
			});

			return Task.CompletedTask;
		}

		private Task Wallet_TokenRemoved(object? Sender, TokenEventArgs e)
		{
			MainThread.BeginInvokeOnMainThread(() =>
			{
				int i, c = this.Tokens.Count;

				for (i = 0; i < c; i++)
				{
					TokenItem Item = this.Tokens[i];

					if (Item.TokenId == e.Token.TokenId)
					{
						this.Tokens.RemoveAt(i);
						break;
					}
				}
			});

			return Task.CompletedTask;
		}

		#region Properties

		/// <summary>
		/// HasTokens of eDaler to process
		/// </summary>
		[ObservableProperty]
		private bool hasTokens;

		/// <summary>
		/// HasMoreTokens of eDaler to process
		/// </summary>
		[ObservableProperty]
		private bool hasMoreTokens;

		/// <summary>
		/// Holds a list of tokens
		/// </summary>
		public ObservableCollection<TokenItem> Tokens { get; } = [];

		#endregion

		/// <summary>
		/// The command to bind to for returning to previous view.
		/// </summary>
		[RelayCommand]
		private Task Back()
		{
			return this.GoBack();
		}

		/// <inheritdoc/>
		public override Task GoBack()
		{
			this.navigationArgs?.TokenItemProvider.TrySetResult(null);
			return base.GoBack();
		}

		/// <summary>
		/// Command executed when more tokens need to be loaded.
		/// </summary>
		[RelayCommand]
		private async Task LoadMoreTokens()
		{
			if (this.HasMoreTokens)
			{
				this.HasMoreTokens = false; // So multiple requests are not made while scrolling.

				try
				{
					TokensEventArgs e = await ServiceRef.XmppService.GetNeuroFeatures(this.Tokens.Count, Constants.BatchSizes.TokenBatchSize);
					SortedDictionary<CaseInsensitiveString, NotificationEvent[]> EventsByCateogy = ServiceRef.NotificationService.GetEventsByCategory(NotificationEventType.Wallet);

					MainThread.BeginInvokeOnMainThread(() =>
					{
						if (e.Ok)
						{
							if (e.Tokens is not null)
							{
								foreach (Token Token in e.Tokens)
								{
									if (!EventsByCateogy.TryGetValue(Token.TokenId, out NotificationEvent[]? Events))
										Events = [];

									this.Tokens.Add(new TokenItem(Token, Events));
								}

								this.HasMoreTokens = e.Tokens.Length == Constants.BatchSizes.TokenBatchSize;
							}
						}
					});
				}
				catch (Exception ex)
				{
					ServiceRef.LogService.LogException(ex);
				}
			}
		}

	}
}
