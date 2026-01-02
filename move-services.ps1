# Services to move (have DTO dependencies):
$servicesToMove = @(
    "IAccessEventService.cs",
    "IAuthenticationService.cs",
    "IDataDoctorService.cs",
    "IFinanceService.cs",
    "IMemberService.cs",
    "IMembershipPlanService.cs",
    "IProductService.cs",
    "IRegistrationService.cs",
    "IReportingService.cs",
    "IReservationService.cs",
    "ISaleService.cs",
    "ISettingsService.cs",
    "IStaffService.cs",
    "ITurnstileService.cs"
)

# Create target directory
New-Item -ItemType Directory -Force -Path "Management.Application\Services" | Out-Null

# Move each service interface
foreach ($service in $servicesToMove) {
    $sourcePath = "Management.Domain\Services\$service"
    $destPath = "Management.Application\Services\$service"
    
    if (Test-Path $sourcePath) {
        Move-Item -Path $sourcePath -Destination $destPath -Force
        Write-Host "Moved: $service"
    }
}

Write-Host "`nMoved $($servicesToMove.Count) service interfaces"
