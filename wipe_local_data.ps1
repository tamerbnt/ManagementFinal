# Multi-Facility Hardening Verification - Local Reset Script
# This script wipes all local data to ensure a clean "First Launch" experience 
# for verifying the new registration and multi-facility auto-provisioning logic.

$ErrorActionPreference = "SilentlyContinue"

Write-Host "--- Multi-Facility Infrastructure Reset ---" -ForegroundColor Cyan

# 1. Stop any running instances of the app
Write-Host "Stopping app processes..."
Stop-Process -Name "Management.Presentation" -Force
Stop-Process -Name "Titan.Client" -Force

# 2. Local App Data (Secure Storage)
$localAppData = Join-Path $env:LOCALAPPDATA "GymOS"
if (Test-Path $localAppData) {
    Write-Host "Wiping LocalAppData: $localAppData"
    Remove-Item -Path "$localAppData\*" -Recurse -Force
}

# 3. Program Data (Configuration Service)
$programData = Join-Path $env:ProgramData "ManagementApp"
if (Test-Path $programData) {
    Write-Host "Wiping ProgramData: $programData"
    Remove-Item -Path "$programData\*" -Recurse -Force
}

# 4. Local Database (SQLite)
$dbFile = "c:\Users\techbox\.gemini\antigravity\scratch\ManagementCopy\Management.Presentation\bin\Debug\net8.0-windows\Management.db"
if (Test-Path $dbFile) {
    Write-Host "Wiping Local Database: $dbFile"
    Remove-Item -Path $dbFile -Force
}

# 5. Application Folder Database (Backup path)
$rootDbFile = "c:\Users\techbox\.gemini\antigravity\scratch\ManagementCopy\Management.db"
if (Test-Path $rootDbFile) {
    Write-Host "Wiping Root Database: $rootDbFile"
    Remove-Item -Path $rootDbFile -Force
}

# 6. Logs
$logFolder = "c:\Users\techbox\.gemini\antigravity\scratch\ManagementCopy\Management.Presentation\bin\Debug\net8.0-windows\logs"
if (Test-Path $logFolder) {
    Write-Host "Cleaning Logs..."
    Remove-Item -Path "$logFolder\*" -Force
}

Write-Host "`nEnvironment Clean! Please restart the application to begin clean registration." -ForegroundColor Green
