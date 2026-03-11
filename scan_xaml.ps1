$rootDir = "c:\Users\techbox\.gemini\antigravity\scratch\ManagementCopy\Management.Presentation\Views"
$outputFile = "c:\Users\techbox\.gemini\antigravity\scratch\ManagementCopy\xaml_hardcoded_report_ps.txt"
$attributes = @("Content", "Header", "Text", "ToolTip", "Placeholder")

$report = New-Object System.Collections.Generic.List[string]

Get-ChildItem -Path $rootDir -Filter *.xaml -Recurse | ForEach-Object {
    $file = $_
    $lines = Get-Content $file.FullName
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        foreach ($attr in $attributes) {
            # Regex to find Attr="Value" where Value does not start with {
            $pattern = "$attr=""([^\{][^""]*)"""
            if ($line -match $pattern) {
                $val = $matches[1]
                if ($val.Trim()) {
                    $report.Add("$($file.Name), $($i + 1), $val, Hardcoded XAML")
                }
            }
        }
    }
}

$report | Out-File -FilePath $outputFile -Encoding utf8
Write-Host "Report generated at $outputFile"
