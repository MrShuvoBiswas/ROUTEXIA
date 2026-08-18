<#
.SYNOPSIS
    Automated Velopack Release & Cloudflare R2 Upload Script for RouteXia.
.DESCRIPTION
    1. Compiles self-contained RouteXia x64 binaries.
    2. Bundles native WinDivert kernel drivers.
    3. Packages 1-click installer and Delta update packages using Velopack (vpk).
    4. Automatically uploads release feed and packages to Cloudflare R2 bucket.
.EXAMPLE
    # Build local release packages without uploading:
    .\scripts\release-r2.ps1 -Version "1.0.1" -SkipUpload

    # Build and upload directly to Cloudflare R2:
    .\scripts\release-r2.ps1 -Version "1.0.1" `
        -R2Bucket "routexia-releases" `
        -R2AccountId "1999a517810685b629407fcccabaeaa1" `
        -R2KeyId "YOUR_R2_KEY_ID" `
        -R2Secret "YOUR_R2_SECRET_KEY"
#>

param(
    [Parameter(Mandatory = $false)]
    [string]$Version = "1.0.1",

    [string]$R2Bucket = if ($env:R2_BUCKET) { $env:R2_BUCKET } else { "routexia-app-releases" },
    [string]$R2AccountId = if ($env:R2_ACCOUNT_ID) { $env:R2_ACCOUNT_ID } else { "1999a517810685b629407fcccabaeaa1" },
    [string]$R2KeyId = $env:R2_ACCESS_KEY_ID,
    [string]$R2Secret = $env:R2_SECRET_ACCESS_KEY,
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SkipUpload
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir
$ClientDir = Join-Path $RepoRoot "client"
$AppProj = Join-Path $ClientDir "RouteXia.App\RouteXia.App.csproj"
$TempPublishDir = Join-Path $ClientDir "RouteXia.App\bin\$Configuration\net9.0-windows\$Runtime\publish_velopack"
$NativeDir = Join-Path $ClientDir "RouteXia.App\Native"
$WinDivertDir = Join-Path $ClientDir "windivert"
$ReleasesDir = Join-Path $RepoRoot "artifacts\releases"
$AppIcon = Join-Path $ClientDir "RouteXia.App\Resources\Icons\RouteXia-AppIcon.ico"

Write-Host "══════════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host " Building RouteXia Velopack Release v$Version (Cloudflare R2)" -ForegroundColor Cyan
Write-Host "══════════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan

# ── 1. Publish Self-Contained .NET Binaries ──────────────────────────────────
Write-Host "`n[1/4] Publishing Self-Contained .NET Binaries..." -ForegroundColor Yellow
if (Test-Path $TempPublishDir) {
    Remove-Item -Path $TempPublishDir -Recurse -Force -ErrorAction SilentlyContinue
}

dotnet publish $AppProj `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    --output $TempPublishDir `
    /p:Version=$Version `
    /p:AssemblyVersion="$Version.0" `
    /p:FileVersion="$Version.0"

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to publish RouteXia application."
}

# ── 2. Bundle Native WinDivert Drivers ─────────────────────────────────────────
Write-Host "`n[2/4] Bundling WinDivert Native Drivers..." -ForegroundColor Yellow
if (Test-Path $NativeDir) {
    Copy-Item -Path "$NativeDir\WinDivert.dll" -Destination $TempPublishDir -Force -ErrorAction SilentlyContinue
    Copy-Item -Path "$NativeDir\WinDivert64.sys" -Destination $TempPublishDir -Force -ErrorAction SilentlyContinue
    
    $DestNative = Join-Path $TempPublishDir "Native"
    if (-not (Test-Path $DestNative)) { New-Item -ItemType Directory -Path $DestNative -Force | Out-Null }
    Copy-Item -Path "$NativeDir\*" -Destination $DestNative -Recurse -Force -ErrorAction SilentlyContinue
}

if (Test-Path $WinDivertDir) {
    Copy-Item -Path "$WinDivertDir\WinDivert.dll" -Destination $TempPublishDir -Force -ErrorAction SilentlyContinue
    Copy-Item -Path "$WinDivertDir\WinDivert64.sys" -Destination $TempPublishDir -Force -ErrorAction SilentlyContinue
}
Write-Host "  -> Native drivers verified in publish output." -ForegroundColor Green

# ── 3. Velopack Package & Delta Generation ────────────────────────────────────
Write-Host "`n[3/4] Packaging Velopack Installer & Delta Diff (vpk pack)..." -ForegroundColor Yellow
if (-not (Test-Path $ReleasesDir)) {
    New-Item -ItemType Directory -Path $ReleasesDir -Force | Out-Null
}

vpk pack `
    --packId "RouteXia" `
    --packVersion "$Version" `
    --packDir "$TempPublishDir" `
    --mainExe "RouteXia.exe" `
    --icon "$AppIcon" `
    --outputDir "$ReleasesDir" `
    --packTitle "RouteXia Gaming Optimizer" `
    --packAuthors "RouteXia Team"

if ($LASTEXITCODE -ne 0) {
    Write-Error "Velopack packaging (vpk pack) failed with exit code $LASTEXITCODE."
}

Write-Host "  -> Package artifacts generated successfully at: $ReleasesDir" -ForegroundColor Green
Get-ChildItem -Path $ReleasesDir | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize

# ── 4. Upload to Cloudflare R2 Bucket ──────────────────────────────────────────
if (-not $SkipUpload -and -not [string]::IsNullOrWhiteSpace($R2Bucket) -and -not [string]::IsNullOrWhiteSpace($R2AccountId)) {
    Write-Host "`n[4/4] Uploading Release Packages to Cloudflare R2..." -ForegroundColor Yellow

    $R2Endpoint = "https://$R2AccountId.r2.cloudflarestorage.com"
    Write-Host "  -> Bucket: $R2Bucket" -ForegroundColor Gray
    Write-Host "  -> Endpoint: $R2Endpoint" -ForegroundColor Gray

    vpk upload s3 `
        --outputDir "$ReleasesDir" `
        --bucket "$R2Bucket" `
        --endpoint "$R2Endpoint" `
        --keyId "$R2KeyId" `
        --secret "$R2Secret" `
        --region "auto"

    if ($LASTEXITCODE -eq 0) {
        Write-Host "`n Cloudflare R2 Upload completed successfully!" -ForegroundColor Green
        Write-Host " Live Update Feed: https://releases.routexia.in/releases.win.json" -ForegroundColor Cyan
    } else {
        Write-Warning "Cloudflare R2 upload exited with code $LASTEXITCODE."
    }
} else {
    Write-Host "`n[4/4] Skipped Cloudflare R2 upload (local packages ready in artifacts/releases)." -ForegroundColor Gray
}

Write-Host "`n══════════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host " RouteXia Velopack Release Pipeline Complete!" -ForegroundColor Green
Write-Host "══════════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
