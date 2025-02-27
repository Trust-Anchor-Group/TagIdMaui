using NeuroAccessMaui.UI.Pages.Applications.Applications;
using NeuroAccessMaui.UI.Pages.Applications.ApplyId;
using NeuroAccessMaui.UI.Pages.Contacts.Chat;
using NeuroAccessMaui.UI.Pages.Contacts.MyContacts;
using NeuroAccessMaui.UI.Pages.Contracts.MyContracts;
using NeuroAccessMaui.UI.Pages.Contracts.NewContract;
using NeuroAccessMaui.UI.Pages.Contracts.ViewContract;
using NeuroAccessMaui.UI.Pages.Identity.TransferIdentity;
using NeuroAccessMaui.UI.Pages.Identity.ViewIdentity;
using NeuroAccessMaui.UI.Pages.Main.Calculator;
using NeuroAccessMaui.UI.Pages.Main.ChangePassword;
using NeuroAccessMaui.UI.Pages.Main.Duration;
using NeuroAccessMaui.UI.Pages.Main.QR;
using NeuroAccessMaui.UI.Pages.Main.Settings;
using NeuroAccessMaui.UI.Pages.Main.VerifyCode;
using NeuroAccessMaui.UI.Pages.Main.XmppForm;
using NeuroAccessMaui.UI.Pages.Petitions.PetitionContract;
using NeuroAccessMaui.UI.Pages.Petitions.PetitionIdentity;
using NeuroAccessMaui.UI.Pages.Petitions.PetitionPeerReview;
using NeuroAccessMaui.UI.Pages.Petitions.PetitionSignature;
using NeuroAccessMaui.UI.Pages.Signatures.ClientSignature;
using NeuroAccessMaui.UI.Pages.Signatures.ServerSignature;
using NeuroAccessMaui.UI.Pages.Things.CanControl;
using NeuroAccessMaui.UI.Pages.Things.CanRead;
using NeuroAccessMaui.UI.Pages.Things.IsFriend;
using NeuroAccessMaui.UI.Pages.Things.MyThings;
using NeuroAccessMaui.UI.Pages.Things.ReadSensor;
using NeuroAccessMaui.UI.Pages.Things.ViewClaimThing;
using NeuroAccessMaui.UI.Pages.Things.ViewThing;
using NeuroAccessMaui.UI.Pages.Utility.Images;
using NeuroAccessMaui.UI.Pages.Wallet.AccountEvent;
using NeuroAccessMaui.UI.Pages.Wallet.BuyEDaler;
using NeuroAccessMaui.UI.Pages.Wallet.EDalerReceived;
using NeuroAccessMaui.UI.Pages.Wallet.IssueEDaler;
using NeuroAccessMaui.UI.Pages.Wallet.MachineReport;
using NeuroAccessMaui.UI.Pages.Wallet.MachineVariables;
using NeuroAccessMaui.UI.Pages.Wallet.MyTokens;
using NeuroAccessMaui.UI.Pages.Wallet.MyWallet;
using NeuroAccessMaui.UI.Pages.Wallet.Payment;
using NeuroAccessMaui.UI.Pages.Wallet.PaymentAcceptance;
using NeuroAccessMaui.UI.Pages.Wallet.PendingPayment;
using NeuroAccessMaui.UI.Pages.Wallet.RequestPayment;
using NeuroAccessMaui.UI.Pages.Wallet.SellEDaler;
using NeuroAccessMaui.UI.Pages.Wallet.SendPayment;
using NeuroAccessMaui.UI.Pages.Wallet.ServiceProviders;
using NeuroAccessMaui.UI.Pages.Wallet.TokenDetails;
using NeuroAccessMaui.UI.Pages.Wallet.TokenEvents;
using Waher.Events;

namespace NeuroAccessMaui.UI.Pages.Main
{
	public partial class AppShell : Shell
	{
		public AppShell()
		{
			try
			{
				this.InitializeComponent();
				this.BindingContext = new AppShellViewModel();

				this.RegisterRoutes();
			}
			catch (Exception ex)
			{
#if DEBUG
				ex = Log.UnnestException(ex);
				App.SendAlertAsync(ex.Message, "text/plain").Wait();
#endif
				throw new ArgumentException("Unable to start app.", ex);
			}
		}

		private void RegisterRoutes()
		{
			// Applications:
			Routing.RegisterRoute(nameof(ApplicationsPage), typeof(ApplicationsPage));
			Routing.RegisterRoute(nameof(ApplyIdPage), typeof(ApplyIdPage));

			// Contacts
			Routing.RegisterRoute(nameof(ChatPage), typeof(ChatPage));
			Routing.RegisterRoute(nameof(MyContactsPage), typeof(MyContactsPage));

			// Contracts
			Routing.RegisterRoute(nameof(MyContractsPage), typeof(MyContractsPage));
			Routing.RegisterRoute(nameof(NewContractPage), typeof(NewContractPage));
			Routing.RegisterRoute(nameof(ViewContractPage), typeof(ViewContractPage));

			// Identity
			Routing.RegisterRoute(nameof(TransferIdentityPage), typeof(TransferIdentityPage));
			Routing.RegisterRoute(nameof(ViewIdentityPage), typeof(ViewIdentityPage));

			// Main
			Routing.RegisterRoute(nameof(CalculatorPage), typeof(CalculatorPage));
			Routing.RegisterRoute(nameof(ChangePasswordPage), typeof(ChangePasswordPage));
			Routing.RegisterRoute(nameof(DurationPage), typeof(DurationPage));
			Routing.RegisterRoute(nameof(ScanQrCodePage), typeof(ScanQrCodePage));
			Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
			Routing.RegisterRoute(nameof(VerifyCodePage), typeof(VerifyCodePage));
			Routing.RegisterRoute(nameof(XmppFormPage), typeof(XmppFormPage));

			// Petitions
			Routing.RegisterRoute(nameof(PetitionContractPage), typeof(PetitionContractPage));
			Routing.RegisterRoute(nameof(PetitionIdentityPage), typeof(PetitionIdentityPage));
			Routing.RegisterRoute(nameof(PetitionPeerReviewPage), typeof(PetitionPeerReviewPage));
			Routing.RegisterRoute(nameof(PetitionSignaturePage), typeof(PetitionSignaturePage));

			// Signatures
			Routing.RegisterRoute(nameof(ClientSignaturePage), typeof(ClientSignaturePage));
			Routing.RegisterRoute(nameof(ServerSignaturePage), typeof(ServerSignaturePage));

			// Things
			Routing.RegisterRoute(nameof(CanControlPage), typeof(CanControlPage));
			Routing.RegisterRoute(nameof(CanReadPage), typeof(CanReadPage));
			Routing.RegisterRoute(nameof(IsFriendPage), typeof(IsFriendPage));
			Routing.RegisterRoute(nameof(MyThingsPage), typeof(MyThingsPage));
			Routing.RegisterRoute(nameof(ReadSensorPage), typeof(ReadSensorPage));
			Routing.RegisterRoute(nameof(ViewClaimThingPage), typeof(ViewClaimThingPage));
			Routing.RegisterRoute(nameof(ViewThingPage), typeof(ViewThingPage));

			// Wallet
			Routing.RegisterRoute(nameof(AccountEventPage), typeof(AccountEventPage));
			Routing.RegisterRoute(nameof(BuyEDalerPage), typeof(BuyEDalerPage));
			Routing.RegisterRoute(nameof(EDalerReceivedPage), typeof(EDalerReceivedPage));
			Routing.RegisterRoute(nameof(IssueEDalerPage), typeof(IssueEDalerPage));
			Routing.RegisterRoute(nameof(MachineReportPage), typeof(MachineReportPage));
			Routing.RegisterRoute(nameof(MachineVariablesPage), typeof(MachineVariablesPage));
			Routing.RegisterRoute(nameof(MyTokensPage), typeof(MyTokensPage));
			Routing.RegisterRoute(nameof(MyEDalerWalletPage), typeof(MyEDalerWalletPage));
			Routing.RegisterRoute(nameof(MyTokensPage), typeof(MyTokensPage));
			Routing.RegisterRoute(nameof(PaymentPage), typeof(PaymentPage));
			Routing.RegisterRoute(nameof(PaymentAcceptancePage), typeof(PaymentAcceptancePage));
			Routing.RegisterRoute(nameof(PendingPaymentPage), typeof(PendingPaymentPage));
			Routing.RegisterRoute(nameof(RequestPaymentPage), typeof(RequestPaymentPage));
			Routing.RegisterRoute(nameof(SellEDalerPage), typeof(SellEDalerPage));
			Routing.RegisterRoute(nameof(SendPaymentPage), typeof(SendPaymentPage));
			Routing.RegisterRoute(nameof(ServiceProvidersPage), typeof(ServiceProvidersPage));
			Routing.RegisterRoute(nameof(TokenDetailsPage), typeof(TokenDetailsPage));
			Routing.RegisterRoute(nameof(TokenEventsPage), typeof(TokenEventsPage));

			// Utility
			Routing.RegisterRoute(nameof(ImageCroppingPage), typeof(ImageCroppingPage));
		}

		/// <summary>
		/// Method called when app has been started and loaded.
		/// </summary>
		public static void AppLoaded()
		{
			if (Current is AppShell AppShell && AppShell.BindingContext is AppShellViewModel ViewModel)
			{
				MainThread.BeginInvokeOnMainThread(async () =>
				{
					await ViewModel.DoInitialize();
				});
			}
		}
	}
}
