<#
.SYNOPSIS
    Automated code signing script for RouteXia binaries and installer.
.DESCRIPTION
    Signs RouteXia.exe, managed/native DLLs, and the final Setup installer
    using Signtool.exe with SHA256 and RFC3161 timestamping.
#>

param(
    [string]$CertificatePath,
    [string]$CertificatePassword,
    [string]$CertificateThumbprint,
    [string]$TimestampServer = "http://timestamp.digicert.com",
    [switch]$CreateSelfSigned
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir
$PublishDir = Join-Path $RepoRoot "client\RouteXia.App\bin\x64\Release\net9.0-windows\win-x64\publish"
$InstallerDir = Join-Path $RepoRoot "artifacts\installer"

Write-Host "══════════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host " RouteXia Binary Code Signing Pipeline" -ForegroundColor Cyan
Write-Host "══════════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan

# Locate signtool.exe
$SignTool = Get-ChildItem -Path "C:\Program Files (x86)\Windows Kits\10\bin" -Filter "signtool.exe" -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match "x64" } |
    Select-Object -First 1 -ExpandProperty FullName

if (-not $SignTool) {
    $SignTool = "signtool.exe"
}

# Self-signed development certificate generation
if ($CreateSelfSigned) {
    Write-Host "`n[+] Generating Development Test Certificate..." -ForegroundColor Yellow
    $cert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject "CN=RouteXia Development Test Signer" `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -NotAfter (Get-Date).AddYears(2)
    $CertificateThumbprint = $cert.Thumbprint
    Write-Host "  -> Generated certificate thumbprint: $CertificateThumbprint" -ForegroundColor Green
}

# Collect binaries to sign
$FilesToSign = @()
if (Test-Path $PublishDir) {
    $FilesToSign += Get-ChildItem -Path $PublishDir -Include "RouteXia*.exe", "RouteXia*.dll" -Recurse | Select-Object -ExpandProperty FullName
}
if (Test-Path $InstallerDir) {
    $FilesToSign += Get-ChildItem -Path $InstallerDir -Filter "*.exe" | Select-Object -ExpandProperty FullName
}

if ($FilesToSign.Count -eq 0) {
    Write-Warning "No binaries found to sign. Ensure 'build-release.ps1' has been executed."
    return
}

Write-Host "`n[+] Signing $($FilesToSign.Count) binaries with SHA256..." -ForegroundColor Yellow

foreach ($file in $FilesToSign) {
    $fileName = Split-Path $file -Leaf
    Write-Host "  -> Signing: $fileName" -ForegroundColor Gray

    try {
        if ($CertificatePath -and (Test-Path $CertificatePath)) {
            & $SignTool sign /fd SHA256 /tr $TimestampServer /td SHA256 /f $CertificatePath /p $CertificatePassword $file
        } elseif ($CertificateThumbprint) {
            & $SignTool sign /fd SHA256 /tr $TimestampServer /td SHA256 /sha1 $CertificateThumbprint $file
        } else {
            # PowerShell Set-AuthenticodeSignature fallback
            $cert = Get-ChildItem -Path "Cert:\CurrentUser\My" -CodeSigningCert | Select-Object -First 1
            if ($cert) {
                Set-AuthenticodeSignature -FilePath $file -Certificate $cert -TimestampServer $TimestampServer | Out-Null
            } else {
                Write-Warning "No code signing certificate specified or found in CurrentUser\My."
                break
            }
        }
    } catch {
        Write-Warning "Failed to sign $fileName : $_"
    }
}

Write-Host "`n Code signing execution finished." -ForegroundColor Green
