#!/usr/bin/env bash
# Creates the Aurora label set — idempotent.
# Deletes stale labels before creating the current set.
#
# Usage:
#   ORG=my-org REPO=my-repo bash .github/setup-labels.sh
# Optional:
#   DRY_RUN=true ORG=x REPO=y bash .github/setup-labels.sh

set -euo pipefail

ORG="${ORG:?Set ORG=your-org-name}"
REPO="${REPO:?Set REPO=your-repo-name}"
DRY_RUN="${DRY_RUN:-false}"
REPO_FULL="$ORG/$REPO"

if [ "$DRY_RUN" = "true" ]; then
  gh() { echo "   [DRY RUN] gh $*" >&2; }
fi

# ── Delete stale labels ────────────────────────────────────────────────────────
echo "→ Removing stale labels"
STALE=(
  "type:feature" "type:adr" "type:bug" "type:dependency"
  "priority:mvp" "priority:enterprise" "priority:nice-to-have"
  "area:admin" "area:ocr" "area:translation" "area:ui" "area:core"
)
for label in "${STALE[@]}"; do
  gh label delete "$label" --repo "$REPO_FULL" --yes 2>/dev/null || true
done
echo "   Done"

# ── Workflow status ────────────────────────────────────────────────────────────
echo "→ Workflow status"
gh label create "status: request" --color "bfd4f2" --description "Requestor submitted — awaiting AI screening + BA decision" --repo "$REPO_FULL" --force
gh label create "status: draft"   --color "ededed" --description "BA writing — not yet reviewed"       --repo "$REPO_FULL" --force
gh label create "status: ready"   --color "0075ca" --description "Dual-approved — branch incoming"     --repo "$REPO_FULL" --force
gh label create "status: active"  --color "e4e669" --description "Branch created — in development"     --repo "$REPO_FULL" --force
gh label create "status: review"  --color "0e8a16" --description "PR open — DoD check pending"         --repo "$REPO_FULL" --force
gh label create "status: uat"     --color "f0e68c" --description "Released — requestor UAT pending"    --repo "$REPO_FULL" --force
echo "   Done"

# ── Approval gates ────────────────────────────────────────────────────────────
echo "→ Approval gates"
gh label create "approved: ba"      --color "c2e0c6" --description "BA approved this user story"       --repo "$REPO_FULL" --force
gh label create "approved: leaddev" --color "c2e0c6" --description "Lead Dev approved this user story" --repo "$REPO_FULL" --force
echo "   Done"

# ── AI review rounds (escalation tracking) ────────────────────────────────────
echo "→ AI review rounds"
gh label create "ai-review: 1" --color "f9d0c4" --description "AI review round 1 complete" --repo "$REPO_FULL" --force
gh label create "ai-review: 2" --color "f9d0c4" --description "AI review round 2 complete" --repo "$REPO_FULL" --force
gh label create "ai-review: 3" --color "d73a4a" --description "AI review exhausted — human escalation required" --repo "$REPO_FULL" --force
echo "   Done"

# ── CI bypass ─────────────────────────────────────────────────────────────────
# rules-exempt skips the agentic-smell check in ci.yml — apply manually when justified.
echo "→ CI bypass"
gh label create "rules-exempt" --color "e4e669" --description "Skips agentic-smell CI check — apply manually with justification" --repo "$REPO_FULL" --force
echo "   Done"

echo ""
echo "Done. Re-run any time — idempotent."
