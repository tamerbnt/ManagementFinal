$files = Get-ChildItem -Path "Management.Application\DTOs\*.cs" -File
foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $newContent = $content -replace 'namespace Management\.Domain\.DTOs', 'namespace Management.Application.DTOs'
    Set-Content -Path $file.FullName -Value $newContent -NoNewline
}

Write-Host "Updated namespaces in $($files.Count) DTO files"
