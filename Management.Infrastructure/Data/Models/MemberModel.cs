using System;
using Management.Domain.Primitives;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Management.Infrastructure.Data.Models
{
    /// <summary>
    /// Supabase-specific model for Member table.
    /// This inherits from BaseModel to work with Supabase Postgrest client.
    /// Maps to/from domain Member entity in repositories.
    /// </summary>
    [Table("members")]
    public class MemberModel : BaseModel, ITenantEntity
    {
        [PrimaryKey("id")]
        public Guid Id { get; set; }

        [Column("full_name")]
        public string FullName { get; set; } = string.Empty;

        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Column("phone_number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Column("card_id")]
        public string CardId { get; set; } = string.Empty;

        [Column("profile_image_url")]
        public string ProfileImageUrl { get; set; } = string.Empty;

        [Column("status")]
        public string Status { get; set; } = string.Empty;

        [Column("start_date")]
        public DateTime StartDate { get; set; }

        [Column("expiration_date")]
        public DateTime ExpirationDate { get; set; }

        [Column("membership_plan_id")]
        public Guid? MembershipPlanId { get; set; }

        [Column("emergency_contact_name")]
        public string EmergencyContactName { get; set; } = string.Empty;

        [Column("emergency_contact_phone")]
        public string? EmergencyContactPhone { get; set; }

        [Column("segment_data_json")]
        public string SegmentDataJson { get; set; } = "{}";

        [Column("notes")]
        public string Notes { get; set; } = string.Empty;

        [Column("tenant_id")]
        public Guid TenantId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [Column("is_deleted")]
        public bool IsDeleted { get; set; }
    }
}
