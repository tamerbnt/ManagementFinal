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
        private readonly Management.Presentation.Stores.ModalNavigationStore _modalNavigationStore;
        private readonly IServiceProvider _serviceProvider;

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
        public ICommand OpenCategoryDetailCommand { get; }

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
            Management.Presentation.Services.State.SessionManager sessionManager,
            Management.Presentation.Stores.ModalNavigationStore modalNavigationStore,
            IServiceProvider serviceProvider)
            : base(terminologyService, facilityContext, logger, diagnosticService, toastService, localizationService, dialogService)
        {
            _navigationService = navigationService;
            _dbContext = dbContext;
            _initTracker = initTracker;
            _dispatcher = dispatcher;
            _authService = authService;
            _sessionManager = sessionManager;
            _modalNavigationStore = modalNavigationStore;
            _serviceProvider = serviceProvider;

            EnterWorkspaceCommand = new AsyncRelayCommand(ExecuteEnterWorkspace, CanExecuteEnterWorkspace);
            EnterRemoteWorkspaceCommand = new AsyncRelayCommand(ExecuteEnterRemoteWorkspace, CanExecuteEnterWorkspace);
            SelectFacilityCommand = new RelayCommand<FacilityTypeOption>(f => { if (f?.IsAvailable == true) SelectedFacility = f; });
            OpenCategoryDetailCommand = new RelayCommand<FacilityTypeOption>(ExecuteOpenCategoryDetail);

            // FIX: Force data execution instantly on instantiation.
            // Bypasses the Navigation pipeline which is intentionally skipped natively by App.xaml.cs startup routing.
            _ = LoadFacilitiesFromLocalAsync();
        }

        private void ExecuteOpenCategoryDetail(FacilityTypeOption? option)
        {
            if (option == null) return;
            var modalVm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<CategoryDetailModalViewModel>(_serviceProvider);
            modalVm.Configure(option, confirmed =>
            {
                SelectedFacility = confirmed;
            });
            _ = _modalNavigationStore.OpenAsync(modalVm);
        }

        private bool CanExecuteEnterWorkspace()
        {
            return SelectedFacility != null && SelectedFacility.IsAvailable && _initTracker.IsComplete;
        }

        private async Task ExecuteEnterWorkspace()
        {
            if (SelectedFacility == null || !SelectedFacility.IsAvailable) return;
            
            if (_dialogService != null)
            {
                bool confirmed = await _dialogService.ShowConfirmationAsync(
                    "Confirm Workspace Archetype",
                    $"You are entering your workspace configured for:\n\n• {SelectedFacility.Name}\n{SelectedFacility.Description}\n\nWould you like to lock in this selection?",
                    "Launch Workspace",
                    "Change Category");

                if (!confirmed) return;
            }
            
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
                            IconKey = GetIcon(f.Type),
                            IsAvailable = f.Type != FacilityType.ProjectAndMilestone && f.Type != FacilityType.RentalAndBooking && f.Type != FacilityType.EducationAndCohort,
                            BadgeText = (f.Type == FacilityType.ProjectAndMilestone || f.Type == FacilityType.RentalAndBooking || f.Type == FacilityType.EducationAndCohort) ? "COMING SOON" : string.Empty
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
                        new FacilityTypeOption { Id = Guid.Empty, Type = FacilityType.PosAndInventory,       Name = "POS & Order/Inventory",     Description = GetDescription(FacilityType.PosAndInventory),       GradientStart = GetGradient(FacilityType.PosAndInventory, true),       GradientEnd = GetGradient(FacilityType.PosAndInventory, false),       IconKey = GetIcon(FacilityType.PosAndInventory), IsAvailable = true },
                        new FacilityTypeOption { Id = Guid.Empty, Type = FacilityType.AppointmentAndService, Name = "Appointment & Service",     Description = GetDescription(FacilityType.AppointmentAndService), GradientStart = GetGradient(FacilityType.AppointmentAndService, true), GradientEnd = GetGradient(FacilityType.AppointmentAndService, false), IconKey = GetIcon(FacilityType.AppointmentAndService), IsAvailable = true },
                        new FacilityTypeOption { Id = Guid.Empty, Type = FacilityType.MembershipAndSession,  Name = "Membership & Session",      Description = GetDescription(FacilityType.MembershipAndSession),  GradientStart = GetGradient(FacilityType.MembershipAndSession, true),  GradientEnd = GetGradient(FacilityType.MembershipAndSession, false),  IconKey = GetIcon(FacilityType.MembershipAndSession), IsAvailable = true },
                        new FacilityTypeOption { Id = Guid.Empty, Type = FacilityType.ProjectAndMilestone,  Name = "Project & Milestone",       Description = GetDescription(FacilityType.ProjectAndMilestone),   GradientStart = GetGradient(FacilityType.ProjectAndMilestone, true),   GradientEnd = GetGradient(FacilityType.ProjectAndMilestone, false),   IconKey = GetIcon(FacilityType.ProjectAndMilestone), IsAvailable = false, BadgeText = "COMING SOON" },
                        new FacilityTypeOption { Id = Guid.Empty, Type = FacilityType.RentalAndBooking,      Name = "Rental & Booking",          Description = GetDescription(FacilityType.RentalAndBooking),      GradientStart = GetGradient(FacilityType.RentalAndBooking, true),      GradientEnd = GetGradient(FacilityType.RentalAndBooking, false),      IconKey = GetIcon(FacilityType.RentalAndBooking), IsAvailable = false, BadgeText = "COMING SOON" },
                        new FacilityTypeOption { Id = Guid.Empty, Type = FacilityType.EducationAndCohort,   Name = "Education & Cohort",        Description = GetDescription(FacilityType.EducationAndCohort),    GradientStart = GetGradient(FacilityType.EducationAndCohort, true),    GradientEnd = GetGradient(FacilityType.EducationAndCohort, false),    IconKey = GetIcon(FacilityType.EducationAndCohort), IsAvailable = false, BadgeText = "COMING SOON" },
                    };
                }

                _dispatcher.Invoke(() =>
                {
                    AvailableFacilities.Clear();
                    foreach (var opt in options)
                    {
                        AvailableFacilities.Add(opt);
                    }

                    // Pre-select Gym/Membership facility if available, otherwise fallback to first available option
                    var defaultFacility = AvailableFacilities.FirstOrDefault(f => f.IsAvailable && (f.Type == FacilityType.Gym || f.Type == FacilityType.MembershipAndSession));
                    if (defaultFacility != null)
                    {
                        SelectedFacility = defaultFacility;
                    }
                    else
                    {
                        var realAvailable = localFacilities
                            .Where(f => f.Type != FacilityType.ProjectAndMilestone && f.Type != FacilityType.RentalAndBooking && f.Type != FacilityType.EducationAndCohort)
                            .OrderByDescending(f => f.LastModifiedAt ?? f.CreatedAt)
                            .FirstOrDefault();

                        if (realAvailable != null)
                        {
                            SelectedFacility = AvailableFacilities.FirstOrDefault(f => f.Id == realAvailable.Id && f.IsAvailable)
                                ?? AvailableFacilities.FirstOrDefault(f => f.IsAvailable);
                        }
                        else
                        {
                            SelectedFacility = AvailableFacilities.FirstOrDefault(f => f.IsAvailable);
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
                // Legacy types
                FacilityType.Gym        => "Fitness & Wellness Analytics",
                FacilityType.Salon      => "Beauty & Spa Operations",
                FacilityType.Restaurant => "Fine Dining Control",
                // Phase 2 archetypes
                FacilityType.PosAndInventory       => "Retail, Wholesale, Supermarkets, Food & Beverage",
                FacilityType.AppointmentAndService => "Salons, Spas, Barbershops, Beauty Clinics",
                FacilityType.MembershipAndSession  => "Gyms, Fitness Studios, Martial Arts, Sports Clubs",
                FacilityType.ProjectAndMilestone   => "Architecture, Law Firms, Creative Agencies",
                FacilityType.RentalAndBooking      => "Coworking Spaces, Event Venues, Equipment Rental",
                FacilityType.EducationAndCohort    => "Training Centers, Academies, Bootcamps, Institutes",
                _                                  => "Titan Managed Workspace"
            };
        }

        private string GetGradient(FacilityType type, bool start)
        {
            return type switch
            {
                // Legacy types
                FacilityType.Gym        => start ? "#0EA5E9" : "#2563EB",
                FacilityType.Salon      => start ? "#F43F5E" : "#E11D48",
                FacilityType.Restaurant => start ? "#F59E0B" : "#D97706",
                // Phase 2 archetypes — distinct palette
                FacilityType.PosAndInventory       => start ? "#6366F1" : "#4338CA",  // Indigo
                FacilityType.AppointmentAndService => start ? "#EC4899" : "#BE185D",  // Pink
                FacilityType.MembershipAndSession  => start ? "#0EA5E9" : "#0284C7",  // Sky
                FacilityType.ProjectAndMilestone   => start ? "#F59E0B" : "#B45309",  // Amber
                FacilityType.RentalAndBooking      => start ? "#10B981" : "#059669",  // Emerald
                FacilityType.EducationAndCohort    => start ? "#8B5CF6" : "#6D28D9",  // Violet
                _                                  => start ? "#64748B" : "#475569"
            };
        }

        private string GetIcon(FacilityType type)
        {
            return type switch
            {
                // Legacy types
                FacilityType.Gym        => FacilityTypeOption.icon_gym,
                FacilityType.Salon      => FacilityTypeOption.icon_salon,
                FacilityType.Restaurant => FacilityTypeOption.icon_restaurant,
                // Phase 2 archetypes
                FacilityType.PosAndInventory       => FacilityTypeOption.icon_pos,
                FacilityType.AppointmentAndService => FacilityTypeOption.icon_appointment,
                FacilityType.MembershipAndSession  => FacilityTypeOption.icon_membership,
                FacilityType.ProjectAndMilestone   => FacilityTypeOption.icon_project,
                FacilityType.RentalAndBooking      => FacilityTypeOption.icon_rental,
                FacilityType.EducationAndCohort    => FacilityTypeOption.icon_education,
                _                                  => FacilityTypeOption.icon_gym
            };
        }
    }
}
