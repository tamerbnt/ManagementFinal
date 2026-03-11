Get-ChildItem -Path "c:\Users\techbox\.gemini\antigravity\scratch\ManagementCopy" -Filter *.cs -Recurse | ForEach-Object {
    $content = Get-Content $_.FullName
    $content = $content -replace '\.TenantId', '.FacilityId'
    $content = $content -replace 'TenantId =', 'FacilityId ='
    $content = $content -replace 'TenantId ==', 'FacilityId =='
    Set-Content $_.FullName $content
}
