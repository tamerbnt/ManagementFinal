# New Project + License System Rebuild Plan

## Background

The goal is to **copy the current codebase** into a fresh working folder, then **redesign the Supabase schema and license system from scratch** to match a new vision. The existing code will be the starting point — not thrown away — so we preserve all the WPF UI, CQRS handlers, sync engine, etc. while replacing the account/license layer beneath.

---

## User Review Required

> [!IMPORTANT]
> **Before we write any code, your answers to the Open Questions below will directly shape the new schema and service design. Please read them carefully.**

> [!WARNING]
> **Supabase schema rebuild = data loss.** All existing data in your current Supabase project will be gone after the reset. If you have real/live data you need to keep, let me know and we will export it first.

---

## Open Questions

> [!IMPORTANT]
> **Q1 — New Supabase project or same project reset?**
> - Option A: Create a brand-new Supabase project (new URL + new anon key) — zero risk of touching live data.
> - Option B: Drop and recreate all tables in the *existing* project (riskier but no credential changes needed).
> Which do you prefer?

> [!IMPORTANT]
> **Q2 — What is the new vision for the license system?**
> The current system works like: `license_key → tenant → devices (hardware-bound)`.
> What do you want to change? For example:
> - License tied to a **user account** (email/password) rather than a license key string?
> - A **subscription plan** model (free, pro, enterprise tiers)?
> - **Per-facility** licensing instead of per-tenant?
> - Completely different concept?

> [!IMPORTANT]
> **Q3 — New folder location for the copied project?**
> Currently at: `c:\Users\techbox\.gemini\antigravity\ManagementBackup1234`
> Where should the new working project be placed? For example:
> `c:\Users\techbox\Projects\ManagementV2` or similar.
> *(Note: working inside `.gemini\antigravity` is fine too.)*

> [!IMPORTANT]
> **Q4 — Account/Profile model changes?**
> Currently: `profiles` table mirrors `auth.users` and links to `staff_members` + `tenants`.
> Do you want to simplify this? E.g., one unified `accounts` table, or keep the separation?

> [!IMPORTANT]
> **Q5 — Tables to remove / add?**
> Are there any **features you want to drop** from the new version (e.g., restaurant module, salon module) or **new tables you already know you need**?

---

## Proposed Execution Phases

Once your vision is clear, we'll execute in these phases:

### Phase 1 — Copy the Codebase
- Use `robocopy` to duplicate the entire solution to the new folder.
- Strip build artifacts (`bin/`, `obj/`, `.vs/`).
- Update the `.sln` and project paths if the folder name changes.
- Validate it builds with `dotnet build`.

---

### Phase 2 — New Supabase Schema (SQL)
Design and write migration SQL scripts for the new tables. Based on what you tell me, this will include:

| Table (current) | Likely Action |
|---|---|
| `profiles` | Redesign or replace with `accounts` |
| `tenants` | Keep / restructure |
| `licenses` | **Complete redesign** |
| `tenant_devices` | Keep / restructure |
| `staff_members` | Keep / restructure |
| `facilities` | Keep / restructure |
| `members` | Keep |
| `membership_plans` | Keep |
| `sales` / `sale_items` | Keep |
| `access_events` | Keep |
| `appointments` | Keep |
| `restaurant_*` / `salon_*` | TBD based on Q5 |
| `dashboard_snapshots` | Keep |

We will also write:
- RLS policies for every table
- The new `verify_license_key` Postgres function (or its replacement)
- Any triggers needed

---

### Phase 3 — Update Supabase C# Models
File: `Management.Infrastructure/Integrations/Supabase/Models/SupabaseModels.cs`
- Update all `[Table]` / `[Column]` model classes to match the new schema exactly.

---

### Phase 4 — Rebuild the License Service
Files to replace:
- `LicenseService.cs` → new logic matching the new license model
- `OnboardingService.cs` → new onboarding flow (sign up → link license → register device)
- `LicenseCheckResult` DTO
- `LicenseLease.cs` domain model (if still used)
- `ILicenseService` interface

---

### Phase 5 — Update Authentication & Tenant Wiring
- `AuthenticationService.cs` — update login flow to work with new account/profile model.
- `TenantService.cs` — update to reflect new tenant/license structure.
- `OnboardingService.cs` — walk-through wizard steps rebuilt from scratch.

---

### Phase 6 — Validate & Smoke Test
- `dotnet build` — must compile clean.
- Walk through login flow manually.
- Verify license validation RPC works end-to-end.

---

## Verification Plan

### Automated
```powershell
dotnet build Management.sln -c Debug
dotnet test
```

### Manual
1. Run app → license screen appears.
2. Enter new-style license key → proceeds to tenant setup.
3. Device registration completes.
4. Login with staff credentials → lands on dashboard.

---

> [!NOTE]
> Once you answer the Open Questions above, I will finalize the schema SQL and phase-by-phase execution plan before touching any code.
