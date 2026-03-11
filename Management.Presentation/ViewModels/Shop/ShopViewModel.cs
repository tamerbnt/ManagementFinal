using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Application.Interfaces;
using Management.Domain.Models;

using Management.Application.ViewModels.Base;
using Management.Application.Services;
using Management.Application.Stores;
using Management.Application.DTOs;
using Management.Domain.Enums;
using Management.Application.Interfaces.App;
using CommunityToolkit.Mvvm.Messaging;
using Management.Domain.Common;
using Management.Presentation.Services;
using Management.Presentation.Services.Localization;
using Management.Domain.Services;
using Microsoft.Extensions.Logging;
using Management.Presentation.Helpers;
using Management.Presentation.ViewModels.Base;

namespace Management.Presentation.ViewModels.Shop
{

    public enum ShopViewMode { Grid, List }

    public partial class ShopViewModel : FacilityAwareViewModelBase, 
        Management.Application.Interfaces.ViewModels.INavigationalLifecycle,
        CommunityToolkit.Mvvm.Messaging.IRecipient<Management.Presentation.Messages.RefreshRequiredMessage<Management.Domain.Models.Product>>,
        Management.Presentation.ViewModels.Base.IParameterReceiver
    {
        private readonly IProductService _productService;
        private readonly ISaleService _saleService;
        private readonly Management.Domain.Services.IDialogService _dialogService;
        private readonly ProductStore _productStore;
        private readonly ISyncService _syncService;
        private bool _isDirty = true;
        private bool _isInitializing;

        protected override void OnLanguageChanged()
        {
            Title = GetTerm("Strings.Shop.Shop") ?? "Shop";
        }

        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private bool _filterAll = true;
        [ObservableProperty] private bool _filterSupplements;
        [ObservableProperty] private bool _filterApparel;
        [ObservableProperty] private bool _filterEquipment;
        [ObservableProperty] private ShopViewMode _viewMode = ShopViewMode.Grid; // Default to Grid (Shop visuals)
        [ObservableProperty] private bool _isCartVisible = true;
        [ObservableProperty] private bool _isCheckoutOpen;
        [ObservableProperty] private Cart _currentCart = new();

        // Modern Management State
        [ObservableProperty] private ProductItemViewModel? _selectedProduct;

        partial void OnSelectedProductChanged(ProductItemViewModel? oldValue, ProductItemViewModel? newValue)
        {
            if (oldValue != null) oldValue.IsActive = false;
            if (newValue != null) newValue.IsActive = true;
        }

        [ObservableProperty] private bool _isDetailOpen;
        [ObservableProperty] private bool _isEditing;
        [ObservableProperty] private bool _hasLowStock;
        [ObservableProperty] private int _lowStockCount;

        // Selection State
        [ObservableProperty] private bool _isSelectionMode;
        [ObservableProperty] private int _selectedCount;
        [ObservableProperty] private bool _selectAll;
        private bool _isUpdatingSelection;

        public ObservableRangeCollection<ProductItemViewModel> Products { get; } = new();
        public ObservableRangeCollection<ProductItemViewModel> InventoryItems => Products; // Unified for now
        public ObservableRangeCollection<ProductItemViewModel> QuickAddProducts { get; } = new();
        
        public int CartItemCount => CurrentCart?.Items.Count ?? 0;

        public async void SetParameter(object parameter)
        {
            if (parameter is string param)
            {
                if (Guid.TryParse(param, out Guid productId))
                {
                    // Ensure data is loaded
                    if (Products.Count == 0 && !IsLoading)
                    {
                        await LoadProductsAsync();
                    }

                    var product = Products.FirstOrDefault(p => p.Id == productId);
                    if (product != null)
                    {
                        SelectedProduct = product;
                        IsDetailOpen = true;
                    }
                }
                else if (param == "Inventory")
                {
                    ViewMode = ShopViewMode.List;
                }
            }
        }
        public decimal Subtotal => CurrentCart?.Total ?? 0;
        public decimal TaxAmount => 0m; 
        public decimal TotalAmount => Subtotal + TaxAmount;
        public string TaxRateDisplay => "0%";


        public IAsyncRelayCommand LoadProductsCommand { get; }
        public IAsyncRelayCommand<ProductDto> AddToCartCommand { get; }
        public IAsyncRelayCommand CheckoutCommand { get; }
        public IAsyncRelayCommand SubmitSaleCommand { get; }
        public IRelayCommand CloseDetailCommand { get; }
        public IRelayCommand EditProductCommand { get; }
        public IAsyncRelayCommand SaveProductCommand { get; }
        public IRelayCommand ClearCartCommand { get; }
        public IRelayCommand ToggleViewModeCommand { get; }
        public IAsyncRelayCommand OpenAddProductCommand { get; }
        public IAsyncRelayCommand<ProductDto> OpenEditProductCommand { get; }
        public IRelayCommand DeleteSelectedCommand { get; }
        public IRelayCommand ClearSelectionCommand { get; }


        public ShopViewModel(
            IProductService productService, 
            ISaleService saleService,
            ILogger<ShopViewModel> logger,
            IDiagnosticService diagnosticService,
            IToastService toastService,
            Management.Domain.Services.IDialogService dialogService,
            ProductStore productStore,
            Management.Domain.Services.IFacilityContextService facilityContext,
            ISyncService syncService,
            ITerminologyService terminologyService,
            ILocalizationService localizationService)
            : base(terminologyService, facilityContext, logger, diagnosticService, toastService, localizationService)
        {
            _productService = productService;
            _saleService = saleService;
            _dialogService = dialogService;
            _productStore = productStore;
            _syncService = syncService;

            _syncService.SyncCompleted += OnSyncCompleted;
            Title = GetTerm("Strings.Shop.Shop") ?? "Shop";

            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Register<Management.Presentation.Messages.RefreshRequiredMessage<Management.Domain.Models.Product>>(this);

            _productStore.StockUpdated += OnProductStockUpdated;

            LoadProductsCommand = new AsyncRelayCommand(LoadProductsAsync);
            AddToCartCommand = new AsyncRelayCommand<ProductDto>(AddToCartAsync);
            CheckoutCommand = new AsyncRelayCommand(CheckoutAsync);
            SubmitSaleCommand = new AsyncRelayCommand(SubmitSaleAsync);
            
            CloseDetailCommand = new RelayCommand(() => {
                IsDetailOpen = false;
                SelectedProduct = null;
                IsEditing = false;
            });

            EditProductCommand = new RelayCommand(() => IsEditing = true);
            
            ToggleViewModeCommand = new RelayCommand(() => 
            {
                ViewMode = ViewMode == ShopViewMode.Grid ? ShopViewMode.List : ShopViewMode.Grid;
            });

            OpenAddProductCommand = new AsyncRelayCommand(async () => {
                _toastService.ShowInfo(GetTerm("Strings.Shop.OpeningAddProductmodal") ?? "Opening product editor...");
                var result = await _dialogService.ShowCustomDialogAsync<AddProductViewModel>();
                
                if (result is ProductDto newProduct)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
                    {
                        var vm = new ProductItemViewModel(newProduct, this);
                        Products.Insert(0, vm);
                        QuickAddProducts.ReplaceRange(Products.Take(3));
                        
                        // Update stats
                        if (newProduct.StockQuantity <= newProduct.ReorderLevel)
                        {
                            LowStockCount++;
                            HasLowStock = true;
                        }
                    });
                    _toastService.ShowSuccess(string.Format(GetTerm("Strings.Shop.AddedToTheList") ?? "Added '{0}' to the list.", newProduct.Name));
                }
            });

            OpenEditProductCommand = new AsyncRelayCommand<ProductDto>(async (product) => {
                if (product == null) return;
                
                _toastService.ShowInfo(GetTerm("Strings.Shop.OpeningProductEditor") ?? "Opening product editor...");
                var result = await _dialogService.ShowCustomDialogAsync<AddProductViewModel>(product);
                
                if (result is ProductDto updatedProduct)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
                    {
                        var productVm = Products.FirstOrDefault(p => p.Id == updatedProduct.Id);
                        if (productVm != null)
                        {
                            // Update the existing VM properties
                            productVm.Name = updatedProduct.Name;
                            productVm.Price = updatedProduct.Price;
                            productVm.StockQuantity = updatedProduct.StockQuantity;
                            productVm.Sku = updatedProduct.SKU;
                            productVm.Category = updatedProduct.Category;
                            productVm.ReorderLevel = updatedProduct.ReorderLevel;
                        }
                    });
                    _toastService.ShowSuccess(string.Format(GetTerm("Strings.Shop.UpdatedSuccessfully") ?? "Updated '{0}' successfully.", updatedProduct.Name));
                }
            });

            DeleteSelectedCommand = new RelayCommand(() => 
            {
                if (SelectedCount == 0) return;
                var toRemove = Products.Where(p => p.IsSelected).ToList();
                foreach (var item in toRemove)
                {
                    Products.Remove(item);
                }
                _toastService.ShowSuccess(string.Format(GetTerm("Strings.Shop.DeletedProducts") ?? "Deleted {0} products.", toRemove.Count));
                IsSelectionMode = false;
                SelectedCount = 0;
            });

            ClearSelectionCommand = new RelayCommand(() => 
            {
                IsSelectionMode = false;
                SelectAll = false;
            });

            _productsCollectionChangedHandler = (s, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (ProductItemViewModel item in e.NewItems)
                        item.PropertyChanged += OnProductPropertyChanged;
                }
                if (e.OldItems != null)
                {
                    foreach (ProductItemViewModel item in e.OldItems)
                        item.PropertyChanged -= OnProductPropertyChanged;
                }
            };
            Products.CollectionChanged += _productsCollectionChangedHandler;

            _cartCollectionChangedHandler = (s, e) => NotifyCartChanges();
            CurrentCart.Items.CollectionChanged += _cartCollectionChangedHandler;

            SaveProductCommand = new AsyncRelayCommand(async () => {
                _toastService.ShowSuccess(GetTerm("Strings.Shop.Productsavedsuccessfully") ?? "Product saved successfully.");
                IsEditing = false;
                await Task.CompletedTask;
            });

            ClearCartCommand = new RelayCommand(() => {
                CurrentCart.Items.Clear();
                NotifyCartChanges();
            });

            _ = LoadCartAsync();
            // LoadProductsAsync is now called via LoadDeferredAsync
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public Task PreInitializeAsync() => Task.CompletedTask;

        public async Task LoadDeferredAsync()
        {
            IsActive = true;
            if (!_isDirty && !_isInitializing) return;
            
            await ExecuteLoadingAsync(async () =>
            {
                if (_isInitializing) return;
                _isInitializing = true;
                _isDirty = false;
                
                await LoadProductsAsync();
                
                _isInitializing = false;
            });
        }

        public void Receive(Management.Presentation.Messages.RefreshRequiredMessage<Management.Domain.Models.Product> message)
        {
            if (message.Value == _facilityContext.CurrentFacilityId)
            {
                _isDirty = true;
                _logger.LogInformation("[Shop] Marked dirty due to Product change.");
            }
        }

        private System.Collections.Specialized.NotifyCollectionChangedEventHandler? _productsCollectionChangedHandler;
        private System.Collections.Specialized.NotifyCollectionChangedEventHandler? _cartCollectionChangedHandler;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (Products != null)
                {
                    if (_productsCollectionChangedHandler != null)
                        Products.CollectionChanged -= _productsCollectionChangedHandler;

                    foreach (var item in Products)
                    {
                        item.PropertyChanged -= OnProductPropertyChanged;
                    }
                }

                if (CurrentCart?.Items != null && _cartCollectionChangedHandler != null)
                {
                    CurrentCart.Items.CollectionChanged -= _cartCollectionChangedHandler;
                }

                if (_productStore != null)
                {
                    _productStore.StockUpdated -= OnProductStockUpdated;
                }

                if (_syncService != null)
                {
                    _syncService.SyncCompleted -= OnSyncCompleted;
                }
            }
            base.Dispose(disposing);
        }

        private void OnProductStockUpdated(ProductDto updatedProduct)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => 
            {
                var productVm = Products.FirstOrDefault(p => p.Id == updatedProduct.Id);
                if (productVm != null)
                {
                    _logger.LogInformation($"[Shop] Updating stock for {updatedProduct.Name}: {updatedProduct.StockQuantity}");
                    productVm.StockQuantity = updatedProduct.StockQuantity;
                    
                    // Update low stock stats
                    LowStockCount = Products.Count(p => p.StockQuantity <= p.ReorderLevel);
                    HasLowStock = LowStockCount > 0;
                }
            });
        }

        private void OnProductPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProductItemViewModel.IsSelected))
            {
                UpdateSelectedCount();
            }
        }

        private void UpdateSelectedCount()
        {
            SelectedCount = Products.Count(p => p.IsSelected);
            IsSelectionMode = SelectedCount > 0;

            if (!_isUpdatingSelection)
            {
                _isUpdatingSelection = true;
                if (SelectedCount == 0) SelectAll = false;
                else if (SelectedCount == Products.Count) SelectAll = true;
                _isUpdatingSelection = false;
            }
        }

        partial void OnSelectAllChanged(bool value)
        {
            if (_isUpdatingSelection) return;

            _isUpdatingSelection = true;
            foreach (var p in Products)
            {
                p.IsSelected = value;
            }
            UpdateSelectedCount();
            _isUpdatingSelection = false;
        }

        private void NotifyCartChanges()
        {
            OnPropertyChanged(nameof(CurrentCart));
            OnPropertyChanged(nameof(CartItemCount));
            OnPropertyChanged(nameof(Subtotal));
            OnPropertyChanged(nameof(TaxAmount));
            OnPropertyChanged(nameof(TotalAmount));
        }

        private async Task LoadProductsAsync()
        {
            if (IsLoading) return;
            IsLoading = true;

            try
            {
                var facilityId = _facilityContext.CurrentFacilityId;
                var result = await _productService.GetActiveProductsAsync(facilityId);

                if (result.IsSuccess)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
                    {
                        // Bulk Update
                        var selectedId = SelectedProduct?.Id;
                        var viewModels = result.Value.Select(dto => new ProductItemViewModel(dto, this)).ToList();
                        Products.ReplaceRange(viewModels);

                        if (selectedId != null)
                        {
                            var newSelected = Products.FirstOrDefault(p => p.Id == selectedId);
                            if (newSelected != null)
                            {
                                SelectedProduct = newSelected;
                            }
                        }

                        QuickAddProducts.ReplaceRange(Products.Take(3));

                        LowStockCount = Products.Count(p => p.StockQuantity <= p.ReorderLevel);
                        HasLowStock = LowStockCount > 0;
                    });
                }
                else
                {
                    _toastService.ShowError((GetTerm("Strings.Shop.Failedtoloadproducts") ?? "Failed to load products: ") + " " + result.Error.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading products");
                _toastService.ShowError(GetTerm("Strings.Shop.Anunexpectederroroccurred_Products") ?? "An unexpected error occurred while loading products.");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task AddToCartAsync(ProductDto product)
        {
            if (product == null) return;
            await ExecuteSafeAsync(async () =>
            {
                var existingItem = CurrentCart.Items.FirstOrDefault(i => i.ProductId == product.Id);
                if (existingItem != null)
                {
                    existingItem.Quantity++;
                }
                else
                {
                    CurrentCart.Items.Add(new CartItemViewModel(product.Id, product.Name, product.Price, 1, NotifyCartChanges));
                }
                NotifyCartChanges();
                await SaveCartAsync();

            }, "Unable to add item to cart.");
        }

        private async Task SaveCartAsync() { /* Persist logic */ await Task.CompletedTask; }
        private async Task LoadCartAsync() { /* Restore logic */ await Task.CompletedTask; }

        private async Task CheckoutAsync()
        {
            if (CurrentCart == null || !CurrentCart.Items.Any())
            {
                _toastService.ShowInfo(GetTerm("Strings.Shop.Yourcartisempty") ?? "Your cart is currently empty.");
                return;
            }
            IsCheckoutOpen = true;
            await Task.CompletedTask;
        }

        private async Task SubmitSaleAsync()
        {
            if (CurrentCart == null || !CurrentCart.Items.Any()) return;

            await ExecuteSafeAsync(async () =>
            {
                var facilityId = _facilityContext.CurrentFacilityId;
                var itemsMap = CurrentCart.Items.ToDictionary(i => i.ProductId, i => i.Quantity);
                
                // Assuming payment method mapping or default
                var request = new CheckoutRequestDto(
                    Management.Domain.Enums.PaymentMethod.Cash, // Default for now
                    TotalAmount,
                    null, // Optional member linkage
                    itemsMap
                );

                var result = await _saleService.ProcessCheckoutAsync(facilityId, request);

                if (result.IsSuccess)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
                    {
                        IsCheckoutOpen = false;
                        CurrentCart.Items.Clear();
                        NotifyCartChanges();
                    });
                    await SaveCartAsync();
                    _toastService.ShowSuccess(GetTerm("Strings.Shop.Checkoutsuccessful") ?? "Checkout completed successfully.");
                }
                else
                {
                    _toastService.ShowError((GetTerm("Strings.Shop.Checkoutfailed") ?? "Checkout failed: ") + " " + result.Error.Message);
                }
            }, GetTerm("Strings.Shop.SaleProcessingFailed") ?? "Sale processing failed.");
        }

        private void OnSyncCompleted(object? sender, EventArgs e)
        {
            if (!ShouldRefreshOnSync()) return;
            System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                if (IsDisposed || IsLoading) return;
                _logger?.LogInformation("[Shop] Sync debounce passed, refreshing products...");
                await LoadProductsAsync();
            });
        }
    }
    
    // Using DTO's PaymentMethod if available or local one
    // public enum PaymentMethod { Cash, Card, MemberCredit }

    public class Cart : ObservableObject
    {
        public ObservableRangeCollection<CartItemViewModel> Items { get; } = new();
        public decimal Total => Items.Sum(i => i.UnitPrice * i.Quantity);
    }

    public partial class CartItemViewModel : ObservableObject
    {
        private readonly Action _onChanged;

        [ObservableProperty] private Guid _productId;
        [ObservableProperty] private string _productName = string.Empty;
        [ObservableProperty] private decimal _unitPrice;
        [ObservableProperty] private int _quantity;

        public IRelayCommand IncrementCommand { get; }
        public IRelayCommand DecrementCommand { get; }

        public CartItemViewModel(Guid productId, string name, decimal price, int quantity, Action onChanged)
        {
            ProductId = productId;
            ProductName = name;
            UnitPrice = price;
            Quantity = quantity;
            _onChanged = onChanged;

            IncrementCommand = new RelayCommand(() => { Quantity++; _onChanged?.Invoke(); });
            DecrementCommand = new RelayCommand(() => { if (Quantity > 1) Quantity--; _onChanged?.Invoke(); });
        }
    }
}

