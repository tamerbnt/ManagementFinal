using System;

namespace Management.Domain.Models
{
    public class SaleItem : Entity
    {
        public Guid SaleId { get; set; }
        public Guid ProductId { get; set; }

        // Snapshot Data (Price changes over time, historical records must not change)
        public string ProductNameSnapshot { get; set; }
        public decimal UnitPriceSnapshot { get; set; }

        public int Quantity { get; set; }
        public decimal TotalLinePrice => UnitPriceSnapshot * Quantity;
    }
}