$rootDir = "c:\Users\techbox\.gemini\antigravity\scratch\ManagementCopy\Management.Presentation"
$outputFile = "c:\Users\techbox\.gemini\antigravity\scratch\ManagementCopy\csharp_hardcoded_report.txt"
$report = New-Object System.Collections.Generic.List[string]

# Targeted regex for common user-facing string patterns in C#
# 1. Dialogs: ShowConfirmationAsync("...", "...")
# 2. Toast/Notifications: ShowError("..."), ShowSuccess("..."), ShowWarning(...)
# 3. Simple assignments: Property = "..." where it looks like text (not IDs or internal tags)
# 4. Throwing user-facing exceptions: throw new Exception("...")

$patterns = @(
    'ShowConfirmationAsync\(\s*"([^"]+)"',
    'ShowConfirmationAsync\(\s*[^,]+,\s*"([^"]+)"',
    'ShowError\(\s*"([^"]+)"',
    'ShowSuccess\(\s*"([^"]+)"',
    'ShowWarning\(\s*"([^"]+)"',
    'Title\s*=\s*"([^"]+)"',
    'AddButtonText\s*=\s*"([^"]+)"',
    'WindowName\s*=\s*"([^"]+)"',
    'Message\s*=\s*"([^"]+)"',
    'Header\s*=\s*"([^"]+)"',
    'Label\s*=\s*"([^"]+)"'
)

Get-ChildItem -Path $rootDir -Include *.cs -Recurse | Where-Object { $_.FullName -match "ViewModels|Services" } | ForEach-Object {
    $file = $_
    $lines = Get-Content $file.FullName
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        foreach ($p in $patterns) {
            if ($line -match $p) {
                $val = $matches[1]
                if ($val.Trim()) {
                    $report.Add("$($file.Name), $($i + 1), $val, Hardcoded C#")
                }
            }
        }
    }
}

$report | Out-File -FilePath $outputFile -Encoding utf8
Write-Host "Report generated at $outputFile"
