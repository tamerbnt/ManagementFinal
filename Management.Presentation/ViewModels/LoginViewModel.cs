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
            set
            {
                if (SetProperty(ref _email, value?.Trim() ?? string.Empty))
                {
                    LoginCommand.NotifyCanExecuteChanged();
                }
            }
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
        public ICommand BackToAccountSetupCommand { get; }


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

            // Pre-seed SelectedFacility from persisted context so Login button and UI are immediately active
            var defaultType = _facilityContext.CurrentFacility != FacilityType.General
                ? _facilityContext.CurrentFacility
                : (_facilityContext.ConfiguredFacility != FacilityType.General ? _facilityContext.ConfiguredFacility : FacilityType.Gym);
            var defaultId = _facilityContext.GetFacilityId(defaultType);
            _selectedFacility = FacilityTypeOption.Create(defaultType, defaultId);

            LoginCommand = new AsyncRelayCommand<object>(ExecuteLogin, CanExecuteLogin);
            BackToAccountSetupCommand = new AsyncRelayCommand(ExecuteBackToAccountSetupAsync);
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
                _facilityContext.PersistFacilityChoice(option.Type);
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
                var minDelayTask = Task.Delay(1500);

                await _sessionStorage.ClearSessionAsync();

                Guid? facilityContextId = SelectedFacility.Id == Guid.Empty ? null : SelectedFacility.Id;
                var result = await _authService.LoginAsync(Email, password, facilityContextId);

                await minDelayTask;

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
                    else if (loggedInFacilityId != Guid.Empty)
                    {
                        SelectedFacility.Id = loggedInFacilityId;
                        _facilityContext.UpdateFacilityId(SelectedFacility.Type, loggedInFacilityId);
                    }

                    await RefreshFacilityDiscoveryAsync();
                    _facilityContext.PersistFacilityChoice(SelectedFacility.Type);
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

                    // FIX Bug #4: Capture IsRemoteMode BEFORE LaunchMainWindow fires ResetApplicationState().
                    // LaunchMainWindowAsync → ResetApplicationState() → SessionManager.ResetState()
                    // sets IsRemoteMode = false, erasing the flag set by SplashOnboardingViewModel.
                    // We re-apply it immediately after the launch so ViewModels see the correct value.
                    bool wasRemoteMode = _sessionManager.IsRemoteMode;

                    ((App)System.Windows.Application.Current).LaunchMainWindow();

                    if (wasRemoteMode)
                    {
                        _sessionManager.IsRemoteMode = true;
                        Serilog.Log.Information("[Login] Remote Mode preserved after main window launch.");
                    }

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

        public async Task InitializeAsync()
        {
            await EnsureFacilitySelectedAsync();
        }

        public async Task OnNavigatedToAsync(object? parameter)
        {
            if (parameter != null)
            {
                await SetParameterAsync(parameter);
            }
            else
            {
                await EnsureFacilitySelectedAsync();
            }
        }

        private async Task EnsureFacilitySelectedAsync()
        {
            var facilityType = _facilityContext.CurrentFacility;
            if (facilityType == FacilityType.General)
            {
                facilityType = _facilityContext.ConfiguredFacility;
            }

            Guid facilityId = _facilityContext.GetFacilityId(facilityType);
            string? facilityName = null;

            try
            {
                var localFacilities = await _dbContext.Facilities
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Where(f => !f.IsDeleted)
                    .ToListAsync();

                if (localFacilities.Any())
                {
                    var matched = facilityType != FacilityType.General
                        ? localFacilities.FirstOrDefault(f => f.Type == facilityType)
                        : null;

                    matched ??= localFacilities
                        .OrderByDescending(f => f.LastModifiedAt ?? f.CreatedAt)
                        .FirstOrDefault();

                    if (matched != null)
                    {
                        facilityType = matched.Type;
                        facilityId = matched.Id;
                        facilityName = matched.Name;
                    }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "[Login] Failed to query local facilities in EnsureFacilitySelectedAsync.");
            }

            if (facilityType == FacilityType.General)
            {
                facilityType = FacilityType.Gym;
            }

            facilityType = facilityType switch
            {
                FacilityType.MembershipAndSession => FacilityType.Gym,
                FacilityType.AppointmentAndService => FacilityType.Salon,
                FacilityType.PosAndInventory => FacilityType.Restaurant,
                _ => facilityType
            };

            if (SelectedFacility == null || SelectedFacility.Type != facilityType || (SelectedFacility.Id == Guid.Empty && facilityId != Guid.Empty))
            {
                SelectedFacility = FacilityTypeOption.Create(facilityType, facilityId, facilityName);
                Serilog.Log.Information("[Login] Facility context ensured: {FacilityName} ({FacilityId}, Type={Type})",
                    SelectedFacility.Name, SelectedFacility.Id, SelectedFacility.Type);
            }

            if (facilityId != Guid.Empty)
            {
                _facilityContext.UpdateFacilityId(facilityType, facilityId);
            }
            _facilityContext.PersistFacilityChoice(facilityType);
        }

        private async Task ExecuteBackToAccountSetupAsync()
        {
            await ExecuteSafeAsync(async () =>
            {
                Serilog.Log.Information("[Login] User navigating back to Account Setup");
                await _navigationService.NavigateToAsync<OnboardingOwnerViewModel>(Email);
            });
        }

    }
}
