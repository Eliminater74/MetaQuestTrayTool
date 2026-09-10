param([string] $ResultsDirectory = 'artifacts/coverage', [double] $MinimumLinePercent = 8)
$ErrorActionPreference = 'Stop'
$reports = @(Get-ChildItem -LiteralPath $ResultsDirectory -Filter coverage.cobertura.xml -Recurse)
if ($reports.Count -eq 0) { throw 'No coverage report was produced.' }
$report = $reports | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
[xml]$coverage = Get-Content -LiteralPath $report.FullName -Raw
$percent = [double]::Parse($coverage.coverage.'line-rate', [Globalization.CultureInfo]::InvariantCulture) * 100
"Line coverage: $($percent.ToString('F2'))% (minimum $MinimumLinePercent%)"
"Coverage report: $($report.FullName)"
if ($percent -lt $MinimumLinePercent) { throw 'Line coverage fell below the baseline floor.' }
