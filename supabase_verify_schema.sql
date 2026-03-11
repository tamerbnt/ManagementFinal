-- ==============================================================================
-- SCHEMA VERIFICATION: DEBUGGING PROFILES TABLE
-- ==============================================================================
-- Run this in your Supabase SQL Editor. 
-- It will help us identify why the account setup is failing.

-- 1. Check if table exists and show its columns
SELECT 
    column_name, 
    data_type, 
    is_nullable, 
    column_default 
FROM 
    information_schema.columns 
WHERE 
    table_name = 'profiles'
    AND table_schema = 'public'
ORDER BY 
    ordinal_position;

-- 2. Check for unique constraints or indexes
SELECT
    indexname,
    indexdef
FROM
    pg_indexes
WHERE
    schemaname = 'public'
    AND tablename = 'profiles';

-- 3. Check current triggers on auth.users
SELECT 
    trigger_name, 
    event_manipulation, 
    action_statement 
FROM 
    information_schema.triggers 
WHERE 
    event_object_table = 'users' 
    AND event_object_schema = 'auth';
