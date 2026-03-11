param(
    [string]$JsonPath = "C:\Users\techbox\.gemini\antigravity\scratch\ManagementCopy\cs_replacements.json",
    [string]$PresentationPath = "c:\Users\techbox\.gemini\antigravity\scratch\ManagementCopy\Management.Presentation"
)

$replacementsByFile = Get-Content $JsonPath -Raw | ConvertFrom-Json

foreach ($fileProp in $replacementsByFile.PSObject.Properties) {
    $file = $fileProp.Name
    $fullPath = Join-Path $PresentationPath $file
    if (-not (Test-Path $fullPath)) {
        continue
    }
    
    $fileText = Get-Content $fullPath -Raw
    if ($null -eq $fileText) { continue }
    $originalText = $fileText
    
    $reps = $fileProp.Value
    if ($null -eq $reps) { continue }
    
    # Force array
    if ($reps -isnot [array]) {
        $reps = @($reps)
    }
    
    foreach ($rep in $reps) {
        if ($null -eq $rep) { continue }
        $val = $rep.Val
        $key = $rep.Key
        $type = $rep.Type
        
        if ([string]::IsNullOrWhiteSpace($val)) { continue }
        
        $toReplace = "_terminologyService.GetTerm(`"$key`")"
        $toFindStr = "`"$val`""
        
        if ($fileText.Contains($toFindStr)) {
            $fileText = $fileText.Replace($toFindStr, $toReplace)
            Write-Host "Replaced '$val' in $file"
        }
    }
    
    if ($fileText -ne $originalText) {
        [System.IO.File]::WriteAllText($fullPath, $fileText, [System.Text.Encoding]::UTF8)
        Write-Host "=> Patched C# ViewModel: $file"
    }
}
