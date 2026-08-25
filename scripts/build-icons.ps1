# Regenerate App.ico + app-icon.png from a square master PNG (1024+ recommended).
param(
    [string]$Master = (Join-Path $PSScriptRoot "..\assets\app-icon-master.png"),
    [string]$OutDir = (Join-Path $PSScriptRoot "..\src\MetaQuestTrayTool\Resources\Icons")
)

$ErrorActionPreference = "Stop"
if (-not (Test-Path $Master)) {
    throw "Master PNG not found: $Master"
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$iconPath = Join-Path $OutDir "App.ico"
$pngPath = Join-Path $OutDir "app-icon.png"

# Sharpen slightly so neon edges survive 16px tray scaling.
magick $Master `
    -resize 512x512 `
    -unsharp 0x0.75+0.5+0.008 `
    $pngPath

magick $Master `
    -resize 256x256 `
    -unsharp 0x0.75+0.5+0.008 `
    -define icon:auto-resize=256,128,64,48,32,24,20,16 `
    $iconPath

Write-Host "Wrote $pngPath"
Write-Host "Wrote $iconPath"
