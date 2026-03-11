using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Application.DTOs;

namespace Management.Presentation.ViewModels.Shop
{
    public partial class ProductItemViewModel : ObservableObject
    {
        private readonly ShopViewModel _parent;
        private readonly ProductDto _product;

        [ObservableProperty]
        private bool _isActive;

        [ObservableProperty]
        private bool _isSelected;
        [ObservableProperty] private string _name;
        [ObservableProperty] private string _description;
        [ObservableProperty] private decimal _price;
        [ObservableProperty] private int _stockQuantity;
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
            _stockQuantity = product.StockQuantity;
            _sku = product.SKU;
            _category = product.Category;
            _imageUrl = product.ImageUrl;
            _reorderLevel = product.ReorderLevel;

            AddToCartCommand = new AsyncRelayCommand(async () => await parent.AddToCartCommand.ExecuteAsync(product));
            
            ViewDetailsCommand = new RelayCommand(() => {
                parent.SelectedProduct = this;
                parent.IsDetailOpen = true;
            });

            ModifyProductCommand = new AsyncRelayCommand(async () => {
                await parent.OpenEditProductCommand.ExecuteAsync(product);
            });
        }

        public string StockStatusText => StockQuantity > ReorderLevel ? "In Stock" : (StockQuantity > 0 ? "Low Stock" : "Out of Stock");
        public string StockLevel => StockQuantity > ReorderLevel ? "Good" : (StockQuantity > 0 ? "Warning" : "Critical");
    }
}
