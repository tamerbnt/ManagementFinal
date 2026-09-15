# Row-Level Security (RLS) Specification

### Core Security Helper
```sql
CREATE OR REPLACE FUNCTION public.get_auth_account_id()
RETURNS uuid
LANGUAGE plpgsql
STABLE
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
BEGIN
  RETURN COALESCE(
    (SELECT id FROM public.accounts WHERE id = auth.uid()),
    (SELECT account_id FROM public.staff WHERE auth_user_id = auth.uid() AND is_active = true LIMIT 1)
  );
END;
$$;
```

### Policy Patterns

#### Tenant Owned Tables (accounts, subscriptions, businesses, branches, devices, staff, etc.)
```sql
CREATE POLICY accounts_owner_all ON public.accounts
    FOR ALL TO authenticated
    USING (id = auth.uid())
    WITH CHECK (id = auth.uid());

CREATE POLICY businesses_tenant_all ON public.businesses
    FOR ALL TO authenticated
    USING (account_id = get_auth_account_id())
    WITH CHECK (account_id = get_auth_account_id());
```

#### Branch-Scoped Tables (branch_sync_logs, staff_activity_logs)
```sql
CREATE POLICY branch_sync_tenant_all ON public.branch_sync_logs
    FOR ALL TO authenticated
    USING (branch_id IN (SELECT id FROM public.branches WHERE account_id = get_auth_account_id()));
```

#### Public Read Catalogs (subscription_plans, business_templates)
```sql
CREATE POLICY subscription_plans_public_read ON public.subscription_plans
    FOR SELECT TO authenticated, anon
    USING (true);
```
