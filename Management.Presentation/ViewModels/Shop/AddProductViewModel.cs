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
using Management.Domain.Services;
using Management.Presentation.Services.Localization;
using Management.Domain.Primitives;
using Management.Application.Interfaces.ViewModels;
using Microsoft.Extensions.Logging;

namespace Management.Presentation.ViewModels.Shop
{
    public partial class AddProductViewModel : FacilityAwareViewModelBase, IModalAware
    {
        private readonly IProductService _productService;
        private readonly ModalNavigationStore _modalNavigationStore;

        [ObservableProperty] private string _name = string.Empty;
        [ObservableProperty] private string _sku = string.Empty;
        [ObservableProperty] private string _category = "Supplements";
        [ObservableProperty] private decimal _price;
        [ObservableProperty] private decimal _cost;
        [ObservableProperty] private int _stockQuantity;
        [ObservableProperty] private int _reorderLevel = 10;

        private bool _isEditMode;
        private Guid _productId;

        public AddProductViewModel(
            ITerminologyService terminologyService,
            IProductService productService,
            IFacilityContextService facilityContext,
            ILogger<AddProductViewModel> logger,
            IDiagnosticService diagnosticService,
            IToastService toastService,
            ModalNavigationStore modalNavigationStore,
            ILocalizationService localizationService)
            : base(terminologyService, facilityContext, logger, diagnosticService, toastService, localizationService)
        {
            _productService = productService;
            _modalNavigationStore = modalNavigationStore;
            UpdateTitle();
        }

        public Task OnModalOpenedAsync(object parameter, CancellationToken cancellationToken = default)
        {
            if (parameter is ProductDto product)
            {
                _isEditMode = true;
                _productId = product.Id;
                Name = product.Name;
                Sku = product.SKU;
                Category = product.Category;
                Price = product.Price;
                Cost = product.Cost;
                StockQuantity = product.StockQuantity;
                ReorderLevel = product.ReorderLevel;
                UpdateTitle();
            }
            return Task.CompletedTask;
        }

        private void UpdateTitle()
        {
            Title = _isEditMode 
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
                    Id = _isEditMode ? _productId : Guid.NewGuid(),
                    Name = Name,
                    SKU = Sku,
                    Category = Category,
                    Price = Price,
                    Cost = Cost,
                    StockQuantity = StockQuantity,
                    ReorderLevel = ReorderLevel
                };

                Result result;
                if (_isEditMode)
                {
                    result = await _productService.UpdateProductAsync(_facilityContext.CurrentFacilityId, product);
                }
                else
                {
                    result = await _productService.CreateProductAsync(_facilityContext.CurrentFacilityId, product);
                }

                if (result.IsSuccess)
                {
                    // Notify ViewModels to refresh (Dirty Flag)
                    CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new Management.Presentation.Messages.RefreshRequiredMessage<Management.Domain.Models.Product>(_facilityContext.CurrentFacilityId));

                    string message = _isEditMode 
                        ? string.Format(GetTerm("Strings.Shop.ProductUpdatedSuccessfully") ?? "Product '{0}' updated successfully.", Name)
                        : string.Format(GetTerm("Strings.Shop.ProductAddedSuccessfully") ?? "Product '{0}' added successfully.", Name);

                    _toastService.ShowSuccess(message);
                    await _modalNavigationStore.CloseAsync(ModalResult.Success(product));
                }
                else
                {
                    _toastService.ShowError($"{(_isEditMode ? "Failed to update" : "Failed to add")} product: {result.Error}");
                }
            }, _isEditMode ? "Updating product..." : "Adding product...");
        }

        [RelayCommand]
        private async Task CancelAsync()
        {
            await _modalNavigationStore.CloseAsync(ModalResult.Cancel());
        }
    }
}
