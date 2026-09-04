#!/usr/bin/env bash
# PreToolUse: Bash — blocks git commit when staged content matches secret patterns.
# If this fires on a real secret: remove it, rotate it, audit git history.
# Override: SKIP_SECRET_SCAN=1 git commit ...

set -uo pipefail

payload=$(cat)
cmd=$(printf '%s' "$payload" | grep -oE '"command":"[^"]*"' | head -1 | grep -oE '"[^"]*"$' | tr -d '"')

echo "$cmd" | grep -qE '\bgit\b.*\bcommit\b' || exit 0
echo "$cmd" | grep -q 'SKIP_SECRET_SCAN=1' && exit 0

# Only scan added lines (+, not +++)
added=$(git diff --staged --unified=0 2>/dev/null | grep '^+[^+]' | sed 's/^+//' || true)
[[ -n "$added" ]] || exit 0

findings=()

echo "$added" | grep -qE 'AKIA[0-9A-Z]{16}' \
    && findings+=("[AWS access key] pattern: AKIA...")

echo "$added" | grep -qE 'sk-ant-[A-Za-z0-9_-]{10,}' \
    && findings+=("[Anthropic key] pattern: sk-ant-...")

echo "$added" | grep -qE 'sk-[A-Za-z0-9]{20,}' \
    && findings+=("[OpenAI-style key] pattern: sk-...")

echo "$added" | grep -qE '-----BEGIN (RSA|EC|OPENSSH|PGP) PRIVATE' \
    && findings+=("[PEM private key] pattern: -----BEGIN ... PRIVATE")

echo "$added" | grep -qE 'ghp_[A-Za-z0-9]{36,}' \
    && findings+=("[GitHub PAT] pattern: ghp_...")

echo "$added" | grep -qE 'gho_[A-Za-z0-9]{36,}' \
    && findings+=("[GitHub OAuth token] pattern: gho_...")

echo "$added" | grep -qE 'ghu_[A-Za-z0-9]{36,}' \
    && findings+=("[GitHub user-to-server token] pattern: ghu_...")

echo "$added" | grep -qE 'github_pat_[A-Za-z0-9_]{82,}' \
    && findings+=("[GitHub fine-grained PAT] pattern: github_pat_...")

echo "$added" | grep -qiE '(password|passwd|pwd)\s*[=:]\s*['"'"'"][^'"'"'"]{4,}['"'"'"]' \
    && findings+=("[Hardcoded password] pattern: password = '...'")

echo "$added" | grep -qiE '(api[_-]?key|apikey|access[_-]?key|secret[_-]?key)\s*[=:]\s*['"'"'"][^'"'"'"]{8,}['"'"'"]' \
    && findings+=("[API key assignment] pattern: api_key = '...'")

echo "$added" | grep -qiE '(server|host)=[^;]+;[^;]*(password|pwd)=' \
    && findings+=("[Connection string with password]")

[[ ${#findings[@]} -eq 0 ]] && exit 0

printf 'SECRET SCAN -- blocking commit:\n' >&2
printf '  Staged content matches secret patterns:\n' >&2
for f in "${findings[@]}"; do printf '  %s\n' "$f" >&2; done
printf '\n  If REAL: remove it, rotate it, audit git history.\n' >&2
printf '  If false positive: SKIP_SECRET_SCAN=1 git commit ...\n' >&2
exit 2
