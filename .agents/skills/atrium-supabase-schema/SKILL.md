---
name: atrium-supabase-schema
description: >-
  Comprehensive reference and operational runbook for the Atrium Supabase PostgreSQL database schema (Phase 2).
  Use this skill whenever inspecting, designing, querying, modifying, migrating, or troubleshooting Supabase tables,
  stored RPC functions, Row-Level Security (RLS) policies, multi-tenant boundaries, or client-side database integrations in Atrium.
---

# Atrium Supabase Database Schema (Phase 2)

This skill provides the architectural specification, entity-relationship model, RPC definitions, and security policies for Atrium's Supabase backend.

---

## 1. Architecture: Cloud Tenancy & C# Managed Business Archetypes

Atrium is a multi-tenant POS and business management platform designed for the cash & lifetime licensing market.
- **Supabase Cloud Role**: Handles identity ("accounts"), subscriptions ("account_subscriptions"), lifetime cash vouchers ("license_vouchers"), branches ("branches"), device authorization ("devices"), security isolation (RLS), and data sync.
- **C# Desktop Application Role**: Differentiates and governs the business categories/archetypes (UI layouts, view models, icons, terminology, and active modules) directly inside the C# codebase.
- In the database, "public.businesses" simply stores "category text NOT NULL DEFAULT 'pos_inventory'", decoupling the cloud database from client-side UI vertical configurations.

---

## 2. Table Directory (17 Tables)

| Part | Table Name | Purpose & Scope |
|---|---|---|
| **3A** | subscription_plans | Catalog of plans (Free Trial, Starter, Pro, Enterprise) and quota limits |
| **3B** | modules | Catalog of system modules (members, pos, classes, inventory, payroll, etc.) |
| **4** | accounts | Core tenant accounts tied 1:1 to auth.users; tracks lifetime / trial status |
| **4** | account_subscriptions | Active subscription records, billing cycles, trial dates, and voucher links |
| **4** | businesses | Company/brand entities owned by an account (stores category text) |
| **4** | branches | Physical locations / stores under a business (replaces legacy facilities) |
| **4** | devices | Authorized hardware POS terminals bound to an account and branch |
| **4** | license_vouchers | Prepaid / lifetime license codes (ATR-START-LIFE-XXXX-XXXX) |
| **4** | account_audit_logs | Security and administrative audit trail |
| **5** | subscription_history | Historical log of tier upgrades, renewals, and voucher redemptions |
| **5** | branch_sync_logs | Synchronization checkpoints for offline-capable desktop POS instances |
| **5** | device_heartbeats | Real-time telemetry, last seen timestamps, and client app versions |
| **5** | system_notifications | Tenant alerts, expiry warnings, and quota notifications |
| **6A** | staff | Team members, PIN hashes, assigned role, and optional auth link |
| **6B** | staff_branch_assignments | Junction table assigning staff members to specific branch locations |
| **6C** | branch_modules | Active functional modules toggled per branch |
| **6D** | staff_module_permissions | Granular permission overrides per staff member and branch |
| **7A** | members_directory | Customer / member directory per account and branch |
| **7B** | registrations | Operational transaction registrations / admissions |
| **7C** | registration_requests | Self-service onboarding requests pending review |

Detailed table definitions: [references/tables.md](./references/tables.md)

---

## 3. Stored RPC Functions (14 Routines)

All business transactions, quota checks, and device handshakes are encapsulated in SECURITY DEFINER functions:

1. check_device_registration(p_hardware_id text) - Device bootstrap handshake. Returns status, device_id, account_id, branch_id, business_id.
2. check_subscription_status(p_hardware_id text) - Verifies license validity, plan tier, and days remaining.
3. onboard_owner_account(...) - Atomic tenant bootstrap: creates account, business, branch, registers device, sets up trial or redeems lifetime voucher.
4. redeem_license_voucher(p_account_id uuid, p_voucher_code text) - Redeems prepaid license code for instant lifetime activation.
5. admin_activate_account(p_email, p_tier_rank, p_admin_secret, p_notes) - Superadmin manual tier upgrade.
6. register_device(p_account_id, p_branch_id, p_hardware_id, p_label) - Enforces plan max_devices quota.
7. revoke_device(p_device_id, p_account_id) - Deactivates device.
8. get_staff_context(p_account_id, p_staff_id) - Loads staff roles, permissions, and assigned branches.
9. generate_license_vouchers(p_tier_rank, p_count, p_admin_secret, p_notes) - Generates cryptographic lifetime voucher keys.
10. create_business(p_account_id, p_name, p_category, p_address, p_phone) - Enforces max_businesses quota.
11. create_branch(p_account_id, p_business_id, p_name, p_address, p_phone) - Enforces max_branches quota.
12. get_staff_profiles(p_email text) - **[PATCH v1.2]** Cloud Recovery RPC. Returns a jsonb array of all staff rows matching p_email (active only). Used by AuthenticationService.ResolveStaffProfileAsync to seed local SQLite on new devices. Role integers match C# StaffRole enum (Owner=8, Manager=1, Cashier=2, Technician=3, Waiter=4, Staff=7).
13. get_tenant_facilities(p_tenant_id uuid) - **[PATCH v1.2]** Returns a jsonb array of active branches joined with their business category, mapped to active C# FacilityType integers (Membership/Gym=1, Appointment/Salon=5, POS/Restaurant=6). Used by AuthenticationService.SeedLocalFacilitiesFromSupabaseAsync to populate local SQLite Facilities table on cloud recovery.
14. get_staff_for_sync(p_account_id uuid, p_updated_after timestamptz, p_facility_id uuid) - **[PATCH v1.3]** Synchronization RPC. Replaces legacy direct queries on non-existent Phase 1 staff_members table. Pulls staff records directly from public.staff and staff_branch_assignments with proper integer role mapping and facility scopes.

Detailed RPC specifications: [references/rpc.md](./references/rpc.md)

---

## 4. Security & RLS Rules

1. **Helper Function get_auth_account_id()**:
   - Implemented in LANGUAGE plpgsql STABLE SECURITY DEFINER.
   - Resolves tenant UUID from either public.accounts (for owners) or public.staff (for staff members).
2. **Standard Tenant RLS Filter**:
   USING (account_id = get_auth_account_id())
   WITH CHECK (account_id = get_auth_account_id());
3. **Branch-Level RLS Filter**:
   USING (branch_id IN (SELECT id FROM public.branches WHERE account_id = get_auth_account_id()));
4. **Public Catalogs**:
   subscription_plans and modules allow public SELECT (USING (true)).

Detailed RLS policies: [references/rls.md](./references/rls.md)

---

## 5. Migration & Modification Protocol

When making future adjustments to this schema:
1. **Never Drop Existing Tables**: Use ALTER TABLE ... ADD COLUMN IF NOT EXISTS to maintain existing customer data.
2. **PostgreSQL Default Order (Error 42P13)**: In function signatures, once a parameter has a DEFAULT, all following parameters MUST also have defaults. Keep required parameters at the beginning.
3. **Business Categories Live in C#**: The database does not need rigid catalog tables for UI verticals. Add or customize categories directly in C#.
4. **Preserve RLS**: If creating a new multi-tenant table, immediately execute ALTER TABLE ... ENABLE ROW LEVEL SECURITY; and add policies referencing get_auth_account_id().
5. **Full Schema Reference**: The canonical Phase 2 DDL is located at [references/schema.sql](./references/schema.sql).

---

## 6. Critical: StaffRole Integer ↔ Supabase Text Mapping

The `public.staff.role` column stores text (`'owner'`, `'manager'`, `'staff'`, etc.).
The C# `StaffRole` enum uses integer values. The RPC `get_staff_profiles` bridges this by returning integers.
**This mapping MUST stay in sync between the SQL CASE statement and the C# enum.**

| Supabase text (`staff.role`) | RPC integer returned | C# `StaffRole` enum value |
|---|---|---|
| `'owner'`      | 8  | `StaffRole.Owner = 8`      |
| `'manager'`    | 1  | `StaffRole.Manager = 1`    |
| `'cashier'`    | 2  | `StaffRole.Cashier = 2`    |
| `'technician'` | 3  | `StaffRole.Technician = 3` |
| `'waiter'`     | 4  | `StaffRole.Waiter = 4`     |
| `'staff'` / any other | 7 | `StaffRole.Staff = 7`  |

> **⚠️ WARNING**: If you add a new role text value in Supabase, you MUST update the CASE statement in `get_staff_profiles` AND add the corresponding integer to `StaffRole` in C#, AND update `MapSupabaseToDomain` in `AuthenticationService.cs`.

## 7. Known Bug History

| Date | Bug | Fix |
|---|---|---|
| 2026-09-15 | `get_staff_profiles` returned `role=10` for owners; C# had no mapping for 10, so owners were silently downgraded to `StaffRole.Staff(7)`, triggering "PC not configured" error | Fixed CASE statement to return 8 for 'owner'; added defensive `remote.Role == 10` guard in `MapSupabaseToDomain` |
| 2026-09-15 | `get_tenant_facilities` did not exist; `SeedLocalFacilitiesFromSupabaseAsync` threw PGRST202, leaving local Facilities table empty; SelectedFacility.Id = Guid.Empty | Created the RPC joining `branches` + `businesses` |
| 2026-09-15 | `SyncService.PullStaffMembersAsync` targeted non-existent `staff_members` table (PGRST205) and selecting `s.permissions` failed (42703) | Created `get_staff_for_sync` RPC (Option B), removed `[Column("permissions")]` on C# `SupabaseStaffMember`, and set `'permissions'` to `'{}'::jsonb` in the RPC |
| 2026-09-16 | `SyncService.PullRegistrationsAsync` queried `registrations` table with Phase 1 columns (`tenant_id`, `facility_id`...) causing 42703 | In Phase 2, `registrations` is operational admissions and lead capture is local-only (web requests in `registration_requests`). Removed pull & marked `Registration` as local-only in SyncService |


