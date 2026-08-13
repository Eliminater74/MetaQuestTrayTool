<#
.SYNOPSIS
  Delete old GitHub Actions workflow runs (keeps Releases / tags untouched).

.DESCRIPTION
  Lists Actions runs for this repo and deletes everything except the newest
  KeepCount runs per workflow (CI, Release, etc.). Does not delete GitHub
  Releases, tags, or artifacts on Releases.

.PARAMETER KeepCount
  Number of newest runs to keep per workflow. Default: 3.

.PARAMETER Repo
  owner/name. Default: current gh repo.

.PARAMETER WhatIf
  Show what would be deleted without deleting.

.EXAMPLE
  .\scripts\cleanup-actions-runs.ps1

.EXAMPLE
  .\scripts\cleanup-actions-runs.ps1 -KeepCount 5 -WhatIf
#>
param(
    [ValidateRange(1, 100)]
    [int] $KeepCount = 3,

    [string] $Repo = "",

    [switch] $WhatIf
)

$ErrorActionPreference = "Stop"

function Require-Gh {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw "GitHub CLI (gh) is required. Install from https://cli.github.com/ and run: gh auth login"
    }

    gh auth status 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "gh is not authenticated. Run: gh auth login"
    }
}

Require-Gh

if ([string]::IsNullOrWhiteSpace($Repo)) {
    $Repo = (gh repo view --json nameWithOwner -q .nameWithOwner).Trim()
    if ([string]::IsNullOrWhiteSpace($Repo)) {
        throw "Could not detect repo. Pass -Repo owner/name"
    }
}

Write-Host "Repo: $Repo"
Write-Host "Keeping newest $KeepCount run(s) per workflow; deleting older completed runs."
Write-Host "Releases / tags are not touched."
Write-Host ""

# gh run list --limit max is 1000.
$json = gh run list --repo $Repo --limit 1000 --json databaseId,displayTitle,workflowName,status,conclusion,createdAt,url,headBranch
if ($LASTEXITCODE -ne 0) {
    throw "gh run list failed."
}

$runs = $json | ConvertFrom-Json
if (-not $runs -or $runs.Count -eq 0) {
    Write-Host "No Actions runs found."
    return
}

Write-Host ("Found {0} run(s) total." -f $runs.Count)

$grouped = $runs | Group-Object -Property workflowName
$toDelete = New-Object System.Collections.Generic.List[object]

foreach ($group in $grouped) {
    $ordered = $group.Group | Sort-Object { [datetime]$_.createdAt } -Descending
    $keep = @($ordered | Select-Object -First $KeepCount)
    $drop = @($ordered | Select-Object -Skip $KeepCount)

    Write-Host ("`n[{0}] {1} run(s) - keep {2}, delete {3}" -f `
        $group.Name, $ordered.Count, $keep.Count, $drop.Count)

    foreach ($run in $drop) {
        # Never delete in-progress / queued runs.
        if ($run.status -ne "completed") {
            Write-Host ("  skip (still {0}): #{1} {2}" -f $run.status, $run.databaseId, $run.displayTitle)
            continue
        }

        $toDelete.Add($run) | Out-Null
        Write-Host ("  delete: #{0}  {1}  {2}" -f $run.databaseId, $run.createdAt, $run.displayTitle)
    }
}

if ($toDelete.Count -eq 0) {
    Write-Host "`nNothing to delete."
    return
}

Write-Host ("`nAbout to delete {0} run(s)." -f $toDelete.Count)

if ($WhatIf) {
    Write-Host "WhatIf: no deletes performed."
    return
}

$deleted = 0
$failed = 0
foreach ($run in $toDelete) {
    $id = [string]$run.databaseId
    gh run delete $id --repo $Repo 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        $deleted++
        Write-Host ("  deleted #{0}" -f $id)
    }
    else {
        # Fallback to REST API (some gh versions lack `run delete`).
        gh api -X DELETE "repos/$Repo/actions/runs/$id" 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) {
            $deleted++
            Write-Host ("  deleted #{0} (API)" -f $id)
        }
        else {
            $failed++
            Write-Warning ("Failed to delete run #{0}" -f $id)
        }
    }
}

Write-Host ("`nDone. Deleted {0}; failed {1}; kept newest {2} per workflow." -f $deleted, $failed, $KeepCount)
