---
name: wpf-modern-ui
description: Generates WPF XAML following the 'Clean SaaS' Design System (Solid Colors, Soft Shadows, Hybrid Theme). Use this for all Views.
---

# WPF Design System Rules (Clean SaaS)

You must adhere to these strict visual constraints.

## 1. Visual Physics (The Look)
- **NO BLUR:** Never use `<BlurEffect>` or `Mica`. It is banned for performance reasons.
- **Backgrounds:** 
  - Main Window Background: Solid Light Gray/Blue (`#F1F5F9`).
  - Cards/Containers: Solid Pure White (`#FFFFFF`) with `CornerRadius="12"`.
- **Shadows:** Use `DropShadowEffect` with `BlurRadius="15"`, `ShadowDepth="5"`, `Opacity="0.05"` (Subtle).

## 2. The Hybrid Layout Structure
- **Sidebar (Left):** MUST be Dark Navy (`#0F172A`). Text is White/Gray.
- **Content (Right):** Light Gray Background.
- **Z-Layering:** Use Grid Z-Index for floating elements:
  - Z-0: Background Color
  - Z-1: Content Area
  - Z-10: Sidebar & TopBar (Floating Anchors)
  - Z-100: Modals / Command Palette

## 3. Component Styling
- **Inputs:** Always use `Style="{StaticResource Input.Apple}"`. (Border-bottom style, Blue glow on focus).
- **Buttons:** Always use `Style="{StaticResource Button.Primary}"` (Blue) or `Button.Ghost`.
- **DataGrid:** Virtualization MUST be enabled (`IsVirtualizing="True"`). No grid lines. 72px Row Height.

## 4. Layout Constraints
- **Scrolling:** NEVER wrap the whole Window in a ScrollViewer. Only wrap specific internal lists (like Activity Feed or Member List).
- **Margins:** Use standard spacing (`24` or `32`). Never use tight spacing (`5`).
