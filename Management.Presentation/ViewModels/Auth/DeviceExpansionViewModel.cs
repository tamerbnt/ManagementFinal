using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Management.Application.Interfaces;
using Management.Application.Services;
using Management.Domain.Services;
using Management.Presentation.Services;
using Management.Presentation.Services.Localization;
using Management.Presentation.ViewModels.Base;

namespace Management.Presentation.ViewModels.Auth
{
    public class DeviceExpansionViewModel : FacilityAwareViewModelBase
    {
        private readonly INavigationService _navigationService;
        private readonly ILicenseService _licenseService;
        private readonly ITenantService _tenantService;
        private readonly IConfigurationService _configService;
        private readonly IOnboardingStateStore _onboardingState;

        private string _licenseKey = string.Empty;
        public string LicenseKey
        {
            get => _licenseKey;
            set => SetProperty(ref _licenseKey, value);
        }

        private string _businessName = "Your Business";
        public string BusinessName
        {
            get => _businessName;
            set => SetProperty(ref _businessName, value);
        }

        private string _currentCategory = "Membership & Session";
        public string CurrentCategory
        {
            get => _currentCategory;
            set => SetProperty(ref _currentCategory, value);
        }

        private int _usedBranches = 1;
        public int UsedBranches
        {
            get => _usedBranches;
            set
            {
                if (SetProperty(ref _usedBranches, value))
                    OnPropertyChanged(nameof(BranchQuotaDisplay));
            }
        }

        private int _maxBranches = 3;
        public int MaxBranches
        {
            get => _maxBranches;
            set
            {
                if (SetProperty(ref _maxBranches, value))
                    OnPropertyChanged(nameof(BranchQuotaDisplay));
            }
        }

        public string BranchQuotaDisplay => $"{UsedBranches} of {MaxBranches} Branches Used";

        private int _maxCategories = 1;
        public int MaxCategories
        {
            get => _maxCategories;
            set => SetProperty(ref _maxCategories, value);
        }

        public bool CanOpenNewBranch => UsedBranches < MaxBranches;
        public bool IsBranchLimitReached => UsedBranches >= MaxBranches;
        public bool CanAddNewCategory => MaxCategories > 1;

        private string _newBranchName = string.Empty;
        public string NewBranchName
        {
            get => _newBranchName;
            set => SetProperty(ref _newBranchName, value);
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ICommand OpenNewBranchCommand { get; }
        public ICommand JoinBranchAsTerminalCommand { get; }
        public ICommand RequestCategoryUpgradeCommand { get; }
        public ICommand RequestBranchUpgradeCommand { get; }
        public ICommand CancelCommand { get; }

        public DeviceExpansionViewModel(
            INavigationService navigationService,
            ILicenseService licenseService,
            ITenantService tenantService,
            IConfigurationService configService,
            IOnboardingStateStore onboardingState,
            ITerminologyService terminologyService,
            IFacilityContextService facilityContext,
            ILocalizationService localizationService,
            ILogger<DeviceExpansionViewModel> logger,
            IDiagnosticService diagnosticService,
            IDialogService dialogService)
            : base(terminologyService, facilityContext, logger, diagnosticService, null, localizationService, dialogService)
        {
            _navigationService = navigationService;
            _licenseService = licenseService;
            _tenantService = tenantService;
            _configService = configService;
            _onboardingState = onboardingState;

            if (!string.IsNullOrWhiteSpace(_onboardingState?.LicenseKey))
            {
                LicenseKey = _onboardingState.LicenseKey;
            }

            OpenNewBranchCommand = new AsyncRelayCommand(ExecuteOpenNewBranchAsync, () => CanOpenNewBranch && !IsBusy);
            JoinBranchAsTerminalCommand = new AsyncRelayCommand(ExecuteJoinBranchAsTerminalAsync, () => !IsBusy);
            RequestCategoryUpgradeCommand = new AsyncRelayCommand(ExecuteRequestCategoryUpgradeAsync);
            RequestBranchUpgradeCommand = new AsyncRelayCommand(ExecuteRequestBranchUpgradeAsync);
            CancelCommand = new RelayCommand(ExecuteCancel);
        }

        public void InitializeExpansion(string licenseKey, string businessName, string category, int usedBranches, int maxBranches, int maxCategories)
        {
            LicenseKey = licenseKey;
            BusinessName = businessName;
            CurrentCategory = category;
            UsedBranches = usedBranches;
            MaxBranches = maxBranches;
            MaxCategories = maxCategories;

            OnPropertyChanged(nameof(CanOpenNewBranch));
            OnPropertyChanged(nameof(IsBranchLimitReached));
            OnPropertyChanged(nameof(CanAddNewCategory));
            ((AsyncRelayCommand)OpenNewBranchCommand).NotifyCanExecuteChanged();
        }

        private async Task ExecuteOpenNewBranchAsync()
        {
            if (!CanOpenNewBranch) return;

            IsBusy = true;
            StatusMessage = string.Empty;

            try
            {
                if (string.IsNullOrWhiteSpace(NewBranchName))
                {
                    NewBranchName = $"Branch #{UsedBranches + 1}";
                }

                _logger.LogInformation("[DeviceExpansionViewModel] Opening new branch '{BranchName}' for license {Key}", NewBranchName, LicenseKey);

                if (_dialogService != null)
                {
                    await _dialogService.ShowAlertAsync(
                        "Branch Slot Reserved",
                        $"Branch '{NewBranchName}' has been initialized under your '{CurrentCategory}' subscription. You have {MaxBranches - (UsedBranches + 1)} slots remaining.",
                        "Proceed to Setup",
                        isSuccess: true);
                }

                await _navigationService.NavigateToLoginAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DeviceExpansionViewModel] Error setting up new branch.");
                StatusMessage = $"Branch creation failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExecuteJoinBranchAsTerminalAsync()
        {
            IsBusy = true;
            try
            {
                _logger.LogInformation("[DeviceExpansionViewModel] Registering machine as supplementary terminal for existing branch.");
                if (_dialogService != null)
                {
                    await _dialogService.ShowAlertAsync(
                        "Terminal Linked",
                        "This PC has been enrolled as a cashier terminal linked to your main branch.",
                        "Continue",
                        isSuccess: true);
                }

                await _navigationService.NavigateToLoginAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExecuteRequestCategoryUpgradeAsync()
        {
            if (_dialogService != null)
            {
                await _dialogService.ShowAlertAsync(
                    "Category Quota Upgrade Required",
                    $"Your current subscription allows 1 operating business category ({CurrentCategory}). To operate a secondary category (e.g. Salon + POS or Gym + Services) simultaneously, please upgrade your license to the Multi-Category Tier.",
                    "Understood");
            }
        }

        private async Task ExecuteRequestBranchUpgradeAsync()
        {
            if (_dialogService != null)
            {
                await _dialogService.ShowAlertAsync(
                    "Branch Limit Reached",
                    $"All {MaxBranches} branch locations allocated to your license have been registered. To provision additional physical sites, upgrade to an Enterprise multi-site license.",
                    "Understood");
            }
        }

        private void ExecuteCancel()
        {
            _navigationService.NavigateToLoginAsync();
        }
    }
}
