$files = Get-ChildItem -Path . -Recurse -Include *.cs
foreach ($file in $files) {
    $content = Get-Content $file.FullName
    $seen = $false
    $newContent = @()
    foreach ($line in $content) {
        if ($line.Trim() -eq "using Management.Application.Services;") {
            if (-not $seen) {
                $newContent += $line
                $seen = $true
            }
            # else skip
        }
        else {
            $newContent += $line
        }
    }
    $newContent | Set-Content $file.FullName
}
Write-Host "Cleanup complete."
