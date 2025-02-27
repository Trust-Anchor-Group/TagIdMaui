using CommunityToolkit.Maui;
using NeuroAccessMaui.UI.Controls;
using NeuroAccessMaui.UI.Pages;
using NeuroAccessMaui.UI.Pages.Applications.Applications;
using NeuroAccessMaui.UI.Pages.Applications.ApplyId;
using NeuroAccessMaui.UI.Pages.Contacts.Chat;
using NeuroAccessMaui.UI.Pages.Contacts.MyContacts;
using NeuroAccessMaui.UI.Pages.Contracts.MyContracts;
using NeuroAccessMaui.UI.Pages.Contracts.NewContract;
using NeuroAccessMaui.UI.Pages.Contracts.ViewContract;
using NeuroAccessMaui.UI.Pages.Identity.TransferIdentity;
using NeuroAccessMaui.UI.Pages.Identity.ViewIdentity;
using NeuroAccessMaui.UI.Pages.Main;
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
using NeuroAccessMaui.UI.Pages.Petitions.PetitionPeerReview.Views;
using NeuroAccessMaui.UI.Pages.Petitions.PetitionSignature;
using NeuroAccessMaui.UI.Pages.Registration;
using NeuroAccessMaui.UI.Pages.Registration.Views;
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
using NeuroAccessMaui.UI.Pages.Wallet;
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
using NeuroAccessMaui.UI.Popups.Photos.Image;
using NeuroAccessMaui.UI.Popups.Tokens.AddTextNote;
using NeuroAccessMaui.UI.Popups.Xmpp.RemoveSubscription;
using NeuroAccessMaui.UI.Popups.Xmpp.ReportOrBlock;
using NeuroAccessMaui.UI.Popups.Xmpp.ReportType;
using NeuroAccessMaui.UI.Popups.Xmpp.SubscribeTo;
using NeuroAccessMaui.UI.Popups.Xmpp.SubscriptionRequest;

namespace NeuroAccessMaui.UI
{
	public static class PageAppExtension
	{
		public static MauiAppBuilder RegisterPagesManager(this MauiAppBuilder Builder)
		{
			// Applications
			Builder.Services.AddTransient<ApplicationsPage, ApplicationsViewModel>();
			Builder.Services.AddTransient<ApplyIdPage, ApplyIdViewModel>();

			// Contacts
			Builder.Services.AddTransient<ChatPage, ChatViewModel>();
			Builder.Services.AddTransient<MyContactsPage, ContactListViewModel>();

			// Contracts
			Builder.Services.AddTransient<MyContractsPage, MyContractsViewModel>();
			Builder.Services.AddTransient<NewContractPage, NewContractViewModel>();
			Builder.Services.AddTransient<ViewContractPage, ViewContractViewModel>();

			// Identity
			Builder.Services.AddTransient<TransferIdentityPage, TransferIdentityViewModel>();
			Builder.Services.AddTransient<ViewIdentityPage, ViewIdentityViewModel>();

			// Main
			Builder.Services.AddTransient<AppShell>();
			Builder.Services.AddTransient<CalculatorPage, CalculatorViewModel>();
			Builder.Services.AddTransient<ChangePasswordPage, ChangePasswordViewModel>();
			Builder.Services.AddTransient<DurationPage, DurationViewModel>();
			Builder.Services.AddTransient<MainPage, MainViewModel>();
			Builder.Services.AddTransient<ScanQrCodePage, ScanQrCodeViewModel>();
			Builder.Services.AddTransient<SettingsPage, SettingsViewModel>();
			Builder.Services.AddTransient<VerifyCodePage, VerifyCodeViewModel>();
			Builder.Services.AddTransient<XmppFormPage, XmppViewModel>();

			// Petitions
			Builder.Services.AddTransient<PetitionContractPage, PetitionContractViewModel>();
			Builder.Services.AddTransient<PetitionIdentityPage, PetitionIdentityViewModel>();
			Builder.Services.AddTransient<PetitionPeerReviewPage, PetitionPeerReviewViewModel>();
			Builder.Services.AddTransient<PetitionSignaturePage, PetitionSignatureViewModel>();
			Builder.Services.AddTransient<PhotoView, EmptyViewModel>();
			Builder.Services.AddTransient<NameView, EmptyViewModel>();
			Builder.Services.AddTransient<PnrView, EmptyViewModel>();
			Builder.Services.AddTransient<NationalityView, EmptyViewModel>();
			Builder.Services.AddTransient<BirthDateView, EmptyViewModel>();
			Builder.Services.AddTransient<GenderView, EmptyViewModel>();
			Builder.Services.AddTransient<PersonalAddressInfoView, EmptyViewModel>();
			Builder.Services.AddTransient<OrganizationalInfoView, EmptyViewModel>();
			Builder.Services.AddTransient<ConsentView, EmptyViewModel>();
			Builder.Services.AddTransient<AuthenticateView, EmptyViewModel>();
			Builder.Services.AddTransient<ApprovedView, EmptyViewModel>();

			Builder.Services.AddTransient<PetitionPeerReviewNavigationArgs>();


			// Registration
			Builder.Services.AddTransient<RegistrationPage, RegistrationViewModel>();
			Builder.Services.AddTransient<LoadingView, LoadingViewModel>();
			//Builder.Services.AddTransient<RequestPurposeView, RequestPurposeViewModel>();
			Builder.Services.AddTransient<ValidatePhoneView, ValidatePhoneViewModel>();
			Builder.Services.AddTransient<ValidateEmailView, ValidateEmailViewModel>();
			Builder.Services.AddTransient<ChooseProviderView, ChooseProviderViewModel>();
			Builder.Services.AddTransient<CreateAccountView, CreateAccountViewModel>();
			Builder.Services.AddTransient<GetStartedView, GetStartedViewModel>();
			Builder.Services.AddTransient<NameEntryView, NameEntryViewModel>();
			Builder.Services.AddTransient<DefinePasswordView, DefinePasswordViewModel>();
			Builder.Services.AddTransient<BiometricsView, BiometricsViewModel>();
			Builder.Services.AddTransient<FinalizeView, FinalizeViewModel>();
			Builder.Services.AddTransient<ContactSupportView, ContactSupportViewModel>();

			// Signatures
			Builder.Services.AddTransient<ClientSignaturePage, ClientSignatureViewModel>();
			Builder.Services.AddTransient<ServerSignaturePage, ServerSignatureViewModel>();

			// Things
			Builder.Services.AddTransient<CanControlPage, CanControlViewModel>();
			Builder.Services.AddTransient<CanReadPage, CanReadViewModel>();
			Builder.Services.AddTransient<IsFriendPage, IsFriendViewModel>();
			Builder.Services.AddTransient<MyThingsPage, MyThingsViewModel>();
			Builder.Services.AddTransient<ReadSensorPage, ReadSensorViewModel>();
			Builder.Services.AddTransient<ViewClaimThingPage, ViewClaimThingViewModel>();
			Builder.Services.AddTransient<ViewThingPage, ViewThingViewModel>();

			// Wallet
			Builder.Services.AddTransient<AccountEventPage, AccountEventViewModel>();
			Builder.Services.AddTransient<BuyEDalerPage, BuyEDalerViewModel>();
			Builder.Services.AddTransient<EDalerReceivedPage, EDalerReceivedViewModel>();
			Builder.Services.AddTransient<IssueEDalerPage, EDalerUriViewModel>();
			Builder.Services.AddTransient<MachineReportPage, MachineReportViewModel>();
			Builder.Services.AddTransient<MachineVariablesPage, MachineVariablesViewModel>();
			Builder.Services.AddTransient<MyTokensPage, MyTokensViewModel>();
			Builder.Services.AddTransient<MyEDalerWalletPage, MyWalletViewModel>();
			Builder.Services.AddTransient<MyTokenWalletPage, MyWalletViewModel>();
			Builder.Services.AddTransient<PaymentPage, EDalerUriViewModel>();
			Builder.Services.AddTransient<PaymentAcceptancePage, EDalerUriViewModel>();
			Builder.Services.AddTransient<PendingPaymentPage, EDalerUriViewModel>();
			Builder.Services.AddTransient<RequestPaymentPage, RequestPaymentViewModel>();
			Builder.Services.AddTransient<SellEDalerPage, SellEDalerViewModel>();
			Builder.Services.AddTransient<SendPaymentPage, EDalerUriViewModel>();
			Builder.Services.AddTransient<ServiceProvidersPage, ServiceProvidersViewModel>();
			Builder.Services.AddTransient<TokenDetailsPage, TokenDetailsViewModel>();
			Builder.Services.AddTransient<TokenEventsPage, TokenEventsViewModel>();

			// Popups
			Builder.Services.AddTransient<ImageView, ImageViewModel>();
			Builder.Services.AddTransient<AddTextNotePopup, AddTextNoteViewModel>();

			// Xmpp
			Builder.Services.AddTransient<RemoveSubscriptionPopup, RemoveSubscriptionViewModel>();
			Builder.Services.AddTransient<ReportOrBlockPopup, ReportOrBlockViewModel>();
			Builder.Services.AddTransient<ReportTypePopup, ReportTypeViewModel>();
			Builder.Services.AddTransient<SubscribeToPopup, SubscribeToViewModel>();
			Builder.Services.AddTransient<SubscriptionRequestPopup, SubscriptionRequestViewModel>();

			//Utility
			Builder.Services.AddTransient<ImageCropperView, ImageCroppingViewModel>();

			return Builder;
		}
	}
}
