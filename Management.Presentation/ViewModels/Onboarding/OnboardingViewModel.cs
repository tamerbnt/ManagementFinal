using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Application.Interfaces;
using Management.Application.Services;
using Management.Application.DTOs; // For OnboardingState
using Management.Infrastructure.Services; // For IOnboardingService
using Management.Presentation.ViewModels.Onboarding.Steps;
using IHardwareService = Management.Application.Interfaces.IHardwareService;

using Management.Presentation.ViewModels.Base;
using Management.Presentation.Services.Localization;
using Management.Application.ViewModels.Base;
using Management.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Management.Presentation.ViewModels.Onboarding
{
    public enum OnboardingStep
    {
        LicenseActivation,
        OwnerRegistration,
        BusinessProfile,
        FacilityConfig,
        Complete
    }

    public partial class OnboardingViewModel : FacilityAwareViewModelBase
    {
        private readonly IHardwareService _hardwareService;
        private readonly ILicenseService _licenseService;
        private readonly IAuthenticationService _authService;
        private readonly IOnboardingService _onboardingService;
        private readonly IConfigurationService _configService;

        [ObservableProperty]
        private OnboardingStep _currentStep = OnboardingStep.LicenseActivation;

        [ObservableProperty]
        private string _licenseKey = string.Empty;

        [ObservableProperty]
        private string _hardwareId = string.Empty;


        [ObservableProperty]
        private OnboardingState _state = new OnboardingState();

        [ObservableProperty]
        private AdminRegistrationViewModel? _adminRegistrationViewModel;

        [ObservableProperty]
        private BusinessInfoViewModel? _businessInfoViewModel;
        
        [ObservableProperty]
        private FacilityConfigViewModel? _facilityConfigViewModel;
        
        [ObservableProperty]
        private FinishViewModel? _finishViewModel;

        public OnboardingViewModel(
            IHardwareService hardwareService, 
            ILicenseService licenseService,
            IAuthenticationService authService,
            IOnboardingService onboardingService,
            IConfigurationService configService,
            ITerminologyService terminologyService,
            IFacilityContextService facilityContext,
            ILocalizationService localizationService,
            ILogger<OnboardingViewModel> logger,
            IDiagnosticService diagnosticService,
            IDialogService dialogService)
            : base(terminologyService, facilityContext, logger, diagnosticService, null, localizationService)
        {
            _hardwareService = hardwareService;
            _licenseService = licenseService;
            _authService = authService;
            _onboardingService = onboardingService;
            _configService = configService;
            
            _hardwareId = _localizationService?.GetString("Strings.Auth.Status.Discovering") ?? "Discovering...";
            
            // Initialize hardware ID asynchronously
            Task.Run(() =>
            {
                try
                {
                    HardwareId = _hardwareService.GetHardwareId();
                }
                catch
                {
                    HardwareId = _localizationService?.GetString("Strings.Auth.Error.HardwareError") ?? "HWD-ERROR-UNKNOWN";
                }
            });
        }

        protected override void OnLanguageChanged()
        {
            if (HardwareId == _localizationService?.GetString("Strings.Auth.Status.Discovering") || HardwareId == "Discovering...")
                HardwareId = _localizationService?.GetString("Strings.Auth.Status.Discovering") ?? "Discovering...";
        }

        [RelayCommand]
        private async Task VerifyLicense()
        {
            if (IsBusy) return;

            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                if (string.IsNullOrWhiteSpace(LicenseKey))
                {
                    ErrorMessage = _localizationService?.GetString("Strings.Auth.Error.LicenseEmpty") ?? "License key cannot be empty.";
                    return;
                }

                // Call the real LicenseService
                var result = await _licenseService.ValidateLicenseAsync(LicenseKey, HardwareId);

                if (result.IsValid)
                {
                    // CRITICAL: Synchronize state so subsequent steps have the verified key/hardware
                    State.LicenseKey = LicenseKey;
                    State.HardwareId = HardwareId;

                    // Move to registration step and initialize the ViewModel
                    CurrentStep = OnboardingStep.OwnerRegistration;
                    InitializeAdminRegistrationViewModel();
                }
                else
                {
                    ErrorMessage = result.ErrorMessage ?? _localizationService?.GetString("Strings.Auth.Error.InvalidLicense") ?? "Invalid License Key.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = string.Format(_localizationService?.GetString("Strings.Auth.Error.VerificationFailed") ?? "License verification failed: {0}", ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void InitializeAdminRegistrationViewModel()
        {
            AdminRegistrationViewModel = new AdminRegistrationViewModel(
                State,
                _terminologyService,
                _facilityContext,
                _logger,
                _diagnosticService,
                _localizationService!);
            
            AdminRegistrationViewModel.RequestNextStep = () =>
            {
                CurrentStep = OnboardingStep.BusinessProfile;
                InitializeBusinessInfoViewModel();
            };
        }

        private void InitializeBusinessInfoViewModel()
        {
            BusinessInfoViewModel = new BusinessInfoViewModel(
                State,
                _terminologyService,
                _facilityContext,
                _logger,
                _diagnosticService,
                _localizationService!,
                _dialogService);
            // Business step logic is usually handled by a "Next" button in the view binding to a command here
            // or the VM having a RequestNextStep.
            // Assuming the View has a "Next" button that Validate() and moves on.
        }

        private void InitializeFacilityConfigViewModel()
        {
            FacilityConfigViewModel = new FacilityConfigViewModel(
                State,
                _terminologyService,
                _facilityContext,
                _logger,
                _diagnosticService,
                _localizationService!,
                _dialogService);
        }

        private void InitializeFinishViewModel()
        {
            FinishViewModel = new FinishViewModel(
                _configService, 
                _onboardingService, 
                State,
                _terminologyService,
                _facilityContext,
                _logger,
                _diagnosticService,
                _localizationService!,
                _dialogService);
        }

        [RelayCommand]
        private void NavigateToNextStep()
        {
            // Generic Next Logic
            if (CurrentStep == OnboardingStep.BusinessProfile)
            {
                 // Validate Business VM
                 if(BusinessInfoViewModel != null && BusinessInfoViewModel.Validate())
                 {
                     CurrentStep = OnboardingStep.FacilityConfig;
                     InitializeFacilityConfigViewModel();
                 }
            }
            else if (CurrentStep == OnboardingStep.FacilityConfig)
            {
                 // Validate Facility VM
                 if(FacilityConfigViewModel != null && FacilityConfigViewModel.Validate())
                 {
                     CurrentStep = OnboardingStep.Complete;
                     InitializeFinishViewModel();
                 }
            }
        }
    }
}
