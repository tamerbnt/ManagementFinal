using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Management.Application.Services
{
    public class InventoryResourceDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        /// <summary>Unit of measurement: kg, L, piece, pack, box, bottle</summary>
        public string Unit { get; set; } = "kg";
        /// <summary>Sum of all logged purchases for this resource</summary>
        public decimal CumulativeTotal { get; set; }
        public Guid FacilityId { get; set; }
        public Guid TenantId { get; set; }
    }

    public class InventoryPurchaseDto
    {
        public Guid Id { get; set; }
        public Guid ResourceId { get; set; }
        public string ResourceName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal UnitPrice { get; set; }
        public string Unit { get; set; } = "kg";
        public DateTime Date { get; set; }
        public string? Note { get; set; }
        public Guid FacilityId { get; set; }
        public Guid TenantId { get; set; }
    }
}
