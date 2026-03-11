-- MASTER SUPABASE CLEANUP SCRIPT (RUN THIS ONCE)

-- 1. DROP ALL OLD FUNCTION OVERLOADS
-- This completely clears out any conflicting overlapping functions.
DROP FUNCTION IF EXISTS public.fn_provision_facility(uuid, uuid, text, integer, text);
DROP FUNCTION IF EXISTS public.fn_provision_facility(uuid, uuid, integer, text);
DROP FUNCTION IF EXISTS public.fn_provision_facility(uuid, uuid, integer);
DROP FUNCTION IF EXISTS public.get_staff_profiles(text);
-- Keep the latest onboard_new_tenant but drop any weird overloads
DROP FUNCTION IF EXISTS public.onboard_new_tenant(uuid, text, text, text, text, text, integer);


-- 2. DEDUPLICATE FACILITIES
-- Keep only the oldest facility for each (tenant, type) combination and delete the rest
DELETE FROM public.staff_members
WHERE facility_id IN (
    SELECT id FROM public.facilities
    WHERE is_deleted = false
    AND id NOT IN (
        SELECT MIN(id)
        FROM public.facilities
        WHERE is_deleted = false
        GROUP BY tenant_id, type
    )
);

DELETE FROM public.facilities
WHERE id NOT IN (
    SELECT MIN(id)
    FROM public.facilities
    WHERE is_deleted = false
    GROUP BY tenant_id, type
);

-- Deduplicate staff members
DELETE FROM public.staff_members
WHERE id NOT IN (
    SELECT MIN(id)
    FROM public.staff_members
    WHERE is_deleted = false
    GROUP BY tenant_id, facility_id, email
);


-- 3. ENFORCE IRONCLAD UNIQUE CONSTRAINTS
-- Prevent the application or any script from ever creating duplicates again
DO $$ 
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_facilities_tenant_type') THEN
    ALTER TABLE public.facilities ADD CONSTRAINT uq_facilities_tenant_type UNIQUE (tenant_id, type);
  END IF;
  
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_staff_tenant_facility_email') THEN
    ALTER TABLE public.staff_members ADD CONSTRAINT uq_staff_tenant_facility_email UNIQUE (tenant_id, facility_id, email);
  END IF;
END $$;


-- 4. RECREATE PROPER FUNCTIONS

-- 4.1 get_staff_profiles (NOW PROPERLY RETURNING THE EMAIL COLUMN)
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
    WHERE s.email = p_email;
END;
$$;


-- 4.2 fn_provision_facility (NOW USING THE p_owner_email PARAMETER CORRECTLY)
CREATE OR REPLACE FUNCTION public.fn_provision_facility(
    p_tenant_id UUID,
    p_owner_id UUID,
    p_owner_email TEXT,
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
    -- Idempotency Check (by TYPE) - Now also backed by a real UNIQUE constraint
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

    -- Create the Owner's staff_members record for this new facility
    INSERT INTO public.staff_members (
        id, tenant_id, facility_id, full_name, email, role, is_active, is_owner, created_at, updated_at
    )
    SELECT 
        gen_random_uuid(),
        p_tenant_id,
        v_facility_id,
        COALESCE(p.full_name, 'Owner'),
        COALESCE(p_owner_email, ''),
        8, -- StaffRole.Owner
        TRUE,
        TRUE,
        NOW(),
        NOW()
    FROM public.profiles p
    WHERE p.id = p_owner_id
      AND NOT EXISTS (
          SELECT 1 FROM public.staff_members s
          WHERE s.tenant_id = p_tenant_id
            AND s.facility_id = v_facility_id
            AND s.email = p_owner_email
      );

    RETURN v_facility_id;
END;
$$;
