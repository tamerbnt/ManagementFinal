using System;
using System.Diagnostics;

using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Application.Interfaces;
using Management.Domain.Models;
using Management.Domain.Interfaces;

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
using Microsoft.Extensions.DependencyInjection;
using Management.Presentation.Messages;

namespace Management.Presentation.ViewModels.Shop
{

    public enum ShopViewMode { Grid, List }

    public partial class ShopViewModel : FacilityAwareViewModelBase, 
        Management.Application.Interfaces.ViewModels.INavigationalLifecycle,
        CommunityToolkit.Mvvm.Messaging.IRecipient<Management.Presentation.Messages.RefreshRequiredMessage<Management.Domain.Models.Product>>,
        Management.Presentation.ViewModels.Base.IParameterReceiver
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IModalNavigationService _modalNavigationService;
        private readonly ISaleService _saleService;
        private readonly Management.Domain.Services.IDialogService _dialogService;
        private readonly ProductStore _productStore;
        private readonly ISyncService _syncService;
        private bool _isDirty = true;

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

        public Management.Domain.Enums.FacilityType CurrentFacilityType => CurrentFacility;

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

        [ObservableProperty] private int _currentPage = 1;
        [ObservableProperty] private int _pageSize = 24;
        [ObservableProperty] private bool _hasMoreItems;
        [ObservableProperty] private int _totalCount;

        public ObservableRangeCollection<ProductItemViewModel> Products { get; } = new();
        public ObservableRangeCollection<ProductItemViewModel> InventoryItems => Products; // Unified for now
        public ObservableRangeCollection<ProductItemViewModel> QuickAddProducts { get; } = new();
        
        public int CartItemCount => CurrentCart?.Items.Count ?? 0;

        public async Task SetParameterAsync(object parameter)
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
        public IAsyncRelayCommand LoadMoreCommand { get; }
        public IAsyncRelayCommand<ProductDto> AddToCartCommand { get; }
        public IAsyncRelayCommand CheckoutCommand { get; }
        public IAsyncRelayCommand SubmitSaleCommand { get; }
        public IRelayCommand CloseDetailCommand { get; }
        public IRelayCommand EditProductCommand { get; }
        public IAsyncRelayCommand SaveProductCommand { get; }
        public IAsyncRelayCommand CancelEditCommand { get; }
        public IRelayCommand ClearCartCommand { get; }
        public IRelayCommand ToggleViewModeCommand { get; }
        public IAsyncRelayCommand OpenAddProductCommand { get; }
        public IAsyncRelayCommand OpenEditProductCommand { get; }
        public IAsyncRelayCommand<ProductDto> OpenRestockProductCommand { get; }
        public IAsyncRelayCommand DeleteSelectedCommand { get; }
        public IRelayCommand ClearSelectionCommand { get; }


        public ShopViewModel(
            IServiceScopeFactory scopeFactory,
            ISaleService saleService,
            ILogger<ShopViewModel> logger,
            IDiagnosticService diagnosticService,
            IToastService toastService,
            Management.Domain.Services.IDialogService dialogService,
            ProductStore productStore,
            Management.Domain.Services.IFacilityContextService facilityContext,
            ISyncService syncService,
            ITerminologyService terminologyService,
            ILocalizationService localizationService,
            IModalNavigationService modalNavigationService)
            : base(terminologyService, facilityContext, logger, diagnosticService, toastService, localizationService)
        {
            _scopeFactory = scopeFactory;
            _saleService = saleService;
            _dialogService = dialogService;
            _productStore = productStore;
            _syncService = syncService;
            _modalNavigationService = modalNavigationService;

            _syncService.SyncCompleted += OnSyncCompleted;
            Title = GetTerm("Strings.Shop.Shop") ?? "Shop";

            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Register<Management.Presentation.Messages.RefreshRequiredMessage<Management.Domain.Models.Product>>(this);

            _productStore.StockUpdated += OnProductStockUpdated;
            
            LoadProductsCommand = new AsyncRelayCommand(async () => {
                CurrentPage = 1;
                await ExecuteLoadingAsync(() => LoadProductsAsync(false));
            });

            LoadMoreCommand = new AsyncRelayCommand(async () => {
                if (!HasMoreItems) return;
                CurrentPage++;
                await ExecuteLoadingAsync(() => LoadProductsAsync(true));
            });

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
                try
                {
                    // Open the modal and wait for it to close.
                    // AddProductViewModel handles saving + success toast + RefreshRequiredMessage internally.
                    // We do NOT manually insert into Products here — the DB reload triggered by
                    // RefreshRequiredMessage will add it correctly, avoiding a duplicate-insert race.
                    await _dialogService.ShowCustomDialogAsync<AddProductViewModel>(null);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error opening add product modal.");
                    _toastService.ShowError(GetTerm("Strings.Shop.FailedToOpenAddProductModal") ?? "Failed to open add product modal.");
                }
            });

            OpenEditProductCommand = new AsyncRelayCommand<ProductDto>(async (product) => {
                if (product == null) return;
                
                _toastService.ShowInfo(GetTerm("Strings.Shop.OpeningProductEditor") ?? "Opening product editor...");
                var result = await _dialogService.ShowCustomDialogAsync<AddProductViewModel>(product);
                
                // Note: UI logic and toast notifications are handled natively by AddProductViewModel 
                // and the resulting global StockUpdated/Refresh events to eliminate UI race conditions.
            });

            OpenRestockProductCommand = new AsyncRelayCommand<ProductDto>(async (product) => {
                if (product == null) return;
                
                // We'll use the same dialog system for LogRestockViewModel
                _toastService.ShowInfo("Opening Restock form...");
                await _dialogService.ShowCustomDialogAsync<LogRestockViewModel>(product);
            });

            DeleteSelectedCommand = new AsyncRelayCommand(async () => 
            {
                if (SelectedCount == 0) return;
                var toRemove = Products.Where(p => p.IsSelected).ToList();
                var removedIds = toRemove.Select(p => p.Id).ToList();
                var facilityId = _facilityContext.CurrentFacilityId;

                // Atomic Pattern: Delete -> Save (Service handles) -> Notify with Undo
                using var scope = _scopeFactory.CreateScope();
                var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
                
                bool anyFailed = false;
                foreach (var id in removedIds)
                {
                    var result = await productService.DeleteProductAsync(facilityId, id);
                    if (result.IsFailure) anyFailed = true;
                }

                if (!anyFailed)
                {
                    foreach (var item in toRemove)
                    {
                        Products.Remove(item);
                    }

                    IsSelectionMode = false;
                    SelectedCount = 0;

                    _toastService.ShowSuccess(
                        $"{toRemove.Count} product(s) deleted.",
                        undoAction: async () => 
                        {
                            using var undoScope = _scopeFactory.CreateScope();
                            var undoService = undoScope.ServiceProvider.GetRequiredService<IProductService>();
                            foreach (var id in removedIds)
                            {
                                await undoService.RestoreProductAsync(facilityId, id);
                            }
                            
                            await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () => 
                            {
                                await LoadProductsAsync(force: true);
                            });
                        });
                }
                else
                {
                    _toastService.ShowError("Some products failed to delete.");
                }
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
                if (SelectedProduct == null) return;

                await ExecuteSafeAsync(async () =>
                {
                    var dto = SelectedProduct.GetType().GetMethod("CreateCurrentDto", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(SelectedProduct, null) as ProductDto;
                    
                    if (dto == null)
                    {
                        _toastService.ShowError("Failed to extract product payload.");
                        return;
                    }

                    using var scope = _scopeFactory.CreateScope();
                    var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
                    var result = await productService.UpdateProductAsync(_facilityContext.CurrentFacilityId, dto);

                    if (result.IsSuccess)
                    {
                        _toastService.ShowSuccess(GetTerm("Strings.Shop.Productsavedsuccessfully") ?? "Product saved successfully.");
                        IsEditing = false;

                        // OnProductStockUpdated (via ProductStore.StockUpdated) already updates the
                        // in-memory item instantly. Now do a silent background DB reload to confirm.
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(150); // brief yield so StockUpdated fires first
                            await System.Windows.Application.Current.Dispatcher.InvokeAsync(
                                async () => await LoadProductsAsync());
                        });
                    }
                    else
                    {
                        _toastService.ShowError($"Failed to save product: {result.Error}");
                    }
                }, "An error occurred while saving the product.");
            });

            CancelEditCommand = new AsyncRelayCommand(async () => {
                if (SelectedProduct == null)
                {
                    IsEditing = false;
                    return;
                }
                
                // Revert any unsaved TwoWay binding mutations by fetching the true DB state
                try 
                {
                    using var scope = _scopeFactory.CreateScope();
                    var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
                    var result = await productService.GetProductAsync(_facilityContext.CurrentFacilityId, SelectedProduct.Id);
                    
                    if (result.IsSuccess && result.Value != null)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() => SelectedProduct.UpdateFromDto(result.Value));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to revert product changes.");
                }
                finally
                {
                    IsEditing = false;
                }
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
            if (!_isDirty) return;
            _isDirty = false;

            await ExecuteLoadingAsync(() => LoadProductsAsync());
        }

        public void Receive(RefreshRequiredMessage<Product> message)
        {
            if (message.Value != _facilityContext.CurrentFacilityId) return;

            _logger?.LogInformation("[Shop] RefreshRequiredMessage received — reloading product list.");

            // Call LoadProductsAsync directly on a background thread.
            // We deliberately bypass LoadDeferredAsync and ExecuteLoadingAsync here because
            // this message fires while SaveProductCommand or SaveAsync may already hold
            // the IsLoading semaphore, causing a silent abort.
            // LoadProductsAsync is safe to run concurrently — it uses its own scoped DbContext.
            _ = Task.Run(async () =>
            {
                await Task.Delay(300); // brief yield so the DB commit finishes before we read
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(
                    async () => await LoadProductsAsync());
            });
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
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
            {
                var productVm = Products.FirstOrDefault(p => p.Id == updatedProduct.Id);
                if (productVm != null)
                {
                    _logger.LogInformation($"[Shop] Synchronizing from backend event for {updatedProduct.Name}: {updatedProduct.StockQuantity}");
                    productVm.UpdateFromDto(updatedProduct);
                    
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

        private void UpdateQuickAddProducts()
        {
            QuickAddProducts.ReplaceRange(Products.Take(3));
        }

        private ProductCategory? GetSelectedCategory()
        {
            if (FilterSupplements) return ProductCategory.Supplements;
            if (FilterApparel) return ProductCategory.Apparel;
            if (FilterEquipment) return ProductCategory.Equipment;
            return null; // If _filterAll is true or no specific filter is selected
        }

        // ─── Filter change handlers ──────────────────────────────────────────────
        // WPF RadioButton groups fire PropertyChanged TWICE per click:
        //   (1) the previously-selected button flips to false  → guard returns early
        //   (2) the newly-selected button flips to true        → we fire the reload
        //
        // IMPORTANT: We call LoadProductsAsync directly via the Dispatcher (the same
        // pattern used by Receive()) instead of ExecuteLoadingAsync.
        // ExecuteLoadingAsync uses a 0-ms semaphore that SILENTLY DROPS the call if
        // the initial page load is still in-flight, making filter clicks unreliable.

        partial void OnFilterAllChanged(bool value)
        {
            if (!value) return;
            CurrentPage = 1;
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(
                async () => await LoadProductsAsync());
        }

        partial void OnFilterSupplementsChanged(bool value)
        {
            if (!value) return;
            CurrentPage = 1;
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(
                async () => await LoadProductsAsync());
        }

        partial void OnFilterApparelChanged(bool value)
        {
            if (!value) return;
            CurrentPage = 1;
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(
                async () => await LoadProductsAsync());
        }

        partial void OnFilterEquipmentChanged(bool value)
        {
            if (!value) return;
            CurrentPage = 1;
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(
                async () => await LoadProductsAsync());
        }

        private async Task LoadProductsAsync()
        {
            await LoadProductsAsync(false);
        }

        public async Task LoadProductsAsync(bool isLoadMore = false, bool force = false)
        {
            try
            {
                var facilityId = _facilityContext.CurrentFacilityId;
                
                using (var scope = _scopeFactory.CreateScope())
                {
                    var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
                    
                    // CORRECT FIX: Pass the selected category to the search query
                    var selectedCategory = GetSelectedCategory();
                    var result = await productService.SearchProductsPagedAsync(facilityId, SearchText, CurrentPage, PageSize, selectedCategory);

                    if (result.IsSuccess)
                    {
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
                        {
                            var selectedId = SelectedProduct?.Id;
                            var viewModels = result.Value.Items.Select(dto => new ProductItemViewModel(dto, this)).ToList();
                            
                            if (isLoadMore)
                            {
                                Products.AddRange(viewModels);
                            }
                            else
                            {
                                Products.ReplaceRange(viewModels);
                            }

                            TotalCount = result.Value.TotalCount;
                            HasMoreItems = Products.Count < TotalCount;

                            if (selectedId != null)
                            {
                                var newSelected = Products.FirstOrDefault(p => p.Id == selectedId);
                                if (newSelected != null)
                                {
                                    SelectedProduct = newSelected;
                                }
                            }

                            UpdateQuickAddProducts();

                            LowStockCount = Products.Count(p => p.StockQuantity <= p.ReorderLevel);
                            HasLowStock = LowStockCount > 0;
                        });
                    }
                    else
                    {
                        _toastService.ShowError((GetTerm("Strings.Shop.Failedtoloadproducts") ?? "Failed to load products: ") + " " + result.Error.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading products");
                _toastService.ShowError(GetTerm("Strings.Shop.Anunexpectederroroccurred_Products") ?? "An unexpected error occurred while loading products.");
            }
            finally
            {
                // IsLoading is managed by ExecuteLoadingAsync caller
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

