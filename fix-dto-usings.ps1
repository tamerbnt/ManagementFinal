# Fix missing using directives in Application layer
$files = Get-ChildItem -Path "Management.Application" -Include *.cs -Recurse | Where-Object { 
    $_.FullName -notlike '*\obj\*' -and 
    $_.FullName -notlike '*\bin\*' 
}

$dtoNames = @('Dto', 'PagedResult', 'SearchRequest')
$count = 0

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    
    # Check if file references any DTO types
    $needsDtoUsing = $false
    foreach ($dtoName in $dtoNames) {
        if ($content -match $dtoName) {
            $needsDtoUsing = $true
            break
        }
    }
    
    # Add using directive if needed and not already present
    if ($needsDtoUsing -and $content -notmatch 'using Management\.Application\.DTOs;') {
        # Find the position after the last using statement
        if ($content -match '(?s)(using [^;]+;\r?\n)+') {
            $lastUsingEnd = $Matches[0]
            $newContent = $content -replace '(?s)(using [^;]+;\r?\n)+', "$lastUsingEnd`using Management.Application.DTOs;`r`n"
            Set-Content -Path $file.FullName -Value $newContent -NoNewline
            $count++
        }
    }
}

Write-Host "Added using directive to $count files"
