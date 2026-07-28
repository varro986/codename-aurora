#!/usr/bin/env bash
# Creates the full GitHub label set for the Codename Aurora workflow.
# Requires: gh CLI authenticated against the target repository.
# Usage: bash .github/setup-labels.sh

set -euo pipefail

# Workflow states
gh label create "status:draft"  --color "ededed" --description "Story is being written"          --force
gh label create "status:ready"  --color "0075ca" --description "DoR met, ready for development"  --force
gh label create "status:active" --color "e4e669" --description "In progress"                      --force
gh label create "status:review" --color "0e8a16" --description "PR open, DoD check pending"       --force

# Types
gh label create "type:feature"  --color "a2eeef" --description "New feature or user story"        --force
gh label create "type:adr"      --color "d876e3" --description "Architecture Decision Record"      --force
gh label create "type:bug"      --color "d73a4a" --description "Something is not working"          --force

# Module areas
gh label create "area:core"        --color "f9d0c4" --description "Aurora.Core interfaces"         --force
gh label create "area:ocr"         --color "f9d0c4" --description "Aurora.OCR module"              --force
gh label create "area:translation" --color "f9d0c4" --description "Aurora.Translation module"      --force
gh label create "area:ui"          --color "f9d0c4" --description "Aurora.UI module"               --force
gh label create "area:admin"       --color "f9d0c4" --description "Aurora.Admin module"            --force
gh label create "area:app"         --color "f9d0c4" --description "Aurora.App composition root"     --force

# Priorities
gh label create "priority:mvp"          --color "b60205" --description "Must-have for first release"     --force
gh label create "priority:enterprise"   --color "e99695" --description "Required for enterprise rollout" --force
gh label create "priority:nice-to-have" --color "cccccc" --description "Good to have, low urgency"       --force

echo "All labels created successfully."
