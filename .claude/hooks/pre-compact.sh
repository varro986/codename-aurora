#!/usr/bin/env bash
# PreCompact — injects current git state into the summary before context compression
# so the next context window knows exactly where the session left off.

set -uo pipefail

root="${CLAUDE_PROJECT_DIR:-$(pwd)}"
time=$(date '+%Y-%m-%d %H:%M')

branch=$(git branch --show-current 2>/dev/null || true)
last_commits=$(git log --oneline -5 2>/dev/null || true)
staged=$(git diff --name-only --cached 2>/dev/null || true)
unstaged=$(git diff --name-only 2>/dev/null || true)
stashes=$(git stash list 2>/dev/null || true)

_trx=()
while IFS= read -r f; do _trx+=("$f"); done < <(find "$root" -name '*.trx' -path '*/TestResults/*' 2>/dev/null)
latest_trx=$( [[ ${#_trx[@]} -gt 0 ]] && ls -t "${_trx[@]}" 2>/dev/null | head -1 || true )
last_test=${latest_trx:+$(basename "$latest_trx") @ $(stat -c '%y' "$latest_trx" 2>/dev/null | cut -c12-16 || stat -f '%Sm' -t '%H:%M' "$latest_trx" 2>/dev/null)}
last_test="${last_test:-none found}"

summary="## Session state — pre-compact $time

**Branch:** \`${branch:-unknown}\`"

[[ -n "$last_commits" ]] && summary+="
**Last 5 commits:**
\`\`\`
$last_commits
\`\`\`"

[[ -n "$staged" ]] && summary+="
**Staged:**
\`\`\`
$staged
\`\`\`"

[[ -n "$unstaged" ]] && summary+="
**Unstaged:**
\`\`\`
$unstaged
\`\`\`"

[[ -n "$stashes" ]] && summary+="
**Stashes:**
\`\`\`
$stashes
\`\`\`"

summary+="
**Last test run:** $last_test

**Architecture reminders:**
- \`#nullable enable\` on every .cs file
- All I/O async — no \`.Result\`, \`.Wait()\`, \`.GetAwaiter().GetResult()\`
- Interfaces at every layer boundary, never concretions
- No secrets in commits — secret-scan hook fires on every commit
- \`dotnet build\` does NOT compile test projects — run \`dotnet test --logger trx\` before push"

escaped=$(printf '%s' "$summary" | sed 's/\\/\\\\/g; s/"/\\"/g; s/$/\\n/g' | tr -d '\n' | sed 's/\\n$//')
printf '{"summary":"%s"}\n' "$escaped"
