$root = "c:\Users\techbox\.gemini\antigravity\scratch\ManagementCopy"

# 1. Rename Directory
Write-Host "Renaming Directory..."
if (Test-Path "$root\Management.Domain\Primitives") {
    Rename-Item "$root\Management.Domain\Primitives" "Common"
}

# 2. Global Replace in all .cs files
Write-Host "Replacing Management.Domain.Primitives with Management.Domain.Common globally..."
Get-ChildItem -Path "$root" -Recurse -Filter *.cs | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    if ($content -match 'Management.Domain.Primitives') {
        $newContent = $content -replace 'Management.Domain.Primitives', 'Management.Domain.Common'
        Set-Content -Path $_.FullName -Value $newContent
    }
}
