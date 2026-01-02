using System;
using Management.Domain.Models;
using Management.Domain.ValueObjects;
using Management.Domain.Enums;
using Xunit;

namespace Management.Tests.Unit.Domain
{
    public class ProductTests
    {
        [Fact]
        public void Create_WithValidData_ShouldSucceed()
        {
            // Arrange
            var price = new Money(19.99m, "USD");
            var cost = new Money(10.00m, "USD");

            // Act
            var result = Management.Domain.Models.Product.Create(
                name: "Protein Powder",
                description: "High quality whey protein",
                price: price,
                cost: cost,
                stockQuantity: 50,
                sku: "PROT-001",
                category: ProductCategory.Supplements,
                imageUrl: "https://example.com/protein.jpg",
                reorderLevel: 10
            );

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("Protein Powder", result.Value.Name);
            Assert.Equal(50, result.Value.StockQuantity);
        }

        [Fact]
        public void UpdateStock_WithPositiveValue_ShouldIncreaseStock()
        {
            // Arrange
            var price = new Money(10m, "USD");
            var cost = new Money(5m, "USD");
            var product = Management.Domain.Models.Product.Create("Test", "", price, cost, 10, "SKU", ProductCategory.Equipment, "", 5).Value;

            // Act
            product.UpdateStock(5, "Received order");

            // Assert
            Assert.Equal(15, product.StockQuantity);
        }

        [Fact]
        public void UpdateStock_WithNegativeValue_ShouldDecreaseStock()
        {
            // Arrange
            var price = new Money(10m, "USD");
            var cost = new Money(5m, "USD");
            var product = Management.Domain.Models.Product.Create("Test", "", price, cost, 10, "SKU", ProductCategory.Equipment, "", 5).Value;

            // Act
            product.UpdateStock(-3, "Sale");

            // Assert
            Assert.Equal(7, product.StockQuantity);
        }
    }
}
