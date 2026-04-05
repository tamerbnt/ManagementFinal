using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Application.Interfaces.App;
using Management.Domain.Services;
using Management.Presentation.ViewModels.Base;
using Management.Presentation.Services;
using Microsoft.Extensions.Logging;

namespace Management.Presentation.ViewModels.History
{
    public partial class InventoryHistoryViewModel : FacilityAwareViewModelBase
    {
        private readonly IProductInventoryService _inventoryService;
        private readonly IFacilityContextService _facilityContext;
        private readonly IModalNavigationService _modalNavigationService;

        [ObservableProperty] private string _topPurchasedItemName = string.Empty;
        [ObservableProperty] private decimal _topPurchasedItemCost;
        [ObservableProperty] private string _highestVelocityItemName = string.Empty;
        [ObservableProperty] private int _highestVelocityItemQty;
        [ObservableProperty] private int _lowStockCount;

        [ObservableProperty] private string _criticalStockItemName = string.Empty;
        [ObservableProperty] private int _criticalStockRunwayDays;
        [ObservableProperty] private bool _isCriticalStockAlert;

        public ObservableCollection<ProductInventoryTransactionDto> Transactions { get; } = new();

        public IAsyncRelayCommand RefreshCommand { get; }
        public IRelayCommand CloseCommand { get; }

        public InventoryHistoryViewModel(
            IProductInventoryService inventoryService,
            IFacilityContextService facilityContext,
            IModalNavigationService modalNavigationService,
            ILogger<InventoryHistoryViewModel> logger,
            IToastService toastService,
            ITerminologyService terminologyService)
            : base(terminologyService, facilityContext, logger, null, toastService)
        {
            _inventoryService = inventoryService;
            _facilityContext = facilityContext;
            _modalNavigationService = modalNavigationService;

            RefreshCommand = new AsyncRelayCommand(LoadDataAsync);
            CloseCommand = new RelayCommand(CloseModal);

            InitializeLocalizedLabels();
            _ = LoadDataAsync();
        }

        private void InitializeLocalizedLabels()
        {
            TopPurchasedItemName = GetTerm("Terminology.History.Status.NA") ?? "N/A";
            HighestVelocityItemName = GetTerm("Terminology.History.Status.NA") ?? "N/A";
            CriticalStockItemName = GetTerm("Terminology.History.Status.AllHealthy") ?? "All Healthy";
        }

        private async Task LoadDataAsync()
        {
            await ExecuteLoadingAsync(async () =>
            {
                var result = await _inventoryService.GetInventoryAnalyticsAsync(_facilityContext.CurrentFacilityId);
                if (result.IsSuccess)
                {
                    var data = result.Value;
                    TopPurchasedItemName = data.TopPurchasedItemName;
                    TopPurchasedItemCost = data.TopPurchasedItemTotalCost;
                    HighestVelocityItemName = data.HighestVelocityItemName;
                    HighestVelocityItemQty = data.HighestVelocityItemQuantity;
                    LowStockCount = data.LowStockCount;

                    if (data.CriticalStock != null)
                    {
                        CriticalStockItemName = data.CriticalStock.ProductName;
                        CriticalStockRunwayDays = data.CriticalStock.RunwayDays;
                        IsCriticalStockAlert = data.CriticalStock.RunwayDays <= 7;
                    }

                    Transactions.Clear();
                    foreach (var t in data.RecentTransactions)
                    {
                        Transactions.Add(t);
                    }
                }
                else
                {
                    _toastService?.ShowError(GetTerm("Terminology.Dashboard.History.LoadingInventory") ?? "Failed to load inventory history.");
                }
            });
        }

        private void CloseModal()
        {
            _modalNavigationService.CloseModal();
        }
    }
}
