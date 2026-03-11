param(
    [string]$PlanPath = "C:\Users\techbox\.gemini\antigravity\brain\e64f79be-3b3e-49dc-9121-63e51f96e8ca\implementation_plan.md",
    [string]$PresentationPath = "c:\Users\techbox\.gemini\antigravity\scratch\ManagementCopy\Management.Presentation"
)

$planLines = Get-Content $PlanPath
$inCs = $false
$currentFile = $null
$dictionary = @{} # Key: String Value, Value: Generated Key
$en_xaml_append = @()

$replacementsByFile = @{}

foreach ($line in $planLines) {
    if ($line.StartsWith("## C# ViewModels")) {
        $inCs = $true
        continue
    }

    if (-not $inCs) { continue }

    if ($line.StartsWith("### ViewModels\")) {
        $currentFile = $line.Substring(4).Trim()
        $replacementsByFile[$currentFile] = @()
    }
    elseif ($line -match '^\|\s*(\d+)\s*\|\s*(Property|UI Method)\s*\(([^)]+)\)\s*\|\s*"(.+?)"\s*\|$') {
        $lineNum = [int]$matches[1]
        $matchType = $matches[2]
        $propOrMethod = $matches[3]
        $val = $matches[4].Replace("\|", "|")
        
        # Generate Key
        $dirName = [System.IO.Path]::GetDirectoryName($currentFile).Replace("ViewModels\", "").Replace("ViewModels", "Global")
        if ([string]::IsNullOrWhiteSpace($dirName)) { $dirName = "Global" }
        $cleanVal = $val -replace '[^a-zA-Z0-9]', ''
        if ($cleanVal.Length -gt 25) { $cleanVal = $cleanVal.Substring(0, 25) }
        
        $key = "Strings.$dirName.$cleanVal"
        $counter = 1
        $baseKey = $key
        while ($dictionary.Values -contains $key -and $dictionary[$val] -ne $key) {
            $key = "$baseKey$counter"
            $counter++
        }
        
        if (-not $dictionary.ContainsKey($val)) {
            $dictionary[$val] = $key
            $escapedXmlVal = [System.Security.SecurityElement]::Escape($val)
            $en_xaml_append += "    <sys:String x:Key=`"$key`">$escapedXmlVal</sys:String>"
        } else {
            $key = $dictionary[$val]
        }
        
        $replacementsByFile[$currentFile] += @{
            LineNum = $lineNum
            Type = $matchType
            PropOrMethod = $propOrMethod
            Val = $val
            Key = $key
        }
    }
}

Write-Host "Generated $($dictionary.Count) unique translation keys for C#."

# Output replacements map for manual patching
$replacementsByFile | ConvertTo-Json -Depth 5 | Out-File "c:\Users\techbox\.gemini\antigravity\scratch\ManagementCopy\cs_replacements.json" -Encoding UTF8

# Dump dictionary to file for manual insertion
if ($en_xaml_append.Count -gt 0) {
    $en_xaml_append | Out-File "c:\Users\techbox\.gemini\antigravity\scratch\ManagementCopy\new_cs_strings.xml" -Encoding UTF8
    Write-Host "New string entries written to new_cs_strings.xml"
}
