-- ==============================================================================
-- DATABASE DIAGNOSTIC: COMPLETE SCHEMA MAPPING
-- ==============================================================================
-- Run this script in your Supabase SQL Editor. 
-- It will provide several result sets. Please copy/paste the outputs or 
-- provide a screenshot of the key tables (facilities, tenants, members, etc.)

-- 1. MAP TABLES AND COLUMNS (The Source of Truth)
SELECT 
    table_name, 
    column_name, 
    data_type, 
    is_nullable, 
    column_default
FROM 
    information_schema.columns 
WHERE 
    table_schema = 'public'
ORDER BY 
    table_name, ordinal_position;

-- 2. MAP RLS POLICIES (Security check)
SELECT 
    schemaname, 
    tablename, 
    policyname, 
    permissive, 
    roles, 
    cmd, 
    qual, 
    with_check
FROM 
    pg_policies
WHERE 
    schemaname = 'public'
ORDER BY 
    tablename;

-- 3. LIST ALL CUSTOM FUNCTIONS (RPC check)
SELECT 
    routine_name, 
    data_type AS return_type
FROM 
    information_schema.routines 
WHERE 
    routine_schema = 'public' 
    AND routine_type = 'FUNCTION'
ORDER BY 
    routine_name;

-- 4. CHECK FOREIGN KEYS (Relationship check)
SELECT
    tc.table_name, 
    kcu.column_name, 
    ccu.table_name AS foreign_table_name,
    ccu.column_name AS foreign_column_name 
FROM 
    information_schema.table_constraints AS tc 
    JOIN information_schema.key_column_usage AS kcu
      ON tc.constraint_name = kcu.constraint_name
      AND tc.table_schema = kcu.table_schema
    JOIN information_schema.constraint_column_usage AS ccu
      ON ccu.constraint_name = tc.constraint_name
      AND ccu.table_schema = tc.table_schema
WHERE tc.constraint_type = 'FOREIGN KEY' AND tc.table_schema = 'public';
