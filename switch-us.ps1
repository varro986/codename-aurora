# switch-us.ps1 — manage user story worktrees.
#
# 1. Proposes cleanup of worktrees whose GitHub issue is closed.
# 2. Fetches remote us-* branches and lets you open one as a worktree.
#
# Requires: gh CLI authenticated (gh auth login)

$env:PATH = [System.Environment]::GetEnvironmentVariable('PATH', 'Machine') + ';' + [System.Environment]::GetEnvironmentVariable('PATH', 'User')

$repo     = "varro986/codename-aurora"
$repoRoot = git rev-parse --show-toplevel
$repoName = Split-Path $repoRoot -Leaf
$parentDir = Split-Path $repoRoot -Parent

# ── 1. CLEANUP ────────────────────────────────────────────────────────────────

$activeWorktrees = git worktree list --porcelain |
    Select-String -Pattern '^worktree ' |
    ForEach-Object { $_.Line -replace '^worktree ', '' } |
    Where-Object { $_ -ne $repoRoot -and $_ -match '-us-(\d+)$' }

$toRemove = @()
foreach ($wt in $activeWorktrees) {
    $issueNum = if ($wt -match '-us-(\d+)$') { $Matches[1] } else { $null }
    if (-not $issueNum) { continue }
    $state = gh issue view $issueNum --repo $repo --json state --jq '.state' 2>$null
    if ($state -eq 'CLOSED') { $toRemove += $wt }
}

if ($toRemove.Count -gt 0) {
    Write-Host ""
    Write-Host "The following worktrees have closed issues:" -ForegroundColor Yellow
    $toRemove | ForEach-Object { Write-Host "  $_" }
    Write-Host ""
    $confirm = Read-Host "Remove them? [y/N]"
    if ($confirm -match '^[yY]$') {
        $toRemove | ForEach-Object {
            Write-Host "  Removing: $_" -ForegroundColor Green
            git worktree remove $_ --force
        }
    }
}

# ── 2. SWITCH ─────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "Fetching remote branches..." -ForegroundColor Cyan
git fetch --prune | Out-Null

$branches = @(git branch -r |
    Where-Object { $_ -match 'origin/us-\d+/' } |
    ForEach-Object { $_.Trim() -replace '^origin/', '' } |
    Sort-Object)

if ($branches.Count -eq 0) {
    Write-Host "No us-* branches found on remote." -ForegroundColor Yellow
    exit 0
}

$wtPaths = @(git worktree list --porcelain |
    Select-String -Pattern '^worktree ' |
    ForEach-Object { $_.Line -replace '^worktree ', '' })

Write-Host ""
Write-Host "Available user story branches:" -ForegroundColor Cyan
for ($i = 0; $i -lt $branches.Count; $i++) {
    $issueNum = ($branches[$i] -split '/')[0] -replace 'us-', ''
    $wtPath   = Join-Path $parentDir "$repoName-us-$issueNum"
    $tag      = if ($wtPaths -contains $wtPath) { " [open]" } else { "" }
    Write-Host "  [$($i + 1)] $($branches[$i])$tag"
}
Write-Host ""

$choice = Read-Host "Choose branch number (Enter to cancel)"
if ([string]::IsNullOrWhiteSpace($choice)) { exit 0 }

$index = [int]$choice - 1
if ($index -lt 0 -or $index -ge $branches.Count) {
    Write-Host "Invalid selection." -ForegroundColor Red
    exit 1
}

$branch   = $branches[$index]
$issueNum = ($branch -split '/')[0] -replace 'us-', ''
$wtPath   = Join-Path $parentDir "$repoName-us-$issueNum"

if (Test-Path $wtPath) {
    Write-Host ""
    Write-Host "Worktree already exists -> opening: $wtPath" -ForegroundColor Yellow
} else {
    Write-Host ""
    Write-Host "Creating worktree at: $wtPath" -ForegroundColor Green
    git worktree add $wtPath $branch
}

Start-Process explorer.exe $wtPath
Write-Host "Done. Worktree at: $wtPath" -ForegroundColor Cyan
