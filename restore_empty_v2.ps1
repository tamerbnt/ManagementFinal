$root = "c:\Users\techbox\.gemini\antigravity\scratch\ManagementCopy"
$originalRoot = "c:\Users\techbox\.gemini\antigravity\scratch\GymManagement"

Get-ChildItem -Path $root -Filter *.cs -Recurse | Where-Object { $_.Length -eq 0 } | ForEach-Object {
    $relPath = $_.FullName.Substring($root.Length + 1)
    
    # Map Management.Something to GymManagement.Something
    $parts = $relPath.Split("\")
    if ($parts[0] -match "^Management\.(.*)") {
        $parts[0] = "GymManagement." + $matches[1]
    }
    $mappedRelPath = [string]::Join("\", $parts)
    $original = Join-Path $originalRoot $mappedRelPath
    
    if (Test-Path $original) {
        Copy-Item $original $_.FullName -Force
        Write-Host "Restored: $($_.FullName) from $original"
    } else {
        # Try without the "Gym" prefix in the middle parts if any (unlikely but safe)
        Write-Host "NOT FOUND: $original"
    }
}
