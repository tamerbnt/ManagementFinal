# Phase 2 Architecture: Supabase Schema Design, Lifetime Licensing & Cash System

> **Document Version:** 3.0 (Updated: One-Time Cash Lifetime Licensing for all Paid Tiers)  
> **Target Environment:** Supabase Cloud (`https://tzzncsbvezywtaknwind.supabase.co`) & Atrium Desktop Solution (`D:\atrium\Atrium2`)  
> **Status:** Final Pre-Implementation Specification (Ready for SQL Migration Script)

---

## 1. Executive Summary & Lifetime Cash Licensing Model

Phase 2 builds the database and authorization foundation for the application.

Following your commercial model for the cash market:
1. **Free Tier = 14-Day Evaluation Trial**: Allows a new client to test the app on their reception PC before purchasing.
2. **All Paid Plans = 100% Lifetime Access (Perpetual Licenses)**:
   - **Starter** = One-time cash payment $\rightarrow$ **Lifetime Access**.
   - **Professional** = One-time cash payment $\rightarrow$ **Lifetime Access**.
   - **Enterprise** = One-time cash payment $\rightarrow$ **Lifetime Access**.
3. **No Recurring Billing / No Subscription Expiration for Paid Users**:
   - Once a paid voucher key is redeemed (or activated by you), the account is marked **`is_lifetime = true`** (`current_period_end = NULL`).
   - The software will **never expire**, never lock them out, and never show a renewal countdown.
4. **Prepaid Cash Activation Keys (Vouchers)**:
   - When a client pays cash in hand, you issue a lifetime key: e.g. `ATR-START-LIFE-7X9K-42M1`.
   - Client enters it in the app $\rightarrow$ Unlocks their lifetime tier instantly.
   - You can also activate them directly in Supabase with one click.

---

## 2. Plan Comparison Table (Lifetime Model)

```
┌─────────────────┬─────────────┬──────────────────┬──────────────────┬──────────────────┐
│ Feature / Limit │ Free Trial  │   Starter        │   Professional   │   Enterprise     │
│                 │  (Tier 0)   │   (Tier 1)       │   (Tier 2)       │   (Tier 3)       │
├─────────────────┼─────────────┼──────────────────┼──────────────────┼──────────────────┤
│ Commercial Model│ Evaluation  │ Cash (One-Time)  │ Cash (One-Time)  │ Cash (One-Time)  │
│ License Duration│ 14 Days     │ LIFETIME (Never) │ LIFETIME (Never) │ LIFETIME (Never) │
│ Businesses      │      1      │        1         │        1         │     Up to 5      │
│ Branches        │  1 Branch   │   Up to 2        │   Up to 10       │    Unlimited     │
│ Devices (PCs)   │    1 PC     │   Up to 3 PCs    │   Up to 20 PCs   │    Unlimited     │
│ Staff Accounts  │ 2 (Owner+1) │   Up to 10       │   Up to 50       │    Unlimited     │
│ All Modules     │ Yes (capped)│       Yes        │       Yes        │       Yes        │
│ Cross-Branch Rep│     No      │       No         │       Yes        │       Yes        │
│ Shared Customers│     No      │       No         │       Yes        │       Yes        │
│ UI Badge        │ "12 Days"   │ "Starter Lifetime" "Pro Lifetime"   │ "Enterprise Life"│
└─────────────────┴─────────────┴──────────────────┴──────────────────┴──────────────────┘
```

---

## 3. Complete Inventory of Supabase Tables (16 Tables)

```
┌────────────────────────────────────────────────────────────────────────┐
│ GROUP A: Identity, Lifetime Licensing & Organization Engine (7 Tables) │
│  1. accounts                     2. subscription_plans (4 Tiers)       │
│  3. account_subscriptions        4. license_vouchers (Lifetime Keys)   │
│  5. businesses                   6. branches                           │
│  7. devices                                                            │
├────────────────────────────────────────────────────────────────────────┤
│ GROUP B: Business Verticals & Modular Permissions (6 Tables)           │
│  8. facility_templates (11 Seeds) 9. modules (8 Core Tools)            │
│ 10. branch_modules (Junction)    11. staff                             │
│ 12. staff_branch_assignments     13. staff_module_permissions          │
├────────────────────────────────────────────────────────────────────────┤
│ GROUP C: Customer Recognition & Cloud Ingestion (3 Tables)             │
│ 14. members_directory (Thin Cloud) 15. registrations                   │
│ 16. registration_requests                                              │
└────────────────────────────────────────────────────────────────────────┘
```

---

### Key Database Adjustments for Lifetime Licensing

#### Table 3: `account_subscriptions`
* `status`: `'trialing'` (Free tier) or `'active'` (Paid lifetime).
* `trial_ends_at`: Set to `now() + 14 days` on signup (only used when `tier_rank = 0`). Set to `NULL` for paid plans.
* `current_period_end`: `NULL` for lifetime licenses (signifies no expiration date).
* `grace_ends_at`: `NULL` for lifetime licenses. Only applies if a 14-day trial runs out without purchasing.

#### Table 4: `license_vouchers`
* `duration_months`: `-1` by default (representing **Lifetime Perpetual License**).
* `code` format: `ATR-[PLAN]-LIFE-[RANDOM]` (e.g., `ATR-START-LIFE-8821-KL74`, `ATR-PRO-LIFE-3912-VX90`).
* Once redeemed:
  - Links to `account_id`.
  - Sets `account_subscriptions.status = 'active'`, `plan_id = voucher.plan_id`, and `current_period_end = NULL` (Lifetime).

---

## 4. Server-Side Postgres RPC Functions

| Function Name | Inputs | Description |
|---|---|---|
| `onboard_owner_account` | `p_owner_id`, `p_full_name`, `p_email`, `p_business_name`, `p_branch_name`, `p_template_id`, `p_hardware_id`, `p_device_label`, `p_voucher_code DEFAULT NULL` | Atomic Genesis signup. If lifetime voucher provided, activates paid lifetime plan immediately; otherwise starts 14-day Free trial. Registers Device #1, business, and branch. |
| `redeem_license_voucher` | `p_account_id`, `p_voucher_code` | Validates lifetime voucher key, marks it redeemed, updates `account_subscriptions` to the new tier with lifetime validity (`current_period_end = NULL`). |
| `admin_activate_account` | `p_email`, `p_tier_rank`, `p_admin_secret` | Direct admin one-click activation: upgrades an account to Starter, Pro, or Enterprise Lifetime after receiving cash in hand. |
| `check_subscription_status` | `p_hardware_id` | Called at app startup. For paid plans, returns `{ status: 'active', is_lifetime: true, expires_at: null, tier_rank, limits }`. For Free, returns remaining trial days. |
| `check_device_registration` | `p_hardware_id` | Fast startup hardware check returning `{is_registered, is_active, account_id, branch_id}`. |
| `register_device` | `p_account_id`, `p_branch_id`, `p_hardware_id`, `p_label` | Registers an expansion PC to a branch, enforcing plan device limits (3 for Starter, 20 for Pro, Unlimited for Enterprise). |
| `revoke_device` | `p_device_id` | Instantly sets `devices.is_active = false`. |
| `get_staff_profiles_by_email` | `p_email` | Returns user's staff records, assigned branches, and module permissions before login. |
| `get_account_branches` | `p_account_id` | Returns active branches for an account. |

---

## 5. User Experience in the Desktop App

1. **Free Trial User:**
   * Top bar badge: `[ ⏱️ Free Trial — 12 Days Left ]`
   * Can click "Activate License" anytime to enter their Lifetime Key.

2. **Paid Lifetime User (Starter, Pro, Enterprise):**
   * Top bar badge: `[ ⭐ Starter Plan — Lifetime License ]` or `[ 💎 Professional Plan — Lifetime License ]`
   * Clean, permanent license state. No expiration warnings, no renewal nags, no grace period alerts.

---

## 6. Execution Plan & Next Step

1. **Review Final Specification**: The 16 tables, lifetime rules, and cash voucher mechanics.
2. **Generate SQL Script**: Output `phase2_initial_schema_and_seeds.sql` with full table DDL, lifetime RPCs, RLS policies, and seed data.
3. **Run in Supabase**: Execute the script in your Supabase SQL Editor.
4. **Verification**: Verify that tables and seed data are initialized and ready for Phase 3 C# model alignment.
