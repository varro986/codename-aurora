#!/usr/bin/env bash
# dev.sh — dev automation (build, run, test, logs, format).
# One entry point so the whole local dev workflow is a single approved command.
#
# Usage: bash scripts/dev.sh <command> [args]
#   pid                     print the running Aurora process id (empty if not running)
#   run    [--build]        launch Aurora; --build runs dotnet build first
#   kill                    kill the running Aurora process
#   test   [filter]         run dotnet test; optional filter passed to --filter
#   build  [Release|Debug]  dotnet build (default: Debug)
#   format [--check]        run csharpier; --check validates without writing
#   logs   [--tail N]       stream last N lines of the log file (default: open in editor)
#
# Set AURORA_SLN to the solution path if not auto-discovered.
# Set AURORA_LOG_DIR to override the default log directory.

set -euo pipefail

PROCESS_NAME="CodenameAurora.App"
SLN="${AURORA_SLN:-$(find . -name '*.slnx' -maxdepth 3 2>/dev/null | head -1)}"

# Windows: $APPDATA/CodenameAurora/logs — Linux: ~/.config/CodenameAurora/logs
if [[ -n "${APPDATA:-}" ]]; then
    DEFAULT_LOG_DIR="$APPDATA/CodenameAurora/logs"
else
    DEFAULT_LOG_DIR="${XDG_CONFIG_HOME:-$HOME/.config}/CodenameAurora/logs"
fi
LOG_DIR="${AURORA_LOG_DIR:-$DEFAULT_LOG_DIR}"

aurora_pid() {
    pgrep -x "$PROCESS_NAME" 2>/dev/null | head -1 || true
}

cmd_pid() {
    local p; p=$(aurora_pid)
    [[ -n "$p" ]] && printf '%s\n' "$p"
}

cmd_run() {
    local build=false
    [[ "${1:-}" == "--build" ]] && build=true
    $build && cmd_build "Debug"
    local p; p=$(aurora_pid)
    if [[ -n "$p" ]]; then
        printf 'Aurora already running (pid %s)\n' "$p"
        return 0
    fi
    dotnet run --project src/CodenameAurora.App/CodenameAurora.App.csproj &
    printf 'Aurora launched\n'
}

cmd_kill() {
    local p; p=$(aurora_pid)
    if [[ -n "$p" ]]; then
        kill "$p" && printf 'Killed pid %s\n' "$p"
    else
        printf 'Aurora not running\n'
    fi
}

cmd_test() {
    local filter="${1:-}"
    local args=(test --configuration Debug --logger trx --results-directory TestResults/)
    [[ -n "$SLN" ]] && args+=("$SLN")
    [[ -n "$filter" && "$filter" != --* ]] && args+=(--filter "$filter")
    dotnet "${args[@]}"
}

cmd_build() {
    local config="${1:-Debug}"
    local args=(build --configuration "$config")
    [[ -n "$SLN" ]] && args+=("$SLN")
    dotnet "${args[@]}"
}

cmd_format() {
    if [[ "${1:-}" == "--check" ]]; then
        dotnet csharpier --check .
    else
        dotnet csharpier .
    fi
}

cmd_logs() {
    local tail=0
    while [[ $# -gt 0 ]]; do
        case "$1" in
            --tail) tail="${2:-50}"; shift 2 ;;
            *) shift ;;
        esac
    done
    [[ -d "$LOG_DIR" ]] || { printf 'Log directory not found: %s\n' "$LOG_DIR" >&2; exit 1; }
    local latest
    latest=$(ls -t "$LOG_DIR"/*.log 2>/dev/null | head -1 || true)
    [[ -n "$latest" ]] || { printf 'No log files in %s\n' "$LOG_DIR"; return 0; }
    if [[ "$tail" -gt 0 ]]; then
        tail -n "$tail" -f "$latest"
    else
        "${EDITOR:-less}" "$latest"
    fi
}

cmd="${1:-help}"
shift || true

case "$cmd" in
    pid)    cmd_pid ;;
    run)    cmd_run "$@" ;;
    kill)   cmd_kill ;;
    test)   cmd_test "$@" ;;
    build)  cmd_build "$@" ;;
    format) cmd_format "$@" ;;
    logs)   cmd_logs "$@" ;;
    *)      sed -n '3,11p' "$0" | sed 's/^# //' ;;
esac
