using System;
using Management.Domain.Enums;

namespace Management.Domain.DTOs
{
    public class ProductDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string SKU { get; set; }
        public decimal Price { get; set; }
        public decimal Cost { get; set; }
        public int StockQuantity { get; set; }

        // FIX: Added missing ReorderLevel property
        public int ReorderLevel { get; set; }

        public string ImageUrl { get; set; }
        public ProductCategory Category { get; set; }
        public string Description { get; set; }
    }
}