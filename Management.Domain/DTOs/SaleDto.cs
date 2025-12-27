using System;
using Management.Domain.Enums;

namespace Management.Domain.DTOs
{
    public class SaleDto
    {
        public Guid Id { get; set; }
        public DateTime Timestamp { get; set; }
        public decimal TotalAmount { get; set; }

        public string TransactionType { get; set; } // "Product Purchase", "Membership"
        public string PaymentMethod { get; set; } // "Visa", "Cash"

        public string MemberName { get; set; } // Optional
    }
}