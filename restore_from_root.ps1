$root = "c:\Users\techbox\.gemini\antigravity\scratch\ManagementCopy\Management.Presentation\ViewModels"

# Get all .cs files in the root ViewModels folder
$rootFiles = Get-ChildItem -Path $root -Filter *ViewModel.cs -File | Where-Object { $_.Length -gt 100 }

foreach ($rootFile in $rootFiles) {
    # Find any files with the same name in subfolders of ViewModels
    $subfolderFile = Get-ChildItem -Path $root -Filter $rootFile.Name -Recurse -File | 
                     Where-Object { $_.FullName -ne $rootFile.FullName }
    
    if ($subfolderFile) {
        if ($subfolderFile.Length -lt 100) {
            Write-Host "Restoring $($subfolderFile.FullName) from $($rootFile.FullName)"
            Copy-Item $rootFile.FullName $subfolderFile.FullName -Force
            # We can't delete the root copy yet just in case, but we mark it for deletion
            Write-Host "SUCCESS: Overwrote empty file in subfolder."
        } else {
            Write-Host "WARNING: Subfolder file $($subfolderFile.FullName) is NOT empty ($($subfolderFile.Length) bytes). Skipping."
        }
    } else {
        Write-Host "INFO: No subfolder match found for $($rootFile.Name). Keeping in root for now."
    }
}
