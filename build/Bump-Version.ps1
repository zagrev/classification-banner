<#
.SYNOPSIS
    Increments the version held in the VERSION file.

.DESCRIPTION
    Reads "MAJOR.MINOR.PATCH" from the VERSION file, increments the requested component,
    writes it back, and reports both the previous and new values. Intended to be run by
    the release pipeline after a successful build, but safe to run locally.

    On GitHub Actions the new version is also written to $GITHUB_OUTPUT so later steps can
    consume it, and to $GITHUB_STEP_SUMMARY for visibility on the run page.

.PARAMETER VersionFile
    Path to the VERSION file.

.PARAMETER Increment
    Which component to bump: Major, Minor, or Patch.

.PARAMETER WhatIfOnly
    Compute and report the next version without writing the file.
#>
[CmdletBinding()]
param(
    [string] $VersionFile = 'VERSION',

    [ValidateSet('Major', 'Minor', 'Patch')]
    [string] $Increment = 'Patch',

    [switch] $WhatIfOnly
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -Path $VersionFile)) {
    Write-Error "Version file '$VersionFile' not found."
    exit 1
}

$raw = (Get-Content -Path $VersionFile -Raw).Trim()

$parsed = $null
if (-not [System.Version]::TryParse($raw, [ref] $parsed) -or $parsed.Build -lt 0) {
    Write-Error "Version file '$VersionFile' contains '$raw', which is not a MAJOR.MINOR.PATCH version."
    exit 1
}

switch ($Increment) {
    'Major' { $next = [System.Version]::new($parsed.Major + 1, 0, 0) }
    'Minor' { $next = [System.Version]::new($parsed.Major, $parsed.Minor + 1, 0) }
    'Patch' { $next = [System.Version]::new($parsed.Major, $parsed.Minor, $parsed.Build + 1) }
}

$previousVersion = "$($parsed.Major).$($parsed.Minor).$($parsed.Build)"
$nextVersion     = "$($next.Major).$($next.Minor).$($next.Build)"

Write-Host "Previous version: $previousVersion"
Write-Host "Next version:     $nextVersion  ($Increment increment)"

if (-not $WhatIfOnly) {
    # Trailing newline, no BOM, so the file stays clean in diffs and on non-Windows tooling.
    [System.IO.File]::WriteAllText(
        (Resolve-Path $VersionFile),
        "$nextVersion`n",
        (New-Object System.Text.UTF8Encoding $false))

    Write-Host "Wrote $nextVersion to $VersionFile"
}

if ($env:GITHUB_OUTPUT) {
    "previous-version=$previousVersion" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
    "version=$nextVersion"              | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
    "tag=v$nextVersion"                 | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
}

if ($env:GITHUB_STEP_SUMMARY) {
    "### Version bumped to ``$nextVersion`` (was ``$previousVersion``)" |
        Out-File -FilePath $env:GITHUB_STEP_SUMMARY -Append -Encoding utf8
}
