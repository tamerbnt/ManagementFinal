using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Application.Stores;
using Management.Application.Interfaces.App;
using Management.Application.Services;
using Management.Presentation.Services.Application;
using Microsoft.EntityFrameworkCore;
using Management.Presentation.Services.State;
using Management.Presentation.Stores;
using Management.Presentation.ViewModels.Shared;
using Microsoft.Extensions.DependencyInjection;
using Management.Domain.Services;
using Management.Presentation.Services;
using Management.Presentation.Services.Localization;
using Microsoft.Extensions.Logging;
using RelayCommand = CommunityToolkit.Mvvm.Input.RelayCommand;
using AsyncRelayCommand = CommunityToolkit.Mvvm.Input.AsyncRelayCommand;
using IConnectionService = Management.Domain.Services.IConnectionService;
using Management.Infrastructure.Services;
using Management.Application.DTOs;
using Management.Presentation.Services.Navigation;
using Management.Presentation.Extensions;
using Management.Presentation.ViewModels;
using Management.Presentation.ViewModels.Base;

namespace Management.Presentation.ViewModels.Shell
{
    public partial class TopBarViewModel : FacilityAwareViewModelBase
    {
        [ObservableProperty]
        private double _height = 80;

        [ObservableProperty]
        private int _notificationCount;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(UserInitials))]
        private string _userName;

        [ObservableProperty]
        private string _userRole;

        [ObservableProperty]
        private bool _isOnline = true; // Assume online by default

        [ObservableProperty]
        private bool _isRfidConnected;

        [ObservableProperty]
        private bool _isPrinterConnected; // Future use or mock

        [ObservableProperty]
        // Phase 1: Manual Sync Support
        private bool _isSyncing = false;

        [ObservableProperty]
        private DateTime? _lastSyncTime;

        public string LastSyncText => LastSyncTime.HasValue 
            ? string.Format(_terminologyService.GetTerm("Terminology.TopBar.Sync.Format"), GetTimeAgo(LastSyncTime.Value))
            : _terminologyService.GetTerm("Terminology.TopBar.Sync.Never");

        public string UserInitials => !string.IsNullOrEmpty(UserName) && UserName.Length > 0 
            ? UserName[0].ToString().ToUpper() 
            : "A";

        public IEnumerable<NotificationItem> Notifications => _notificationStore.Notifications;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private bool _isSearchPopupOpen;

        [ObservableProperty]
        private bool _isSearchLoading;

        public ObservableCollection<SearchResultDto> SearchResults { get; } = new();

        private readonly IBreadcrumbService _breadcrumbService;
        private readonly IConnectionService _connectionService;
        private readonly Management.Domain.Services.IRfidReader _rfidReader;
        private readonly SessionManager _sessionManager;
        private readonly IServiceProvider _serviceProvider;
        private readonly Management.Application.Interfaces.App.ISyncService _syncService;
        private readonly Management.Application.Stores.NotificationStore _notificationStore;
        private readonly Management.Presentation.Services.IModalNavigationService _modalNavigationService;
        private readonly Management.Application.Services.IAuthenticationService _authenticationService;
        private readonly ISearchService _searchService;
        private readonly INavigationService _navigationService;
        private Timer? _notificationTimer;
        private CancellationTokenSource? _searchCts;

        protected override void OnLanguageChanged()
        {
            UpdateUserInfo();
            OnPropertyChanged(nameof(LastSyncText));
        }

        public IRelayCommand ToggleCommandPaletteCommand { get; set; }
        public IRelayCommand ToggleNotificationsCommand { get; set; }
        public IRelayCommand MarkAsReadCommand { get; set; }
        public IRelayCommand OpenProfileMenuCommand { get; set; }
        public IRelayCommand OpenSettingsCommand { get; set; }
        public IRelayCommand OpenAccountSettingsCommand { get; set; }
        public IRelayCommand CloseSettingsCommand { get; set; }
        public IRelayCommand MarkAllAsReadCommand { get; set; }
        public CommunityToolkit.Mvvm.Input.AsyncRelayCommand LogoutCommand { get; }
        public IRelayCommand<BreadcrumbItem> NavigateToBreadcrumbCommand { get; }
        public CommunityToolkit.Mvvm.Input.AsyncRelayCommand ManualSyncCommand { get; }

        public TopBarViewModel(
            IBreadcrumbService breadcrumbService,
            IConnectionService connectionService,
            Management.Domain.Services.IRfidReader rfidReader,
            SessionManager sessionManager,
            IServiceProvider serviceProvider,
            Management.Application.Interfaces.App.ISyncService syncService,
            Management.Application.Interfaces.App.IToastService toastService,
            Management.Application.Stores.NotificationStore notificationStore,
            Management.Presentation.Services.IModalNavigationService modalNavigationService,
            Management.Application.Services.IAuthenticationService authenticationService,
            Management.Domain.Services.IDialogService dialogService,
            ITerminologyService terminologyService,
            ISearchService searchService,
            INavigationService navigationService,
            ILogger<TopBarViewModel> logger,
            IDiagnosticService diagnosticService,
            IFacilityContextService facilityContext,
            ILocalizationService localizationService) 
            : base(terminologyService, facilityContext, logger, diagnosticService, toastService, localizationService)
        {
            _breadcrumbService = breadcrumbService;
            _connectionService = connectionService;
            _rfidReader = rfidReader;
            _sessionManager = sessionManager;
            _serviceProvider = serviceProvider;
            _syncService = syncService;
            _notificationStore = notificationStore;
            _modalNavigationService = modalNavigationService;
            _authenticationService = authenticationService;
            _searchService = searchService;
            _navigationService = navigationService;

            _notificationStore.UnreadCountChanged += () =>
            {
                NotificationCount = _notificationStore.UnreadCount;
                OnPropertyChanged(nameof(Notifications));
            };
            NotificationCount = _notificationStore.UnreadCount;

            _sessionManager.PropertyChanged += OnSessionPropertyChanged;
            UpdateUserInfo();

            // Initial State
            IsOnline = _connectionService.IsOnline();
            
            IsRfidConnected = _rfidReader.IsConnected;

            // Subscription
            _connectionService.ConnectionStatusChanged += OnConnectionStatusChanged;
            _connectionService.SupabaseStatusChanged += OnSupabaseStatusChanged;
            _rfidReader.ConnectionStatusChanged += OnRfidConnectionStatusChanged;

            // Default implementation (can be overridden by MainViewModel)
            ToggleCommandPaletteCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => { });
            ToggleNotificationsCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => 
            {
                // Toggle logic handled by Popup in XAML
                // But we can use this to mark as read if we want
            });
            MarkAsReadCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<NotificationItem>(item =>
            {
                if (item != null)
                {
                    item.IsRead = true;
                    _notificationStore.MarkAsRead(item.Id);
                    
                    // Open the detail modal using the standard service method
                    _ = _modalNavigationService.OpenModalAsync<NotificationDetailViewModel>(parameter: item);
                }
            });
            MarkAllAsReadCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => 
            {
                _notificationStore.MarkAllAsRead();
            });
            OpenProfileMenuCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => { });
            OpenSettingsCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => { });
            OpenAccountSettingsCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => { });
            CloseSettingsCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => { });

            NavigateToBreadcrumbCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<BreadcrumbItem>(item =>
            {
                if (item?.ViewModelType != null && !item.IsActive)
                {
                    if (_breadcrumbService is Management.Presentation.Services.Application.BreadcrumbService bs)
                    {
                        bs.Navigate(item.ViewModelType);
                    }
                }
            });

            LogoutCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(async () => 
            {
                // Show confirmation dialog
                var modalStore = _serviceProvider.GetRequiredService<ModalNavigationStore>();
                var confirmVm = _serviceProvider.GetRequiredService<ConfirmationModalViewModel>();
                confirmVm.Configure(
                    title: _terminologyService.GetTerm("Terminology.TopBar.Logout.Title"),
                    message: _terminologyService.GetTerm("Terminology.TopBar.Logout.Message"),
                    confirmText: _terminologyService.GetTerm("Terminology.TopBar.Logout.Confirm"),
                    cancelText: _terminologyService.GetTerm("Terminology.Global.Cancel"),
                    isDestructive: true
                );

                var modalResult = await modalStore.OpenAsync(confirmVm);
                
                if (modalResult.IsSuccess)
                {
                    // Force a final sync push before clearing session
                    try
                    {
                    _toastService.ShowWarning(_terminologyService.GetTerm("Terminology.TopBar.Status.Syncing"));
                        await _syncService.PushChangesAsync(CancellationToken.None);
                    }
                    catch { /* Ignore sync errors on logout, we tried */ }

                    await _authenticationService.LogoutAsync();
                    _sessionManager.Clear();
                    
                    // CRITICAL: Reset all stateful Singletons (State Isolation)
                    try 
                    {
                        var resettables = _serviceProvider.GetServices<Management.Domain.Interfaces.IStateResettable>();
                        foreach (var resettable in resettables)
                        {
                            _logger.LogInformation("Resetting state for {ResettableType}", resettable.GetType().Name);
                            resettable.ResetState();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to reset state during logout.");
                    }

                    // Reset UI to Guest
                    UpdateUserInfo();

                    // Switch to Auth Shell
                    if (System.Windows.Application.Current is App app)
                    {
                        app.Logout();
                    }
                }
            });

            // Phase 1: Manual Sync Command
            ManualSyncCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(PerformManualSyncAsync, () => !IsSyncing);

            // Start notification count polling (every 5 seconds)
            _notificationTimer = new Timer(async _ => await UpdateNotificationCountAsync(), null, TimeSpan.Zero, TimeSpan.FromSeconds(5));

            ToggleCommandPaletteCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => { }); // Keeping for compatibility but search bar is primary
            ExecuteSearchItemCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<SearchResultDto>(ExecuteSearchItem);
        }

        public IRelayCommand<SearchResultDto> ExecuteSearchItemCommand { get; }

        partial void OnSearchQueryChanged(string? oldValue, string newValue)
        {
            // We allow empty strings now to trigger the "Static Commands" (initial focus behavior)
            DebounceSearch(newValue ?? string.Empty);
        }

        private void DebounceSearch(string query)
        {
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(300, token);
                    if (token.IsCancellationRequested) return;

                    await ExecuteSearchAsync(query, token);
                }
                catch (TaskCanceledException) { }
            }, token);
        }

        private async Task ExecuteSearchAsync(string query, CancellationToken token)
        {
            IsSearchLoading = true;
            try
            {
                var results = await _searchService.SearchAsync(query, _facilityContext.CurrentFacility);
                if (token.IsCancellationRequested) return;

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    SearchResults.Clear();
                    foreach (var result in results)
                    {
                        SearchResults.Add(result);
                    }
                    
                    // Only open if there are results and the query isn't null
                    IsSearchPopupOpen = SearchResults.Any();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Global Search Execution Error: {ex.Message}");
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    SearchResults.Clear();
                    IsSearchPopupOpen = false;
                });
            }
            finally
            {
                IsSearchLoading = false;
            }
        }

        private void ExecuteSearchItem(SearchResultDto? item)
        {
            if (item == null) return;
            IsSearchPopupOpen = false;

            var param = item.ActionParameter?.ToString();
            if (item.ActionKey != "Nav" || string.IsNullOrEmpty(param)) return;

            var parts = param.Split('|');
            var viewName = parts[0];
            // Pass the rest of the parts as the parameter
            var entityId = parts.Length > 1 ? string.Join('|', parts.Skip(1)) : null;

            switch (viewName)
            {
                case "DashboardView":
                    _ = _navigationService.NavigateToAsync<Management.Presentation.ViewModels.GymHome.GymHomeViewModel>();
                    break;
                case "MembersView":
                    _ = _navigationService.NavigateToAsync<Management.Presentation.ViewModels.Members.MembersViewModel>(entityId);
                    break;
                case "FinanceAndStaffView":
                case "StaffView":
                    _ = _navigationService.NavigateToAsync<Management.Presentation.ViewModels.Finance.FinanceAndStaffViewModel>(entityId);
                    break;
                case "ShopView":
                case "PosView":
                case "ProductView":
                    _ = _navigationService.NavigateToAsync<Management.Presentation.ViewModels.Shop.ShopViewModel>(entityId);
                    break;
                case "SchedulerView":
                    _ = _navigationService.NavigateToAsync<Management.Presentation.ViewModels.Scheduler.SchedulerViewModel>(entityId);
                    break;
                case "MenuManagementView":
                    _ = _navigationService.NavigateToAsync<Management.Presentation.ViewModels.Settings.MenuManagementViewModel>(entityId);
                    break;
                case "SettingsView":
                    if (OpenSettingsCommand?.CanExecute(null) == true)
                    {
                        // Handle special case for plans: Plans_Guid
                        if (entityId != null && entityId.StartsWith("Plans_"))
                        {
                            var planId = entityId.Substring("Plans_".Length);
                            OpenSettingsCommand.Execute($"MembershipPlans|{planId}");
                        }
                        else
                        {
                            OpenSettingsCommand.Execute(entityId);
                        }
                    }
                    break;
                default:
                    // Fallback to basic nav if it's just a view name type string
                    break;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Unsubscribe from events
                if (_sessionManager != null)
                {
                    _sessionManager.PropertyChanged -= OnSessionPropertyChanged;
                }

                if (_connectionService != null)
                {
                    _connectionService.ConnectionStatusChanged -= OnConnectionStatusChanged;
                    _connectionService.SupabaseStatusChanged -= OnSupabaseStatusChanged;
                }

                if (_rfidReader != null)
                {
                    _rfidReader.ConnectionStatusChanged -= OnRfidConnectionStatusChanged;
                }

                // Stop timer
                _notificationTimer?.Dispose();
                _notificationTimer = null;
            }

            base.Dispose(disposing);
        }

        private void OnSessionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            UpdateUserInfo();
        }

        private async Task UpdateNotificationCountAsync()
        {
            try
            {
                if (_syncService == null) return;
                var count = await _syncService.GetPendingOutboxCountAsync();
                NotificationCount = count;
            }
            catch
            {
                // Silently fail to avoid crashing the UI
            }
        }

        private void UpdateUserInfo()
        {
            UserName = _sessionManager.CurrentUser?.FullName ?? _terminologyService.GetTerm("Terminology.TopBar.Guest");
            UserRole = _sessionManager.CurrentUser?.Role.ToString() ?? _terminologyService.GetTerm("Terminology.TopBar.Guest");
        }

        public void UpdateHeight(double offset)
        {
            Height = offset > 40 ? 56 : 80;
        }

        private void OnConnectionStatusChanged(bool isOnline)
        {
            // Use the enhanced service to check real connectivity
            IsOnline = _connectionService.IsOnline();
            _logger.LogInformation("Connection status changed: {IsOnline}", IsOnline);
        }

        private void OnSupabaseStatusChanged(bool isReachable)
        {
            var toastService = _serviceProvider.GetService<IToastService>();
            if (isReachable)
            {
                toastService?.ShowSuccess(_terminologyService.GetTerm("Terminology.TopBar.Connection.Restored"), "Cloud Online");
                _logger?.LogInformation("Supabase connection restored.");
            }
            else
            {
                toastService?.ShowError(_terminologyService.GetTerm("Terminology.TopBar.Connection.Lost"), "Cloud Offline");
                _logger?.LogWarning("Supabase connection lost.");
            }
            
            // Also update the visual online indicator if it depends on Supabase specifically
            IsOnline = _connectionService.IsOnline();
        }

        private void OnRfidConnectionStatusChanged(bool isConnected)
        {
            IsRfidConnected = isConnected;
        }

        // Phase 1: Manual Sync Implementation
        private async Task PerformManualSyncAsync()
        {
            if (IsSyncing) return;

            IsSyncing = true;
            ManualSyncCommand.NotifyCanExecuteChanged();

            try
            {
                var toastService = _serviceProvider.GetService<IToastService>();
                toastService?.ShowWarning(_terminologyService.GetTerm("Terminology.TopBar.Status.Syncing"), "Syncing");

                // DIAGNOSTIC START
                using (var scope = _serviceProvider.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<Management.Infrastructure.Data.AppDbContext>();
                    var tenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();
                    var facilityContext = scope.ServiceProvider.GetRequiredService<IFacilityContextService>();

                    var currentTenant = tenantService.GetTenantId();
                    var currentFacility = facilityContext.CurrentFacilityId;

                    _logger.LogWarning($"[DIAGNOSTIC] Current Context - Tenant: {currentTenant}, Facility: {currentFacility}");

                    var products = await db.Products.IgnoreQueryFilters().ToListAsync();
                    _logger.LogWarning($"[DIAGNOSTIC] Local Products Total (Raw): {products.Count}");

                    foreach (var p in products)
                    {
                        _logger.LogWarning($"[DIAGNOSTIC] Product: {p.Name} | Tenant: {p.TenantId} | Facility: {p.FacilityId} | Active: {p.IsActive}");
                    }
                }
                // DIAGNOSTIC END

                bool pushSuccess = false;
                bool pullSuccess = false;

                await Task.Run(async () =>
                {
                    pushSuccess = await _syncService.PushChangesAsync(CancellationToken.None);
                    pullSuccess = await _syncService.PullChangesAsync(CancellationToken.None);
                });

                if (!pushSuccess || !pullSuccess)
                {
                    // Error notifications are already shown by SyncService. 
                    // Just update the UI state and return.
                    LastSyncTime = DateTime.Now;
                    OnPropertyChanged(nameof(LastSyncText));
                    return;
                }

                var currentUser = _sessionManager.CurrentUser;

                // Check if sync was skipped due to offline mode
                var syncStatus = _syncService.Status;
                var pendingCount = await _syncService.GetPendingOutboxCountAsync();
                
                if (syncStatus == SyncStatus.Offline && pendingCount > 0)
                {
                    toastService?.ShowWarning(
                        $"Sync skipped: You are in offline mode. {pendingCount} item(s) are waiting to sync. Please login with email/password to enable cloud sync.",
                        "Sync Skipped - Offline Mode"
                    );
                    _logger.LogWarning($"[Sync] Sync skipped due to offline mode. {pendingCount} items pending sync.");
                    LastSyncTime = DateTime.Now;
                    OnPropertyChanged(nameof(LastSyncText));
                    return;
                }

                // DIAGNOSTIC CHECK
                int productCount = 0;
                int planCount = 0;
                string? displayTenantId = currentUser?.TenantId.ToString(); // Fixed CS0023

                using (var scope = _serviceProvider.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<Management.Infrastructure.Data.AppDbContext>();
                    productCount = await dbContext.Products.CountAsync();
                    planCount = await dbContext.MembershipPlans.CountAsync();
                    
                    // AUTO-RECOVERY: If still zero, reset sync date and try one HUGE pull
                    if (productCount == 0 || planCount == 0)
                    {
                        _logger?.LogWarning("Sync already in progress, skipping manual sync request.");
                        await _syncService.ResetSyncContextAsync();
                        await _syncService.PullChangesAsync(CancellationToken.None);
                        
                        // Re-check
                        productCount = await dbContext.Products.CountAsync();
                        planCount = await dbContext.MembershipPlans.CountAsync();
                    }

                    var logMsg = $"[DIAGNOSTIC] Sync Complete. Products: {productCount}, Plans: {planCount}, Tenant: {displayTenantId}, Facility: {currentUser?.FacilityId}";
                    _logger.LogWarning(logMsg);
                    Console.WriteLine(logMsg); // Backup log to console
                }

                LastSyncTime = DateTime.Now;
                OnPropertyChanged(nameof(LastSyncText));
                toastService?.ShowSuccess($"{_terminologyService.GetTerm("Terminology.TopBar.Status.SyncSuccess")}. Products: {productCount}, Plans: {planCount}. Tenant: {displayTenantId}", "Sync Complete");
            }
            catch (Exception ex)
            {
                var toastService = _serviceProvider.GetService<IToastService>();
                toastService?.ShowError($"Error: {ex.Message}", "Sync Failed");
                _logger.LogError(ex, "Manual sync failed");
            }
            finally
            {
                IsSyncing = false;
                ManualSyncCommand.NotifyCanExecuteChanged();
            }
        }

        private string GetTimeAgo(DateTime dateTime)
        {
            var timeSpan = DateTime.Now - dateTime;
            if (timeSpan.TotalSeconds < 60) return _terminologyService.GetTerm("Terminology.TopBar.Sync.JustNow");
            if (timeSpan.TotalMinutes < 60) return string.Format(_terminologyService.GetTerm("Terminology.TopBar.Sync.MinsAgo"), (int)timeSpan.TotalMinutes);
            if (timeSpan.TotalHours < 24) return string.Format(_terminologyService.GetTerm("Terminology.TopBar.Sync.HoursAgo"), (int)timeSpan.TotalHours);
            return string.Format(_terminologyService.GetTerm("Terminology.TopBar.Sync.DaysAgo"), (int)timeSpan.TotalDays);
        }
    }
}
