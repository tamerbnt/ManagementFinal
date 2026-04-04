using System;
using Management.Domain.Models.Enums;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;

namespace Management.Domain.Models
{
    public class InventoryTransaction : Entity, ITenantEntity, IFacilityEntity
    {
        public Guid TenantId { get; set; }
        public Guid FacilityId { get; set; }
        public Guid ProductId { get; private set; }
        public InventoryTransactionType TransactionType { get; private set; }
        public int QuantityChange { get; private set; }
        public int ResultingStock { get; private set; }
        public Money UnitCost { get; private set; }
        public Money TotalCost { get; private set; }
        public Money? NewSalePrice { get; private set; }
        public string Notes { get; private set; }
        public DateTime Timestamp { get; private set; }

        private InventoryTransaction(
            Guid id,
            Guid tenantId,
            Guid facilityId,
            Guid productId,
            InventoryTransactionType type,
            int quantityChange,
            int resultingStock,
            Money unitCost,
            Money totalCost,
            Money? newSalePrice,
            string notes) : base(id)
        {
            TenantId = tenantId;
            FacilityId = facilityId;
            ProductId = productId;
            TransactionType = type;
            QuantityChange = quantityChange;
            ResultingStock = resultingStock;
            UnitCost = unitCost;
            TotalCost = totalCost;
            NewSalePrice = newSalePrice;
            Notes = notes ?? string.Empty;
            Timestamp = DateTime.UtcNow;
        }

        private InventoryTransaction() { } // EF Core

        public static Result<InventoryTransaction> Create(
            Guid tenantId,
            Guid facilityId,
            Guid productId,
            InventoryTransactionType type,
            int quantityChange,
            int resultingStock,
            Money unitCost,
            Money? newSalePrice,
            string notes)
        {
            if (productId == Guid.Empty)
                return Result.Failure<InventoryTransaction>(new Error("InventoryTransaction.InvalidProduct", "Product ID is required."));

            var totalCost = new Money(quantityChange * unitCost.Amount, unitCost.Currency);

            var transaction = new InventoryTransaction(
                Guid.NewGuid(),
                tenantId,
                facilityId,
                productId,
                type,
                quantityChange,
                resultingStock,
                unitCost,
                totalCost,
                newSalePrice,
                notes);

            return Result.Success(transaction);
        }
    }
}
