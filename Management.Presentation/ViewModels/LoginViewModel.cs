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
using Management.Infrastructure.Services;
using Management.Infrastructure.Data;
using ISessionStorageService = Management.Domain.Services.ISessionStorageService;
using Management.Domain.Enums;
using Management.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Management.Presentation.ViewModels
{
    public class LoginViewModel : FacilityAwareViewModelBase, Management.Application.Interfaces.ViewModels.IAsyncViewModel
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
        
        /// <summary>
        /// Explicitly sets the initialization state and notifies the login command.
        /// This is used by App.xaml.cs to block/unblock the UI during startup.
        /// </summary>
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


        // Holds both the real DB Guid (for LoginAsync) and the FacilityType enum (for SetFacility)
        public class FacilityTypeOption
        {
            public Guid Id { get; set; }
            public FacilityType Type { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string IconKey { get; set; } = string.Empty;
            public string GradientStart { get; set; } = "#0EA5E9";
            public string GradientEnd { get; set; } = "#2563EB";
        }

        private ObservableCollection<FacilityTypeOption> _availableFacilities = new();
        public ObservableCollection<FacilityTypeOption> AvailableFacilities
        {
            get => _availableFacilities;
            set => SetProperty(ref _availableFacilities, value);
        }

        private FacilityTypeOption? _selectedFacility;
        public FacilityTypeOption? SelectedFacility
        {
            get => _selectedFacility;
            set => SetProperty(ref _selectedFacility, value);
        }

        public AsyncRelayCommand<object> LoginCommand { get; }
        public ICommand ForgotPasswordCommand { get; }
        public ICommand SelectFacilityCommand { get; }

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

            _isInitializingApp = false; // Default to ready

            LoginCommand = new AsyncRelayCommand<object>(ExecuteLogin, CanExecuteLogin);
            ForgotPasswordCommand = new RelayCommand(ExecuteForgotPassword);
            SelectFacilityCommand = new RelayCommand<FacilityTypeOption>(f => SelectedFacility = f);

            // NOTE: LoadFacilitiesAsync is NOT fired here — it requires network and runs too early.
            // OnNavigatedToAsync calls it after the navigation lifecycle is ready.
        }

        private bool CanExecuteLogin(object? parameter)
        {
            return !IsBusy && !IsInitializingApp && !string.IsNullOrWhiteSpace(Email);
        }

        private async Task ExecuteLogin(object? parameter)
        {
            if (parameter is not PasswordBox passwordBox) return;

            var password = passwordBox.Password;
            if (string.IsNullOrWhiteSpace(password))
            {
                ErrorMessage = _localizationService?.GetString("Strings.Auth.Error.PasswordRequired") ?? "Please enter your password.";
                HasError = true;
                return;
            }

            // Validate facility selection
            if (SelectedFacility == null)
            {
                ErrorMessage = _localizationService?.GetString("Strings.Auth.Error.SelectFacilityToContinue") ?? "Please select a facility to continue.";
                HasError = true;
                _toastService?.ShowWarning(ErrorMessage, _localizationService?.GetString("Strings.Auth.Error.SelectFacility") ?? "Select Facility");
                return;
            }

            IsBusy = true;
            ErrorMessage = null;
            HasError = false;

            try
            {
                // Clear any existing session first
                await _sessionStorage.ClearSessionAsync();

                // FIX: If using placeholder ID (e.g. from LoadStaticDefaults), pass NULL to LoginAsync
                // to authenticate first, then let AuthService derive the correct facility.
                Guid? facilityContextId = SelectedFacility.Id;
                if (SelectedFacility.Id == Guid.Empty ||
                    (AvailableFacilities.Count == 3 && AvailableFacilities.All(f => f.Id == Guid.Empty)))
                {
                    Serilog.Log.Information("[Login] Placeholder facility selected. Authenticating without facility context...");
                    facilityContextId = null;
                }

                var result = await _authService.LoginAsync(Email, password, facilityContextId);

                if (result.IsSuccess)
                {
                    _sessionManager.SetUser(result.Value);

                    // Change 1 — GUARD: Only update if we have a real GUID.
                    // Do NOT overwrite startup-discovered values with Guid.Empty from static defaults.
                    if (SelectedFacility.Id != Guid.Empty)
                    {
                        _facilityContext.UpdateFacilityId(SelectedFacility.Type, SelectedFacility.Id);
                        Serilog.Log.Information("[Login] UpdateFacilityId: {Type} = {Id}", SelectedFacility.Type, SelectedFacility.Id);
                    }
                    else
                    {
                        Serilog.Log.Information("[Login] Skipping UpdateFacilityId — SelectedFacility.Id is Guid.Empty. Keeping startup-discovered GUID.");
                    }

                    // Change 2 — Re-run discovery now that a Supabase session/connection is live.
                    // If it returns real GUIDs they overwrite and improve on the local DB values.
                    await RefreshFacilityDiscoveryAsync();

                    _facilityContext.SetFacility(SelectedFacility.Type);


                    // Schema 3: Expansion Flow Logic
                    if (IsExpansionMode && _onboardingState.TargetTenantId.HasValue)
                    {
                        var tenantId = _onboardingState.TargetTenantId.Value;

                        var count = await _onboardingService.GetDeviceCountAsync(tenantId);
                        if (count >= 3)
                        {
                            await _authService.LogoutAsync();
                            ErrorMessage = _localizationService?.GetString("Strings.Auth.Error.DeviceLimitReached") ?? "Device Limit Reached. This workspace is already active on 3 machines.";
                            HasError = true;
                            await _dialogService.ShowAlertAsync(_localizationService?.GetString("Strings.Auth.Error.RegistrationFailed") ?? "Registration Failed", ErrorMessage);
                            return;
                        }

                        var regResult = await _onboardingService.RegisterCurrentDeviceAsync(tenantId, $"{Environment.MachineName} (Expansion)", _onboardingState.LicenseKey);
                        if (regResult.IsFailure)
                        {
                            await _authService.LogoutAsync();
                            ErrorMessage = $"Failed to link this device: {regResult.Error.Message}";
                            HasError = true;
                            return;
                        }

                        _onboardingState.Clear();
                    }

                    // Phase 5: Check Onboarding Status
                    if (result.Value.TenantId == Guid.Empty)
                    {
                        Serilog.Log.Information($"[LoginViewModel] User {Email} is verified but missing TenantId. Redirecting to Business Finalization.");
                        await _navigationService.NavigateToAsync<OnboardingOwnerViewModel>(Email);
                        return;
                    }

                    // Handoff: Switch from AuthWindow to MainWindow.
                    // NOTE: We do NOT call NavigateToAsync(0) here — that method reads
                    // _sessionManager.CurrentFacility which may still be the config default (e.g. Restaurant).
                    // MainViewModel.InitializeInitialView() handles the first navigation using the
                    // correct _facilityContext source after SetFacility() has already been called above.
                    ((App)System.Windows.Application.Current).LaunchMainWindow();

                    // Fire-and-forget: pull fresh data in background AFTER the window is shown.
                    // Using Task.Run to move off the UI thread in case PullChangesAsync has sync init.
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            Serilog.Log.Information("[Login] Background sync starting after window launch...");
                            await _syncService.PullChangesAsync(CancellationToken.None);
                            Serilog.Log.Information("[Login] Background sync completed.");
                        }
                        catch (Exception syncEx)
                        {
                            Serilog.Log.Warning(syncEx, "[Login] Background sync failed, dashboard may show stale data.");
                        }
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
                ErrorMessage = string.Format(_localizationService?.GetString("Strings.Auth.Error.UnexpectedError") ?? "An unexpected error occurred: {0}", ex.Message);
                HasError = true;
            }
            finally
            {
                IsBusy = false;
                LoginCommand.NotifyCanExecuteChanged();
            }
        }

        private string FormatAuthError(string rawError)
        {
            if (string.IsNullOrEmpty(rawError)) return _localizationService?.GetString("Strings.Auth.Error.UnknownError") ?? "An unknown error occurred.";

            if (rawError.Contains("\"msg\":") || rawError.Contains("\"error\":"))
            {
                try
                {
                    var jsonStr = rawError;
                    if (jsonStr.Contains("Login failed: ")) jsonStr = jsonStr.Replace("Login failed: ", "");

                    var json = Newtonsoft.Json.Linq.JObject.Parse(jsonStr);
                    var msg = json.Value<string>("msg") ?? json.Value<string>("error_description") ?? json.Value<string>("error");

                    if (!string.IsNullOrEmpty(msg))
                    {
                        if (msg.Contains("invalid_credentials") || msg.Contains("Invalid login credentials")) return _localizationService?.GetString("Strings.Auth.Error.InvalidCredentials") ?? "Invalid email or password.";
                        if (msg.Contains("Email not confirmed") || msg.Contains("email_not_confirmed"))
                        {
                            return _localizationService?.GetString("Strings.Auth.Error.EmailNotConfirmed") ?? "Your email address has not been confirmed yet. If you have logged in on this PC before, you can use your PIN code as the password.";
                        }
                        return msg;
                    }
                }
                catch { /* Fallback to raw */ }
            }

            if (rawError.Contains("invalid_credentials")) return _localizationService?.GetString("Strings.Auth.Error.InvalidCredentials") ?? "Invalid email or password.";
            if (rawError.Contains("email_not_confirmed") || rawError.Contains("Email not confirmed"))
            {
                return _localizationService?.GetString("Strings.Auth.Error.ActivationRequired") ?? "Account Activation Required. If you've accessed this PC before, try your PIN code; otherwise, please confirm your email in the dashboard.";
            }
            if (rawError.Contains("400")) return _localizationService?.GetString("Strings.Auth.Error.InvalidAttempt") ?? "Invalid login attempt. Please check your credentials.";

            return rawError;
        }

        public async Task InitializeAsync()
        {
            // Change 3 — Load facility UI from local SQLite first (fast, no network).
            // This guarantees real names + GUIDs even offline and before Supabase connects.
            await LoadFacilitiesFromLocalAsync();
        }

        public async Task OnNavigatedToAsync(object? parameter)
        {
            await InitializeAsync();
        }

        private void ExecuteForgotPassword()
        {
            _dialogService.ShowAlertAsync(_localizationService?.GetString("Strings.Auth.Title.ForgotPassword") ?? "Forgot Password", _localizationService?.GetString("Strings.Auth.Message.ForgotPassword") ?? "Please contact your administrator to reset your password.");
        }

        /// <summary>
        /// Change 3: Loads facility options from the local SQLite database.
        /// Uses IgnoreQueryFilters() because CurrentFacilityId may still be Guid.Empty at this point.
        /// Falls back to static defaults only if the local DB is truly empty.
        /// </summary>
        private async Task LoadFacilitiesFromLocalAsync()
        {
            try
            {
                Serilog.Log.Information("[Login] Loading facility options from local SQLite...");
                var localFacilities = await _dbContext.Facilities
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Where(f => !f.IsDeleted)
                    .ToListAsync();

                if (localFacilities.Count > 0)
                {
                    // Deduplicate by type, keep first record per type.
                    var options = localFacilities
                        .GroupBy(f => f.Type)
                        .Select(g => g.First())
                        .Select(f => new FacilityTypeOption
                        {
                            Id = f.Id,
                            Type = f.Type,
                            Name = f.Name,
                            Description = GetDescriptionForType((int)f.Type),
                            IconKey = GetIconForType((int)f.Type),
                            GradientStart = GetGradientStartForType((int)f.Type),
                            GradientEnd = GetGradientEndForType((int)f.Type)
                        })
                        .ToList();

                    AvailableFacilities = new ObservableCollection<FacilityTypeOption>(options);
                    Serilog.Log.Information("[Login] Loaded {Count} facility options from local DB.", options.Count);
                }
                else
                {
                    Serilog.Log.Warning("[Login] Local DB has no facilities. Falling back to static defaults.");
                    LoadStaticDefaults();
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[Login] Local facility load failed. Falling back to static defaults.");
                LoadStaticDefaults();
            }

            if (AvailableFacilities.Count > 0 && SelectedFacility == null)
            {
                SelectedFacility = AvailableFacilities[0];
            }
        }

        /// <summary>
        /// Change 2: Re-runs the Supabase RPC discovery after a valid session is established.
        /// If it returns real GUIDs they update the context; if it fails, the local DB values remain.
        /// </summary>
        private async Task RefreshFacilityDiscoveryAsync()
        {
            try
            {
                Serilog.Log.Information("[Login] Post-auth: running Supabase facility discovery...");
                var discoveryResult = await _onboardingService.GetLicensedFacilitiesAsync();

                if (discoveryResult.IsSuccess && discoveryResult.Value.Any())
                {
                    var map = discoveryResult.Value
                        .GroupBy(f => (FacilityType)f.Type)
                        .ToDictionary(g => g.Key, g => g.First().Id);
                    _facilityContext.UpdateFacilities(map);
                    Serilog.Log.Information("[Login] Post-auth discovery updated {Count} facility GUIDs from Supabase.", map.Count);
                }
                else
                {
                    Serilog.Log.Warning("[Login] Post-auth discovery returned no results. Retaining local DB GUIDs.");
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "[Login] Post-auth discovery failed. Retaining local DB GUIDs.");
            }
        }

        private string GetDescriptionForType(int type) => type switch
        {
            (int)FacilityType.Salon => "Hair salons, Spas, and Wellness centers.",
            (int)FacilityType.Restaurant => "Cafes, Fine dining, and Quick service restaurants.",
            _ => "Fitness centers, CrossFit boxes, and Personal Training studios."
        };

        private string GetIconForType(int type) => type switch
        {
            (int)FacilityType.Gym => "Icon.Dumbbell",
            (int)FacilityType.Salon => "Icon.Scissors",
            (int)FacilityType.Restaurant => "Icon.Utensils",
            _ => "Icon.Dumbbell"
        };

        private string GetGradientStartForType(int type) => type switch
        {
            (int)FacilityType.Salon => "#EC4899",
            (int)FacilityType.Restaurant => "#F59E0B",
            _ => "#0EA5E9"
        };

        private string GetGradientEndForType(int type) => type switch
        {
            (int)FacilityType.Salon => "#D946EF",
            (int)FacilityType.Restaurant => "#EF4444",
            _ => "#2563EB"
        };

        private void LoadStaticDefaults()
        {
            AvailableFacilities = new ObservableCollection<FacilityTypeOption>
            {
                new FacilityTypeOption
                {
                    Id = Guid.Empty,
                    Type = FacilityType.Gym,
                    Name = _localizationService?.GetString("Strings.Auth.Facility.Gym") ?? "Gym",
                    Description = _localizationService?.GetString("Strings.Auth.Facility.GymDesc") ?? "Fitness centers, CrossFit boxes, and Personal Training studios.",
                    IconKey = GetIconForType((int)FacilityType.Gym),
                    GradientStart = GetGradientStartForType((int)FacilityType.Gym),
                    GradientEnd = GetGradientEndForType((int)FacilityType.Gym)
                },
                new FacilityTypeOption
                {
                    Id = Guid.Empty,
                    Type = FacilityType.Salon,
                    Name = _localizationService?.GetString("Strings.Auth.Facility.Salon") ?? "Salon",
                    Description = _localizationService?.GetString("Strings.Auth.Facility.SalonDesc") ?? "Hair salons, Spas, and Wellness centers.",
                    IconKey = GetIconForType((int)FacilityType.Salon),
                    GradientStart = GetGradientStartForType((int)FacilityType.Salon),
                    GradientEnd = GetGradientEndForType((int)FacilityType.Salon)
                },
                new FacilityTypeOption
                {
                    Id = Guid.Empty,
                    Type = FacilityType.Restaurant,
                    Name = _localizationService?.GetString("Strings.Auth.Facility.Restaurant") ?? "Restaurant",
                    Description = _localizationService?.GetString("Strings.Auth.Facility.RestaurantDesc") ?? "Cafes, Fine dining, and Quick service restaurants.",
                    IconKey = GetIconForType((int)FacilityType.Restaurant),
                    GradientStart = GetGradientStartForType((int)FacilityType.Restaurant),
                    GradientEnd = GetGradientEndForType((int)FacilityType.Restaurant)
                }
            };

            Serilog.Log.Warning("[Login] No facilities discovered. Showing static defaults (3 options with placeholder IDs).");
        }
    }
}
