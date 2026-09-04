#!/usr/bin/env bash
# CI smell check — ADDED lines in the PR diff against house rules.
# Usage: bash scripts/ci/smell-check.sh <git-range>
# Exit 0: clean. Exit 1: findings. Fails open on internal errors.

set -uo pipefail

range="${1:-}"
diff_file="${2:-}"

if [[ -n "$diff_file" ]]; then
    diff_text=$(cat "$diff_file") || exit 0
elif [[ -n "$range" ]]; then
    diff_text=$(git diff --unified=0 "$range" 2>/dev/null) || exit 0
else
    echo "smell-check: usage: smell-check.sh <git-range> | smell-check.sh --diff-file <path>"; exit 0
fi
[[ -n "$diff_text" ]] || { echo "smell-check: clean (empty diff)"; exit 0; }

findings=()
current_file=""

check_line() {
    local file="$1" line="$2"
    local is_test=false is_program=false

    echo "$file" | grep -qiE '(^|/)tests?/' && is_test=true
    echo "$file" | grep -qE 'Program\.cs$'   && is_program=true

    if [[ "$file" =~ \.cs$ ]]; then
        echo "$line" | grep -qE '^\s+//[^/]' && ! echo "$line" | grep -qE '^\s*///' \
            && findings+=("$file: [NO_WHAT_COMMENT] Inline // must explain WHY, not WHAT")

        ! $is_program && echo "$line" | grep -qE '\.Result\b|\.Wait\(\)|\.GetAwaiter\(\)\.GetResult\(\)' \
            && findings+=("$file: [NO_BLOCKING_ASYNC] Use await. Exception: Program.cs only")

        echo "$line" | grep -qE '\basync\s+void\b' && ! echo "$line" | grep -qE 'EventHandler|EventArgs' \
            && findings+=("$file: [NO_ASYNC_VOID] Return Task instead. Exception: event handlers")

        echo "$line" | grep -qE 'catch\s*(\([^)]*\))?\s*\{\s*\}' \
            && findings+=("$file: [NO_EMPTY_CATCH] Empty catch swallows exceptions")

        ! $is_test && echo "$line" | grep -qiE '("Server=|"Data Source=|"mongodb://|password\s*=\s*"[^"]{3})' \
            && findings+=("$file: [NO_HARDCODED_CONNECTION_STRING] Use IConfiguration")

        ! $is_program && ! $is_test && ! echo "$file" | grep -qE 'Presentation/' \
            && echo "$line" | grep -qE '\bConsole\.Write(Line)?\(' \
            && findings+=("$file: [NO_CONSOLE_WRITELINE] Use ILogger<T>")

        echo "$line" | grep -qE '[a-zA-Z0-9_)\]>]![.;,)[:space:]]' \
            && ! echo "$line" | grep -qE '!=|//|///' \
            && findings+=("$file: [NO_NULLABLE_SUPPRESSION] Add WHY comment or make nullability explicit")

        ! $is_test && echo "$line" | grep -qE '\.Dispatcher\.Invoke\(' \
            && findings+=("$file: [NO_DISPATCHER_INVOKE] Use Dispatcher.InvokeAsync instead")
    fi

    if [[ "$file" =~ \.(cs|feature)$ ]]; then
        echo "$line" | grep -qE '\b(FIXME|STOPSHIP):' \
            && findings+=("$file: [FORBIDDEN_COMMENT] Resolve the issue or open a GitHub Issue")
    fi
}

while IFS= read -r line; do
    if [[ "$line" =~ ^\+\+\+\ b/(.+)$ ]]; then
        current_file="${BASH_REMATCH[1]}"
    elif [[ -n "$current_file" && "$line" =~ ^\+[^\+] ]]; then
        check_line "$current_file" "${line:1}"
    fi
done <<< "$diff_text"

if [[ ${#findings[@]} -eq 0 ]]; then
    echo "smell-check: clean"
    exit 0
fi

printf 'SMELL CHECK: %d finding(s)\n' "${#findings[@]}"
for f in "${findings[@]}"; do printf '  %s\n' "$f"; done
exit 1
