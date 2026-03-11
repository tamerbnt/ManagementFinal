Get-ChildItem -Path "c:\Users\techbox\.gemini\antigravity\scratch\ManagementCopy" -Filter *.cs -Recurse | Where-Object { $_.Length -eq 0 } | ForEach-Object {
    $original = $_.FullName.Replace("ManagementCopy", "GymManagement")
    if (Test-Path $original) {
        Copy-Item $original $_.FullName -Force
        Write-Host "Restored: $($_.FullName)"
    } else {
        Write-Host "NOT FOUND: $original"
    }
}
