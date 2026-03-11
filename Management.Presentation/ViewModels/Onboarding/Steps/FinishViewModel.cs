using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Application.DTOs;
using Management.Application.Interfaces;
using Management.Application.Services;
using Management.Domain.Services;
using Management.Presentation.ViewModels.Onboarding.Base;
using Management.Presentation.Services.Localization;
using Management.Presentation.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Management.Application.ViewModels.Base;
using Management.Presentation.ViewModels.Shell;
using Management.Presentation.Views.Shell;

namespace Management.Presentation.ViewModels.Onboarding.Steps
{
    public partial class FinishViewModel : WizardStepViewModel
    {
        private readonly IConfigurationService _configService;
        private readonly Management.Infrastructure.Services.IOnboardingService _onboardingService;

        public OnboardingState State { get; }

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        public FinishViewModel(
            IConfigurationService configService, 
            Management.Infrastructure.Services.IOnboardingService onboardingService, 
            OnboardingState state,
            ITerminologyService terminologyService,
            IFacilityContextService facilityContext,
            ILogger logger,
            IDiagnosticService diagnosticService,
            ILocalizationService localizationService,
            IDialogService dialogService)
            : base(terminologyService, facilityContext, logger, diagnosticService, localizationService, dialogService)
        {
            _configService = configService;
            _onboardingService = onboardingService;
            State = state;
            Title = _localizationService?.GetString("Strings.Auth.AccountSetup") ?? "Setup Complete";
        }

        protected override void OnLanguageChanged()
        {
            Title = _localizationService?.GetString("Strings.Auth.AccountSetup") ?? "Setup Complete";
        }

        public override bool Validate()
        {
            return true; // Final step is just a "finish" button
        }

        [RelayCommand]
        private async Task Finish()
        {
            try 
            {
                IsBusy = true;
                StatusMessage = _localizationService?.GetString("Strings.Auth.Loading.FinalizingSetup") ?? "Finalizing account setup...";

                // 1. Call Transactional Onboarding (One-Shot)
                var result = await _onboardingService.CompleteOnboardingAsync(State);

                if (result.IsFailure)
                {
                    ErrorMessage = string.Format(_localizationService?.GetString("Strings.Auth.Error.OnboardingFailed") ?? "Onboarding Failed: {0}", result.Error.Message);
                    HasError = true;
                    return;
                }

                Guid facilityId = result.Value;
                StatusMessage = _localizationService?.GetString("Strings.Onboarding.Steps.CreatingAccount") ?? "Account created. Registering device...";

                // 2. Register current device
                var deviceResult = await _onboardingService.RegisterCurrentDeviceAsync(facilityId, "Primary Workstation", State.LicenseKey);
                
                if (deviceResult.IsFailure)
                {
                     ErrorMessage = string.Format(_localizationService?.GetString("Strings.Auth.Error.DeviceRegistrationFailed") ?? "Account created but device registration failed: {0}", deviceResult.Error.Message);
                     HasError = true;
                }

                // 3. Save local config
                await _configService.SaveConfigAsync(State, "license.dat");

                // Get current window (Onboarding)
                var currentWindow = System.Windows.Application.Current.Windows.OfType<System.Windows.Window>().FirstOrDefault(w => w.DataContext is OnboardingViewModel);

                if (System.Windows.Application.Current is App app)
                {
                    var mainWindow = app.ServiceProvider.GetService(typeof(MainWindow)) as System.Windows.Window;
                    if (mainWindow != null)
                    {
                        mainWindow.Show();
                        System.Windows.Application.Current.MainWindow = mainWindow;
                        currentWindow?.Close();
                    }
                    else
                    {
                         await _dialogService.ShowAlertAsync(_localizationService?.GetString("Strings.Auth.Title.Error") ?? "Error", _localizationService?.GetString("Strings.Auth.Message.SetupFailed") ?? "Main application window could not be initialized.");
                    }
                }
            }  
            catch (Exception ex)
            {
                ErrorMessage = string.Format(_localizationService?.GetString("Strings.Auth.Message.SetupFailed") ?? "Failed to complete setup: {0}", ex.Message);
                HasError = true;
            }
            finally
            {
                IsBusy = false;
                StatusMessage = string.Empty;
            }
        }
    }
}
