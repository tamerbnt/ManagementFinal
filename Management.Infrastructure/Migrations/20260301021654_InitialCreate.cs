using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Management.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "access_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    facility_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    turnstile_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    card_id = table.Column<string>(type: "TEXT", nullable: false),
                    transaction_id = table.Column<string>(type: "TEXT", nullable: false),
                    is_access_granted = table.Column<bool>(type: "INTEGER", nullable: false),
                    access_status = table.Column<int>(type: "INTEGER", nullable: false),
                    failure_reason = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_synced = table.Column<bool>(type: "INTEGER", nullable: false),
                    row_version = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_access_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "appointments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    facility_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    client_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    client_name = table.Column<string>(type: "TEXT", nullable: false),
                    staff_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    staff_name = table.Column<string>(type: "TEXT", nullable: false),
                    service_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    service_name = table.Column<string>(type: "TEXT", nullable: false),
                    start_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    end_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    status = table.Column<int>(type: "INTEGER", nullable: false),
                    price = table.Column<decimal>(type: "TEXT", nullable: false),
                    notes = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_synced = table.Column<bool>(type: "INTEGER", nullable: false),
                    row_version = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_appointments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "facilities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    description = table.Column<string>(type: "TEXT", nullable: false),
                    type = table.Column<int>(type: "INTEGER", nullable: false),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_modified_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    facility_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    row_version = table.Column<byte[]>(type: "BLOB", nullable: false),
                    is_synced = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_facilities", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "facility_zones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    facility_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    capacity = table.Column<int>(type: "INTEGER", nullable: false),
                    type = table.Column<int>(type: "INTEGER", nullable: false),
                    is_operational = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_synced = table.Column<bool>(type: "INTEGER", nullable: false),
                    row_version = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_facility_zones", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "gym_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    facility_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    gym_name = table.Column<string>(type: "TEXT", nullable: false),
                    email = table.Column<string>(type: "TEXT", nullable: false),
                    phone_number = table.Column<string>(type: "TEXT", nullable: false),
                    website = table.Column<string>(type: "TEXT", nullable: false),
                    tax_id = table.Column<string>(type: "TEXT", nullable: false),
                    address = table.Column<string>(type: "TEXT", nullable: false),
                    logo_url = table.Column<string>(type: "TEXT", nullable: false),
                    max_occupancy = table.Column<int>(type: "INTEGER", nullable: false),
                    daily_revenue_target = table.Column<decimal>(type: "TEXT", nullable: false),
                    is_maintenance_mode = table.Column<bool>(type: "INTEGER", nullable: false),
                    operating_hours_json = table.Column<string>(type: "TEXT", nullable: false),
                    is_light_mode = table.Column<bool>(type: "INTEGER", nullable: false),
                    language = table.Column<string>(type: "TEXT", nullable: false),
                    date_format = table.Column<string>(type: "TEXT", nullable: false),
                    high_contrast = table.Column<bool>(type: "INTEGER", nullable: false),
                    reduced_motion = table.Column<bool>(type: "INTEGER", nullable: false),
                    text_scale = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_synced = table.Column<bool>(type: "INTEGER", nullable: false),
                    row_version = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gym_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "integration_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    facility_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    provider_name = table.Column<string>(type: "TEXT", nullable: false),
                    api_key = table.Column<string>(type: "TEXT", nullable: false),
                    api_url = table.Column<string>(type: "TEXT", nullable: false),
                    is_enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_synced = table.Column<bool>(type: "INTEGER", nullable: false),
                    row_version = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_integration_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    facility_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    full_name = table.Column<string>(type: "TEXT", nullable: false),
                    email = table.Column<string>(type: "TEXT", nullable: false),
                    phone_number = table.Column<string>(type: "TEXT", nullable: false),
                    profile_image_url = table.Column<string>(type: "TEXT", nullable: false),
                    segment_data_json = table.Column<string>(type: "TEXT", nullable: false),
                    status = table.Column<int>(type: "INTEGER", nullable: false),
                    start_date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    expiration_date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    membership_plan_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    card_id = table.Column<string>(type: "TEXT", nullable: false),
                    remaining_sessions = table.Column<int>(type: "INTEGER", nullable: false),
                    emergency_contact_name = table.Column<string>(type: "TEXT", nullable: false),
                    emergency_contact_phone = table.Column<string>(type: "TEXT", nullable: true),
                    notes = table.Column<string>(type: "TEXT", nullable: false),
                    gender = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_synced = table.Column<bool>(type: "INTEGER", nullable: false),
                    row_version = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_members", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "membership_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    facility_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    description = table.Column<string>(type: "TEXT", nullable: false),
                    duration_days = table.Column<int>(type: "INTEGER", nullable: false),
                    price_amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    price_currency = table.Column<string>(type: "TEXT", nullable: false),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_session_pack = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_walk_in = table.Column<bool>(type: "INTEGER", nullable: false),
                    gender_rule = table.Column<int>(type: "INTEGER", nullable: false),
                    schedule_json = table.Column<string>(type: "TEXT", nullable: true),
                    price = table.Column<decimal>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_synced = table.Column<bool>(type: "INTEGER", nullable: false),
                    row_version = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_membership_plans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "offline_actions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    facility_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    entity_type = table.Column<string>(type: "TEXT", nullable: false),
                    action_type = table.Column<int>(type: "INTEGER", nullable: false),
                    payload = table.Column<string>(type: "TEXT", nullable: false),
                    retry_count = table.Column<int>(type: "INTEGER", nullable: false),
                    last_error = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_synced = table.Column<bool>(type: "INTEGER", nullable: false),
                    row_version = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_offline_actions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    facility_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    entity_type = table.Column<string>(type: "TEXT", nullable: false),
                    entity_id = table.Column<string>(type: "TEXT", nullable: false),
                    action = table.Column<string>(type: "TEXT", nullable: false),
                    content_json = table.Column<string>(type: "TEXT", nullable: false),
                    created_by = table.Column<Guid>(type: "TEXT", nullable: true),
                    is_processed = table.Column<bool>(type: "INTEGER", nullable: false),
                    processed_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    error_count = table.Column<int>(type: "INTEGER", nullable: false),
                    is_conflict = table.Column<bool>(type: "INTEGER", nullable: false),
                    last_error = table.Column<string>(type: "TEXT", nullable: true),
                    server_payload = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_synced = table.Column<bool>(type: "INTEGER", nullable: false),
                    row_version = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "payroll_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    facility_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    staff_member_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    pay_period_start = table.Column<DateTime>(type: "TEXT", nullable: false),
                    pay_period_end = table.Column<DateTime>(type: "TEXT", nullable: false),
                    amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    amount_currency = table.Column<string>(type: "TEXT", nullable: false),
                    paid_amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    paid_amount_currency = table.Column<string>(type: "TEXT", nullable: false),
                    base_salary = table.Column<decimal>(type: "TEXT", nullable: false),
                    absence_count = table.Column<int>(type: "INTEGER", nullable: false),
                    absence_deduction = table.Column<decimal>(type: "TEXT", nullable: false),
                    is_paid = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_synced = table.Column<bool>(type: "INTEGER", nullable: false),
                    row_version = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payroll_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    facility_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    description = table.Column<string>(type: "TEXT", nullable: false),
                    price_amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    price_currency = table.Column<string>(type: "TEXT", nullable: false),
                    cost_amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    cost_currency = table.Column<string>(type: "TEXT", nullable: false),
                    stock_quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    sku = table.Column<string>(type: "TEXT", nullable: false),
                    category = table.Column<int>(type: "INTEGER", nullable: false),
                    image_url = table.Column<string>(type: "TEXT", nullable: false),
                    reorder_level = table.Column<int>(type: "INTEGER", nullable: false),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    price = table.Column<decimal>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_synced = table.Column<bool>(type: "INTEGER", nullable: false),
                    row_version = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_products", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "registrations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    facility_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    full_name = table.Column<string>(type: "TEXT", nullable: false),
                    email = table.Column<string>(type: "TEXT", nullable: false),
                    phone_number = table.Column<string>(type: "TEXT", nullable: false),
                    source = table.Column<string>(type: "TEXT", nullable: false),
                    status = table.Column<int>(type: "INTEGER", nullable: false),
                    notes = table.Column<string>(type: "TEXT", nullable: false),
                    interest_payload_json = table.Column<string>(type: "TEXT", nullable: false),
                    preferred_plan_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    preferred_start_date = table.Column<DateTime>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_synced = table.Column<bool>(type: "INTEGER", nullable: false),
                    row_version = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_registrations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reservations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    facility_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    member_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    resource_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    resource_type = table.Column<string>(type: "TEXT", nullable: false),
                    service_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    start_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    end_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_synced = table.Column<bool>(type: "INTEGER", nullable: false),
                    row_version = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reservations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "restaurant_menu_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    facility_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    category = table.Column<string>(type: "TEXT", nullable: false),
                    price = table.Column<decimal>(type: "TEXT", nullable: false),
                    image_path = table.Column<string>(type: "TEXT", nullable: false),
                    is_available = table.Column<bool>(type: "INTEGER", nullable: false),
                    ingredients_json = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_synced = table.Column<bool>(type: "INTEGER", nullable: false),
                    row_version = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_restaurant_menu_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "restaurant_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    facility_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    table_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    table_number = table.Column<string>(type: "TEXT", nullable: true),
                    section = table.Column<string>(type: "TEXT", nullable: true),
                    daily_order_number = table.Column<int>(type: "INTEGER", nullable: false),
                    party_size = table.Column<int>(type: "INTEGER", nullable: false),
                    delivered_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    completed_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    status = table.Column<int>(type: "INTEGER", nullable: false),
                    subtotal = table.Column<decimal>(type: "TEXT", nullable: false),
                    tax = table.Column<decimal>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_synced = table.Column<bool>(type: "INTEGER", nullable: false),
                    row_version = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_restaurant_orders", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "restaurant_tables",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    facility_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    table_number = table.Column<int>(type: "INTEGER", nullable: false),
                    label = table.Column<string>(type: "TEXT", nullable: false),
                    section = table.Column<string>(type: "TEXT", nullable: false),
                    max_seats = table.Column<int>(type: "INTEGER", nullable: false),
                    current_occupancy = table.Column<int>(type: "INTEGER", nullable: false),
                    status = table.Column<int>(type: "INTEGER", nullable: false),
                    x = table.Column<double>(type: "REAL", nullable: false),
                    y = table.Column<double>(type: "REAL", nullable: false),
                    width = table.Column<double>(type: "REAL", nullable: false),
                    height = table.Column<double>(type: "REAL", nullable: false),
                    shape = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_synced = table.Column<bool>(type: "INTEGER", nullable: false),
                    row_version = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_restaurant_tables", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    facility_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    member_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    subtotal_amount__amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    subtotal_amount__currency = table.Column<string>(type: "TEXT", nullable: false),
                    tax_amount__amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    tax_amount__currency = table.Column<string>(type: "TEXT", nullable: false),
                    total_amount__amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    total_amount__currency = table.Column<string>(type: "TEXT", nullable: false),
                    payment_method = table.Column<int>(type: "INTEGER", nullable: false),
                    transaction_type = table.Column<string>(type: "TEXT", nullable: false),
                    category = table.Column<int>(type: "INTEGER", nullable: false),
                    captured_label = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_synced = table.Column<bool>(type: "INTEGER", nullable: false),
                    row_version = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "salon_services",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    facility_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    category = table.Column<string>(type: "TEXT", nullable: false),
                    base_price = table.Column<decimal>(type: "TEXT", nullable: false),
                    duration_minutes = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_synced = table.Column<bool>(type: "INTEGER", nullable: false),
                    row_version = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_salon_services", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "staff_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    facility_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    full_name = table.Column<string>(type: "TEXT", nullable: false),
                    email = table.Column<string>(type: "TEXT", nullable: false),
                    phone_number = table.Column<string>(type: "TEXT", nullable: false),
                    role = table.Column<int>(type: "INTEGER", nullable: false),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    hire_date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    salary = table.Column<decimal>(type: "TEXT", nullable: false),
                    payment_day = table.Column<int>(type: "INTEGER", nullable: false),
                    permissions = table.Column<string>(type: "jsonb", nullable: false),
                    supabase_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    allowed_modules = table.Column<string>(type: "jsonb", nullable: false),
                    pin_code = table.Column<string>(type: "TEXT", nullable: true),
                    card_id = table.Column<string>(type: "TEXT", nullable: true),
                    pending_auth_email = table.Column<string>(type: "TEXT", nullable: true),
                    auth_sync_status = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_synced = table.Column<bool>(type: "INTEGER", nullable: false),
                    row_version = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_staff_members", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    facility_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    total_amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    payment_method = table.Column<int>(type: "INTEGER", nullable: false),
                    audit_note = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_synced = table.Column<bool>(type: "INTEGER", nullable: false),
                    row_version = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transactions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "turnstiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    facility_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    location = table.Column<string>(type: "TEXT", nullable: false),
                    hardware_id = table.Column<string>(type: "TEXT", nullable: false),
                    status = table.Column<int>(type: "INTEGER", nullable: false),
                    is_locked = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_synced = table.Column<bool>(type: "INTEGER", nullable: false),
                    row_version = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_turnstiles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "product_usage",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    facility_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    product_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    product_name = table.Column<string>(type: "TEXT", nullable: false),
                    quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    price_per_unit = table.Column<decimal>(type: "TEXT", nullable: false),
                    appointment_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_synced = table.Column<bool>(type: "INTEGER", nullable: false),
                    row_version = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_usage", x => x.id);
                    table.ForeignKey(
                        name: "fk_product_usage_appointments_appointment_id",
                        column: x => x.appointment_id,
                        principalTable: "appointments",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "facility_schedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    facility_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    day_of_week = table.Column<int>(type: "INTEGER", nullable: false),
                    start_time = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    end_time = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    rule_type = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_synced = table.Column<bool>(type: "INTEGER", nullable: false),
                    row_version = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_facility_schedules", x => x.id);
                    table.ForeignKey(
                        name: "fk_facility_schedules_facilities_facility_id",
                        column: x => x.facility_id,
                        principalTable: "facilities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "membership_plan_facilities",
                columns: table => new
                {
                    accessible_facilities_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    accessible_plans_id = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_membership_plan_facilities", x => new { x.accessible_facilities_id, x.accessible_plans_id });
                    table.ForeignKey(
                        name: "fk_membership_plan_facilities__facilities_accessible_facilities_id",
                        column: x => x.accessible_facilities_id,
                        principalTable: "facilities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_membership_plan_facilities__membership_plans_accessible_plans_id",
                        column: x => x.accessible_plans_id,
                        principalTable: "membership_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    facility_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    restaurant_order_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    price = table.Column<decimal>(type: "TEXT", nullable: false),
                    quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_synced = table.Column<bool>(type: "INTEGER", nullable: false),
                    row_version = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_order_items_restaurant_orders_restaurant_order_id",
                        column: x => x.restaurant_order_id,
                        principalTable: "restaurant_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sale_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    sale_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    product_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    product_name_snapshot = table.Column<string>(type: "TEXT", nullable: false),
                    price_snapshot = table.Column<decimal>(type: "TEXT", nullable: false),
                    price_snapshot_currency = table.Column<string>(type: "TEXT", nullable: false),
                    quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_synced = table.Column<bool>(type: "INTEGER", nullable: false),
                    row_version = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sale_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_sale_items_sales_sale_id",
                        column: x => x.sale_id,
                        principalTable: "sales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transaction_line",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    product_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    product_name = table.Column<string>(type: "TEXT", nullable: false),
                    quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    price = table.Column<decimal>(type: "TEXT", nullable: false),
                    tax_rate = table.Column<decimal>(type: "TEXT", nullable: false),
                    transaction_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_synced = table.Column<bool>(type: "INTEGER", nullable: false),
                    row_version = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transaction_line", x => x.id);
                    table.ForeignKey(
                        name: "fk_transaction_line_transactions_transaction_id",
                        column: x => x.transaction_id,
                        principalTable: "transactions",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_access_events_facility_id",
                table: "access_events",
                column: "facility_id");

            migrationBuilder.CreateIndex(
                name: "ix_access_events_is_deleted",
                table: "access_events",
                column: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_access_events_is_synced",
                table: "access_events",
                column: "is_synced");

            migrationBuilder.CreateIndex(
                name: "ix_access_events_tenant_id",
                table: "access_events",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointments_facility_id",
                table: "appointments",
                column: "facility_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointments_is_deleted",
                table: "appointments",
                column: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_appointments_is_synced",
                table: "appointments",
                column: "is_synced");

            migrationBuilder.CreateIndex(
                name: "ix_appointments_tenant_id",
                table: "appointments",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_facilities_is_deleted",
                table: "facilities",
                column: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_facilities_is_synced",
                table: "facilities",
                column: "is_synced");

            migrationBuilder.CreateIndex(
                name: "ix_facilities_tenant_id",
                table: "facilities",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_facility_schedules_facility_id",
                table: "facility_schedules",
                column: "facility_id");

            migrationBuilder.CreateIndex(
                name: "ix_facility_schedules_is_deleted",
                table: "facility_schedules",
                column: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_facility_schedules_is_synced",
                table: "facility_schedules",
                column: "is_synced");

            migrationBuilder.CreateIndex(
                name: "ix_facility_schedules_tenant_id",
                table: "facility_schedules",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_facility_zones_facility_id",
                table: "facility_zones",
                column: "facility_id");

            migrationBuilder.CreateIndex(
                name: "ix_facility_zones_is_deleted",
                table: "facility_zones",
                column: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_facility_zones_is_synced",
                table: "facility_zones",
                column: "is_synced");

            migrationBuilder.CreateIndex(
                name: "ix_facility_zones_tenant_id",
                table: "facility_zones",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_gym_settings_facility_id",
                table: "gym_settings",
                column: "facility_id");

            migrationBuilder.CreateIndex(
                name: "ix_gym_settings_is_deleted",
                table: "gym_settings",
                column: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_gym_settings_is_synced",
                table: "gym_settings",
                column: "is_synced");

            migrationBuilder.CreateIndex(
                name: "ix_gym_settings_tenant_id",
                table: "gym_settings",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_integration_configs_facility_id",
                table: "integration_configs",
                column: "facility_id");

            migrationBuilder.CreateIndex(
                name: "ix_integration_configs_is_deleted",
                table: "integration_configs",
                column: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_integration_configs_is_synced",
                table: "integration_configs",
                column: "is_synced");

            migrationBuilder.CreateIndex(
                name: "ix_integration_configs_tenant_id",
                table: "integration_configs",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "idx_member_card_id",
                table: "members",
                column: "card_id");

            migrationBuilder.CreateIndex(
                name: "idx_member_expiration_date",
                table: "members",
                column: "expiration_date");

            migrationBuilder.CreateIndex(
                name: "idx_member_gender",
                table: "members",
                column: "gender");

            migrationBuilder.CreateIndex(
                name: "idx_member_start_date",
                table: "members",
                column: "start_date");

            migrationBuilder.CreateIndex(
                name: "idx_member_status",
                table: "members",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_members_facility_id",
                table: "members",
                column: "facility_id");

            migrationBuilder.CreateIndex(
                name: "ix_members_is_deleted",
                table: "members",
                column: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_members_is_synced",
                table: "members",
                column: "is_synced");

            migrationBuilder.CreateIndex(
                name: "ix_members_tenant_id",
                table: "members",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_membership_plan_facilities_accessible_plans_id",
                table: "membership_plan_facilities",
                column: "accessible_plans_id");

            migrationBuilder.CreateIndex(
                name: "ix_membership_plans_facility_id",
                table: "membership_plans",
                column: "facility_id");

            migrationBuilder.CreateIndex(
                name: "ix_membership_plans_is_deleted",
                table: "membership_plans",
                column: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_membership_plans_is_synced",
                table: "membership_plans",
                column: "is_synced");

            migrationBuilder.CreateIndex(
                name: "ix_membership_plans_tenant_id",
                table: "membership_plans",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_offline_actions_facility_id",
                table: "offline_actions",
                column: "facility_id");

            migrationBuilder.CreateIndex(
                name: "ix_offline_actions_is_deleted",
                table: "offline_actions",
                column: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_offline_actions_is_synced",
                table: "offline_actions",
                column: "is_synced");

            migrationBuilder.CreateIndex(
                name: "ix_offline_actions_tenant_id",
                table: "offline_actions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_items_facility_id",
                table: "order_items",
                column: "facility_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_items_is_deleted",
                table: "order_items",
                column: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_order_items_is_synced",
                table: "order_items",
                column: "is_synced");

            migrationBuilder.CreateIndex(
                name: "ix_order_items_restaurant_order_id",
                table: "order_items",
                column: "restaurant_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_items_tenant_id",
                table: "order_items",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "idx_outbox_unprocessed_created",
                table: "outbox_messages",
                columns: new[] { "is_processed", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_facility_id",
                table: "outbox_messages",
                column: "facility_id");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_is_deleted",
                table: "outbox_messages",
                column: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_is_synced",
                table: "outbox_messages",
                column: "is_synced");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_tenant_id",
                table: "outbox_messages",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_payroll_entries_facility_id",
                table: "payroll_entries",
                column: "facility_id");

            migrationBuilder.CreateIndex(
                name: "ix_payroll_entries_is_deleted",
                table: "payroll_entries",
                column: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_payroll_entries_is_synced",
                table: "payroll_entries",
                column: "is_synced");

            migrationBuilder.CreateIndex(
                name: "ix_payroll_entries_tenant_id",
                table: "payroll_entries",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_usage_appointment_id",
                table: "product_usage",
                column: "appointment_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_usage_facility_id",
                table: "product_usage",
                column: "facility_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_usage_is_deleted",
                table: "product_usage",
                column: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_product_usage_is_synced",
                table: "product_usage",
                column: "is_synced");

            migrationBuilder.CreateIndex(
                name: "ix_product_usage_tenant_id",
                table: "product_usage",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_products_facility_id",
                table: "products",
                column: "facility_id");

            migrationBuilder.CreateIndex(
                name: "ix_products_is_deleted",
                table: "products",
                column: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_products_is_synced",
                table: "products",
                column: "is_synced");

            migrationBuilder.CreateIndex(
                name: "ix_products_tenant_id",
                table: "products",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_registrations_facility_id",
                table: "registrations",
                column: "facility_id");

            migrationBuilder.CreateIndex(
                name: "ix_registrations_is_deleted",
                table: "registrations",
                column: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_registrations_is_synced",
                table: "registrations",
                column: "is_synced");

            migrationBuilder.CreateIndex(
                name: "ix_registrations_tenant_id",
                table: "registrations",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_reservations_facility_id",
                table: "reservations",
                column: "facility_id");

            migrationBuilder.CreateIndex(
                name: "ix_reservations_is_deleted",
                table: "reservations",
                column: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_reservations_is_synced",
                table: "reservations",
                column: "is_synced");

            migrationBuilder.CreateIndex(
                name: "ix_reservations_tenant_id",
                table: "reservations",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_restaurant_menu_items_facility_id",
                table: "restaurant_menu_items",
                column: "facility_id");

            migrationBuilder.CreateIndex(
                name: "ix_restaurant_menu_items_is_deleted",
                table: "restaurant_menu_items",
                column: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_restaurant_menu_items_is_synced",
                table: "restaurant_menu_items",
                column: "is_synced");

            migrationBuilder.CreateIndex(
                name: "ix_restaurant_menu_items_tenant_id",
                table: "restaurant_menu_items",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_restaurant_orders_facility_id",
                table: "restaurant_orders",
                column: "facility_id");

            migrationBuilder.CreateIndex(
                name: "ix_restaurant_orders_is_deleted",
                table: "restaurant_orders",
                column: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_restaurant_orders_is_synced",
                table: "restaurant_orders",
                column: "is_synced");

            migrationBuilder.CreateIndex(
                name: "ix_restaurant_orders_tenant_id",
                table: "restaurant_orders",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_restaurant_tables_facility_id",
                table: "restaurant_tables",
                column: "facility_id");

            migrationBuilder.CreateIndex(
                name: "ix_restaurant_tables_is_deleted",
                table: "restaurant_tables",
                column: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_restaurant_tables_is_synced",
                table: "restaurant_tables",
                column: "is_synced");

            migrationBuilder.CreateIndex(
                name: "ix_restaurant_tables_tenant_id",
                table: "restaurant_tables",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_sale_items_is_deleted",
                table: "sale_items",
                column: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_sale_items_is_synced",
                table: "sale_items",
                column: "is_synced");

            migrationBuilder.CreateIndex(
                name: "ix_sale_items_sale_id",
                table: "sale_items",
                column: "sale_id");

            migrationBuilder.CreateIndex(
                name: "ix_sale_items_tenant_id",
                table: "sale_items",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_facility_id",
                table: "sales",
                column: "facility_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_is_deleted",
                table: "sales",
                column: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_sales_is_synced",
                table: "sales",
                column: "is_synced");

            migrationBuilder.CreateIndex(
                name: "ix_sales_tenant_id",
                table: "sales",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_salon_services_facility_id",
                table: "salon_services",
                column: "facility_id");

            migrationBuilder.CreateIndex(
                name: "ix_salon_services_is_deleted",
                table: "salon_services",
                column: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_salon_services_is_synced",
                table: "salon_services",
                column: "is_synced");

            migrationBuilder.CreateIndex(
                name: "ix_salon_services_tenant_id",
                table: "salon_services",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_staff_members_facility_id",
                table: "staff_members",
                column: "facility_id");

            migrationBuilder.CreateIndex(
                name: "ix_staff_members_is_deleted",
                table: "staff_members",
                column: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_staff_members_is_synced",
                table: "staff_members",
                column: "is_synced");

            migrationBuilder.CreateIndex(
                name: "ix_staff_members_tenant_id",
                table: "staff_members",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_transaction_line_is_deleted",
                table: "transaction_line",
                column: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_transaction_line_is_synced",
                table: "transaction_line",
                column: "is_synced");

            migrationBuilder.CreateIndex(
                name: "ix_transaction_line_tenant_id",
                table: "transaction_line",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_transaction_line_transaction_id",
                table: "transaction_line",
                column: "transaction_id");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_facility_id",
                table: "transactions",
                column: "facility_id");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_is_deleted",
                table: "transactions",
                column: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_is_synced",
                table: "transactions",
                column: "is_synced");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_tenant_id",
                table: "transactions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_turnstiles_facility_id",
                table: "turnstiles",
                column: "facility_id");

            migrationBuilder.CreateIndex(
                name: "ix_turnstiles_is_deleted",
                table: "turnstiles",
                column: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_turnstiles_is_synced",
                table: "turnstiles",
                column: "is_synced");

            migrationBuilder.CreateIndex(
                name: "ix_turnstiles_tenant_id",
                table: "turnstiles",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "access_events");

            migrationBuilder.DropTable(
                name: "facility_schedules");

            migrationBuilder.DropTable(
                name: "facility_zones");

            migrationBuilder.DropTable(
                name: "gym_settings");

            migrationBuilder.DropTable(
                name: "integration_configs");

            migrationBuilder.DropTable(
                name: "members");

            migrationBuilder.DropTable(
                name: "membership_plan_facilities");

            migrationBuilder.DropTable(
                name: "offline_actions");

            migrationBuilder.DropTable(
                name: "order_items");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "payroll_entries");

            migrationBuilder.DropTable(
                name: "product_usage");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "registrations");

            migrationBuilder.DropTable(
                name: "reservations");

            migrationBuilder.DropTable(
                name: "restaurant_menu_items");

            migrationBuilder.DropTable(
                name: "restaurant_tables");

            migrationBuilder.DropTable(
                name: "sale_items");

            migrationBuilder.DropTable(
                name: "salon_services");

            migrationBuilder.DropTable(
                name: "staff_members");

            migrationBuilder.DropTable(
                name: "transaction_line");

            migrationBuilder.DropTable(
                name: "turnstiles");

            migrationBuilder.DropTable(
                name: "facilities");

            migrationBuilder.DropTable(
                name: "membership_plans");

            migrationBuilder.DropTable(
                name: "restaurant_orders");

            migrationBuilder.DropTable(
                name: "appointments");

            migrationBuilder.DropTable(
                name: "sales");

            migrationBuilder.DropTable(
                name: "transactions");
        }
    }
}
