using System;
using Management.Application.Services;
using System.Collections.ObjectModel;
using Management.Application.Services;
using System.Linq;
using Management.Application.Services;
using System.Windows.Input;
using Management.Application.Services;
using Management.Presentation.Services;
using Management.Application.Services;
using Management.Application.Stores;
using Management.Application.Services;
using Management.Presentation.Stores;
using Management.Application.Services;
using Microsoft.Extensions.DependencyInjection; // Added for GetRequiredService extension
using Management.Domain.Enums;
using Management.Application.Services;
using Management.Domain.Services;
using Management.Application.Services;
using Management.Infrastructure.Services;
using Management.Application.Services;
using Management.Presentation.Extensions;
using Management.Application.Services;
using Management.Presentation.ViewModels; // For NavigationItemViewModel
using Management.Presentation.Views.AccessControl;
using Management.Application.Services;
using Management.Presentation.Views.Dashboard;
using Management.Application.Services;
using Management.Presentation.Views.FinanceAndStaff;
using Management.Application.Services;
using Management.Presentation.Views.Members;
using Management.Application.Services;
using Management.Presentation.Views.Shop;
using Management.Application.Services;
using Management.Presentation.Views.Restaurant;
using Management.Application.Services;
using Management.Presentation.Views.Salon;
using Management.Application.Services;

namespace Management.Presentation.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly INavigationService _navigationService;
        private readonly NavigationStore _navigationStore;
        private readonly ModalNavigationStore _modalNavigationStore; // New
        private readonly IAuthenticationService _authService; // For Account button info
        private readonly IResilienceService _resilienceService; // For Offline badge & Queue
        private readonly IUndoService _undoService;
        private readonly SyncStore _syncStore;
        private readonly IDialogService _dialogService;
        private readonly IDispatcher _dispatcher;
        private readonly ITerminologyService _terminologyService;

        // --- 1. SHELL STATE ---

        public Management.Domain.Services.IFacilityContextService FacilityContext { get; }
        public ITerminologyService TerminologyService => _terminologyService;
        public ICommandPaletteService CommandPalette { get; }
        public INotificationService NotificationService { get; }

        public string CurrentTerminology => _terminologyService.GetTerm("Guest");
        public string ActiveThemePath => $"/Management.Presentation;component/Resources/Themes/Theme.{FacilityContext.CurrentFacility}.xaml";
        
        public string ActiveAccentBrush => FacilityContext.CurrentFacility switch
        {
            FacilityType.Salon => "#D8A7D8", // Lavender Rose
            FacilityType.Restaurant => "#FF7F50", // Coral
            _ => "#007ACC" // Default Blue
        };

        public bool ShowOfflineBanner => IsOffline;
        
        private int _globalRowHeight = 72;
        public int GlobalRowHeight
        {
            get => _globalRowHeight;
            set => SetProperty(ref _globalRowHeight, value);
        }

        private bool _isOffline;
        public bool IsOffline
        {
            get => _isOffline;
            set => SetProperty(ref _isOffline, value);
        }

        private bool _isAutoSaved;
        public bool IsAutoSaved
        {
            get => _isAutoSaved;
            set => SetProperty(ref _isAutoSaved, value);
        }

        private bool _isPrinting;
        public bool IsPrinting
        {
            get => _isPrinting;
            set => SetProperty(ref _isPrinting, value);
        }

        private string _windowTitle = "Management Workspace";
        public string WindowTitle
        {
            get => _windowTitle;
            set => SetProperty(ref _windowTitle, value);
        }

        private bool _isLoggedIn;
        public bool IsLoggedIn
        {
            get => _isLoggedIn;
            set => SetProperty(ref _isLoggedIn, value);
        }

        // Breadcrumbs & Shell Info
        public string FacilityName => FacilityContext.CurrentFacility.ToString();
        
        private bool _isDiagnosticVisible;
        public bool IsDiagnosticVisible
        {
            get => _isDiagnosticVisible;
            set => SetProperty(ref _isDiagnosticVisible, value);
        }

        public string DiagnosticMemory => "42.5 MB"; // Mocked
        public string DiagnosticFPS => "60"; // Mocked
        public string DiagnosticConnectivity => IsOffline ? "Offline" : "Online";
        public int DiagnosticQueueCount => _resilienceService.PendingActions.Count;
        public string DiagnosticResourceDict => $"Theme.{FacilityContext.CurrentFacility}.xaml";
        
        private string _searchQuery = string.Empty;
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                {
                    // Clear selection on search change (Requirement Task 4)
                    ClearSelection();
                }
            }
        }

        private string _currentScreenName = string.Empty;
        public string CurrentScreenName
        {
            get => _currentScreenName;
            set => SetProperty(ref _currentScreenName, value);
        }

        private bool _hasSubItem;
        public bool HasSubItem
        {
            get => _hasSubItem;
            set => SetProperty(ref _hasSubItem, value);
        }

        private string _subItemName = string.Empty;
        public string SubItemName
        {
            get => _subItemName;
            set => SetProperty(ref _subItemName, value);
        }

        private bool _isOperational;
        /// <summary>
        /// True when the user is logged in and the main workspace should be shown.
        /// </summary>
        public bool IsOperational
        {
            get => _isOperational;
            set => SetProperty(ref _isOperational, value);
        }

        private bool _isOnboarding;
        /// <summary>
        /// True when in LicenseEntry or OnboardingOwner states.
        /// </summary>
        public bool IsOnboarding
        {
            get => _isOnboarding;
            set => SetProperty(ref _isOnboarding, value);
        }

        private int _selectionCount;
        public int SelectionCount
        {
            get => _selectionCount;
            set
            {
                if (SetProperty(ref _selectionCount, value))
                {
                    OnPropertyChanged(nameof(SelectionCountText));
                    OnPropertyChanged(nameof(IsBulkSelectionActive));
                }
            }
        }

        public bool IsBulkSelectionActive => SelectionCount > 0;

        public string SelectionCountText => $"{SelectionCount} {TerminologyService.GetTerm(SelectionCount == 1 ? "Item" : "Items")} Selected";

        public ICommand LogoutCommand { get; private set; } = null!;
        public ICommand OpenCommandPaletteCommand { get; private set; } = null!;
        public ICommand ToggleDiagnosticCommand { get; private set; } = null!;
        public ICommand UndoCommand { get; private set; } = null!;
        public ICommand ToggleDensityCommand { get; private set; } = null!;

        public IUndoService UndoService => _undoService;
        public bool IsUndoVisible => _undoService.IsBannerVisible;

        // Expose the current view for the ContentPresenter
        public ViewModelBase? CurrentViewModel => _navigationStore.CurrentViewModel as ViewModelBase;

        // --- MODAL STATE ---
        public ViewModelBase? CurrentModalViewModel => _modalNavigationStore.CurrentModalViewModel as ViewModelBase;
        public bool IsModalOpen => _modalNavigationStore.IsOpen;

        private void OnModalPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ModalNavigationStore.CurrentModalViewModel) || e.PropertyName == nameof(ModalNavigationStore.IsOpen))
            {
                 _dispatcher.Invoke(() =>
                {
                    OnPropertyChanged(nameof(CurrentModalViewModel));
                    OnPropertyChanged(nameof(IsModalOpen));
                    Serilog.Log.Information($"[MainViewModel] Modal Update: IsOpen={IsModalOpen}, VM={CurrentModalViewModel?.GetType().Name ?? "null"}");
                });
            }
        }

        // --- 2. SIDEBAR NAVIGATION ---
        
        private bool _sidebarCollapsed;
        public bool SidebarCollapsed
        {
            get => _sidebarCollapsed;
            set => SetProperty(ref _sidebarCollapsed, value);
        }

        public ObservableCollection<NavigationItemViewModel> NavigationItems { get; }
            = new ObservableCollection<NavigationItemViewModel>();

        public ICommand NavigateCommand { get; }

        // --- 3. CONTEXT-AWARE ACTIONS (TopBar) ---
        // Design System Section 10.5

        private string _addButtonText = string.Empty;
        public string AddButtonText
        {
            get => _addButtonText;
            set => SetProperty(ref _addButtonText, value);
        }

        private bool _isAddButtonEnabled;
        public bool IsAddButtonEnabled
        {
            get => _isAddButtonEnabled;
            set => SetProperty(ref _isAddButtonEnabled, value);
        }

        private ICommand? _addCommand;
        public ICommand? AddCommand
        {
            get => _addCommand;
            set => SetProperty(ref _addCommand, value);
        }

        // --- 4. CONSTRUCTOR ---

        private readonly ISessionMonitorService _sessionMonitor;

        public MainViewModel(
            NavigationStore navigationStore,
            INavigationService navigationService,
            IAuthenticationService authService,
            IResilienceService resilienceService,
            IUndoService undoService,
            ISessionMonitorService sessionMonitor,
            SyncStore syncStore,
            IDialogService dialogService,
            IDispatcher dispatcher,
            Management.Domain.Services.IFacilityContextService facilityContext,
            ITerminologyService terminologyService,
            ICommandPaletteService commandPalette,
            INotificationService notificationService,
            IServiceProvider serviceProvider) // Added ServiceProvider to resolve ModalStore (avoid breaking signature too much if possible, or just add Store)
        {
            _navigationStore = navigationStore;
            _navigationService = navigationService;
            _authService = authService;
            _resilienceService = resilienceService;
            _undoService = undoService;
            _sessionMonitor = sessionMonitor;
            _syncStore = syncStore;
            _dialogService = dialogService;
            _dispatcher = dispatcher;
            FacilityContext = facilityContext;
            _terminologyService = terminologyService;
            CommandPalette = commandPalette;
            NotificationService = notificationService;
            _modalNavigationStore = serviceProvider.GetRequiredService<ModalNavigationStore>();
            
            // Subscribe to Session Expiry
            _sessionMonitor.SessionExpired += OnSessionExpired;

            // Subscribe to state changes
            _navigationStore.CurrentViewModelChanged += OnCurrentViewModelChanged;
            _modalNavigationStore.PropertyChanged += OnModalPropertyChanged;
            FacilityContext.FacilityChanged += OnFacilityChanged;
            _resilienceService.ConnectivityChanged += (s, isOnline) => OnConnectionStatusChanged(isOnline);
            _syncStore.ConflictDetected += OnConflictDetected;
            _syncStore.SyncStatusChanged += () => OnPropertyChanged(nameof(IsSyncing));

            // Initialize Commands
            NavigateCommand = new RelayCommand<NavigationItemViewModel>(ExecuteNavigate);
            LogoutCommand = new RelayCommand(ExecuteLogout);
            OpenCommandPaletteCommand = new RelayCommand(() => CommandPalette.Open());
            ToggleDiagnosticCommand = new RelayCommand(() => IsDiagnosticVisible = !IsDiagnosticVisible);
            UndoCommand = new AsyncRelayCommand(async () => await _undoService.UndoAsync(), () => _undoService.CanUndo);
            ToggleDensityCommand = new RelayCommand(() => GlobalRowHeight = GlobalRowHeight == 72 ? 48 : 72);
            _undoService.CanUndoChanged += (s, e) => ((AsyncRelayCommand)UndoCommand).RaiseCanExecuteChanged();
            _undoService.VisibilityChanged += (s, e) => OnPropertyChanged(nameof(IsUndoVisible));

            // Initialize Sidebar
            InitializeNavigationItems();

            // Set Initial State
            CommandPalette.Close();
            OnCurrentViewModelChanged();
            UpdateContextAwareActions();
        }

        private void OnFacilityChanged(Management.Domain.Enums.FacilityType newFacility)
        {
             _dispatcher.Invoke(() => {
                InitializeNavigationItems(); // Rebuild sidebar for new facility context
                _navigationStore.CurrentViewModel = null; // Clear view
                _navigationService.NavigateToAsync(0); // Navigate to Dashboard
                CommandPalette.Close(); // Close search to refresh indices
            });
        }

        // --- 5. LOGIC IMPLEMENTATION ---

        private void OnCurrentViewModelChanged()
        {
            _dispatcher.Invoke(() =>
            {
                var currentVm = _navigationStore.CurrentViewModel;

                // Notify View that Content has changed
                OnPropertyChanged(nameof(CurrentViewModel));

                // Setup Shell Visibility Logic (Refined for Sequoia)
                // IsLoggedIn = authenticated user exists AND we are not in onboarding
                IsLoggedIn = currentVm != null &&
                             currentVm is not LoginViewModel &&
                             currentVm is not LicenseEntryViewModel &&
                             currentVm is not OnboardingOwnerViewModel;

                // IsOnboarding = License Entry or New Tenant Registration
                IsOnboarding = currentVm is LicenseEntryViewModel ||
                               currentVm is OnboardingOwnerViewModel;

                // IsOperational = Authenticated and inside the workspace (not null, not onboarding)
                IsOperational = IsLoggedIn && !IsOnboarding;

                Serilog.Log.Information($"State Update: IsLoggedIn={IsLoggedIn}, IsOnboarding={IsOnboarding}, IsOperational={IsOperational}, View={currentVm?.GetType().Name ?? "null"}");
                
                if (IsOnboarding)
                {
                    CommandPalette.Close();
                }

                // Update Breadcrumbs
                if (currentVm != null)
                {
                    var type = currentVm.GetType();
                    var item = NavigationItems.FirstOrDefault(i => i.TargetViewModelType == type);
                    CurrentScreenName = item?.DisplayName ?? "Workspace";
                    HasSubItem = false; // Reset sub-item
                }

                // Update TopBar Button Logic
                if (IsOperational)
                {
                    UpdateContextAwareActions();
                    UpdateSidebarSelection();
                    _sessionMonitor.StartMonitoringAsync();
                }
                else
                {
                    // Clean up UI state for onboarding/login
                    AddButtonText = string.Empty;
                    AddCommand = null;
                    IsAddButtonEnabled = false;

                    if (currentVm is LoginViewModel || IsOnboarding)
                    {
                        _sessionMonitor.StopMonitoringAsync();
                    }
                }

                OnPropertyChanged(nameof(FacilityName));
                OnPropertyChanged(nameof(MemberLabel));
                OnPropertyChanged(nameof(MemberPluralLabel));
            });
        }

        public bool IsSyncing => _syncStore.IsSyncing;

        private void OnSessionExpired(object? sender, Domain.Services.SessionExpiredEventArgs e)
        {
             _dispatcher.Invoke(() =>
             {
                 // We can use IDialogService to show the custom view?
                 // Or IModalNavigationService? IDialogService usually wraps MessageBox.
                 // We need to show the custom modal.
                 // Ideally MainViewModel shouldn't know about ModalNavigationService directly if IDialogService handles it.
                 // But looking at IDialogService, it might be simple.
                 
                 // However, we just registered SessionExpiredViewModel mapping.
                 // So we can use IModalNavigationService (store).
                 
                 // Limitation: MainViewModel constructor didn't receive IModalNavigationService.
                 // I should add it or use a service locator pattern via IDialogService if it supports custom viewmodels.
                 
                 // Let's assume IDialogService can ShowCustom<T>(message).
                 // IF NOT, I'll fallback to MessageBox for now OR update Constructor.
                 
                 // Update: Constructor injection is cleaner.
                 // But IDialogService.ShowCustomDialogAsync<T> exists in my earlier reading of MainViewModel (lines 135-141).
                 // "await _dialogService.ShowCustomDialogAsync<ConflictResolutionViewModel>(parameters);"
                 
                 // So I can use that!
                 _dialogService.ShowCustomDialogAsync<SessionExpiredViewModel>(e.Message);
             });
        }

        private void OnConflictDetected(Management.Domain.Models.OutboxMessage message)
        {
            _dispatcher.InvokeAsync(async () =>
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
            _dispatcher.Invoke(async () => {
                IsOffline = !isOnline;
                OnPropertyChanged(nameof(ShowOfflineBanner));
                if (isOnline) await _resilienceService.ProcessQueueAsync();
            });
        }

        private void ExecuteNavigate(NavigationItemViewModel item)
        {
            if (item == null) return;

            // Use the Service to navigate by Index
            // We find the index of the clicked item in the collection
            int index = NavigationItems.IndexOf(item);
            if (index >= 0)
            {
                _navigationService.NavigateToAsync(index);
            }
        }

        private void UpdateContextAwareActions()
        {
            // Design System Section 10.5 Logic
            // Determines what the "Big Plus Button" does based on the active screen

            var currentVm = _navigationStore.CurrentViewModel;

            switch (currentVm)
            {
                case DashboardViewModel _:
                case MembersViewModel _:
                    AddButtonText = TerminologyService.GetTerm("TerminologyAddMemberLabel");
                    IsAddButtonEnabled = true;
                    AddCommand = new RelayCommand(ExecuteAddMember);
                    break;

                case FinanceAndStaffViewModel _:
                    AddButtonText = TerminologyService.GetTerm("TerminologyAddPaymentLabel");
                    IsAddButtonEnabled = true;
                    AddCommand = new RelayCommand(ExecuteAddPayment);
                    break;

                case ShopViewModel _:
                    AddButtonText = TerminologyService.GetTerm("TerminologyAddProductLabel");
                    IsAddButtonEnabled = true;
                    AddCommand = new RelayCommand(ExecuteAddProduct);
                    break;

                case TablesViewModel _:
                    AddButtonText = TerminologyService.GetTerm("TerminologyNewOrderLabel");
                    IsAddButtonEnabled = true;
                    AddCommand = new RelayCommand(ExecuteNewOrder);
                    break;

                case AccessControlViewModel _:
                case RegistrationsViewModel _:
                default:
                    AddButtonText = TerminologyService.GetTerm("TerminologyAddNewLabel");
                    IsAddButtonEnabled = false;
                    AddCommand = null;
                    break;
            }
        }

        private void UpdateSidebarSelection()
        {
            // Reset all
            foreach (var item in NavigationItems)
            {
                item.IsActive = false;
            }

            // Find matching item based on ViewModel Type
            // In a real app, you might map Types to Keys or Enums
            var currentType = _navigationStore.CurrentViewModel?.GetType();

            var activeItem = NavigationItems.FirstOrDefault(i => i.TargetViewModelType == currentType);
            if (activeItem != null)
            {
                activeItem.IsActive = true;
            }
        }

        private void InitializeNavigationItems()
        {
            NavigationItems.Clear();
            NavigationItems.Add(new NavigationItemViewModel("Dashboard", "IconDashboard", typeof(DashboardViewModel), _navigationStore));

            if (FacilityContext.CurrentFacility == FacilityType.Restaurant)
            {
                NavigationItems.Add(new NavigationItemViewModel("Tables", "IconTable", typeof(TablesViewModel), _navigationStore));
                NavigationItems.Add(new NavigationItemViewModel("KDS", "IconKds", typeof(KitchenDisplayViewModel), _navigationStore));
            }
            else if (FacilityContext.CurrentFacility == FacilityType.Salon)
            {
                NavigationItems.Add(new NavigationItemViewModel("Appointments", "IconCalendar", typeof(AppointmentsViewModel), _navigationStore));
                NavigationItems.Add(new NavigationItemViewModel("Services", "IconScissors", typeof(ServicesViewModel), _navigationStore));
            }
            else
            {
                NavigationItems.Add(new NavigationItemViewModel("Access Control", "IconTurnstile", typeof(AccessControlViewModel), _navigationStore));
                NavigationItems.Add(new NavigationItemViewModel("Clients", "IconMembers", typeof(MembersViewModel), _navigationStore));
                NavigationItems.Add(new NavigationItemViewModel("Registrations", "IconRegistrations", typeof(RegistrationsViewModel), _navigationStore));
            }

            NavigationItems.Add(new NavigationItemViewModel("History", "IconHistory", typeof(HistoryViewModel), _navigationStore));
            NavigationItems.Add(new NavigationItemViewModel("Finance & Staff", "IconFinance", typeof(FinanceAndStaffViewModel), _navigationStore));
            NavigationItems.Add(new NavigationItemViewModel("Shop", "IconShop", typeof(ShopViewModel), _navigationStore));
            NavigationItems.Add(new NavigationItemViewModel("Settings", "IconSettings", typeof(SettingsViewModel), _navigationStore));
        }

        private void ClearSelection()
        {
            // Requirement 4: SelectionRule
            // In a real app, we might proxy this to the Active ViewModel if it implements ISelectable
            if (_navigationStore.CurrentViewModel is MembersViewModel membersVm)
            {
                membersVm.ClearSelectionCommand.Execute(null);
            }
        }

        // --- 6. PLACEHOLDER ACTIONS ---
        // These would trigger Modal Services in a full implementation

        private async void ExecuteAddMember() 
        { 
            await _dialogService.ShowAlertAsync("Registration form will appear here in the next module.", "Development Info");
        }
        private void ExecuteAddPayment() { /* _modalService.Show<AddPaymentViewModel>(); */ }
        private void ExecuteAddProduct() { /* _modalService.Show<AddProductViewModel>(); */ }
        private void ExecuteNewOrder() { /* Open NewOrderModal */ }

        public string TerminologyTitle => FacilityContext.CurrentFacility == FacilityType.Salon ? "Client" : "Member";
        public string MemberLabel => TerminologyTitle;
        public string MemberPluralLabel => TerminologyTitle + "s";

        private async void ExecuteLogout()
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
    }
}
