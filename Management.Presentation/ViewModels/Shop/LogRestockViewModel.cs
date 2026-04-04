using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Application.DTOs;
using Management.Application.Interfaces.App;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;
using Management.Presentation.Services;
using Management.Presentation.Stores;
using Management.Presentation.ViewModels.Base;
using Microsoft.Extensions.Logging;

namespace Management.Presentation.ViewModels.Shop
{
    public partial class LogRestockViewModel : ViewModelBase, IParameterReceiver
    {
        private readonly IProductInventoryService _inventoryService;
        private readonly IFacilityContextService _facilityContext;
        private readonly IModalNavigationService _modalNavigationService;
        private readonly ModalNavigationStore _modalStore;
        private ProductDto? _product;

        [ObservableProperty] private string _productName = string.Empty;
        [ObservableProperty] private string _sku = string.Empty;
        [ObservableProperty] private int _currentStock;
        [ObservableProperty] private decimal _currentPrice;
        
        [ObservableProperty] 
        [NotifyPropertyChangedFor(nameof(TotalCost))]
        [NotifyPropertyChangedFor(nameof(Margin))]
        [NotifyPropertyChangedFor(nameof(MarginColor))]
        private int _quantity = 1;

        [ObservableProperty] 
        [NotifyPropertyChangedFor(nameof(TotalCost))]
        [NotifyPropertyChangedFor(nameof(Margin))]
        [NotifyPropertyChangedFor(nameof(MarginColor))]
        private decimal _unitCost;

        [ObservableProperty] 
        [NotifyPropertyChangedFor(nameof(Margin))]
        [NotifyPropertyChangedFor(nameof(MarginColor))]
        private decimal _newSalePrice;

        [ObservableProperty] private string _notes = string.Empty;

        public decimal TotalCost => Quantity * UnitCost;

        public decimal Margin 
        {
            get 
            {
                if (NewSalePrice <= 0) return 0;
                return ((NewSalePrice - UnitCost) / NewSalePrice) * 100;
            }
        }

        public string MarginColor => Margin switch
        {
            < 10 => "#FF5252", // Critical (Red)
            < 30 => "#FFAB40", // Warning (Orange)
            _ => "#00E676"     // Good (Green)
        };

        public IAsyncRelayCommand SubmitCommand { get; }
        public IRelayCommand CancelCommand { get; }

        public LogRestockViewModel(
            IProductInventoryService inventoryService,
            IFacilityContextService facilityContext,
            IModalNavigationService modalNavigationService,
            ModalNavigationStore modalStore,
            ILogger<LogRestockViewModel> logger,
            IToastService toastService)
            : base(logger, null, toastService)
        {
            _inventoryService = inventoryService;
            _facilityContext = facilityContext;
            _modalNavigationService = modalNavigationService;
            _modalStore = modalStore;

            SubmitCommand = new AsyncRelayCommand(SubmitAsync);
            CancelCommand = new RelayCommand(() => CloseModal());
        }

        public async Task SetParameterAsync(object parameter)
        {
            if (parameter is ProductDto dto)
            {
                _product = dto;
                ProductName = dto.Name;
                Sku = dto.SKU;
                CurrentStock = dto.StockQuantity;
                CurrentPrice = dto.Price;
                UnitCost = dto.Cost;
                NewSalePrice = dto.Price;
            }
            await Task.CompletedTask;
        }

        private async Task SubmitAsync()
        {
            if (_product == null)
            {
                _toastService?.ShowError("Product context lost. Please close and try again.");
                return;
            }

            if (Quantity <= 0)
            {
                _toastService?.ShowError("Quantity must be greater than zero.");
                return;
            }

            await ExecuteSafeAsync(async () =>
            {
                var result = await _inventoryService.LogRestockAsync(
                    _facilityContext.CurrentFacilityId,
                    _product.Id,
                    Quantity,
                    new Money(UnitCost, "DA"),
                    new Money(NewSalePrice, "DA"),
                    Notes);

                if (result.IsSuccess)
                {
                    _toastService?.ShowSuccess($"Restocked {Quantity} units of {ProductName}.");
                    CloseModal(true);
                }
                else
                {
                    _toastService?.ShowError(result.Error.Message);
                }
            }, "Failed to log restock.");
        }

        private void CloseModal(bool success = false)
        {
            // Support both overlay store and window-based service for resilience
            if (success)
            {
                _ = _modalStore.CloseAsync(ModalResult.Success());
            }
            else
            {
                _ = _modalStore.CloseAsync(ModalResult.Cancel());
            }

            if (_modalNavigationService.IsModalOpen)
            {
                _modalNavigationService.CloseModal();
            }
        }
    }
}
