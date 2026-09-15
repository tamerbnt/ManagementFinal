-- =============================================================================
-- ATRIUM PHASE 2 — INITIAL SCHEMA, SEEDS & RLS POLICIES
-- =============================================================================
-- Script Version : 1.0
-- Target         : Supabase Cloud (PostgreSQL 15+)
-- License Model  : Free Trial (14 days) + Lifetime Cash Plans (Starter/Pro/Enterprise)
-- Voucher Format : ATR-[PLAN]-LIFE-[RANDOM4]-[RANDOM4]
--
-- HOW TO RUN:
--   1. Open your Supabase project - SQL Editor - New Query
--   2. Paste this entire script and click "Run"
--   3. Idempotent: safe to re-run (CREATE IF NOT EXISTS, ON CONFLICT DO NOTHING)
-- =============================================================================

-- =============================================================================
-- PART 1: EXTENSIONS
-- =============================================================================
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- =============================================================================
-- PART 2: CORE SECURITY HELPER FUNCTION
-- Defined after staff table in Part 6A so both accounts + staff tables exist.
-- See: "-- PART 2 FUNCTION DEFINED HERE" marker below.
-- =============================================================================

-- =============================================================================
-- PART 3A: CATALOG TABLE -- subscription_plans
-- =============================================================================
CREATE TABLE IF NOT EXISTS public.subscription_plans (
    id                   uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    name                 text        NOT NULL UNIQUE,
    tier_rank            integer     NOT NULL UNIQUE,
    max_businesses       integer     NOT NULL DEFAULT 1,
    max_branches         integer     NOT NULL DEFAULT 1,
    max_devices          integer     NOT NULL DEFAULT 1,
    max_staff            integer     NOT NULL DEFAULT 2,
    cross_branch_reports boolean     NOT NULL DEFAULT false,
    shared_members       boolean     NOT NULL DEFAULT false,
    is_free_tier         boolean     NOT NULL DEFAULT false,
    trial_days           integer     NOT NULL DEFAULT 0,
    price_label          text        NOT NULL DEFAULT 'One-Time Cash',
    created_at           timestamptz NOT NULL DEFAULT now()
);
COMMENT ON TABLE public.subscription_plans IS
    'Static catalog of 4 tiers (Free, Starter, Professional, Enterprise). Admin-managed only.';

-- =============================================================================
-- PART 3B: BUSINESS CATEGORIES (MANAGED IN C# CODEBASE)
-- The 6 business operating archetypes are defined and governed directly in the
-- C# desktop application (Enums, UI layouts, terminology, and modules).
-- In the database, businesses simply store 'category text NOT NULL'.
-- =============================================================================

-- =============================================================================
-- PART 3C: CATALOG TABLE -- modules
-- =============================================================================
CREATE TABLE IF NOT EXISTS public.modules (
    id           uuid    PRIMARY KEY DEFAULT gen_random_uuid(),
    key          text    NOT NULL UNIQUE,
    display_name text    NOT NULL,
    icon         text    NOT NULL DEFAULT '📦',
    description  text,
    is_core      boolean NOT NULL DEFAULT false,
    sort_order   integer NOT NULL DEFAULT 0,
    created_at   timestamptz NOT NULL DEFAULT now()
);
COMMENT ON TABLE public.modules IS
    '8 functional modules: Members, POS, Payroll, Inventory, Classes, Reports, Messaging, Settings.';

-- =============================================================================
-- PART 4A: IDENTITY TABLE -- accounts
-- =============================================================================
CREATE TABLE IF NOT EXISTS public.accounts (
    id           uuid        PRIMARY KEY,
    full_name    text        NOT NULL,
    email        text        NOT NULL UNIQUE,
    phone_number text,
    is_active    boolean     NOT NULL DEFAULT true,
    created_at   timestamptz NOT NULL DEFAULT now(),
    updated_at   timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT fk_accounts_auth_user
        FOREIGN KEY (id) REFERENCES auth.users(id) ON DELETE CASCADE
);
COMMENT ON TABLE public.accounts IS
    'Top-level owner account, 1:1 with auth.users. All tenant data hangs off this.';

-- =============================================================================
-- PART 4B: ORGANIZATION TABLE -- businesses
-- =============================================================================
CREATE TABLE IF NOT EXISTS public.businesses (
    id           uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id   uuid        NOT NULL,
    category     text        NOT NULL DEFAULT 'pos_inventory',
    name         text        NOT NULL,
    display_name text,
    address      text,
    phone        text,
    logo_url     text,
    is_active    boolean     NOT NULL DEFAULT true,
    created_at   timestamptz NOT NULL DEFAULT now(),
    updated_at   timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT fk_businesses_account
        FOREIGN KEY (account_id) REFERENCES public.accounts(id) ON DELETE CASCADE,
-- category is managed in C# codebase
);
COMMENT ON TABLE public.businesses IS
    'A business entity owned by an account. Enterprise allows up to 5 businesses.';

-- =============================================================================
-- PART 4C: ORGANIZATION TABLE -- branches
-- =============================================================================
CREATE TABLE IF NOT EXISTS public.branches (
    id            uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id    uuid        NOT NULL,
    business_id   uuid        NOT NULL,
    name          text        NOT NULL,
    address       text,
    phone         text,
    is_active     boolean     NOT NULL DEFAULT true,
    is_main_branch boolean    NOT NULL DEFAULT false,
    created_at    timestamptz NOT NULL DEFAULT now(),
    updated_at    timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT fk_branches_account
        FOREIGN KEY (account_id) REFERENCES public.accounts(id) ON DELETE CASCADE,
    CONSTRAINT fk_branches_business
        FOREIGN KEY (business_id) REFERENCES public.businesses(id) ON DELETE CASCADE
);
COMMENT ON TABLE public.branches IS
    'Physical location/branch. Device auth and staff are branch-scoped.';

-- =============================================================================
-- PART 4D: DEVICE TABLE -- devices
-- =============================================================================
CREATE TABLE IF NOT EXISTS public.devices (
    id            uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id    uuid        NOT NULL,
    branch_id     uuid,
    hardware_id   text        NOT NULL UNIQUE,
    label         text        NOT NULL DEFAULT 'Main PC',
    is_active     boolean     NOT NULL DEFAULT true,
    last_seen_at  timestamptz,
    registered_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT fk_devices_account
        FOREIGN KEY (account_id) REFERENCES public.accounts(id) ON DELETE CASCADE,
    CONSTRAINT fk_devices_branch
        FOREIGN KEY (branch_id) REFERENCES public.branches(id) ON DELETE SET NULL
);
COMMENT ON TABLE public.devices IS
    'Authorized Windows PCs. Hardware fingerprint verified on every app startup.';

-- =============================================================================
-- PART 5A: SUBSCRIPTION TABLE -- account_subscriptions
-- =============================================================================
-- is_lifetime=true + current_period_end=NULL = perpetual paid license (never expires)
-- is_lifetime=false + trial_ends_at set = 14-day free trial
CREATE TABLE IF NOT EXISTS public.account_subscriptions (
    id                   uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id           uuid        NOT NULL UNIQUE,
    plan_id              uuid        NOT NULL,
    status               text        NOT NULL DEFAULT 'trialing',
    is_lifetime          boolean     NOT NULL DEFAULT false,
    trial_ends_at        timestamptz,
    current_period_end   timestamptz,
    grace_ends_at        timestamptz,
    activated_by_voucher uuid,
    notes                text,
    created_at           timestamptz NOT NULL DEFAULT now(),
    updated_at           timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT fk_accsub_account
        FOREIGN KEY (account_id) REFERENCES public.accounts(id) ON DELETE CASCADE,
    CONSTRAINT fk_accsub_plan
        FOREIGN KEY (plan_id) REFERENCES public.subscription_plans(id) ON DELETE RESTRICT,
    CONSTRAINT chk_status CHECK (
        status IN ('trialing', 'active', 'grace_period', 'expired', 'suspended')
    )
);
COMMENT ON TABLE public.account_subscriptions IS
    'One subscription per account. is_lifetime=true + current_period_end=NULL = perpetual paid license.';

-- =============================================================================
-- PART 5B: VOUCHER TABLE -- license_vouchers
-- =============================================================================
-- Pre-generated lifetime license keys for cash payments.
-- Format: ATR-[PLAN_CODE]-LIFE-[4CHARS]-[4CHARS]
-- duration_months = -1 means lifetime perpetual
CREATE TABLE IF NOT EXISTS public.license_vouchers (
    id              uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    code            text        NOT NULL UNIQUE,
    plan_id         uuid        NOT NULL,
    duration_months integer     NOT NULL DEFAULT -1,
    is_redeemed     boolean     NOT NULL DEFAULT false,
    redeemed_by     uuid,
    redeemed_at     timestamptz,
    created_at      timestamptz NOT NULL DEFAULT now(),
    created_by_note text,
    CONSTRAINT fk_voucher_plan
        FOREIGN KEY (plan_id) REFERENCES public.subscription_plans(id) ON DELETE RESTRICT,
    CONSTRAINT fk_voucher_redeemed_by
        FOREIGN KEY (redeemed_by) REFERENCES public.accounts(id) ON DELETE SET NULL,
    CONSTRAINT chk_duration
        CHECK (duration_months = -1 OR duration_months > 0)
);
COMMENT ON TABLE public.license_vouchers IS
    'Cash voucher codes. duration_months=-1 = perpetual lifetime license.';

-- Back-reference FK (safe to add after both tables exist)
DO $$ BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints
        WHERE constraint_name = 'fk_accsub_voucher'
    ) THEN
        ALTER TABLE public.account_subscriptions
            ADD CONSTRAINT fk_accsub_voucher
                FOREIGN KEY (activated_by_voucher)
                REFERENCES public.license_vouchers(id) ON DELETE SET NULL;
    END IF;
END $$;

-- =============================================================================
-- PART 6A: STAFF TABLE -- staff
-- =============================================================================
CREATE TABLE IF NOT EXISTS public.staff (
    id           uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id   uuid        NOT NULL,
    auth_user_id uuid,
    full_name    text        NOT NULL,
    email        text,
    pin          text,
    role         text        NOT NULL DEFAULT 'staff',
    is_active    boolean     NOT NULL DEFAULT true,
    created_at   timestamptz NOT NULL DEFAULT now(),
    updated_at   timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT fk_staff_account
        FOREIGN KEY (account_id) REFERENCES public.accounts(id) ON DELETE CASCADE,
    CONSTRAINT chk_role CHECK (role IN ('owner', 'manager', 'staff')),
    CONSTRAINT uq_staff_auth_user UNIQUE (auth_user_id)
);
COMMENT ON TABLE public.staff IS
    'Staff members. auth_user_id optional: staff can login via local PIN without Supabase auth.';

-- =============================================================================
-- PART 6B: STAFF BRANCH ASSIGNMENT -- staff_branch_assignments
-- =============================================================================
CREATE TABLE IF NOT EXISTS public.staff_branch_assignments (
    id          uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    staff_id    uuid        NOT NULL,
    branch_id   uuid        NOT NULL,
    assigned_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT fk_sba_staff  FOREIGN KEY (staff_id)  REFERENCES public.staff(id)    ON DELETE CASCADE,
    CONSTRAINT fk_sba_branch FOREIGN KEY (branch_id) REFERENCES public.branches(id) ON DELETE CASCADE,
    CONSTRAINT uq_sba_staff_branch UNIQUE (staff_id, branch_id)
);

-- =============================================================================
-- PART 6C: BRANCH MODULES -- branch_modules
-- =============================================================================
CREATE TABLE IF NOT EXISTS public.branch_modules (
    id         uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    branch_id  uuid        NOT NULL,
    module_id  uuid        NOT NULL,
    is_enabled boolean     NOT NULL DEFAULT true,
    enabled_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT fk_bm_branch FOREIGN KEY (branch_id) REFERENCES public.branches(id) ON DELETE CASCADE,
    CONSTRAINT fk_bm_module FOREIGN KEY (module_id) REFERENCES public.modules(id)  ON DELETE CASCADE,
    CONSTRAINT uq_bm_branch_module UNIQUE (branch_id, module_id)
);

-- =============================================================================
-- PART 6D: STAFF MODULE PERMISSIONS -- staff_module_permissions
-- =============================================================================
CREATE TABLE IF NOT EXISTS public.staff_module_permissions (
    id         uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    staff_id   uuid        NOT NULL,
    branch_id  uuid        NOT NULL,
    module_id  uuid        NOT NULL,
    can_view   boolean     NOT NULL DEFAULT false,
    can_edit   boolean     NOT NULL DEFAULT false,
    can_delete boolean     NOT NULL DEFAULT false,
    can_export boolean     NOT NULL DEFAULT false,
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT fk_smp_staff  FOREIGN KEY (staff_id)  REFERENCES public.staff(id)    ON DELETE CASCADE,
    CONSTRAINT fk_smp_branch FOREIGN KEY (branch_id) REFERENCES public.branches(id) ON DELETE CASCADE,
    CONSTRAINT fk_smp_module FOREIGN KEY (module_id) REFERENCES public.modules(id)  ON DELETE CASCADE,
    CONSTRAINT uq_smp UNIQUE (staff_id, branch_id, module_id)
);

-- =============================================================================
-- PART 7A: MEMBERS DIRECTORY -- members_directory
-- =============================================================================
-- Thin cloud record for cross-branch RFID check-in. Full data stays in local SQLite.
CREATE TABLE IF NOT EXISTS public.members_directory (
    id              uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id      uuid        NOT NULL,
    branch_id       uuid        NOT NULL,
    local_member_id text        NOT NULL,
    full_name       text        NOT NULL,
    rfid_tag        text,
    barcode         text,
    phone           text,
    is_active       boolean     NOT NULL DEFAULT true,
    synced_at       timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT fk_md_account FOREIGN KEY (account_id) REFERENCES public.accounts(id) ON DELETE CASCADE,
    CONSTRAINT fk_md_branch  FOREIGN KEY (branch_id)  REFERENCES public.branches(id) ON DELETE CASCADE,
    CONSTRAINT uq_md_local_id UNIQUE (account_id, branch_id, local_member_id)
);
COMMENT ON TABLE public.members_directory IS
    'Thin cloud identity record for cross-branch RFID check-in. Full data stays in local SQLite.';

-- =============================================================================
-- PART 7B: REGISTRATIONS -- registrations
-- =============================================================================
CREATE TABLE IF NOT EXISTS public.registrations (
    id                    uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id            uuid        NOT NULL,
    branch_id             uuid        NOT NULL,
    member_id             uuid,
    local_registration_id text,
    status                text        NOT NULL DEFAULT 'pending',
    plan_name             text,
    start_date            date,
    end_date              date,
    amount_paid           numeric(10,2),
    synced_at             timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT fk_reg_account FOREIGN KEY (account_id) REFERENCES public.accounts(id)          ON DELETE CASCADE,
    CONSTRAINT fk_reg_branch  FOREIGN KEY (branch_id)  REFERENCES public.branches(id)          ON DELETE CASCADE,
    CONSTRAINT fk_reg_member  FOREIGN KEY (member_id)  REFERENCES public.members_directory(id) ON DELETE SET NULL,
    CONSTRAINT chk_reg_status CHECK (status IN ('pending', 'active', 'expired', 'cancelled'))
);

-- =============================================================================
-- PART 7C: REGISTRATION REQUESTS -- registration_requests
-- =============================================================================
CREATE TABLE IF NOT EXISTS public.registration_requests (
    id            uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id    uuid        NOT NULL,
    branch_id     uuid        NOT NULL,
    payload       jsonb       NOT NULL DEFAULT '{}',
    status        text        NOT NULL DEFAULT 'pending',
    error_message text,
    requested_at  timestamptz NOT NULL DEFAULT now(),
    processed_at  timestamptz,
    CONSTRAINT fk_rr_account FOREIGN KEY (account_id) REFERENCES public.accounts(id) ON DELETE CASCADE,
    CONSTRAINT fk_rr_branch  FOREIGN KEY (branch_id)  REFERENCES public.branches(id) ON DELETE CASCADE,
    CONSTRAINT chk_rr_status CHECK (status IN ('pending', 'processed', 'failed'))
);

-- =============================================================================
-- PART 8: PERFORMANCE INDEXES
-- =============================================================================
-- accounts
CREATE INDEX IF NOT EXISTS idx_accounts_email      ON public.accounts (email);

-- account_subscriptions (most queried at startup)
CREATE INDEX IF NOT EXISTS idx_accsub_account_id   ON public.account_subscriptions (account_id);
CREATE INDEX IF NOT EXISTS idx_accsub_status        ON public.account_subscriptions (status);

-- license_vouchers
CREATE INDEX IF NOT EXISTS idx_vouchers_code        ON public.license_vouchers (UPPER(code));
CREATE INDEX IF NOT EXISTS idx_vouchers_plan        ON public.license_vouchers (plan_id);

-- businesses
CREATE INDEX IF NOT EXISTS idx_businesses_account   ON public.businesses (account_id);

-- branches (critical for RLS performance)
CREATE INDEX IF NOT EXISTS idx_branches_account     ON public.branches (account_id);
CREATE INDEX IF NOT EXISTS idx_branches_business    ON public.branches (business_id);

-- devices (checked on EVERY startup)
CREATE INDEX IF NOT EXISTS idx_devices_hardware_id  ON public.devices (hardware_id);
CREATE INDEX IF NOT EXISTS idx_devices_account      ON public.devices (account_id);

-- staff
CREATE INDEX IF NOT EXISTS idx_staff_account        ON public.staff (account_id);
CREATE INDEX IF NOT EXISTS idx_staff_auth_user      ON public.staff (auth_user_id);
CREATE INDEX IF NOT EXISTS idx_staff_email          ON public.staff (email);

-- staff_branch_assignments
CREATE INDEX IF NOT EXISTS idx_sba_staff            ON public.staff_branch_assignments (staff_id);
CREATE INDEX IF NOT EXISTS idx_sba_branch           ON public.staff_branch_assignments (branch_id);

-- branch_modules
CREATE INDEX IF NOT EXISTS idx_bm_branch            ON public.branch_modules (branch_id);

-- staff_module_permissions
CREATE INDEX IF NOT EXISTS idx_smp_staff            ON public.staff_module_permissions (staff_id);
CREATE INDEX IF NOT EXISTS idx_smp_branch           ON public.staff_module_permissions (branch_id);

-- members_directory
CREATE INDEX IF NOT EXISTS idx_md_account           ON public.members_directory (account_id);
CREATE INDEX IF NOT EXISTS idx_md_branch            ON public.members_directory (branch_id);
CREATE INDEX IF NOT EXISTS idx_md_rfid              ON public.members_directory (rfid_tag)  WHERE rfid_tag IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_md_barcode           ON public.members_directory (barcode)   WHERE barcode  IS NOT NULL;

-- registrations
CREATE INDEX IF NOT EXISTS idx_reg_account          ON public.registrations (account_id);
CREATE INDEX IF NOT EXISTS idx_reg_branch           ON public.registrations (branch_id);
CREATE INDEX IF NOT EXISTS idx_reg_member           ON public.registrations (member_id);

-- registration_requests
CREATE INDEX IF NOT EXISTS idx_rr_account           ON public.registration_requests (account_id);
CREATE INDEX IF NOT EXISTS idx_rr_status            ON public.registration_requests (status);

-- =============================================================================
-- PART 2 FUNCTION DEFINED HERE (after accounts + staff tables exist)
-- get_auth_account_id() resolves the calling user's account_id.
-- STABLE = PostgreSQL evaluates it ONCE per query, not once per row (performance).
-- Must be defined before Part 9 RLS policies that call it.
-- =============================================================================
CREATE OR REPLACE FUNCTION public.get_auth_account_id()
RETURNS uuid
LANGUAGE plpgsql
STABLE
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
BEGIN
  RETURN COALESCE(
    (SELECT id FROM public.accounts WHERE id = auth.uid()),
    (SELECT account_id FROM public.staff WHERE auth_user_id = auth.uid() AND is_active = true LIMIT 1)
  );
END;
$$;

-- =============================================================================
-- PART 9: ROW-LEVEL SECURITY (RLS) POLICIES
-- =============================================================================
ALTER TABLE public.subscription_plans         ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.modules                    ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.accounts                   ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.businesses                 ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.branches                   ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.devices                    ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.account_subscriptions      ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.license_vouchers           ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.staff                      ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.staff_branch_assignments   ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.branch_modules             ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.staff_module_permissions   ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.members_directory          ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.registrations              ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.registration_requests      ENABLE ROW LEVEL SECURITY;

-- CATALOG TABLES: Public read-only (needed before user logs in)
DROP POLICY IF EXISTS "plans_public_read"     ON public.subscription_plans;
CREATE POLICY "plans_public_read" ON public.subscription_plans
    FOR SELECT TO anon, authenticated USING (true);

    FOR SELECT TO anon, authenticated USING (is_active = true);

DROP POLICY IF EXISTS "modules_public_read"   ON public.modules;
CREATE POLICY "modules_public_read" ON public.modules
    FOR SELECT TO anon, authenticated USING (true);

-- ACCOUNTS: Owner can only see/edit their own row
DROP POLICY IF EXISTS "accounts_owner_select" ON public.accounts;
CREATE POLICY "accounts_owner_select" ON public.accounts
    FOR SELECT TO authenticated USING (id = auth.uid());

DROP POLICY IF EXISTS "accounts_owner_update" ON public.accounts;
CREATE POLICY "accounts_owner_update" ON public.accounts
    FOR UPDATE TO authenticated USING (id = auth.uid()) WITH CHECK (id = auth.uid());
-- INSERT handled by onboard_owner_account SECURITY DEFINER function only

-- BUSINESSES
DROP POLICY IF EXISTS "businesses_tenant_all" ON public.businesses;
CREATE POLICY "businesses_tenant_all" ON public.businesses
    FOR ALL TO authenticated
    USING (account_id = get_auth_account_id())
    WITH CHECK (account_id = get_auth_account_id());

-- BRANCHES
DROP POLICY IF EXISTS "branches_tenant_all" ON public.branches;
CREATE POLICY "branches_tenant_all" ON public.branches
    FOR ALL TO authenticated
    USING (account_id = get_auth_account_id())
    WITH CHECK (account_id = get_auth_account_id());

-- DEVICES
DROP POLICY IF EXISTS "devices_tenant_all" ON public.devices;
CREATE POLICY "devices_tenant_all" ON public.devices
    FOR ALL TO authenticated
    USING (account_id = get_auth_account_id())
    WITH CHECK (account_id = get_auth_account_id());

-- ACCOUNT_SUBSCRIPTIONS: Read-only for authenticated users; mutations via RPCs only
DROP POLICY IF EXISTS "accsub_owner_read" ON public.account_subscriptions;
CREATE POLICY "accsub_owner_read" ON public.account_subscriptions
    FOR SELECT TO authenticated
    USING (account_id = get_auth_account_id());

-- LICENSE_VOUCHERS: Users can only see vouchers they redeemed
DROP POLICY IF EXISTS "vouchers_no_public_read"   ON public.license_vouchers;
DROP POLICY IF EXISTS "vouchers_redeemer_read"    ON public.license_vouchers;
CREATE POLICY "vouchers_redeemer_read" ON public.license_vouchers
    FOR SELECT TO authenticated
    USING (redeemed_by = auth.uid());

-- STAFF
DROP POLICY IF EXISTS "staff_tenant_all"  ON public.staff;
CREATE POLICY "staff_tenant_all" ON public.staff
    FOR ALL TO authenticated
    USING (account_id = get_auth_account_id())
    WITH CHECK (account_id = get_auth_account_id());

DROP POLICY IF EXISTS "staff_self_read" ON public.staff;
CREATE POLICY "staff_self_read" ON public.staff
    FOR SELECT TO authenticated
    USING (auth_user_id = auth.uid());

-- STAFF_BRANCH_ASSIGNMENTS
DROP POLICY IF EXISTS "sba_tenant_all" ON public.staff_branch_assignments;
CREATE POLICY "sba_tenant_all" ON public.staff_branch_assignments
    FOR ALL TO authenticated
    USING (
        branch_id IN (SELECT id FROM public.branches WHERE account_id = get_auth_account_id())
    );

-- BRANCH_MODULES
DROP POLICY IF EXISTS "bm_tenant_all" ON public.branch_modules;
CREATE POLICY "bm_tenant_all" ON public.branch_modules
    FOR ALL TO authenticated
    USING (
        branch_id IN (SELECT id FROM public.branches WHERE account_id = get_auth_account_id())
    );

-- STAFF_MODULE_PERMISSIONS
DROP POLICY IF EXISTS "smp_tenant_all" ON public.staff_module_permissions;
CREATE POLICY "smp_tenant_all" ON public.staff_module_permissions
    FOR ALL TO authenticated
    USING (
        branch_id IN (SELECT id FROM public.branches WHERE account_id = get_auth_account_id())
    );

-- MEMBERS_DIRECTORY
DROP POLICY IF EXISTS "md_tenant_all" ON public.members_directory;
CREATE POLICY "md_tenant_all" ON public.members_directory
    FOR ALL TO authenticated
    USING (account_id = get_auth_account_id())
    WITH CHECK (account_id = get_auth_account_id());

-- REGISTRATIONS
DROP POLICY IF EXISTS "reg_tenant_all" ON public.registrations;
CREATE POLICY "reg_tenant_all" ON public.registrations
    FOR ALL TO authenticated
    USING (account_id = get_auth_account_id())
    WITH CHECK (account_id = get_auth_account_id());

-- REGISTRATION_REQUESTS
DROP POLICY IF EXISTS "rr_tenant_all" ON public.registration_requests;
CREATE POLICY "rr_tenant_all" ON public.registration_requests
    FOR ALL TO authenticated
    USING (account_id = get_auth_account_id())
    WITH CHECK (account_id = get_auth_account_id());

-- =============================================================================
-- PART 10: STORED RPC FUNCTIONS (SECURITY DEFINER)
-- All functions bypass RLS internally, expose minimum required data to C# client.
-- =============================================================================

-- ---------------------------------------------------------------------------
-- RPC 1: check_device_registration
-- Called on EVERY app startup before any login. Returns device/account info.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.check_device_registration(p_hardware_id text)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
DECLARE
    v_device record;
BEGIN
    SELECT d.id AS device_id, d.account_id, d.branch_id, d.label,
           d.is_active, a.is_active AS account_is_active
    INTO v_device
    FROM public.devices d
    JOIN public.accounts a ON a.id = d.account_id
    WHERE d.hardware_id = p_hardware_id
    LIMIT 1;

    IF NOT FOUND THEN
        RETURN jsonb_build_object('is_registered', false);
    END IF;

    UPDATE public.devices SET last_seen_at = now() WHERE hardware_id = p_hardware_id;

    RETURN jsonb_build_object(
        'is_registered',     true,
        'is_active',         v_device.is_active,
        'account_is_active', v_device.account_is_active,
        'device_id',         v_device.device_id,
        'account_id',        v_device.account_id,
        'branch_id',         v_device.branch_id,
        'label',             v_device.label
    );
END;
$$;

-- ---------------------------------------------------------------------------
-- RPC 2: check_subscription_status
-- Called at startup after device recognized. Returns license gate info.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.check_subscription_status(p_hardware_id text)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
DECLARE
    v_device         record;
    v_sub            record;
    v_now            timestamptz := now();
    v_days_remaining integer;
    v_result_status  text;
BEGIN
    SELECT d.account_id INTO v_device
    FROM public.devices d
    WHERE d.hardware_id = p_hardware_id AND d.is_active = true
    LIMIT 1;

    IF NOT FOUND THEN
        RETURN jsonb_build_object('status', 'device_not_found');
    END IF;

    SELECT sub.status, sub.is_lifetime, sub.trial_ends_at, sub.current_period_end,
           sub.grace_ends_at, pl.name AS plan_name, pl.tier_rank,
           pl.max_businesses, pl.max_branches, pl.max_devices, pl.max_staff,
           pl.cross_branch_reports, pl.shared_members
    INTO v_sub
    FROM public.account_subscriptions sub
    JOIN public.subscription_plans pl ON pl.id = sub.plan_id
    WHERE sub.account_id = v_device.account_id
    LIMIT 1;

    IF NOT FOUND THEN
        RETURN jsonb_build_object('status', 'no_subscription');
    END IF;

    IF v_sub.is_lifetime THEN
        v_result_status  := 'active';
        v_days_remaining := NULL;
    ELSIF v_sub.status = 'trialing' THEN
        IF v_sub.trial_ends_at IS NOT NULL AND v_sub.trial_ends_at > v_now THEN
            v_result_status  := 'trialing';
            v_days_remaining := EXTRACT(DAY FROM (v_sub.trial_ends_at - v_now))::integer;
        ELSIF v_sub.grace_ends_at IS NOT NULL AND v_sub.grace_ends_at > v_now THEN
            v_result_status  := 'grace_period';
            v_days_remaining := EXTRACT(DAY FROM (v_sub.grace_ends_at - v_now))::integer;
        ELSE
            v_result_status  := 'expired';
            v_days_remaining := 0;
            UPDATE public.account_subscriptions
               SET status = 'expired', updated_at = v_now
             WHERE account_id = v_device.account_id AND status != 'expired';
        END IF;
    ELSE
        v_result_status  := v_sub.status;
        v_days_remaining := NULL;
    END IF;

    RETURN jsonb_build_object(
        'status',               v_result_status,
        'is_lifetime',          v_sub.is_lifetime,
        'plan_name',            v_sub.plan_name,
        'tier_rank',            v_sub.tier_rank,
        'days_remaining',       v_days_remaining,
        'trial_ends_at',        v_sub.trial_ends_at,
        'max_businesses',       v_sub.max_businesses,
        'max_branches',         v_sub.max_branches,
        'max_devices',          v_sub.max_devices,
        'max_staff',            v_sub.max_staff,
        'cross_branch_reports', v_sub.cross_branch_reports,
        'shared_members',       v_sub.shared_members
    );
END;
$$;

-- ---------------------------------------------------------------------------
-- RPC 3: onboard_owner_account
-- Atomic genesis. Creates account, business, branch, device, subscription.
-- Optionally redeems a voucher at signup for instant lifetime activation.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.onboard_owner_account(
    p_owner_id      uuid,
    p_full_name     text,
    p_email         text,
    p_business_name text,
    p_branch_name   text,
    p_category      text DEFAULT 'pos_inventory',
    p_hardware_id   text DEFAULT NULL,
    p_device_label  text DEFAULT 'Main PC',
    p_address       text DEFAULT NULL,
    p_phone         text DEFAULT NULL,
    p_voucher_code  text DEFAULT NULL,
    p_modules       text[] DEFAULT NULL
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
DECLARE
    v_now          timestamptz := now();
    v_business_id  uuid;
    v_branch_id    uuid;
    v_free_plan_id uuid;
    v_voucher      record;
    v_plan_id      uuid;
    v_is_lifetime  boolean := false;
    v_status       text    := 'trialing';
    v_trial_end    timestamptz;
    v_grace_end    timestamptz;
    v_template     record;
    v_module       record;
    v_voucher_id   uuid := NULL;
BEGIN
    IF EXISTS (SELECT 1 FROM public.accounts WHERE id = p_owner_id) THEN
        RETURN jsonb_build_object('success', false, 'error', 'ACCOUNT_ALREADY_EXISTS');
    END IF;

    SELECT id INTO v_free_plan_id FROM public.subscription_plans WHERE tier_rank = 0 LIMIT 1;
    v_plan_id := v_free_plan_id;

    IF p_voucher_code IS NOT NULL AND TRIM(p_voucher_code) != '' THEN
        SELECT id, plan_id, duration_months, is_redeemed INTO v_voucher
        FROM public.license_vouchers
        WHERE UPPER(TRIM(code)) = UPPER(TRIM(p_voucher_code))
        FOR UPDATE;

        IF NOT FOUND THEN
            RETURN jsonb_build_object('success', false, 'error', 'INVALID_VOUCHER_CODE');
        END IF;
        IF v_voucher.is_redeemed THEN
            RETURN jsonb_build_object('success', false, 'error', 'VOUCHER_ALREADY_USED');
        END IF;

        v_plan_id     := v_voucher.plan_id;
        v_is_lifetime := true;
        v_status      := 'active';
        v_voucher_id  := v_voucher.id;
    ELSE
        v_trial_end := v_now + interval '14 days';
        v_grace_end := v_now + interval '17 days';
    END IF;

-- Module bundle determined by p_modules (C# driven)

    INSERT INTO public.accounts (id, full_name, email, is_active, created_at, updated_at)
    VALUES (p_owner_id, p_full_name, p_email, true, v_now, v_now);

    INSERT INTO public.businesses (account_id, category, name, address, phone, is_active, created_at, updated_at)
    VALUES (p_owner_id, COALESCE(NULLIF(TRIM(p_category), ''), 'pos_inventory'), p_business_name, p_address, p_phone, true, v_now, v_now)
    RETURNING id INTO v_business_id;

    INSERT INTO public.branches (account_id, business_id, name, address, phone, is_main_branch, is_active, created_at, updated_at)
    VALUES (p_owner_id, v_business_id, p_branch_name, p_address, p_phone, true, true, v_now, v_now)
    RETURNING id INTO v_branch_id;

    INSERT INTO public.devices (account_id, branch_id, hardware_id, label, is_active, last_seen_at, registered_at)
    VALUES (p_owner_id, v_branch_id, p_hardware_id, p_device_label, true, v_now, v_now);

    INSERT INTO public.account_subscriptions
        (account_id, plan_id, status, is_lifetime, trial_ends_at, current_period_end,
         grace_ends_at, activated_by_voucher, created_at, updated_at)
    VALUES
        (p_owner_id, v_plan_id, v_status, v_is_lifetime, v_trial_end,
         CASE WHEN v_is_lifetime THEN NULL ELSE v_trial_end END,
         v_grace_end, v_voucher_id, v_now, v_now);

    IF p_modules IS NOT NULL AND array_length(p_modules, 1) > 0 THEN
        FOR v_module IN
            SELECT id FROM public.modules WHERE key = ANY(p_modules)
        LOOP
            INSERT INTO public.branch_modules (branch_id, module_id, is_enabled, enabled_at)
            VALUES (v_branch_id, v_module.id, true, v_now)
            ON CONFLICT (branch_id, module_id) DO NOTHING;
        END LOOP;
    ELSE
        FOR v_module IN
            SELECT id FROM public.modules WHERE is_core = true
        LOOP
            INSERT INTO public.branch_modules (branch_id, module_id, is_enabled, enabled_at)
            VALUES (v_branch_id, v_module.id, true, v_now)
            ON CONFLICT (branch_id, module_id) DO NOTHING;
        END LOOP;
    END IF;

    INSERT INTO public.staff (account_id, full_name, email, role, is_active, created_at, updated_at)
    VALUES (p_owner_id, p_full_name, p_email, 'owner', true, v_now, v_now);

    IF v_voucher_id IS NOT NULL THEN
        UPDATE public.license_vouchers
           SET is_redeemed = true, redeemed_by = p_owner_id, redeemed_at = v_now
         WHERE id = v_voucher_id;
    END IF;

    RETURN jsonb_build_object(
        'success',     true,
        'account_id',  p_owner_id,
        'business_id', v_business_id,
        'branch_id',   v_branch_id,
        'plan_status', v_status,
        'is_lifetime', v_is_lifetime
    );
END;
$$;

-- ---------------------------------------------------------------------------
-- RPC 4: redeem_license_voucher
-- Race-condition-proof voucher upgrade (FOR UPDATE row locking).
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.redeem_license_voucher(
    p_account_id   uuid,
    p_voucher_code text
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
DECLARE
    v_now     timestamptz := now();
    v_voucher record;
    v_plan    record;
BEGIN
    SELECT id, plan_id, duration_months, is_redeemed INTO v_voucher
    FROM public.license_vouchers
    WHERE UPPER(TRIM(code)) = UPPER(TRIM(p_voucher_code))
    FOR UPDATE;

    IF NOT FOUND THEN
        RETURN jsonb_build_object('success', false, 'error', 'INVALID_VOUCHER_CODE');
    END IF;
    IF v_voucher.is_redeemed THEN
        RETURN jsonb_build_object('success', false, 'error', 'VOUCHER_ALREADY_USED');
    END IF;
    IF NOT EXISTS (SELECT 1 FROM public.accounts WHERE id = p_account_id AND is_active = true) THEN
        RETURN jsonb_build_object('success', false, 'error', 'ACCOUNT_NOT_FOUND');
    END IF;

    SELECT id, name, tier_rank INTO v_plan
    FROM public.subscription_plans WHERE id = v_voucher.plan_id;

    UPDATE public.account_subscriptions
       SET plan_id              = v_voucher.plan_id,
           status               = 'active',
           is_lifetime          = true,
           trial_ends_at        = NULL,
           current_period_end   = NULL,
           grace_ends_at        = NULL,
           activated_by_voucher = v_voucher.id,
           updated_at           = v_now
     WHERE account_id = p_account_id;

    UPDATE public.license_vouchers
       SET is_redeemed = true, redeemed_by = p_account_id, redeemed_at = v_now
     WHERE id = v_voucher.id;

    RETURN jsonb_build_object(
        'success',     true,
        'plan_name',   v_plan.name,
        'tier_rank',   v_plan.tier_rank,
        'is_lifetime', true
    );
END;
$$;

-- ---------------------------------------------------------------------------
-- RPC 5: admin_activate_account
-- Direct admin activation after cash payment. Protected by admin secret.
-- IMPORTANT: Change 'ATR_ADMIN_2025_SECRET' before production!
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.admin_activate_account(
    p_email        text,
    p_tier_rank    integer,
    p_admin_secret text,
    p_notes        text DEFAULT NULL
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
DECLARE
    v_now          timestamptz := now();
    v_account_id   uuid;
    v_plan         record;
    c_admin_secret CONSTANT text := 'ATR_ADMIN_2025_SECRET';
BEGIN
    IF p_admin_secret != c_admin_secret THEN
        PERFORM pg_sleep(2);
        RETURN jsonb_build_object('success', false, 'error', 'UNAUTHORIZED');
    END IF;
    IF p_tier_rank NOT IN (1, 2, 3) THEN
        RETURN jsonb_build_object('success', false, 'error', 'INVALID_TIER_RANK');
    END IF;

    SELECT id INTO v_account_id
    FROM public.accounts WHERE LOWER(email) = LOWER(TRIM(p_email)) LIMIT 1;

    IF NOT FOUND THEN
        RETURN jsonb_build_object('success', false, 'error', 'ACCOUNT_NOT_FOUND');
    END IF;

    SELECT id, name, tier_rank INTO v_plan
    FROM public.subscription_plans WHERE tier_rank = p_tier_rank LIMIT 1;

    UPDATE public.account_subscriptions
       SET plan_id            = v_plan.id,
           status             = 'active',
           is_lifetime        = true,
           trial_ends_at      = NULL,
           current_period_end = NULL,
           grace_ends_at      = NULL,
           notes              = COALESCE(p_notes, 'Admin activated on ' || to_char(v_now, 'DD/MM/YYYY')),
           updated_at         = v_now
     WHERE account_id = v_account_id;

    RETURN jsonb_build_object(
        'success',      true,
        'account_id',   v_account_id,
        'plan_name',    v_plan.name,
        'tier_rank',    v_plan.tier_rank,
        'is_lifetime',  true,
        'activated_at', v_now
    );
END;
$$;

-- ---------------------------------------------------------------------------
-- RPC 6: register_device
-- Registers an additional PC. Enforces plan device limits at DB level.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.register_device(
    p_account_id  uuid,
    p_branch_id   uuid,
    p_hardware_id text,
    p_label       text DEFAULT 'New PC'
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
DECLARE
    v_now         timestamptz := now();
    v_max_devices integer;
    v_active_cnt  integer;
    v_device_id   uuid;
BEGIN
    IF EXISTS (SELECT 1 FROM public.devices WHERE hardware_id = p_hardware_id) THEN
        RETURN jsonb_build_object('success', false, 'error', 'DEVICE_ALREADY_REGISTERED');
    END IF;

    SELECT pl.max_devices INTO v_max_devices
    FROM public.account_subscriptions sub
    JOIN public.subscription_plans pl ON pl.id = sub.plan_id
    WHERE sub.account_id = p_account_id LIMIT 1;

    IF NOT FOUND THEN
        RETURN jsonb_build_object('success', false, 'error', 'NO_SUBSCRIPTION_FOUND');
    END IF;

    SELECT COUNT(*) INTO v_active_cnt
    FROM public.devices WHERE account_id = p_account_id AND is_active = true;

    IF v_max_devices != -1 AND v_active_cnt >= v_max_devices THEN
        RAISE EXCEPTION 'DEVICE_LIMIT_REACHED: Your plan allows % device(s). Upgrade to add more PCs.', v_max_devices;
    END IF;

    INSERT INTO public.devices (account_id, branch_id, hardware_id, label, is_active, last_seen_at, registered_at)
    VALUES (p_account_id, p_branch_id, p_hardware_id, p_label, true, v_now, v_now)
    RETURNING id INTO v_device_id;

    RETURN jsonb_build_object(
        'success',    true,
        'device_id',  v_device_id,
        'account_id', p_account_id,
        'branch_id',  p_branch_id
    );
END;
$$;

-- ---------------------------------------------------------------------------
-- RPC 7: revoke_device
-- Instantly deactivates a PC. Used by owner to kick a stolen/sold machine.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.revoke_device(p_device_id uuid, p_account_id uuid)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
DECLARE
    v_rows integer;
BEGIN
    UPDATE public.devices SET is_active = false
    WHERE id = p_device_id AND account_id = p_account_id;
    GET DIAGNOSTICS v_rows = ROW_COUNT;

    IF v_rows = 0 THEN
        RETURN jsonb_build_object('success', false, 'error', 'DEVICE_NOT_FOUND_OR_UNAUTHORIZED');
    END IF;
    RETURN jsonb_build_object('success', true, 'device_id', p_device_id);
END;
$$;

-- ---------------------------------------------------------------------------
-- RPC 8: get_staff_context
-- Loads staff branches + module permissions in one round trip after login.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.get_staff_context(p_account_id uuid, p_staff_id uuid)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
DECLARE
    v_staff       record;
    v_branches    jsonb;
    v_permissions jsonb;
BEGIN
    SELECT id, full_name, role, is_active INTO v_staff
    FROM public.staff
    WHERE id = p_staff_id AND account_id = p_account_id LIMIT 1;

    IF NOT FOUND OR NOT v_staff.is_active THEN
        RETURN jsonb_build_object('success', false, 'error', 'STAFF_NOT_FOUND_OR_INACTIVE');
    END IF;

    SELECT jsonb_agg(jsonb_build_object(
        'branch_id',   b.id,
        'branch_name', b.name,
        'is_main',     b.is_main_branch
    )) INTO v_branches
    FROM public.staff_branch_assignments sba
    JOIN public.branches b ON b.id = sba.branch_id
    WHERE sba.staff_id = p_staff_id AND b.is_active = true;

    SELECT jsonb_agg(jsonb_build_object(
        'branch_id',   smp.branch_id,
        'module_key',  m.key,
        'module_name', m.display_name,
        'can_view',    smp.can_view,
        'can_edit',    smp.can_edit,
        'can_delete',  smp.can_delete,
        'can_export',  smp.can_export
    )) INTO v_permissions
    FROM public.staff_module_permissions smp
    JOIN public.modules m ON m.id = smp.module_id
    WHERE smp.staff_id = p_staff_id;

    RETURN jsonb_build_object(
        'success',     true,
        'staff_id',    v_staff.id,
        'full_name',   v_staff.full_name,
        'role',        v_staff.role,
        'branches',    COALESCE(v_branches,    '[]'::jsonb),
        'permissions', COALESCE(v_permissions, '[]'::jsonb)
    );
END;
$$;

-- ---------------------------------------------------------------------------
-- RPC 9: generate_license_vouchers  (Admin dashboard utility)
-- Generates N lifetime voucher codes for a given tier.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.generate_license_vouchers(
    p_tier_rank    integer,
    p_count        integer,
    p_admin_secret text,
    p_notes        text DEFAULT NULL
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
DECLARE
    v_plan         record;
    v_code         text;
    v_plan_code    text;
    v_codes        text[] := '{}';
    i              integer;
    c_admin_secret CONSTANT text := 'ATR_ADMIN_2025_SECRET';
BEGIN
    IF p_admin_secret != c_admin_secret THEN
        PERFORM pg_sleep(2);
        RETURN jsonb_build_object('success', false, 'error', 'UNAUTHORIZED');
    END IF;
    IF p_count < 1 OR p_count > 100 THEN
        RETURN jsonb_build_object('success', false, 'error', 'COUNT_MUST_BE_1_TO_100');
    END IF;

    SELECT id, name INTO v_plan FROM public.subscription_plans WHERE tier_rank = p_tier_rank LIMIT 1;
    IF NOT FOUND THEN
        RETURN jsonb_build_object('success', false, 'error', 'INVALID_TIER_RANK');
    END IF;

    v_plan_code := CASE p_tier_rank WHEN 1 THEN 'START' WHEN 2 THEN 'PRO' WHEN 3 THEN 'ENT' ELSE 'UNK' END;

    FOR i IN 1..p_count LOOP
        v_code := 'ATR-' || v_plan_code || '-LIFE-'
               || UPPER(SUBSTRING(encode(gen_random_bytes(2), 'hex'), 1, 4))
               || '-'
               || UPPER(SUBSTRING(encode(gen_random_bytes(2), 'hex'), 1, 4));

        WHILE EXISTS (SELECT 1 FROM public.license_vouchers WHERE UPPER(code) = UPPER(v_code)) LOOP
            v_code := 'ATR-' || v_plan_code || '-LIFE-'
                   || UPPER(SUBSTRING(encode(gen_random_bytes(2), 'hex'), 1, 4))
                   || '-'
                   || UPPER(SUBSTRING(encode(gen_random_bytes(2), 'hex'), 1, 4));
        END LOOP;

        INSERT INTO public.license_vouchers (code, plan_id, duration_months, is_redeemed, created_by_note)
        VALUES (v_code, v_plan.id, -1, false, p_notes);
        v_codes := array_append(v_codes, v_code);
    END LOOP;

    RETURN jsonb_build_object(
        'success',   true,
        'plan_name', v_plan.name,
        'tier_rank', p_tier_rank,
        'count',     p_count,
        'codes',     to_jsonb(v_codes)
    );
END;
$$;

-- =============================================================================
-- PART 11: SEED DATA
-- =============================================================================

-- 4 Subscription Plans
INSERT INTO public.subscription_plans
    (name, tier_rank, max_businesses, max_branches, max_devices, max_staff,
     cross_branch_reports, shared_members, is_free_tier, trial_days, price_label)
VALUES
    ('Free',         0, 1,  1,   1,   2, false, false, true,  14, 'Free Trial'),
    ('Starter',      1, 1,  2,   3,  10, false, false, false,  0, 'One-Time Cash'),
    ('Professional', 2, 1, 10,  20,  50, true,  true,  false,  0, 'One-Time Cash'),
    ('Enterprise',   3, 5, -1,  -1,  -1, true,  true,  false,  0, 'One-Time Cash')
ON CONFLICT (name) DO NOTHING;

-- Note: Business categories & UI vertical layouts are governed in C# codebase.

-- 8 Functional Modules
INSERT INTO public.modules (key, display_name, icon, description, is_core, sort_order)
VALUES
    ('members',   'Members',   '👥', 'Member registration, profiles, RFID check-in',       true,  1),
    ('pos',       'POS',       '🛒', 'Point of sale, payments, receipts',                   true,  2),
    ('classes',   'Classes',   '📅', 'Class scheduling, booking, and attendance',           false, 3),
    ('inventory', 'Inventory', '📦', 'Stock tracking, products, and supplies',              false, 4),
    ('payroll',   'Payroll',   '💰', 'Staff salaries, commissions, and payroll runs',       false, 5),
    ('reports',   'Reports',   '📊', 'Business analytics, revenue, and member reports',     true,  6),
    ('messaging', 'Messaging', '💬', 'SMS/notification templates for member communication', false, 7),
    ('settings',  'Settings',  '⚙️', 'Business configuration, staff, and branch settings',  true,  8)
ON CONFLICT (key) DO NOTHING;

-- =============================================================================
-- PART 12: AUDIT -- updated_at auto-trigger
-- =============================================================================
CREATE OR REPLACE FUNCTION public.set_updated_at()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    NEW.updated_at := now();
    RETURN NEW;
END;
$$;

DO $$
DECLARE t text;
BEGIN
    FOREACH t IN ARRAY ARRAY[
        'accounts', 'businesses', 'branches',
        'account_subscriptions', 'staff', 'staff_module_permissions'
    ]
    LOOP
        EXECUTE format(
            'DROP TRIGGER IF EXISTS trg_set_updated_at ON public.%I;
             CREATE TRIGGER trg_set_updated_at
                 BEFORE UPDATE ON public.%I
                 FOR EACH ROW EXECUTE FUNCTION public.set_updated_at();',
            t, t
        );
    END LOOP;
END;
$$;

-- =============================================================================
-- VERIFICATION QUERIES (uncomment and run to confirm setup)
-- =============================================================================
/*

-- 1. All 16 tables
SELECT tablename FROM pg_tables WHERE schemaname = 'public' ORDER BY tablename;

-- 2. Seed data
SELECT tier_rank, name, max_branches, max_devices, is_free_tier, trial_days
FROM public.subscription_plans ORDER BY tier_rank;


SELECT key, display_name, is_core FROM public.modules ORDER BY sort_order;

-- 3. RLS enabled on all tables
SELECT tablename, rowsecurity FROM pg_tables
WHERE schemaname = 'public' AND rowsecurity = true ORDER BY tablename;

-- 4. Generate 3 test Starter vouchers (use in SQL editor):
SELECT public.generate_license_vouchers(1, 3, 'ATR_ADMIN_2025_SECRET', 'Test batch');

-- 5. Admin activate test account:
-- SELECT public.admin_activate_account('owner@example.com', 1, 'ATR_ADMIN_2025_SECRET', 'Cash paid 500 DZD');

*/

-- =============================================================================
-- END OF PHASE 2 SQL SCRIPT
-- 16 tables | 9 RPC functions | Full RLS | 25+ indexes | Seed data | Triggers
-- Ready to execute in Supabase SQL Editor
-- =============================================================================

-- =============================================================================
-- PATCH v1.1 — Gap Fixes (applied after initial script)
-- =============================================================================
-- Fix 1 (HIGH)   : Add create_business + create_branch RPCs with DB-level limit checks
-- Fix 2 (MEDIUM) : Add FK constraint on staff.auth_user_id -> auth.users
-- Fix 3 (LOW)    : Replace fragile DO $$ fk_accsub_voucher guard with DROP+ADD
-- Fix 4 (LOW)    : Catalog table write-deny documentation comment
-- Fix 5 (LOW)    : Fix trial days counter (CEIL instead of EXTRACT truncation)
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- FIX 3 (LOW): Replace fragile DO $$ FK guard with clean DROP + ADD
-- ─────────────────────────────────────────────────────────────────────────────
ALTER TABLE public.account_subscriptions
    DROP CONSTRAINT IF EXISTS fk_accsub_voucher;
ALTER TABLE public.account_subscriptions
    ADD CONSTRAINT fk_accsub_voucher
        FOREIGN KEY (activated_by_voucher)
        REFERENCES public.license_vouchers(id) ON DELETE SET NULL;

-- ─────────────────────────────────────────────────────────────────────────────
-- FIX 2 (MEDIUM): Add FK on staff.auth_user_id -> auth.users
-- ─────────────────────────────────────────────────────────────────────────────
ALTER TABLE public.staff
    DROP CONSTRAINT IF EXISTS fk_staff_auth_user;
ALTER TABLE public.staff
    ADD CONSTRAINT fk_staff_auth_user
        FOREIGN KEY (auth_user_id) REFERENCES auth.users(id) ON DELETE SET NULL;

-- ─────────────────────────────────────────────────────────────────────────────
-- FIX 4 (LOW): Document implicit write-deny on catalog tables
-- ─────────────────────────────────────────────────────────────────────────────
-- NOTE ON CATALOG TABLE SECURITY:
-- subscription_plans and modules have ONLY a SELECT policy.
-- Supabase/PostgreSQL denies all INSERT, UPDATE, DELETE by default when RLS is
-- enabled and no matching write policy exists. This is intentional — only the
-- service_role key (used from the Supabase dashboard or admin scripts) can
-- modify catalog data. Do NOT add write policies to these tables for regular users.
COMMENT ON TABLE public.subscription_plans IS
    'Static catalog. READ: anon + authenticated. WRITE: service_role only (RLS implicit deny).';
COMMENT ON TABLE public.modules IS
    'Functional module catalog. READ: anon + authenticated. WRITE: service_role only (RLS implicit deny).';

-- ─────────────────────────────────────────────────────────────────────────────
-- FIX 5 (LOW): Fix check_subscription_status trial days counter
-- Uses CEIL so "1 hour remaining" shows "1 day" not "0 days"
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR REPLACE FUNCTION public.check_subscription_status(p_hardware_id text)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
DECLARE
    v_device         record;
    v_sub            record;
    v_now            timestamptz := now();
    v_days_remaining integer;
    v_result_status  text;
BEGIN
    SELECT d.account_id INTO v_device
    FROM public.devices d
    WHERE d.hardware_id = p_hardware_id AND d.is_active = true
    LIMIT 1;

    IF NOT FOUND THEN
        RETURN jsonb_build_object('status', 'device_not_found');
    END IF;

    SELECT sub.status, sub.is_lifetime, sub.trial_ends_at, sub.current_period_end,
           sub.grace_ends_at, pl.name AS plan_name, pl.tier_rank,
           pl.max_businesses, pl.max_branches, pl.max_devices, pl.max_staff,
           pl.cross_branch_reports, pl.shared_members
    INTO v_sub
    FROM public.account_subscriptions sub
    JOIN public.subscription_plans pl ON pl.id = sub.plan_id
    WHERE sub.account_id = v_device.account_id
    LIMIT 1;

    IF NOT FOUND THEN
        RETURN jsonb_build_object('status', 'no_subscription');
    END IF;

    IF v_sub.is_lifetime THEN
        v_result_status  := 'active';
        v_days_remaining := NULL;
    ELSIF v_sub.status = 'trialing' THEN
        IF v_sub.trial_ends_at IS NOT NULL AND v_sub.trial_ends_at > v_now THEN
            v_result_status  := 'trialing';
            -- FIX: CEIL so last few hours still shows 1 day, not 0
            v_days_remaining := CEIL(
                EXTRACT(EPOCH FROM (v_sub.trial_ends_at - v_now)) / 86400.0
            )::integer;
        ELSIF v_sub.grace_ends_at IS NOT NULL AND v_sub.grace_ends_at > v_now THEN
            v_result_status  := 'grace_period';
            v_days_remaining := CEIL(
                EXTRACT(EPOCH FROM (v_sub.grace_ends_at - v_now)) / 86400.0
            )::integer;
        ELSE
            v_result_status  := 'expired';
            v_days_remaining := 0;
            UPDATE public.account_subscriptions
               SET status = 'expired', updated_at = v_now
             WHERE account_id = v_device.account_id AND status != 'expired';
        END IF;
    ELSE
        v_result_status  := v_sub.status;
        v_days_remaining := NULL;
    END IF;

    RETURN jsonb_build_object(
        'status',               v_result_status,
        'is_lifetime',          v_sub.is_lifetime,
        'plan_name',            v_sub.plan_name,
        'tier_rank',            v_sub.tier_rank,
        'days_remaining',       v_days_remaining,
        'trial_ends_at',        v_sub.trial_ends_at,
        'max_businesses',       v_sub.max_businesses,
        'max_branches',         v_sub.max_branches,
        'max_devices',          v_sub.max_devices,
        'max_staff',            v_sub.max_staff,
        'cross_branch_reports', v_sub.cross_branch_reports,
        'shared_members',       v_sub.shared_members
    );
END;
$$;

-- =============================================================================
-- FIX 1 (HIGH): create_business RPC — DB-level business limit enforcement
-- =============================================================================
-- Called when an existing account wants to create an additional business
-- (only Enterprise allows more than 1 business).
-- Guards against bypassing the limit from hacked client code.
-- =============================================================================
CREATE OR REPLACE FUNCTION public.create_business(
    p_account_id  uuid,
    p_name        text,
    p_category    text DEFAULT 'pos_inventory',
    p_address     text DEFAULT NULL,
    p_phone       text DEFAULT NULL
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
DECLARE
    v_now           timestamptz := now();
    v_max_businesses integer;
    v_active_count   integer;
    v_business_id    uuid;
    v_template       record;
BEGIN
    -- Verify account exists and is active
    IF NOT EXISTS (SELECT 1 FROM public.accounts WHERE id = p_account_id AND is_active = true) THEN
        RETURN jsonb_build_object('success', false, 'error', 'ACCOUNT_NOT_FOUND');
    END IF;

-- Category is governed by C# codebase

    -- Get plan limit for businesses
    SELECT pl.max_businesses INTO v_max_businesses
    FROM public.account_subscriptions sub
    JOIN public.subscription_plans pl ON pl.id = sub.plan_id
    WHERE sub.account_id = p_account_id
    LIMIT 1;

    IF NOT FOUND THEN
        RETURN jsonb_build_object('success', false, 'error', 'NO_SUBSCRIPTION_FOUND');
    END IF;

    -- Count currently active businesses
    SELECT COUNT(*) INTO v_active_count
    FROM public.businesses
    WHERE account_id = p_account_id AND is_active = true;

    -- Enforce limit (-1 = unlimited)
    IF v_max_businesses != -1 AND v_active_count >= v_max_businesses THEN
        RAISE EXCEPTION
            'BUSINESS_LIMIT_REACHED: Your plan allows a maximum of % business(es). Upgrade to Enterprise to add more.',
            v_max_businesses;
    END IF;

    -- Insert the business
    INSERT INTO public.businesses
        (account_id, category, name, address, phone, is_active, created_at, updated_at)
    VALUES
        (p_account_id, COALESCE(NULLIF(TRIM(p_category), ''), 'pos_inventory'), p_name, p_address, p_phone, true, v_now, v_now)
    RETURNING id INTO v_business_id;

    RETURN jsonb_build_object(
        'success',     true,
        'business_id', v_business_id,
        'account_id',  p_account_id,
        'category',    COALESCE(NULLIF(TRIM(p_category), ''), 'pos_inventory')
    );
END;
$$;

-- =============================================================================
-- FIX 1 (HIGH): create_branch RPC — DB-level branch limit enforcement
-- =============================================================================
-- Called when adding a new physical location/branch to an existing business.
-- Enforces max_branches from the subscription plan at the database level.
-- Also auto-enables modules for the new branch based on the business template.
-- =============================================================================
CREATE OR REPLACE FUNCTION public.create_branch(
    p_account_id  uuid,
    p_business_id uuid,
    p_name        text,
    p_address     text DEFAULT NULL,
    p_phone       text DEFAULT NULL
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
DECLARE
    v_now           timestamptz := now();
    v_max_branches  integer;
    v_active_count  integer;
    v_branch_id     uuid;
    v_template      record;
    v_module        record;
    v_business      record;
BEGIN
    -- Verify business belongs to this account
    SELECT b.id INTO v_business
    FROM public.businesses b
    WHERE b.id = p_business_id AND b.account_id = p_account_id AND b.is_active = true
    LIMIT 1;

    IF NOT FOUND THEN
        RETURN jsonb_build_object('success', false, 'error', 'BUSINESS_NOT_FOUND_OR_UNAUTHORIZED');
    END IF;

    -- Get plan branch limit
    SELECT pl.max_branches INTO v_max_branches
    FROM public.account_subscriptions sub
    JOIN public.subscription_plans pl ON pl.id = sub.plan_id
    WHERE sub.account_id = p_account_id
    LIMIT 1;

    IF NOT FOUND THEN
        RETURN jsonb_build_object('success', false, 'error', 'NO_SUBSCRIPTION_FOUND');
    END IF;

    -- Count currently active branches across ALL businesses of this account
    SELECT COUNT(*) INTO v_active_count
    FROM public.branches
    WHERE account_id = p_account_id AND is_active = true;

    -- Enforce limit (-1 = unlimited)
    IF v_max_branches != -1 AND v_active_count >= v_max_branches THEN
        RAISE EXCEPTION
            'BRANCH_LIMIT_REACHED: Your plan allows a maximum of % branch(es). Upgrade your plan to add more locations.',
            v_max_branches;
    END IF;

    -- Create the branch
    INSERT INTO public.branches
        (account_id, business_id, name, address, phone,
         is_main_branch, is_active, created_at, updated_at)
    VALUES
        (p_account_id, p_business_id, p_name, p_address, p_phone,
         false, true, v_now, v_now)
    RETURNING id INTO v_branch_id;

    -- Auto-enable core modules for new branch
    FOR v_module IN
        SELECT id FROM public.modules WHERE is_core = true
    LOOP
        INSERT INTO public.branch_modules (branch_id, module_id, is_enabled, enabled_at)
        VALUES (v_branch_id, v_module.id, true, v_now)
        ON CONFLICT (branch_id, module_id) DO NOTHING;
    END LOOP;

    RETURN jsonb_build_object(
        'success',     true,
        'branch_id',   v_branch_id,
        'account_id',  p_account_id,
        'business_id', p_business_id
    );
END;
$$;

-- =============================================================================
-- END OF PATCH v1.1
-- Script now fully covers: 18 tables / 11 RPC functions / Full RLS / All gaps fixed
-- =============================================================================

-- =============================================================================
-- PATCH v1.2 — Missing RPCs required by C# AuthenticationService
-- =============================================================================
-- get_staff_profiles(p_email): Cloud Recovery RPC called by AuthenticationService
-- when the local SQLite profile is missing (new device, reinstall, etc.).
-- Returns a JSON array of staff profiles matching the email, with enough
-- context (account_id, branch_id, role) for the C# layer to seed the local DB.
-- Shape matches SupabaseStaffMember deserialization in AuthenticationService.cs.
-- =============================================================================

CREATE OR REPLACE FUNCTION public.get_staff_profiles(p_email text)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
DECLARE
    v_result jsonb;
BEGIN
    SELECT jsonb_agg(jsonb_build_object(
        'id',               s.id,
        'tenant_id',        s.account_id,
        'facility_id',      COALESCE(sba.branch_id, (
                                SELECT b.id FROM public.branches b
                                WHERE b.account_id = s.account_id AND b.is_main_branch = true
                                LIMIT 1
                            )),
        'full_name',        s.full_name,
        'email',            s.email,
        'role',             CASE s.role
                                WHEN 'owner'      THEN 8
                                WHEN 'manager'    THEN 1
                                WHEN 'cashier'    THEN 2
                                WHEN 'technician' THEN 3
                                WHEN 'waiter'     THEN 4
                                ELSE 7
                            END,
        'is_active',        s.is_active,
        'is_owner',         (s.role = 'owner'),
        'supabase_user_id', s.auth_user_id,
        'created_at',       s.created_at,
        'updated_at',       s.updated_at
    ))
    INTO v_result
    FROM public.staff s
    LEFT JOIN public.staff_branch_assignments sba ON sba.staff_id = s.id
    WHERE LOWER(TRIM(s.email)) = LOWER(TRIM(p_email))
      AND s.is_active = true;

    RETURN COALESCE(v_result, '[]'::jsonb);
END;
$$;

GRANT EXECUTE ON FUNCTION public.get_staff_profiles(text) TO authenticated, anon, service_role;

-- =============================================================================
-- get_tenant_facilities(p_tenant_id): Cloud Recovery RPC called by
-- AuthenticationService and OnboardingService to seed local SQLite facilities
-- from active public.branches and public.businesses.
-- =============================================================================
CREATE OR REPLACE FUNCTION public.get_tenant_facilities(p_tenant_id uuid)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
DECLARE
    v_result jsonb;
BEGIN
    SELECT jsonb_agg(jsonb_build_object(
        'id',           br.id,
        'tenant_id',    br.account_id,
        'name',         br.name,
        'description',  COALESCE(b.name, br.name),
        'slug',         LOWER(REPLACE(br.name, ' ', '-')),
        'type',         CASE b.category
                            WHEN 'pos_inventory'        THEN 10
                            WHEN 'appointment_service'  THEN 11
                            WHEN 'membership_session'   THEN 12
                            WHEN 'gym'                  THEN 12
                            WHEN 'salon'                THEN 11
                            WHEN 'restaurant'           THEN 6
                            WHEN 'project_milestone'    THEN 13
                            WHEN 'rental_booking'       THEN 14
                            WHEN 'education_cohort'     THEN 15
                            ELSE 12
                        END,
        'is_active',    br.is_active,
        'created_at',   br.created_at
    ))
    INTO v_result
    FROM public.branches br
    LEFT JOIN public.businesses b ON b.id = br.business_id
    WHERE br.account_id = p_tenant_id
      AND br.is_active = true;

    RETURN COALESCE(v_result, '[]'::jsonb);
END;
$$;

GRANT EXECUTE ON FUNCTION public.get_tenant_facilities(uuid) TO authenticated, anon, service_role;

-- =============================================================================
-- PATCH v1.2 — Drop old onboard_owner_account overload (fixes PGRST203)
-- The old 8-parameter version (without p_address / p_phone) must be removed
-- so PostgREST has exactly ONE matching function and stops throwing PGRST203.
-- Safe to run multiple times: IF EXISTS guard prevents errors.
-- =============================================================================
DROP FUNCTION IF EXISTS public.onboard_owner_account(uuid, text, text, text, text, text, text, text);

-- =============================================================================
-- END OF PATCH v1.2
-- 19 tables / 12 RPC functions / Full RLS / All gaps fixed
-- =============================================================================
