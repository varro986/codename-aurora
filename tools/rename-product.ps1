<#
.SYNOPSIS
Renames the product in the repository: file contents, file names and folder names.

.DESCRIPTION
Usage:
  pwsh tools/rename-product.ps1 -To <NewName>            dry run: lists what would change, changes nothing
  pwsh tools/rename-product.ps1 -To <NewName> -Apply     applies the changes
  -From <OldName> (default Aurora), -Root <repository> (default: the repository holding this script)

The name is matched case-sensitively as a word or as the start of an identifier ("Aurora", "Aurora.Core",
"AuroraSettings"), never inside another word or in lowercase ("codename-aurora" stays).
Scope: the files git tracks or would track; ignored files (bin/, obj/) and binary files are skipped, and so are the
ADRs in specs/decisions/, which are never edited. The repository folder and the GitHub repository keep their name:
renaming them is up to the owner. Every target is checked before anything changes; an existing target stops the run.
Run again after -Apply: it finds nothing to do.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Z][A-Za-z0-9]*$')]
    [string]$To,

    [ValidatePattern('^[A-Z][A-Za-z0-9]*$')]
    [string]$From = 'Aurora',

    [switch]$Apply,

    [string]$Root = (Split-Path $PSScriptRoot -Parent)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ExcludedPrefixes = @('specs/decisions/')

function Get-NamePattern([string]$Name) {
    # Not preceded by a letter, not followed by a lowercase letter: a word or the start of an identifier.
    "(?<![A-Za-z])$([regex]::Escape($Name))(?![a-z])"
}

function Get-ProductFiles([string]$RepositoryRoot) {
    $listed = git -C $RepositoryRoot ls-files --cached --others --exclude-standard
    if ($LASTEXITCODE -ne 0) { throw "Not a git repository: $RepositoryRoot" }
    $listed |
        Where-Object { $path = $_; -not ($ExcludedPrefixes | Where-Object { $path.StartsWith($_) }) } |
        Where-Object { Test-Path -LiteralPath (Join-Path $RepositoryRoot $_) -PathType Leaf } |
        Sort-Object -Unique
}

function Get-ContentEdits([string]$RepositoryRoot, [string[]]$Files, [string]$Pattern, [string]$NewName) {
    foreach ($file in $Files) {
        $full = Join-Path $RepositoryRoot $file
        $bytes = [IO.File]::ReadAllBytes($full)
        if ($bytes -contains 0) { continue }
        $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
        $text = [Text.UTF8Encoding]::new($false).GetString($bytes, $(if ($hasBom) { 3 } else { 0 }), $bytes.Length - $(if ($hasBom) { 3 } else { 0 }))
        $count = [regex]::Matches($text, $Pattern).Count
        if ($count -gt 0) {
            [pscustomobject]@{ Path = $file; Count = $count; Text = [regex]::Replace($text, $Pattern, $NewName); Bom = $hasBom }
        }
    }
}

function Get-PathRenames([string[]]$Files, [string]$Pattern, [string]$NewName) {
    # Leaf renames on the original paths: files first, then folders deepest first, so no rename moves another's path.
    $renames = [ordered]@{}
    foreach ($file in $Files) {
        $segments = $file -split '/'
        for ($i = $segments.Length - 1; $i -ge 0; $i--) {
            $leaf = $segments[$i]
            $newLeaf = [regex]::Replace($leaf, $Pattern, $NewName)
            if ($newLeaf -ne $leaf) {
                $path = ($segments[0..$i] -join '/')
                if (-not $renames.Contains($path)) {
                    $renames[$path] = [pscustomobject]@{ Path = $path; NewName = $newLeaf; Depth = $i; IsFile = ($i -eq $segments.Length - 1) }
                }
            }
        }
    }
    $renames.Values | Sort-Object @{ Expression = 'IsFile'; Descending = $true }, @{ Expression = 'Depth'; Descending = $true }
}

$pattern = Get-NamePattern $From
$files = @(Get-ProductFiles $Root)
$edits = @(Get-ContentEdits $Root $files $pattern $To)
$renames = @(Get-PathRenames $files $pattern $To)

foreach ($rename in $renames) {
    $target = Join-Path (Split-Path (Join-Path $Root $rename.Path) -Parent) $rename.NewName
    if (Test-Path -LiteralPath $target) { throw "Target already exists, nothing changed: $target" }
}

if ($edits.Count -eq 0 -and $renames.Count -eq 0) {
    Write-Output "Nothing to do: no '$From' left in $Root."
    return
}

foreach ($edit in $edits) { Write-Output "edit    $($edit.Path) ($($edit.Count))" }
foreach ($rename in $renames) { Write-Output "rename  $($rename.Path) -> $($rename.NewName)" }

if (-not $Apply) {
    Write-Output "Dry run: $($edits.Count) files to edit, $($renames.Count) names to change. Run again with -Apply."
    return
}

foreach ($edit in $edits) {
    [IO.File]::WriteAllText((Join-Path $Root $edit.Path), $edit.Text, [Text.UTF8Encoding]::new($edit.Bom))
}
foreach ($rename in $renames) {
    Rename-Item -LiteralPath (Join-Path $Root $rename.Path) -NewName $rename.NewName
}
Write-Output "Done: $($edits.Count) files edited, $($renames.Count) names changed. Rebuild and run the tests."
