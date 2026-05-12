-- ============================================================
-- REMOTE VIEW INVESTIGATION QUERIES
-- Run these in the Supabase SQL Editor to audit your data sync.
-- ============================================================

-- 1. Check Dashboard Snapshots (The main source for the Remote Dashboard)
-- This table should have one row per facility.
SELECT 
    id, 
    facility_id, 
    tenant_id, 
    last_updated_at,
    -- Extract specific fields from the JSON payload to verify hydration
    snapshot_data::json->>'CheckInsToday' as check_ins,
    snapshot_data::json->>'DailyRevenue' as revenue
FROM dashboard_snapshots
ORDER BY last_updated_at DESC;


-- 2. Check Daily History Summaries (Source for the Remote History view)
-- This table tracks daily performance metrics.
SELECT 
    id,
    facility_id,
    tenant_id,
    summary_date,
    total_revenue,
    check_in_count
FROM daily_history_summaries
ORDER BY summary_date DESC;


-- 3. Diagnostic: Check for RLS (Row-Level Security) Denials
-- If the queries above return nothing but you see "Successfully pushed" in your logs,
-- it usually means the rows were inserted but your current dashboard user 
-- doesn't have permission to SELECT them.
-- Check if you (the dashboard user) can see any rows in the auth.users table 
-- that match the tenant_id in the snapshots.
SELECT id, email, raw_user_meta_data->>'tenant_id' as user_tenant
FROM auth.users;


-- 4. Check Table Schema Integrity
-- Ensure columns match what the C# code expects.
SELECT column_name, data_type 
FROM information_schema.columns 
WHERE table_name = 'dashboard_snapshots';
