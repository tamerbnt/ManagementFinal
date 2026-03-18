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
        public Money UnitPriceSnapshot { get; private set; }
        public int Quantity { get; private set; }

        public Money TotalLinePrice => new Money(UnitPriceSnapshot.Amount * Quantity, UnitPriceSnapshot.Currency);

        private SaleItem(Guid id, Guid saleId, Guid productId, string productNameSnapshot, Money unitPriceSnapshot, int quantity)
            : base(id)
        {
            SaleId = saleId;
            ProductId = productId;
            ProductNameSnapshot = productNameSnapshot;
            UnitPriceSnapshot = unitPriceSnapshot;
            Quantity = quantity;
        }

        private SaleItem() { ProductNameSnapshot = string.Empty; UnitPriceSnapshot = null!; }

        public static Result<SaleItem> Create(Guid saleId, Guid productId, string productName, Money unitPrice, int quantity)
        {
            if (quantity <= 0)
                return Result.Failure<SaleItem>(new Error("SaleItem.InvalidQuantity", "Quantity must be greater than zero"));

            // Ensure a fresh Money instance to avoid EF Core tracking conflicts
            var priceSnapshot = new Money(unitPrice.Amount, unitPrice.Currency);
            return Result.Success(new SaleItem(Guid.NewGuid(), saleId, productId, productName, priceSnapshot, quantity));
        }
    }
}
