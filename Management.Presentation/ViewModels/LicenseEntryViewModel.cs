using System.Windows.Input;
using Management.Presentation.Extensions;
using Management.Presentation.Services;
using Management.Domain.Services;
using Management.Infrastructure.Services;
using System.Threading.Tasks;

namespace Management.Presentation.ViewModels
{
    public class LicenseEntryViewModel : ViewModelBase
    {
        private readonly INavigationService _navigationService;
        private readonly IDialogService _dialogService;
        private readonly Supabase.Client _supabase;
        private readonly IHardwareService _hardwareService;
        private readonly ITerminologyService _terminologyService;
        private readonly IOnboardingService _onboardingService;
        private readonly IOnboardingStateStore _onboardingState;

        private string _licenseKey = string.Empty;
        public string LicenseKey
        {
            get => _licenseKey;
            set 
            {
                if (SetProperty(ref _licenseKey, value))
                {
                    ((AsyncRelayCommand)ActivateCommand).RaiseCanExecuteChanged();
                }
            }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        // --- Terminology Injection ---
        public string TitleLabel => "Product Activation"; // Fallback to key if preferred
        public string DescriptionLabel => "This device is not registered. Please enter your license key to activate your workspace.";
        public string InputHeaderLabel => "LICENSE KEY";
        public string InputPlaceholderLabel => "e.g. 1234-ABCD-5678-EFGH";
        public string ActionButtonLabel => "Activate Workspace";
        public string FooterLabel => "Need help? Contact support@antigravity.corp";

        public ICommand ActivateCommand { get; }

        public LicenseEntryViewModel(
            INavigationService navigationService,
            IDialogService dialogService,
            Supabase.Client supabase,
            IHardwareService hardwareService,
            ITerminologyService terminologyService,
            IOnboardingService onboardingService,
            IOnboardingStateStore onboardingState)
        {
            _navigationService = navigationService;
            _dialogService = dialogService;
            _supabase = supabase;
            _hardwareService = hardwareService;
            _terminologyService = terminologyService;
            _onboardingService = onboardingService;
            _onboardingState = onboardingState;

            ActivateCommand = new AsyncRelayCommand(ExecuteActivateAsync, () => !string.IsNullOrWhiteSpace(LicenseKey) && !IsBusy);
        }

        private async Task ExecuteActivateAsync()
        {
            Serilog.Log.Information($"[LicenseEntryViewModel] ActivateCommand executed for LicenseKey: '{LicenseKey}'");
            IsBusy = true;
             try
            {
                Serilog.Log.Information("[LicenseEntryViewModel] Calling ValidateLicenseAsync...");
                var result = await _onboardingService.ValidateLicenseAsync(LicenseKey);
                Serilog.Log.Information($"[LicenseEntryViewModel] Result Received. Success: {result.IsSuccess}");
                
                if (result.IsSuccess)
                {
                    var validation = result.Value;
                    _onboardingState.LicenseKey = LicenseKey; // Persist for next steps

                    if (!validation.IsAssigned)
                    {
                        // Schema 1: Genesis Flow
                        Serilog.Log.Information("[LicenseEntryViewModel] Genesis Flow: Navigating to OnboardingOwnerViewModel...");
                        await _dialogService.ShowAlertAsync("Activation Success", "License Validated. Let's set up your workspace.", isSuccess: true);
                        await _navigationService.NavigateToAsync<OnboardingOwnerViewModel>();
                    }
                    else
                    {
                        // Schema 3: Expansion Flow
                        Serilog.Log.Information("[LicenseEntryViewModel] Expansion Flow: Setting state and navigating to LoginViewModel...");
                        
                        _onboardingState.ExpansionMessage = "This license is active. Please log in to authorize this new device.";
                        _onboardingState.TargetTenantId = validation.TenantId;

                        await _navigationService.NavigateToLoginAsync();
                    }
                }
                else
                {
                    Serilog.Log.Error($"[LicenseEntryViewModel] Validation Failed: {result.Error.Message}");
                    await _dialogService.ShowAlertAsync(result.Error.Message, "Activation Failed");
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[LicenseEntryViewModel] UNHANDLED EXCEPTION in Activate command");
                await _dialogService.ShowAlertAsync($"Unexpected Error: {ex.Message}", "Critical Error");
            }
            finally
            {
                IsBusy = false;
                Serilog.Log.Information("[LicenseEntryViewModel] Command completed, IsBusy = false");
            }
        }
    }
}
