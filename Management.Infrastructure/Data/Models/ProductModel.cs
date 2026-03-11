using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Management.Infrastructure.Data.Models
{
    [Table("products")]
    public class ProductModel : BaseModel
    {
        [PrimaryKey("id", false)]
        [Column("id")] // Explicitly map ID to ensure schema cache picks it up
        public Guid Id { get; set; }

        [Column("tenant_id")]
        public Guid TenantId { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("sku")]
        public string SKU { get; set; } = string.Empty;

        [Column("price_amount")]
        public decimal Price { get; set; }

        [Column("stock_quantity")]
        public int StockQuantity { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("facility_id")]
        public Guid FacilityId { get; set; }

        [Column("cost_amount")]
        public decimal Cost { get; set; }
    }
}
