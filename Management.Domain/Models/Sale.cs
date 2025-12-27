using System;
using System.Collections.Generic;
using Management.Domain.Enums;

namespace Management.Domain.Models
{
    public class Sale : Entity
    {
        public DateTime Timestamp { get; set; }
        public Guid? MemberId { get; set; } // Nullable (Walk-in customer)

        // Totals
        public decimal SubtotalAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }

        public PaymentMethod PaymentMethod { get; set; }
        public string TransactionType { get; set; } // "Purchase", "Renewal"

        // Navigation Property
        public List<SaleItem> Items { get; set; } = new List<SaleItem>();
    }
}