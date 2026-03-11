$root = "c:\Users\techbox\.gemini\antigravity\scratch\ManagementCopy"
$scratch = "c:\Users\techbox\.gemini\antigravity\scratch"

Get-ChildItem -Path $root -Filter *.cs -Recurse | Where-Object { $_.Length -eq 0 } | ForEach-Object {
    $fileName = $_.Name
    $filePath = $_.FullName
    Write-Host "Searching for: $fileName"
    
    # Exclude ManagementCopy from search to avoid finding itself
    $alternatives = Get-ChildItem -Path $scratch -Filter $fileName -Recurse -File -ErrorAction SilentlyContinue | Where-Object { 
        $_.FullName -notmatch "ManagementCopy" -and $_.Length -gt 0
    }
    
    if ($alternatives) {
        # Prefer GymManagement or Extracted ones
        $best = $alternatives | Sort-Object { 
            if ($_.FullName -match "GymManagement") { 1 }
            elseif ($_.FullName -match "Extracted") { 2 }
            else { 3 }
        } | Select-Object -First 1
        
        Copy-Item $best.FullName $filePath -Force
        Write-Host "Restored $fileName from $($best.FullName)"
    } else {
        Write-Host "COULD NOT RESTORE $fileName"
    }
}
