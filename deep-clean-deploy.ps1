$ErrorActionPreference = "Stop"
Write-Host "=== TITAN DEEP CLEAN DEPLOY PIPELINE ===" -ForegroundColor Cyan

# 1. Violent Process Termination
Write-Host "`n[1/5] Terminating stale processes..." -ForegroundColor Yellow
$processes = @("Titan.Client", "Titan", "GymOS", "Management.Presentation")
foreach ($proc in $processes) {
    Get-Process $proc -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Write-Host "  Killed $proc (if existing)"
}
Start-Sleep -Seconds 2 # Wait for locks to release

# 2. Obliterate AppData (The core of the paradox fix)
Write-Host "`n[2/5] Obliterating stale AppData..." -ForegroundColor Yellow
$localAppData = [Environment]::GetFolderPath("LocalApplicationData")
$folders = @("$localAppData\Titan", "$localAppData\TitanManagementSystem", "$localAppData\GymOS")

foreach ($folder in $folders) {
    if (Test-Path $folder) {
        Remove-Item -Path $folder -Recurse -Force
        Write-Host "  Nuked: $folder" -ForegroundColor Green
    } else {
        Write-Host "  Skipped (Not Found): $folder" -ForegroundColor DarkGray
    }
}

# 3. Clean and Build
Write-Host "`n[3/5] Cleaning and Compiling..." -ForegroundColor Yellow
Set-Location "c:\Users\techbox\.gemini\antigravity\ManagementBackup1234"
dotnet clean "Management.Presentation\Management.Presentation.csproj" -c Release
dotnet build "Management.Presentation\Management.Presentation.csproj" -c Release
if ($LASTEXITCODE -ne 0) { throw "Build failed!" }
Write-Host "  Build Successful" -ForegroundColor Green

# 4. Clean Publish
Write-Host "`n[4/5] Publishing..." -ForegroundColor Yellow
$publishDir = "Management.Presentation\bin\Release\net8.0-windows\win-x64\publish"
if (Test-Path $publishDir) { Remove-Item -Path $publishDir -Recurse -Force }
dotnet publish "Management.Presentation\Management.Presentation.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false
if ($LASTEXITCODE -ne 0) { throw "Publish failed!" }
Write-Host "  Publish Successful" -ForegroundColor Green

# 5. Velopack Generation
Write-Host "`n[5/5] Generating Velopack Installer (v1.2.3)..." -ForegroundColor Yellow
vpk pack -u TitanManagementSystem -v 1.2.3 -p "$publishDir" -e Titan.Client.exe
if ($LASTEXITCODE -ne 0) { throw "Velopack pack failed!" }

Write-Host "`n=== DEPLOYMENT PIPELINE COMPLETE ===" -ForegroundColor Green
Write-Host "The Setup.exe for version 1.2.3 is ready in the Releases folder." -ForegroundColor Cyan
Write-Host "USER ACTION REQUIRED: You MUST run the new Setup.exe before testing." -ForegroundColor Red
