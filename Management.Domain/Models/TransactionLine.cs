using System;
using Management.Domain.Primitives;

namespace Management.Domain.Models
{
    public class TransactionLine : Entity, ITenantEntity
    {
        public Guid TenantId { get; set; }
        public Guid ProductId { get; private set; }
        public string ProductName { get; private set; } = string.Empty;
        public int Quantity { get; private set; }
        public decimal Price { get; private set; }
        public decimal TaxRate { get; private set; }
        public decimal Total => Math.Round(Quantity * Price * (1 + TaxRate), 2, MidpointRounding.AwayFromZero);

        private TransactionLine(Guid id, Guid productId, string productName, int quantity, decimal price, decimal taxRate) : base(id)
        {
            ProductId = productId;
            ProductName = productName;
            Quantity = quantity;
            Price = price;
            TaxRate = taxRate;
        }

        private TransactionLine() { }

        public static Result<TransactionLine> Create(Guid productId, string productName, int quantity, decimal price, decimal taxRate)
        {
            if (quantity <= 0) return Result.Failure<TransactionLine>(new Error("TransactionLine.InvalidQuantity", "Quantity must be greater than zero."));
            if (price < 0) return Result.Failure<TransactionLine>(new Error("TransactionLine.InvalidPrice", "Price cannot be negative."));

            return Result.Success(new TransactionLine(Guid.NewGuid(), productId, productName, quantity, price, taxRate));
        }

        public void AddQuantity(int quantity)
        {
            if (quantity <= 0) return;
            Quantity += quantity;
        }
    }
}
