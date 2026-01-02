using System.Windows.Input;
using Management.Presentation.Extensions;
using Management.Presentation.Services;
using Management.Domain.Services;
using System.Threading.Tasks;
using Management.Infrastructure.Services;
using System;
using Microsoft.Extensions.DependencyInjection;

namespace Management.Presentation.ViewModels
{
    public class OnboardingOwnerViewModel : ViewModelBase
    {
        private readonly INavigationService _navigationService;
        private readonly IDialogService _dialogService;
        private readonly Supabase.Client _supabase;
        private readonly IOnboardingService _onboardingService;
        private readonly IHardwareService _hardwareService;
        private readonly IOnboardingStateStore _onboardingState;

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
            IOnboardingStateStore onboardingState)
        {
            _navigationService = navigationService;
            _dialogService = dialogService;
            _supabase = supabase;
            _onboardingService = onboardingService;
            _hardwareService = hardwareService;
            _onboardingState = onboardingState;

            CompleteOnboardingCommand = new AsyncRelayCommand(ExecuteCompleteOnboardingAsync, 
                () => !string.IsNullOrWhiteSpace(BusinessName) && 
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
                var slug = BusinessName.ToLower().Replace(" ", "-");
                var result = await _onboardingService.CreateAccountAndOnboardAsync(AdminEmail, Password, licenseKey, BusinessName, slug);
                
                if (result.IsFailure)
                {
                    await _dialogService.ShowAlertAsync("Error", $"Onboarding failed: {result.Error.Message}");
                    return;
                }

                var tenantId = result.Value;

                // 2. Register current device
                var deviceResult = await _onboardingService.RegisterCurrentDeviceAsync(tenantId, $"{Environment.MachineName} (Owner)");
                
                if (deviceResult.IsFailure)
                {
                    await _dialogService.ShowAlertAsync("Warning", $"Account created, but device registration failed: {deviceResult.Error.Message}");
                }

                // 3. Update Global Context
                var tenantService = (ITenantService)System.Windows.Application.Current.Dispatcher.Invoke(() => 
                    ((App)System.Windows.Application.Current).ServiceProvider.GetRequiredService<ITenantService>());
                tenantService.SetTenantId(tenantId);

                // 4. Trigger Service Re-initialization (Soft Restart)
                Serilog.Log.Information("[OnboardingOwnerViewModel] Triggering service re-initialization...");
                await ((App)System.Windows.Application.Current).ReinitializeOperationalServicesAsync();

                await _dialogService.ShowAlertAsync("Success", "Setup complete! Your workspace has been initialized.", isSuccess: true);
                
                // Final wait for UI to settle
                await Task.Delay(1000);
                await _navigationService.NavigateToAsync<DashboardViewModel>();
            }
            catch (Exception ex)
            {
                await _dialogService.ShowAlertAsync($"Onboarding failed: {ex.Message}", "Error");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
