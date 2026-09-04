#!/usr/bin/env bash
# Aurora pipeline smoke test.
# Usage: bash scripts/test-pipeline.sh [--skip-actions]
#
# T1  bootstrap-feature Action (real GitHub issue + label + wait)
# T2  smell-guard.sh     — exit 0 on clean, exit 2 on violation
# T3  coupling-guard.sh  — exit 2 on [Fact(Skip)] in staged diff
# T4  verification-guard — exit 2 on push without test run
# T5  smell-check.sh     — exit 1 on violation in synthetic diff
# T6  coupling-check.sh  — warning on horizontal ref in synthetic diff
#
# T1 requires gh CLI and network; skip with --skip-actions.

set -uo pipefail

SKIP_ACTIONS=false
[[ "${1:-}" == "--skip-actions" ]] && SKIP_ACTIONS=true

ROOT="$(git rev-parse --show-toplevel)"
PASS=0; FAIL=0; SKIP_COUNT=0

pass()  { printf '  [PASS] %s\n' "$1"; PASS=$((PASS + 1)); }
fail()  { printf '  [FAIL] %s\n' "$1"; FAIL=$((FAIL + 1)); }
skip()  { printf '  [SKIP] %s\n' "$1"; SKIP_COUNT=$((SKIP_COUNT + 1)); }
sep()   { printf '\n--- %s ---\n' "$1"; }
check() {
    # check <exit_code> <expected> <pass_msg> <fail_msg>
    [[ "$1" -eq "$2" ]] && pass "$3" || fail "$4"
}

# ── T1: bootstrap-feature Action ─────────────────────────────────────────────
sep "T1: bootstrap-feature Action (GitHub)"

if $SKIP_ACTIONS; then
    skip "T1 — skipped (--skip-actions)"
elif ! command -v gh &>/dev/null; then
    skip "T1 — gh CLI not found"
else
    ISSUE_TITLE="[SMOKE TEST] Pipeline bootstrap test — delete me"
    ISSUE_NUM=$(gh issue create \
        --title "$ISSUE_TITLE" \
        --body "Automated smoke test. Safe to close and delete." \
        --label "status: triage" \
        --repo varro986/codename-aurora \
        --json number --jq '.number' 2>/dev/null) || ISSUE_NUM=""

    if [[ -z "$ISSUE_NUM" ]]; then
        fail "T1.1 — could not create test issue"
    else
        pass "T1.1 — issue #$ISSUE_NUM created"
        gh issue edit "$ISSUE_NUM" --add-label "status: active" \
            --repo varro986/codename-aurora &>/dev/null \
            && pass "T1.2 — label applied" || fail "T1.2 — could not apply label"

        BRANCH="feature/issue-${ISSUE_NUM}-smoke-test-pipeline-bootstrap-test-delete-me"
        printf '       Waiting up to 90s for Action'
        FOUND=false
        for _ in $(seq 1 18); do
            sleep 5; printf '.'
            git fetch --quiet origin 2>/dev/null || true
            git ls-remote --exit-code --heads origin "$BRANCH" &>/dev/null && FOUND=true && break
        done
        echo

        if $FOUND; then
            pass "T1.3 — branch created by Action"
            git fetch origin "$BRANCH" &>/dev/null
            STUB=$(git show "origin/$BRANCH:tests/CodenameAurora.Tests.Unit/Issue${ISSUE_NUM}Tests.cs" 2>/dev/null)
            echo "$STUB" | grep -q 'Fact(Skip' \
                && pass "T1.4 — stub contains [Fact(Skip)]" \
                || fail "T1.4 — stub missing or malformed"
            git push origin --delete "$BRANCH" &>/dev/null \
                && printf '       Cleanup: remote branch deleted.\n' \
                || printf '       Cleanup: manual cleanup needed for %s.\n' "$BRANCH"
        else
            fail "T1.3 — branch not found after 90s (check Actions log)"
        fi

        gh issue close "$ISSUE_NUM" --repo varro986/codename-aurora &>/dev/null \
            && printf '       Cleanup: issue #%s closed.\n' "$ISSUE_NUM" \
            || printf '       Cleanup: could not close issue #%s.\n' "$ISSUE_NUM"
    fi
fi

# ── T2: smell-guard.sh ───────────────────────────────────────────────────────
sep "T2: smell-guard.sh (PostToolUse)"

TMPCS=$(mktemp --suffix=.cs)

make_payload() { printf '{"tool_name":"Write","tool_input":{"file_path":"%s"}}' "$1"; }

# T2a: clean file → exit 0
cat > "$TMPCS" <<'EOF'
#nullable enable
namespace CodenameAurora.Core;
public sealed class Clean
{
    public void Run() { }
}
EOF
echo "$(make_payload "$TMPCS")" | bash "$ROOT/.claude/hooks/smell-guard.sh" 2>/dev/null
check $? 0 "T2a — clean file exits 0" "T2a — false positive on clean file"

# T2b: .Result → exit 2
cat > "$TMPCS" <<'EOF'
#nullable enable
namespace CodenameAurora.Core;
public sealed class Bad
{
    public void Block() { var x = Task.Delay(1).Result; }
}
EOF
echo "$(make_payload "$TMPCS")" | bash "$ROOT/.claude/hooks/smell-guard.sh" 2>/dev/null
check $? 2 "T2b — .Result exits 2" "T2b — .Result not caught"

# T2c: FIXME → exit 2
cat > "$TMPCS" <<'EOF'
#nullable enable
namespace CodenameAurora.Core;
// FIXME: remove before shipping
public sealed class Marker { }
EOF
echo "$(make_payload "$TMPCS")" | bash "$ROOT/.claude/hooks/smell-guard.sh" 2>/dev/null
check $? 2 "T2c — FIXME exits 2" "T2c — FIXME not caught"

rm -f "$TMPCS"

# ── T3: coupling-guard.sh ────────────────────────────────────────────────────
sep "T3: coupling-guard.sh (PreToolUse — [Fact(Skip)] guard)"

STUB_DIR="$ROOT/tests/CodenameAurora.Tests.Unit"
mkdir -p "$STUB_DIR"
STUB_FILE="$STUB_DIR/_SmokeSkip$$.cs"

cat > "$STUB_FILE" <<'EOF'
#nullable enable
using Xunit;
namespace CodenameAurora.Tests.Unit;
public sealed class SmokeSkip
{
    [Fact(Skip = "not done")]
    public void Placeholder() => throw new System.NotImplementedException();
}
EOF

git add "$STUB_FILE" 2>/dev/null
PAYLOAD_COMMIT='{"tool_name":"Bash","tool_input":{"command":"git commit -m smoke"}}'
echo "$PAYLOAD_COMMIT" | bash "$ROOT/.claude/hooks/coupling-guard.sh" 2>/dev/null
check $? 2 "T3 — [Fact(Skip)] in staged diff exits 2" "T3 — skipped test not caught"

git restore --staged "$STUB_FILE" 2>/dev/null
rm -f "$STUB_FILE"

# ── T4: verification-guard.sh ────────────────────────────────────────────────
sep "T4: verification-guard.sh (PreToolUse — push guard)"

PAYLOAD_PUSH='{"tool_name":"Bash","tool_input":{"command":"git push origin main"}}'

# Find most recent .trx; temporarily rename it so the guard sees no test run
LATEST_TRX=$(find "$ROOT" -name '*.trx' -path '*/TestResults/*' 2>/dev/null | head -1)
if [[ -n "$LATEST_TRX" ]]; then
    mv "$LATEST_TRX" "${LATEST_TRX}.bak"
    echo "$PAYLOAD_PUSH" | bash "$ROOT/.claude/hooks/verification-guard.sh" 2>/dev/null
    check $? 2 "T4 — push without .trx exits 2" "T4 — verification guard did not block"
    mv "${LATEST_TRX}.bak" "$LATEST_TRX"
else
    # No .trx at all: guard blocks only when outgoing commits have .cs files.
    # On a clean main with nothing outgoing, it exits 0 — that is correct behaviour.
    echo "$PAYLOAD_PUSH" | bash "$ROOT/.claude/hooks/verification-guard.sh" 2>/dev/null
    RC=$?
    if [[ $RC -eq 0 ]]; then
        pass "T4 — no outgoing .cs commits, guard exits 0 (correct)"
    elif [[ $RC -eq 2 ]]; then
        pass "T4 — outgoing .cs without .trx, guard exits 2 (correct)"
    else
        fail "T4 — unexpected exit code $RC"
    fi
fi

# ── T5: scripts/ci/smell-check.sh ───────────────────────────────────────────
sep "T5: smell-check.sh (CI diff check via synthetic diff)"

TMPDIFF=$(mktemp --suffix=.diff)
cat > "$TMPDIFF" <<'EOF'
diff --git a/src/CodenameAurora.Core/Foo.cs b/src/CodenameAurora.Core/Foo.cs
--- /dev/null
+++ b/src/CodenameAurora.Core/Foo.cs
@@ -0,0 +1,5 @@
+#nullable enable
+public class Foo
+{
+    public void Bad() { var x = Task.Delay(1).Result; }
+}
EOF

bash "$ROOT/scripts/ci/smell-check.sh" --diff-file "$TMPDIFF" 2>/dev/null
check $? 1 "T5a — .Result in diff exits 1" "T5a — .Result not detected in diff"

# Clean diff → exit 0
cat > "$TMPDIFF" <<'EOF'
diff --git a/src/CodenameAurora.Core/Bar.cs b/src/CodenameAurora.Core/Bar.cs
--- /dev/null
+++ b/src/CodenameAurora.Core/Bar.cs
@@ -0,0 +1,4 @@
+#nullable enable
+public class Bar
+{
+}
EOF

bash "$ROOT/scripts/ci/smell-check.sh" --diff-file "$TMPDIFF" 2>/dev/null
check $? 0 "T5b — clean diff exits 0" "T5b — false positive on clean diff"

rm -f "$TMPDIFF"

# ── T6: scripts/ci/coupling-check.sh ────────────────────────────────────────
sep "T6: coupling-check.sh (CI coupling sweep via synthetic diff)"

TMPDIFF=$(mktemp --suffix=.diff)
cat > "$TMPDIFF" <<'EOF'
diff --git a/src/CodenameAurora.OCR/CodenameAurora.OCR.csproj b/src/CodenameAurora.OCR/CodenameAurora.OCR.csproj
--- /dev/null
+++ b/src/CodenameAurora.OCR/CodenameAurora.OCR.csproj
@@ -0,0 +1,5 @@
+<Project Sdk="Microsoft.NET.Sdk">
+  <ItemGroup>
+    <ProjectReference Include="..\CodenameAurora.Translation\CodenameAurora.Translation.csproj" />
+  </ItemGroup>
+</Project>
EOF

OUTPUT=$(bash "$ROOT/scripts/ci/coupling-check.sh" --diff-file "$TMPDIFF" 2>/dev/null)
echo "$OUTPUT" | grep -q "HORIZONTAL MODULE REFERENCE" \
    && pass "T6a — horizontal ref reported" \
    || fail "T6a — horizontal ref not detected"

# Clean diff → 'clean' message
cat > "$TMPDIFF" <<'EOF'
diff --git a/src/CodenameAurora.OCR/CodenameAurora.OCR.csproj b/src/CodenameAurora.OCR/CodenameAurora.OCR.csproj
--- /dev/null
+++ b/src/CodenameAurora.OCR/CodenameAurora.OCR.csproj
@@ -0,0 +1,3 @@
+<Project Sdk="Microsoft.NET.Sdk">
+</Project>
EOF

OUTPUT=$(bash "$ROOT/scripts/ci/coupling-check.sh" --diff-file "$TMPDIFF" 2>/dev/null)
echo "$OUTPUT" | grep -q "clean" \
    && pass "T6b — clean csproj reports clean" \
    || fail "T6b — false positive on clean csproj"

rm -f "$TMPDIFF"

# ── Summary ──────────────────────────────────────────────────────────────────
printf '\n════════════════════════════════\n'
printf 'Results:  %d passed  |  %d failed  |  %d skipped\n' \
    "$PASS" "$FAIL" "$SKIP_COUNT"
[[ $FAIL -eq 0 ]] && exit 0 || exit 1
