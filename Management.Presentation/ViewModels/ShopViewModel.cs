using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Management.Application.Services; // Updated namespace
using Management.Application.Stores;     // Added for ProductStore
using Management.Domain.DTOs;
using Management.Domain.Enums;
using Management.Domain.Services;
using Management.Presentation.Extensions; // Using Custom Extensions
using Management.Presentation.Services;

namespace Management.Presentation.ViewModels
{
    public class ShopViewModel : ViewModelBase
    {
        private readonly IProductService _productService;
        private readonly ISaleService _saleService;
        private readonly IDialogService _dialogService;
        private readonly INotificationService _notificationService;

        // FIX: Inject Store to handle stock updates correctly
        private readonly ProductStore _productStore;

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

        private int _cartItemCount;
        public int CartItemCount { get => _cartItemCount; set => SetProperty(ref _cartItemCount, value); }

        public string TaxRateDisplay => "(5%)";

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
            ProductStore productStore) // Added Store injection
        {
            _productService = productService;
            _saleService = saleService;
            _dialogService = dialogService;
            _notificationService = notificationService;
            _productStore = productStore;

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
            var dtos = await _productService.GetActiveProductsAsync();

            _allProducts.Clear();
            foreach (var dto in dtos)
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
            var dtos = await _productService.GetInventoryStatusAsync();

            InventoryItems.Clear();
            foreach (var dto in dtos)
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

        private void RecalculateTotals()
        {
            Subtotal = CartItems.Sum(x => x.TotalLinePrice);
            TaxAmount = Subtotal * 0.05m;
            TotalAmount = Subtotal + TaxAmount;
            CartItemCount = CartItems.Sum(x => x.Quantity);

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

        private void ExecuteDeleteProduct(Guid productId)
        {
            // Implementation omitted for brevity
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
        public ProductCategory Category => _dto.Category;

        public string StockStatusText => StockLevel > 10 ? "In Stock" : (StockLevel > 0 ? "Low Stock" : "Out of Stock");

        public ICommand AddToCartCommand { get; }
        public ICommand EditProductCommand { get; set; }
        public ICommand DeleteProductCommand { get; set; }

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
        public string Name { get; set; }
        public string SKU { get; set; }
        public int StockCount { get; set; }
        public int ReorderLevel { get; set; }
        public DateTime LastUpdated { get; set; }
        public string ImageUrl { get; set; }

        public int StockLevel => StockCount;

        public ICommand EditCommand { get; set; }

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