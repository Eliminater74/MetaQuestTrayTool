#Requires -Version 5.1
<#
.SYNOPSIS
  Extracts one version section from CHANGELOG.md (Keep a Changelog style).

.EXAMPLE
  .\scripts\Extract-Changelog.ps1 -Version 1.1.6
  .\scripts\Extract-Changelog.ps1 -Version 1.1.6 -OutFile installer\WHATSNEW.txt -IncludeHeader
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Version,

    [string] $ChangelogPath = "",

    [string] $OutFile = "",

    [switch] $IncludeHeader,

    [switch] $PlainText
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($ChangelogPath)) {
    $ChangelogPath = Join-Path $root "CHANGELOG.md"
}

if (-not (Test-Path $ChangelogPath)) {
    throw "CHANGELOG.md not found: $ChangelogPath"
}

$version = $Version.Trim().TrimStart('v', 'V')
$lines = Get-Content -Path $ChangelogPath -Encoding utf8
$start = -1
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match "^## \[$([regex]::Escape($version))\]") {
        $start = $i
        break
    }
}

if ($start -lt 0) {
    throw "No CHANGELOG section found for version $version"
}

$end = $lines.Count
for ($i = $start + 1; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match '^## \[') {
        $end = $i
        break
    }
}

$chunk = New-Object System.Collections.Generic.List[string]
for ($i = $start; $i -lt $end; $i++) {
    if ($lines[$i] -match '^\[[^\]]+\]:\s*https?://') {
        continue
    }
    $chunk.Add($lines[$i].TrimEnd())
}

while ($chunk.Count -gt 0 -and [string]::IsNullOrWhiteSpace($chunk[$chunk.Count - 1])) {
    $chunk.RemoveAt($chunk.Count - 1)
}

$section = $chunk.ToArray()

if ($PlainText) {
    $section = $section | ForEach-Object {
        $_ -replace '^##\s+', '' `
           -replace '^###\s+', '' `
           -replace '\*\*(.+?)\*\*', '$1' `
           -replace '`([^`]+)`', '$1'
    }
}

$text = if ($IncludeHeader) {
    @(
        "Meta Quest Tray Tool $version - What's new",
        "",
        "Read these notes before installing. You can cancel Setup if you prefer to stay on your current version.",
        "",
        ($section -join [Environment]::NewLine),
        "",
        "Full history: https://github.com/Eliminater74/MetaQuestTrayTool/blob/main/CHANGELOG.md"
    ) -join [Environment]::NewLine
}
else {
    $section -join [Environment]::NewLine
}

if (-not [string]::IsNullOrWhiteSpace($OutFile)) {
    $dir = Split-Path -Parent $OutFile
    if ($dir -and -not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    $outPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutFile)
    $utf8WithBom = New-Object System.Text.UTF8Encoding -ArgumentList $true
    [System.IO.File]::WriteAllText($outPath, $text + [Environment]::NewLine, $utf8WithBom)
    Write-Host "Wrote $OutFile"
}
else {
    Write-Output $text
}
