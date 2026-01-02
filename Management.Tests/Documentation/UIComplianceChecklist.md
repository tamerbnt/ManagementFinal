# UI Compliance Audit Checklist - Apple 2025 (Sequoia) Aesthetic

## XAML Spy / Snoop Audit Properties

### Critical Properties to Verify

#### 1. Layout Rendering Properties
**Property**: `UseLayoutRounding`
- **Expected Value**: `True`
- **Location**: All `Window` and `UserControl` root elements
- **Purpose**: Ensures pixel-perfect rendering on all DPI settings
- **Audit Command** (XAML Spy):
  ```
  Find all elements where UseLayoutRounding != True
  ```

**Property**: `SnapsToDevicePixels`
- **Expected Value**: `True`
- **Location**: All visual elements (especially `Border`, `Rectangle`, `Line`)
- **Purpose**: Prevents blurry edges on non-integer pixel boundaries
- **Audit Command** (Snoop):
  ```
  Filter: SnapsToDevicePixels == False
  ```

#### 2. Corner Radius Consistency
**Property**: `CornerRadius`
- **Expected Values**:
  - Cards/Modals: `16` or `24`
  - Buttons: `8`
  - Input Fields: `8`
  - Small chips: `4`
- **Location**: All `Border` elements
- **Audit Command**:
  ```
  Find all Border elements
  Check CornerRadius values match design system
  ```

#### 3. Color Resources
**Property**: `Background`, `Foreground`, `BorderBrush`
- **Expected**: All use `{DynamicResource}` bindings
- **Forbidden**: Hardcoded HEX values (e.g., `#FFFFFF`, `#000000`)
- **Audit Command**:
  ```
  Search for: Background="#
  Search for: Foreground="#
  Search for: BorderBrush="#
  ```
- **Expected Result**: Zero matches

#### 4. Typography
**Property**: `FontFamily`
- **Expected Values**:
  - Titles/Headers: `Segoe UI Variable Display`
  - Body/Data: `Inter`
- **Location**: All `TextBlock` and `TextBox` elements
- **Audit Command**:
  ```
  Find all TextBlock where FontFamily != "Inter" AND FontFamily != "Segoe UI Variable Display"
  ```

#### 5. Spacing & Padding
**Property**: `Margin`, `Padding`
- **Expected**: Multiples of 8 (8px grid system)
- **Valid Values**: `0, 8, 16, 24, 32, 40, 48, 56, 64`
- **Audit Command**:
  ```
  Find elements with non-conforming Margin/Padding values
  ```

#### 6. Border Thickness
**Property**: `BorderThickness`
- **Expected**: `1` for separators, `2` for focus states
- **Location**: All `Border` elements
- **Audit Command**:
  ```
  Find all Border where BorderThickness != "1" AND BorderThickness != "2"
  ```

---

## MainWindow Specific Audit

### Required Properties
```xml
<Window x:Class="Management.Presentation.MainWindow"
        UseLayoutRounding="True"
        SnapsToDevicePixels="True"
        Background="{DynamicResource WindowBg}"
        ...>
```

### Checklist
- [ ] `UseLayoutRounding="True"` on root Window
- [ ] `SnapsToDevicePixels="True"` on root Window
- [ ] `Background="{DynamicResource WindowBg}"` (not hardcoded)
- [ ] All child `Border` elements have `CornerRadius` defined
- [ ] All `TextBlock` elements use `{DynamicResource TextPrimary/Secondary/Tertiary}`
- [ ] No hardcoded color values anywhere in XAML
- [ ] All `Margin` and `Padding` values are multiples of 8
- [ ] `FontFamily` is either `Inter` or `Segoe UI Variable Display`

---

## Automated Audit Script (PowerShell)

```powershell
# Find all XAML files
$xamlFiles = Get-ChildItem -Path ".\Management.Presentation" -Filter "*.xaml" -Recurse

foreach ($file in $xamlFiles) {
    $content = Get-Content $file.FullName -Raw
    
    # Check for hardcoded colors
    if ($content -match 'Background="#|Foreground="#|BorderBrush="#') {
        Write-Warning "Hardcoded color found in: $($file.Name)"
    }
    
    # Check for UseLayoutRounding
    if ($content -notmatch 'UseLayoutRounding="True"' -and $file.Name -like "*View.xaml") {
        Write-Warning "Missing UseLayoutRounding in: $($file.Name)"
    }
    
    # Check for SnapsToDevicePixels
    if ($content -notmatch 'SnapsToDevicePixels="True"' -and $file.Name -like "*View.xaml") {
        Write-Warning "Missing SnapsToDevicePixels in: $($file.Name)"
    }
}
```

---

## Visual Inspection Checklist

### Using XAML Spy
1. Launch application
2. Attach XAML Spy to process
3. Navigate to MainWindow
4. Verify Properties panel shows:
   - `UseLayoutRounding: True`
   - `SnapsToDevicePixels: True`
5. Inspect all `Border` elements for `CornerRadius` consistency
6. Verify all colors resolve to DynamicResource (not static values)

### Using Snoop
1. Launch Snoop
2. Select application process
3. Use "Filter" to find elements with:
   - `UseLayoutRounding == False`
   - `SnapsToDevicePixels == False`
4. Use "Search" to find hardcoded HEX colors
5. Verify visual tree hierarchy matches design system

---

## Common Violations

### ❌ Incorrect
```xml
<Border Background="#1A1A1A" CornerRadius="12" BorderThickness="1.5">
    <TextBlock Text="Hello" Foreground="#FFFFFF" Margin="10"/>
</Border>
```

### ✅ Correct
```xml
<Border Background="{DynamicResource SurfaceBg}" 
        CornerRadius="16" 
        BorderThickness="1"
        SnapsToDevicePixels="True">
    <TextBlock Text="Hello" 
               Foreground="{DynamicResource TextPrimary}" 
               Margin="16"/>
</Border>
```

---

## Reporting Template

```
UI Compliance Audit Report
Date: [Date]
Auditor: [Name]

MainWindow:
- UseLayoutRounding: [✓/✗]
- SnapsToDevicePixels: [✓/✗]
- Background Resource: [✓/✗]
- CornerRadius Consistency: [✓/✗]

Total Violations: [Count]
Critical Issues: [Count]
Warnings: [Count]

Action Items:
1. [Issue description and fix]
2. [Issue description and fix]
```
