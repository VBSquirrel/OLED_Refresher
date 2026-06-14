#Requires -Version 5
<#
.SYNOPSIS
    Installs OLED Refresher for the current user and starts it.

.DESCRIPTION
    Copies the published EXE to %LOCALAPPDATA%\OledRefresher, registers it to start at sign-in
    (HKCU Run key), and launches it. No administrator rights required.

    Build first with build\publish.ps1 (or point -ExePath at an existing OledRefresher.exe).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\build\Install-OledRefresher.ps1
#>
param(
    [string]$ExePath
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
if (-not $ExePath) {
    $ExePath = Join-Path $root "publish\OledRefresher.exe"
}

if (-not (Test-Path $ExePath)) {
    throw "Could not find OledRefresher.exe at '$ExePath'. Run build\publish.ps1 first."
}

$installDir = Join-Path $env:LOCALAPPDATA "OledRefresher"
New-Item -ItemType Directory -Force -Path $installDir | Out-Null

$destExe = Join-Path $installDir "OledRefresher.exe"

# Stop a running instance so the file is not locked during copy.
Get-Process -Name "OledRefresher" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 400

Copy-Item -Path $ExePath -Destination $destExe -Force
Write-Host "Installed to: $destExe" -ForegroundColor Green

# Register run-at-login (the app also self-manages this, but set it now for immediate effect).
$runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
Set-ItemProperty -Path $runKey -Name "OledRefresher" -Value ('"{0}"' -f $destExe)
Write-Host "Registered to start at sign-in." -ForegroundColor Green

Start-Process -FilePath $destExe
Write-Host ""
Write-Host "OLED Refresher is running. Look for the tray icon (near the clock)." -ForegroundColor Cyan
Write-Host "Right-click the tray icon for options, or edit settings at:"
Write-Host "  $env:APPDATA\OledRefresher\config.json"
