---
name: project-domain-rules
description: The Master Product Specification for the 'Titan' Multi-Facility Management System. Contains business logic, module requirements, and user flows.
---

# Project Titan: Domain & Business Logic

You are building a **Multi-Facility Management System** (Gym, Salon, Restaurant) that runs **Offline-First** on Windows.

## 1. Core Identity & Licensing
- **Business Model:** One License Key = Up to 3 Devices.
- **Hardware Lock:** The app generates a unique `HardwareID` (Motherboard + CPU + Registry Fallback).
- **Architecture:** Local SQLite (Source of Truth) <-> Background Sync <-> Supabase (Cloud Backup).
- **Users:**
  - **Owner:** Full access to all facilities.
  - **Staff:** Restricted to specific Facility and Permissions.

## 2. The Three Facility Types
The app behaves differently based on the `FacilityType` selected during onboarding:

### 🏋️ Type A: Gym (Access Control Focus)
- **Primary View:** Dashboard with Live Access Feed.
- **Core Action:** **Check-In**.
  - Input: RFID Card / Barcode (Scanner acts as Keyboard).
  - Logic: Validate `Subscription.IsActive` + `FacilityAccess` + `TimeRestrictions`.
  - Feedback: Large Green (Go) or Red (Stop) visual.
- **Shared Access:** Gym members may have access to the Pool/Hammam (defined in `MembershipPlan`).

### 💇 Type B: Salon (Scheduler Focus)
- **Primary View:** Appointment Scheduler (Canvas-based).
- **Core Action:** **Booking**.
  - UI: Horizontal Time / Vertical Staff columns.
  - Logic: Prevent double-booking. Filter "Available Slots" based on Service Duration.
- **Entities:** Clients (not Members), Services (not Products).

### 🍽️ Type C: Restaurant (POS Focus)
- **Primary View:** Floor Plan (Draggable Tables).
- **Core Action:** **Order Management**.
  - States: Available -> Occupied -> Order Sent -> Ready -> Paid.
  - Kitchen: Orders push to a real-time display (SignalR/Polling).
- **Inventory:** Menu Items have Ingredients and Stock tracking.

## 3. Financial Module (Global)
- **Point of Sale (POS):**
  - Layout: Product Grid (Left) + Cart (Right).
  - Logic: Calculates Subtotal, Tax (Configurable), Discounts.
- **Hardware Integration:**
  - **Printers:** Sends Raw ESC/POS hex codes to Thermal Printers (Network/USB).
  - **Cash Drawer:** Sends "Kick" command on cash transaction.

## 4. Design System Alignment (Hybrid Theme)
- **Theme:** "Hybrid SaaS".
  - **Sidebar:** Dark Navy (`#0F172A`).
  - **Content Background:** Light Gray (`#F1F5F9`).
  - **Cards:** Pure White (`#FFFFFF`) with Soft Shadows.
- **Navigation:** Fixed Sidebar & TopBar. Only inner content scrolls.

## 5. Critical Workflows
- **Onboarding:** License Check -> Admin Create -> Facility Context Selection -> Encrypted Local Config Save.
- **Sync:** Parents (`Members`) sync before Children (`Sales`) to prevent Foreign Key crashes. Last-Write-Wins logic.
