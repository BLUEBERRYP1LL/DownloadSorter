# DownloadSorter Code Signing Script
# Run this after building to sign the executable
#
# Prerequisites:
# 1. A code signing certificate (.pfx file)
# 2. Windows SDK installed (for signtool.exe)
#
# Usage:
#   .\sign-windows.ps1 -CertPath "path\to\cert.pfx" -CertPassword "password"

param(
    [Parameter(Mandatory=$true)]
    [string]$CertPath,

    [Parameter(Mandatory=$true)]
    [string]$CertPassword,

    [string]$TimestampServer = "http://timestamp.digicert.com",

    [string]$ExePath = "..\publish\sorter.exe"
)

$ErrorActionPreference = "Stop"

# Find signtool
$SignTool = Get-ChildItem -Path "C:\Program Files (x86)\Windows Kits" -Recurse -Filter "signtool.exe" |
    Where-Object { $_.FullName -match "x64" } |
    Select-Object -First 1 -ExpandProperty FullName

if (-not $SignTool) {
    Write-Error "signtool.exe not found. Install Windows SDK."
    exit 1
}

Write-Host "Using signtool: $SignTool"

# Verify certificate exists
if (-not (Test-Path $CertPath)) {
    Write-Error "Certificate not found: $CertPath"
    exit 1
}

# Verify executable exists
if (-not (Test-Path $ExePath)) {
    Write-Error "Executable not found: $ExePath"
    exit 1
}

Write-Host "Signing: $ExePath"

# Sign the executable
& $SignTool sign `
    /f $CertPath `
    /p $CertPassword `
    /fd SHA256 `
    /tr $TimestampServer `
    /td SHA256 `
    /v `
    $ExePath

if ($LASTEXITCODE -ne 0) {
    Write-Error "Signing failed!"
    exit 1
}

Write-Host ""
Write-Host "Successfully signed: $ExePath" -ForegroundColor Green

# Verify signature
Write-Host ""
Write-Host "Verifying signature..."
& $SignTool verify /pa /v $ExePath

if ($LASTEXITCODE -ne 0) {
    Write-Error "Signature verification failed!"
    exit 1
}

Write-Host ""
Write-Host "Signature verified successfully!" -ForegroundColor Green
