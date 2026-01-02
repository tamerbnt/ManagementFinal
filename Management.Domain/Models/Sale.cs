using System;
using System.Collections.Generic;
using System.Linq;
using Management.Domain.Enums;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;

namespace Management.Domain.Models
{
    public class Sale : AggregateRoot
    {
        public DateTime Timestamp { get; private set; }
        public Guid? MemberId { get; private set; }

        // Totals
        public Money SubtotalAmount { get; private set; } = null!;
        public Money TaxAmount { get; private set; } = null!;
        public Money TotalAmount { get; private set; } = null!;

        public PaymentMethod PaymentMethod { get; private set; }
        public string TransactionType { get; private set; } = string.Empty;

        private readonly List<SaleItem> _items = new();
        public IReadOnlyCollection<SaleItem> Items => _items.AsReadOnly();

        private Sale(Guid id, Guid? memberId, DateTime timestamp, PaymentMethod paymentMethod, string transactionType)
            : base(id)
        {
            MemberId = memberId;
            Timestamp = timestamp;
            PaymentMethod = paymentMethod;
            TransactionType = transactionType;
            
            SubtotalAmount = Money.Zero();
            TaxAmount = Money.Zero();
            TotalAmount = Money.Zero();
        }

        private Sale() { }

        public static Result<Sale> Create(Guid? memberId, PaymentMethod paymentMethod, string transactionType)
        {
            return Result.Success(new Sale(Guid.NewGuid(), memberId, DateTime.UtcNow, paymentMethod, transactionType));
        }

        public void AddLineItem(Product product, int quantity)
        {
             // Pass this.Id as saleId
             var item = SaleItem.Create(this.Id, product.Id, product.Name, product.Price, quantity);
             if (item.IsSuccess)
             {
                _items.Add(item.Value);
                RecalculateTotals();
             }
        }

        public void AddItem(SaleItem item)
        {
            _items.Add(item);
            RecalculateTotals();
        }

        public void RecalculateTotals()
        {
            decimal subTotal = _items.Sum(i => i.TotalLinePrice.Amount);
            decimal tax = subTotal * 0.1m; // Example 10% tax implementation - should really be a strategy pattern or passed in
            
            SubtotalAmount = new Money(subTotal, "USD");
            TaxAmount = new Money(tax, "USD");
            TotalAmount = new Money(subTotal + tax, "USD");
        }
    }
}