Run a holistic audit of the ENTIRE repository, no exceptions.

For every file in the project (exclude: bin/, obj/, node_modules/, old/):

1. **Read the file** — no file is skipped.
2. **Verify the laws** in AGENTS.md:
   - `#nullable enable` present in every `.cs`
   - No inline `//` inside method bodies (unless a documented WHY-only exception)
   - No `.Result` / `.Wait()` / `.GetAwaiter().GetResult()`
   - Interfaces at layer boundaries, never concretions
   - No hardcoded secrets
   - Zero new warnings (TreatWarningsAsErrors)
3. **Verify AS smells** (AS-1..AS-6):
   - AS-1 Reviewer-persuasion comments in the diff
   - AS-2 Leaked internal referents (task IDs, tool names, etc.)
   - AS-3 Smuggled behavior change under "refactor"
   - AS-4 Hot-path cost blindness (undeclared I/O on critical paths)
   - AS-5 Coverage theater (tests asserting mock calls, not invariants)
   - AS-6 Context-window responsiveness (open review threads ignored)
4. **Verify module isolation**: dependencies inward-only, Core has no outward deps, no sibling module cross-references.

Report every violation with `file:line`, violated rule, severity (`blocking` / `required` / `advisory`).
Conclusion: total count by severity. No file is exempt.
