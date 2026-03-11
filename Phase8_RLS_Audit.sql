-- SECURITY AUDIT: Facility Schedules RLS
-- This script verifies and enforces isolation for the new Phase 7 tables.

-- 1. Enable RLS
ALTER TABLE IF EXISTS facility_schedules ENABLE ROW LEVEL SECURITY;

-- 2. Clean up existing policies (if any)
DROP POLICY IF EXISTS "Facilities can see their own schedules" ON facility_schedules;
DROP POLICY IF EXISTS "Facility staff can manage schedules" ON facility_schedules;

-- 3. Create Tenant/Facility Isolation Policy
-- Logic: 
-- - Staff can read/write if their JWT contains the matching tenant_id 
-- - AND the record belongs to their current facility_id (or they are a tenant owner)

CREATE POLICY "Users can manage their own facility schedules"
ON facility_schedules
FOR ALL
TO authenticated
USING (
    ((auth.jwt() -> 'user_metadata') ->> 'tenant_id')::uuid = tenant_id 
    AND (
        ((auth.jwt() -> 'user_metadata') ->> 'facility_id')::uuid = facility_id
        OR ((auth.jwt() -> 'user_metadata') ->> 'role' = 'Owner')
    )
)
WITH CHECK (
    ((auth.jwt() -> 'user_metadata') ->> 'tenant_id')::uuid = tenant_id 
    AND (
        ((auth.jwt() -> 'user_metadata') ->> 'facility_id')::uuid = facility_id
        OR ((auth.jwt() -> 'user_metadata') ->> 'role' = 'Owner')
    )
);

-- 4. Verification Check
SELECT tablename, rowsecurity 
FROM pg_tables 
WHERE tablename = 'facility_schedules';

SELECT policyname, cmd, qual, with_check 
FROM pg_policies 
WHERE tablename = 'facility_schedules';
