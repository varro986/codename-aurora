<#
.SYNOPSIS
Runs the product's checks from cicd/steps.json, on the machine before every push and in the remote CI.

.DESCRIPTION
Usage (from any folder):
  pwsh -NoProfile -File cicd/run.ps1                     every step
  pwsh -NoProfile -File cicd/run.ps1 -Step build,unit    only the named steps (what the remote CI runs)
  pwsh -NoProfile -File cicd/run.ps1 -List               the steps and what they need, without running them
  -Strict: a missing need is a failure instead of a skip

Needs: a command name (dotnet), "pester5" (Pester 5 or later), "desktop" (an interactive Windows session, not a CI
runner). Every step runs in its own process from its folder. Exit code 0 when no step failed, 1 otherwise.
#>
[CmdletBinding()]
param(
    [string[]]$Step,
    [switch]$List,
    [switch]$Strict
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$steps = @((Get-Content -LiteralPath (Join-Path $PSScriptRoot 'steps.json') -Raw | ConvertFrom-Json).steps)

if ($Step) {
    $unknown = @($Step | Where-Object { $_ -notin $steps.name })
    if ($unknown) { throw "Unknown steps: $($unknown -join ', '). Run with -List to see them." }
    $steps = @($steps | Where-Object { $_.name -in $Step })
}

function Test-Need([string]$Name) {
    switch ($Name) {
        'pester5' { [bool](Get-Module -ListAvailable Pester | Where-Object { $_.Version.Major -ge 5 }) }
        # CI runners have no user at the desktop: foreground, focus and input tests cannot run there.
        'desktop' { $IsWindows -and [Environment]::UserInteractive -and -not $env:CI }
        default { [bool](Get-Command $Name -ErrorAction SilentlyContinue) }
    }
}

if ($List) {
    foreach ($s in $steps) { "$($s.name)  [needs: $($s.needs -join ', ')]  $($s.run)" }
    return
}

$pwsh = (Get-Process -Id $PID).Path
$results = foreach ($s in $steps) {
    $missing = @($s.needs | Where-Object { -not (Test-Need $_) })
    if ($missing) {
        [pscustomobject]@{ State = $(if ($Strict) { 'FAILED' } else { 'skipped' }); Step = $s.name; Note = "missing $($missing -join ', ')"; Seconds = 0 }
        continue
    }
    Write-Host "`n=== $($s.name)" -ForegroundColor Cyan
    $timer = [Diagnostics.Stopwatch]::StartNew()
    Push-Location -LiteralPath (Join-Path $root $s.dir)
    try {
        & $pwsh -NoProfile -Command $s.run | Out-Host
        $code = $LASTEXITCODE
    }
    finally { Pop-Location }
    [pscustomobject]@{ State = $(if ($code) { 'FAILED' } else { 'ok' }); Step = $s.name; Note = $(if ($code) { "exit code $code" } else { '' }); Seconds = [int]$timer.Elapsed.TotalSeconds }
}

Write-Host "`n=== Summary"
$results | Format-Table State, Step, Note, Seconds -AutoSize | Out-String -Width 200
$failed = @($results | Where-Object State -eq 'FAILED').Count
"steps: $(@($results).Count); failed $failed; skipped $(@($results | Where-Object State -eq 'skipped').Count)"
if ($failed) { exit 1 }
