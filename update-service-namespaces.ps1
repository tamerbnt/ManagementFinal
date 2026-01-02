# Update namespaces in moved service files
$files = Get-ChildItem -Path "Management.Application\Services\I*Service.cs" -File

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $newContent = $content -replace 'namespace Management\.Domain\.Services', 'namespace Management.Application.Services'
    Set-Content -Path $file.FullName -Value $newContent -NoNewline
}

Write-Host "Updated namespaces in $($files.Count) service files"
