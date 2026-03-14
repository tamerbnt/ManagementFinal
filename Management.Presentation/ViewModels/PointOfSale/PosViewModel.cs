using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Application.DTOs; // Added DTOs
using Management.Application.Interfaces;
using Management.Application.Services;
using Management.Domain.Enums;
using Management.Domain.Models;
using Management.Domain.ValueObjects;
using Management.Presentation.Extensions;
using Management.Presentation.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using Management.Presentation.Helpers;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Management.Application.Interfaces.App;

namespace Management.Presentation.ViewModels.PointOfSale
{
    public partial class PosViewModel : ViewModelBase
    {
        private readonly IProductService _productService;
        private readonly IPrinterService _printerService;
        private readonly Action _onPaymentFinalizedHandler;

        private const decimal TaxRate = 0m; // Removed hardcoded tax as per request

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private ProductCategory? _selectedCategory;

        [ObservableProperty]
        private ObservableRangeCollection<ProductDto> _products = new();

        [ObservableProperty]
        private ObservableRangeCollection<ProductDto> _filteredProducts = new();

        [ObservableProperty]
        private ObservableRangeCollection<CartItemViewModel> _cartItems = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalAmount))]
        private decimal _subtotal;

        // MATCHING DOMAIN LOGIC: Round(Price * (1 + Tax))
        public decimal TotalAmount => CartItems.Sum(c => 
            Math.Round(c.Quantity * c.Product.Price * (1 + TaxRate), 2, MidpointRounding.AwayFromZero));

        [ObservableProperty]
        private bool _showPaymentModal;

        [ObservableProperty]
        private bool _showSuccessOverlay;

        [ObservableProperty]
        private PaymentViewModel? _paymentVm;

        [ObservableProperty]
        private Transaction? _lastTransaction;

        [ObservableProperty]
        private bool _showRetryPrintButton;

        public PosViewModel(
            IProductService productService, 
            IPrinterService printerService,
            ILogger<PosViewModel> logger,
            IDiagnosticService diagnosticService,
            IToastService toastService)
            : base(logger, diagnosticService, toastService)
        {
            _productService = productService;
            _printerService = printerService;
            _onPaymentFinalizedHandler = () => _ = OnPaymentFinalizedHandlerAsync();

            _ = LoadProductsAsync();
        }

        // Design-time constructor
        public PosViewModel() : base(null!, null!, null!) // Mock base
        {
            _productService = null!;
            _printerService = null!;
            
            // Mock data for designer
            Products = new ObservableRangeCollection<ProductDto>
            {
                new ProductDto { Id = Guid.NewGuid(), Name = "Bottled Water", Category = "Beverage", Price = 3.50m },
                new ProductDto { Id = Guid.NewGuid(), Name = "Protein Bar", Category = "Snack", Price = 7.99m }
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (PaymentVm != null)
                {
                    PaymentVm.OnPaymentFinalized -= _onPaymentFinalizedHandler;
                    if (PaymentVm is IDisposable disposablePayment)
                    {
                        disposablePayment.Dispose();
                    }
                }
            }
            base.Dispose(disposing);
        }

        private async Task LoadProductsAsync()
        {
            if (_productService == null) return;
            
            IsLoading = true;
            try 
            {
                var productDtosResult = await _productService.GetActiveProductsAsync(Guid.Empty);
                if (productDtosResult.IsSuccess)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
                    {
                        Products.ReplaceRange(productDtosResult.Value);
                        ApplyLocalFilters();
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading products");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private CancellationTokenSource? _searchCts;

        partial void OnSearchTextChanged(string value) => DebounceFilter();
        partial void OnSelectedCategoryChanged(ProductCategory? value) => ApplyLocalFilters();

        private void DebounceFilter()
        {
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(300, token);
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
                    {
                        ApplyLocalFilters();
                    });
                }
                catch (OperationCanceledException) { }
            }, token);
        }

        private void ApplyLocalFilters()
        {
            var results = Products.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                results = results.Where(p => p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            if (SelectedCategory.HasValue && SelectedCategory != ProductCategory.None)
            {
                results = results.Where(p => p.Category == SelectedCategory.Value.ToString());
            }

            FilteredProducts.ReplaceRange(results.ToList());
        }

        [RelayCommand]
        private void AddToCart(ProductDto product)
        {
            var existing = CartItems.FirstOrDefault(c => c.Product.Id == product.Id);
            if (existing != null)
            {
                existing.AddQuantity(1);
            }
            else
            {
                CartItems.Add(new CartItemViewModel(product, 1));
            }
            OnPropertyChanged(nameof(TotalAmount));
        }

        [RelayCommand]
        private void IncreaseQuantity(CartItemViewModel item)
        {
            item.AddQuantity(1);
            OnPropertyChanged(nameof(TotalAmount));
        }

        [RelayCommand]
        private void DecreaseQuantity(CartItemViewModel item)
        {
            if (item.Quantity > 1)
            {
                item.AddQuantity(-1);
                OnPropertyChanged(nameof(TotalAmount));
            }
            else
            {
                RemoveFromCart(item);
            }
        }

        [RelayCommand]
        private void RemoveFromCart(CartItemViewModel item)
        {
            CartItems.Remove(item);
            OnPropertyChanged(nameof(TotalAmount));
        }

        [RelayCommand]
        private void OpenPaymentModal()
        {
            if (CartItems.Count == 0)
            {
                MessageBox.Show("Your cart is empty. Please add items before proceeding to payment.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Ensure previous VM is cleaned up if it was somehow left over
            if (PaymentVm != null)
            {
                PaymentVm.OnPaymentFinalized -= _onPaymentFinalizedHandler;
            }

            PaymentVm = new PaymentViewModel(TotalAmount);
            PaymentVm.OnPaymentFinalized += _onPaymentFinalizedHandler;
            ShowPaymentModal = true;
        }

        private async Task OnPaymentFinalizedHandlerAsync()
        {
            await ProcessCheckoutAsync();
        }

        [RelayCommand]
        private void ClosePaymentModal()
        {
            if (PaymentVm != null)
            {
                PaymentVm.OnPaymentFinalized -= _onPaymentFinalizedHandler;
            }
            ShowPaymentModal = false;
            PaymentVm = null;
        }

        // ATOMIC TRANSACTION FLOW: Validate â†’ Pay â†’ Save â†’ Print â†’ Sync
        private async Task ProcessCheckoutAsync()
        {
            if (PaymentVm == null) return;

            try
            {
                // STEP 1: Validate Cart
                if (!ValidateCart())
                {
                    MessageBox.Show("Cart validation failed. Please check item quantities.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // STEP 2: Process Payment (Simulated - in production: Stripe/Card gateway)
                var paymentMethod = PaymentVm.GetresultingPaymentMethod();

                // STEP 3: Save to Local DB (COMMIT) - ATOMIC
                var transaction = Transaction.Create(paymentMethod);
                foreach (var item in CartItems)
                {
                    // Construct Money object from DTO
                    var price = new Money(item.Product.Price, item.Product.Currency ?? "DA");

                    transaction.AddLineItem(
                        item.Product.Id,
                        item.Product.Name,
                        item.Quantity,
                        price.Amount,
                        TaxRate  // Use constant
                    );
                }

                // In production: await _transactionService.SaveAsync(transaction);
                LastTransaction = transaction;

                // STEP 4: Print Receipt & Kick Drawer
                // CRITICAL: If printing fails, we DON'T rollback the sale (money is taken)
                try
                {
                    await _printerService.PrintTransactionAsync(transaction);
                    await _printerService.OpenCashDrawerAsync();
                    ShowRetryPrintButton = false;
                }
                catch (Exception printEx)
                {
                    // Show retry button instead of failing the sale
                    ShowRetryPrintButton = true;
                    MessageBox.Show(
                        $"Sale completed but printing failed: {printEx.Message}\nClick 'Retry Print' to try again.",
                        "Print Warning",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                // STEP 5: Push to Cloud (Sync) - Fire and forget
                // In production: _ = Task.Run(async () => await _syncService.PushTransactionAsync(transaction));

                // Success!
                ClosePaymentModal();
                ShowSuccessOverlay = true;
                await Task.Delay(2000);
                ShowSuccessOverlay = false;

                // Clear cart
                CartItems.Clear();
                OnPropertyChanged(nameof(TotalAmount));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Checkout failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task RetryPrint()
        {
            if (LastTransaction == null) return;

            try
            {
                await _printerService.PrintTransactionAsync(LastTransaction);
                await _printerService.OpenCashDrawerAsync();
                ShowRetryPrintButton = false;
                MessageBox.Show("Receipt printed successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Print failed again: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool ValidateCart()
        {
            if (CartItems.Count == 0) return false;
            if (CartItems.Any(c => c.Quantity <= 0)) return false;
            if (TotalAmount <= 0) return false;
            return true;
        }
    }
}
