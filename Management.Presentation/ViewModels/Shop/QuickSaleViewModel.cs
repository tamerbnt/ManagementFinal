using System;
using Management.Presentation.Extensions;
using Management.Presentation.Helpers;
using System.Collections.ObjectModel;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Management.Application.DTOs;
using Management.Application.Interfaces.App;
using Management.Application.Services;
using Management.Application.Stores;
using Management.Domain.Services;
using Management.Presentation.ViewModels.Base;
using Management.Application.ViewModels.Base;
using Management.Presentation.Stores;
using Management.Domain.Services;
using Management.Presentation.Services.Localization;
using Microsoft.Extensions.Logging;

namespace Management.Presentation.ViewModels.Shop
{
    public partial class QuickSaleViewModel : FacilityAwareViewModelBase
    {
        private readonly IProductService _productService;
        private readonly ISaleService _saleService;
        private readonly ModalNavigationStore _modalNavigationStore;
        private readonly ProductStore _productStore;

        [ObservableProperty]
        private ObservableRangeCollection<ProductDto> _products = new();
        private List<ProductDto> _allProducts = new();

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ProcessSaleCommand))]
        private ProductDto? _selectedProduct;

        public bool CanProcessSale => SelectedProduct != null;

        public QuickSaleViewModel(
            ITerminologyService terminologyService,
            IFacilityContextService facilityContext,
            ILogger<QuickSaleViewModel> logger,
            IDiagnosticService diagnosticService,
            IToastService toastService,
            IProductService productService,
            ISaleService saleService,
            ModalNavigationStore modalNavigationStore,
            ProductStore productStore,
            ILocalizationService localizationService)
            : base(terminologyService, facilityContext, logger, diagnosticService, toastService, localizationService)
        {
            _productService = productService;
            _saleService = saleService;
            _modalNavigationStore = modalNavigationStore;
            _productStore = productStore;
            
            Title = GetTerm("Strings.Shop.QuickSale") ?? "Quick Sale";
            _productStore.StockUpdated += OnProductStockUpdated;
            _ = LoadProductsAsync();
        }

        protected override void OnLanguageChanged()
        {
            Title = GetTerm("Strings.Shop.QuickSale") ?? "Quick Sale";
        }

        public async override Task OnModalOpenedAsync(object parameter, System.Threading.CancellationToken cancellationToken = default)
        {
            if (!_allProducts.Any())
            {
                await LoadProductsAsync();
            }
            await Task.CompletedTask;
        }

        [RelayCommand]
        private async Task LoadProductsAsync()
        {
            await ExecuteSafeAsync(async () =>
            {
                IsLoading = true;
                var result = await _productService.GetActiveProductsAsync(_facilityContext.CurrentFacilityId);
                
                if (result.IsSuccess)
                {
                    _allProducts = result.Value.ToList();
                    
                    Products.ReplaceRange(_allProducts);

                    FilterProducts(SearchQuery); 
                }
                else
                {
                    ShowError((GetTerm("Strings.Shop.Failedtoloadproducts") ?? "Failed to load products:").TrimEnd(':'));
                }
            });
            IsLoading = false;
        }

        partial void OnSearchQueryChanged(string value)
        {
            FilterProducts(value);
        }

        private void FilterProducts(string query)
        {
            List<ProductDto> filtered;
            if (string.IsNullOrWhiteSpace(query))
            {
                filtered = _allProducts;
            }
            else
            {
                filtered = _allProducts.Where(p => 
                    p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || 
                    (p.Category != null && p.Category.Contains(query, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }

            Products.ReplaceRange(filtered);

            // Clear selection if it's no longer in the filtered list
            if (SelectedProduct != null && !Products.Contains(SelectedProduct))
            {
                SelectedProduct = null;
            }
        }

        [RelayCommand]
        private void SetSelectedProduct(ProductDto product)
        {
            SelectedProduct = product;
        }

        [RelayCommand(CanExecute = nameof(CanProcessSale))]
        private async Task ProcessSaleAsync()
        {
            if (SelectedProduct == null) return;

            await ExecuteSafeAsync(async () =>
            {
                var itemsMap = new Dictionary<Guid, int> { { SelectedProduct.Id, 1 } };
                var request = new CheckoutRequestDto(
                    Management.Domain.Enums.PaymentMethod.Cash,
                    SelectedProduct.Price,
                    null,
                    itemsMap
                );

                var result = await _saleService.ProcessCheckoutAsync(_facilityContext.CurrentFacilityId, request);
                if (result.IsSuccess)
                {
                    // Notify ViewModels to refresh (Dirty Flag)
                    CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new Management.Presentation.Messages.RefreshRequiredMessage<Management.Domain.Models.Sale>(_facilityContext.CurrentFacilityId));

                    await _modalNavigationStore.CloseAsync(ModalResult.Success(SelectedProduct));
                }
                else
                {
                    ShowError((GetTerm("Strings.Shop.Checkoutfailed") ?? "Checkout failed: ") + " " + result.Error.Message);
                }
            }, "Sale processing failed.");
        }

        private void OnProductStockUpdated(ProductDto updatedProduct)
        {
            var product = _allProducts.FirstOrDefault(p => p.Id == updatedProduct.Id);
            if (product != null)
            {
                product.StockQuantity = updatedProduct.StockQuantity;
                // Since this uses Record/ObservableRangeCollection with NotifyCollectionChanged, 
                // but the individual record properties might not notify unless they are mutable and have NotifyPropertyChanged.
                // ProductDto properties ARE mutable but do NOT have NotifyPropertyChanged.
                // So we might need to Replace the item in the collection or use an ObservableObject wrapper.
                // For now, let's just Refresh the filtered list to reflect changes.
                FilterProducts(SearchQuery);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_productStore != null)
                {
                    _productStore.StockUpdated -= OnProductStockUpdated;
                }
            }
            base.Dispose(disposing);
        }

        [RelayCommand]
        private async Task CancelAsync()
        {
            await _modalNavigationStore.CloseAsync(ModalResult.Cancel());
        }
    }
}
