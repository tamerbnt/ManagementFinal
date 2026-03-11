param(
    [string]$PlanPath = "C:\Users\techbox\.gemini\antigravity\brain\e64f79be-3b3e-49dc-9121-63e51f96e8ca\implementation_plan.md",
    [string]$PresentationPath = "c:\Users\techbox\.gemini\antigravity\scratch\ManagementCopy\Management.Presentation"
)

$planLines = Get-Content $PlanPath
$currentFile = $null
$dictionary = @{} # Key: String Value, Value: Generated Key
$en_xaml_append = @()

$replacementsByFile = @{}

foreach ($line in $planLines) {
    if ($line.StartsWith("### Views\")) {
        $currentFile = $line.Substring(4).Trim()
        $replacementsByFile[$currentFile] = @()
    }
    elseif ($line -match '^\|\s*(\d+)\s*\|\s*(Attribute|Inline)\s*\(([^)]+)\)\s*\|\s*"(.+?)"\s*\|$') {
        $lineNum = [int]$matches[1]
        $matchType = $matches[2]
        $attrOrTag = $matches[3]
        $val = $matches[4].Replace("\|", "|")
        
        # Generate Key
        $dirName = [System.IO.Path]::GetDirectoryName($currentFile).Replace("Views\", "").Replace("Views", "Global")
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
            AttrOrTag = $attrOrTag
            Val = $val
            Key = $key
        }
    }
}

Write-Host "Generated $($dictionary.Count) unique translation keys."

foreach ($file in $replacementsByFile.Keys) {
    $fullPath = Join-Path $PresentationPath $file
    if (-not (Test-Path $fullPath)) {
        Write-Warning "File not found: $fullPath"
        continue
    }
    
    $fileLines = Get-Content $fullPath
    $reps = $replacementsByFile[$file] | Sort-Object LineNum -Descending
    
    foreach ($rep in $reps) {
        $idx = $rep.LineNum - 1
        $lineText = $fileLines[$idx]
        
        if ($rep.Type -eq "Attribute") {
            # Replace Attribute="..." with Attribute="{DynamicResource Key}"
            $attr = $rep.AttrOrTag
            $val = $rep.Val
            # Need to carefully match the attribute and value
            # e.g. Text="Hello" -> Text="{DynamicResource Strings.Global.Hello}"
            # Because of potential regex chars in $val, we use a simple string replace
            $toFind = "$attr=`"$val`""
            $toReplace = "$attr=`"{DynamicResource $($rep.Key)}`""
            $newLineText = $lineText.Replace($toFind, $toReplace)
            if ($newLineText -eq $lineText) {
                # Try single quotes ? rarely used in WPF but just in case
            }
            $fileLines[$idx] = $newLineText
        }
        elseif ($rep.Type -eq "Inline") {
            # Replace >Hello</Tag> with >{DynamicResource Key}</Tag>
            $tag = $rep.AttrOrTag
            $val = $rep.Val
            $toFind = ">$val</$tag>"
            $toReplace = ">{DynamicResource $($rep.Key)}</$tag>"
            $fileLines[$idx] = $lineText.Replace($toFind, $toReplace)
        }
    }
    
    Set-Content -Path $fullPath -Value $fileLines -Encoding UTF8
    Write-Host "Updated XAML: $file"
}

# Dump dictionary to file for manual insertion
$en_xaml_append | Out-File "c:\Users\techbox\.gemini\antigravity\scratch\ManagementCopy\new_strings.xml" -Encoding UTF8
Write-Host "New string entries written to new_strings.xml"
