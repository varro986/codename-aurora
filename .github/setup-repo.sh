#!/usr/bin/env bash
# Repository governance setup — generic, idempotent, self-calibrating.
#
# Creates org teams, assigns repo permissions, configures branch and tag protection.
# Safe to re-run at any time: uses PUT/upsert throughout, never duplicates resources.
# required_approving_review_count is auto-derived from the architects team size,
# so the script works in solo mode and scales automatically as the team grows.
#
# Requires: gh CLI authenticated as an org Owner.
#
# Usage:
#   ORG=my-org REPO=my-repo bash .github/setup-repo.sh
#
# Required env vars:
#   ORG              GitHub organisation slug
#   REPO             Repository name
#
# Optional env vars:
#   DEFAULT_BRANCH   Branch to protect                       (default: main)
#   TAG_PATTERN      Release tag glob for ruleset            (default: v*)
#   RULESET_NAME     Ruleset name                            (default: release-tag-protection)
#   REQUIRED_CHECKS  Comma-separated CI check names to gate  (default: "", none required)
#                    Example: "Build,Architecture Tests"
#   ENFORCE_ADMINS   Enforce branch protection on admins     (default: false)
#   DRY_RUN          Log all actions without executing       (default: false)
#                    Usage: DRY_RUN=true ORG=x REPO=y bash .github/setup-repo.sh

set -euo pipefail

ORG="${ORG:?Set ORG=your-org-name}"
REPO="${REPO:?Set REPO=your-repo-name}"
DEFAULT_BRANCH="${DEFAULT_BRANCH:-main}"
TAG_PATTERN="${TAG_PATTERN:-v*}"
RULESET_NAME="${RULESET_NAME:-release-tag-protection}"
REQUIRED_CHECKS="${REQUIRED_CHECKS:-}"
ENFORCE_ADMINS="${ENFORCE_ADMINS:-false}"
DRY_RUN="${DRY_RUN:-false}"

# ── helpers ────────────────────────────────────────────────────────────────

# Creates a team if it does not exist. Prints team ID to stdout; diagnostics to stderr.
# In dry-run mode, prints intent and returns dummy ID "0".
ensure_team() {
  local slug=$1 name=$2 desc=$3
  if [ "$DRY_RUN" = "true" ]; then
    echo "   [DRY RUN] Would ensure team: $name (slug=$slug)" >&2
    echo "0"
    return 0
  fi
  local id
  id=$(gh api "orgs/$ORG/teams/$slug" --jq '.id' 2>/dev/null || echo "")
  if [ -z "$id" ]; then
    id=$(gh api --method POST "orgs/$ORG/teams" \
      --field name="$name" \
      --field description="$desc" \
      --field privacy="closed" \
      --jq '.id')
    echo "   Created: $name (id=$id)" >&2
  else
    echo "   Exists:  $name (id=$id)" >&2
  fi
  echo "$id"
}

# Assigns a team to the repo with the given permission (PUT = idempotent).
assign_team() {
  local slug=$1 perm=$2
  echo "   $slug → $perm"
  if [ "$DRY_RUN" = "true" ]; then
    echo "   [DRY RUN] Would assign: $slug → $perm on $ORG/$REPO" >&2
    return 0
  fi
  gh api --method PUT "orgs/$ORG/teams/$slug/repos/$ORG/$REPO" \
    --field permission="$perm" >/dev/null
}

# Converts "check1,check2" to JSON array ["check1","check2"].
checks_to_json() {
  echo "$1" | tr ',' '\n' \
    | sed 's/^[[:space:]]*//;s/[[:space:]]*$//' \
    | awk 'BEGIN{printf "["} NR>1{printf ","} {printf "\"%s\"",$0} END{printf "]"}'
}

# ── 0. Pre-flight ──────────────────────────────────────────────────────────
# Verify org is reachable before making any changes.
# A 403/404 here means the token lacks org Owner rights or the org does not exist.
if [ "$DRY_RUN" = "true" ]; then
  echo "→ Pre-flight (DRY RUN — skipping API check)"
  echo "   Would verify org: $ORG / repo: $REPO"
else
  echo "→ Pre-flight"
  if ! gh api "orgs/$ORG" --jq '.login' &>/dev/null; then
    echo "   ERROR: org '$ORG' not found or token lacks org Owner permission." >&2
    exit 1
  fi
  echo "   Org '$ORG' accessible ✓"
fi

# ── 1. Teams ───────────────────────────────────────────────────────────────
echo "→ Teams"
ARCH_ID=$(ensure_team "architects"   "Architects"   "Architects — ADR approval, release gating")
          ensure_team "lead-devs"    "Lead Devs"    "Lead Developers — code review and merge" >/dev/null
          ensure_team "analysts"     "Analysts"     "Business Analysts — user story authoring" >/dev/null
          ensure_team "contributors" "Contributors" "Contributors — feature development" >/dev/null
          ensure_team "requestors"   "Requestors"   "Requestors — submit feature requests via issues" >/dev/null

# ── 2. Repo permissions ────────────────────────────────────────────────────
echo "→ Repo permissions"
assign_team "architects"   "admin"
assign_team "lead-devs"    "maintain"
assign_team "analysts"     "triage"
assign_team "contributors" "push"
assign_team "requestors"   "pull"

# ── 3. Auto-calibrate required reviewers ──────────────────────────────────
# 0 if architects has <2 members (solo mode), 1 otherwise.
# Re-run this script after adding a second member to architects to activate reviews.
echo "→ Calibrating required reviewers"
if [ "$DRY_RUN" = "true" ]; then
  ARCH_MEMBERS="0"
  echo "   [DRY RUN] Assuming architects members: 0"
else
  ARCH_MEMBERS=$(gh api "orgs/$ORG/teams/architects/members" --jq 'length' 2>/dev/null || echo "0")
fi
REQUIRED_REVIEWS=$([ "$ARCH_MEMBERS" -ge 2 ] && echo "1" || echo "0")
echo "   architects members: $ARCH_MEMBERS → required_approving_review_count: $REQUIRED_REVIEWS"

# ── 4. Branch protection ───────────────────────────────────────────────────
echo "→ Branch protection: $DEFAULT_BRANCH"

if [ -n "$REQUIRED_CHECKS" ]; then
  STATUS_CHECKS_JSON=$(printf '{"strict":true,"contexts":%s}' "$(checks_to_json "$REQUIRED_CHECKS")")
else
  STATUS_CHECKS_JSON="null"
fi

BRANCH_PAYLOAD=$(printf '{
  "required_status_checks": %s,
  "enforce_admins": %s,
  "required_pull_request_reviews": {
    "required_approving_review_count": %s,
    "dismiss_stale_reviews": true,
    "require_code_owner_reviews": true
  },
  "restrictions": null,
  "allow_force_pushes": false,
  "allow_deletions": false
}' "$STATUS_CHECKS_JSON" "$ENFORCE_ADMINS" "$REQUIRED_REVIEWS")

if [ "$DRY_RUN" = "true" ]; then
  echo "   [DRY RUN] Would PUT branch protection on $DEFAULT_BRANCH"
  echo "   [DRY RUN] required_reviews=$REQUIRED_REVIEWS enforce_admins=$ENFORCE_ADMINS"
else
  echo "$BRANCH_PAYLOAD" | gh api --method PUT \
    "repos/$ORG/$REPO/branches/$DEFAULT_BRANCH/protection" --input -
fi
echo "   Done"

# ── 5. Tag ruleset: release tags restricted to architects ──────────────────
# Uses Rulesets API (not legacy tags/protection) — supports team-scoped bypass actors.
echo "→ Tag ruleset: $TAG_PATTERN (architects only)"

if [ "$DRY_RUN" = "true" ]; then
  echo "   [DRY RUN] Would upsert ruleset '$RULESET_NAME' for pattern $TAG_PATTERN"
  echo "   [DRY RUN] bypass_actors: architects team (id=$ARCH_ID)"
else
  # Remove legacy basic tag protection if present (superseded by this ruleset).
  OLD_IDS=$(gh api "repos/$ORG/$REPO/tags/protection" \
    --jq --arg p "$TAG_PATTERN" '.[] | select(.pattern==$p) | .id' 2>/dev/null || echo "")
  for id in $OLD_IDS; do
    gh api --method DELETE "repos/$ORG/$REPO/tags/protection/$id"
    echo "   Removed legacy tag protection id=$id"
  done

  RULESET_PAYLOAD=$(printf '{
  "name": "%s",
  "target": "tag",
  "enforcement": "active",
  "conditions": {
    "ref_name": {
      "include": ["refs/tags/%s"],
      "exclude": []
    }
  },
  "rules": [
    { "type": "creation" },
    { "type": "deletion" },
    { "type": "non_fast_forward" }
  ],
  "bypass_actors": [
    {
      "actor_id": %s,
      "actor_type": "Team",
      "bypass_mode": "always"
    }
  ]
}' "$RULESET_NAME" "$TAG_PATTERN" "$ARCH_ID")

  EXISTING_ID=$(gh api "repos/$ORG/$REPO/rulesets" \
    --jq --arg n "$RULESET_NAME" '.[] | select(.name==$n) | .id' 2>/dev/null || echo "")

  if [ -z "$EXISTING_ID" ]; then
    echo "$RULESET_PAYLOAD" | gh api --method POST "repos/$ORG/$REPO/rulesets" --input - >/dev/null
    echo "   Created ruleset"
  else
    echo "$RULESET_PAYLOAD" | gh api --method PUT "repos/$ORG/$REPO/rulesets/$EXISTING_ID" --input - >/dev/null
    echo "   Updated ruleset (id=$EXISTING_ID)"
  fi
fi
echo "   Done"

# ── 6. GitHub Actions: fork PR approval ───────────────────────────────────
# Prevents fork PRs from running workflows with write permissions without approval.
echo "→ Actions: fork PR approval policy"
if [ "$DRY_RUN" = "true" ]; then
  echo "   [DRY RUN] Would set default_workflow_permissions=read, can_approve_pull_request_reviews=false"
else
  gh api --method PUT "repos/$ORG/$REPO/actions/permissions/workflow" \
    --field default_workflow_permissions="read" \
    --field can_approve_pull_request_reviews=false
fi
echo "   Done"

echo ""
echo "Done. Re-run any time — idempotent."
