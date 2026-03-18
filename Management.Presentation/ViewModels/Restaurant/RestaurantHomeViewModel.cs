using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Linq;
using Management.Application.DTOs;
using Management.Application.Interfaces;
using Management.Application.Interfaces.App;
using Management.Application.Services;
using Management.Domain.Interfaces;
using Management.Domain.Models.Restaurant;
using Management.Presentation.Helpers;
using Management.Domain.Services;
using Management.Presentation.Extensions;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Management.Presentation.ViewModels.Shared;
using Management.Presentation.Services;
using Management.Presentation.Services.State;
using Management.Presentation.ViewModels.Settings;
using Management.Presentation.Stores;
using Management.Application.Interfaces.ViewModels;
using Management.Presentation.Services.Localization;
using Management.Presentation.ViewModels.Base;
using CommunityToolkit.Mvvm.Messaging;
using Management.Presentation.Messages;
using Management.Domain.Models;

namespace Management.Presentation.ViewModels.Restaurant
{
    public partial class RestaurantHomeViewModel : FacilityAwareViewModelBase,
        IFacilityHomeViewModel,
        IAsyncViewModel,
        IStateResettable,
        IRecipient<RefreshRequiredMessage<Sale>>,
        IRecipient<RefreshRequiredMessage<Member>>,
        IRecipient<RefreshRequiredMessage<Registration>>,
        IRecipient<RefreshRequiredMessage<PayrollEntry>>,
        IRecipient<RefreshRequiredMessage<InventoryPurchaseDto>>,
        IRecipient<FacilityActionCompletedMessage>,
        IRecipient<TableStatusChangedMessage>
    {
        public ObservableRangeCollection<IActivityItem> ActivityStream { get; } = new();

        private readonly ITableService _tableService;
        private readonly IOrderService _orderService;
        private readonly IEnumerable<IHistoryProvider> _historyProviders;
        private readonly ModalNavigationStore _modalNavigationStore;
        private readonly INavigationService _navigationService;
        private readonly SessionManager _sessionManager;
        private readonly IDispatcher _dispatcher;
        private readonly IDiagnosticService _diagnosticService;
        private readonly ISyncService _syncService;
        private CancellationTokenSource? _refreshDebounceCts;

        [ObservableProperty] private int _activeTablesCount;
        [ObservableProperty] private decimal _revenueToday;
        [ObservableProperty] private int _openOrdersCount;

        [ObservableProperty]
        private string _scanInput = string.Empty;

        public IAsyncRelayCommand ScanCommand { get; }




        public string CurrentTime => DateTime.Now.ToString("HH:mm");
        public string CurrentDate => DateTime.Now.ToString("dddd, MMMM dd");

        public RestaurantHomeViewModel(
            ITableService tableService,
            IOrderService orderService,
            INavigationService navigationService,
            IFacilityContextService facilityContext,
            IEnumerable<IHistoryProvider> historyProviders,
            ITerminologyService terminologyService,
            IToastService toastService,
            ModalNavigationStore modalNavigationStore,
            SessionManager sessionManager,
            IDispatcher dispatcher,
            IDiagnosticService diagnosticService,
            ILogger<RestaurantHomeViewModel> logger,
            ISyncService syncService,
            ILocalizationService? localizationService = null)
            : base(terminologyService, facilityContext, logger, diagnosticService, toastService, localizationService)
        {
            _tableService = tableService ?? throw new ArgumentNullException(nameof(tableService));
            _orderService = orderService ?? throw new ArgumentNullException(nameof(orderService));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _historyProviders = historyProviders ?? throw new ArgumentNullException(nameof(historyProviders));
            _modalNavigationStore = modalNavigationStore ?? throw new ArgumentNullException(nameof(modalNavigationStore));
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _diagnosticService = diagnosticService ?? throw new ArgumentNullException(nameof(diagnosticService));
            _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));

            _syncService.SyncCompleted += OnSyncCompleted;
            _facilityContext.FacilityChanged += OnFacilityChanged;

            ScanCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(() => Task.CompletedTask);

            WeakReferenceMessenger.Default.RegisterAll(this);
        }

        private void OnSyncCompleted(object? sender, EventArgs e)
        {
            if (IsDisposed || !ShouldRefreshOnSync()) return;
            _dispatcher.InvokeAsync(async () =>
            {
                if (IsDisposed || IsLoading) return;
                _logger?.LogInformation("[RestaurantHome] Sync debounce passed, refreshing metrics and activity...");
                await InitializeAsync();
            });
        }

        [RelayCommand]
        public async Task OnLoadedAsync()
        {
            IsActive = true;
            await InitializeAsync();
        }

        public async Task InitializeAsync()
        {
            // FIX Step 3.1: Guard against loading before FacilityId is resolved
            if (CurrentFacilityId == Guid.Empty)
            {
                _logger?.LogWarning("[RestaurantHome] InitializeAsync aborted: FacilityId is Guid.Empty.");
                return;
            }

            // SAFETY: No hardcoded Task.Delay here. Initialization triggered by Loaded event.
            await Task.WhenAll(
                LoadMetricsAsync(),
                LoadRecentActivityAsync()
            );
        }

        private async Task LoadMetricsAsync()
        {
            await ExecuteLoadingAsync(async () =>
            {
                var tables = await _tableService.GetTablesAsync(CurrentFacilityId);
                var activeTables = tables.Count(t => t.Status != TableStatus.Available);

                var activeOrdersResult = await _orderService.GetActiveOrdersAsync(CurrentFacilityId);
                
                // Revenue calculation: Use local date bounds to sync with "Today"
                var startLocal = DateTime.Today.ToUniversalTime();
                var endLocal = DateTime.Today.AddDays(1).AddTicks(-1).ToUniversalTime();
                var revenueResult = await _orderService.GetTodayRevenueAsync(CurrentFacilityId, startLocal, endLocal);
                
                var activeOrdersCount = activeOrdersResult.IsSuccess && activeOrdersResult.Value != null 
                    ? activeOrdersResult.Value.Count() 
                    : 0;

                var todayRevenue = revenueResult.IsSuccess ? revenueResult.Value : 0;

                await _dispatcher.InvokeAsync(() =>
                {
                    ActiveTablesCount = activeTables;
                    OpenOrdersCount = activeOrdersCount;
                    RevenueToday = todayRevenue;
                });
            });
        }

        private async Task LoadRecentActivityAsync()
        {
            try
            {
                // 1. Resolve Provider
                var segmentName = CurrentFacility.ToString();
                var provider = _historyProviders.FirstOrDefault(p => p.SegmentName == segmentName);
                if (provider == null) return;

                // 2. Fetch Last 48 Hours to ensure coverage
                var recentEvents = await provider.GetHistoryAsync(CurrentFacilityId, DateTime.UtcNow.AddDays(-3), DateTime.UtcNow);

                var logItems = recentEvents.Take(50).Select(e => 
                {
                    var icon = e.Type switch
                    {
                        HistoryEventType.Access => e.IsSuccessful ? "✅" : "❌",
                        HistoryEventType.Payment => "🛒",
                        HistoryEventType.Order => "🛒",
                        _ => "✨"
                    };

                    var initials = e.Type switch
                    {
                        HistoryEventType.Access => new string((e.Title ?? "??").Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(s => s[0]).Take(2).ToArray()).ToUpper(),
                        HistoryEventType.Payment => "$$",
                        HistoryEventType.Order => "$$",
                        _ => "??"
                    };

                    var resolvedTitle = !string.IsNullOrEmpty(e.TitleLocalizationKey)
                        ? string.Format(_terminologyService.GetTerm(e.TitleLocalizationKey), e.TitleLocalizationArgs ?? Array.Empty<object>())
                        : e.Title;

                    var resolvedDetails = !string.IsNullOrEmpty(e.DetailsLocalizationKey)
                         ? string.Format(_terminologyService.GetTerm(e.DetailsLocalizationKey), e.DetailsLocalizationArgs ?? Array.Empty<object>())
                         : e.Details;

                    var subtitle = e.Amount.HasValue && e.Amount > 0 
                        ? $"{e.Amount:N0} DA - {resolvedDetails}"
                        : resolvedDetails;

                    return new ActivityLogItem(resolvedTitle, subtitle, icon, initials)
                    {
                        Timestamp = e.Timestamp.ToLocalTime().ToString("HH:mm"),
                        SortDate = e.Timestamp
                    };
                }).ToList();

                await _dispatcher.InvokeAsync(() =>
                {
                    ActivityStream.ReplaceRange(logItems);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load restaurant activity stream from HistoryProvider");
            }
        }

        [RelayCommand]
        private async Task NewTableAsync()
        {
            // Navigate to Floor Plan to add a table if none exist, or show selection modal
            var tables = await _tableService.GetTablesAsync(CurrentFacilityId);
            if (!tables.Any())
            {
                await _navigationService.NavigateToAsync<FloorPlanViewModel>();
                _toastService?.ShowInfo("Add some tables to your floor plan first.");
                return;
            }

            await _modalNavigationStore.OpenAsync<SelectTableViewModel>();
        }

        [RelayCommand]
        private async Task TakeoutAsync()
        {
            await _navigationService.NavigateToAsync<RestaurantOrderingViewModel>(Guid.Empty);
        }

        [RelayCommand]
        private async Task PayBillAsync()
        {
            await _modalNavigationStore.OpenAsync<OpenOrdersViewModel>();
        }

        [RelayCommand]
        private async Task MenuStatusAsync()
        {
            await _navigationService.NavigateToAsync<MenuManagementViewModel>();
        }

        public void ResetState()
        {
            IsActive = false;
            ActivityStream.Clear();
            ScanInput = string.Empty;
        }

        // ── Messenger Receive handlers ──────────────────────────────────────
        public void Receive(RefreshRequiredMessage<Sale> message)
        {
            if (message.Value != CurrentFacilityId) return;
            HandleRefreshAsync();
        }

        public void Receive(RefreshRequiredMessage<Member> message)
        {
            if (message.Value != CurrentFacilityId) return;
            HandleRefreshAsync();
        }

        public void Receive(RefreshRequiredMessage<Registration> message)
        {
            if (message.Value != CurrentFacilityId) return;
            HandleRefreshAsync();
        }

        public void Receive(RefreshRequiredMessage<PayrollEntry> message)
        {
            if (message.Value != CurrentFacilityId) return;
            HandleRefreshAsync();
        }

        public void Receive(RefreshRequiredMessage<InventoryPurchaseDto> message)
        {
            if (message.Value != CurrentFacilityId) return;
            HandleRefreshAsync();
        }

        public void Receive(FacilityActionCompletedMessage message)
        {
            if (message.Value != CurrentFacilityId) return;
            HandleRefreshAsync();
        }

        public void Receive(TableStatusChangedMessage message)
        {
            if (message.Value != CurrentFacilityId) return;
            HandleRefreshAsync();
        }

        private void HandleRefreshAsync()
        {
            _refreshDebounceCts?.Cancel();
            _refreshDebounceCts = new CancellationTokenSource();
            var token = _refreshDebounceCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(300, token);
                    if (token.IsCancellationRequested || IsDisposed) return;
                    await _dispatcher.InvokeAsync(async () =>
                    {
                        if (!IsDisposed) await InitializeAsync();
                    });
                }
                catch (TaskCanceledException) { }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "[RestaurantHome] Error during background refresh");
                }
            }, token);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _refreshDebounceCts?.Cancel();
                _refreshDebounceCts?.Dispose();
                WeakReferenceMessenger.Default.UnregisterAll(this);
                if (_facilityContext != null)
                {
                    _facilityContext.FacilityChanged -= OnFacilityChanged;
                }
                if (_syncService != null)
                {
                    _syncService.SyncCompleted -= OnSyncCompleted;
                }
            }
            base.Dispose(disposing);
        }

        private void OnFacilityChanged(Management.Domain.Enums.FacilityType type)
        {
            if (IsDisposed) return;
            _logger?.LogInformation("[RestaurantHome] FacilityChanged event received ({Type}).", type);
            var newFacilityId = _facilityContext.CurrentFacilityId;
            
            if (newFacilityId != Guid.Empty)
            {
                _logger?.LogInformation("[RestaurantHome] FacilityId resolved ({Id}). Reloading data.", newFacilityId);
                _dispatcher.InvokeAsync(async () => 
                {
                    if (IsDisposed) return;
                    await InitializeAsync();
                });
            }
        }
    }
}
