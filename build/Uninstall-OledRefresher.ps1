#Requires -Version 5
<#
.SYNOPSIS
    Stops and removes OLED Refresher for the current user.

.PARAMETER RemoveSettings
    Also delete %APPDATA%\OledRefresher (config.json and log.txt).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\build\Uninstall-OledRefresher.ps1 -RemoveSettings
#>
param(
    [switch]$RemoveSettings
)

$ErrorActionPreference = "SilentlyContinue"

Get-Process -Name "OledRefresher" | Stop-Process -Force
Start-Sleep -Milliseconds 400

$runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
Remove-ItemProperty -Path $runKey -Name "OledRefresher" -ErrorAction SilentlyContinue

$installDir = Join-Path $env:LOCALAPPDATA "OledRefresher"
if (Test-Path $installDir) { Remove-Item -Path $installDir -Recurse -Force }

if ($RemoveSettings) {
    $dataDir = Join-Path $env:APPDATA "OledRefresher"
    if (Test-Path $dataDir) { Remove-Item -Path $dataDir -Recurse -Force }
    Write-Host "Removed app, autostart entry and settings." -ForegroundColor Green
} else {
    Write-Host "Removed app and autostart entry. Settings kept at $env:APPDATA\OledRefresher." -ForegroundColor Green
}
