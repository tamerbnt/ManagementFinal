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

        public string? AppliedPromotionName { get; private set; }
        public Guid? ManualDiscountId { get; private set; }
        public Money? ManualDiscountAmount { get; private set; }

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

        public void SetPromotionName(string? name) => AppliedPromotionName = name;
        public void SetManualDiscount(Guid? discountId, Money? amount)
        {
            ManualDiscountId = discountId;
            ManualDiscountAmount = amount;
        }

        public Result AddLineItem(
            Product product, 
            int quantity, 
            Money? unitPrice = null, 
            Money? originalPrice = null, 
            Money? discountAmount = null)
        {
            // Pass the custom unitPrice if provided (Net), else fallback to product.Price (Gross)
            var finalUnitPrice = unitPrice ?? product.Price;
            
            var item = SaleItem.Create(this.Id, product.Id, product.Name, finalUnitPrice, quantity, originalPrice, discountAmount);
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
            // Net Total (What was actually paid before manual transaction-level discount)
            decimal netTotal = _items.Sum(i => i.TotalLinePrice.Amount);
            
            // Apply Manual Transaction-level Discount if present
            if (ManualDiscountAmount != null)
            {
                netTotal -= ManualDiscountAmount.Amount;
            }

            // Gross Total (Before any discounts)
            decimal grossTotal = _items.Sum(i => (i.OriginalPrice?.Amount ?? i.UnitPriceSnapshot.Amount) * i.Quantity);
            
            SubtotalAmount = new Money(grossTotal, "DA");
            TaxAmount = Money.Zero(); 
            TotalAmount = new Money(Math.Max(0, netTotal), "DA");
        }
    }
}
