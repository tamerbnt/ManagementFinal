using System;
using Management.Domain.Enums;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;

namespace Management.Domain.Models
{
    public class Product : AggregateRoot, ITenantEntity, IFacilityEntity
    {
        public Guid TenantId { get; set; }
        public Guid FacilityId { get; set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public Money Price { get; private set; }
        public Money Cost { get; private set; }
        public int StockQuantity { get; private set; }
        public string SKU { get; private set; }
        public ProductCategory Category { get; private set; }
        public string ImageUrl { get; private set; }
        public int ReorderLevel { get; private set; }
        public bool IsActive { get; private set; }

        private Product(
            Guid id, 
            string name, 
            string description, 
            Money price, 
            Money cost, 
            int stockQuantity, 
            string sku, 
            ProductCategory category, 
            string imageUrl, 
            int reorderLevel, 
            bool isActive) : base(id)
        {
            Name = name;
            Description = description ?? string.Empty;
            Price = price;
            Cost = cost;
            StockQuantity = stockQuantity;
            SKU = sku ?? string.Empty;
            Category = category;
            ImageUrl = imageUrl ?? string.Empty;
            ReorderLevel = reorderLevel;
            IsActive = isActive;
        }

        // Additional constructor for EF Core
        private Product() 
        {
            Name = default!;
            Description = default!;
            Price = default!;
            Cost = default!;
            SKU = default!;
            Category = default!; // Assuming value object or enum, if class need default!
            ImageUrl = default!;
        }

        public static Result<Product> Create(
            string name,
            string description,
            Money price,
            Money cost,
            int stockQuantity,
            string sku,
            ProductCategory category,
            string imageUrl,
            int reorderLevel)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Result.Failure<Product>(new Error("Product.EmptyName", "Product name cannot be empty."));

            if (stockQuantity < 0)
                return Result.Failure<Product>(new Error("Product.NegativeStock", "Stock quantity cannot be negative."));

            var product = new Product(
                Guid.NewGuid(),
                name,
                description ?? string.Empty,
                price,
                cost,
                stockQuantity,
                sku ?? string.Empty,
                category,
                imageUrl ?? string.Empty,
                reorderLevel,
                true);

            return Result.Success(product);
        }

        public void UpdateDetails(
            string name, 
            string description, 
            string sku, 
            Money price, 
            Money cost, 
            int reorderLevel, 
            ProductCategory category, 
            string imageUrl)
        {
            if (!string.IsNullOrWhiteSpace(name)) Name = name;
            Description = description ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(sku)) SKU = sku;
            Price = price;
            Cost = cost;
            ReorderLevel = reorderLevel;
            Category = category;
            ImageUrl = imageUrl ?? string.Empty;
            
            UpdateTimestamp();
        }

        public void UpdateStock(int quantityChange, string reason)
        {
            if (StockQuantity + quantityChange < 0)
                throw new InvalidOperationException("Stock quantity cannot be negative.");

            StockQuantity += quantityChange;
            // potential audit log or domain event here: DomainEvents.Raise(new ProductStockAdjusted(Id, quantityChange, reason));
            UpdateTimestamp();
        }

        public Result DecrementStock(int quantity)
        {
            if (quantity < 0) 
                return Result.Failure(new Error("Product.InvalidQuantity", "Decrement quantity cannot be negative."));
            
            if (StockQuantity < quantity)
                return Result.Failure(new Error("Product.InsufficientStock", $"Insufficient stock for {Name}."));

            StockQuantity -= quantity;
            UpdateTimestamp();
            return Result.Success();
        }

        public void Deactivate()
        {
            IsActive = false;
        }

        public void Activate()
        {
            IsActive = true;
        }
    }
}
