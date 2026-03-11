$enFile = "c:\Users\techbox\.gemini\antigravity\scratch\ManagementCopy\Management.Presentation\Resources\Localization\Strings.en.xaml"
$frFile = "c:\Users\techbox\.gemini\antigravity\scratch\ManagementCopy\Management.Presentation\Resources\Localization\Strings.fr.xaml"
$arFile = "c:\Users\techbox\.gemini\antigravity\scratch\ManagementCopy\Management.Presentation\Resources\Localization\Strings.ar.xaml"
$outputFile = "c:\Users\techbox\.gemini\antigravity\scratch\ManagementCopy\resource_comparison_report.txt"

function Get-Keys($path) {
    $content = Get-Content $path
    $keys = @{}
    foreach ($line in $content) {
        if ($line -match 'x:Key="([^"]+)"') {
            $keys[$matches[1]] = $true
        }
    }
    return $keys
}

$enKeys = Get-Keys $enFile
$frKeys = Get-Keys $frFile
$arKeys = Get-Keys $arFile

$allKeys = @{}
foreach ($k in $enKeys.Keys) { $allKeys[$k] = $true }
foreach ($k in $frKeys.Keys) { $allKeys[$k] = $true }
foreach ($k in $arKeys.Keys) { $allKeys[$k] = $true }

$report = New-Object System.Collections.Generic.List[string]
$report.Add("Resource Key Comparison Report")
$report.Add("-----------------------------")
$report.Add("Total unique keys across all files: $($allKeys.Count)")
$report.Add("Keys in EN: $($enKeys.Count)")
$report.Add("Keys in FR: $($frKeys.Count)")
$report.Add("Keys in AR: $($arKeys.Count)")
$report.Add("")

$report.Add("Missing in EN:")
foreach ($k in $allKeys.Keys) { if (-not $enKeys.ContainsKey($k)) { $report.Add("  $k") } }
$report.Add("")

$report.Add("Missing in FR:")
foreach ($k in $allKeys.Keys) { if (-not $frKeys.ContainsKey($k)) { $report.Add("  $k") } }
$report.Add("")

$report.Add("Missing in AR:")
foreach ($k in $allKeys.Keys) { if (-not $arKeys.ContainsKey($k)) { $report.Add("  $k") } }

$report | Out-File -FilePath $outputFile -Encoding utf8
Write-Host "Report generated at $outputFile"
