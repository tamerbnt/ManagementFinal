using System.Windows.Input;
using Management.Presentation.Extensions;
using Management.Presentation.ViewModels.Shell;
using Management.Presentation.ViewModels.Auth;
using Management.Presentation.Services;
using Management.Domain.Services;
using System.Threading.Tasks;
using Management.Infrastructure.Services;
using System;
using Microsoft.Extensions.DependencyInjection;
using Management.Application.Interfaces;
using Management.Presentation.ViewModels.Base;
using Management.Application.ViewModels.Base;
using Management.Presentation.Services.Localization;
using Microsoft.Extensions.Logging;
using Management.Application.Services;

namespace Management.Presentation.ViewModels
{
    public class OnboardingOwnerViewModel : FacilityAwareViewModelBase
    {
        private readonly INavigationService _navigationService;
        private readonly IDialogService _dialogService;
        private readonly Supabase.Client _supabase;
        private readonly IHardwareService _hardwareService;
        private readonly IOnboardingStateStore _onboardingState;
        private readonly IOnboardingService _onboardingService;

        private string _businessName = string.Empty;
        public string BusinessName
        {
            get => _businessName;
            set 
            {
                if (SetProperty(ref _businessName, value))
                {
                    ((AsyncRelayCommand)CompleteOnboardingCommand).RaiseCanExecuteChanged();
                }
            }
        }

        private string _adminFullName = string.Empty;
        public string AdminFullName
        {
            get => _adminFullName;
            set 
            {
                if (SetProperty(ref _adminFullName, value))
                {
                    ((AsyncRelayCommand)CompleteOnboardingCommand).RaiseCanExecuteChanged();
                }
            }
        }

        private string _adminEmail = string.Empty;
        public string AdminEmail
        {
            get => _adminEmail;
            set 
            {
                if (SetProperty(ref _adminEmail, value))
                {
                    ((AsyncRelayCommand)CompleteOnboardingCommand).RaiseCanExecuteChanged();
                }
            }
        }

        private string _password = string.Empty;
        public string Password
        {
            get => _password;
            set 
            {
                if (SetProperty(ref _password, value))
                {
                    ((AsyncRelayCommand)CompleteOnboardingCommand).RaiseCanExecuteChanged();
                }
            }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set 
            {
                if (SetProperty(ref _isBusy, value))
                {
                    ((AsyncRelayCommand)CompleteOnboardingCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public ICommand CompleteOnboardingCommand { get; }

        public OnboardingOwnerViewModel(
            INavigationService navigationService,
            IDialogService dialogService,
            Supabase.Client supabase,
            IOnboardingService onboardingService,
            IHardwareService hardwareService,
            IOnboardingStateStore onboardingState,
            ITerminologyService terminologyService,
            IFacilityContextService facilityContext,
            ILocalizationService localizationService,
            ILogger<OnboardingOwnerViewModel> logger,
            IDiagnosticService diagnosticService)
            : base(terminologyService, facilityContext, logger, diagnosticService, null, localizationService, dialogService)
        {
            _navigationService = navigationService;
            _dialogService = dialogService;
            _supabase = supabase;
            _onboardingService = onboardingService;
            _hardwareService = hardwareService;
            _onboardingState = onboardingState;

            CompleteOnboardingCommand = new AsyncRelayCommand(ExecuteCompleteOnboardingAsync, 
                () => !string.IsNullOrWhiteSpace(BusinessName) && 
                      !string.IsNullOrWhiteSpace(AdminFullName) &&
                      !string.IsNullOrWhiteSpace(AdminEmail) && 
                      !string.IsNullOrWhiteSpace(Password) && 
                      !IsBusy);
        }

        private async Task ExecuteCompleteOnboardingAsync()
        {
            Serilog.Log.Information($"[OnboardingOwnerViewModel] Starting onboarding for: {BusinessName} ({AdminEmail})");
            IsBusy = true;
            try
            {
                // Use the persisted license key (Genesis Flow)
                var licenseKey = _onboardingState.LicenseKey ?? "TEMP-LICENSE";
                Serilog.Log.Information($"[OnboardingOwnerViewModel] Using License Key: {licenseKey}");

                // 1. Create Account and Onboard Tenant
                var state = new Management.Application.DTOs.OnboardingState
                {
                    AdminEmail = AdminEmail,
                    AdminPassword = Password,
                    LicenseKey = licenseKey,
                    BusinessName = BusinessName,
                    AdminFullName = AdminFullName
                };
                
                System.Diagnostics.Debug.WriteLine($"[ONBOARDING] Facility provisioning starting for {BusinessName} {DateTime.Now:HH:mm:ss.fff}");
                var provStopwatch = System.Diagnostics.Stopwatch.StartNew();
                var result = await _onboardingService.CompleteOnboardingAsync(state);
                provStopwatch.Stop();
                System.Diagnostics.Debug.WriteLine($"[ONBOARDING] Provisioning result={result.IsSuccess} Duration={provStopwatch.ElapsedMilliseconds}ms");
                
                if (result.IsFailure)
                {
                    await _dialogService.ShowAlertAsync(_localizationService?.GetString("Strings.Auth.Title.Error") ?? "Error", string.Format(_localizationService?.GetString("Strings.Auth.Error.OnboardingFailed") ?? "Onboarding failed: {0}", result.Error.Message));
                    return;
                }

                var tenantId = result.Value;

                // 2. Register current device
                var deviceResult = await _onboardingService.RegisterCurrentDeviceAsync(tenantId, $"{Environment.MachineName} (Owner)", licenseKey);
                
                if (deviceResult.IsFailure)
                {
                    await _dialogService.ShowAlertAsync(_localizationService?.GetString("Strings.Auth.Title.Warning") ?? "Warning", string.Format(_localizationService?.GetString("Strings.Auth.Error.DeviceRegistrationFailed") ?? "Account created, but device registration failed: {0}", deviceResult.Error.Message));
                }

                // 3. Update Global Context
                var tenantService = (ITenantService)await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
                    ((App)System.Windows.Application.Current).ServiceProvider.GetRequiredService<ITenantService>());
                tenantService.SetTenantId(tenantId);
                
                // Persist TenantId to config file to prevent setup redirection on restart
                _facilityContext.SaveTenantId(tenantId);

                // 4. Trigger Service Re-initialization (Soft Restart)
                Serilog.Log.Information("[OnboardingOwnerViewModel] Triggering service re-initialization...");
                await ((App)System.Windows.Application.Current).ReinitializeOperationalServicesAsync();

                await _dialogService.ShowAlertAsync(_localizationService?.GetString("Strings.Auth.Title.Success") ?? "Success", _localizationService?.GetString("Strings.Auth.Message.OnboardingComplete") ?? "Setup complete! Your workspace has been initialized.", isSuccess: true);
                
                // Final wait for UI to settle
                await Task.Delay(1000);
                await _navigationService.NavigateToAsync<SplashOnboardingViewModel>();
            }
            catch (Exception ex)
            {
                await _dialogService.ShowAlertAsync(string.Format(_localizationService?.GetString("Strings.Auth.Error.OnboardingFailed") ?? "Onboarding failed: {0}", ex.Message), _localizationService?.GetString("Strings.Auth.Title.Error") ?? "Error");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
