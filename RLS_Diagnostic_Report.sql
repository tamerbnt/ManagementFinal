-- =========================================================================
-- DEEP DIAGNOSTIC: RLS Policies & Schema State
-- =========================================================================
-- Use this script to confirm exactly which tables have RLS enabled, 
-- what policies exist, and what permissions they enforce.

-- 1. Check RLS Status & Policy Counts
SELECT 
    t.tablename,
    t.rowsecurity AS rls_enabled,
    COUNT(p.policyname) AS policy_count
FROM 
    pg_tables t
LEFT JOIN 
    pg_policies p ON t.tablename = p.tablename
WHERE 
    t.schemaname = 'public'
GROUP BY 
    t.tablename, t.rowsecurity
ORDER BY 
    t.tablename;

-- 2. Detailed Policy Inspection (What rules are actually active?)
SELECT 
    schemaname,
    tablename,
    policyname,
    permissive,
    roles,
    cmd AS operation,
    qual AS using_expression,
    with_check AS check_expression
FROM 
    pg_policies
WHERE 
    schemaname = 'public'
ORDER BY 
    tablename, cmd;

-- 3. Check for Schema Drift (Product Columns)
SELECT 
    table_name, 
    column_name, 
    data_type 
FROM 
    information_schema.columns 
WHERE 
    table_name = 'products' 
    AND column_name IN ('price', 'price_amount', 'cost', 'cost_amount');
