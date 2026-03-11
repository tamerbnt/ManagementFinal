using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

using Newtonsoft.Json.Linq;

namespace Management.Infrastructure.Integrations.Supabase.Models
{
    public abstract class SupabaseBaseModel : BaseModel
    {
        protected static DateTime ToUtc(DateTime value)
        {
            return value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
                : value.ToUniversalTime();
        }
    }


    [Table("profiles")]
    public class SupabaseProfile : SupabaseBaseModel
    {
        [PrimaryKey("id")]
        public Guid UserId { get; set; }

        [Column("tenant_id")]
        public Guid? TenantId { get; set; }

        [Column("permissions")]
        public JToken? PermissionsJson { get; set; }

        [Column("allowed_modules")]
        public JToken? AllowedModulesJson { get; set; }

        [Column("supabase_user_id")]
        public Guid? SupabaseUserId { get; set; }

        [Column("full_name")]
        public string? FullName { get; set; }

        [Column("role")]
        public int? Role { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    [Table("tenants")]
    public class SupabaseTenant : SupabaseBaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        [Column("slug")]
        public string? Slug { get; set; }

        [Column("industry")]
        public string? Industry { get; set; }

        [Column("status")]
        public string Status { get; set; } = "active";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    [Table("facilities")]
    public class SupabaseFacility : SupabaseBaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("tenant_id")]
        public Guid TenantId { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Column("slug")]
        public string? Slug { get; set; }

        [Column("type")]
        public int Type { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    [Table("members")]
    public class SupabaseMember : SupabaseBaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("tenant_id")]
        public Guid TenantId { get; set; }

        [Column("facility_id")]
        public Guid FacilityId { get; set; }

        [Column("full_name")]
        [Newtonsoft.Json.JsonProperty("full_name")]
        public string? FullName { get; set; }

        [Column("email")]
        [Newtonsoft.Json.JsonProperty("email")]
        public string? Email { get; set; }

        [Column("phone_number")]
        public string? Phone { get; set; }

        [Column("status")]
        public int Status { get; set; } = 1; // Default to Active (1)

        [Column("card_id")]
        public string? CardId { get; set; }

        [Column("profile_image_url")]
        public string? ProfileImageUrl { get; set; }

        [Column("start_date")]
        public DateTime StartDate { get; set; }

        [Column("expiration_date")]
        public DateTime? ExpirationDate { get; set; }

        [Column("membership_plan_id")]
        public Guid? MembershipPlanId { get; set; }

        [Column("gender")]
        public int Gender { get; set; }

        [Column("remaining_sessions")]
        public int RemainingSessions { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("emergency_contact_name")]
        public string? EmergencyContactName { get; set; }

        [Column("emergency_contact_phone")]
        public string? EmergencyContactPhone { get; set; }

        [Column("segment_data_json")]
        public string? SegmentDataJson { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    [Table("tenant_devices")]
    public class SupabaseDevice : SupabaseBaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("tenant_id")]
        public Guid TenantId { get; set; }

        [Column("license_id")]
        public Guid? LicenseId { get; set; }

        [Column("hardware_id")]
        public string? HardwareId { get; set; }

        [Column("label")]
        public string? Label { get; set; }

        [Column("registered_at")]
        public DateTime RegisteredAt { get; set; }
    }

    [Table("licenses")]
    public class SupabaseLicense : SupabaseBaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("license_key")]
        public string? LicenseKey { get; set; }

        [Column("tenant_id")]
        public Guid? TenantId { get; set; }

        [Column("max_devices")]
        public int MaxDevices { get; set; }

        [Column("expires_at")]
        public DateTime ExpiresAt { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    [Table("access_events")]
    public class SupabaseAccessEvent : SupabaseBaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("tenant_id")]
        public Guid TenantId { get; set; }

        [Column("facility_id")]
        public Guid FacilityId { get; set; }

        [Column("card_id")]
        public string? MemberId { get; set; }

        [Column("turnstile_id")]
        public Guid? TurnstileId { get; set; }

        [Column("access_status")]
        public int? Status { get; set; }

        [Column("failure_reason")]
        public string? Reason { get; set; }

        [Column("scanned_at")]
        public DateTime ScannedAt { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    [Table("sale_items")]
    public class SupabaseSaleItem : SupabaseBaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("tenant_id")]
        public Guid TenantId { get; set; }

        [Column("sale_id")]
        public Guid SaleId { get; set; }

        [Column("product_id")]
        public Guid? ProductId { get; set; }

        [Column("name_snapshot")]
        public string? NameSnapshot { get; set; }

        [Column("quantity")]
        public int Quantity { get; set; }

        [Column("price_snapshot")]
        public decimal PriceSnapshot { get; set; }

        [Column("tax_amount")]
        public decimal TaxAmount { get; set; }
    }

    [Table("staff_members")]
    public class SupabaseStaffMember : SupabaseBaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("tenant_id")]
        public Guid TenantId { get; set; }

        [Column("facility_id")]
        public Guid FacilityId { get; set; }

        [Column("full_name")]
        [Newtonsoft.Json.JsonProperty("full_name")]
        public string? FullName { get; set; }

        [Column("email")]
        [Newtonsoft.Json.JsonProperty("email")]
        public string? Email { get; set; }

        [Column("role")]
        public int Role { get; set; } = 7; // Default to Staff

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [Column("is_owner")]
        public bool IsOwner { get; set; }

        [Column("phone_number")]
        public string? PhoneNumber { get; set; }

        [Column("salary")]
        public decimal Salary { get; set; }

        [Column("payment_day")]
        public int PaymentDay { get; set; }

        [Column("rfid_tag")]
        public string? CardId { get; set; }

        [Column("permissions")]
        public JToken? PermissionsJson { get; set; }

        [Column("allowed_modules")]
        public JToken? AllowedModulesJson { get; set; }

        [Column("supabase_user_id")]
        public Guid? SupabaseUserId { get; set; }
    }

    [Table("membership_plans")]
    public class SupabaseMembershipPlan : SupabaseBaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("tenant_id")]
        public Guid TenantId { get; set; }

        [Column("facility_id")]
        public Guid FacilityId { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Column("duration_days")]
        public int DurationDays { get; set; }

        [Column("price_amount")]
        public decimal Price { get; set; }

        [Column("price_currency")]
        public string? Currency { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }

        [Column("is_session_pack")]
        public bool IsSessionPack { get; set; }

        [Column("is_walk_in")]
        public bool IsWalkIn { get; set; }

        [Column("gender_rule")]
        public int GenderRule { get; set; }

        [Column("schedule_json")]
        public string? ScheduleJson { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    [Table("facility_schedules")]
    public class SupabaseFacilitySchedule : SupabaseBaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("tenant_id")]
        public Guid TenantId { get; set; }

        [Column("facility_id")]
        public Guid FacilityId { get; set; }

        [Column("day_of_week")]
        public int DayOfWeek { get; set; }

        [Column("start_time")]
        public string? StartTime { get; set; }

        [Column("end_time")]
        public string? EndTime { get; set; }

        [Column("rule_type")]
        public int RuleType { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    [Table("membership_plan_facilities")]
    public class SupabaseMembershipPlanFacility : BaseModel
    {
        [Column("membership_plan_id")]
        public Guid MembershipPlanId { get; set; }

        [Column("facility_id")]
        public Guid FacilityId { get; set; }
    }

    [Table("money")]
    public class SupabaseMoney : BaseModel
    {
        [Column("MembershipPlanId")] 
        public Guid MembershipPlanId { get; set; }

        [Column("amount")]
        public decimal Amount { get; set; }

        [Column("currency")]
        public string? Currency { get; set; }
    }

    [Table("registrations")]
    public class SupabaseRegistration : SupabaseBaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("tenant_id")]
        public Guid TenantId { get; set; }

        [Column("facility_id")]
        public Guid FacilityId { get; set; }

        [Column("full_name")]
        [Newtonsoft.Json.JsonProperty("full_name")]
        public string? FullName { get; set; }

        [Column("email")]
        [Newtonsoft.Json.JsonProperty("email")]
        public string? Email { get; set; }

        [Column("phone_number")]
        public string? PhoneNumber { get; set; }

        [Column("source")]
        public string? Source { get; set; }

        [Column("status")]
        public int Status { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("preferred_plan_id")]
        public Guid? PreferredPlanId { get; set; }

        [Column("preferred_start_date")]
        public DateTime? PreferredStartDate { get; set; }

        [Column("interest_payload_json")]
        public string? InterestPayloadJson { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    [Table("gym_settings")]
    public class SupabaseGymSettings : SupabaseBaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("tenant_id")]
        public Guid TenantId { get; set; }

        [Column("facility_id")]
        public Guid FacilityId { get; set; }

        [Column("gym_name")]
        public string? GymName { get; set; }

        [Column("address")]
        public string? Address { get; set; }

        [Column("phone_number")] 
        public string? Phone { get; set; }

        [Column("email")]
        public string? Email { get; set; }

        [Column("operating_hours_json")]
        public string? OperatingHoursJson { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    [Table("turnstiles")]
    public class SupabaseTurnstile : SupabaseBaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("tenant_id")]
        public Guid TenantId { get; set; }

        [Column("facility_id")]
        public Guid FacilityId { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        [Column("ip_address")]
        public string? IpAddress { get; set; }

        [Column("port")]
        public int Port { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }


    [Table("appointments")]
    public class SupabaseAppointment : SupabaseBaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("tenant_id")]
        public Guid TenantId { get; set; }

        [Column("facility_id")]
        public Guid FacilityId { get; set; }

        [Column("client_id")]
        public Guid ClientId { get; set; }

        [Column("client_name")]
        public string? ClientName { get; set; }

        [Column("staff_id")]
        public Guid StaffId { get; set; }

        [Column("staff_name")]
        public string? StaffName { get; set; }

        [Column("service_id")]
        public Guid ServiceId { get; set; }

        [Column("service_name")]
        public string? ServiceName { get; set; }

        [Column("start_time")]
        public DateTime StartTime { get; set; }

        [Column("end_time")]
        public DateTime EndTime { get; set; }

        [Column("status")]
        public int Status { get; set; }

        [Column("price")]
        public decimal Price { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    [Table("restaurant_menu_items")]
    public class SupabaseRestaurantMenuItem : SupabaseBaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("tenant_id")]
        public Guid TenantId { get; set; }

        [Column("facility_id")]
        public Guid FacilityId { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        [Column("category")]
        public string? Category { get; set; }

        [Column("price")]
        public decimal Price { get; set; }

        [Column("image_path")]
        public string? ImagePath { get; set; }

        [Column("is_available")]
        public bool IsAvailable { get; set; }

        [Column("ingredients_json")]
        public string? IngredientsJson { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    [Table("restaurant_orders")]
    public class SupabaseRestaurantOrder : SupabaseBaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("tenant_id")]
        public Guid TenantId { get; set; }

        [Column("facility_id")]
        public Guid FacilityId { get; set; }

        [Column("table_number")]
        public string? TableNumber { get; set; }

        [Column("total_amount")]
        public decimal TotalAmount { get; set; }

        [Column("status")]
        public int Status { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }
}
