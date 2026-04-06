param (
    [string]$Version = "1.2.0.0"
)

Write-Host "========================================="
Write-Host " Building Luxurya Installer v$Version "
Write-Host "========================================="

$PublishDir = "Management.Presentation\bin\Release\net8.0-windows\win-x64\publish"

# 1. Build and Publish Application
Write-Host "`n[1/2] Publishing Application..."
# Using explicit parameters to ensure strict velopack compatibility (No SingleFile so delta updates work)
dotnet publish "Management.Presentation\Management.Presentation.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=true `
    -p:Version=$Version `
    -o $PublishDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed. Exiting." -ForegroundColor Red
    exit 1
}

# 2. Pack using Velopack (vpk)
Write-Host "`n[2/2] Running Velopack (vpk) Pack..."
# Note: we use our assets folder. Ensure assets/app.ico exists.
vpk pack `
    -u LuxuryaManagement `
    -v $Version `
    -p $PublishDir `
    -o "releases" `
    -e "Luxurya.Client.exe" `
    -i "assets\app.ico"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Velopack pack failed. Exiting." -ForegroundColor Red
    exit 1
}

Write-Host "`n========================================="
Write-Host " Success! Installer is ready in 'releases' directory." -ForegroundColor Green
Write-Host "========================================="
