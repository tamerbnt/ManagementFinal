-- Phase 11: Master RLS Policy Definition
-- This script enforces strict Tenant and Facility isolation for all core tables.
-- It relies on the 'user_metadata' in the JWT, which is populated by the C# client.

-- -------------------------------------------------------------------------
-- HELPER: Ensure the tables have RLS enabled
-- -------------------------------------------------------------------------

ALTER TABLE IF EXISTS products ENABLE ROW LEVEL SECURITY;
ALTER TABLE IF EXISTS members ENABLE ROW LEVEL SECURITY;
ALTER TABLE IF EXISTS sales ENABLE ROW LEVEL SECURITY;
ALTER TABLE IF EXISTS sale_items ENABLE ROW LEVEL SECURITY;
ALTER TABLE IF EXISTS membership_plans ENABLE ROW LEVEL SECURITY;
ALTER TABLE IF EXISTS staff_members ENABLE ROW LEVEL SECURITY;
ALTER TABLE IF EXISTS payroll_entries ENABLE ROW LEVEL SECURITY;
ALTER TABLE IF EXISTS transactions ENABLE ROW LEVEL SECURITY;
ALTER TABLE IF EXISTS registrations ENABLE ROW LEVEL SECURITY;
ALTER TABLE IF EXISTS appointments ENABLE ROW LEVEL SECURITY;
ALTER TABLE IF EXISTS reservations ENABLE ROW LEVEL SECURITY;
ALTER TABLE IF EXISTS events ENABLE ROW LEVEL SECURITY;
ALTER TABLE IF EXISTS facility_zones ENABLE ROW LEVEL SECURITY;

-- -------------------------------------------------------------------------
-- PRODUCTS
-- -------------------------------------------------------------------------
DROP POLICY IF EXISTS "Tenant Staff Access Products" ON products;
CREATE POLICY "Tenant Staff Access Products" ON products
FOR ALL
TO authenticated
USING (
    ((auth.jwt() -> 'user_metadata') ->> 'tenant_id')::uuid = tenant_id 
    AND 
    ((auth.jwt() -> 'user_metadata') ->> 'facility_id')::uuid = facility_id
)
WITH CHECK (
    ((auth.jwt() -> 'user_metadata') ->> 'tenant_id')::uuid = tenant_id 
    AND 
    ((auth.jwt() -> 'user_metadata') ->> 'facility_id')::uuid = facility_id
);

-- -------------------------------------------------------------------------
-- MEMBERS
-- -------------------------------------------------------------------------
DROP POLICY IF EXISTS "Tenant Staff Access Members" ON members;
CREATE POLICY "Tenant Staff Access Members" ON members
FOR ALL
TO authenticated
USING (
    ((auth.jwt() -> 'user_metadata') ->> 'tenant_id')::uuid = tenant_id 
    AND 
    (
        ((auth.jwt() -> 'user_metadata') ->> 'facility_id')::uuid = facility_id
        OR facility_id IS NULL -- Global members (rare but possible)
    )
)
WITH CHECK (
    ((auth.jwt() -> 'user_metadata') ->> 'tenant_id')::uuid = tenant_id 
);

-- -------------------------------------------------------------------------
-- SALES & ITEMS
-- -------------------------------------------------------------------------
DROP POLICY IF EXISTS "Tenant Staff Access Sales" ON sales;
CREATE POLICY "Tenant Staff Access Sales" ON sales
FOR ALL TO authenticated
USING ( ((auth.jwt() -> 'user_metadata') ->> 'tenant_id')::uuid = tenant_id )
WITH CHECK ( ((auth.jwt() -> 'user_metadata') ->> 'tenant_id')::uuid = tenant_id );

DROP POLICY IF EXISTS "Tenant Staff Access SaleItems" ON sale_items;
CREATE POLICY "Tenant Staff Access SaleItems" ON sale_items
FOR ALL TO authenticated
USING ( ((auth.jwt() -> 'user_metadata') ->> 'tenant_id')::uuid = tenant_id )
WITH CHECK ( ((auth.jwt() -> 'user_metadata') ->> 'tenant_id')::uuid = tenant_id );

-- -------------------------------------------------------------------------
-- MEMBERSHIP PLANS
-- -------------------------------------------------------------------------
DROP POLICY IF EXISTS "Tenant Staff Access Plans" ON membership_plans;
CREATE POLICY "Tenant Staff Access Plans" ON membership_plans
FOR ALL TO authenticated
USING ( ((auth.jwt() -> 'user_metadata') ->> 'tenant_id')::uuid = tenant_id )
WITH CHECK ( ((auth.jwt() -> 'user_metadata') ->> 'tenant_id')::uuid = tenant_id );

-- -------------------------------------------------------------------------
-- STAFF MEMBERS (Self & Admin)
-- -------------------------------------------------------------------------
DROP POLICY IF EXISTS "Staff Access Own Tenant" ON staff_members;
CREATE POLICY "Staff Access Own Tenant" ON staff_members
FOR ALL TO authenticated
USING ( 
    id = auth.uid() -- ALLOW SELF LOOKUP (CRITICAL FOR LOGIN)
    OR 
    ((auth.jwt() -> 'user_metadata') ->> 'tenant_id')::uuid = tenant_id 
)
WITH CHECK ( 
    id = auth.uid() 
    OR 
    ((auth.jwt() -> 'user_metadata') ->> 'tenant_id')::uuid = tenant_id 
);

-- -------------------------------------------------------------------------
-- REGISTRATIONS
-- -------------------------------------------------------------------------
DROP POLICY IF EXISTS "Staff Access Registrations" ON registrations;
CREATE POLICY "Staff Access Registrations" ON registrations
FOR ALL TO authenticated
USING ( ((auth.jwt() -> 'user_metadata') ->> 'tenant_id')::uuid = tenant_id )
WITH CHECK ( ((auth.jwt() -> 'user_metadata') ->> 'tenant_id')::uuid = tenant_id );

-- -------------------------------------------------------------------------
-- PAYROLL
-- -------------------------------------------------------------------------
DROP POLICY IF EXISTS "Staff Access Payroll" ON payroll_entries;
CREATE POLICY "Staff Access Payroll" ON payroll_entries
FOR ALL TO authenticated
USING ( ((auth.jwt() -> 'user_metadata') ->> 'tenant_id')::uuid = tenant_id )
WITH CHECK ( ((auth.jwt() -> 'user_metadata') ->> 'tenant_id')::uuid = tenant_id );

-- -------------------------------------------------------------------------
-- FACILITIES (Tenant Staff Discovery)
-- -------------------------------------------------------------------------
DROP POLICY IF EXISTS "Tenant Staff Access Facilities" ON facilities;
CREATE POLICY "Tenant Staff Access Facilities" ON facilities
FOR SELECT TO authenticated
USING (
    tenant_id IN (
        SELECT s.tenant_id FROM public.staff_members s
        WHERE s.id = auth.uid() 
           OR LOWER(s.email) = LOWER(auth.jwt() ->> 'email')
    )
);

