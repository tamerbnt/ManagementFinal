-- ==============================================================================
-- SUPABASE UNIFIED HEALING SCRIPT: Schema Integrity & Profile Restoration
-- ==============================================================================
-- This script fixes critical data gaps in Supabase, restores identity automation, 
-- and handles multi-facility provisioning safely.
-- Run this in your Supabase SQL Editor.

-- 1. HARDEN CORE TABLES (Schema Integrity)
DO $$ 
BEGIN 
    -- Ensure tenant_devices has a unique constraint on hardware_id for ON CONFLICT logic
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'tenant_devices_hardware_id_key') THEN
        ALTER TABLE public.tenant_devices ADD CONSTRAINT tenant_devices_hardware_id_key UNIQUE (hardware_id);
    END IF;

    -- NEW: Ensure staff_members has a constraint to reject placeholder emails from infrastructure fallbacks
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'check_email_valid') THEN
        ALTER TABLE public.staff_members ADD CONSTRAINT check_email_valid CHECK (email NOT LIKE 'unknown@%');
    END IF;
END $$;

-- 1.5 DATA RECOVERY (Mass Repair for existing NULLs)
DO $$
BEGIN
    -- Fix existing NULLs in profiles (Link back to their business)
    UPDATE public.profiles p
    SET tenant_id = t.id
    FROM public.tenants t
    WHERE p.tenant_id IS NULL AND t.owner_id = p.id;

    -- Fix existing NULLs in tenant_devices (Link back to their business)
    UPDATE public.tenant_devices d
    SET tenant_id = l.tenant_id
    FROM public.licenses l
    WHERE d.tenant_id IS NULL AND d.license_id = l.id AND l.tenant_id IS NOT NULL;

    -- Fix owner profiles (Ensure role 8)
    UPDATE public.profiles p
    SET role = 8
    FROM public.tenants t
    WHERE t.owner_id = p.id AND (p.role IS NULL OR p.role != 8);

    -- NEW: Sync auth metadata for all existing owners
    -- This ensures RLS works correctly for all existing users immediately
    UPDATE auth.users u
    SET raw_user_meta_data = u.raw_user_meta_data || 
        jsonb_build_object('tenant_id', t.id, 'role', '8')
    FROM public.tenants t
    WHERE u.id = t.owner_id;

    -- DATABASE CLEANUP: Delete orphan facilities created under empty tenant_id
    -- These were created by a deserialization bug (now fixed) that zeroed out all GUIDs
    DELETE FROM public.staff_members 
    WHERE tenant_id = '00000000-0000-0000-0000-000000000000';

    DELETE FROM public.facilities 
    WHERE tenant_id = '00000000-0000-0000-0000-000000000000';

    -- NEW: Fix existing facilities with incorrect type 0 (General)
    -- Map them to the correct FacilityType enum value (Gym=1, Salon=5, Restaurant=6)
    -- based on the license key used to register the tenant.
    UPDATE public.facilities f
    SET 
        type = CASE 
            WHEN l.license_key ILIKE '%SALON%' THEN 5
            WHEN l.license_key ILIKE '%REST%' THEN 6
            WHEN l.license_key ILIKE '%GYM%' THEN 1
            ELSE 1 -- Default to Gym if unknown
        END,
        name = COALESCE(NULLIF(t.name, ''), 'Main Facility')
    FROM public.tenants t
    JOIN public.licenses l ON l.tenant_id = t.id
    WHERE f.tenant_id = t.id 
      AND f.type = 0; -- Only fix the ones created with the bug

END $$;

-- 2. DROP OLD FUNCTIONS (Clean Slate)

-- 3. IDENTITY AUTOMATION (auth.users -> profiles)
-- This ensures every GoTrue user gets a corresponding public record.
CREATE OR REPLACE FUNCTION public.handle_new_user_setup()
RETURNS TRIGGER AS $$
BEGIN
    BEGIN
        INSERT INTO public.profiles (id, full_name, role, email)
        VALUES (
            NEW.id, 
            COALESCE(NEW.raw_user_meta_data->>'full_name', NEW.email), 
            8, -- Owner Role (StaffRole.Owner)
            NEW.email
        )
        ON CONFLICT (id) DO NOTHING;
    EXCEPTION WHEN OTHERS THEN
        -- Safely ignore profile errors to allow user registration to proceed
        RAISE WARNING 'Profile creation failed for user %: %', NEW.id, SQLERRM;
    END;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

-- Trigger: create profile on user signup
DROP TRIGGER IF EXISTS on_auth_user_created ON auth.users;
CREATE TRIGGER on_auth_user_created
  AFTER INSERT ON auth.users
  FOR EACH ROW EXECUTE FUNCTION public.handle_new_user_setup();

-- 3. HARDENED LICENSE VERIFICATION (Registers device hardware)
-- Resolves the blank 'tenant_devices' table issue.
CREATE OR REPLACE FUNCTION public.verify_license_key(
    p_lookup_key TEXT,
    p_hardware_id TEXT,
    p_label TEXT
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
DECLARE
    v_license_id UUID;
    v_tenant_id UUID;
    v_is_active BOOLEAN;
BEGIN
    -- 1. Check if license exists and is active
    SELECT id, tenant_id, is_active INTO v_license_id, v_tenant_id, v_is_active
    FROM public.licenses
    WHERE license_key = UPPER(p_lookup_key)
    LIMIT 1;

    IF v_license_id IS NULL THEN
        RETURN jsonb_build_object('valid', false, 'message', 'License key not found');
    END IF;

    IF NOT v_is_active THEN
        RETURN jsonb_build_object('valid', false, 'message', 'License is inactive');
    END IF;

    -- 2. Register/Update Device Hardware Binding (Idempotent)
    INSERT INTO public.tenant_devices (id, tenant_id, license_id, hardware_id, label, registered_at)
    VALUES (gen_random_uuid(), v_tenant_id, v_license_id, p_hardware_id, p_label, NOW())
    ON CONFLICT (hardware_id) DO UPDATE SET 
        tenant_id = COALESCE(public.tenant_devices.tenant_id, EXCLUDED.tenant_id),
        label = EXCLUDED.label,
        registered_at = NOW();

    RETURN jsonb_build_object(
        'valid', true, 
        'message', 'License validated and hardware registered',
        'license_id', v_license_id,
        'tenant_id', v_tenant_id
    );
END;
$$;

-- 3.5 STARTUP VERIFICATION (Safe discovery for unauthenticated clients)
CREATE OR REPLACE FUNCTION public.check_device_activation(p_hardware_id TEXT)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
DECLARE
    v_tenant_id UUID;
    v_license_id UUID;
BEGIN
    SELECT d.tenant_id, d.license_id INTO v_tenant_id, v_license_id
    FROM public.tenant_devices d
    JOIN public.licenses l ON d.license_id = l.id
    WHERE d.hardware_id = p_hardware_id AND l.is_active = true
    LIMIT 1;

    IF v_tenant_id IS NOT NULL THEN
        RETURN jsonb_build_object(
            'active', true,
            'tenant_id', v_tenant_id,
            'license_id', v_license_id
        );
    ELSE
        RETURN jsonb_build_object('active', false);
    END IF;
END;
$$;

-- 4. UNIFIED PROVISIONING (Facility Cloner)
CREATE OR REPLACE FUNCTION public.fn_provision_facility(
    p_tenant_id UUID,
    p_owner_id UUID,
    p_owner_email TEXT,
    p_owner_name TEXT, -- ADDED: Ensures name is correctly synced
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
    -- Idempotency Check (by TYPE, not name — prevents duplicates from different name calls)
    SELECT id INTO v_facility_id 
    FROM public.facilities 
    WHERE tenant_id = p_tenant_id 
      AND type = p_facility_type
      AND is_deleted = FALSE
    LIMIT 1;

    IF v_facility_id IS NOT NULL THEN
        RETURN v_facility_id;
    END IF;

    v_facility_id := gen_random_uuid();

    -- Insert new facility
    INSERT INTO public.facilities (
        id,
        tenant_id,
        owner_id,
        name,
        type,
        is_active,
        is_synced,
        is_deleted,
        facility_id,
        updated_at,
        description
    ) VALUES (
        v_facility_id,
        p_tenant_id,
        p_owner_id,
        COALESCE(p_facility_name, 'Main Facility'),
        p_facility_type,
        TRUE,
        TRUE,
        FALSE,
        v_facility_id,
        NOW(),
        ''
    );

    -- 4.4 IDENTITY HEALING (Sync name and tenant_id to profiles)
    UPDATE public.profiles
    SET 
        full_name = COALESCE(p_owner_name, full_name),
        email = LOWER(p_owner_email), -- Lowercase here
        updated_at = NOW()
    WHERE id = p_owner_id;

    -- 4.5 Ensure Owner staff record exists (Linked by ID for Gym, or unique for others)
    INSERT INTO public.staff_members (
        id, tenant_id, facility_id, full_name, email, role, is_active, is_owner, created_at, updated_at
    )
    VALUES (
        CASE WHEN p_facility_type = 1 THEN p_owner_id ELSE gen_random_uuid() END,
        p_tenant_id,
        v_facility_id,
        p_owner_name,
        LOWER(p_owner_email), -- Lowercase here
        8, -- Owner
        TRUE,
        TRUE,
        NOW(),
        NOW()
    )
    ON CONFLICT (tenant_id, facility_id, email) 
    DO UPDATE SET 
        role = EXCLUDED.role,
        is_owner = TRUE,
        updated_at = NOW();

    RETURN v_facility_id;
END;
$$;

-- 4.6 FACILITY DISCOVERY (Secure fetch — bypasses RLS for login screen population)
-- Prevents fallback to static Gym/Salon/Restaurant defaults with wrong type integers
CREATE OR REPLACE FUNCTION public.get_tenant_facilities(p_tenant_id UUID)
RETURNS TABLE (
    id UUID,
    name TEXT,
    type INTEGER,
    is_active BOOLEAN
)
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
BEGIN
    RETURN QUERY
    SELECT f.id, f.name, f.type, f.is_active
    FROM public.facilities f
    WHERE f.tenant_id = p_tenant_id
      AND f.is_deleted = FALSE
      AND f.is_active = TRUE;
END;
$$;

-- 4.5 LOGIN DISCOVERY (Securely retrieve context without RLS deadlock)
CREATE OR REPLACE FUNCTION public.get_staff_profiles(p_email TEXT)
RETURNS TABLE (
    id UUID,
    tenant_id UUID,
    facility_id UUID,
    full_name TEXT,
    email TEXT,
    role INTEGER,
    is_active BOOLEAN,
    is_owner BOOLEAN
) 
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
BEGIN
    RETURN QUERY
    SELECT s.id, s.tenant_id, s.facility_id, s.full_name, s.email, s.role, s.is_active, s.is_owner
    FROM public.staff_members s
    WHERE LOWER(s.email) = LOWER(p_email);
END;
$$;

-- 5. MASTER ONBOARDING (Business Registration)
-- Resolves NULL license_key, missing license link, and blank staff_members.
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
    v_facility_id UUID;
BEGIN
    -- 1. Create/Recover Tenant
    INSERT INTO public.tenants (id, name, slug, owner_id, license_key)
    VALUES (gen_random_uuid(), p_tenant_name, p_tenant_slug, p_owner_id, UPPER(p_license_key))
    ON CONFLICT (owner_id) DO UPDATE SET 
        name = EXCLUDED.name,
        license_key = EXCLUDED.license_key
    RETURNING id INTO v_tenant_id;

    -- 2. Link License & Devices to Tenant
    UPDATE public.licenses SET tenant_id = v_tenant_id WHERE license_key = UPPER(p_license_key);
    UPDATE public.tenant_devices SET tenant_id = v_tenant_id WHERE license_id = (SELECT id FROM public.licenses WHERE license_key = UPPER(p_license_key));

    -- 3. IDENTITY HEALING (Sync name and lowercase email to profiles)
    UPDATE public.profiles
    SET 
        full_name = COALESCE(p_owner_name, full_name),
        email = LOWER(p_email),
        updated_at = NOW()
    WHERE id = p_owner_id;

    -- 3. Provision the Basic Facility (Now passes owner name correctly)
    v_facility_id := public.fn_provision_facility(v_tenant_id, p_owner_id, p_email, p_owner_name, p_facility_type);

    -- 4. Establish Owner Profile in Staff Members (Resolves "Guest" Profile Issue)
    -- FIX: Ensure email is never empty to prevent C# .Value crash
    IF p_email IS NULL OR p_email = '' THEN
        RAISE EXCEPTION 'Cannot onboard tenant with empty email.';
    END IF;

    INSERT INTO public.staff_members (
        id, 
        tenant_id, 
        facility_id, 
        full_name, 
        email, 
        role, 
        is_active, 
        is_owner,
        created_at,
        updated_at
    )
    VALUES (
        p_owner_id, -- Keep ID sync with auth.user/profile for discovery
        v_tenant_id, 
        v_facility_id, 
        COALESCE(p_owner_name, 'Owner'), 
        p_email, 
        8, -- Owner Role (StaffRole.Owner)
        TRUE, 
        TRUE,
        NOW(),
        NOW()
    )
    ON CONFLICT (id) DO UPDATE SET 
        tenant_id = v_tenant_id,
        facility_id = EXCLUDED.facility_id,
        full_name = EXCLUDED.full_name,
        email = EXCLUDED.email,
        role = EXCLUDED.role,
        is_owner = TRUE,
        updated_at = NOW();

    -- 5. IDENTITY HEALING (Sync name and tenant_id to profiles)
    UPDATE public.profiles
    SET full_name = p_owner_name,
        tenant_id = v_tenant_id,
        role = 8
    WHERE id = p_owner_id;

    -- 6. AUTH METADATA SYNC (Inject tenant_id into GoTrue JWT claims)
    -- This resolves the RLS Deadlock during future logins
    UPDATE auth.users 
    SET raw_user_meta_data = raw_user_meta_data || 
        jsonb_build_object('tenant_id', v_tenant_id, 'role', '8')
    WHERE id = p_owner_id;

    RETURN v_tenant_id;
END;
$$;

-- 6. FORCE SCHEMA RELOAD
NOTIFY pgrst, 'reload config';

-- 7. VERIFICATION LOG
DO $$ 
BEGIN 
    RAISE NOTICE 'Supabase Healing Script Applied successfully.';
END $$;
