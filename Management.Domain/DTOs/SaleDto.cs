using System;

namespace Management.Domain.DTOs
{
    public record SaleDto
    {
        public Guid Id { get; set; }
        public DateTime Timestamp { get; set; }
        public decimal TotalAmount { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public string MemberName { get; set; } = string.Empty;
        public Dictionary<Guid, int> Items { get; set; } = new();
    }
}