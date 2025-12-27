using System;

namespace Management.Domain.DTOs
{
    public class InventoryDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; }
        public string SKU { get; set; }
        public string ImageUrl { get; set; }

        public int CurrentStock { get; set; }
        public int ReorderPoint { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}