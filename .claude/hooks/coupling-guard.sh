#!/usr/bin/env bash
# PreToolUse: Bash — fires on git commit. Checks two coupling axes:
# 1. New <ProjectReference> in a .csproj (horizontal module dependency)
# 2. Core interface modified without lockstep (all implementors + arch tests + ADR)
# Override: SKIP_COUPLING_CHECK=1 git commit ...

set -uo pipefail

payload=$(cat)
cmd=$(printf '%s' "$payload" | grep -oE '"command":"[^"]*"' | head -1 | grep -oE '"[^"]*"$' | tr -d '"')

echo "$cmd" | grep -qE '\bgit\b.*\bcommit\b' || exit 0
echo "$cmd" | grep -q 'SKIP_COUPLING_CHECK=1' && exit 0

findings=()

# Axis 0: skipped tests committed — ACs not verified, "done" definition violated
if git diff --staged -- '*.cs' | grep -qE '^\+.*\[Fact\(Skip'; then
    findings+=("SKIPPED TEST COMMITTED: [Fact(Skip)] found in staged diff.")
    findings+=("  Remove Skip before committing. If blocked, open a tracking Issue instead.")
fi

# Axis 1: new ProjectReference in a csproj
if git diff --staged -- '*.csproj' | grep -qE '^\+.*<ProjectReference'; then
    findings+=("HORIZONTAL MODULE REFERENCE: <ProjectReference> added to a .csproj.")
    findings+=("  Verify it points to Core only (or this is the composition root).")
    findings+=("  No sibling module cross-references allowed.")
fi

# Axis 2: Core interface file modified
core_changed=$(git diff --staged --name-only | grep -iE '\.Core[./].*\.cs$' || true)
if [[ -n "$core_changed" ]] && git diff --staged -- "$core_changed" | grep -qE '^\+\s+(interface |Task |ValueTask )'; then
    findings+=("CORE INTERFACE CHANGE: Core/.cs interface modified.")
    findings+=("  Required lockstep: all implementors updated, architecture tests pass, ADR created if structural.")
fi

# Axis 3: .claude/ system file changed without GUIDE.md update
if git diff --staged --name-only | grep -qE '^\.claude/(hooks|skills|commands|settings\.json)'; then
    if ! git diff --staged --name-only | grep -q '^\.claude/GUIDE\.md'; then
        findings+=("GUIDE OUT OF SYNC: .claude/ system file changed without updating .claude/GUIDE.md.")
        findings+=("  Document the change in GUIDE.md (same commit). Override: SKIP_COUPLING_CHECK=1")
    fi
fi

# Axis 4: .github/ workflow/template file changed without .github/GUIDE.md update
if git diff --staged --name-only | grep -qE '^\.github/(workflows|ISSUE_TEMPLATE|CODEOWNERS|dependabot\.yml|PULL_REQUEST_TEMPLATE)'; then
    if ! git diff --staged --name-only | grep -q '^\.github/GUIDE\.md'; then
        findings+=("GITHUB GUIDE OUT OF SYNC: .github/ system file changed without updating .github/GUIDE.md.")
        findings+=("  Document the change in .github/GUIDE.md (same commit). Override: SKIP_COUPLING_CHECK=1")
    fi
fi

[[ ${#findings[@]} -eq 0 ]] && exit 0

printf 'COUPLING GUARD -- review required before committing:\n' >&2
for f in "${findings[@]}"; do printf '  %s\n' "$f" >&2; done
printf '\n  Override (confirmed safe): SKIP_COUPLING_CHECK=1 git commit ...\n' >&2
exit 2
