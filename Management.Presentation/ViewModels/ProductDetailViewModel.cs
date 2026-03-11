using System;
using Management.Application.Services;
using System.Collections.Generic;
using Management.Application.Services;
using System.Linq;
using Management.Application.Services;
using System.Threading;
using Management.Application.Services;
using System.Threading.Tasks;
using Management.Application.Services;
using System.Windows.Input;
using Management.Application.Services;
using Management.Application.Stores;
using Management.Application.Services;
using Management.Application.DTOs;
using Management.Application.Services;
using Management.Domain.Enums;
using Management.Application.Services;
using Management.Domain.Services;
using Management.Application.Services;
using Management.Presentation.Extensions;
using Management.Application.Services;
using Management.Presentation.Services;
using Management.Application.Services;

namespace Management.Presentation.ViewModels
{
    public class ProductDetailViewModel : ViewModelBase, IModalViewModel, IInitializable<object?>
    {
        private readonly IProductService _productService;
        private readonly ProductStore _productStore;
        private readonly IModalNavigationService _modalService;
        private readonly IDialogService _dialogService;
        private readonly INotificationService _notificationService;
        private readonly Management.Domain.Services.IFacilityContextService _facilityContext;
        private readonly ITerminologyService _terminologyService;

        public ModalSize PreferredSize => ModalSize.Medium;

        public Task<bool> CanCloseAsync() => Task.FromResult(true);

        // --- STATE ---
        private Guid? _productId;
        public bool IsEditMode => _productId.HasValue;
        public string Title => IsEditMode ? "Edit Product" : "Add Product";

        private string _name = string.Empty;
        public string Name { get => _name; set => SetProperty(ref _name, value); }
 
        private string _sku = string.Empty;
        public string SKU { get => _sku; set => SetProperty(ref _sku, value); }
 
        private decimal _price;
        public decimal Price { get => _price; set => SetProperty(ref _price, value); }
 
        private decimal _cost;
        public decimal Cost { get => _cost; set => SetProperty(ref _cost, value); }
 
        private int _stockLevel;
        public int StockLevel { get => _stockLevel; set => SetProperty(ref _stockLevel, value); }
 
        private int _reorderLevel;
        public int ReorderLevel { get => _reorderLevel; set => SetProperty(ref _reorderLevel, value); }
 
        private string _description = string.Empty;
        public string Description { get => _description; set => SetProperty(ref _description, value); }
 
        private ProductCategory _selectedCategory;
        public ProductCategory SelectedCategory { get => _selectedCategory; set => SetProperty(ref _selectedCategory, value); }
 
        private string _productImage = string.Empty;
        public string ProductImage { get => _productImage; set => SetProperty(ref _productImage, value); }

        public IEnumerable<ProductCategory> Categories => Enum.GetValues(typeof(ProductCategory)).Cast<ProductCategory>();

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        // --- COMMANDS ---
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand BrowseImageCommand { get; }
        public ICommand RemoveImageCommand { get; }

        public ProductDetailViewModel(
            IProductService productService,
            ProductStore productStore,
            IModalNavigationService modalService,
            IDialogService dialogService,
            INotificationService notificationService,
            Management.Domain.Services.IFacilityContextService facilityContext,
            ITerminologyService terminologyService)
        {
            _productService = productService;
            _productStore = productStore;
            _modalService = modalService;
            _dialogService = dialogService;
            _notificationService = notificationService;
            _facilityContext = facilityContext;
            _terminologyService = terminologyService;

            SaveCommand = new AsyncRelayCommand(ExecuteSaveAsync);
            CancelCommand = new RelayCommand(async () => await _modalService.CloseCurrentModalAsync());
            BrowseImageCommand = new AsyncRelayCommand(ExecuteBrowseImageAsync);
            RemoveImageCommand = new RelayCommand(() => ProductImage = string.Empty);
        }

        public async Task InitializeAsync(object? parameter, CancellationToken cancellationToken = default)
        {
            IsBusy = true;
            try
            {
                if (parameter is Guid id)
                {
                    _productId = id;
                    var facilityId = _facilityContext.CurrentFacilityId;
                    var result = await _productService.GetProductAsync(facilityId, id);
                    if (result.IsSuccess)
                    {
                        var dto = result.Value;
                        Name = dto.Name;
                        SKU = dto.SKU;
                        Price = dto.Price;
                        Cost = dto.Cost;
                        StockLevel = dto.StockQuantity;
                        ReorderLevel = dto.ReorderLevel;
                        Description = dto.Description;
                        ProductImage = dto.ImageUrl;
                        if (Enum.TryParse<ProductCategory>(dto.Category, true, out var cat))
                        {
                            SelectedCategory = cat;
                        }
                    }
                }
                else
                {
                    _productId = null;
                    SelectedCategory = ProductCategory.Supplements;
                    StockLevel = 0;
                    ReorderLevel = 10;
                }

                OnPropertyChanged(nameof(IsEditMode));
                OnPropertyChanged(nameof(Title));
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExecuteSaveAsync()
        {
            if (string.IsNullOrWhiteSpace(Name) || Price < 0)
            {
                _notificationService.ShowError(_terminologyService.GetTerm("Strings.Global.Pleasecheckyourinputs") ?? "Please check your inputs.");
                return;
            }

            var dto = new ProductDto
            {
                Id = _productId ?? Guid.Empty,
                Name = Name,
                SKU = SKU,
                Price = Price,
                Cost = Cost,
                StockQuantity = StockLevel,
                ReorderLevel = ReorderLevel,
                Category = SelectedCategory.ToString(),
                Description = Description,
                ImageUrl = ProductImage
            };

            IsBusy = true;
            try
            {
                var facilityId = _facilityContext.CurrentFacilityId;
                var result = IsEditMode 
                    ? await _productService.UpdateProductAsync(facilityId, dto) 
                    : await _productService.CreateProductAsync(facilityId, dto);

                if (result.IsSuccess)
                {
                    _notificationService.ShowSuccess($"Product {(IsEditMode ? "updated" : "created")} successfully.");
                    await _modalService.CloseCurrentModalAsync();
                }
                else
                {
                    _notificationService.ShowError(result.Error.Message);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExecuteBrowseImageAsync()
        {
            var path = await _dialogService.ShowOpenFileDialogAsync(_terminologyService.GetTerm("Strings.Global.Imagesjpgpngwebp") ?? "Images (*.jpg, *.png, *.webp)|*.jpg;*.png;*.webp");
            if (!string.IsNullOrEmpty(path))
            {
                ProductImage = path;
            }
        }
    }
}