#!/usr/bin/env bash
# PostToolUse: Edit|Write — checks added lines in .cs/.feature files against house rules.
# Reads added content from git diff (tracked files) or full file (new files).
# Fails open on any error.

set -uo pipefail

payload=$(cat)

tool_name=$(printf '%s' "$payload" | grep -oE '"tool_name":"[^"]*"' | head -1 | grep -oE '"[^"]*"$' | tr -d '"')
[[ "$tool_name" == "Edit" || "$tool_name" == "Write" ]] || exit 0

file_path=$(printf '%s' "$payload" | grep -oE '"file_path":"[^"]*"' | head -1 | grep -oE '"[^"]*"$' | tr -d '"' | sed 's/\\\\/\\/g')
ext="${file_path##*.}"
[[ "$ext" == "cs" || "$ext" == "feature" ]] || exit 0
[[ -f "$file_path" ]] || exit 0

# Added lines only: git diff for tracked files, full file for new ones
if git ls-files --error-unmatch "$file_path" &>/dev/null 2>&1; then
    added=$(git diff HEAD -- "$file_path" 2>/dev/null | grep '^+[^+]' | sed 's/^+//')
else
    added=$(cat "$file_path")
fi
[[ -n "$added" ]] || exit 0

# violated <include_regex> [skip_regex] [grep_flags]
# Returns 0 (violation) if any line matches include AND does NOT match skip.
# O(2) grep processes per rule regardless of file size.
violated() {
    local include="$1" skip="${2:-}" flags="${3:-}"
    if [[ -z "$skip" ]]; then
        echo "$added" | grep -qE $flags "$include"
        return $?
    fi
    echo "$added" | grep -E $flags "$include" | grep -qvE "$skip"
}

is_test=false
echo "$file_path" | grep -qiE '[Tt]est' && is_test=true
is_program=false
echo "$file_path" | grep -qE 'Program\.cs$' && is_program=true

findings=()

violated '^\s+//[^/]' '^\s*///' \
    && findings+=("[NO_WHAT_COMMENT] Inline // must explain WHY, not WHAT. English only.")

violated '\.Result\b|\.Wait\(\)|\.GetAwaiter\(\)\.GetResult\(\)' '' \
    && ! $is_program \
    && findings+=("[NO_BLOCKING_ASYNC] Use await. Exception: top-level Program.cs only.")

violated '\basync\s+void\b' 'EventHandler|EventArgs' \
    && findings+=("[NO_ASYNC_VOID] Return Task instead. Exception: event handlers.")

violated 'catch\s*(\([^)]*\))?\s*\{\s*\}' '' \
    && findings+=("[NO_EMPTY_CATCH] Empty catch swallows exceptions. Log, rethrow, or handle.")

violated '\.Dispatcher\.Invoke\(' '' \
    && ! $is_test \
    && findings+=("[NO_DISPATCHER_INVOKE] Use Dispatcher.InvokeAsync instead.")

violated '\b(FIXME|STOPSHIP):' '' \
    && findings+=("[FORBIDDEN_COMMENT] FIXME/STOPSHIP must not be committed. Open an Issue instead.")

# Null-forgiveness without a WHY comment on the same line
violated '[a-zA-Z0-9_)>]!\.' ' // \S' \
    && findings+=("[NO_NULLABLE_SUPPRESSION] Null-forgiveness (!) without a WHY comment. Make nullability explicit or add // reason.")

if ! $is_test && ! $is_program; then
    violated '\bConsole\.Write(Line)?\(' '' \
        && findings+=("[NO_CONSOLE_WRITELINE] Use ILogger<T> for structured logging.")

    violated '(password|passwd|pwd)\s*=\s*"[^"]{3}|"Server=|"Data Source=' '' '-i' \
        && findings+=("[NO_HARDCODED_SECRET] Hardcoded credential or connection string. Use IConfiguration.")
fi

# New .cs file missing #nullable enable (only fires if file is new — not tracked by git yet)
if [[ "$ext" == "cs" ]] && ! git ls-files --error-unmatch "$file_path" &>/dev/null 2>&1; then
    grep -q '#nullable enable' "$file_path" \
        || findings+=("[MISSING_NULLABLE] New .cs file missing '#nullable enable'. Required on every file.")
fi

[[ ${#findings[@]} -eq 0 ]] && exit 0

printf 'SMELL GUARD: house rule violations in %s:\n' "$file_path" >&2
for f in "${findings[@]}"; do printf '  %s\n' "$f" >&2; done
printf 'Fix before proceeding — CI enforces the same rules on the PR diff.\n' >&2
exit 2
