# Stored RPC Functions Reference

All RPC functions run with SECURITY DEFINER and SET search_path = public, pg_temp.

### 1. check_device_registration(p_hardware_id text)
- Returns jsonb containing:
  status: 'registered' | 'unregistered', device_id, account_id, branch_id, business_id.

### 2. check_subscription_status(p_hardware_id text)
- Returns jsonb containing subscription status, plan rank, days remaining (calculated via CEIL), and feature flags (cross_branch_reports, shared_members).

### 3. onboard_owner_account(...)
- Parameters:
  - p_owner_id uuid
  - p_full_name text
  - p_email text
  - p_business_name text
  - p_branch_name text
  - p_category text DEFAULT 'pos_inventory' (C# managed category: pos_inventory, appointment_service, membership_session, project_milestone, rental_booking, education_cohort)
  - p_hardware_id text DEFAULT NULL
  - p_device_label text DEFAULT 'Main PC'
  - p_voucher_code text DEFAULT NULL
  - p_modules text[] DEFAULT NULL
- Returns jsonb containing success: true, account_id, business_id, branch_id, device_id, plan.

### 4. redeem_license_voucher(p_account_id uuid, p_voucher_code text)
- Returns jsonb with success: true, plan, and message. Activates perpetual lifetime access.

### 5. admin_activate_account(p_email, p_tier_rank, p_admin_secret, p_notes)
- Returns jsonb with upgraded account subscription.

### 6. register_device(p_account_id uuid, p_branch_id uuid, p_hardware_id text, p_label text DEFAULT 'New PC')
- Enforces Tier max_devices quota. Returns success: true, device_id or error DEVICE_LIMIT_REACHED.

### 7. revoke_device(p_device_id uuid, p_account_id uuid)
- Returns jsonb with success: true and released slot.

### 8. get_staff_context(p_account_id uuid, p_staff_id uuid)
- Returns jsonb with staff profile, branches array, and permissions array.

### 9. generate_license_vouchers(p_tier_rank integer, p_count integer, p_admin_secret text, p_notes text DEFAULT NULL)
- Returns Cryptographic ATR-<TIER>-LIFE-XXXX-XXXX codes.

### 10. create_business(p_account_id uuid, p_name text, p_category text DEFAULT 'pos_inventory', p_address text DEFAULT NULL, p_phone text DEFAULT NULL)
- Enforces Tier max_businesses quota. Returns success: true, business_id, category.

### 11. create_branch(p_account_id uuid, p_business_id uuid, p_name text, p_address text DEFAULT NULL, p_phone text DEFAULT NULL)
- Enforces Tier max_branches quota. Automatically enables core modules on the new branch.

---

### 12. get_staff_profiles(p_email text)  [PATCH v1.2]
- **Called by**: `AuthenticationService.ResolveStaffProfileAsync` — Cloud Recovery path.
- **Purpose**: Returns every active `public.staff` row matching `p_email`, joined with their primary branch assignment. Used to seed the local SQLite DB on a new or re-installed device.
- Returns `jsonb` array. Each element shape (matches `SupabaseStaffMember` C# model):

| JSON field | Source | Notes |
|---|---|---|
| `id` | `staff.id` | Staff UUID |
| `tenant_id` | `staff.account_id` | Account / tenant UUID |
| `facility_id` | `staff_branch_assignments.branch_id` or main branch | Branch UUID |
| `full_name` | `staff.full_name` | |
| `email` | `staff.email` | |
| `role` | CASE on `staff.role` text → integer | See mapping table in SKILL.md §6 |
| `is_active` | `staff.is_active` | |
| `is_owner` | `staff.role = 'owner'` | boolean shortcut |
| `supabase_user_id` | `staff.auth_user_id` | Supabase Auth UUID |
| `created_at` / `updated_at` | `staff.*` | |

- **GRANT**: `authenticated`, `anon`, `service_role`
- **⚠️ Role integer mapping** (MUST match C# `StaffRole` enum):
  - `'owner'` → **8**, `'manager'` → **1**, `'cashier'` → 2, `'technician'` → 3, `'waiter'` → 4, `'staff'`/other → **7**

---

### 13. get_tenant_facilities(p_tenant_id uuid)  [PATCH v1.2]
- **Called by**: `AuthenticationService.SeedLocalFacilitiesFromSupabaseAsync` — Cloud Recovery path.
- **Purpose**: Returns all active `public.branches` for a tenant, joined with their parent `public.businesses` category. Used to populate the local SQLite `Facilities` table, fixing `SelectedFacility.Id = Guid.Empty` on unconfigured PCs.
- Returns `jsonb` array. Each element shape (matches `SupabaseFacility` C# model):

| JSON field | Source | Notes |
|---|---|---|
| `id` | `branches.id` | Branch UUID → local Facility.Id |
| `tenant_id` | `branches.account_id` | |
| `name` | `branches.name` | |
| `description` | `businesses.name` or branch name | |
| `slug` | LOWER(REPLACE(branch name, ' ', '-')) | URL-safe identifier |
| `type` | CASE on `businesses.category` text → integer | Maps to C# `FacilityType` enum |
| `is_active` | `branches.is_active` | |
| `created_at` | `branches.created_at` | |

- **Category → FacilityType integer mapping**:
  - `'pos_inventory'` → 10, `'appointment_service'`/`'salon'` → 11, `'membership_session'`/`'gym'` → 12, `'restaurant'` → 6, `'project_milestone'` → 13, `'rental_booking'` → 14, `'education_cohort'` → 15, fallback → 12
- **GRANT**: `authenticated`, `anon`, `service_role`

---

### 14. get_staff_for_sync(p_account_id uuid, p_updated_after timestamptz DEFAULT NULL, p_facility_id uuid DEFAULT NULL)  [PATCH v1.3]
- **Called by**: `SyncService.PullStaffMembersAsync` — Synchronizing cloud staff records into local SQLite.
- **Purpose**: Replaces legacy direct table queries on non-existent Phase 1 `staff_members` table. Directly queries Phase 2 `public.staff` joined with `staff_branch_assignments`, returning the JSON shape expected by `SupabaseStaffMember`.
- Returns `jsonb` array of staff objects:

| JSON field | Source | Notes |
|---|---|---|
| `id` | `staff.id` | Staff UUID |
| `tenant_id` | `staff.account_id` | Account UUID |
| `primary_facility_id` | `staff_branch_assignments.branch_id` or main branch | Branch UUID |
| `full_name` | `staff.full_name` | |
| `email` | `staff.email` | |
| `role` | CASE on `staff.role` text → integer | Integer matching C# `StaffRole` enum |
| `is_owner` | `staff.role = 'owner'` | Boolean |
| `is_active` | `staff.is_active` | |
| `auth_user_id` | `staff.auth_user_id` | Supabase Auth UUID |
| `permissions` | `'{}'::jsonb` | Empty object (Phase 2 uses `staff_module_permissions`) |
| `created_at` / `updated_at` | `staff.*` | Timestamp |

- **Filtering**:
  - `p_account_id`: Scopes to tenant.
  - `p_updated_after`: Incremental cutoff timestamp (NULL pulls all active staff).
  - `p_facility_id`: Scopes to a specific branch for non-owners (NULL pulls all branch staff for owners).
- **GRANT**: `authenticated`, `anon`, `service_role`

