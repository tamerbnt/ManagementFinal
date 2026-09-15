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

    // Phase 2: targets public.staff (renamed from staff_members in Phase 1)
    // PUSH (outbox upsert): [Column] attributes drive REST payload to public.staff
    // PULL (RPC): get_staff_for_sync RPC JSON deserialized via [JsonProperty]
    [Table("staff")]
    public class SupabaseStaffMember : SupabaseBaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        // Phase 2: column is account_id; JsonProperty maps RPC field "tenant_id"
        [Column("account_id")]
        [Newtonsoft.Json.JsonProperty("tenant_id")]
        public Guid TenantId { get; set; }

        // facility_id NOT a column on public.staff — populated from RPC JSON only
        [Newtonsoft.Json.JsonIgnore]
        [Newtonsoft.Json.JsonProperty("primary_facility_id")]
        public Guid FacilityId { get; set; }

        [Column("full_name")]
        [Newtonsoft.Json.JsonProperty("full_name")]
        public string? FullName { get; set; }

        [Column("email")]
        [Newtonsoft.Json.JsonProperty("email")]
        public string? Email { get; set; }

        // Pull: RPC returns integer; Push: RoleText converts back to Supabase text
        [Newtonsoft.Json.JsonIgnore]
        [Newtonsoft.Json.JsonProperty("role")]
        public int Role { get; set; } = 7;

        [Column("role")]
        [Newtonsoft.Json.JsonIgnore]
        public string RoleText => Role switch { 8 => "owner", 1 => "manager", 2 => "cashier", 3 => "technician", 4 => "waiter", _ => "staff" };

        [Column("is_active")]
        [Newtonsoft.Json.JsonProperty("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        [Newtonsoft.Json.JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        [Newtonsoft.Json.JsonProperty("updated_at")]
        public DateTime UpdatedAt { get; set; }

        // Derived from role — not a DB column; from RPC JSON only
        [Newtonsoft.Json.JsonIgnore]
        [Newtonsoft.Json.JsonProperty("is_owner")]
        public bool IsOwner { get; set; }

        // Not in public.staff (Phase 2) — local SQLite only
        [Newtonsoft.Json.JsonIgnore] public string? PhoneNumber { get; set; }
        [Newtonsoft.Json.JsonIgnore] public decimal Salary { get; set; }
        [Newtonsoft.Json.JsonIgnore] public int PaymentDay { get; set; }
        [Newtonsoft.Json.JsonIgnore] public string? CardId { get; set; }
        [Newtonsoft.Json.JsonIgnore] public JToken? AllowedModulesJson { get; set; }

        // Not in public.staff (Phase 2) — from RPC JSON only
        [Newtonsoft.Json.JsonProperty("permissions")]
        public JToken? PermissionsJson { get; set; }

        // Phase 2: auth_user_id (was supabase_user_id in Phase 1)
        [Column("auth_user_id")]
        [Newtonsoft.Json.JsonProperty("auth_user_id")]
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

        [Column("sessions_per_week")]
        public int SessionsPerWeek { get; set; }

        [Column("is_walk_in")]
        public bool IsWalkIn { get; set; }

        [Column("is_personal_training")]
        public bool IsPersonalTraining { get; set; }

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

    [Table("registration_requests")]
    public class SupabaseRegistrationRequest : SupabaseBaseModel
    {
        [PrimaryKey("id", false)]
        public string Id { get; set; } = string.Empty;

        [Column("facility_slug")]
        public string FacilitySlug { get; set; } = string.Empty;

        [Column("gender")]
        public string Gender { get; set; } = string.Empty;

        [Column("full_name")]
        [Newtonsoft.Json.JsonProperty("full_name")]
        public string FullName { get; set; } = string.Empty;

        [Column("email")]
        [Newtonsoft.Json.JsonProperty("email")]
        public string Email { get; set; } = string.Empty;

        [Column("phone_number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Column("desired_plan")]
        public string DesiredPlan { get; set; } = string.Empty;

        [Column("status")]
        public string Status { get; set; } = "pending";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
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

    [Table("dashboard_snapshots")]
    public class SupabaseDashboardSnapshot : SupabaseBaseModel
    {
        [PrimaryKey("facility_id")]
        public Guid FacilityId { get; set; }

        [Column("id")]
        public Guid Id { get; set; }

        [Column("tenant_id")]
        public Guid TenantId { get; set; }

        [Column("snapshot_data")]
        public JToken? SnapshotData { get; set; }

        [Column("last_updated_at")]
        public DateTime LastUpdatedAt { get; set; }
    }

    [Table("daily_history_summaries")]
    public class SupabaseDailyHistorySummary : SupabaseBaseModel
    {
        [PrimaryKey("facility_id")]
        public Guid FacilityId { get; set; }

        [PrimaryKey("summary_date")]
        public DateTime SummaryDate { get; set; }

        [Column("id")]
        public Guid Id { get; set; }

        [Column("tenant_id")]
        public Guid TenantId { get; set; }

        [Column("total_revenue")]
        public decimal TotalRevenue { get; set; }

        [Column("check_in_count")]
        public int CheckInCount { get; set; }

        [Column("sales_count")]
        public int SalesCount { get; set; }

        [Column("events_json")]
        public string? EventsJson { get; set; }

        [Column("last_updated_at")]
        public DateTime LastUpdatedAt { get; set; }
    }

    // =============================================================================
    // PHASE 2 SUPABASE POSTGREST MODELS (MAPPED TO POSTGRESQL SCHEMA)
    // =============================================================================

    [Table("subscription_plans")]
    public class SupabaseSubscriptionPlan : SupabaseBaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("tier_rank")]
        public int TierRank { get; set; }

        [Column("max_businesses")]
        public int MaxBusinesses { get; set; } = 1;

        [Column("max_branches")]
        public int MaxBranches { get; set; } = 1;

        [Column("max_devices")]
        public int MaxDevices { get; set; } = 1;

        [Column("max_staff")]
        public int MaxStaff { get; set; } = 2;

        [Column("cross_branch_reports")]
        public bool CrossBranchReports { get; set; }

        [Column("shared_members")]
        public bool SharedMembers { get; set; }

        [Column("is_free_tier")]
        public bool IsFreeTier { get; set; }

        [Column("trial_days")]
        public int TrialDays { get; set; }

        [Column("price_label")]
        public string PriceLabel { get; set; } = "One-Time Cash";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    [Table("modules")]
    public class SupabaseModule : SupabaseBaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("key")]
        public string Key { get; set; } = string.Empty;

        [Column("display_name")]
        public string DisplayName { get; set; } = string.Empty;

        [Column("icon")]
        public string Icon { get; set; } = "ðŸ“¦";

        [Column("description")]
        public string? Description { get; set; }

        [Column("is_core")]
        public bool IsCore { get; set; }

        [Column("sort_order")]
        public int SortOrder { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    [Table("accounts")]
    public class SupabaseAccount : SupabaseBaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("full_name")]
        public string FullName { get; set; } = string.Empty;

        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Column("phone_number")]
        public string? PhoneNumber { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    [Table("account_subscriptions")]
    public class SupabaseAccountSubscription : SupabaseBaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("account_id")]
        public Guid AccountId { get; set; }

        [Column("plan_id")]
        public Guid PlanId { get; set; }

        [Column("status")]
        public string Status { get; set; } = "trialing";

        [Column("is_lifetime")]
        public bool IsLifetime { get; set; }

        [Column("trial_ends_at")]
        public DateTime? TrialEndsAt { get; set; }

        [Column("current_period_end")]
        public DateTime? CurrentPeriodEnd { get; set; }

        [Column("grace_ends_at")]
        public DateTime? GraceEndsAt { get; set; }

        [Column("activated_by_voucher")]
        public Guid? ActivatedByVoucher { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    [Table("businesses")]
    public class SupabaseBusiness : SupabaseBaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("account_id")]
        public Guid AccountId { get; set; }

        [Column("category")]
        public string Category { get; set; } = "pos_inventory";

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("display_name")]
        public string? DisplayName { get; set; }

        [Column("address")]
        public string? Address { get; set; }

        [Column("phone")]
        public string? Phone { get; set; }

        [Column("logo_url")]
        public string? LogoUrl { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    [Table("branches")]
    public class SupabaseBranch : SupabaseBaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("account_id")]
        public Guid AccountId { get; set; }

        [Column("business_id")]
        public Guid BusinessId { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("address")]
        public string? Address { get; set; }

        [Column("phone")]
        public string? Phone { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("is_main_branch")]
        public bool IsMainBranch { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    [Table("devices")]
    public class SupabaseDeviceRecord : SupabaseBaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("account_id")]
        public Guid AccountId { get; set; }

        [Column("branch_id")]
        public Guid? BranchId { get; set; }

        [Column("hardware_id")]
        public string HardwareId { get; set; } = string.Empty;

        [Column("label")]
        public string Label { get; set; } = "Main PC";

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("last_seen_at")]
        public DateTime? LastSeenAt { get; set; }

        [Column("registered_at")]
        public DateTime RegisteredAt { get; set; }
    }

    [Table("license_vouchers")]
    public class SupabaseLicenseVoucher : SupabaseBaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("code")]
        public string Code { get; set; } = string.Empty;

        [Column("plan_id")]
        public Guid PlanId { get; set; }

        [Column("duration_months")]
        public int DurationMonths { get; set; } = -1; // -1 = lifetime

        [Column("is_redeemed")]
        public bool IsRedeemed { get; set; }

        [Column("redeemed_by")]
        public Guid? RedeemedBy { get; set; }

        [Column("redeemed_at")]
        public DateTime? RedeemedAt { get; set; }

        [Column("created_by_note")]
        public string? CreatedByNote { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    [Table("staff")]
    public class SupabaseStaff : SupabaseBaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("account_id")]
        public Guid AccountId { get; set; }

        [Column("auth_user_id")]
        public Guid? AuthUserId { get; set; }

        [Column("full_name")]
        public string FullName { get; set; } = string.Empty;

        [Column("email")]
        public string? Email { get; set; }

        [Column("pin")]
        public string? Pin { get; set; }

        [Column("role")]
        public string Role { get; set; } = "staff";

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    [Table("staff_branch_assignments")]
    public class SupabaseStaffBranchAssignment : SupabaseBaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("staff_id")]
        public Guid StaffId { get; set; }

        [Column("branch_id")]
        public Guid BranchId { get; set; }

        [Column("assigned_at")]
        public DateTime AssignedAt { get; set; }
    }

    [Table("branch_modules")]
    public class SupabaseBranchModule : SupabaseBaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("branch_id")]
        public Guid BranchId { get; set; }

        [Column("module_id")]
        public Guid ModuleId { get; set; }

        [Column("is_enabled")]
        public bool IsEnabled { get; set; } = true;

        [Column("enabled_at")]
        public DateTime EnabledAt { get; set; }
    }

    [Table("staff_module_permissions")]
    public class SupabaseStaffModulePermission : SupabaseBaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("staff_id")]
        public Guid StaffId { get; set; }

        [Column("branch_id")]
        public Guid BranchId { get; set; }

        [Column("module_id")]
        public Guid ModuleId { get; set; }

        [Column("can_view")]
        public bool CanView { get; set; }

        [Column("can_edit")]
        public bool CanEdit { get; set; }

        [Column("can_delete")]
        public bool CanDelete { get; set; }

        [Column("can_export")]
        public bool CanExport { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    [Table("members_directory")]
    public class SupabaseMemberDirectory : SupabaseBaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("account_id")]
        public Guid AccountId { get; set; }

        [Column("branch_id")]
        public Guid BranchId { get; set; }

        [Column("local_member_id")]
        public string LocalMemberId { get; set; } = string.Empty;

        [Column("full_name")]
        public string FullName { get; set; } = string.Empty;

        [Column("rfid_tag")]
        public string? RfidTag { get; set; }

        [Column("barcode")]
        public string? Barcode { get; set; }

        [Column("phone")]
        public string? Phone { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("synced_at")]
        public DateTime SyncedAt { get; set; }
    }

}
