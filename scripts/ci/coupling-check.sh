#!/usr/bin/env bash
# CI coupling sweep — advisory checks over a git range.
# Usage: bash scripts/ci/coupling-check.sh <git-range>
# Always exits 0 (advisory). Fails open on internal errors.

set -uo pipefail

range="${1:-}"
diff_file="${2:-}"

if [[ -n "$diff_file" ]]; then
    diff_text=$(cat "$diff_file") || exit 0
elif [[ -n "$range" ]]; then
    diff_text=$(git diff --unified=0 "$range" 2>/dev/null) || exit 0
else
    echo "coupling-check: usage: coupling-check.sh <git-range> | coupling-check.sh --diff-file <path>"; exit 0
fi

warnings=()
current_file=""

while IFS= read -r line; do
    if [[ "$line" =~ ^\+\+\+\ b/(.+)$ ]]; then
        current_file="${BASH_REMATCH[1]}"
    elif [[ -n "$current_file" && "$line" =~ ^\+[^\+] ]]; then
        added="${line:1}"
        echo "$current_file" | grep -qiE '(^|/)tests?/' && continue

        if [[ "$current_file" =~ ^src/.*\.csproj$ ]] && echo "$added" | grep -qE '<ProjectReference'; then
            warnings+=("[HORIZONTAL MODULE REFERENCE] <ProjectReference> in $current_file")
            warnings+=("  Checklist: target must be CodenameAurora.Core, or file is CodenameAurora.App (composition root).")
        fi

        if [[ "$current_file" =~ ^src/CodenameAurora\.Core/.*\.cs$ ]] \
            && echo "$added" | grep -qE '\binterface\b|\bTask\b|\bValueTask\b'; then
            warnings+=("[CORE INTERFACE CHANGE] $current_file")
            warnings+=("  Checklist: all implementors updated, arch tests pass, ADR created if structural.")
        fi
    fi
done <<< "$diff_text"

[[ ${#warnings[@]} -eq 0 ]] && { echo "coupling-check: clean"; exit 0; }

printf 'COUPLING SWEEP (advisory) over %s:\n' "$range"
for w in "${warnings[@]}"; do printf '  %s\n' "$w"; done
exit 0
