using System;
using Management.Domain.Enums;

namespace Management.Domain.Models
{
    public class Product : Entity
    {
        public string Name { get; set; }
        public string SKU { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }

        // Classification
        public ProductCategory Category { get; set; }

        // Financials
        public decimal Price { get; set; } // Selling Price
        public decimal Cost { get; set; }  // Purchasing Cost

        // Inventory
        public int StockQuantity { get; set; }
        public int ReorderLevel { get; set; }
        public bool IsActive { get; set; }
    }
}