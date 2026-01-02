using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Management.Application.Stores;
using Management.Domain.Services;
using Management.Presentation.Services;
using Management.Presentation.Extensions;
using Management.Infrastructure.Services;
using Management.Domain.Primitives;

namespace Management.Presentation.ViewModels
{
    public class OnboardingViewModel : ViewModelBase
    {
        private readonly IOnboardingService _onboardingService;
        private readonly INavigationService _navigationService;
        private readonly IDialogService _dialogService;
        private readonly AccountStore _accountStore;

        private int _currentStep = 1;
        public int CurrentStep
        {
            get => _currentStep;
            set { _currentStep = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsStep1)); OnPropertyChanged(nameof(IsStep2)); OnPropertyChanged(nameof(IsStep3)); }
        }

        public bool IsStep1 => CurrentStep == 1;
        public bool IsStep2 => CurrentStep == 2;
        public bool IsStep3 => CurrentStep == 3;

        // Step 1: License
        private string _licenseKey = string.Empty;
        public string LicenseKey { get => _licenseKey; set { _licenseKey = value; OnPropertyChanged(); } }

        // Step 2: Identity
        private string _email = string.Empty;
        public string Email { get => _email; set { _email = value; OnPropertyChanged(); } }

        private string _password = string.Empty;
        public string Password { get => _password; set { _password = value; OnPropertyChanged(); } }

        private string _tenantName = string.Empty;
        public string TenantName 
        { 
            get => _tenantName; 
            set 
            { 
                _tenantName = value; 
                OnPropertyChanged(); 
                // Auto-generate kebab-case slug from business name
                TenantSlug = GenerateSlug(value);
            } 
        }

        private string _tenantSlug = string.Empty;
        public string TenantSlug { get => _tenantSlug; private set { _tenantSlug = value; OnPropertyChanged(); } }

        // Step 3: Device
        private string _deviceLabel = Environment.MachineName;
        public string DeviceLabel { get => _deviceLabel; set { _deviceLabel = value; OnPropertyChanged(); } }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set { _isBusy = value; OnPropertyChanged(); } }

        private Guid _newTenantId;

        public ICommand NextCommand { get; }
        public ICommand BackCommand { get; }

        public OnboardingViewModel(
            IOnboardingService onboardingService,
            INavigationService navigationService,
            IDialogService dialogService,
            AccountStore accountStore)
        {
            _onboardingService = onboardingService;
            _navigationService = navigationService;
            _dialogService = dialogService;
            _accountStore = accountStore;

            NextCommand = new RelayCommand(async () => await ExecuteNext(), () => !IsBusy);
            BackCommand = new RelayCommand(() => CurrentStep--, () => !IsBusy && CurrentStep > 1);
        }

        private async Task ExecuteNext()
        {
            IsBusy = true;
            try
            {
                if (CurrentStep == 1)
                {
                    var result = await _onboardingService.ValidateLicenseAsync(LicenseKey);
                    if (result.IsSuccess) CurrentStep = 2;
                    else await _dialogService.ShowAlertAsync(result.Error.Message, "Invalid License");
                }
                else if (CurrentStep == 2)
                {
                    var result = await _onboardingService.CreateAccountAndOnboardAsync(Email, Password, LicenseKey, TenantName, TenantSlug);
                    if (result.IsSuccess)
                    {
                        _newTenantId = result.Value;
                        CurrentStep = 3;
                    }
                    else await _dialogService.ShowAlertAsync(result.Error.Message, "Onboarding Failed");
                }
                else if (CurrentStep == 3)
                {
                    var result = await _onboardingService.RegisterCurrentDeviceAsync(_newTenantId, DeviceLabel);
                    if (result.IsSuccess)
                    {
                        await _dialogService.ShowAlertAsync("Onboarding complete! Launching your workspace...", "Success");
                        // Navigate to Dashboard - the MainViewModel will handle terminology
                        await _navigationService.NavigateToAsync<DashboardViewModel>();
                    }
                    else
                    {
                        await _dialogService.ShowAlertAsync(result.Error.Message, "Device Registration Failed");
                    }
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private string GenerateSlug(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            
            // Convert to lowercase, replace spaces and special chars with hyphens
            var slug = input.ToLowerInvariant()
                .Replace(" ", "-")
                .Replace("_", "-");
            
            // Remove any characters that aren't alphanumeric or hyphens
            slug = System.Text.RegularExpressions.Regex.Replace(slug, "[^a-z0-9-]", "");
            
            // Remove consecutive hyphens
            slug = System.Text.RegularExpressions.Regex.Replace(slug, "-+", "-");
            
            // Trim hyphens from start and end
            return slug.Trim('-');
        }
    }
}
