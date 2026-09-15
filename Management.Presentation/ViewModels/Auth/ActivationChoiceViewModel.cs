using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Management.Application.Services;
using Management.Domain.Services;
using Management.Presentation.Services;
using Management.Presentation.Services.Localization;
using Management.Presentation.ViewModels.Base;
using Management.Application.Interfaces;

namespace Management.Presentation.ViewModels.Auth
{
    /// <summary>
    /// Step 1 of the Phase 2 onboarding flow: Activation Choice.
    /// Presents two options:
    ///   1. Start 14-Day Free Evaluation (Trial)
    ///   2. Activate with Lifetime Voucher / License Key
    /// Hosted inside AuthWindow's floating white card.
    /// </summary>
    public class ActivationChoiceViewModel : FacilityAwareViewModelBase
    {
        private readonly INavigationService _navigationService;
        private readonly IOnboardingStateStore _onboardingState;
        private readonly IConfigurationService? _configService;

        private bool _isTrialAvailable = true;
        public bool IsTrialAvailable
        {
            get => _isTrialAvailable;
            set => SetProperty(ref _isTrialAvailable, value);
        }

        private string _trialBadgeText = "Instant Access";
        public string TrialBadgeText
        {
            get => _trialBadgeText;
            set => SetProperty(ref _trialBadgeText, value);
        }

        public ICommand StartTrialCommand { get; }
        public ICommand EnterVoucherCommand { get; }

        public ActivationChoiceViewModel(
            INavigationService navigationService,
            IOnboardingStateStore onboardingState,
            ITerminologyService terminologyService,
            IFacilityContextService facilityContext,
            ILocalizationService localizationService,
            ILogger<ActivationChoiceViewModel> logger,
            IDiagnosticService diagnosticService,
            IDialogService dialogService,
            IConfigurationService? configService = null)
            : base(terminologyService, facilityContext, logger, diagnosticService, null, localizationService, dialogService)
        {
            _navigationService = navigationService;
            _onboardingState = onboardingState;
            _configService = configService;

            StartTrialCommand = new AsyncRelayCommand(ExecuteStartTrialAsync);
            EnterVoucherCommand = new AsyncRelayCommand(ExecuteEnterVoucherAsync);

            _ = CheckTrialAvailabilityAsync();
        }

        private async Task CheckTrialAvailabilityAsync()
        {
            try
            {
                if (_configService != null)
                {
                    var lease = await _configService.LoadConfigAsync<Management.Domain.Models.LicenseLease>("license.lease");
                    if (lease != null && (lease.AccountId.HasValue || lease.ExpiryDate <= System.DateTime.UtcNow))
                    {
                        IsTrialAvailable = false;
                        TrialBadgeText = "Trial Already Used";
                        Serilog.Log.Information("[ActivationChoiceViewModel] Trial previously used on this hardware. Disabling trial choice.");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Serilog.Log.Warning(ex, "[ActivationChoiceViewModel] Could not read license lease to verify trial availability.");
            }
        }

        private async Task ExecuteStartTrialAsync()
        {
            if (!IsTrialAvailable)
            {
                if (_dialogService != null)
                {
                    await _dialogService.ShowAlertAsync(
                        "Evaluation Trial Used",
                        "The 14-day free evaluation has already been utilized on this device. Please activate with a license key or cash voucher to proceed.",
                        "Activate License");
                    await _navigationService.NavigateToAsync<LicenseEntryViewModel>();
                }
                return;
            }

            Serilog.Log.Information("[ActivationChoiceViewModel] User chose 14-Day Free Evaluation (Trial).");
            _onboardingState.VoucherCode = null;
            _onboardingState.LicenseKey = "TRIAL";
            await _navigationService.NavigateToAsync<OnboardingOwnerViewModel>();
        }

        private async Task ExecuteEnterVoucherAsync()
        {
            Serilog.Log.Information("[ActivationChoiceViewModel] User chose to enter Lifetime Voucher / License Key.");
            await _navigationService.NavigateToAsync<LicenseEntryViewModel>();
        }
    }
}
