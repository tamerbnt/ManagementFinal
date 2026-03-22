using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Management.Application.Services;
using Management.Presentation.ViewModels.Members;
using Management.Presentation.Models;
using Management.Presentation.ViewModels.Shared; // Added
using Management.Presentation.ViewModels.Finance;
using Management.Presentation.Services;
using Management.Presentation.Services.State;
using Management.Presentation.Services.Application;
using Management.Presentation.Services.Localization;
using Management.Application.Interfaces.App;
using INavigationService = Management.Presentation.Services.INavigationService;
using Management.Presentation.ViewModels.GymHome;
using Management.Presentation.ViewModels.History;
using Management.Presentation.ViewModels.Shop;
using Management.Presentation.ViewModels.Registrations;
using Management.Presentation.ViewModels.Settings;
using Management.Domain.Enums;
using Management.Application.Stores;
using Management.Presentation.ViewModels;
using Management.Presentation.Stores;
using Management.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Management.Presentation.Extensions;
using Management.Presentation.Services.Navigation;
using Management.Domain.Services;
using Management.Infrastructure.Services;
using Management.Domain.Models;
using System.Windows.Input;
using System.Linq;
using Management.Presentation.ViewModels.Sync;
using Management.Presentation.Models;
using RelayCommand = CommunityToolkit.Mvvm.Input.RelayCommand;
using AsyncRelayCommand = CommunityToolkit.Mvvm.Input.AsyncRelayCommand;

namespace Management.Presentation.ViewModels.Shell
{
    public partial class MainViewModel : ViewModelBase, IStateResettable
    {
        private readonly INavigationService _navigationService;
        private readonly SessionManager _sessionManager;
        private readonly System.IServiceProvider _serviceProvider;
        private readonly IBreadcrumbService _breadcrumbService;
        private readonly INavigationRegistry _navigationRegistry;
        private readonly IFacilityContextService _facilityContext;
        private readonly IResilienceService _resilienceService;
        private readonly IUndoService _undoService;
        private readonly ISessionMonitorService _sessionMonitor;
        private readonly SyncStore _syncStore;
        private readonly IDialogService _dialogService;
        private readonly IAuthenticationService _authService;
        private readonly ITerminologyService _terminologyService;
        private readonly ICommandPaletteService _paletteService;
        private readonly INotificationService _notificationService;
        private readonly Dictionary<Type, object> _viewCache = new();
        // Suppresses OnFacilityChanged navigation during the initial startup handoff.
        // Set to false by InitializeInitialView() once the first navigation is queued.
        private bool _isInitializing = true;
        // Atomic navigation lock: 0 = free, 1 = navigation in progress.
        // Prevents the race condition where multiple concurrent fire-and-forget navigations
        // all pass the type check before any of them has set NextViewModel.
        private int _navigationInProgress = 0;

        public ObservableCollection<ToastViewModel> ActiveToasts => _notificationService.ActiveToasts;

        [ObservableProperty]
        private TopBarViewModel _topBar;

        [ObservableProperty]
        private CommandPaletteViewModel _commandPalette;

        [ObservableProperty]
        private ConnectivityViewModel _connectivity; // Added

        [ObservableProperty]
        private bool _isNavigating;

        [ObservableProperty]
        private bool _isSettingsOpen;

        [ObservableProperty]
        private Settings.SettingsViewModel? _settings;

        [ObservableProperty]
        private bool _isSidebarCollapsed;

        [ObservableProperty]
        private bool _isEcoMode;

        [ObservableProperty]
        private int _activeIndex = -1; // -1 ensures the first navigation (0) triggers a change

        private readonly ModalNavigationStore _modalNavigationStore;
        private Type? _lastViewType;
        private string? _lastViewTitle;

        // --- SHELL PROPERTIES ---
        [ObservableProperty]
        private string _windowTitle = "Management Workspace";

        [ObservableProperty]
        private bool _isLoggedIn;

        [ObservableProperty]
        private bool _isOperational;

        [ObservableProperty]
        private bool _isOnboarding;

        [ObservableProperty]
        private bool _isOffline;

        [ObservableProperty]
        private bool _isAutoSaved;

        [ObservableProperty]
        private bool _isPrinting;

        [ObservableProperty]
        private bool _isDiagnosticVisible;

        [ObservableProperty]
        private int _globalRowHeight = 72;

        [ObservableProperty]
        private string _currentScreenName = "Workspace";

        [ObservableProperty]
        private bool _hasSubItem;

        [ObservableProperty]
        private string _subItemName = string.Empty;

        [ObservableProperty]
        private int _selectionCount;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        // Context-Aware Actions
        [ObservableProperty]
        private string _addButtonText = string.Empty;

        [ObservableProperty]
        private bool _isAddButtonEnabled;

        [ObservableProperty]
        private ICommand? _addCommand;

        public bool IsBulkSelectionActive => SelectionCount > 0;
        public string SelectionCountText => $"{SelectionCount} {_terminologyService.GetTerm(SelectionCount == 1 ? "Item" : "Items")} Selected";
        public bool ShowOfflineBanner => IsOffline;
        public string DiagnosticMemory => "42.5 MB"; // Mocked
        public string DiagnosticFPS => "60"; // Mocked
        public string DiagnosticConnectivity => IsOffline ? "Offline" : "Online";
        public int DiagnosticQueueCount => _resilienceService.PendingActions.Count;

        public string FacilityName => _facilityContext.CurrentFacility.ToString();
        public string MemberLabel => _facilityContext.CurrentFacility == FacilityType.Salon ? "Client" : "Member";
        public string MemberPluralLabel => MemberLabel + "s";

        public object? CurrentModalViewModel => _modalNavigationStore.CurrentModalViewModel;
        public bool IsModalOpen => _modalNavigationStore.IsOpen;

        [RelayCommand]
        private void OpenSettings()
        {
            System.Diagnostics.Debug.WriteLine("[MainViewModel] OpenSettings() called");
            IsSettingsOpen = true;
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] IsSettingsOpen set to: {IsSettingsOpen}");
            if (Settings == null)
            {
                Settings = _serviceProvider.GetRequiredService<Settings.SettingsViewModel>();
                System.Diagnostics.Debug.WriteLine("[MainViewModel] Settings ViewModel created");
            }
        }

        [RelayCommand]
        private void CloseSettings()
        {
            IsSettingsOpen = false;
            // Re-sync sidebar with whatever is currently visible
            if (CurrentView != null)
            {
                SyncSidebarWithCurrentView(CurrentView.GetType());
            }
        }

        [RelayCommand]
        private void OpenAccountSettings()
        {
            System.Diagnostics.Debug.WriteLine("[MainViewModel] OpenAccountSettings() called");
            IsSettingsOpen = true;
            if (Settings == null)
            {
                Settings = _serviceProvider.GetRequiredService<Settings.SettingsViewModel>();
            }
            // Navigate to Account tab
            Settings.SelectedTab = "Account";
            System.Diagnostics.Debug.WriteLine("[MainViewModel] Settings opened with Account tab selected");
        }

        private object? _currentView;
        public object? CurrentView
        {
            get => _currentView;
            private set => SetProperty(ref _currentView, value);
        }

        [ObservableProperty]
        private ObservableCollection<NavigationItemViewModel> _menuItems = new();

        private NavigationItemViewModel _selectedMenuItem = default!;
        public NavigationItemViewModel SelectedMenuItem
        {
            get => _selectedMenuItem;
            set
            {
                if (SetProperty(ref _selectedMenuItem, value) && value != null)
                {
                    // Update Settings status
                    bool isSettings = value.DisplayName == "Settings";
                    System.Diagnostics.Debug.WriteLine($"[MainViewModel] SelectedMenuItem changed to: {value.DisplayName}, isSettings: {isSettings}");
                    IsSettingsOpen = isSettings;
                    System.Diagnostics.Debug.WriteLine($"[MainViewModel] IsSettingsOpen set to: {IsSettingsOpen}");

                    // Sync visuals and Index atomically
                    SyncSidebarWithCurrentView(value.TargetViewModelType);

                    // Only navigate if it's NOT Settings (to avoid blank background)
                    if (!isSettings)
                    {
                        _ = NavigateToViewModelAsync(value.TargetViewModelType);
                    }
                }
            }
        }

        public MainViewModel(
            INavigationService navigationService, 
            SessionManager sessionManager, 
            TopBarViewModel topBar,
            CommandPaletteViewModel commandPalette,
            ConnectivityViewModel connectivity, // Injected
            IToastService toastService,
            IBreadcrumbService breadcrumbService,
            IResilienceService resilienceService,
            IUndoService undoService,
            ISessionMonitorService sessionMonitor,
            SyncStore syncStore,
            IDialogService dialogService,
            IAuthenticationService authService,
            ITerminologyService terminologyService,
            ICommandPaletteService paletteService,
            INotificationService notificationService,
            System.IServiceProvider serviceProvider,
            ILogger<MainViewModel> logger,
            IDiagnosticService diagnosticService)
            : base(logger, diagnosticService, toastService)
        {
            _navigationService = navigationService;
            _sessionManager = sessionManager;
            _topBar = topBar;
            _commandPalette = commandPalette;
            _connectivity = connectivity;
            _serviceProvider = serviceProvider;
            _breadcrumbService = breadcrumbService;

            _resilienceService = resilienceService;
            _undoService = undoService;
            _sessionMonitor = sessionMonitor;
            _syncStore = syncStore;
            _dialogService = dialogService;
            _authService = authService;
            _terminologyService = terminologyService;
            _paletteService = paletteService;
            _notificationService = notificationService;

            _facilityContext = _serviceProvider.GetRequiredService<IFacilityContextService>();
            _navigationRegistry = _serviceProvider.GetRequiredService<INavigationRegistry>();
            _modalNavigationStore = _serviceProvider.GetRequiredService<ModalNavigationStore>();



            // Subscribe to navigation changes
            _serviceProvider.GetRequiredService<NavigationStore>().CurrentViewModelChanged += OnCurrentViewModelChanged;
            _resilienceService.ConnectivityChanged += (s, isOnline) => OnConnectionStatusChanged(isOnline);
            _sessionMonitor.SessionExpired += OnSessionExpired;

            var localizationService = _serviceProvider.GetRequiredService<ILocalizationService>();
            localizationService.LanguageChanged += (s, e) => 
            {
                System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    foreach (var item in MenuItems)
                    {
                        item.DisplayName = _terminologyService.GetTerm(item.ResourceKey);
                    }
                    OnPropertyChanged(nameof(FacilityName));
                    OnPropertyChanged(nameof(MemberLabel));
                    OnPropertyChanged(nameof(MemberPluralLabel));
                });
            };

            // Initialize Commands
            LogoutCommand = new AsyncRelayCommand(ExecuteLogout);
            OpenCommandPaletteCommand = new RelayCommand(() => _paletteService.Open());
            ToggleDiagnosticCommand = new RelayCommand(() => IsDiagnosticVisible = !IsDiagnosticVisible);
            UndoCommand = new AsyncRelayCommand(async () => await _undoService.UndoAsync(), () => _undoService.CanUndo);
            ToggleDensityCommand = new RelayCommand(() => GlobalRowHeight = GlobalRowHeight == 72 ? 48 : 72);
            // Wire generated commands to TopBar
            _topBar.OpenAccountSettingsCommand = OpenAccountSettingsCommand;
            _topBar.OpenSettingsCommand = OpenSettingsCommand;
            _topBar.CloseSettingsCommand = CloseSettingsCommand;
            
            _undoService.CanUndoChanged += (s, e) => ((AsyncRelayCommand)UndoCommand).NotifyCanExecuteChanged();
            _undoService.VisibilityChanged += (s, e) => OnPropertyChanged(nameof(IsUndoVisible));

            // Breadcrumb navigation
            if (_breadcrumbService is Management.Presentation.Services.Application.BreadcrumbService bs)
            {
                bs.Navigated += async (type) => 
                {
                    await NavigateToViewModelAsync(type);
                };
            }

            _modalNavigationStore.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ModalNavigationStore.CurrentModalViewModel) || 
                    e.PropertyName == nameof(ModalNavigationStore.IsOpen))
                {
                    OnPropertyChanged(nameof(CurrentModalViewModel));
                    OnPropertyChanged(nameof(IsModalOpen));
                }
            };

            InitializeMenu();
            InitializeInitialView();
            _ = HydrateSessionAsync();

            _facilityContext.FacilityChanged += OnFacilityChanged;
            
            // Note: Removed redundant OnCurrentViewModelChanged() call here.
            // App.xaml.cs controls initial navigation and sets CurrentViewModel,
            // which inherently fires the event.
        }

        public ICommand LogoutCommand { get; private set; } = null!;
        public ICommand OpenCommandPaletteCommand { get; private set; } = null!;
        public ICommand ToggleDiagnosticCommand { get; private set; } = null!;
        public ICommand UndoCommand { get; private set; } = null!;
        public ICommand ToggleDensityCommand { get; private set; } = null!;
        public bool IsUndoVisible => _undoService.IsBannerVisible;
        public bool IsSyncing => _syncStore.IsSyncing;


        private void OnFacilityChanged(FacilityType type)
        {
            // During the initial startup handoff the IStateResettable loop + InitializeInitialView
            // handles the first navigation. Suppress this event until that is done.
            if (_isInitializing) return;

            // Clear view cache on switch to ensure facility-specific styles/resources are fresh
            _viewCache.Clear();
            RefreshMenu();
            
            // Navigate to the new facility's home view
            var homeViewType = _navigationRegistry.GetHomeViewType(type);
            _ = NavigateToViewModelAsync(homeViewType);
        }

        private async Task HydrateSessionAsync()
        {
            if (!_sessionManager.IsLoggedIn)
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var authService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
                    var result = await authService.GetCurrentUserAsync();
                    if (result.IsSuccess)
                    {
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
                        {
                            _sessionManager.SetUser(result.Value);
                        });
                    }
                }
            }
        }

        public void InitializeInitialView()
        {
            // Set null first to ensure the property change fires for the same index (0) if needed
            _selectedMenuItem = null!;
            if (MenuItems.Count > 0)
            {
                SelectedMenuItem = MenuItems[0]; // Home
            }
            // Unlock: startup handoff is complete. FacilityChanged can now trigger navigation.
            _isInitializing = false;
        }

        [RelayCommand]
        private void Navigate(NavigationItemViewModel item)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] Navigate command called with: {item?.DisplayName}");
            if (item != null)
            {
                SelectedMenuItem = item;
            }
        }

        private void InitializeMenu()
        {
            RefreshMenu();
        }

        private void RefreshMenu()
        {
            var items = _navigationRegistry.GetItems(_facilityContext.CurrentFacility);

            // Populate synchronously so MenuItems is ready before InitializeInitialView() is called
            MenuItems.Clear();
            foreach (var item in items)
            {
                MenuItems.Add(new NavigationItemViewModel(
                    _terminologyService.GetTerm(item.ResourceKey),
                    item.ResourceKey,
                    item.IconKey,
                    item.TargetViewModelType));
            }
        }

        [RelayCommand]
        private void ToggleNotifications()
        {
            // TODO: Implement Notification Logic
            if (TopBar != null) TopBar.NotificationCount = 0; // Clear count as mock 'read' action
        }

        [RelayCommand]
        private void OpenProfileMenu()
        {
            // TODO: Implement Profile Menu Logic
        }

        [RelayCommand]
        private void ToggleCommandPalette()
        {
            CommandPalette.IsVisible = !CommandPalette.IsVisible;
            if (CommandPalette.IsVisible)
            {
                CommandPalette.SearchQuery = string.Empty;
            }
        }

        [RelayCommand]
        private void ToggleSidebar()
        {
            IsSidebarCollapsed = !IsSidebarCollapsed;
        }

        [RelayCommand]
        private async Task ChangeFacility()
        {
            // Step 1: Selection
            var selectionResult = await _modalNavigationStore.OpenAsync<ChangeFacilityViewModel>();
            
            if (selectionResult.IsSuccess && selectionResult.Data is FacilityOption selected)
            {
                // Step 2: Authentication
                var authResult = await _modalNavigationStore.OpenAsync<FacilityAuthViewModel>();
                
                if (authResult.IsSuccess)
                {
                    _facilityContext.SetFacility(selected.Type);
                    _toastService.ShowSuccess($"Successfully switched to {selected.Name}.");
                }
            }
        }

        private void OnCurrentViewModelChanged()
        {
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var navigationStore = _serviceProvider.GetRequiredService<NavigationStore>();
                IsNavigating = navigationStore.IsNavigating;
                
                var currentVm = navigationStore.CurrentViewModel;
                
                // [Optimization]: Use Cached View Instance if available
                CurrentView = GetOrCacheView(currentVm);

                // Notify View that Content has changed
                OnPropertyChanged(nameof(CurrentViewModel));

                // Setup Shell Visibility Logic
                IsLoggedIn = currentVm != null &&
                             currentVm is not LoginViewModel &&
                             currentVm is not LicenseEntryViewModel &&
                             currentVm is not OnboardingOwnerViewModel;

                IsOnboarding = currentVm is LicenseEntryViewModel ||
                               currentVm is OnboardingOwnerViewModel;

                IsOperational = IsLoggedIn && !IsOnboarding;

                Serilog.Log.Information($"State Update: IsLoggedIn={IsLoggedIn}, IsOnboarding={IsOnboarding}, IsOperational={IsOperational}, View={currentVm?.GetType().Name ?? "null"}");
                
                if (IsOnboarding)
                {
                    _paletteService.Close();
                }

                // Update Breadcrumbs & Sidebar
                if (currentVm != null)
                {
                    var type = currentVm.GetType();
                    SyncSidebarWithCurrentView(type);
                }

                // Update TopBar Button Logic
                if (IsOperational)
                {
                    UpdateContextAwareActions();
                if (IsLoggedIn) _ = _sessionMonitor.StartMonitoringAsync();
                }
                else
                {
                    AddButtonText = string.Empty;
                    AddCommand = null;
                    IsAddButtonEnabled = false;

                    if (currentVm is LoginViewModel || IsOnboarding)
                    {
                        _ = _sessionMonitor.StopMonitoringAsync();
                    }
                }

                OnPropertyChanged(nameof(FacilityName));
                OnPropertyChanged(nameof(MemberLabel));
                OnPropertyChanged(nameof(MemberPluralLabel));
            });
        }

        private void OnSessionExpired(object? sender, Management.Domain.Services.SessionExpiredEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _dialogService.ShowCustomDialogAsync<SessionExpiredViewModel>(e.Message);
            });
        }

        private void OnConflictDetected(Management.Domain.Models.OutboxMessage message)
        {
            System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                var parameters = new ConflictResolutionParameters
                {
                    EntityName = message.EntityType,
                    EntityId = Guid.Parse(message.EntityId),
                    LocalContent = message.ContentJson,
                    RemoteContent = "Remote data unavailable (Offline Conflict)", 
                    ConflictMessage = message.LastError
                };

                await _dialogService.ShowCustomDialogAsync<ConflictResolutionViewModel>(parameters);
            });
        }

        private void OnConnectionStatusChanged(bool isOnline)
        {
            System.Windows.Application.Current.Dispatcher.InvokeAsync(async () => {
                IsOffline = !isOnline;
                OnPropertyChanged(nameof(ShowOfflineBanner));
                OnPropertyChanged(nameof(DiagnosticConnectivity));
                if (isOnline) await _resilienceService.ProcessQueueAsync();
            });
        }

        private void UpdateContextAwareActions()
        {
            var navigationStore = _serviceProvider.GetRequiredService<NavigationStore>();
            var currentVm = navigationStore.CurrentViewModel;

            switch (currentVm)
            {
                case DashboardViewModel _:
                case MembersViewModel _:
                    AddButtonText = _terminologyService.GetTerm("Terminology.Add.Member");
                    IsAddButtonEnabled = true;
                    AddCommand = new RelayCommand(ExecuteAddMember);
                    break;

                case FinanceAndStaffViewModel _:
                    AddButtonText = _terminologyService.GetTerm("Terminology.Add.Payment");
                    IsAddButtonEnabled = true;
                    AddCommand = new RelayCommand(ExecuteAddPayment);
                    break;

                case ShopViewModel _:
                    AddButtonText = _terminologyService.GetTerm("Terminology.Add.Product");
                    IsAddButtonEnabled = true;
                    AddCommand = new RelayCommand(ExecuteAddProduct);
                    break;

                case RegistrationsViewModel _:
                default:
                    AddButtonText = _terminologyService.GetTerm("Terminology.Add.New");
                    IsAddButtonEnabled = false;
                    AddCommand = null;
                    break;
            }
        }

        private void ExecuteAddMember() { /* Implementation skipped per user instructions or existing logic */ }
        private void ExecuteAddPayment() { }
        private void ExecuteAddProduct() { }

        private async Task ExecuteLogout()
        {
            if (await _dialogService.ShowConfirmationAsync(
                "Are you sure you want to log out?",
                "Logout Confirmation",
                "Logout",
                "Cancel"))
            {
                await _authService.LogoutAsync();
                await _navigationService.NavigateToLoginAsync();
            }
        }

        private async Task NavigateToViewModelAsync(System.Type viewModelType)
        {
            // TEMP DIAGNOSTIC — remove after verifying single navigation on startup
            Serilog.Log.Debug("[Navigation] NavigateToViewModelAsync called for {Type}", viewModelType.Name);

            // Thread-safe atomic lock: if another navigation is already in progress, skip.
            // This is the primary guard against the triple-navigation race condition.
            if (System.Threading.Interlocked.CompareExchange(ref _navigationInProgress, 1, 0) != 0)
            {
                Serilog.Log.Debug("[Navigation] Skipped concurrent navigation to {Type} — lock held.", viewModelType.Name);
                return;
            }

            try
            {
                var navigationStore = _serviceProvider.GetRequiredService<NavigationStore>();

                // Secondary guard: avoid redundant navigation to the same VM type
                if (navigationStore.CurrentViewModel?.GetType() == viewModelType ||
                    navigationStore.NextViewModel?.GetType() == viewModelType)
                {
                    return;
                }

                await _navigationService.NavigateToAsync(viewModelType);
            }
            catch (System.Exception ex)
            {
                _toastService.ShowError($"Navigation failed: {ex.Message}");
                System.Console.WriteLine($"[CRITICAL] Navigation to {viewModelType.Name} failed: {ex}");
            }
            finally
            {
                // Always release the lock so future navigations can proceed
                System.Threading.Interlocked.Exchange(ref _navigationInProgress, 0);
            }
        }

        private void ClearSelection()
        {
            if (CurrentView is MembersViewModel membersVm)
            {
                // Proxy selection clearing logic here if needed
            }
        }

        public object? CurrentViewModel => CurrentView;

        private void SyncSidebarWithCurrentView(System.Type viewType)
        {
            if (viewType == null) return;
            
            // Find the item that matches this view type
            var match = MenuItems.FirstOrDefault(m => m.TargetViewModelType == viewType);

            if (match != null)
            {
                // 1. Force SelectedMenuItem sync silently if it's different (External Nav)
                if (match != _selectedMenuItem)
                {
                    _selectedMenuItem = match;
                    OnPropertyChanged(nameof(SelectedMenuItem));
                }

                // 2. Sync Settings Open state (important for Palette navigation)
                bool isTargetSettings = match.DisplayName == "Settings";
                if (isTargetSettings)
                {
                    OpenSettings();
                }
                else
                {
                    // Close settings when navigating AWAY to another module
                    if (IsSettingsOpen) CloseSettings();
                }

                // 3. Atomic visual sync
                foreach (var item in MenuItems) 
                {
                    item.IsActive = (item == match);
                }
                
                // 4. Indicator sync - Force a flicker to ensure VisualStateManager transitions always fire.
                int newIndex = MenuItems.IndexOf(match);
                ActiveIndex = -1;
                ActiveIndex = newIndex;

                // 5. Update Breadcrumbs - Always show a trail
                if (_breadcrumbService is Management.Presentation.Services.Application.BreadcrumbService bs)
                {
                    // Determine the home screen (first navigation or explicit home)
                    string homeTitle = "Home";
                    Type? homeType = typeof(GymHomeViewModel); // Default home
                    
                    // If we have a previous screen and it's different from current, show: Previous > Current
                    if (_lastViewType != null && _lastViewType != viewType)
                    {
                        bs.SetBreadcrumbs((_lastViewTitle ?? homeTitle, _lastViewType), (match.DisplayName, viewType));
                    }
                    // Otherwise, show: Home > Current (unless we ARE home)
                    else if (viewType != homeType)
                    {
                        bs.SetBreadcrumbs((homeTitle, homeType), (match.DisplayName, viewType));
                    }
                    // If we're on the home screen, just show "Home"
                    else
                    {
                        bs.SetBreadcrumbs((homeTitle, homeType));
                    }
                }

                // Update last state for next time
                _lastViewType = viewType;
                _lastViewTitle = match.DisplayName;
            }
        }

        public void ResetState()
        {
            IsSettingsOpen = false;
            // Clear view cache on switch to ensure facility-specific styles/resources are fresh
            _viewCache.Clear();
            InitializeMenu();
            InitializeInitialView();
        }

        private object? GetOrCacheView(object? viewModel)
        {
            if (viewModel == null) return null;

            var vmType = viewModel.GetType();
            
            // Only cache major functional screens
            bool shouldCache = vmType.Name.EndsWith("ViewModel") && 
                              !vmType.Name.Contains("Modal") && 
                              !vmType.Name.Contains("Detail") &&
                              !vmType.Name.Contains("Editor") &&
                              !vmType.Name.Contains("Login") &&
                              !vmType.Name.Contains("Onboarding");

            if (!shouldCache) return viewModel;

            if (_viewCache.TryGetValue(vmType, out var cachedView))
            {
                if (cachedView is FrameworkElement fe)
                {
                    fe.DataContext = viewModel;
                }
                return cachedView;
            }

            // Resolve and create the View instance
            // We use naming convention as a fallback if ViewMappingService only handles windows
            object? view = null;
            try
            {
                var viewTypeName = vmType.FullName?.Replace("ViewModel", "View");
                if (viewTypeName != null)
                {
                    // Handle namespace differences if any
                    viewTypeName = viewTypeName.Replace(".ViewModels.", ".Views.");
                    
                    var viewType = vmType.Assembly.GetType(viewTypeName);
                    if (viewType != null)
                    {
                        view = ActivatorUtilities.CreateInstance(_serviceProvider, viewType);
                        if (view is FrameworkElement fe)
                        {
                            fe.DataContext = viewModel;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create cached view for {VmType}. Falling back to standard DataTemplate.", vmType.Name);
            }

            if (view != null)
            {
                _viewCache[vmType] = view;
                return view;
            }

            return viewModel; // Fallback to DataTemplate
        }
    }

    // Shared extension to allow typed navigation from Type
    public static class NavigationExtensions
    {
        public static async Task NavigateToAsync(this INavigationService service, System.Type type)
        {
            // Find the NavigateToAsync method that has 0 parameters and 1 generic argument
            var method = service.GetType()
                .GetMethods()
                .First(m => m.Name == "NavigateToAsync" && 
                            m.IsGenericMethod && 
                            m.GetParameters().Length == 0)
                .MakeGenericMethod(type);

            var task = method.Invoke(service, null) as Task;
            if (task != null) await task;
        }
    }
}
