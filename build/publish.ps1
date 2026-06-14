#Requires -Version 5
<#
.SYNOPSIS
    Publishes OLED Refresher as a single self-contained EXE (no .NET runtime needed on the target PC).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\build\publish.ps1

.NOTES
    Requires the .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0
    Output: <repo>\publish\OledRefresher.exe
#>
param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$root    = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\OledRefresher\OledRefresher.csproj"
$output  = Join-Path $root "publish"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "The .NET SDK was not found. Install .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0"
}

Write-Host "Publishing OLED Refresher ($Configuration / $Runtime)..." -ForegroundColor Cyan

dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -o $output

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

Write-Host ""
Write-Host "Done. Executable:" -ForegroundColor Green
Write-Host "  $(Join-Path $output 'OledRefresher.exe')"
Write-Host ""
Write-Host "Next: run build\Install-OledRefresher.ps1 to install and start it at login."
