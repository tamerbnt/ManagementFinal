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
        private readonly MediatR.IMediator _mediator;
        private readonly IMemberService _memberService;
        private readonly IPricingService _pricingService;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsProductSelectionActive))]
        [NotifyPropertyChangedFor(nameof(IsWalkInSelectionActive))]
        [NotifyPropertyChangedFor(nameof(IsControlPanelEmpty))]
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
        private string _walkInSearchQuery = string.Empty;

        [ObservableProperty]
        private ObservableCollection<WalkInPlanDto> _walkInPlans = new();

        [ObservableProperty]
        private ObservableCollection<WalkInPlanDto> _filteredWalkInPlans = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(WalkInPrice))]
        [NotifyPropertyChangedFor(nameof(WalkInTotal))]
        [NotifyPropertyChangedFor(nameof(GrandTotal))]
        private WalkInPlanDto? _selectedWalkInPlan;

        [ObservableProperty]
        private MemberDto? _selectedMember;

        [ObservableProperty]
        private string _memberSearchQuery = string.Empty;

        [ObservableProperty]
        private bool _isMemberSearching;

        [ObservableProperty]
        private ObservableCollection<MemberDto> _searchedMembers = new();

        public PricingResult? SelectedProductPricing { get; private set; }
        public PricingResult? SelectedWalkInPricing { get; private set; }

        public decimal WalkInPrice => SelectedWalkInPricing?.EffectivePrice.Amount ?? SelectedWalkInPlan?.Price ?? 0m;

        private List<WalkInPlanDto> _allWalkInPlans = new();


        public decimal ProductsTotal => CartItems.Sum(item => item.Price * item.Quantity);
        public decimal WalkInTotal => WalkInCount * WalkInPrice;
        public decimal GrandTotal => ProductsTotal + WalkInTotal;

        public bool CanCheckout => GrandTotal > 0;

        public int SelectedProductQuantity
        {
            get
            {
                if (SelectedProduct == null) return 0;
                var item = CartItems.FirstOrDefault(i => i.ProductId == SelectedProduct.Id);
                return item?.Quantity ?? 1; // Start at 1 for new selections
            }
        }

        public bool IsSelectedProductInCart => SelectedProduct != null && CartItems.Any(i => i.ProductId == SelectedProduct.Id);

        private List<ProductDto> _allProducts = new();

        public MultiSaleCartViewModel(
            ITerminologyService terminologyService,
            IFacilityContextService facilityContext,
            ILogger<MultiSaleCartViewModel> logger,
            IDiagnosticService diagnosticService,
            IToastService toastService,
            IProductService productService, 
            ISaleService saleService,
            ProductStore productStore,
            IGymOperationService gymOperationService,
            ModalNavigationStore modalNavigationStore,
            Management.Domain.Services.IDialogService dialogService,
            ILocalizationService localizationService,
            MediatR.IMediator mediator,
            IMemberService memberService,
            IPricingService pricingService)
            : base(terminologyService, facilityContext, logger, diagnosticService, toastService, localizationService)
        {
            _productService = productService;
            _saleService = saleService;
            _modalNavigationStore = modalNavigationStore;
            _dialogService = dialogService;
            _productStore = productStore;
            _gymOperationService = gymOperationService;
            _mediator = mediator;
            _memberService = memberService;
            _pricingService = pricingService;

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

        partial void OnSearchQueryChanged(string? oldValue, string? newValue)
        {
            FilterProducts(newValue ?? string.Empty);
        }

        partial void OnWalkInSearchQueryChanged(string? oldValue, string? newValue)
        {
            FilterWalkInPlans(newValue ?? string.Empty);
        }

        private CancellationTokenSource? _memberSearchCts;

        partial void OnMemberSearchQueryChanged(string? oldValue, string? newValue)
        {
            _memberSearchCts?.Cancel();
            if (string.IsNullOrWhiteSpace(newValue)) { SearchedMembers.Clear(); return; }
            
            _memberSearchCts = new CancellationTokenSource();
            var token = _memberSearchCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(400, token);
                    if (token.IsCancellationRequested) return;

                    var request = new MemberSearchRequest(newValue);
                    var result = await _memberService.SearchMembersAsync(_facilityContext.CurrentFacilityId, request, 1, 10);
                    if (result.IsSuccess)
                    {
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => SearchedMembers.ReplaceAll(result.Value.Items));
                    }
                }
                catch (TaskCanceledException) { }
            }, token);
        }

        [RelayCommand]
        private async Task SelectMemberAsync(MemberDto member)
        {
            SelectedMember = member;
            MemberSearchQuery = string.Empty;
            SearchedMembers.Clear();
            await RecalculateAllPricesAsync();
        }

        [RelayCommand]
        private async Task ClearSelectedMemberAsync()
        {
            SelectedMember = null;
            await RecalculateAllPricesAsync();
        }

        private async Task RecalculateAllPricesAsync()
        {
            // 1. Recalculate current selections
            await UpdateSelectedProductPricingAsync();
            await UpdateSelectedWalkInPricingAsync();

            // 2. Recalculate cart items
            foreach (var item in CartItems)
            {
                var product = _allProducts.FirstOrDefault(p => p.Id == item.ProductId);
                if (product != null)
                {
                    var result = await _pricingService.CalculateEffectivePriceAsync(
                        _facilityContext.CurrentFacilityId, 
                        product.Id, 
                        new Management.Domain.ValueObjects.Money(product.Price, "DA"),
                        SelectedMember?.Gender,
                        SelectedMember?.MembershipPlanId);
                    
                    item.Price = result.EffectivePrice.Amount;
                    item.OriginalPrice = result.OriginalPrice.Amount;
                    item.IsDiscounted = result.IsDiscountApplied;
                }
            }

            OnPropertyChanged(nameof(ProductsTotal));
            OnPropertyChanged(nameof(GrandTotal));
            OnPropertyChanged(nameof(CanCheckout));
        }

        private async Task UpdateSelectedProductPricingAsync()
        {
            if (SelectedProduct == null) { SelectedProductPricing = null; return; }
            SelectedProductPricing = await _pricingService.CalculateEffectivePriceAsync(
                _facilityContext.CurrentFacilityId,
                SelectedProduct.Id,
                new Management.Domain.ValueObjects.Money(SelectedProduct.Price, "DA"),
                SelectedMember?.Gender,
                SelectedMember?.MembershipPlanId);
            
            OnPropertyChanged(nameof(SelectedProductPricing));
            OnPropertyChanged(nameof(SelectedProductQuantity));
            OnPropertyChanged(nameof(IsSelectedProductInCart));
            OnPropertyChanged(nameof(IsProductSelectionActive));
            OnPropertyChanged(nameof(IsControlPanelEmpty));
        }

        private async Task UpdateSelectedWalkInPricingAsync()
        {
            if (SelectedWalkInPlan == null) { SelectedWalkInPricing = null; return; }
            
             SelectedWalkInPricing = await _pricingService.CalculateEffectivePriceAsync(
                _facilityContext.CurrentFacilityId,
                Guid.Empty, 
                new Management.Domain.ValueObjects.Money(SelectedWalkInPlan.Price, "DA"),
                SelectedMember?.Gender,
                SelectedMember?.MembershipPlanId);
             
             OnPropertyChanged(nameof(SelectedWalkInPricing));
             OnPropertyChanged(nameof(WalkInPrice));
             OnPropertyChanged(nameof(WalkInTotal));
             OnPropertyChanged(nameof(GrandTotal));
             OnPropertyChanged(nameof(IsWalkInSelectionActive));
             OnPropertyChanged(nameof(IsControlPanelEmpty));
        }

        partial void OnSelectedProductChanged(ProductDto? oldValue, ProductDto? newValue) => _ = UpdateSelectedProductPricingAsync();

        partial void OnSelectedWalkInPlanChanged(WalkInPlanDto? oldValue, WalkInPlanDto? newValue) => _ = UpdateSelectedWalkInPricingAsync();

        public bool IsProductSelectionActive => CurrentTab == CartTab.Products && SelectedProduct != null;
        public bool IsWalkInSelectionActive => CurrentTab == CartTab.WalkIn && SelectedWalkInPlan != null;
        public bool IsControlPanelEmpty => !IsProductSelectionActive && !IsWalkInSelectionActive;

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

        private void FilterWalkInPlans(string query)
        {
            List<WalkInPlanDto> filtered;
            if (string.IsNullOrWhiteSpace(query))
            {
                filtered = _allWalkInPlans;
            }
            else
            {
                filtered = _allWalkInPlans.Where(p =>
                    p.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }

            FilteredWalkInPlans.Clear();
            foreach (var p in filtered)
                FilteredWalkInPlans.Add(p);
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
                var price = SelectedProductPricing?.EffectivePrice.Amount ?? product.Price;
                CartItems.Add(new CartItemViewModel
                {
                    ProductId = product.Id,
                    Name = product.Name,
                    Price = price,
                    OriginalPrice = product.Price,
                    IsDiscounted = (SelectedProductPricing?.DiscountAmount.Amount ?? 0) > 0,
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
            OnPropertyChanged(nameof(SelectedProductQuantity));
            OnPropertyChanged(nameof(IsSelectedProductInCart));
        }

        [RelayCommand]
        private void IncrementSelectedProduct()
        {
            if (SelectedProduct == null) return;
            AddProductToCart(SelectedProduct);
            OnPropertyChanged(nameof(SelectedProductQuantity));
            OnPropertyChanged(nameof(IsSelectedProductInCart));
        }

        [RelayCommand]
        private void DecrementSelectedProduct()
        {
            if (SelectedProduct == null) return;
            var item = CartItems.FirstOrDefault(i => i.ProductId == SelectedProduct.Id);
            if (item != null)
            {
                if (item.Quantity > 1)
                {
                    item.Quantity--;
                }
                else
                {
                    CartItems.Remove(item);
                    // Also clear selection if it's no longer in cart? 
                    // No, let the user stay on the product but with 0/removed state.
                }
                OnPropertyChanged(nameof(ProductsTotal));
                OnPropertyChanged(nameof(GrandTotal));
                OnPropertyChanged(nameof(CanCheckout));
                OnPropertyChanged(nameof(SelectedProductQuantity));
                OnPropertyChanged(nameof(IsSelectedProductInCart));
            }
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
                _allWalkInPlans = walkInResult.ToList();
                WalkInPlans.Clear();
                FilteredWalkInPlans.Clear();
                foreach (var plan in _allWalkInPlans)
                {
                    WalkInPlans.Add(plan);
                    FilteredWalkInPlans.Add(plan);
                }
            }
        }

        [RelayCommand]
        private void ClearSelection()
        {
            if (SelectedProduct != null)
            {
                var item = CartItems.FirstOrDefault(i => i.ProductId == SelectedProduct.Id);
                if (item != null)
                {
                    CartItems.Remove(item);
                }
                SelectedProduct = null;
            }
            else if (SelectedWalkInPlan != null)
            {
                WalkInCount = 0;
                SelectedWalkInPlan = null;
            }

            OnPropertyChanged(nameof(ProductsTotal));
            OnPropertyChanged(nameof(WalkInTotal));
            OnPropertyChanged(nameof(GrandTotal));
            OnPropertyChanged(nameof(CanCheckout));
            OnPropertyChanged(nameof(SelectedProductQuantity));
            OnPropertyChanged(nameof(IsSelectedProductInCart));
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
                var saleIds = new System.Collections.Generic.List<Guid>();

                // 1. Process products via ISaleService for inventory tracking
                if (CartItems.Any())
                {
                    var itemsMap = CartItems.ToDictionary(i => i.ProductId, i => i.Quantity);
                    var productRequest = new CheckoutRequestDto(
                        Management.Domain.Enums.PaymentMethod.Cash,
                        ProductsTotal,
                        SelectedMember?.Id,
                        itemsMap
                    );

                    // Suppress notification to prevent fragmentation
                    var productResult = await _saleService.ProcessCheckoutAsync(
                        _facilityContext.CurrentFacilityId, 
                        productRequest, 
                        publishNotification: false);

                    if (!productResult.IsSuccess)
                    {
                        ShowError((GetTerm("Strings.Shop.ProductCheckoutFailed") ?? "Product checkout failed: ") + " " + productResult.Error.Message);
                        return;
                    }
                    saleIds.Add(productResult.Value);
                }

                // 2. Process walk-ins (Non-inventoried services)
                for (int i = 0; i < WalkInCount; i++)
                {
                    // Suppress notification to prevent fragmentation
                    var walkInResult = await _gymOperationService.ProcessWalkInAsync(
                        WalkInPrice, 
                        _facilityContext.CurrentFacilityId, 
                        SelectedWalkInPlan?.Name ?? "Walk-In",
                        publishNotification: false);

                    if (walkInResult.Success)
                    {
                        saleIds.Add(walkInResult.SaleId);
                    }
                }

                // 3. Publish ONE composite notification for the whole cart
                if (saleIds.Any())
                {
                    var batchIdString = string.Join(",", saleIds);
                    var itemCount = CartItems.Count + WalkInCount;
                    await _mediator.Publish(new Management.Application.Notifications.FacilityActionCompletedNotification(
                        _facilityContext.CurrentFacilityId,
                        "Checkout",
                        "Multi-Sale Cart",
                        $"Processed checkout for {itemCount} items ({GrandTotal:N0} DA)",
                        batchIdString));
                }

                // 4. Notify ViewModels to refresh (Messenger remains the same)
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
        private decimal _originalPrice;

        [ObservableProperty]
        private bool _isDiscounted;
        
        [ObservableProperty]
        private int _quantity;
    }
}
