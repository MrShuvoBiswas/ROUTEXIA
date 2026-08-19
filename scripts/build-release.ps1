<#
.SYNOPSIS
    Automated build & packaging script for RouteXia production release.
.DESCRIPTION
    Compiles RouteXia in self-contained Release mode for win-x64, bundles native WinDivert driver files,
    and compiles the production Inno Setup installer package.
#>

param(
    [string]$Version = "1.0.0",
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
$WinDivertDir = Join-Path $ClientDir "windivert"
$InstallerScript = Join-Path $RepoRoot "installer\RouteXia.iss"
$ArtifactsDir = Join-Path $RepoRoot "artifacts\installer"

Write-Host "══════════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host " Building RouteXia Production Release v$Version ($Configuration | $Runtime)" -ForegroundColor Cyan
Write-Host "══════════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan

# 1. Publish self-contained .NET application
Write-Host "`n[1/3] Publishing Self-Contained .NET x64 Application..." -ForegroundColor Yellow
if (Test-Path $PublishDir) {
    Remove-Item -Path $PublishDir -Recurse -Force -ErrorAction SilentlyContinue
}

dotnet publish $AppProj `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    --output $PublishDir `
    /p:Version=$Version `
    /p:AssemblyVersion="$Version.0" `
    /p:FileVersion="$Version.0"

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to publish RouteXia application."
}

# 2. Copy native WinDivert driver files to publish directory
Write-Host "`n[2/3] Bundling WinDivert Native Drivers..." -ForegroundColor Yellow

if (Test-Path $NativeDir) {
    Copy-Item -Path "$NativeDir\WinDivert.dll" -Destination $PublishDir -Force -ErrorAction SilentlyContinue
    Copy-Item -Path "$NativeDir\WinDivert64.sys" -Destination $PublishDir -Force -ErrorAction SilentlyContinue
    
    $DestNative = Join-Path $PublishDir "Native"
    if (-not (Test-Path $DestNative)) { New-Item -ItemType Directory -Path $DestNative -Force | Out-Null }
    Copy-Item -Path "$NativeDir\*" -Destination $DestNative -Recurse -Force -ErrorAction SilentlyContinue
}

if (Test-Path $WinDivertDir) {
    Copy-Item -Path "$WinDivertDir\WinDivert.dll" -Destination $PublishDir -Force -ErrorAction SilentlyContinue
    Copy-Item -Path "$WinDivertDir\WinDivert64.sys" -Destination $PublishDir -Force -ErrorAction SilentlyContinue
}

Write-Host "  -> Native WinDivert drivers bundled into publish output." -ForegroundColor Green

# 3. Compile Inno Setup installer
if (-not $SkipInstaller) {
    Write-Host "`n[3/3] Compiling Inno Setup Production Installer..." -ForegroundColor Yellow
    
    if (-not (Test-Path $ArtifactsDir)) {
        New-Item -ItemType Directory -Path $ArtifactsDir -Force | Out-Null
    }

    $InnoCompilerCandidates = @(
        "$env:LOCALAPPDATA\Programs\Antigravity IDE\resources\app\node_modules\innosetup\bin\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )

    $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($cmd -and $cmd.Source) {
        $InnoCompilerCandidates += $cmd.Source
    }

    $InnoCompiler = $InnoCompilerCandidates | Where-Object { Test-Path $_ -PathType Leaf } | Select-Object -First 1

    if ($InnoCompiler) {
        Write-Host "  -> Using Inno Setup Compiler: $InnoCompiler" -ForegroundColor Gray
        & $InnoCompiler "/DMyAppVersion=$Version" $InstallerScript
        if ($LASTEXITCODE -eq 0) {
            Write-Host "`n Installer generated successfully at: $ArtifactsDir" -ForegroundColor Green
            Get-ChildItem -Path $ArtifactsDir -Filter "*.exe" | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
        } else {
            Write-Error "Inno Setup compilation exited with code $LASTEXITCODE."
        }
    } else {
        Write-Warning "Inno Setup (ISCC.exe) not found in standard paths."
        Write-Host "  -> Published binaries ready in: $PublishDir" -ForegroundColor Green
    }
}

Write-Host "`n Build pipeline completed successfully!" -ForegroundColor Green
