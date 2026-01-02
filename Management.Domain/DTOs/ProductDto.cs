using System;
using Management.Domain.Enums;

namespace Management.Domain.DTOs
{
    public record ProductDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal Cost { get; set; }
        public string? Currency { get; set; } = "USD";
        public int StockQuantity { get; set; }
        public string SKU { get; set; } = string.Empty;
        public string Category { get; set; } = "Other";
        public string ImageUrl { get; set; } = string.Empty;
        public int ReorderLevel { get; set; }
    }
}