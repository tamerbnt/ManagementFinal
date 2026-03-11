-- ==============================================================================
-- SIMPLIFIED DIAGNOSTIC: COLUMN DEFINITIONS ONLY
-- ==============================================================================
-- Run this script in your Supabase SQL Editor. 
-- PLEASE COPY THE RESULTS OR SCREENSHOT THE ENTIRE TABLE.

SELECT 
    table_name, 
    column_name, 
    data_type, 
    is_nullable, 
    column_default,
    character_maximum_length
FROM 
    information_schema.columns 
WHERE 
    table_name IN ('facilities', 'tenants', 'members')
    AND table_schema = 'public'
ORDER BY 
    table_name, ordinal_position;
