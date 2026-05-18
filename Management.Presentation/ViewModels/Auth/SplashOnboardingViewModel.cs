using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Management.Application.Interfaces.App;
using Management.Application.Interfaces.ViewModels;
using Management.Domain.Enums;
using Management.Infrastructure.Data;
using Management.Presentation.Services;
using Management.Presentation.ViewModels.Base;
using Management.Application.DTOs;
using Management.Domain.Primitives;

namespace Management.Presentation.ViewModels.Auth
{
    public class SplashOnboardingViewModel : FacilityAwareViewModelBase, IAsyncViewModel
    {
        private readonly INavigationService _navigationService;
        private readonly AppDbContext _dbContext;
        private readonly IAppInitializationTracker _initTracker;
        private readonly IDispatcher _dispatcher;
        private readonly Management.Application.Services.IAuthenticationService _authService;
        private readonly Management.Presentation.Services.State.SessionManager _sessionManager;

        private bool _isLoading = true;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        // FIX: Separate flags per button so only the clicked button shows its animation.
        // Both buttons previously shared IsBusy, making them animate simultaneously.
        private bool _isEnteringWorkspace;
        public bool IsEnteringWorkspace
        {
            get => _isEnteringWorkspace;
            set => SetProperty(ref _isEnteringWorkspace, value);
        }

        private bool _isEnteringRemote;
        public bool IsEnteringRemote
        {
            get => _isEnteringRemote;
            set => SetProperty(ref _isEnteringRemote, value);
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
            set 
            {
                if (SetProperty(ref _selectedFacility, value))
                {
                    foreach (var option in AvailableFacilities)
                    {
                        option.IsSelected = (option == value);
                    }
                    EnterWorkspaceCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public IAppInitializationTracker InitTracker => _initTracker;

        public AsyncRelayCommand EnterWorkspaceCommand { get; }
        public AsyncRelayCommand EnterRemoteWorkspaceCommand { get; }
        public ICommand SelectFacilityCommand { get; }

        public SplashOnboardingViewModel(
            INavigationService navigationService,
            AppDbContext dbContext,
            IAppInitializationTracker initTracker,
            Management.Presentation.Services.Localization.ILocalizationService localizationService,
            Management.Domain.Services.ITerminologyService terminologyService,
            Management.Domain.Services.IFacilityContextService facilityContext,
            Management.Domain.Services.IDialogService dialogService,
            Management.Application.Interfaces.App.IToastService toastService,
            ILogger<SplashOnboardingViewModel> logger,
            Management.Application.Services.IDiagnosticService diagnosticService,
            IDispatcher dispatcher,
            Management.Application.Services.IAuthenticationService authService,
            Management.Presentation.Services.State.SessionManager sessionManager)
            : base(terminologyService, facilityContext, logger, diagnosticService, toastService, localizationService, dialogService)
        {
            _navigationService = navigationService;
            _dbContext = dbContext;
            _initTracker = initTracker;
            _dispatcher = dispatcher;
            _authService = authService;
            _sessionManager = sessionManager;

            EnterWorkspaceCommand = new AsyncRelayCommand(ExecuteEnterWorkspace, CanExecuteEnterWorkspace);
            EnterRemoteWorkspaceCommand = new AsyncRelayCommand(ExecuteEnterRemoteWorkspace, CanExecuteEnterWorkspace);
            SelectFacilityCommand = new RelayCommand<FacilityTypeOption>(f => SelectedFacility = f);

            // FIX: Force data execution instantly on instantiation.
            // Bypasses the Navigation pipeline which is intentionally skipped natively by App.xaml.cs startup routing.
            _ = LoadFacilitiesFromLocalAsync();
        }

        private bool CanExecuteEnterWorkspace()
        {
            return SelectedFacility != null && _initTracker.IsComplete;
        }

        private async Task ExecuteEnterWorkspace()
        {
            if (SelectedFacility == null) return;
            
            IsEnteringWorkspace = true;
            try
            {
                var delayTask = Task.Delay(1500);

                if (SelectedFacility.Type == FacilityType.Restaurant)
                {
                    await delayTask; // Preserve the 1.5s visual feedback
                    if (_dialogService != null)
                    {
                        await _dialogService.ShowAlertAsync(
                            "Coming Soon",
                            "Restaurant features are currently under construction. Please check back in a future update!",
                            "Back to Login"
                        );
                    }
                    return;
                }

                // Persist the choice
                _facilityContext.SetFacility(SelectedFacility.Type);
                
                // Check for valid existing session (Auto-Login)
                // CRITICAL: We skip auto-login if the user has explicitly logged out during this session.
                var currentUserResult = _authService.IsLogoutActive 
                    ? Result.Failure<StaffDto>(new Error("Auth.ForcedLogin", "Forcing login after logout."))
                    : await _authService.GetCurrentUserAsync();

                await delayTask; // Ensure at least 1500ms have passed

                if (currentUserResult.IsSuccess && currentUserResult.Value != null)
                {
                    var user = currentUserResult.Value;
                    // If the session matches the selected facility (or user is Owner), skip login
                    if (user.Role == Management.Domain.Enums.StaffRole.Owner || user.FacilityId == SelectedFacility.Id)
                    {
                        Serilog.Log.Information("[Splash] Valid session found for {Email}. Bypassing login.", user.Email);
                        _sessionManager.SetUser(user);
                        
                        if (System.Windows.Application.Current is Management.Presentation.App app)
                        {
                            await app.LaunchMainWindowAsync();
                        }
                        return;
                    }
                    else 
                    {
                        // Optionally alert the user here or just let them fall through to login 
                        Serilog.Log.Information("[Splash] Valid session found but facility mismatched. Falling through to login.");
                    }
                }
                
                // No valid session or facility mismatch: Navigate to Login, passing the selected facility as context
                await _navigationService.NavigateToAsync<LoginViewModel>(SelectedFacility);
            }
            finally
            {
                IsEnteringWorkspace = false;
            }
        }

        private async Task ExecuteEnterRemoteWorkspace()
        {
            if (SelectedFacility == null) return;
            
            IsEnteringRemote = true;
            try
            {
                var delayTask = Task.Delay(1500);
                await delayTask; // Ensure at least 1500ms have passed

                if (_dialogService != null)
                {
                    await _dialogService.ShowAlertAsync(
                        "Coming Soon",
                        "Remote View features are currently under construction. Please check back in a future update!",
                        "Back to Login"
                    );
                }
            }
            finally
            {
                IsEnteringRemote = false;
            }
        }

        public async Task InitializeAsync()
        {
            await LoadFacilitiesFromLocalAsync();
        }

        private async Task LoadFacilitiesFromLocalAsync()
        {
            IsLoading = true;
            try
            {
                var localFacilities = await _dbContext.Facilities
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Where(f => !f.IsDeleted)
                    .ToListAsync();

                List<FacilityTypeOption> options;

                if (localFacilities.Count > 0)
                {
                    // FIX 3: Deterministic selection — per type, pick the most recently updated facility
                    // This guarantees the same facility is selected every time, not a random DBSet order.
                    options = localFacilities
                        .GroupBy(f => f.Type)
                        .Select(g => g.OrderByDescending(f => f.LastModifiedAt ?? f.CreatedAt).First())
                        .Select(f => new FacilityTypeOption
                        {
                            Id = f.Id,
                            Type = f.Type,
                            Name = f.Name,
                            Description = GetDescription(f.Type),
                            GradientStart = GetGradient(f.Type, true),
                            GradientEnd = GetGradient(f.Type, false),
                            IconKey = GetIcon(f.Type)
                        })
                        .ToList();

                    Serilog.Log.Information("[Splash] Loaded {Count} facility type(s) from local SQLite", options.Count);
                }
                else
                {
                    // No local data yet — show discoverable fallback cards
                    // This happens on first run before sync has had time to populate the DB
                    Serilog.Log.Warning("[Splash] No facilities found in local SQLite — showing fallback options");
                    options = new List<FacilityTypeOption>
                    {
                        new FacilityTypeOption { Id = Guid.Empty, Type = FacilityType.Gym, Name = "Titan Gym", Description = GetDescription(FacilityType.Gym), GradientStart = GetGradient(FacilityType.Gym, true), GradientEnd = GetGradient(FacilityType.Gym, false), IconKey = GetIcon(FacilityType.Gym) },
                        new FacilityTypeOption { Id = Guid.Empty, Type = FacilityType.Salon, Name = "Titan Salon", Description = GetDescription(FacilityType.Salon), GradientStart = GetGradient(FacilityType.Salon, true), GradientEnd = GetGradient(FacilityType.Salon, false), IconKey = GetIcon(FacilityType.Salon) },
                        new FacilityTypeOption { Id = Guid.Empty, Type = FacilityType.Restaurant, Name = "Titan Restaurant", Description = GetDescription(FacilityType.Restaurant), GradientStart = GetGradient(FacilityType.Restaurant, true), GradientEnd = GetGradient(FacilityType.Restaurant, false), IconKey = GetIcon(FacilityType.Restaurant) }
                    };
                }

                _dispatcher.Invoke(() =>
                {
                    AvailableFacilities.Clear();
                    foreach (var opt in options)
                    {
                        AvailableFacilities.Add(opt);
                    }

                    // Pre-select Gym facility if available in discovery options, otherwise fallback to most recently updated real facility
                    var defaultGym = AvailableFacilities.FirstOrDefault(f => f.Type == FacilityType.Gym);
                    if (defaultGym != null)
                    {
                        SelectedFacility = defaultGym;
                    }
                    else
                    {
                        var realFacilities = localFacilities
                            .OrderByDescending(f => f.LastModifiedAt ?? f.CreatedAt)
                            .FirstOrDefault();

                        if (realFacilities != null)
                        {
                            SelectedFacility = AvailableFacilities.FirstOrDefault(f => f.Id == realFacilities.Id)
                                ?? AvailableFacilities.FirstOrDefault();
                        }
                        else
                        {
                            // Fallback mode — no real facility ID, don't pre-select
                            SelectedFacility = null;
                        }
                    }
                    
                    IsLoading = false;
                });
            }
            catch (Exception ex)
            {
                IsLoading = false;
                _logger.LogError(ex, "[Splash] Failed to load facilities for splash discovery.");
            }
        }

        private string GetDescription(FacilityType type)
        {
            return type switch
            {
                FacilityType.Gym => "Fitness & Wellness Analytics",
                FacilityType.Salon => "Beauty & Spa Operations",
                FacilityType.Restaurant => "Fine Dining Control",
                _ => "Titan Managed Workspace"
            };
        }

        private string GetGradient(FacilityType type, bool start)
        {
            return type switch
            {
                FacilityType.Gym => start ? "#0EA5E9" : "#2563EB",
                FacilityType.Salon => start ? "#F43F5E" : "#E11D48",
                FacilityType.Restaurant => start ? "#F59E0B" : "#D97706",
                _ => start ? "#64748B" : "#475569"
            };
        }

        private string GetIcon(FacilityType type)
        {
            return type switch
            {
                FacilityType.Gym => FacilityTypeOption.icon_gym,
                FacilityType.Salon => FacilityTypeOption.icon_salon,
                FacilityType.Restaurant => FacilityTypeOption.icon_restaurant,
                _ => FacilityTypeOption.icon_gym
            };
        }
    }
}
