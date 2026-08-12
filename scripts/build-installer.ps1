#Requires -Version 5.1
<#
.SYNOPSIS
  Publishes Meta Quest Tray Tool (self-contained win-x64) and builds the Inno Setup installer.

.EXAMPLE
  .\scripts\build-installer.ps1
#>
[CmdletBinding()]
param(
    [string] $Version = "",
    [switch] $SkipPublish,
    [string] $Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

function Get-ProjectVersion {
    $props = Join-Path $root "Directory.Build.props"
    if (-not (Test-Path $props)) { return "1.0.0" }
    [xml] $xml = Get-Content $props
    $node = $xml.SelectSingleNode("//Version")
    if ($node -and $node.InnerText.Trim()) { return $node.InnerText.Trim() }
    return "1.0.0"
}

function Find-ISCC {
    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
        "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe"
    )
    foreach ($path in $candidates) {
        if (Test-Path $path) { return $path }
    }
    $cmd = Get-Command iscc.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-ProjectVersion
}

$publishDir = Join-Path $root "publish\win-x64"
$iss = Join-Path $root "installer\MetaQuestTrayTool.iss"
$csproj = Join-Path $root "src\MetaQuestTrayTool\MetaQuestTrayTool.csproj"
$distDir = Join-Path $root "dist"

Write-Host "Meta Quest Tray Tool installer build" -ForegroundColor Cyan
Write-Host "  Version : $Version"
Write-Host "  Publish : $publishDir"
Write-Host ""

if (-not $SkipPublish) {
    Write-Host "Publishing self-contained win-x64 ($Configuration)..." -ForegroundColor Yellow
    if (Test-Path $publishDir) {
        Remove-Item $publishDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

    & dotnet publish $csproj `
        -c $Configuration `
        -r win-x64 `
        --self-contained true `
        -p:PublishReadyToRun=true `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -p:Version=$Version `
        -o $publishDir

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }

    $exe = Join-Path $publishDir "MetaQuestTrayTool.exe"
    $adb = Join-Path $publishDir "platform-tools\adb.exe"
    if (-not (Test-Path $exe)) { throw "Publish missing MetaQuestTrayTool.exe" }
    if (-not (Test-Path $adb)) { throw "Publish missing platform-tools\adb.exe" }
    Write-Host "Publish OK." -ForegroundColor Green
}
else {
    if (-not (Test-Path (Join-Path $publishDir "MetaQuestTrayTool.exe"))) {
        throw "SkipPublish set but $publishDir is empty. Run without -SkipPublish first."
    }
    Write-Host "Skipping publish (using existing folder)." -ForegroundColor DarkYellow
}

$iscc = Find-ISCC
if (-not $iscc) {
    Write-Host ""
    Write-Host "Inno Setup 6 was not found." -ForegroundColor Red
    Write-Host "Install it, then re-run this script:" -ForegroundColor Yellow
    Write-Host '  winget install --id JRSoftware.InnoSetup -e'
    Write-Host '  # or: choco install innosetup -y'
    Write-Host ""
    Write-Host "Publish folder is ready at: $publishDir"
    exit 2
}

Write-Host "Compiling installer with Inno Setup..." -ForegroundColor Yellow
New-Item -ItemType Directory -Path $distDir -Force | Out-Null

# Paths in the .iss are relative to the .iss file location (installer\).
$publishForIss = "..\publish\win-x64"
& $iscc `
    "/DMyAppVersion=$Version" `
    "/DPublishDir=$publishForIss" `
    $iss

if ($LASTEXITCODE -ne 0) {
    throw "ISCC failed with exit code $LASTEXITCODE"
}

$setup = Join-Path $distDir "MetaQuestTrayTool-Setup-$Version.exe"
if (-not (Test-Path $setup)) {
    throw "Expected installer not found: $setup"
}

$sizeMb = [math]::Round((Get-Item $setup).Length / 1MB, 1)
Write-Host ""
Write-Host "Installer ready:" -ForegroundColor Green
Write-Host "  $setup ($sizeMb MB)"
Write-Host ""
Write-Host "Users can double-click that Setup.exe to install (OTT-style)."
