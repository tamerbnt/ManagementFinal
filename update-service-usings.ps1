# Update using directives across the solution
$files = Get-ChildItem -Path . -Include *.cs -Recurse | Where-Object { 
    $_.FullName -notlike '*\obj\*' -and 
    $_.FullName -notlike '*\bin\*' 
}

$movedServices = @(
    "IAccessEventService",
    "IAuthenticationService",
    "IDataDoctorService",
    "IFinanceService",
    "IMemberService",
    "IMembershipPlanService",
    "IProductService",
    "IRegistrationService",
    "IReportingService",
    "IReservationService",
    "ISaleService",
    "ISettingsService",
    "IStaffService",
    "ITurnstileService"
)

$count = 0
foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $modified = $false
    
    foreach ($service in $movedServices) {
        if ($content -match $service) {
            # Add Application.Services using if not present
            if ($content -notmatch 'using Management\.Application\.Services;') {
                # Find the last using statement
                if ($content -match '(using [^;]+;)') {
                    $content = $content -replace '(using [^;]+;)(\r?\n)', "`$1`$2using Management.Application.Services;`$2"
                    $modified = $true
                }
            }
        }
    }
    
    if ($modified) {
        Set-Content -Path $file.FullName -Value $content -NoNewline
        $count++
    }
}

Write-Host "Updated $count files with Application.Services using directive"
