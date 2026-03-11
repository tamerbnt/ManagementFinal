using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Domain.Models.Restaurant;
using Management.Presentation.Extensions;
using Management.Presentation.Stores;
using Management.Application.Interfaces;
using Management.Application.Interfaces.App;
using Management.Application.Services;
using Management.Domain.Services;
using Management.Presentation.Services;
using Management.Presentation.Services.State;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.Messaging;
using Management.Presentation.Messages;

namespace Management.Presentation.ViewModels.Restaurant
{
    /// <summary>
    /// A single order line item displayed in the table detail popup.
    /// </summary>
    public partial class OrderLineItemViewModel : ObservableObject
    {
        [ObservableProperty] private string _name = string.Empty;
        [ObservableProperty] private int _quantity = 1;
        [ObservableProperty] private decimal _unitPrice;
        public decimal LineTotal => Quantity * UnitPrice;
    }

    /// <summary>
    /// ViewModel for the Table Detail modal popup.
    /// Opened when a table tile is clicked on the Floor Plan screen.
    /// </summary>
    public partial class TableDetailViewModel : ViewModelBase
    {
        private readonly ModalNavigationStore _modalNavigationStore;
        private readonly IOrderService _orderService;
        private readonly ITableService _tableService;
        private readonly INavigationService _navigationService;
        private readonly IFacilityContextService _facilityContext;
        private readonly SessionManager _sessionManager;

        // --- Table Info ---
        [ObservableProperty] private string _tableNumber = string.Empty;
        [ObservableProperty] private int _capacity;
        [ObservableProperty] private int _guestCount;
        [ObservableProperty] private string _serverInitials = string.Empty;
        [ObservableProperty] private string _elapsedTime = string.Empty;
        [ObservableProperty] private TableStatus _tableStatus;

        // Source tile ref — used to update the floor plan canvas immediately after actions.
        // CRITICAL: tile.Id == TableModel.Id (GUID) — always use this for DB updates,
        // NOT Label, because labels can be duplicated across sections (e.g. "T-01" in
        // both Main Hall and Patio).
        private TableTileViewModel? _sourceTile;

        // --- Order ---
        [ObservableProperty]
        private ObservableCollection<OrderLineItemViewModel> _orderItems = new();

        [ObservableProperty] private bool _isPaid;
        [ObservableProperty] private decimal _subtotal;
        [ObservableProperty] private decimal _taxAmount;
        [ObservableProperty] private decimal _grandTotal;

        public bool HasOrders => OrderItems.Count > 0;
        public bool IsAvailable => TableStatus == TableStatus.Available;
        public bool IsOccupied => TableStatus is TableStatus.Occupied or TableStatus.OrderSent
                                               or TableStatus.BillRequested or TableStatus.Ready;

        public TableDetailViewModel(
            ModalNavigationStore modalNavigationStore,
            IOrderService orderService,
            ITableService tableService,
            INavigationService navigationService,
            IFacilityContextService facilityContext,
            SessionManager sessionManager,
            ILogger<TableDetailViewModel>? logger = null)
            : base(logger, null, null)
        {
            _modalNavigationStore = modalNavigationStore;
            _orderService = orderService;
            _tableService = tableService;
            _navigationService = navigationService;
            _facilityContext = facilityContext;
            _sessionManager = sessionManager;
        }

        public async Task LoadFromTile(TableTileViewModel tile)
        {
            _sourceTile    = tile;
            TableNumber    = tile.TableNumber;   // = m.Label (e.g. "T-01", "P-01", "B-01")
            Capacity       = tile.Capacity;
            GuestCount     = tile.GuestCount;
            ServerInitials = tile.ServerInitials;
            ElapsedTime    = tile.ElapsedTime;
            TableStatus    = tile.Status;
            IsPaid         = false;

            OrderItems.Clear();

            // ALWAYS check for active orders to handle stale status ("Floor Plan showing Available when Order exists")
            var result = await _orderService.GetOrderByTableIdAsync(_sourceTile.Id);
            
            if (result.IsSuccess && result.Value != null)
            {
                // Self-Heal: If order exists but table is marked Available, promote it to Occupied
                if (TableStatus == TableStatus.Available)
                {
                    _logger?.LogInformation("Self-healing table {TableNumber}: Found active order, setting status to Occupied", TableNumber);
                    TableStatus = TableStatus.Occupied;
                    
                    // Update the tile in the canvas immediately
                    _sourceTile.Status = TableStatus.Occupied;
                    
                    // Trigger background refresh so other views update (and persisted state is corrected)
                    WeakReferenceMessenger.Default.Send(new TableStatusChangedMessage(_facilityContext.CurrentFacilityId));
                    
                    // Persist helpfully
                    var tableModel = await _tableService.GetTableByIdAsync(_sourceTile.Id);
                    if (tableModel != null && tableModel.Status != TableStatus.Occupied)
                    {
                        tableModel.Status = TableStatus.Occupied;
                        await _tableService.UpdateTableAsync(tableModel);
                    }
                }

                IsPaid = result.Value.Status == "Paid" || result.Value.Status == "Completed";
                foreach (var item in result.Value.Items)
                {
                    OrderItems.Add(new OrderLineItemViewModel
                    {
                        Name      = item.Name,
                        Quantity  = item.Quantity,
                        UnitPrice = item.Price
                    });
                }
            }

            RecalculateTotals();
            OnPropertyChanged(nameof(HasOrders));
            OnPropertyChanged(nameof(IsAvailable));
            OnPropertyChanged(nameof(IsOccupied));
        }

        private void RecalculateTotals()
        {
            decimal sub = 0;
            foreach (var item in OrderItems)
                sub += item.LineTotal;

            Subtotal   = sub;
            TaxAmount  = 0;
            GrandTotal = Subtotal;
        }

        [RelayCommand]
        private async Task CloseAsync()
        {
            await _modalNavigationStore.CloseAsync(ModalResult.Cancel());
        }

        /// <summary>
        /// Opens the POS ordering screen for this (available) table.
        /// FIX: Uses _sourceTile.Id (GUID) to update exactly the right DB row —
        /// prevents the cross-section bug where two tables share the same Label.
        /// </summary>
        [RelayCommand]
        private async Task OpenTableAsync()
        {
            if (string.IsNullOrWhiteSpace(TableNumber) || _sourceTile == null) return;

            var facilityId = _facilityContext.CurrentFacilityId;
            var tenantId   = _sessionManager.CurrentTenantId;

            var result = await _orderService.StartOrderAsync(_sourceTile.Id, TableNumber, tenantId, facilityId, GuestCount);
            if (result.IsSuccess)
            {
                // Update the tile in the canvas immediately
                _sourceTile.Status = TableStatus.Occupied;

                // FIX: Use GUID-based lookup so only this exact table (in this section)
                // gets updated — not any other table that happens to share the same Label.
                var tableModel = await _tableService.GetTableByIdAsync(_sourceTile.Id);
                if (tableModel != null)
                {
                    tableModel.Status = TableStatus.Occupied;
                    await _tableService.UpdateTableAsync(tableModel);
                }

                await _modalNavigationStore.CloseAsync(ModalResult.Success());
                WeakReferenceMessenger.Default.Send(new TableStatusChangedMessage(facilityId));
                await _navigationService.NavigateToAsync<RestaurantOrderingViewModel>(result.Value);
            }
        }

        /// <summary>
        /// Pay Bill: prints receipt + completes the order for this occupied table.
        ///
        /// FIX (stale status self-heal): If no active order is found for this table
        /// (e.g. left over from old integer-key orders or a DB orphan), the table status
        /// is reset to Available instead of silently doing nothing. This clears stale
        /// "Occupied" tiles on Patio and Bar Seating that have no matching order.
        ///
        /// FIX: Uses GUID to update the correct table when resetting status.
        /// </summary>
        [RelayCommand]
        private async Task PayBillAsync()
        {
            if (string.IsNullOrWhiteSpace(TableNumber) || _sourceTile == null) return;

            var orderResult = await _orderService.GetOrderByTableIdAsync(_sourceTile.Id);

            if (!orderResult.IsSuccess || orderResult.Value == null)
            {
                // No active order found — stale Occupied status. Self-heal: reset to Available.
                await ResetTableToAvailableAsync();
                WeakReferenceMessenger.Default.Send(new TableStatusChangedMessage(_facilityContext.CurrentFacilityId));
                await _modalNavigationStore.CloseAsync(ModalResult.Success());
                return;
            }

            var orderId = orderResult.Value.Id;

            // Print receipt non-fatally (fire-and-forget; payment succeeds even if printer is offline)
            _ = _orderService.PrintOrderAsync(orderId);

            // Complete / mark as paid
            var completeResult = await _orderService.CompleteOrderAsync(orderId);
            if (completeResult.IsSuccess)
            {
                await ResetTableToAvailableAsync();
                IsPaid      = true;
                TableStatus = TableStatus.Available;
                WeakReferenceMessenger.Default.Send(new TableStatusChangedMessage(_facilityContext.CurrentFacilityId));
                await _modalNavigationStore.CloseAsync(ModalResult.Success());
            }
        }

        /// <summary>
        /// Resets both the in-memory tile and the persisted DB row to Available.
        /// Uses GUID-based lookup to avoid cross-section label collision.
        /// </summary>
        private async Task ResetTableToAvailableAsync()
        {
            if (_sourceTile == null) return;

            // Update the floor plan canvas tile immediately
            _sourceTile.Status = TableStatus.Available;

            // Persist using the tile's GUID — never the label — to avoid cross-section collision
            var tableModel = await _tableService.GetTableByIdAsync(_sourceTile.Id);
            if (tableModel != null)
            {
                tableModel.Status = TableStatus.Available;
                await _tableService.UpdateTableAsync(tableModel);
            }
        }
    }
}
