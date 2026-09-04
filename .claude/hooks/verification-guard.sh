#!/usr/bin/env bash
# PreToolUse: Bash — blocks git push when .cs files in outgoing commits
# are newer than the last dotnet test run (.trx timestamp).
# dotnet build does NOT compile test projects. This enforces: test before push.
# Override: SKIP_VERIFY_CHECK=1 git push ...

set -uo pipefail

payload=$(cat)
cmd=$(printf '%s' "$payload" | grep -oE '"command":"[^"]*"' | head -1 | grep -oE '"[^"]*"$' | tr -d '"')

echo "$cmd" | grep -qE '\bgit\b.*\bpush\b' || exit 0
echo "$cmd" | grep -q 'SKIP_VERIFY_CHECK=1' && exit 0
echo "$cmd" | grep -q '\-\-dry-run' && exit 0

root="${CLAUDE_PROJECT_DIR:-$(pwd)}"

# Find outgoing range
upstream=$(git rev-parse --abbrev-ref --symbolic-full-name '@{upstream}' 2>/dev/null || true)
if [[ -n "$upstream" ]]; then
    range="$upstream..HEAD"
elif git rev-parse --verify origin/main &>/dev/null; then
    range="origin/main..HEAD"
elif git rev-parse --verify main &>/dev/null; then
    range="main..HEAD"
else
    exit 0
fi

# Find .cs files in outgoing commits touching src/ or tests/
changed=$(git diff --name-only "$range" 2>/dev/null | grep -E '^(src|tests)/.*\.cs$' || true)
[[ -n "$changed" ]] || exit 0

# Find newest .trx file
latest_trx=$(find "$root" -name '*.trx' -path '*/TestResults/*' 2>/dev/null | xargs ls -t 2>/dev/null | head -1 || true)

if [[ -z "$latest_trx" ]]; then
    printf 'VERIFICATION GUARD -- blocking push:\n' >&2
    printf '  .cs files in outgoing commits, but dotnet test has never run in this tree.\n' >&2
    printf '  Run: dotnet test --configuration Release --results-directory TestResults/ --logger trx\n' >&2
    printf '  Or bypass: SKIP_VERIFY_CHECK=1 git push ...\n' >&2
    exit 2
fi

trx_time=$(stat -c '%Y' "$latest_trx" 2>/dev/null || stat -f '%m' "$latest_trx" 2>/dev/null || echo 0)
stale=()

while IFS= read -r rel; do
    full="$root/$rel"
    [[ -f "$full" ]] || continue
    src_time=$(stat -c '%Y' "$full" 2>/dev/null || stat -f '%m' "$full" 2>/dev/null || echo 0)
    [[ "$src_time" -gt "$trx_time" ]] && stale+=("  $rel")
done <<< "$changed"

[[ ${#stale[@]} -eq 0 ]] && exit 0

printf 'VERIFICATION GUARD -- blocking push:\n' >&2
printf '  .cs files modified after last test run:\n' >&2
for s in "${stale[@]:0:3}"; do printf '%s\n' "$s" >&2; done
[[ ${#stale[@]} -gt 3 ]] && printf '  ... and %d more\n' "$((${#stale[@]} - 3))" >&2
printf '  Run: dotnet test --configuration Release --results-directory TestResults/ --logger trx\n' >&2
printf '  Or bypass: SKIP_VERIFY_CHECK=1 git push ...\n' >&2
exit 2
