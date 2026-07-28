#!/usr/bin/env bash
# Creates the standard GitHub label set — generic, idempotent.
# Covers workflow states, issue types, and priorities.
# Project-specific area labels should be added per-repo (see example below).
#
# Requires: gh CLI authenticated against the target repository.
#
# Usage:
#   ORG=my-org REPO=my-repo bash .github/setup-labels.sh
#
# Optional env vars:
#   DRY_RUN   Log all actions without executing (default: false)
#             Usage: DRY_RUN=true ORG=x REPO=y bash .github/setup-labels.sh

set -euo pipefail

ORG="${ORG:?Set ORG=your-org-name}"
REPO="${REPO:?Set REPO=your-repo-name}"
DRY_RUN="${DRY_RUN:-false}"
REPO_FULL="$ORG/$REPO"

# In dry-run mode, override gh to log instead of execute.
if [ "$DRY_RUN" = "true" ]; then
  gh() { echo "   [DRY RUN] gh $*" >&2; }
fi

echo "→ Workflow states"
gh label create "status:draft"  --color "ededed" --description "Story is being written"          --repo "$REPO_FULL" --force
gh label create "status:ready"  --color "0075ca" --description "DoR met, ready for development"  --repo "$REPO_FULL" --force
gh label create "status:active" --color "e4e669" --description "In progress"                      --repo "$REPO_FULL" --force
gh label create "status:review" --color "0e8a16" --description "PR open, DoD check pending"       --repo "$REPO_FULL" --force
echo "   Done"

echo "→ Issue types"
gh label create "type:feature"    --color "a2eeef" --description "New feature or user story"        --repo "$REPO_FULL" --force
gh label create "type:adr"        --color "d876e3" --description "Architecture Decision Record"      --repo "$REPO_FULL" --force
gh label create "type:bug"        --color "d73a4a" --description "Something is not working"          --repo "$REPO_FULL" --force
gh label create "type:dependency" --color "1d76db" --description "Dependency update (Dependabot)"    --repo "$REPO_FULL" --force
echo "   Done"

echo "→ Priorities"
gh label create "priority:mvp"          --color "b60205" --description "Must-have for first release"     --repo "$REPO_FULL" --force
gh label create "priority:enterprise"   --color "e99695" --description "Required for enterprise rollout" --repo "$REPO_FULL" --force
gh label create "priority:nice-to-have" --color "cccccc" --description "Good to have, low urgency"       --repo "$REPO_FULL" --force
echo "   Done"

# Project-specific area labels — add below for each repo.
# Example:
#   gh label create "area:core"    --color "f9d0c4" --description "Core module" --repo "$REPO_FULL" --force
#   gh label create "area:ui"      --color "f9d0c4" --description "UI module"   --repo "$REPO_FULL" --force

echo ""
echo "Done. Re-run any time — idempotent."
