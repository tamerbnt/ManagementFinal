$root = "c:\Users\techbox\.gemini\antigravity\scratch\ManagementCopy"

# 1. Update Namespaces in moved files
Write-Host "Updating Namespaces in Management.Presentation\Stores..."
Get-ChildItem -Path "$root\Management.Presentation\Stores" -Filter *.cs | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    $newContent = $content -replace 'namespace Management.Application.Stores', 'namespace Management.Presentation.Stores'
    Set-Content -Path $_.FullName -Value $newContent
}

# 2. Update Usings in Presentation and Tests
Write-Host "Updating Usings in Presentation and Tests..."
$paths = @(
    "$root\Management.Presentation",
    "$root\Management.Tests",
    "$root\Management.Tests.Unit"
)
foreach ($path in $paths) {
    if (Test-Path $path) {
        Get-ChildItem -Path $path -Recurse -Filter *.cs | ForEach-Object {
            $content = Get-Content $_.FullName -Raw
            if ($content -match 'using Management.Application.Stores;') {
                $newContent = $content -replace 'using Management.Application.Stores;', 'using Management.Presentation.Stores;'
                Set-Content -Path $_.FullName -Value $newContent
            }
        }
    }
}

# 3. Remove Usings in Application
Write-Host "Removing Usings in Application..."
Get-ChildItem -Path "$root\Management.Application" -Recurse -Filter *.cs | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    if ($content -match 'using Management.Application.Stores;') {
        $newContent = $content -replace 'using Management.Application.Stores;', '' # Empty string or newline? replace typically leaves empty.
        # Clean up empty lines? Simple usage removal might leave blank line, which is fine.
        Set-Content -Path $_.FullName -Value $newContent
    }
}
