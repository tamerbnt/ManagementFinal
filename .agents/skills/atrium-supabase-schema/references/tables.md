# Table Specifications & Schemas

### Catalog & Configuration
- **subscription_plans**: id (uuid PK), name (text UNIQUE), tier_rank (int UNIQUE: 0=Trial, 1=Starter, 2=Pro, 3=Enterprise), max_businesses (int), max_branches (int), max_devices (int), max_staff (int), cross_branch_reports (bool), shared_members (bool), is_free_tier (bool), trial_days (int), price_label (text), created_at.
- **modules**: id (uuid PK), key (text UNIQUE), display_name (text), icon (text), description (text), is_core (bool), sort_order (int).

### Multi-Tenant Core
- **accounts**: id (uuid PK, references auth.users ON DELETE CASCADE), full_name (text), email (text UNIQUE), phone (text), is_active (bool), created_at, updated_at.
- **account_subscriptions**: id (uuid PK), account_id (uuid FK to accounts), plan_id (uuid FK to subscription_plans), status (text), is_lifetime (bool), trial_ends_at (timestamptz), current_period_end (timestamptz), grace_ends_at (timestamptz), activated_by_voucher (uuid FK), created_at, updated_at.
- **businesses**: id (uuid PK), account_id (uuid FK to accounts), category (text NOT NULL DEFAULT 'pos_inventory' — managed in C# codebase), name (text), display_name (text), address (text), phone (text), logo_url (text), is_active (bool), created_at, updated_at.
- **branches**: id (uuid PK), account_id (uuid FK to accounts), business_id (uuid FK to businesses), name (text), code (text), address (text), phone (text), is_main_branch (bool), is_active (bool), created_at, updated_at.
- **devices**: id (uuid PK), account_id (uuid FK to accounts), branch_id (uuid FK to branches), hardware_id (text UNIQUE), label (text), is_active (bool), last_seen_at (timestamptz), registered_at (timestamptz).
- **license_vouchers**: id (uuid PK), code (text UNIQUE), plan_id (uuid FK to subscription_plans), duration_months (int - 0=lifetime), is_redeemed (bool), redeemed_by_account_id (uuid FK to accounts), redeemed_at (timestamptz), created_by_note (text), created_at.
- **account_audit_logs**: id (uuid PK), account_id (uuid FK to accounts), event_type (text), actor_id (uuid), metadata (jsonb), ip_address (text), created_at.

### Observability & Modules
- **branch_modules**: id (uuid PK), branch_id (uuid FK to branches), module_id (uuid FK to modules), is_enabled (bool), enabled_at (timestamptz).
- **branch_sync_logs**: id (uuid PK), branch_id (uuid FK to branches), direction (text: 'push'/'pull'), status (text), records_synced (int), error_message (text), synced_at.
- **device_heartbeats**: id (uuid PK), device_id (uuid FK to devices), client_version (text), os_info (text), ping_at (timestamptz).
- **system_notifications**: id (uuid PK), account_id (uuid FK to accounts), title (text), message (text), type (text: 'warning', 'info', 'alert'), is_read (bool), created_at.

### Staff & Roles
- **staff**: id (uuid PK), account_id (uuid FK to accounts), auth_user_id (uuid references auth.users), full_name (text), email (text), phone (text), role (staff_role_enum: owner, manager, cashier, technician, waiter), pin_hash (text), is_active (bool), created_at, updated_at.
- **staff_branch_assignments**: id (uuid PK), staff_id (uuid FK to staff), branch_id (uuid FK to branches), is_primary (bool), created_at.
- **staff_module_permissions**: id (uuid PK), staff_id (uuid FK to staff), branch_id (uuid FK to branches), module_id (uuid FK to modules), can_view (bool), can_edit (bool), can_delete (bool), granted_at (timestamptz).

### Customers & Admissions
- **members_directory**: id (uuid PK), account_id (uuid FK to accounts), branch_id (uuid FK to branches), full_name (text), email (text), phone (text), rfid_tag (text), barcode (text), is_active (bool), created_at.
- **registrations**: id (uuid PK), account_id (uuid FK to accounts), branch_id (uuid FK to branches), member_id (uuid FK), type (text), amount_paid (numeric), created_at.
- **registration_requests**: id (uuid PK), account_id (uuid FK to accounts), branch_id (uuid FK to branches), payload (jsonb), status (text), requested_at, processed_at.
