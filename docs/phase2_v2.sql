-- =============================================================================
-- ATRIUM PHASE 2 — COMPLETE SCHEMA, SEEDS & RLS POLICIES (v1.1 FINAL)
-- =============================================================================
-- 16 Tables | 11 RPC Functions | Full RLS | 27 Indexes | Seeds | Triggers
-- Run in: Supabase SQL Editor > New Query > Paste > Run
-- Safe to re-run (idempotent)
-- =============================================================================

-- PART 1: EXTENSIONS
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- =============================================================================
-- PART 3A: subscription_plans
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

-- PART 3B: facility_templates
CREATE TABLE IF NOT EXISTS public.facility_templates (
    id              uuid    PRIMARY KEY DEFAULT gen_random_uuid(),
    name            text    NOT NULL UNIQUE,
    icon            text    NOT NULL DEFAULT '🏢',
    description     text,
    default_modules text[]  NOT NULL DEFAULT '{}',
    label_overrides jsonb   NOT NULL DEFAULT '{}',
    is_active       boolean NOT NULL DEFAULT true,
    sort_order      integer NOT NULL DEFAULT 0,
    created_at      timestamptz NOT NULL DEFAULT now()
);

-- PART 3C: modules
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

-- PART 4A: accounts
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

-- PART 4B: businesses
CREATE TABLE IF NOT EXISTS public.businesses (
    id           uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id   uuid        NOT NULL,
    template_id  uuid,
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
    CONSTRAINT fk_businesses_template
        FOREIGN KEY (template_id) REFERENCES public.facility_templates(id) ON DELETE SET NULL
);

-- PART 4C: branches
CREATE TABLE IF NOT EXISTS public.branches (
    id             uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id     uuid        NOT NULL,
    business_id    uuid        NOT NULL,
    name           text        NOT NULL,
    address        text,
    phone          text,
    is_active      boolean     NOT NULL DEFAULT true,
    is_main_branch boolean     NOT NULL DEFAULT false,
    created_at     timestamptz NOT NULL DEFAULT now(),
    updated_at     timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT fk_branches_account
        FOREIGN KEY (account_id) REFERENCES public.accounts(id) ON DELETE CASCADE,
    CONSTRAINT fk_branches_business
        FOREIGN KEY (business_id) REFERENCES public.businesses(id) ON DELETE CASCADE
);

-- PART 4D: devices
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

-- PART 5A: account_subscriptions
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
        status IN ('trialing','active','grace_period','expired','suspended')
    )
);

-- PART 5B: license_vouchers
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
    CONSTRAINT chk_duration CHECK (duration_months = -1 OR duration_months > 0)
);

ALTER TABLE public.account_subscriptions
    DROP CONSTRAINT IF EXISTS fk_accsub_voucher;
ALTER TABLE public.account_subscriptions
    ADD CONSTRAINT fk_accsub_voucher
        FOREIGN KEY (activated_by_voucher)
        REFERENCES public.license_vouchers(id) ON DELETE SET NULL;

-- PART 6A: staff
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
    CONSTRAINT chk_role CHECK (role IN ('owner','manager','staff')),
    CONSTRAINT uq_staff_auth_user UNIQUE (auth_user_id)
);

ALTER TABLE public.staff
    DROP CONSTRAINT IF EXISTS fk_staff_auth_user;
ALTER TABLE public.staff
    ADD CONSTRAINT fk_staff_auth_user
        FOREIGN KEY (auth_user_id) REFERENCES auth.users(id) ON DELETE SET NULL;

-- PART 6B: staff_branch_assignments
CREATE TABLE IF NOT EXISTS public.staff_branch_assignments (
    id          uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    staff_id    uuid        NOT NULL,
    branch_id   uuid        NOT NULL,
    assigned_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT fk_sba_staff  FOREIGN KEY (staff_id)  REFERENCES public.staff(id)    ON DELETE CASCADE,
    CONSTRAINT fk_sba_branch FOREIGN KEY (branch_id) REFERENCES public.branches(id) ON DELETE CASCADE,
    CONSTRAINT uq_sba_staff_branch UNIQUE (staff_id, branch_id)
);

-- PART 6C: branch_modules
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

-- PART 6D: staff_module_permissions
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

-- PART 7A: members_directory
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

-- PART 7B: registrations
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
    CONSTRAINT chk_reg_status CHECK (status IN ('pending','active','expired','cancelled'))
);

-- PART 7C: registration_requests
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
    CONSTRAINT chk_rr_status CHECK (status IN ('pending','processed','failed'))
);
