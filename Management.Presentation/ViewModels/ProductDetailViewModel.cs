using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Management.Application.Services;
using Management.Application.Stores;
using Management.Domain.DTOs;
using Management.Domain.Enums;
using Management.Domain.Services;
using Management.Presentation.Extensions;
using Management.Presentation.Services;
using Management.Presentation.ViewModels;

namespace Management.Presentation.ViewModels
{
    public class ProductDetailViewModel : ViewModelBase, INavigationAware
    {
        private readonly IProductService _productService;
        private readonly ProductStore _productStore;
        private readonly ModalNavigationStore _modalStore;
        private readonly IDialogService _dialogService;
        private readonly INotificationService _notificationService;

        // --- STATE ---
        private Guid? _productId;
        public bool IsEditMode => _productId.HasValue;
        public string Title => IsEditMode ? "Edit Product" : "Add Product";

        // Form Fields
        public string Name { get; set; }
        public string SKU { get; set; }
        public decimal Price { get; set; }
        public decimal Cost { get; set; }
        public int StockLevel { get; set; }
        public int ReorderLevel { get; set; }
        public string Description { get; set; }
        public ProductCategory SelectedCategory { get; set; }

        private string _productImage;
        public string ProductImage { get => _productImage; set => SetProperty(ref _productImage, value); }

        // Dropdown Source
        public IEnumerable<ProductCategory> Categories => Enum.GetValues(typeof(ProductCategory)).Cast<ProductCategory>();

        // --- COMMANDS ---
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand BrowseImageCommand { get; }
        public ICommand RemoveImageCommand { get; }

        public ProductDetailViewModel(
            IProductService productService,
            ProductStore productStore,
            ModalNavigationStore modalStore,
            IDialogService dialogService,
            INotificationService notificationService)
        {
            _productService = productService;
            _productStore = productStore;
            _modalStore = modalStore;
            _dialogService = dialogService;
            _notificationService = notificationService;

            SaveCommand = new AsyncRelayCommand(ExecuteSaveAsync);
            CancelCommand = new RelayCommand(() => _modalStore.Close());
            BrowseImageCommand = new AsyncRelayCommand(ExecuteBrowseImageAsync);
            RemoveImageCommand = new RelayCommand(() => ProductImage = null);
        }

        public async Task OnNavigatedToAsync(object parameter, CancellationToken cancellationToken = default)
        {
            if (parameter is Guid id)
            {
                // Edit Mode
                _productId = id;
                // Fetch data... (Assuming we have a GetById, or we search)
                // For V1, we might rely on the caller passing the DTO, but usually ID is safer.
                // Mock load for structure:
                // var dto = await _productService.GetProductAsync(id);
                // MapDtoToFields(dto);
                OnPropertyChanged(nameof(Title));
            }
            else
            {
                // Create Mode: Defaults
                SelectedCategory = ProductCategory.Supplements;
                StockLevel = 0;
                ReorderLevel = 10;
                OnPropertyChanged(nameof(Title));
            }
        }

        private async Task ExecuteSaveAsync()
        {
            // Basic Validation
            if (string.IsNullOrWhiteSpace(Name) || Price < 0)
            {
                _notificationService.ShowError("Please check your inputs.");
                return;
            }

            var dto = new ProductDto
            {
                Id = _productId ?? Guid.NewGuid(),
                Name = Name,
                SKU = SKU,
                Price = Price,
                Cost = Cost,
                StockQuantity = StockLevel,
                ReorderLevel = ReorderLevel,
                Category = SelectedCategory,
                Description = Description,
                ImageUrl = ProductImage
            };

            try
            {
                if (IsEditMode)
                {
                    await _productService.UpdateProductAsync(dto);
                    // Store triggers UI update in ShopView automatically
                }
                else
                {
                    await _productService.CreateProductAsync(dto);
                }

                _modalStore.Close();
                _notificationService.ShowSuccess($"Product {(IsEditMode ? "updated" : "created")} successfully.");
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error saving product: {ex.Message}");
            }
        }

        private async Task ExecuteBrowseImageAsync()
        {
            var path = await _dialogService.ShowOpenFileDialogAsync("Images|*.jpg;*.png;*.webp");
            if (!string.IsNullOrEmpty(path))
            {
                ProductImage = path;
            }
        }
    }
}