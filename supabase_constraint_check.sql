-- ==============================================================================
-- CONSTRAINT CHECK: FINDING REQUIRED COLUMNS IN PROFILES
-- ==============================================================================
-- Run this in your Supabase SQL Editor.

SELECT 
    column_name, 
    is_nullable, 
    column_default,
    data_type
FROM 
    information_schema.columns 
WHERE 
    table_name = 'profiles' 
    AND table_schema = 'public'
    AND is_nullable = 'NO'
    AND column_default IS NULL;
