-- ==============================================================================
-- FINAL DEFINITIVE SUPER-FIX: SCHEMA HEALING VERSION 2
-- ==============================================================================
-- This script hardens BOTH the tenants and facilities tables using your 
-- specific schema dump (Ref: facility_id NOT NULL and tenants column mismatch).

-- -------------------------------------------------------------------------
-- 1. HEAL TENANTS TABLE (Adding missing Biz-Logic columns)
-- -------------------------------------------------------------------------

DO $$ 
BEGIN 
    -- Add missing columns to tenants
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='tenants' AND column_name='owner_id') THEN
        ALTER TABLE public.tenants ADD COLUMN owner_id UUID;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='tenants' AND column_name='license_key') THEN
        ALTER TABLE public.tenants ADD COLUMN license_key TEXT;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='tenants' AND column_name='slug') THEN
        ALTER TABLE public.tenants ADD COLUMN slug TEXT;
    END IF;
END $$;

-- -------------------------------------------------------------------------
-- 2. HEAL FACILITIES TABLE (Fixing the Redundant facility_id & NOT NULLs)
-- -------------------------------------------------------------------------

DO $$ 
BEGIN 
    -- Ensure columns exist
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='facilities' AND column_name='description') THEN
        ALTER TABLE public.facilities ADD COLUMN description TEXT DEFAULT '';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='facilities' AND column_name='is_active') THEN
        ALTER TABLE public.facilities ADD COLUMN is_active BOOLEAN DEFAULT TRUE;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='facilities' AND column_name='owner_id') THEN
        ALTER TABLE public.facilities ADD COLUMN owner_id UUID;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='facilities' AND column_name='updated_at') THEN
        ALTER TABLE public.facilities ADD COLUMN updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW();
    END IF;

    -- CRITICAL FIX: Add default to the problematic facility_id column found in diagnostic
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='facilities' AND column_name='facility_id') THEN
        -- We make it nullable or give it a default to stop the crash.
        -- Since it's a facility table, facility_id should just be the row ID.
        ALTER TABLE public.facilities ALTER COLUMN facility_id DROP NOT NULL;
    END IF;

    -- Add default values to prevent any future NULL violations
    ALTER TABLE public.facilities ALTER COLUMN description SET DEFAULT '';
    ALTER TABLE public.facilities ALTER COLUMN is_active SET DEFAULT TRUE;
    ALTER TABLE public.facilities ALTER COLUMN updated_at SET DEFAULT NOW();

    -- Ensure NOT NULL columns are safe for existing rows
    UPDATE public.facilities SET description = '' WHERE description IS NULL;
    UPDATE public.facilities SET is_active = TRUE WHERE is_active IS NULL;

END $$;

-- -------------------------------------------------------------------------
-- 3. REDEFINE PROVISIONING LOGIC (Bulletproof Functions)
-- -------------------------------------------------------------------------

-- Drop old functions to ensure clean slate
DROP FUNCTION IF EXISTS public.fn_provision_facility(uuid, uuid, integer, text);
DROP FUNCTION IF EXISTS public.fn_provision_facility(uuid, uuid, integer);
DROP FUNCTION IF EXISTS public.onboard_new_tenant(uuid, text, text, text, text, text, integer);

-- A. Resilient Primary Provisioning Function
CREATE OR REPLACE FUNCTION public.fn_provision_facility(
    p_tenant_id UUID,
    p_owner_id UUID,
    p_facility_type INTEGER,
    p_facility_name TEXT DEFAULT 'Main Facility'
)
RETURNS UUID
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
DECLARE
    v_facility_id UUID;
BEGIN
    v_facility_id := gen_random_uuid();

    -- Check if it already exists (Idempotency)
    -- We'll check by tenant and name to be safe
    IF EXISTS (SELECT 1 FROM public.facilities WHERE tenant_id = p_tenant_id AND name = p_facility_name) THEN
        SELECT id INTO v_facility_id FROM public.facilities 
        WHERE tenant_id = p_tenant_id AND name = p_facility_name LIMIT 1;
        RETURN v_facility_id;
    END IF;

    -- Insert using the EXACT columns discovered in diagnostic
    -- We include facility_id = v_facility_id because your schema requires it!
    INSERT INTO public.facilities (
        id,
        tenant_id,
        owner_id,
        name,
        type,
        description,
        is_active,
        facility_id,
        is_synced,
        is_deleted
    ) VALUES (
        v_facility_id,
        p_tenant_id,
        p_owner_id,
        COALESCE(p_facility_name, 'Main Facility'),
        p_facility_type,
        '',
        TRUE,
        v_facility_id, -- Maps to the redundant NOT NULL column
        TRUE,
        FALSE
    );

    RETURN v_facility_id;
END;
$$;

-- B. Resilience Overload (3-arg)
CREATE OR REPLACE FUNCTION public.fn_provision_facility(
    p_tenant_id UUID,
    p_owner_id UUID,
    p_facility_type INTEGER
)
RETURNS UUID
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
BEGIN
    RETURN public.fn_provision_facility(p_tenant_id, p_owner_id, p_facility_type, NULL);
END;
$$;

-- C. Complete Onboarding Master Function
CREATE OR REPLACE FUNCTION public.onboard_new_tenant(
    p_owner_id UUID,
    p_owner_name TEXT,
    p_email TEXT,
    p_license_key TEXT,
    p_tenant_name TEXT,
    p_tenant_slug TEXT,
    p_facility_type INTEGER
)
RETURNS UUID
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
DECLARE
    v_tenant_id UUID;
BEGIN
    -- 1. Upsert the Tenant
    -- We only use columns guaranteed to exist now (including slug/owner_id we just added)
    INSERT INTO public.tenants (id, name, slug, owner_id)
    VALUES (gen_random_uuid(), p_tenant_name, p_tenant_slug, p_owner_id)
    ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name
    RETURNING id INTO v_tenant_id;

    -- 2. Provision the Basic Facility
    PERFORM public.fn_provision_facility(v_tenant_id, p_owner_id, p_facility_type);

    RETURN v_tenant_id;
END;
$$;

-- D. Force Schema Cache Reload
NOTIFY pgrst, 'reload config';
