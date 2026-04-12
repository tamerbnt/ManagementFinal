using System;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;

namespace Management.Domain.Models
{
    public class SaleItem : Entity, ITenantEntity
    {
        public Guid TenantId { get; set; }
        public Guid SaleId { get; private set; }
        public Guid ProductId { get; private set; }
        public string ProductNameSnapshot { get; private set; }
        
        // Storing as Money
        public Money UnitPriceSnapshot { get; private set; } // The final price charged
        public Money? OriginalPrice { get; private set; }   // The price before promotion
        public Money? DiscountAmount { get; private set; }  // The amount saved per unit

        public int Quantity { get; private set; }

        public Money TotalLinePrice => new Money(UnitPriceSnapshot.Amount * Quantity, UnitPriceSnapshot.Currency);
        public Money TotalDiscountAmount => new Money((DiscountAmount?.Amount ?? 0) * Quantity, UnitPriceSnapshot.Currency);

        private SaleItem(
            Guid id, 
            Guid saleId, 
            Guid productId, 
            string productNameSnapshot, 
            Money unitPriceSnapshot, 
            int quantity,
            Money? originalPrice = null,
            Money? discountAmount = null)
            : base(id)
        {
            SaleId = saleId;
            ProductId = productId;
            ProductNameSnapshot = productNameSnapshot;
            UnitPriceSnapshot = unitPriceSnapshot;
            Quantity = quantity;
            OriginalPrice = originalPrice;
            DiscountAmount = discountAmount;
        }

        private SaleItem() { ProductNameSnapshot = string.Empty; UnitPriceSnapshot = null!; }

        public static Result<SaleItem> Create(
            Guid saleId, 
            Guid productId, 
            string productName, 
            Money unitPrice, 
            int quantity,
            Money? originalPrice = null,
            Money? discountAmount = null)
        {
            if (quantity <= 0)
                return Result.Failure<SaleItem>(new Error("SaleItem.InvalidQuantity", "Quantity must be greater than zero"));

            if (unitPrice == null)
                return Result.Failure<SaleItem>(new Error("SaleItem.InvalidPrice", "Product price is missing. Try restarting the app if this persists."));

            // Ensure a fresh Money instance to avoid EF Core tracking conflicts
            var priceSnapshot = new Money(unitPrice.Amount, unitPrice.Currency);
            var originalPriceSnapshot = originalPrice != null ? new Money(originalPrice.Amount, originalPrice.Currency) : null;
            var discountAmountSnapshot = discountAmount != null ? new Money(discountAmount.Amount, discountAmount.Currency) : null;

            return Result.Success(new SaleItem(
                Guid.NewGuid(), 
                saleId, 
                productId, 
                productName, 
                priceSnapshot, 
                quantity, 
                originalPriceSnapshot, 
                discountAmountSnapshot));
        }
    }
}
