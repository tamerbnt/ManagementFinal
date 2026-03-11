---
name: supabase-backend
description: Rules for interacting with Supabase, PostgreSQL, and Sync Logic. Use this for Data Services.
---

# Supabase Integration Rules

## 1. RPC / Database Functions
- **Parameter Naming:** SQL Functions ALWAYS use the `p_` prefix (e.g., `p_hardware_id`) to avoid ambiguous column errors.
- **C# Invocation:** The Dictionary keys passed to `_supabase.Rpc` MUST match the SQL parameter names exactly.
  - *Correct:* `{"p_lookup_key", key}`
  - *Incorrect:* `{"lookup_key", key}`

## 2. Data Sync Topology
- **Sync Order:** Always sync "Parent" tables first (`Members`, `Products`) before "Child" tables (`Sales`, `Logs`) to prevent Foreign Key crashes.
- **Conflict Strategy:** Last-Write-Wins based on `updated_at`.

## 3. Data Types
- **Dates:** Always convert `DateTime` to `.ToUniversalTime()` before sending to Postgres.
- **IDs:** Use `Guid` (UUID v7 preferred) for all Primary Keys to support offline creation.
