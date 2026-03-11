using CommunityToolkit.Mvvm.ComponentModel;
using Management.Application.DTOs;
using System;

namespace Management.Presentation.ViewModels.PointOfSale
{
    public partial class CartItemViewModel : ObservableObject
    {
        public ProductDto Product { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Total))]
        private int _quantity;

        // Use the product's price from DTO
        public decimal Price => Product.Price;

        public decimal Total => Quantity * Price;

        public CartItemViewModel(ProductDto product, int quantity = 1)
        {
            Product = product;
            Quantity = quantity;
        }

        public void AddQuantity(int amount)
        {
            Quantity += amount;
        }
    }
}
