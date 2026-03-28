using System;
using System.Collections.Generic;
using System.Linq;
using Management.Domain.Enums;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;

namespace Management.Domain.Models
{
    public class Sale : AggregateRoot, ITenantEntity, IFacilityEntity
    {
        public Guid TenantId { get; set; }
        public Guid FacilityId { get; set; }
        public DateTime Timestamp { get; private set; }
        public Guid? MemberId { get; private set; }

        // Totals
        public Money SubtotalAmount { get; private set; } = null!;
        public Money TaxAmount { get; private set; } = null!;
        public Money TotalAmount { get; private set; } = null!;

        public PaymentMethod PaymentMethod { get; private set; }
        public string TransactionType { get; private set; } = string.Empty;
        
        // Senior Refactor: Immutable Attribution
        public SaleCategory Category { get; private set; }
        public string CapturedLabel { get; private set; } = string.Empty;

        private readonly List<SaleItem> _items = new();
        public IReadOnlyCollection<SaleItem> Items => _items.AsReadOnly();

        private Sale(Guid id, Guid? memberId, DateTime timestamp, PaymentMethod paymentMethod, string transactionType, SaleCategory category, string capturedLabel)
            : base(id)
        {
            MemberId = memberId;
            Timestamp = timestamp;
            PaymentMethod = paymentMethod;
            TransactionType = transactionType;
            Category = category;
            CapturedLabel = capturedLabel;
            
            SubtotalAmount = Money.Zero();
            TaxAmount = Money.Zero();
            TotalAmount = Money.Zero();
        }

        private Sale() { }

        public static Result<Sale> Create(Guid? memberId, PaymentMethod paymentMethod, string transactionType, SaleCategory category = SaleCategory.General, string capturedLabel = "")
        {
            return Result.Success(new Sale(Guid.NewGuid(), memberId, DateTime.UtcNow, paymentMethod, transactionType, category, capturedLabel));
        }

        public Result AddLineItem(Product product, int quantity)
        {
             // Pass this.Id as saleId
             var item = SaleItem.Create(this.Id, product.Id, product.Name, product.Price, quantity);
             if (item.IsFailure) return item;

             _items.Add(item.Value);
             RecalculateTotals();
             return Result.Success();
        }

        public void AddItem(SaleItem item)
        {
            _items.Add(item);
            RecalculateTotals();
        }

        public void RecalculateTotals()
        {
            decimal total = _items.Sum(i => i.TotalLinePrice.Amount);
            
            SubtotalAmount = new Money(total, "DA");
            TaxAmount = Money.Zero(); // No tax as requested
            TotalAmount = new Money(total, "DA");
        }
    }
}
