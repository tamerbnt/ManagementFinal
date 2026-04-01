using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Management.Application.Services;
using Management.Application.Stores;
using Management.Application.Interfaces.App;
using Management.Application.Interfaces.ViewModels;
using Management.Domain.Services;
using Management.Domain.Enums;
using Management.Presentation.Services;
using Management.Presentation.Extensions;
using Management.Presentation.Services.State;
using Management.Presentation.Services.Localization;
using Management.Presentation.ViewModels.Base;
using Management.Presentation.ViewModels.Auth;
using Management.Infrastructure.Services;
using Management.Infrastructure.Data;
using ISessionStorageService = Management.Domain.Services.ISessionStorageService;

namespace Management.Presentation.ViewModels
{
    public class LoginViewModel : FacilityAwareViewModelBase, IAsyncViewModel, IParameterReceiver
    {
        private readonly IAuthenticationService _authService;
        private readonly INavigationService _navigationService;
        private readonly ISessionStorageService _sessionStorage;
        private readonly IOnboardingStateStore _onboardingState;
        private readonly IOnboardingService _onboardingService;
        private readonly SessionManager _sessionManager;
        private readonly ISyncService _syncService;
        private readonly AppDbContext _dbContext;

        private string _email = string.Empty;
        public string Email
        {
            get => _email;
            set => SetProperty(ref _email, value?.Trim() ?? string.Empty);
        }

        public string? ExpansionMessage => _onboardingState.ExpansionMessage;
        public bool IsExpansionMode => !string.IsNullOrEmpty(ExpansionMessage);

        private bool _rememberMe;
        public bool RememberMe
        {
            get => _rememberMe;
            set => SetProperty(ref _rememberMe, value);
        }

        private bool _isInitializingApp;
        public bool IsInitializingApp
        {
            get => _isInitializingApp;
            set
            {
                SetProperty(ref _isInitializingApp, value);
                LoginCommand.NotifyCanExecuteChanged();
            }
        }
        
        public void SetInitializingState(bool value)
        {
            IsInitializingApp = value;
            LoginCommand.NotifyCanExecuteChanged();
        }

        private string _appInitializationStatus = string.Empty;
        public string AppInitializationStatus
        {
            get => _appInitializationStatus;
            set => SetProperty(ref _appInitializationStatus, value);
        }

        private FacilityTypeOption? _selectedFacility;
        public FacilityTypeOption? SelectedFacility
        {
            get => _selectedFacility;
            set 
            {
                SetProperty(ref _selectedFacility, value);
                LoginCommand.NotifyCanExecuteChanged();
            }
        }

        public AsyncRelayCommand<object> LoginCommand { get; }
        public ICommand ForgotPasswordCommand { get; }
        public ICommand ChangeFacilityCommand { get; }

        public LoginViewModel(
            IAuthenticationService authService,
            INavigationService navigationService,
            ISessionStorageService sessionStorage,
            Management.Domain.Services.IDialogService dialogService,
            IOnboardingStateStore onboardingState,
            IOnboardingService onboardingService,
            IToastService toastService,
            SessionManager sessionManager,
            ISyncService syncService,
            Management.Domain.Services.IFacilityContextService facilityContext,
            AppDbContext dbContext,
            ITerminologyService terminologyService,
            ILocalizationService localizationService,
            ILogger<LoginViewModel> logger,
            IDiagnosticService diagnosticService)
            : base(terminologyService, facilityContext, logger, diagnosticService, toastService, localizationService, dialogService)
        {
            _authService = authService;
            _navigationService = navigationService;
            _sessionStorage = sessionStorage;
            _onboardingState = onboardingState;
            _onboardingService = onboardingService;
            _sessionManager = sessionManager;
            _syncService = syncService;
            _dbContext = dbContext;

            _isInitializingApp = false;

            LoginCommand = new AsyncRelayCommand<object>(ExecuteLogin, CanExecuteLogin);
            ForgotPasswordCommand = new RelayCommand(ExecuteForgotPassword);
            ChangeFacilityCommand = new AsyncRelayCommand(() => _navigationService.NavigateToSplashAsync());
        }

        public Task SetParameterAsync(object parameter)
        {
            if (parameter is FacilityTypeOption option)
            {
                SelectedFacility = option;
                Serilog.Log.Information("[Login] Context Lock applied: {FacilityName} ({FacilityId})", option.Name, option.Id);
                
                // Immediately update context to ensure correct tenant scoping for the auth request
                if (option.Id != Guid.Empty)
                {
                    _facilityContext.UpdateFacilityId(option.Type, option.Id);
                }
            }
            return Task.CompletedTask;
        }

        private bool CanExecuteLogin(object? parameter)
        {
            return !IsBusy && !IsInitializingApp && !string.IsNullOrWhiteSpace(Email) && SelectedFacility != null;
        }

        private async Task ExecuteLogin(object? parameter)
        {
            if (parameter is not PasswordBox passwordBox || SelectedFacility == null) return;

            var password = passwordBox.Password;
            if (string.IsNullOrWhiteSpace(password))
            {
                ErrorMessage = _localizationService?.GetString("Strings.Auth.Error.PasswordRequired") ?? "Please enter your password.";
                HasError = true;
                return;
            }

            IsBusy = true;
            ErrorMessage = null;
            HasError = false;

            try
            {
                await _sessionStorage.ClearSessionAsync();

                Guid? facilityContextId = SelectedFacility.Id == Guid.Empty ? null : SelectedFacility.Id;
                var result = await _authService.LoginAsync(Email, password, facilityContextId);

                if (result.IsSuccess)
                {
                    var loggedInFacilityId = result.Value.FacilityId;
                    bool isFacilityMatch = loggedInFacilityId == SelectedFacility.Id;
                    bool isOwner = result.Value.Role == Management.Domain.Enums.StaffRole.Owner;

                    if (!isFacilityMatch && !isOwner)
                    {
                        Serilog.Log.Warning("[Security] Login blocked. FacilityMismatch. StaffFacility={StaffFacility} LoginChoice={LoginChoice}",
                            loggedInFacilityId, SelectedFacility.Id);

                        ErrorMessage = _localizationService?.GetString("Strings.Auth.Error.FacilityMismatch") ?? "Access denied: Account not authorized for this facility.";
                        HasError = true;
                        IsBusy = false;
                        await _authService.LogoutAsync();
                        return;
                    }

                    _sessionManager.SetUser(result.Value);

                    if (SelectedFacility.Id != Guid.Empty)
                    {
                        _facilityContext.UpdateFacilityId(SelectedFacility.Type, SelectedFacility.Id);
                    }

                    await RefreshFacilityDiscoveryAsync();
                    _facilityContext.SetFacility(SelectedFacility.Type);

                    if (IsExpansionMode && _onboardingState.TargetTenantId.HasValue)
                    {
                        // Expansion logic remains...
                        _onboardingState.Clear();
                    }

                    if (result.Value.TenantId == Guid.Empty)
                    {
                        await _navigationService.NavigateToAsync<OnboardingOwnerViewModel>(Email);
                        return;
                    }

                    ((App)System.Windows.Application.Current).LaunchMainWindow();

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _syncService.PullChangesAsync(CancellationToken.None);
                        }
                        catch (Exception ex) { Serilog.Log.Warning(ex, "[Login] Background sync failed."); }
                    });
                }
                else
                {
                    ErrorMessage = FormatAuthError(result.Error.Message);
                    HasError = true;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                HasError = true;
            }
            finally
            {
                IsBusy = false;
                LoginCommand.NotifyCanExecuteChanged();
            }
        }

        private async Task RefreshFacilityDiscoveryAsync()
        {
            try
            {
                var discoveryResult = await _onboardingService.GetLicensedFacilitiesAsync();
                if (discoveryResult.IsSuccess && discoveryResult.Value.Any())
                {
                    var map = discoveryResult.Value
                        .GroupBy(f => (FacilityType)f.Type)
                        .ToDictionary(g => g.Key, g => g.First().Id);
                    _facilityContext.UpdateFacilities(map);
                }
            }
            catch (Exception ex) { Serilog.Log.Warning(ex, "[Login] Post-auth discovery failed."); }
        }

        private string FormatAuthError(string rawError)
        {
            if (string.IsNullOrEmpty(rawError)) return "Unknown error";
            if (rawError.Contains("invalid_credentials")) return "Invalid email or password.";
            return rawError;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task OnNavigatedToAsync(object? parameter)
        {
            if (parameter != null) await SetParameterAsync(parameter);
        }

        private void ExecuteForgotPassword()
        {
            _dialogService.ShowAlertAsync("Forgot Password", "Contact administrator to reset.");
        }
    }
}
