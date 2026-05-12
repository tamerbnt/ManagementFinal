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
using Management.Presentation.Services;

namespace Management.Presentation.ViewModels.Shop
{
    public partial class QuickSaleViewModel : FacilityAwareViewModelBase
    {
        private readonly IProductService _productService;
        private readonly ISaleService _saleService;
        private readonly IMemberService _memberService;
        private readonly ModalNavigationStore _modalNavigationStore;
        private readonly ProductStore _productStore;
        private readonly IPricingService _pricingService;
        private readonly IDiscountService _discountService;

        [ObservableProperty]
        private ObservableRangeCollection<ProductDto> _products = new();
        private List<ProductDto> _allProducts = new();

        [ObservableProperty]
        private ObservableRangeCollection<MemberDto> _searchedMembers = new();

        [ObservableProperty]
        private MemberDto? _selectedMember;

        [ObservableProperty]
        private string _memberSearchQuery = string.Empty;

        [ObservableProperty]
        private bool _isMemberSearching;


        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ProcessSaleCommand))]
        private ProductDto? _selectedProduct;

        partial void OnSelectedProductChanged(ProductDto? oldValue, ProductDto? newValue)
        {
            _ = UpdatePricingAsync();
        }

        [ObservableProperty]
        private decimal _effectivePrice;

        [ObservableProperty]
        private decimal? _originalPrice;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsDiscounted))]
        private decimal? _discountAmount;

        [ObservableProperty]
        private string? _appliedPromotionName;

        [ObservableProperty]
        private ObservableCollection<DiscountDto> _availableDiscounts = new();

        [ObservableProperty]
        private DiscountDto? _selectedDiscount;

        public bool IsDiscounted => DiscountAmount > 0;

        public bool CanProcessSale => SelectedProduct != null;

        public QuickSaleViewModel(
            ITerminologyService terminologyService,
            IFacilityContextService facilityContext,
            ILogger<QuickSaleViewModel> logger,
            IDiagnosticService diagnosticService,
            IToastService toastService,
            IProductService productService,
            ISaleService saleService,
            IMemberService memberService,
            ModalNavigationStore modalNavigationStore,
            ProductStore productStore,
            ILocalizationService localizationService,
            IPricingService pricingService,
            IDiscountService discountService)
            : base(terminologyService, facilityContext, logger, diagnosticService, toastService, localizationService)
        {
            _productService = productService;
            _saleService = saleService;
            _memberService = memberService;
            _modalNavigationStore = modalNavigationStore;
            _productStore = productStore;
            _pricingService = pricingService;
            _discountService = discountService;

            
            Title = GetTerm("Strings.Shop.QuickSale") ?? "Quick Sale";
            _productStore.StockUpdated += OnProductStockUpdated;
            _ = LoadProductsAsync();
            _ = LoadDiscountsAsync();
        }

        [RelayCommand]
        private async Task LoadDiscountsAsync()
        {
            var result = await _discountService.GetDiscountsAsync(_facilityContext.CurrentFacilityId);
            if (result.IsSuccess)
            {
                AvailableDiscounts = new ObservableCollection<DiscountDto>(result.Value.Where(d => d.IsActive));
            }
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

        partial void OnSearchQueryChanged(string? oldValue, string? newValue)
        {
            FilterProducts(newValue ?? string.Empty);
        }

        private CancellationTokenSource? _memberSearchCts;

        partial void OnMemberSearchQueryChanged(string? oldValue, string? newValue)
        {
            _memberSearchCts?.Cancel();
            _memberSearchCts = new CancellationTokenSource();
            var token = _memberSearchCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(400, token);
                    if (token.IsCancellationRequested) return;

                    await SearchMembersAsync(newValue ?? string.Empty);
                }
                catch (TaskCanceledException) { }
            }, token);
        }

        private async Task SearchMembersAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => SearchedMembers.Clear());
                return;
            }

            IsMemberSearching = true;
            try
            {
                var request = new MemberSearchRequest(query);
                var result = await _memberService.SearchMembersAsync(_facilityContext.CurrentFacilityId, request, 1, 10);

                if (result.IsSuccess)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        SearchedMembers.ReplaceRange(result.Value.Items);
                    });
                }
            }
            finally
            {
                IsMemberSearching = false;
            }
        }

        [RelayCommand]
        private async Task SelectMemberAsync(MemberDto member)
        {
            SelectedMember = member;
            MemberSearchQuery = string.Empty;
            SearchedMembers.Clear();
            await UpdatePricingAsync();
        }

        [RelayCommand]
        private async Task ClearSelectedMemberAsync()
        {
            SelectedMember = null;
            await UpdatePricingAsync();
        }

        partial void OnSelectedDiscountChanged(DiscountDto? value) => _ = UpdatePricingAsync();

        private async Task UpdatePricingAsync()
        {
            if (SelectedProduct == null) 
            {
                EffectivePrice = 0;
                OriginalPrice = null;
                DiscountAmount = null;
                AppliedPromotionName = null;
                return;
            }

            var basePrice = new Management.Domain.ValueObjects.Money(SelectedProduct.Price, "DA");
            
            decimal? manualVal = null;
            bool isPerc = false;
            if (SelectedDiscount != null)
            {
                isPerc = SelectedDiscount.IsPercentage;
                manualVal = SelectedDiscount.Value;
            }

            var result = await _pricingService.CalculateEffectivePriceAsync(
                _facilityContext.CurrentFacilityId,
                SelectedProduct.Id,
                basePrice,
                SelectedMember?.Gender,
                SelectedMember?.MembershipPlanId,
                manualDiscountValue: manualVal,
                isManualDiscountPercentage: isPerc);

            EffectivePrice = result.EffectivePrice.Amount;
            OriginalPrice = result.IsDiscountApplied ? result.OriginalPrice.Amount : null;
            DiscountAmount = result.IsDiscountApplied ? result.DiscountAmount.Amount : null;
            AppliedPromotionName = result.AppliedPromotionName;
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
        private async Task SetSelectedProductAsync(ProductDto product)
        {
            SelectedProduct = product;
            await UpdatePricingAsync();
        }

        [RelayCommand(CanExecute = nameof(CanProcessSale))]
        private async Task ProcessSaleAsync()
        {
            System.Diagnostics.Debug.WriteLine("[QUICKSALE] ProcessSaleAsync started");
            if (SelectedProduct == null) return;

            await ExecuteSafeAsync(async () =>
            {
                var itemsMap = new Dictionary<Guid, int> { { SelectedProduct.Id, 1 } };
                var request = new CheckoutRequestDto(
                    Management.Domain.Enums.PaymentMethod.Cash,
                    EffectivePrice, // Use discounted price
                    SelectedMember?.Id,
                    itemsMap,
                    ManualDiscountId: SelectedDiscount?.Id,
                    ManualDiscountAmount: OriginalPrice.HasValue ? (OriginalPrice.Value - EffectivePrice) : 0
                );


                System.Diagnostics.Debug.WriteLine("[QUICKSALE] Sending ProcessCheckoutCommand via SaleService");
                var result = await _saleService.ProcessCheckoutAsync(_facilityContext.CurrentFacilityId, request);
                System.Diagnostics.Debug.WriteLine($"[QUICKSALE] Sale result: {(result.IsSuccess ? "Success" : $"Failure: {result.Error.Message}")}");

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
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (IsDisposed) return;

                var product = _allProducts.FirstOrDefault(p => p.Id == updatedProduct.Id);
                if (product != null)
                {
                    product.StockQuantity = updatedProduct.StockQuantity;
                    // Since this uses Record/ObservableRangeCollection with NotifyCollectionChanged, 
                    // but the individual record properties might not notify unless they are mutable and have NotifyPropertyChanged.
                    // ProductDto properties ARE mutable but do NOT have NotifyPropertyChanged.
                    // So we must Refresh the filtered list on the UI thread to reflect changes.
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
}
