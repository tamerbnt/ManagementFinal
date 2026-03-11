using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Application.Interfaces.App;
using Management.Application.Interfaces.ViewModels;
using Management.Application.Services;
using Management.Domain.Models.Restaurant;
using Management.Domain.Services;
using Management.Presentation.Extensions;
using Management.Presentation.Services;
using Management.Presentation.Services.State;
using Management.Presentation.Stores;
using Management.Presentation.Services.Localization;
using Microsoft.Extensions.Logging;
using System;
using Management.Presentation.ViewModels.Base;
using Management.Application.Interfaces;
using CommunityToolkit.Mvvm.Messaging;
using Management.Presentation.Messages;

namespace Management.Presentation.ViewModels.Restaurant
{
    public partial class TableTileViewModel : ObservableObject
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        [ObservableProperty] private string _tableNumber = string.Empty;
        [ObservableProperty] private int _capacity = 4;
        [ObservableProperty] private int _guestCount = 0;
        [ObservableProperty] private decimal _orderTotal = 0;
        [ObservableProperty] private bool _hasOrder = false;
        [ObservableProperty] private string _serverInitials = string.Empty;
        [ObservableProperty] private string _elapsedTime = string.Empty;
        [ObservableProperty] private TableStatus _status = TableStatus.Available;
        [ObservableProperty] private string _section = "Main Hall";   // â†  which floor section this table belongs to

        public bool IsOccupied => Status is TableStatus.Occupied or TableStatus.OrderSent or TableStatus.BillRequested or TableStatus.Ready;
        public bool ShowTimer  => IsOccupied && !string.IsNullOrEmpty(ElapsedTime);
        public string GuestDisplay => IsOccupied ? $"{GuestCount}/{Capacity}" : $"/{Capacity}";

        partial void OnStatusChanged(TableStatus value)
        {
            OnPropertyChanged(nameof(IsOccupied));
            OnPropertyChanged(nameof(ShowTimer));
            OnPropertyChanged(nameof(GuestDisplay));
        }
    }

    public partial class FloorPlanViewModel : FacilityAwareViewModelBase, IAsyncViewModel, IRecipient<TableStatusChangedMessage>
    {
        private readonly ModalNavigationStore _modalNavigationStore;
        private readonly TableDetailViewModel _tableDetailViewModel;
        private readonly AddTableViewModel    _addTableViewModel;
        private readonly ITableService        _tableService;
        private readonly IOrderService        _orderService;
        private readonly ISyncService         _syncService;
        private readonly SessionManager        _sessionManager;
        private readonly IDispatcher          _dispatcher;

        // All tables across all sections
        [ObservableProperty]
        private ObservableCollection<TableTileViewModel> _tables = new();

        // The filtered view shown in the canvas â€” only the active section
        [ObservableProperty]
        private ObservableCollection<TableTileViewModel> _filteredTables = new();

        [ObservableProperty] private int _totalTables;
        [ObservableProperty] private int _occupiedTables;
        [ObservableProperty] private int _availableTables;

        // Available section names
        public static readonly string[] Sections = { "Main Hall", "Patio", "Bar Seating" };

        [ObservableProperty] private string _activeSection = "Main Hall";

        public FloorPlanViewModel(
            ITerminologyService terminologyService,
            ILocalizationService localizationService,
            ILogger<FloorPlanViewModel> logger,
            IDiagnosticService diagnosticService,
            IToastService toastService,
            ModalNavigationStore modalNavigationStore,
            TableDetailViewModel tableDetailViewModel,
            AddTableViewModel addTableViewModel,
            ITableService tableService,
            IOrderService orderService,
            ISyncService syncService,
            IFacilityContextService facilityContext,
            SessionManager sessionManager,
            IDispatcher dispatcher) : base(terminologyService, facilityContext, logger, diagnosticService, toastService, localizationService)
        {
            _modalNavigationStore = modalNavigationStore;
            _tableDetailViewModel = tableDetailViewModel;
            _addTableViewModel    = addTableViewModel;
            _tableService         = tableService;
            _orderService         = orderService;
            _syncService          = syncService;
            _sessionManager       = sessionManager;
            _dispatcher           = dispatcher;
            
            Title = GetTerm("Strings.Restaurant.FloorPlan");

            _syncService.SyncCompleted += OnSyncCompleted;
            WeakReferenceMessenger.Default.Register<TableStatusChangedMessage>(this);
        }

        protected override void OnLanguageChanged()
        {
            base.OnLanguageChanged();
            Title = GetTerm("Strings.Restaurant.FloorPlan");
            // Setting active section triggers a refresh using OnPropertyChanged(nameof(ActiveSection)) implicitly via SetSection, but let's just refresh view
            RefreshView();
        }

        public async Task InitializeAsync()
        {
            IsActive = true;
            await LoadTables();
        }

        private async Task LoadTables()
        {
            var facilityId = _facilityContext.CurrentFacilityId;
            if (facilityId == Guid.Empty) return;

            // Run DB queries on a background thread to avoid blocking the UI
            var models = await Task.Run(() => _tableService.GetTablesAsync(facilityId));
            var activeOrdersResult = await Task.Run(() => _orderService.GetActiveOrdersAsync(facilityId));

            var activeOrders = activeOrdersResult.IsSuccess && activeOrdersResult.Value != null
                ? activeOrdersResult.Value.ToList()
                : new List<RestaurantOrderDto>();

            // Marshal UI update back to the UI thread via InvokeAsync (non-blocking)
            await _dispatcher.InvokeAsync(() =>
            {
                var existingTableIds = Tables.Select(t => t.Id).ToHashSet();
                var incomingTableIds = models.Select(m => m.Id).ToHashSet();

                // 1. Remove tables that no longer exist
                var toRemove = Tables.Where(t => !incomingTableIds.Contains(t.Id)).ToList();
                foreach (var r in toRemove) Tables.Remove(r);

                // 2. Upsert (Update existing or Add new)
                foreach (var m in models)
                {
                    var activeOrder = activeOrders.FirstOrDefault(o => o.TableId == m.Id);
                    var existing = Tables.FirstOrDefault(t => t.Id == m.Id);

                    if (existing != null)
                    {
                        // Update existing instance to preserve UI state/selection
                        existing.TableNumber = m.Label;
                        existing.Capacity = m.MaxSeats;
                        existing.Status = m.Status;
                        existing.Section = m.Section;
                        existing.OrderTotal = activeOrder?.Total ?? 0;
                        existing.HasOrder = activeOrder != null;
                        existing.GuestCount = activeOrder?.PartySize ?? 0;
                        existing.ServerInitials = !string.IsNullOrEmpty(activeOrder?.ServerName)
                            ? new string(activeOrder.ServerName.Split(' ').Select(s => s.FirstOrDefault()).ToArray()).ToUpper()
                            : string.Empty;
                        existing.ElapsedTime = activeOrder != null ? $"{(DateTime.UtcNow - activeOrder.CreatedAt).TotalMinutes:F0}m" : string.Empty;
                    }
                    else
                    {
                        // Add new entry
                        Tables.Add(new TableTileViewModel
                        {
                            Id = m.Id,
                            TableNumber = m.Label,
                            Capacity = m.MaxSeats,
                            Status = m.Status,
                            Section = m.Section,
                            OrderTotal = activeOrder?.Total ?? 0,
                            HasOrder = activeOrder != null,
                            GuestCount = activeOrder?.PartySize ?? 0,
                            ServerInitials = !string.IsNullOrEmpty(activeOrder?.ServerName)
                                ? new string(activeOrder.ServerName.Split(' ').Select(s => s.FirstOrDefault()).ToArray()).ToUpper()
                                : string.Empty,
                            ElapsedTime = activeOrder != null ? $"{(DateTime.UtcNow - activeOrder.CreatedAt).TotalMinutes:F0}m" : string.Empty
                        });
                    }
                }
                RefreshView();
            });
        }

        // Called when a section tab is clicked â€” switches the active section
        [RelayCommand]
        private void SetSection(string section)
        {
            if (ActiveSection == section) return;
            ActiveSection = section;
            RefreshView();
        }

        partial void OnActiveSectionChanged(string value)
        {
            RefreshView();
        }

        [RelayCommand]
        private async Task OpenTableDetailAsync(TableTileViewModel? tile)
        {
            if (tile is null) return;
            await _tableDetailViewModel.LoadFromTile(tile);
            await _modalNavigationStore.OpenAsync(_tableDetailViewModel);
            RefreshView();
        }

        [RelayCommand]
        private async Task AddTableAsync()
        {
            // Pre-set the section on the form to match the current tab
            _addTableViewModel.TableNumber   = string.Empty;
            _addTableViewModel.Capacity      = 4;
            _addTableViewModel.SelectedShape = "Square";
            _addTableViewModel.SelectedSection = ActiveSection;

            var result = await _modalNavigationStore.OpenAsync(_addTableViewModel);

            if (result.IsSuccess && result.Data is TableTileViewModel newTile)
            {
                var model = new TableModel
                {
                    Id = newTile.Id,
                    Label = newTile.TableNumber,
                    Section = newTile.Section,
                    MaxSeats = newTile.Capacity,
                    Status = newTile.Status,
                    TableNumber = int.TryParse(newTile.TableNumber, out int num) ? num : 0,
                    FacilityId = _facilityContext.CurrentFacilityId,
                    TenantId = _sessionManager.CurrentTenantId
                };

                var saved = await _tableService.AddTableAsync(model);
                if (saved)
                {
                    Tables.Add(newTile);
                    RefreshView();
                    _toastService?.ShowSuccess($"Table {newTile.TableNumber} added to {newTile.Section}.");
                }
                else
                {
                    _toastService?.ShowError("Failed to add table. Please try again.");
                }
            }
        }

        // Rebuilds FilteredTables from the master Tables list, then refreshes stats
        private void RefreshView()
        {
            var currentSectionItems = Tables.Where(t => t.Section == ActiveSection).ToList();
            
            // Update the filtered collection in-place to avoid breaking UI bindings
            var toRemove = FilteredTables.Where(ft => !currentSectionItems.Any(cs => cs.Id == ft.Id)).ToList();
            foreach (var r in toRemove) FilteredTables.Remove(r);

            foreach (var item in currentSectionItems)
            {
                if (!FilteredTables.Contains(item))
                {
                    FilteredTables.Add(item);
                }
            }

            RefreshStats();
        }

        public void RefreshStats()
        {
            TotalTables     = FilteredTables.Count;
            OccupiedTables  = FilteredTables.Count(t => t.IsOccupied);
            AvailableTables = FilteredTables.Count(t => t.Status == TableStatus.Available);
        }

        private void OnSyncCompleted(object? sender, EventArgs e)
        {
            if (!ShouldRefreshOnSync()) return;
            _ = _dispatcher.InvokeAsync(async () =>
            {
                if (IsDisposed) return;
                _logger?.LogInformation("[FloorPlan] Sync debounce passed, refreshing tables...");
                await LoadTables();
            });
        }

        public void Receive(TableStatusChangedMessage message)
        {
            if (message.Value == _facilityContext.CurrentFacilityId)
            {
                // Dispatch the whole reload through InvokeAsync so it is scheduled
                // on the UI thread properly. We add a tiny delay to ensure a 
                // consistent DB state across different async contexts.
                _ = _dispatcher.InvokeAsync(async () => 
                {
                    await Task.Delay(200); // Wait for DB write stability
                    await LoadTables();
                });
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _syncService.SyncCompleted -= OnSyncCompleted;
                    WeakReferenceMessenger.Default.Unregister<TableStatusChangedMessage>(this);
                }
            }
            base.Dispose(disposing);
        }
    }
}
