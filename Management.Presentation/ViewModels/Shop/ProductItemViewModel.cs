using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Application.DTOs;

namespace Management.Presentation.ViewModels.Shop
{
    public partial class ProductItemViewModel : ObservableObject
    {
        private readonly ShopViewModel _parent;
        private ProductDto _product;

        [ObservableProperty]
        private bool _isActive;

        [ObservableProperty]
        private bool _isSelected;
        [ObservableProperty] private string _name;
        [ObservableProperty] private string _description;
        [ObservableProperty] private decimal _price;
        [ObservableProperty] private decimal _cost;
        
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StockStatusText))]
        [NotifyPropertyChangedFor(nameof(StockLevel))]
        private int _stockQuantity;
        
        [ObservableProperty] private string _sku;
        [ObservableProperty] private string _category;
        [ObservableProperty] private string _imageUrl;
        [ObservableProperty] private int _reorderLevel;

        public Guid Id => _product.Id;

        public IRelayCommand AddToCartCommand { get; }
        public IRelayCommand ViewDetailsCommand { get; }
        public IAsyncRelayCommand ModifyProductCommand { get; }

        public ProductItemViewModel(ProductDto product, ShopViewModel parent)
        {
            _product = product;
            _parent = parent;

            _name = product.Name;
            _description = product.Description;
            _price = product.Price;
            _cost = product.Cost;
            _stockQuantity = product.StockQuantity;
            _sku = product.SKU;
            _category = product.Category;
            _imageUrl = product.ImageUrl;
            _reorderLevel = product.ReorderLevel;

            AddToCartCommand = new AsyncRelayCommand(async () => await parent.AddToCartCommand.ExecuteAsync(CreateCurrentDto()));
            
            ViewDetailsCommand = new RelayCommand(() => {
                parent.SelectedProduct = this;
                parent.IsDetailOpen = true;
            });

            ModifyProductCommand = new AsyncRelayCommand(async () => {
                await parent.OpenEditProductCommand.ExecuteAsync(CreateCurrentDto());
            });
        }

        private ProductDto CreateCurrentDto()
        {
            return new ProductDto
            {
                Id = this.Id,
                Name = this.Name,
                Description = this.Description,
                Price = this.Price,
                Cost = this.Cost,
                StockQuantity = this.StockQuantity,
                SKU = this.Sku,
                Category = this.Category,
                ImageUrl = this.ImageUrl,
                ReorderLevel = this.ReorderLevel
            };
        }

        public void UpdateFromDto(ProductDto updatedDto)
        {
            _product = updatedDto;
            Name = updatedDto.Name;
            Description = updatedDto.Description;
            Price = updatedDto.Price;
            Cost = updatedDto.Cost;
            StockQuantity = updatedDto.StockQuantity;
            Sku = updatedDto.SKU;
            Category = updatedDto.Category;
            ImageUrl = updatedDto.ImageUrl;
            ReorderLevel = updatedDto.ReorderLevel;
        }

        public string StockStatusText => StockQuantity > ReorderLevel ? "In Stock" : (StockQuantity > 0 ? "Low Stock" : "Out of Stock");
        public string StockLevel => StockQuantity > ReorderLevel ? "Good" : (StockQuantity > 0 ? "Warning" : "Critical");
    }
}
