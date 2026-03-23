using System;
using Management.Application.DTOs;

namespace Management.Application.DTOs
{
    public record SaleDto
    {
        public Guid Id { get; set; }
        public DateTime Timestamp { get; set; }
        public decimal TotalAmount { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public string MemberName { get; set; } = string.Empty;
        public Guid? MemberId { get; set; }
        public Dictionary<Guid, int> Items { get; set; } = new();
        public Dictionary<string, int> ItemsSnapshot { get; set; } = new();
    }
}
