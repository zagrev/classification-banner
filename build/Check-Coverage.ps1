<#
.SYNOPSIS
    Fails the build when line coverage falls below a required threshold.

.DESCRIPTION
    Reads the Cobertura report produced by "dotnet test --collect:'XPlat Code Coverage'"
    and compares overall line coverage against the supplied minimum. Types marked
    [ExcludeFromCodeCoverage] - the WPF hosts and the shell interop wrapper - are already
    absent from the report, so this measures the testable surface only.

.PARAMETER ResultsDirectory
    Directory searched recursively for coverage.cobertura.xml.

.PARAMETER MinimumLineRate
    Required line coverage as a percentage.
#>
[CmdletBinding()]
param(
    [string] $ResultsDirectory = 'TestResults',
    [double] $MinimumLineRate = 85.0
)

$ErrorActionPreference = 'Stop'

$report = Get-ChildItem -Path $ResultsDirectory -Recurse -Filter 'coverage.cobertura.xml' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $report) {
    Write-Error "No coverage.cobertura.xml found under '$ResultsDirectory'. Did the test step run with --collect:'XPlat Code Coverage'?"
    exit 1
}

[xml] $coverage = Get-Content -Path $report.FullName

$lineRate   = [double] $coverage.coverage.'line-rate' * 100
$branchRate = [double] $coverage.coverage.'branch-rate' * 100

Write-Host "Coverage report: $($report.FullName)"
Write-Host ''
Write-Host ('{0,-42} {1,8}' -f 'Class', 'Lines')
Write-Host ('{0,-42} {1,8}' -f ('-' * 42), '--------')

foreach ($class in $coverage.coverage.packages.package.classes.class | Sort-Object name) {
    Write-Host ('{0,-42} {1,7:N1}%' -f $class.name, ([double] $class.'line-rate' * 100))
}

Write-Host ''
Write-Host ('Overall line coverage:   {0:N2}%' -f $lineRate)
Write-Host ('Overall branch coverage: {0:N2}%' -f $branchRate)
Write-Host ('Required minimum:        {0:N2}%' -f $MinimumLineRate)
Write-Host ''

if ($lineRate -lt $MinimumLineRate) {
    Write-Error ('Line coverage {0:N2}% is below the required minimum of {1:N2}%.' -f $lineRate, $MinimumLineRate)
    exit 1
}

Write-Host ('Coverage check passed ({0:N2}% >= {1:N2}%).' -f $lineRate, $MinimumLineRate)
