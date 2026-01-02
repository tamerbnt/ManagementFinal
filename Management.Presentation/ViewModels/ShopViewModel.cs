using System;
using Management.Application.Services;
using System.Collections.Generic;
using Management.Application.Services;
using System.Collections.ObjectModel;
using Management.Application.Services;
using System.Linq;
using Management.Application.Services;
using System.Threading.Tasks;
using Management.Application.Services;
using System.Windows.Input;
using Management.Application.Services;
// using Management.Presentation.Services; (Already below)
using Management.Application.Stores;     // Added for ProductStore
using Management.Application.DTOs;
using Management.Application.Services;
using Management.Domain.Enums;
using Management.Application.Services;
using Management.Domain.Services;
using Management.Application.Services;
using Management.Presentation.Extensions; // Using Custom Extensions
using Management.Presentation.Services;
using Management.Application.Services;
using MediatR;
using Management.Application.Services;
using Management.Application.Features.Shop.Queries.CalculateShopTotals;
using Management.Application.Services;

namespace Management.Presentation.ViewModels
{
    public class ShopViewModel : ViewModelBase
    {
        private readonly IProductService _productService;
        private readonly ISaleService _saleService;
        private readonly IDialogService _dialogService;
        private readonly INotificationService _notificationService;

        private readonly IMediator _mediator;
        private readonly ProductStore _productStore;
        private readonly Management.Domain.Services.IFacilityContextService _facilityContext;

        private List<ProductItemViewModel> _allProducts = new List<ProductItemViewModel>();

        // --- 1. SHELL STATE ---

        private int _selectedTabIndex;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                if (SetProperty(ref _selectedTabIndex, value))
                {
                    IsCartVisible = value == 0;
                }
            }
        }

        private bool _isCartVisible = true;
        public bool IsCartVisible
        {
            get => _isCartVisible;
            set => SetProperty(ref _isCartVisible, value);
        }

        // --- 2. POS CATALOG STATE ---

        public ObservableCollection<ProductItemViewModel> Products { get; }
            = new ObservableCollection<ProductItemViewModel>();

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    ApplyFilters();
            }
        }

        private ProductCategory? _currentCategoryFilter = null;

        public bool FilterAll
        {
            get => _currentCategoryFilter == null;
            set { if (value) SetCategoryFilter(null); }
        }

        public bool FilterSupplements
        {
            get => _currentCategoryFilter == ProductCategory.Supplements;
            set { if (value) SetCategoryFilter(ProductCategory.Supplements); }
        }

        public bool FilterApparel
        {
            get => _currentCategoryFilter == ProductCategory.Apparel;
            set { if (value) SetCategoryFilter(ProductCategory.Apparel); }
        }

        public bool FilterEquipment
        {
            get => _currentCategoryFilter == ProductCategory.Equipment;
            set { if (value) SetCategoryFilter(ProductCategory.Equipment); }
        }

        private void SetCategoryFilter(ProductCategory? category)
        {
            _currentCategoryFilter = category;
            ApplyFilters();
        }

        // --- 3. CART ENGINE ---

        public ObservableCollection<CartItemViewModel> CartItems { get; }
            = new ObservableCollection<CartItemViewModel>();

        private decimal _subtotal;
        public decimal Subtotal { get => _subtotal; set => SetProperty(ref _subtotal, value); }

        private decimal _taxAmount;
        public decimal TaxAmount { get => _taxAmount; set => SetProperty(ref _taxAmount, value); }

        private decimal _totalAmount;
        public decimal TotalAmount { get => _totalAmount; set => SetProperty(ref _totalAmount, value); }

        private string _taxRateDisplay = "(5%)";
        public string TaxRateDisplay { get => _taxRateDisplay; set => SetProperty(ref _taxRateDisplay, value); }

        private int _cartItemCount;
        public int CartItemCount { get => _cartItemCount; set => SetProperty(ref _cartItemCount, value); }

        // --- 4. INVENTORY STATE ---

        public ObservableCollection<InventoryItemViewModel> InventoryItems { get; }
            = new ObservableCollection<InventoryItemViewModel>();

        private bool _hasLowStock;
        public bool HasLowStock { get => _hasLowStock; set => SetProperty(ref _hasLowStock, value); }

        private int _lowStockCount;
        public int LowStockCount { get => _lowStockCount; set => SetProperty(ref _lowStockCount, value); }

        // --- 5. COMMANDS ---

        public ICommand CheckoutCommand { get; }
        public ICommand ClearCartCommand { get; }
        public ICommand AddProductCommand { get; }
        public ICommand FilterLowStockCommand { get; }

        public ShopViewModel(
            IProductService productService,
            ISaleService saleService,
            IDialogService dialogService,
            INotificationService notificationService,
            ProductStore productStore,
            Management.Domain.Services.IFacilityContextService facilityContext,
            IMediator mediator) 
        {
            _productService = productService;
            _saleService = saleService;
            _dialogService = dialogService;
            _notificationService = notificationService;
            _productStore = productStore;
            _facilityContext = facilityContext;
            _mediator = mediator;

            CheckoutCommand = new RelayCommand(ExecuteCheckout, CanExecuteCheckout);
            ClearCartCommand = new RelayCommand(ExecuteClearCart);
            AddProductCommand = new RelayCommand(ExecuteAddProduct);
            FilterLowStockCommand = new RelayCommand(ExecuteFilterLowStock);

            _ = LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                var t1 = LoadCatalogAsync();
                var t2 = LoadInventoryAsync();
                await Task.WhenAll(t1, t2);
            }
            catch (Exception) { /* Handle error */ }
        }

        private async Task LoadCatalogAsync()
        {
            var facilityId = _facilityContext.CurrentFacilityId;
            var result = await _productService.GetActiveProductsAsync(facilityId);
            if (result.IsFailure)
            {
                _notificationService.ShowError("Error loading product catalog.");
                return;
            }

            _allProducts.Clear();
            foreach (var dto in result.Value)
            {
                var vm = new ProductItemViewModel(dto, OnAddToCart);
                vm.EditProductCommand = new AsyncRelayCommand(async () => await ExecuteEditProduct(vm.Id));
                vm.DeleteProductCommand = new RelayCommand(() => ExecuteDeleteProduct(vm.Id));

                _allProducts.Add(vm);
            }

            ApplyFilters();
        }

        private async Task LoadInventoryAsync()
        {
            var facilityId = _facilityContext.CurrentFacilityId;
            var result = await _productService.GetInventoryStatusAsync(facilityId);
            if (result.IsFailure)
            {
                _notificationService.ShowError("Error loading inventory info.");
                return;
            }

            InventoryItems.Clear();
            foreach (var dto in result.Value)
            {
                var vm = new InventoryItemViewModel(dto);
                vm.EditCommand = new AsyncRelayCommand(async () => await ExecuteEditProduct(vm.Id));
                InventoryItems.Add(vm);
            }

            var lowStockItems = InventoryItems.Where(x => x.StockLevel <= x.ReorderLevel).ToList();
            LowStockCount = lowStockItems.Count;
            HasLowStock = LowStockCount > 0;
        }

        private void ApplyFilters()
        {
            IEnumerable<ProductItemViewModel> query = _allProducts;

            if (!string.IsNullOrWhiteSpace(SearchText))
                query = query.Where(p => p.Name.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0);

            if (_currentCategoryFilter != null)
                query = query.Where(p => p.Category == _currentCategoryFilter.Value);

            Products.Clear();
            foreach (var item in query) Products.Add(item);
        }

        private void OnAddToCart(ProductDto product)
        {
            var existingItem = CartItems.FirstOrDefault(x => x.ProductId == product.Id);

            if (existingItem != null)
                existingItem.Quantity++;
            else
                CartItems.Add(new CartItemViewModel(product, OnCartItemChanged));

            RecalculateTotals();
        }

        private void OnCartItemChanged()
        {
            var toRemove = CartItems.Where(x => x.Quantity <= 0).ToList();
            foreach (var item in toRemove) CartItems.Remove(item);
            RecalculateTotals();
        }

        private async void RecalculateTotals()
        {
            // ARCHITECTURAL FIX: Logic moved to Application Layer
            var query = new CalculateShopTotalsQuery(
                CartItems.Select(x => new CartItemDto(x.ProductId, x.UnitPrice, x.Quantity)).ToList()
            );

            var totals = await _mediator.Send(query);

            Subtotal = totals.Subtotal;
            TaxAmount = totals.TaxAmount;
            TotalAmount = totals.TotalAmount;
            CartItemCount = totals.CartItemCount;
            TaxRateDisplay = totals.TaxRateDisplay;

            (CheckoutCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void ExecuteClearCart()
        {
            CartItems.Clear();
            RecalculateTotals();
        }

        private bool CanExecuteCheckout() => CartItems.Count > 0;

        private async void ExecuteCheckout()
        {
            // FIX: Use ShowCustomDialogAsync with the correct ViewModel type
            await _dialogService.ShowCustomDialogAsync<CheckoutViewModel>();

            // Note: The CheckoutViewModel will handle clearing the cart (via SaleStore) 
            // and closing the modal upon success.
        }

        private async void ExecuteAddProduct()
        {
            // FIX: Use ShowCustomDialogAsync for ProductDetail
            await _dialogService.ShowCustomDialogAsync<ProductDetailViewModel>();
        }

        private async Task ExecuteEditProduct(Guid productId)
        {
            // FIX: Pass the ID as parameter
            await _dialogService.ShowCustomDialogAsync<ProductDetailViewModel>(productId);
        }

        private async void ExecuteDeleteProduct(Guid productId)
        {
            var result = await _dialogService.ShowConfirmationAsync(
                "Delete Product",
                "Are you sure you want to delete this product? This action cannot be undone.",
                "Delete",
                "Cancel",
                isDestructive: true);

            if (result)
            {
                var facilityId = _facilityContext.CurrentFacilityId;
                var deleteResult = await _productService.DeleteProductAsync(facilityId, productId);
                if (deleteResult.IsSuccess)
                {
                    _notificationService.ShowSuccess("Product deleted successfully.");
                    await LoadDataAsync();
                }
                else
                {
                    _notificationService.ShowError(deleteResult.Error.Message);
                }
            }
        }

        private void ExecuteFilterLowStock()
        {
            SelectedTabIndex = 1;
        }
    }

    // --- SUB-VIEWMODELS ---

    public class ProductItemViewModel : ViewModelBase
    {
        private readonly ProductDto _dto;

        public Guid Id => _dto.Id;
        public string Name => _dto.Name;
        public decimal Price => _dto.Price;
        public int StockLevel => _dto.StockQuantity;
        public string ImageUrl => _dto.ImageUrl;
        public ProductCategory Category => Enum.TryParse<ProductCategory>(_dto.Category, true, out var c) ? c : ProductCategory.Other;

        public string StockStatusText => StockLevel > 10 ? "In Stock" : (StockLevel > 0 ? "Low Stock" : "Out of Stock");

        public ICommand AddToCartCommand { get; }
        public ICommand EditProductCommand { get; set; } = null!;
        public ICommand DeleteProductCommand { get; set; } = null!;

        public ProductItemViewModel(ProductDto dto, Action<ProductDto> addCallback)
        {
            _dto = dto;
            AddToCartCommand = new RelayCommand(() => addCallback(_dto));
        }
    }

    public class CartItemViewModel : ViewModelBase
    {
        private readonly ProductDto _dto;
        private readonly Action _changeCallback;

        public Guid ProductId => _dto.Id;
        public string ProductName => _dto.Name;
        public decimal UnitPrice => _dto.Price;

        private int _quantity;
        public int Quantity
        {
            get => _quantity;
            set
            {
                if (SetProperty(ref _quantity, value))
                {
                    OnPropertyChanged(nameof(TotalLinePrice));
                    _changeCallback?.Invoke();
                }
            }
        }

        public decimal TotalLinePrice => Quantity * UnitPrice;

        public ICommand IncrementCommand { get; }
        public ICommand DecrementCommand { get; }

        public CartItemViewModel(ProductDto dto, Action changeCallback)
        {
            _dto = dto;
            _changeCallback = changeCallback;
            Quantity = 1;

            IncrementCommand = new RelayCommand(() => Quantity++);
            DecrementCommand = new RelayCommand(() => Quantity--);
        }
    }

    public class InventoryItemViewModel : ViewModelBase
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public int StockCount { get; set; }
        public int ReorderLevel { get; set; }
        public DateTime LastUpdated { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
 
        public int StockLevel => StockCount;
 
        public ICommand EditCommand { get; set; } = null!;

        public InventoryItemViewModel(InventoryDto dto)
        {
            Id = dto.ProductId;
            Name = dto.ProductName;
            SKU = dto.SKU;
            StockCount = dto.CurrentStock;
            ReorderLevel = dto.ReorderPoint;
            LastUpdated = dto.LastUpdated;
            ImageUrl = dto.ImageUrl;
        }
    }
}