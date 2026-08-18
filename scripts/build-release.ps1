<#
.SYNOPSIS
    Automated build & packaging script for RouteXia production release.
.DESCRIPTION
    Compiles RouteXia in Release mode for win-x64, bundles native WinDivert driver files,
    and compiles the Inno Setup installer package.
#>

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir
$ClientDir = Join-Path $RepoRoot "client"
$AppProj = Join-Path $ClientDir "RouteXia.App\RouteXia.App.csproj"
$PublishDir = Join-Path $ClientDir "RouteXia.App\bin\$Configuration\net9.0-windows\$Runtime\publish"
$NativeDir = Join-Path $ClientDir "RouteXia.App\Native"
$InstallerScript = Join-Path $RepoRoot "installer\RouteXia.iss"
$ArtifactsDir = Join-Path $RepoRoot "artifacts\installer"

Write-Host "══════════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host " Building RouteXia Production Release ($Configuration | $Runtime)" -ForegroundColor Cyan
Write-Host "══════════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan

# 1. Publish .NET application
Write-Host "`n[1/3] Publishing .NET x64 Application..." -ForegroundColor Yellow
dotnet publish $AppProj `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained false `
    --output $PublishDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to publish RouteXia application."
}

# 2. Copy native WinDivert driver files to publish directory
Write-Host "`n[2/3] Bundling WinDivert Native Drivers..." -ForegroundColor Yellow
if (Test-Path $NativeDir) {
    try {
        Copy-Item -Path "$NativeDir\WinDivert.dll" -Destination $PublishDir -Force -ErrorAction SilentlyContinue
        Copy-Item -Path "$NativeDir\WinDivert64.sys" -Destination $PublishDir -Force -ErrorAction SilentlyContinue
        
        $DestNative = Join-Path $PublishDir "Native"
        if (-not (Test-Path $DestNative)) { New-Item -ItemType Directory -Path $DestNative | Out-Null }
        Copy-Item -Path "$NativeDir\*" -Destination $DestNative -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "  -> WinDivert drivers bundled into publish output." -ForegroundColor Green
    } catch {
        Write-Host "  -> WinDivert drivers already present in publish output." -ForegroundColor Green
    }
} else {
    Write-Warning "Native drivers directory not found at $NativeDir."
}

# 3. Compile Inno Setup installer if available
if (-not $SkipInstaller) {
    Write-Host "`n[3/3] Compiling Inno Setup Installer..." -ForegroundColor Yellow
    
    if (-not (Test-Path $ArtifactsDir)) {
        New-Item -ItemType Directory -Path $ArtifactsDir -Force | Out-Null
    }

    $InnoCompiler = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
        "ISCC.exe"
    ) | Where-Object { Test-Path $_ -PathType Leaf } | Select-Object -First 1

    if ($InnoCompiler) {
        Write-Host "  -> Using Inno Setup Compiler: $InnoCompiler" -ForegroundColor Gray
        & $InnoCompiler $InstallerScript
        if ($LASTEXITCODE -eq 0) {
            Write-Host "`n Installer generated successfully at: $ArtifactsDir" -ForegroundColor Green
        } else {
            Write-Warning "Inno Setup compilation exited with code $LASTEXITCODE."
        }
    } else {
        Write-Host "  -> Inno Setup (ISCC.exe) not found in standard paths." -ForegroundColor Gray
        Write-Host "  -> Install Inno Setup 6 from https://jrsoftware.org/isdl.php to compile the .exe installer." -ForegroundColor Gray
        Write-Host "  -> Standalone published binaries are ready in: $PublishDir" -ForegroundColor Green
    }
}

Write-Host "`n Build pipeline completed successfully!" -ForegroundColor Green
