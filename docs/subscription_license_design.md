# Subscription & License System — Full Design Presentation
### Version 2 — Updated with Business Vertical Layer

## What This Document Covers

This document describes the complete design of the new account, subscription, license, and business vertical system for the Management Platform — from the moment an owner discovers the app to the moment a staff member operates it daily in any type of business. Every concept is explained in plain language, with no code.

---

## Part 1 — Core Concepts & Definitions

Before anything else, we need to agree on the meaning of every key term. These definitions are the foundation of the entire system.

### Account
An **Account** is the top-level entity that represents a paying customer — always the business owner. One account = one owner. The account is linked to a single email address and password registered through Supabase Auth. Everything in the system belongs, directly or indirectly, to an Account. When an owner pays for a subscription, the subscription is attached to the Account.

### Business
A **Business** is the brand or company that the owner operates. An account can own one or more businesses (e.g., an investor who owns a gym chain AND a restaurant chain). For most users, they will have exactly one business. The business holds the brand name, logo, country, currency, and contact information. It does not operate on its own — it exists as an organizational container.

### Branch
A **Branch** is a physical location where the business operates. A branch is always owned by a Business. This is the core unit of operation — staff work at branches, customers belong to branches, sales happen at branches, devices are registered to branches. A single business can have as many branches as their subscription plan allows.

A branch is also linked to a **Facility Template** (see Part 8) which defines the vocabulary and default modules for that branch's business type.

Examples:
- "Ahmed Fitness Co." (Business) → "Downtown Branch", "Westside Branch", "Airport Branch" (Branches)
- "Bella Salon" (Business) → "Main Street Branch", "Mall Branch" (Branches)

### Facility Template
A **Facility Template** is a business vertical profile assigned to a branch. It describes what kind of operation the branch runs and carries two things: a suggested list of default modules, and a map of label overrides that replace generic system words with industry-specific terms. Templates are completely independent from the Subscription Plan — a template defines vocabulary and defaults, never limits or permissions. See Part 8 for the full list of templates and how they work.

### Module
A **Module** is a functional capability that can be enabled on a branch. Modules are not tied to any business type — they are tools that any branch can use in any combination. The available modules are:

- **POS Module**: Point-of-sale. Handles products, product categories, orders, payment collection, receipts, and cashier operations. Suitable for any business that sells items or services at a counter.
- **Appointments Module**: Handles bookable services, staff schedules, client bookings, and calendar management. Suitable for any business where clients book time slots.
- **Memberships Module**: Handles recurring membership plans, customer profiles, access control via RFID/barcode cards, session tracking, and turnstile integration.
- **Inventory Module**: Handles stock items, stock levels, supplier tracking, and low-stock alerts. Works alongside POS.
- **Projects Module**: Handles projects, phases, milestones, deliverables, and time entries. Suitable for architecture studios, agencies, consultancies, and any project-based operation.
- **Resource Booking Module**: Handles bookable resources (desks, rooms, equipment, venues), time-slot reservations, and pricing rules. Suitable for coworking spaces, venues, and rental businesses.
- **Enrollment Module**: Handles courses, cohorts, enrollments, and progress tracking. Suitable for schools, institutes, training centers, and any cohort-based educational operation.
- **HRM Module** *(future)*: Handles staff shifts, attendance, and payroll. Not in scope for the first rebuild but the schema must accommodate it.

Each branch independently selects which modules it uses. Modules can be pre-suggested by the branch's Facility Template, but the owner always has the final say on which are actually enabled.

### Subscription Plan
A **Subscription Plan** is a tier that the owner purchases for their entire Account. The plan defines the *limits and permissions* that apply across all their businesses and branches. It governs only numerical limits (branches, devices, staff, businesses) and feature gates (cross-branch reporting, API access). It does not define vocabulary, default modules, or business type in any way. That is the Facility Template's job. These two systems never overlap in responsibility.

### Device
A **Device** is a registered Windows machine that is authorized to run the Management app for a specific branch. Devices are registered by the owner after login. The number of devices allowed across all branches is defined by the subscription plan. Devices are identified by a hardware fingerprint (CPU ID + disk serial combined) but this is used for identification and auditing, not as a hard authentication gate — the active subscription is the gate.

### Roles — Owner and Staff
There are exactly two roles in the system:

- **Owner**: The person who created the Account. There is one Owner per Account. The Owner has full access to everything — all businesses, all branches, all modules, all settings, subscription management, and device management.
- **Staff**: Any other person invited to work at one or more branches. A Staff member's access is controlled by which modules they are assigned to on each branch, and by specific per-action permissions within those modules (e.g., "can process refunds: No"). Staff members cannot access Account-level settings, subscription management, or device management.

There are no other role names. Labels like "Trainer," "Stylist," "Cashier," or "Teacher" are business-specific job titles that exist informally — they do not exist as system roles.

---

## Part 2 — The Subscription Plans

There are four tiers. Each tier is strictly a superset of the one below it — you never lose a feature by upgrading.

---

### Tier 0 — Free (Trial)
**Purpose**: Let a new owner explore the app before committing.

**Duration**: 14 days from account creation. After 14 days, the account is locked and the owner must upgrade to continue.

**Limits**:
- 1 Business
- 1 Branch
- 1 Device registered
- 2 Staff accounts (including the owner)
- All modules available but with data caps (e.g., max 50 customers, max 200 products)
- No cloud sync history beyond 7 days
- No priority support

**How it works**: When an owner creates an account, they automatically start on Free. No credit card required. The app is fully functional within the limits above. When the trial expires, the app enters a read-only grace mode for 3 days, then locks entirely.

---

### Tier 1 — Starter
**Purpose**: Single-location small businesses.

**Price**: Monthly or annual billing (exact pricing TBD).

**Limits**:
- 1 Business
- Up to 2 Branches
- Up to 3 Devices total across all branches
- Up to 10 Staff accounts
- All modules available, no data caps
- Full cloud sync history (unlimited)
- Email support

**Who this is for**: A single gym, a single salon, a small restaurant, a solo coworking space that may want to expand to a second location later.

---

### Tier 2 — Professional
**Purpose**: Growing businesses with multiple locations.

**Limits**:
- 1 Business
- Up to 10 Branches
- Up to 20 Devices total across all branches
- Up to 50 Staff accounts
- All modules
- Cross-branch reporting and analytics
- Priority email + chat support
- Early access to new modules

**Who this is for**: A gym chain with several locations, a salon franchise, a restaurant group, a school with multiple campuses.

---

### Tier 3 — Enterprise
**Purpose**: Large operations or multi-business owners.

**Limits**:
- Up to 5 Businesses under one Account
- Unlimited Branches per Business
- Unlimited Devices
- Unlimited Staff accounts
- All modules
- Dedicated account manager
- Custom onboarding support
- API access for integrations

**Who this is for**: Investors or management groups operating multiple distinct brands.

---

### Plan Comparison Table

| Feature | Free | Starter | Professional | Enterprise |
|---|---|---|---|---|
| Businesses | 1 | 1 | 1 | 5 |
| Branches | 1 | 2 | 10 | Unlimited |
| Devices | 1 | 3 | 20 | Unlimited |
| Staff Accounts | 2 | 10 | 50 | Unlimited |
| All Modules | Yes (capped) | Yes | Yes | Yes |
| Cross-Branch Reports | No | No | Yes | Yes |
| Data History | 7 days | Unlimited | Unlimited | Unlimited |
| Support | None | Email | Priority | Dedicated |
| Duration | 14 days | Monthly/Annual | Monthly/Annual | Annual |

The Subscription Plan tier is stored as a clean integer rank (0, 1, 2, 3) in the database — never as a free-text string. This allows the system to perform simple comparisons when gating features, such as: "Is this account's tier rank at least 2 (Professional) to show cross-branch reporting?"

---

## Part 3 — How the System Works End-to-End

This section walks through the complete lifecycle, from a new owner signing up to a staff member operating the app daily.

---

### Step 1 — Owner Signs Up

The owner opens the Management app for the first time. They see an onboarding screen with two options: **Sign In** (existing account) or **Create Account** (new owner).

The owner chooses **Create Account** and enters:
- Full name
- Email address
- Password

The app calls Supabase Auth to create the user. An email verification link is sent. Until the email is verified, the account is in a "pending verification" state and the app shows a waiting screen.

Once verified, a record is created for the Account in the database. The account is automatically assigned the **Free** plan, and the 14-day trial clock starts.

---

### Step 2 — Business & First Branch Setup

After email verification, the owner is taken through a one-time setup wizard with the following steps in order:

1. **Name your business** — Business name, optional logo, country, currency.
2. **Name your first branch** — Branch name, address.
3. **What kind of business is this branch?** — The owner picks a Facility Template from a searchable list (e.g., Gym, Salon, Restaurant, Coworking Space, School, Architecture Studio). Selecting a template pre-fills the module selection and sets the label vocabulary for this branch. The owner can search by name or browse by cluster type. This step is optional — they can skip it and configure everything manually.
4. **Review and adjust modules** — The owner sees the modules that were pre-selected by the template and can add or remove any of them freely. The template is a suggestion, not a lock.
5. **Done** — The owner lands on the Dashboard.

The system creates the Business and the first Branch automatically. The owner is registered as the Owner of the Account.

---

### Step 3 — Subscription Management

The owner can view and manage their subscription from the **Account Settings** screen. Here they can see:

- Current plan name and limits
- How many branches they have vs. how many are allowed
- How many devices are registered vs. the limit
- Days remaining (on Free) or next billing date
- A button to upgrade, downgrade, or cancel

If the owner tries to create a branch and they are at their plan's branch limit, the app shows a clear message: *"You've reached the branch limit for your current plan. Upgrade to Professional to add more."* The option to upgrade is one click away.

---

### Step 4 — Device Registration

The Management app is a Windows desktop app. To run it on a machine, that machine must be registered as a Device under the account. Here is how that works:

**First device (the owner's machine)**:
During the setup wizard, after the branch is configured, the owner is prompted to register the current machine. The app reads the machine's hardware fingerprint (CPU + disk serial combined into a unique ID) and sends it to the server. The server records this as Device #1, assigned to the first branch. No key entry required.

**Additional devices**:
From the Account Settings → Devices screen, the owner can see all registered devices and add new ones. When a new machine runs the Management app for the first time, it prompts the user to log in. After login, if the device is not yet registered, it shows a "Register This Device" screen asking the owner to assign it to a branch. The server checks if the account has device slots available on their plan. If yes, it registers it. If no, it shows an upgrade prompt.

**Device limits are enforced at registration time, not at login time.** Once a device is registered, it can always log in as long as the subscription is active. Removing a device from the list frees up that slot.

**Device revocation**: The owner can revoke any device from the Account Settings panel at any time. A revoked device, the next time it tries to start the app, will be informed that it has been deactivated and will not be allowed to proceed.

---

### Step 5 — Staff Invitations

There are two roles: Owner and Staff. The Owner invites Staff from the Branch Settings screen. They enter the staff member's email and select:
- Which branch(es) they work at
- Which modules they have access to within each branch
- Specific permissions within each module (e.g., "can process refunds: No")

The staff member receives an email invitation with a link. They click the link, set their password, and are now able to log in to the app on any registered device. When they log in, the app loads their specific module access and permissions and shows only what they are allowed to use.

---

### Step 6 — Daily App Startup (Session Flow)

Every time the Management app starts on a registered device, the following happens in sequence:

1. **Load cached session**: The app checks if a valid session token exists on disk. This allows the app to start without an internet connection if the session is recent.
2. **Validate subscription**: If online, the app contacts the server to verify the account's subscription is active. If expired, the app enters grace mode (read-only for 3 days).
3. **Verify device registration**: The app checks that this machine's hardware ID is still in the registered devices list and has not been revoked.
4. **Select branch**: If the logged-in staff member is assigned to more than one branch, they are asked to select which branch they are working at today. If they only have one branch, this is skipped.
5. **Load branch context**: The app loads the modules enabled for the selected branch, applies the branch's Facility Template label overrides, and builds the navigation accordingly.
6. **Start working**: The staff member is now in the operational app.

If the device is offline at startup and a valid cached session exists that is less than 72 hours old, all of the above steps use cached data and the app starts in offline mode. Operations that require cloud (like customer registration) are queued and synced when connectivity returns.

---

## Part 4 — How Modules Interact Per Branch

A single branch can have multiple modules active simultaneously. The navigation bar adapts based on what is enabled. Labels shown in the examples below reflect what the app displays after applying the branch's Facility Template — the underlying module is the same, only the words change.

**Gym Branch** (Memberships + POS — "Gym" template applied):
- Dashboard shows check-in count, revenue, active members.
- Memberships section for managing plans, members, access events.
- POS section for selling merchandise, drinks, supplements.
- Access control (turnstile/RFID) connected to the Memberships module.

**Salon Branch** (Appointments + POS — "Salon" template applied):
- Dashboard shows today's bookings, revenue, staff utilization.
- Appointments section for booking management and staff calendars.
- POS section for collecting payment after a service.

**Restaurant Branch** (POS + Inventory — "Restaurant" template applied):
- Dashboard shows open tables, revenue, order queue.
- POS section with floor plan, table orders, kitchen printing.
- Inventory section for tracking ingredients and stock.

**Coworking Space Branch** (Resource Booking + Memberships — "Coworking" template applied):
- Dashboard shows desk occupancy, active memberships, today's bookings.
- Resource Booking section for managing desks, rooms, and time slots.
- Memberships section for recurring plans.

**School Branch** (Enrollment + Appointments — "School" template applied):
- Dashboard shows active enrollments, upcoming sessions, cohort progress.
- Enrollment section for courses, cohorts, and student tracking.
- Appointments section for individual tutoring or consultation booking.

**Architecture Studio Branch** (Projects + POS — "Architecture Studio" template applied):
- Dashboard shows active projects, milestones due, billable hours.
- Projects section for managing client projects, phases, and deliverables.
- POS section for invoicing and service billing.

**Multi-Purpose Branch** (any combination of modules):
- Full navigation with every selected section available.
- Useful for a sports club that has gym access, a café, and a personal training booking system.

---

## Part 5 — Cross-Branch Concepts

### Staff Working at Multiple Branches
A staff member can be assigned to more than one branch. When they log in, they select which branch they are working at for that session. Their module access and permissions can differ per branch.

### Shared Customers
A customer belongs to a specific branch by default. However, on Professional and Enterprise plans, the owner can enable **cross-branch customer sharing**, which means a customer registered at Downtown can be served at Westside and their record is visible across all branches of the same business.

### Cross-Branch Reporting (Professional+)
The owner's dashboard on Professional and Enterprise plans includes an aggregated view across all branches — total revenue, total active customers, total appointments today — so they can see the health of the entire operation from one screen.

---

## Part 6 — Subscription Enforcement Rules

These are the specific rules the system enforces and how it handles edge cases gracefully.

**Rule 1 — Branch limit reached**:
The owner cannot create a new branch if they are at their plan's limit. The UI shows a clear message and an upgrade button. Existing branches are unaffected.

**Rule 2 — Device limit reached**:
A new device cannot be registered if the account is at the device limit. Existing registered devices continue to work normally. The owner must either upgrade or revoke an existing device to free a slot.

**Rule 3 — Staff limit reached**:
New staff cannot be invited if the account is at the staff limit. Existing staff accounts are unaffected.

**Rule 4 — Subscription expired (Grace Mode)**:
When a subscription expires (payment failed or cancelled), the account enters a 3-day grace period. During grace mode:
- All devices can still log in and read data.
- No new records can be created (no new customers, no new sales).
- A prominent banner is shown urging the owner to renew.
- After 3 days, the app locks entirely and shows only a renewal screen.

**Rule 5 — Trial expired**:
Same behavior as Rule 4, but the lock happens immediately after 14 days if no plan is purchased. The owner's data is preserved and accessible again the moment they subscribe.

**Rule 6 — Downgrade**:
If an owner downgrades (e.g., from Professional to Starter), the system does not delete branches or devices. However, if they have 6 branches and Starter allows 2, the extra 4 branches are put into an **archived** state — their data is safe but staff cannot operate them until the owner re-upgrades or manually deletes the excess branches.

**Rule 7 — Dashboard widget gating**:
Dashboard KPI widgets are gated by two independent checks, both of which must pass for a widget to be visible: (1) the branch has the relevant module enabled, and (2) the account's subscription tier rank meets the minimum required for that widget. For example, a cross-branch revenue widget requires both the Professional tier (rank ≥ 2) AND at least two active branches. These checks use the integer tier rank stored on the Subscription Plan, never a string comparison.

---

## Part 7 — Security & Trust Model

**Who trusts what**:
- The **app** trusts the server for subscription status and permissions. These are checked at startup and cached locally for offline use.
- The **server** (Supabase) trusts the authenticated session token (JWT) issued by Supabase Auth. Every database request is validated against Row Level Security (RLS) policies that check the user's Account and Branch membership.
- **Data isolation**: Every row in every table is tagged with the Account ID. RLS policies prevent any user from reading or writing data that does not belong to their Account. This is enforced at the database level, not just the app level.

**What happens if someone shares their credentials**:
Staff members log in with their own personal email and password. Sharing credentials is a user behavior issue, not a system design problem. Device registration limits the blast radius — even with shared credentials, the session can only be used on registered devices.

**What happens if a device is stolen or lost**:
The owner revokes the device from Account Settings. The next time that device's app tries to start, it will be denied. Any locally cached data on that machine remains but cannot be synced or updated.

---

## Part 8 — Facility Templates & Business Verticals

### What a Facility Template Is

A **Facility Template** is a profile assigned to a branch that describes what kind of business it runs. It carries two things:

1. **A default module set** — the modules that are pre-selected when the owner sets up the branch. The owner can always add or remove modules after selection. The template is a starting point, not a restriction.
2. **A label override map** — a dictionary of generic system words and their business-specific replacements. For example, the system word "customer" becomes "Student" in a school branch, or "Guest" in a hotel branch. Every screen in the app that displays one of these words looks it up through a single label-resolution function rather than having the word hardcoded. This means adding a new business vertical never requires changing app screens — only adding a new template row.

### Important: Template vs. Subscription Plan

These two systems are completely independent and must never be confused:

- **Subscription Plan** = how much you can have (branches, devices, staff) and which premium features are unlocked.
- **Facility Template** = what your branch is called and which modules are suggested.

A school on the Starter plan gets the School template vocabulary. A gym on the Enterprise plan gets the Gym template vocabulary. The plan does not change labels. The template does not change limits. They do not talk to each other.

### The Label Resolution Rule

When the app needs to display a word like "customer," "session," or "booking," it goes through the following steps:

1. Check if the branch's Facility Template has an override for that word.
2. If yes, use the override (e.g., "Student").
3. If no override exists, fall back to the module's generic default label (e.g., "Customer").

This logic lives in one place — a single label-resolution service used across the entire app. There are no per-screen conditionals. Adding a new vertical means adding a new template with its label map. Nothing else changes.

### Cluster Types

Every Facility Template belongs to one of six cluster types. The cluster type is a machine-readable category used for analytics grouping, reporting defaults, and future AI features. It does not affect what modules are available.

| Cluster Type | Description | Example Verticals |
|---|---|---|
| **Membership** | Recurring access-based businesses | Gym, Coworking Space, Club |
| **Appointment** | Time-slot booking businesses | Salon, Clinic, Barbershop, Tutor |
| **Order** | Counter or table-based selling | Restaurant, Café, Supermarket, Retail |
| **Project** | Deliverable and milestone-based work | Architecture Studio, Agency, Consultancy |
| **Rental** | Resource-based time-slot businesses | Venue, Equipment Rental, Parking |
| **Cohort** | Group-learning and enrollment businesses | School, Training Center, Online Institute |

### Template Catalog (Initial Set)

The following templates will be available at launch. Each entry shows the template name, its cluster type, default modules, and key label overrides.

---

**Gym**
- Cluster: Membership
- Default modules: Memberships, POS
- Label overrides: customer → Member, session → Check-in, booking → Session

**Salon / Barbershop**
- Cluster: Appointment
- Default modules: Appointments, POS
- Label overrides: customer → Client, booking → Appointment, staff → Stylist *(display only, not a role)*

**Restaurant**
- Cluster: Order
- Default modules: POS, Inventory
- Label overrides: customer → Guest, booking → Reservation, product → Dish

**Café**
- Cluster: Order
- Default modules: POS, Inventory
- Label overrides: customer → Guest, product → Item

**Supermarket / Retail**
- Cluster: Order
- Default modules: POS, Inventory
- Label overrides: customer → Customer, product → Product *(no change — defaults apply)*

**Coworking Space**
- Cluster: Membership + Rental
- Default modules: Memberships, Resource Booking
- Label overrides: customer → Member, resource → Desk/Room, session → Day Pass

**Clinic / Medical Practice**
- Cluster: Appointment
- Default modules: Appointments, POS
- Label overrides: customer → Patient, booking → Appointment, staff → Doctor *(display only)*

**School / Training Center**
- Cluster: Cohort
- Default modules: Enrollment, Appointments
- Label overrides: customer → Student, session → Class, booking → Session, product → Course

**Architecture Studio / Agency**
- Cluster: Project
- Default modules: Projects, POS
- Label overrides: customer → Client, session → Meeting, product → Service

**Sports Club**
- Cluster: Membership
- Default modules: Memberships, Appointments, POS
- Label overrides: customer → Member, booking → Training Session

**Venue / Event Space**
- Cluster: Rental
- Default modules: Resource Booking, POS
- Label overrides: customer → Client, resource → Hall/Space, session → Event

**Generic / Other**
- Cluster: Order
- Default modules: POS
- Label overrides: none — all generic defaults apply
- Used when no template fits; the owner configures everything manually.

---

## Summary — The Full Picture

| Concept | What it is | Belongs to |
|---|---|---|
| Account | The paying owner entity | Top level |
| Business | A brand or company | Account |
| Branch | A physical location | Business |
| Facility Template | A business vertical profile (vocabulary + default modules) | Branch |
| Module | A functional capability (POS, Appointments, etc.) | Branch |
| Subscription Plan | Tier defining numeric limits and feature access | Account |
| Device | A registered Windows machine | Branch |
| Owner | The person who created and owns the Account | Account |
| Staff | Any other person invited to work at branches | Account, assigned to Branches |
| Customer | The end-user of the business (label varies by template) | Branch (or Business on Pro+) |

The entire system flows in one direction: an **Account** subscribes to a **Plan**, which determines what they can create. They create **Businesses** and **Branches**. Each **Branch** is given a **Facility Template** that sets its vocabulary and pre-selects its modules. **Devices** are registered to branches. The **Owner** and invited **Staff** log in on devices and operate the modules. **Customers** are served through those modules, addressed by whatever label the template assigns to them.

The license system is no longer a string key — it is the active subscription on the Account. If the subscription is active and the device is registered, the app works. That is the entire gate.
