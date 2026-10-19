[CmdletBinding()]
param(
    [switch]$NoBuild,
    [string]$ResultsDirectory
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $ResultsDirectory) {
    $runId = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
    $ResultsDirectory = Join-Path $root "TestResults/unit-mocking/$runId"
} elseif (-not [IO.Path]::IsPathRooted($ResultsDirectory)) {
    $ResultsDirectory = Join-Path $root $ResultsDirectory
}
$ResultsDirectory = [IO.Path]::GetFullPath($ResultsDirectory)
New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null
$projects = @('AuthUserService', 'FleetVehicleService', 'TripDispatchService', 'TrackingTelemetryService')
$rows = @()
$hasFailure = $false
foreach ($project in $projects) {
    Write-Host "Running isolated unit tests: $project"
    $projectPath = Join-Path $root "tests/$project.Tests/$project.Tests.csproj"
    $reportName = "$project.trx"
    $reportPath = Join-Path $ResultsDirectory $reportName
    if (Test-Path -LiteralPath $reportPath) {
        throw "Result file already exists: $reportPath. Choose a fresh results directory."
    }
    $arguments = @('test', $projectPath, '--filter', 'Category=Unit', '--logger', "trx;LogFileName=$reportName", '--results-directory', $ResultsDirectory, '--verbosity', 'minimal')
    if ($NoBuild) { $arguments += '--no-build' }
    & dotnet @arguments
    $testExitCode = $LASTEXITCODE
    if ($testExitCode -ne 0) { $hasFailure = $true }
    if (-not (Test-Path -LiteralPath $reportPath)) {
        $hasFailure = $true
        $rows += "| $project | - | - | - | - | Missing report |"
        continue
    }
    [xml]$report = Get-Content -LiteralPath $reportPath -Raw
    $counters = $report.SelectSingleNode("//*[local-name()='ResultSummary']/*[local-name()='Counters']")
    if ($null -eq $counters -or [int]$counters.total -eq 0 -or [int]$counters.executed -eq 0) {
        $hasFailure = $true
        $rows += "| $project | 0 | 0 | 0 | 0 | No executed tests |"
        continue
    }
    $outcome = 'Passed'
    if ($testExitCode -ne 0 -or [int]$counters.failed -gt 0 -or [int]$counters.passed -ne [int]$counters.total) {
        $outcome = 'Failed or skipped'
        $hasFailure = $true
    }
    $rows += "| $project | $($counters.total) | $($counters.passed) | $($counters.failed) | $($counters.notExecuted) | $outcome |"
}
$summary = @(
    '# Smart Fleet unit testing and mocking',
    '',
    'Selected tests: Category=Unit. Includes existing classified tests and the SE3112 additions; counts are not an authorship claim.',
    '',
    '| Service | Total | Passed | Failed | Skipped | Result |',
    '|---|---:|---:|---:|---:|---|'
) + $rows
$summaryPath = Join-Path $ResultsDirectory 'summary.md'
$summary | Set-Content -LiteralPath $summaryPath -Encoding UTF8
if ($env:GITHUB_STEP_SUMMARY) { $summary | Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Encoding UTF8 }
$summary | ForEach-Object { Write-Host $_ }
Write-Host "Results: $ResultsDirectory"
if ($hasFailure) { throw 'One or more unit suites failed, skipped tests, or produced no test results. See the TRX files.' }
