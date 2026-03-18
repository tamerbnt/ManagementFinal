using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Application.DTOs;
using CommunityToolkit.Mvvm.Messaging;
using Management.Application.Interfaces.App;
using Management.Application.Services;
using Management.Application.Stores;
using Management.Domain.Services;
using Management.Presentation.Extensions;
using Management.Presentation.Stores;
using Management.Presentation.ViewModels.Base;
using Management.Domain.Services;
using Management.Presentation.Services.Localization;
using Microsoft.Extensions.Logging;

namespace Management.Presentation.ViewModels.GymHome
{
    public enum CartTab
    {
        Products,
        WalkIn
    }

    public partial class MultiSaleCartViewModel : FacilityAwareViewModelBase
    {
        private readonly IProductService _productService;
        private readonly ISaleService _saleService;
        private readonly ProductStore _productStore;
        private readonly IGymOperationService _gymOperationService;
        private readonly ModalNavigationStore _modalNavigationStore;
        private readonly Management.Domain.Services.IDialogService _dialogService;

        [ObservableProperty]
        private CartTab _currentTab = CartTab.Products;

        [ObservableProperty]
        private ObservableCollection<ProductDto> _products = new();
        
        [ObservableProperty]
        private ObservableCollection<CartItemViewModel> _cartItems = new();

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private ProductDto? _selectedProduct;

        [ObservableProperty]
        private int _walkInCount = 0;

        [ObservableProperty]
        private ObservableCollection<WalkInPlanDto> _walkInPlans = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(WalkInPrice))]
        [NotifyPropertyChangedFor(nameof(WalkInTotal))]
        [NotifyPropertyChangedFor(nameof(GrandTotal))]
        private WalkInPlanDto? _selectedWalkInPlan;

        public decimal WalkInPrice => SelectedWalkInPlan?.Price ?? 0m;


        public decimal ProductsTotal => CartItems.Sum(item => item.Price * item.Quantity);
        public decimal WalkInTotal => WalkInCount * WalkInPrice;
        public decimal GrandTotal => ProductsTotal + WalkInTotal;

        public bool CanCheckout => GrandTotal > 0;

        private List<ProductDto> _allProducts = new();

        public MultiSaleCartViewModel(
            ITerminologyService terminologyService,
            IFacilityContextService facilityContext,
            ILogger<MultiSaleCartViewModel> logger,
            IDiagnosticService diagnosticService,
            IToastService toastService,
            IProductService productService, 
            ISaleService saleService,
            ModalNavigationStore modalNavigationStore,
            Management.Domain.Services.IDialogService dialogService,
            ProductStore productStore,
            IGymOperationService gymOperationService,
            ILocalizationService localizationService)
            : base(terminologyService, facilityContext, logger, diagnosticService, toastService, localizationService)
        {
            _productService = productService;
            _saleService = saleService;
            _modalNavigationStore = modalNavigationStore;
            _dialogService = dialogService;
            _productStore = productStore;
            _gymOperationService = gymOperationService;

            Title = GetTerm("Strings.GymHome.MultiSaleCart") ?? "Multi-Sale / Cart";
            _productStore.StockUpdated += OnProductStockUpdated;
            _ = LoadProductsAsync();
            _ = LoadWalkInPlansAsync();
        }

        protected override void OnLanguageChanged()
        {
            Title = GetTerm("Strings.GymHome.MultiSaleCart") ?? "Multi-Sale / Cart";
        }

        public override async Task OnModalOpenedAsync(object parameter, System.Threading.CancellationToken cancellationToken = default)
        {
            if (!_allProducts.Any())
            {
                await LoadProductsAsync();
            }
            if (!WalkInPlans.Any())
            {
                await LoadWalkInPlansAsync();
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
                    Products.Clear();
                    foreach (var p in _allProducts)
                        Products.Add(p);

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

            Products.Clear();
            foreach (var p in filtered)
                Products.Add(p);
        }

        [RelayCommand]
        private void SwitchTab(string tabName)
        {
            if (Enum.TryParse<CartTab>(tabName, out var tab))
            {
                CurrentTab = tab;
            }
        }

        [RelayCommand]
        private void AddProductToCart(ProductDto product)
        {
            var existingItem = CartItems.FirstOrDefault(item => item.ProductId == product.Id);
            if (existingItem != null)
            {
                existingItem.Quantity++;
            }
            else
            {
                CartItems.Add(new CartItemViewModel
                {
                    ProductId = product.Id,
                    Name = product.Name,
                    Price = product.Price,
                    Quantity = 1
                });
            }
            OnPropertyChanged(nameof(ProductsTotal));
            OnPropertyChanged(nameof(GrandTotal));
            OnPropertyChanged(nameof(CanCheckout));
        }

        [RelayCommand]
        private void RemoveProductFromCart(CartItemViewModel item)
        {
            CartItems.Remove(item);
            OnPropertyChanged(nameof(ProductsTotal));
            OnPropertyChanged(nameof(GrandTotal));
            OnPropertyChanged(nameof(CanCheckout));
        }

        [RelayCommand]
        private void SetSelectedWalkInPlan(WalkInPlanDto plan)
        {
            SelectedWalkInPlan = plan;
        }

        [RelayCommand]
        private void IncrementWalkIn()
        {
            WalkInCount++;
            OnPropertyChanged(nameof(WalkInTotal));
            OnPropertyChanged(nameof(GrandTotal));
            OnPropertyChanged(nameof(CanCheckout));
        }

        [RelayCommand]
        private void DecrementWalkIn()
        {
            if (WalkInCount > 0)
            {
                WalkInCount--;
                OnPropertyChanged(nameof(WalkInTotal));
                OnPropertyChanged(nameof(GrandTotal));
                OnPropertyChanged(nameof(CanCheckout));
            }
        }


        private async Task LoadWalkInPlansAsync()
        {
            var walkInResult = await _gymOperationService.GetWalkInPlansAsync(_facilityContext.CurrentFacilityId);
            if (walkInResult != null)
            {
                WalkInPlans.Clear();
                foreach (var plan in walkInResult)
                {
                    WalkInPlans.Add(plan);
                }
                SelectedWalkInPlan = WalkInPlans.FirstOrDefault();
            }
        }

        [RelayCommand]
        private async Task CheckoutAsync()
        {
            if (!CanCheckout) return;

            var confirmed = await _dialogService.ShowConfirmationAsync(
                GetTerm("Strings.Shop.ConfirmMultiSale") ?? "Confirm Multi-Sale",
                string.Format(GetTerm("Strings.Shop.MultiSaleSummary") ?? "Total: {0:N2} DA\n\nProducts: {1:N2} DA\nWalk-Ins: {2:N2} DA\nMembership: {3:N2} DA\n\nProceed with checkout?", 
                    GrandTotal, ProductsTotal, WalkInTotal, 0m),
                GetTerm("Terminology.Global.Confirm") ?? "Confirm",
                GetTerm("Terminology.Global.Cancel") ?? "Cancel",
                false);

            if (!confirmed) return;

            await ExecuteSafeAsync(async () =>
            {
                // 1. Process products via ISaleService for inventory tracking
                if (CartItems.Any())
                {
                    var itemsMap = CartItems.ToDictionary(i => i.ProductId, i => i.Quantity);
                    var productRequest = new CheckoutRequestDto(
                        Management.Domain.Enums.PaymentMethod.Cash,
                        ProductsTotal,
                        null,
                        itemsMap
                    );

                    var productResult = await _saleService.ProcessCheckoutAsync(_facilityContext.CurrentFacilityId, productRequest);
                    if (!productResult.IsSuccess)
                    {
                        ShowError((GetTerm("Strings.Shop.ProductCheckoutFailed") ?? "Product checkout failed: ") + " " + productResult.Error.Message);
                        // Continue or stop? Usually stop if a partial failure occurs in a "multi" sale?
                        // For Titan, we'll stop to let user fix issues.
                        return;
                    }
                }

                // 2. Process walk-ins (Non-inventoried services)
                for (int i = 0; i < WalkInCount; i++)
                {
                    await _gymOperationService.ProcessWalkInAsync(WalkInPrice, _facilityContext.CurrentFacilityId, SelectedWalkInPlan?.Name ?? "Walk-In");
                }

                // 3. Notify ViewModels to refresh (Dirty Flag)
                CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new Management.Presentation.Messages.RefreshRequiredMessage<Management.Domain.Models.Sale>(_facilityContext.CurrentFacilityId));
                CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new Management.Presentation.Messages.RefreshRequiredMessage<Management.Domain.Models.Member>(_facilityContext.CurrentFacilityId));

                await _modalNavigationStore.CloseAsync(ModalResult.Success(GrandTotal));
            }, GetTerm("Strings.Shop.Checkoutfailed")?.TrimEnd(':') ?? "Checkout failed.");
        }

        private void OnProductStockUpdated(ProductDto updatedProduct)
        {
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (IsDisposed) return;
                
                // Sync local product list
                var product = _allProducts.FirstOrDefault(p => p.Id == updatedProduct.Id);
                if (product != null)
                {
                    product.StockQuantity = updatedProduct.StockQuantity;
                    FilterProducts(SearchQuery);
                }
            });
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

    public partial class CartItemViewModel : ObservableObject
    {
        public Guid ProductId { get; set; }
        
        [ObservableProperty]
        private string _name = string.Empty;
        
        [ObservableProperty]
        private decimal _price;
        
        [ObservableProperty]
        private int _quantity;
    }
}
