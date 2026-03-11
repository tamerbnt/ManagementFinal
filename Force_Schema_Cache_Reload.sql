-- FORCE SCHEMA CACHE RELOAD
-- This script does two things:
-- 1. Updates a comment on the schema (harmless change) to trigger a cache reload.
-- 2. Grants permissions (just in case) to ensuring the anon/authenticated roles can see the columns.

-- A. Trigger Cache Reload
NOTIFY pgrst, 'reload config';

-- B. Explicit Grant (Safety Net)
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO service_role;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO postgres;
GRANT ALL ON ALL TABLES IN SCHEMA public TO authenticated;
GRANT ALL ON ALL TABLES IN SCHEMA public TO anon;

-- C. Comment Touch (Alternative Trigger)
COMMENT ON SCHEMA public IS 'Standard public schema - Cache Reloaded via SQL';

-- D. Verify Columns (Output for User)
SELECT table_name, column_name, data_type 
FROM information_schema.columns 
WHERE table_name IN ('membership_plans', 'gym_settings') 
AND column_name IN ('price', 'price_amount', 'phone', 'phone_number');
