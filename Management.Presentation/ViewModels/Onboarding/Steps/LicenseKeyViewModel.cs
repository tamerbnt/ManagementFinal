using System;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Application.Interfaces;
using Management.Application.Services;
using Management.Presentation.ViewModels.Onboarding.Base;
using Microsoft.Extensions.Logging;
using Management.Presentation.Services;
using Management.Presentation.Services.Localization;
using Management.Domain.Services;

namespace Management.Presentation.ViewModels.Onboarding.Steps
{
    /// <summary>
    /// ViewModel for the License Verification step of the onboarding wizard.
    /// Handles hardware fingerprinting and license key validation state.
    /// </summary>
    public partial class LicenseKeyViewModel : WizardStepViewModel
    {
        private readonly IHardwareService _hardwareService;
        private readonly ILicenseService _licenseService;
        private readonly Management.Application.DTOs.OnboardingState _state;
        private Action? _onVerifySuccess;

        [ObservableProperty]
        private string _licenseKey = string.Empty;

        [ObservableProperty]
        private string _hardwareId = "Awaiting discovery...";

        [ObservableProperty]
        private bool _isBusy;

        public LicenseKeyViewModel(
            IHardwareService hardwareService, 
            ILicenseService licenseService, 
            Management.Application.DTOs.OnboardingState state,
            ITerminologyService terminologyService,
            IFacilityContextService facilityContext,
            ILogger<LicenseKeyViewModel> logger,
            IDiagnosticService diagnosticService,
            ILocalizationService localizationService,
            IDialogService dialogService)
            : base(terminologyService, facilityContext, logger, diagnosticService, localizationService, dialogService)
        {
            _hardwareService = hardwareService;
            _licenseService = licenseService;
            _state = state;
            
            Title = _localizationService?.GetString("Strings.Onboarding.LicenseVerification") ?? "License Verification";
            HardwareId = _localizationService?.GetString("Strings.Auth.Status.AwaitingDiscovery") ?? "Awaiting discovery...";
        }

        protected override void OnLanguageChanged()
        {
            Title = _localizationService?.GetString("Strings.Onboarding.LicenseVerification") ?? "License Verification";
            if (HardwareId == _localizationService?.GetString("Strings.Auth.Status.AwaitingDiscovery") || HardwareId == "Awaiting discovery...")
            {
                HardwareId = _localizationService?.GetString("Strings.Auth.Status.AwaitingDiscovery") ?? "Awaiting discovery...";
            }
        }

        /// <summary>
        /// Sets the callback to invoke when verification succeeds.
        /// </summary>
        public void SetVerifySuccessCallback(Action callback)
        {
            _onVerifySuccess = callback;
        }

        /// <summary>
        /// Fetches the unique hardware identifier asynchronously upon entering this step.
        /// </summary>
        public override async Task OnEnterAsync()
        {
            IsBusy = true;
            try 
            {
                HardwareId = await Task.Run(() => _hardwareService.GetHardwareId());
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Validates that a license key has been provided.
        /// </summary>
        public override bool Validate()
        {
            return !string.IsNullOrWhiteSpace(LicenseKey);
        }

        [RelayCommand]
        private async Task VerifyAsync()
        {
            try
            {
                IsBusy = true;

                // Validate input
                if (!Validate())
                {
                    await _dialogService.ShowAlertAsync(_localizationService?.GetString("Strings.Auth.Title.ValidationError") ?? "Validation Error", _localizationService?.GetString("Strings.Auth.Error.EnterLicenseKey") ?? "Please enter a valid license key.");
                    return;
                }

                // Call actual license service
                var result = await _licenseService.ValidateLicenseAsync(LicenseKey, HardwareId);

                if (result.IsValid)
                {
                     // Save to shared state
                    _state.LicenseKey = LicenseKey;
                    _state.HardwareId = HardwareId;

                     // Success - trigger navigation to next step
                    _onVerifySuccess?.Invoke();
                }
                else
                {
                    await _dialogService.ShowAlertAsync(_localizationService?.GetString("Strings.Auth.Title.ValidationError") ?? "Validation Error", string.Format(_localizationService?.GetString("Strings.Auth.Error.VerificationFailed") ?? "License verification failed: {0}", result.ErrorMessage));
                }
            }
            catch (Exception ex)
            {
                await _dialogService.ShowAlertAsync(_localizationService?.GetString("Strings.Auth.Title.Error") ?? "Error", string.Format(_localizationService?.GetString("Strings.Auth.Error.VerificationFailed") ?? "License verification failed: {0}", ex.Message));
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task CopyHardwareIdAsync()
        {
            try
            {
                Clipboard.SetText(HardwareId);
                // Optional: Show a brief confirmation (could use a toast notification in production)
            }
            catch (Exception ex)
            {
                await _dialogService.ShowAlertAsync(_localizationService?.GetString("Strings.Auth.Title.Error") ?? "Error", string.Format(_localizationService?.GetString("Strings.Auth.Error.CopyFailed") ?? "Failed to copy Hardware ID: {0}", ex.Message));
            }
        }
    }
}
