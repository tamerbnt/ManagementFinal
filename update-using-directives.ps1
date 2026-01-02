$files = Get-ChildItem -Path . -Include *.cs -Recurse | Where-Object { 
    $_.FullName -notlike '*\obj\*' -and 
    $_.FullName -notlike '*\bin\*' 
}

$count = 0
foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    if ($content -match 'using Management\.Domain\.DTOs;') {
        $newContent = $content -replace 'using Management\.Domain\.DTOs;', 'using Management.Application.DTOs;'
        Set-Content -Path $file.FullName -Value $newContent -NoNewline
        $count++
    }
}

Write-Host "Updated $count files with new using directive"
