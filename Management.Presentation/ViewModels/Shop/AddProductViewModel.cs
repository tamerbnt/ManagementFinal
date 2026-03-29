using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Management.Application.DTOs;
using Management.Application.Interfaces.App;
using Management.Application.Services;
using Management.Domain.Services;
using Management.Presentation.Extensions;
using Management.Presentation.ViewModels.Base;
using Management.Presentation.Stores;
using Management.Presentation.Services.Localization;
using Management.Domain.Primitives;
using Management.Application.Interfaces.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Management.Presentation.ViewModels.Shop
{
    public partial class AddProductViewModel : FacilityAwareViewModelBase, IModalAware
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ModalNavigationStore _modalNavigationStore;

        [ObservableProperty] private string _name = string.Empty;
        [ObservableProperty] private string _description = string.Empty;
        [ObservableProperty] private string _sku = string.Empty;
        [ObservableProperty] private string _category = "Supplements";
        [ObservableProperty] private string? _imageUrl;
        [ObservableProperty] private decimal _price;
        [ObservableProperty] private decimal _cost;
        [ObservableProperty] private int _stockQuantity;
        [ObservableProperty] private int _reorderLevel = 10;
        
        [ObservableProperty] private bool _isEditMode;
        private Guid _productId;

        private readonly IDialogService _dialogService;

        public AddProductViewModel(
            ITerminologyService terminologyService,
            IServiceScopeFactory scopeFactory,
            IFacilityContextService facilityContext,
            ILogger<AddProductViewModel> logger,
            IDiagnosticService diagnosticService,
            IToastService toastService,
            IDialogService dialogService,
            ModalNavigationStore modalNavigationStore,
            ILocalizationService localizationService)
            : base(terminologyService, facilityContext, logger, diagnosticService, toastService, localizationService)
        {
            _scopeFactory = scopeFactory;
            _modalNavigationStore = modalNavigationStore;
            _dialogService = dialogService;
            UpdateTitle();
        }

        public Task OnModalOpenedAsync(object parameter, CancellationToken cancellationToken = default)
        {
            if (parameter is ProductDto product)
            {
                IsEditMode = true;
                _productId = product.Id;
                Name = product.Name;
                Sku = product.SKU;
                Category = product.Category ?? "Supplements";
                Price = product.Price;
                Cost = product.Cost;
                StockQuantity = product.StockQuantity;
                ReorderLevel = product.ReorderLevel;
                // Preserve description and image
                Description = product.Description;
                ImageUrl = product.ImageUrl;
                UpdateTitle();
            }
            return Task.CompletedTask;
        }

        private void UpdateTitle()
        {
            Title = IsEditMode 
                ? (GetTerm("Strings.Shop.EditProduct") ?? "Edit Product")
                : (GetTerm("Strings.Shop.AddNewProduct") ?? "Add New Product");
        }

        protected override void OnLanguageChanged()
        {
            UpdateTitle();
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                _toastService.ShowError(GetTerm("Strings.Shop.ProductNameisrequired") ?? "Product Name is required.");
                return;
            }

            await ExecuteLoadingAsync(async () =>
            {
                var product = new ProductDto
                {
                    Id = IsEditMode ? _productId : Guid.NewGuid(),
                    Name = Name,
                    Description = Description,
                    ImageUrl = ImageUrl,
                    SKU = Sku,
                    Category = Category,
                    Price = Price,
                    Cost = Cost,
                    StockQuantity = StockQuantity,
                    ReorderLevel = ReorderLevel
                };

                // CRITICAL FIX: resolve IProductService from a FRESH scope so the underlying
                // UpdateProductCommandHandler gets its own isolated AppDbContext.
                // Resolving from the root container gives the handler the long-lived root-scope
                // DbContext which can be in a stale/dirty state from previous operations.
                Result result;
                using var scope = _scopeFactory.CreateScope();
                var productService = scope.ServiceProvider.GetRequiredService<IProductService>();

                if (IsEditMode)
                {
                    result = await productService.UpdateProductAsync(_facilityContext.CurrentFacilityId, product);
                }
                else
                {
                    result = await productService.CreateProductAsync(_facilityContext.CurrentFacilityId, product);
                }

                if (result.IsSuccess)
                {
                    // Notify ViewModels to refresh (Dirty Flag)
                    CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new Management.Presentation.Messages.RefreshRequiredMessage<Management.Domain.Models.Product>(_facilityContext.CurrentFacilityId));

                    string message = IsEditMode 
                        ? string.Format(GetTerm("Strings.Shop.ProductUpdatedSuccessfully") ?? "Product '{0}' updated successfully.", Name)
                        : string.Format(GetTerm("Strings.Shop.ProductAddedSuccessfully") ?? "Product '{0}' added successfully.", Name);

                    _toastService.ShowSuccess(message);
                    await _modalNavigationStore.CloseAsync(ModalResult.Success(product));
                }
                else
                {
                    _toastService.ShowError($"{(IsEditMode ? "Failed to update" : "Failed to add")} product: {result.Error}");
                }
            }, null);
        }

        [RelayCommand]
        private async Task DeleteAsync()
        {
            if (!IsEditMode || _productId == Guid.Empty) return;

            var facilityId = _facilityContext.CurrentFacilityId;
            var productId = _productId;
            var productName = Name;

            await ExecuteLoadingAsync(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
                var result = await productService.DeleteProductAsync(facilityId, productId);

                if (result.IsSuccess)
                {
                    WeakReferenceMessenger.Default.Send(new Management.Presentation.Messages.RefreshRequiredMessage<Management.Domain.Models.Product>(facilityId));
                    _toastService.ShowSuccess(
                        $"Product '{productName}' deleted.",
                        undoAction: async () => 
                        {
                            using var undoScope = _scopeFactory.CreateScope();
                            var undoService = undoScope.ServiceProvider.GetRequiredService<IProductService>();
                            await undoService.RestoreProductAsync(facilityId, productId);
                            WeakReferenceMessenger.Default.Send(new Management.Presentation.Messages.RefreshRequiredMessage<Management.Domain.Models.Product>(facilityId));
                        });
                    
                    await _modalNavigationStore.CloseAsync(ModalResult.Success());
                }
                else
                {
                    _toastService.ShowError($"Failed to delete product: {result.Error}");
                }
            }, "Deleting product...");
        }

        [RelayCommand]
        private async Task CancelAsync()
        {
            await _modalNavigationStore.CloseAsync(ModalResult.Cancel());
        }
    }
}
