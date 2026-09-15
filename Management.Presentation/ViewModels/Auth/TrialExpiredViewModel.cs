using System;
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
    public class TrialExpiredViewModel : FacilityAwareViewModelBase
    {
        private readonly INavigationService _navigationService;
        private readonly ILicenseService _licenseService;
        private readonly ITenantService _tenantService;
        private readonly IConfigurationService _configService;

        private string _licenseKey = string.Empty;
        public string LicenseKey
        {
            get => _licenseKey;
            set
            {
                if (SetProperty(ref _licenseKey, value))
                {
                    ErrorMessage = string.Empty;
                    ((AsyncRelayCommand)RedeemCommand).NotifyCanExecuteChanged();
                }
            }
        }

        private string _accountEmail = string.Empty;
        public string AccountEmail
        {
            get => _accountEmail;
            set => SetProperty(ref _accountEmail, value);
        }

        private string _expiryNotice = "Your 14-day evaluation period has ended.";
        public string ExpiryNotice
        {
            get => _expiryNotice;
            set => SetProperty(ref _expiryNotice, value);
        }

        private string _errorMessage = string.Empty;
        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                if (SetProperty(ref _errorMessage, value))
                {
                    OnPropertyChanged(nameof(HasError));
                }
            }
        }

        public bool HasError => !string.IsNullOrWhiteSpace(_errorMessage);

        public ICommand RedeemCommand { get; }
        public ICommand ExitAppCommand { get; }

        public TrialExpiredViewModel(
            INavigationService navigationService,
            ILicenseService licenseService,
            ITenantService tenantService,
            IConfigurationService configService,
            ITerminologyService terminologyService,
            IFacilityContextService facilityContext,
            ILocalizationService localizationService,
            ILogger<TrialExpiredViewModel> logger,
            IDiagnosticService diagnosticService,
            IDialogService dialogService)
            : base(terminologyService, facilityContext, logger, diagnosticService, null, localizationService, dialogService)
        {
            _navigationService = navigationService;
            _licenseService = licenseService;
            _tenantService = tenantService;
            _configService = configService;

            RedeemCommand = new AsyncRelayCommand(ExecuteRedeemAsync, () => !string.IsNullOrWhiteSpace(LicenseKey) && !IsBusy);
            ExitAppCommand = new RelayCommand(ExecuteExitApp);

            _ = LoadAccountDetailsAsync();
        }

        private async Task LoadAccountDetailsAsync()
        {
            try
            {
                var lease = await _configService.LoadConfigAsync<Management.Domain.Models.LicenseLease>("license.lease");
                if (lease != null)
                {
                    AccountEmail = lease.PlanName ?? "Registered Workspace";
                    ExpiryNotice = $"Evaluation expired on {lease.ExpiryDate.ToLocalTime():yyyy-MM-dd}. All your local records and data remain safely preserved.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[TrialExpiredViewModel] Could not load offline lease metadata.");
            }
        }

        private async Task ExecuteRedeemAsync()
        {
            if (string.IsNullOrWhiteSpace(LicenseKey)) return;

            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                var accountId = _tenantService.GetAccountId() ?? _tenantService.GetTenantId();
                if (!accountId.HasValue || accountId.Value == Guid.Empty)
                {
                    // Fallback to reading lease account id
                    var lease = await _configService.LoadConfigAsync<Management.Domain.Models.LicenseLease>("license.lease");
                    if (lease?.AccountId != null)
                    {
                        accountId = lease.AccountId;
                    }
                }

                if (!accountId.HasValue || accountId.Value == Guid.Empty)
                {
                    ErrorMessage = "Account identity could not be verified. Please connect to internet and restart.";
                    return;
                }

                var cleanCode = LicenseKey.Trim().ToUpper();
                _logger.LogInformation("[TrialExpiredViewModel] Redeeming voucher {Code} for account {AccountId}", cleanCode, accountId.Value);

                var result = await _licenseService.RedeemVoucherAsync(accountId.Value, cleanCode);
                if (result.IsValid)
                {
                    _logger.LogInformation("[TrialExpiredViewModel] Voucher redeemed successfully! Unlocking workspace.");
                    if (_dialogService != null)
                    {
                        await _dialogService.ShowAlertAsync(
                            "License Activated",
                            $"Congratulations! Your {result.PlanName} perpetual license has been activated. All existing data is unlocked.",
                            "Continue to Workspace",
                            isSuccess: true);
                    }

                    // Proceed to login or splash
                    await _navigationService.NavigateToLoginAsync();
                }
                else
                {
                    ErrorMessage = result.FailureReason ?? "Voucher redemption failed. Please verify the code and try again.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TrialExpiredViewModel] Exception during voucher upgrade.");
                ErrorMessage = $"Upgrade error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ExecuteExitApp()
        {
            System.Windows.Application.Current.Shutdown();
        }
    }
}
