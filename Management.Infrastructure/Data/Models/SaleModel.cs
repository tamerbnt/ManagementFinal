using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Management.Infrastructure.Data.Models
{
    [Table("sales")]
    public class SaleModel : BaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("tenant_id")]
        public Guid TenantId { get; set; }

        [Column("member_id")]
        public Guid? MemberId { get; set; }

        [Column("total_amount")]
        public decimal TotalAmount { get; set; }

        [Column("timestamp")]
        public DateTime Timestamp { get; set; }

        [Column("payment_method")]
        public int PaymentMethod { get; set; }

        [Column("facility_id")]
        public Guid FacilityId { get; set; }
    }
}
