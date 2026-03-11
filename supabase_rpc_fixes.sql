-- ==============================================================================
-- SUPABASE RPC FIXES: fn_provision_facility
-- ==============================================================================
-- The C# application and the `onboard_new_tenant` function expect these signatures. 
-- Please run this script in your Supabase SQL Editor.

-- Drop existing functions to allow changing return types
DROP FUNCTION IF EXISTS public.fn_provision_facility(uuid, uuid, integer, text);
DROP FUNCTION IF EXISTS public.fn_provision_facility(uuid, uuid, integer);

-- 1. Create the 4-argument version (Used directly by C# OnboardingService)
CREATE OR REPLACE FUNCTION public.fn_provision_facility(
    p_tenant_id UUID,
    p_owner_id UUID,
    p_facility_type INTEGER,
    p_facility_name TEXT
)
RETURNS UUID
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
DECLARE
    v_facility_id UUID;
BEGIN
    -- Insert the new facility
    INSERT INTO public.facilities (
        id,
        tenant_id,
        name,
        type,
        description,
        is_active
    ) VALUES (
        gen_random_uuid(),
        p_tenant_id,
        COALESCE(p_facility_name, 'Main Facility'),
        p_facility_type,
        '',
        true
    ) RETURNING id INTO v_facility_id;

    -- Return the newly created facility id
    RETURN v_facility_id;
END;
$$;


-- 2. Create the 3-argument version (Used internally by onboard_new_tenant)
CREATE OR REPLACE FUNCTION public.fn_provision_facility(
    p_tenant_id UUID,
    p_owner_id UUID,
    p_facility_type INTEGER
)
RETURNS UUID
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
DECLARE
    v_facility_name TEXT;
BEGIN
    -- Determine default name based on type (0=Gym, 1=Salon, 2=Restaurant, etc.)
    IF p_facility_type = 0 THEN
        v_facility_name := 'Main Gym';
    ELSIF p_facility_type = 1 THEN
        v_facility_name := 'Main Salon';
    ELSIF p_facility_type = 2 THEN
        v_facility_name := 'Main Restaurant';
    ELSE
        v_facility_name := 'Main Facility';
    END IF;

    -- Call the 4-argument version to avoid duplicate logic
    RETURN public.fn_provision_facility(
        p_tenant_id,
        p_owner_id,
        p_facility_type,
        v_facility_name
    );
END;
$$;
