# Agent Constitution

This file is the structural law of the project for ANY coding agent (Claude,
Cursor, Copilot, and successors). It defines architecture, what "done" means,
fragile zones, security rules, and the routing table.

Read it as binding. Deep domain guidance lives in the skills named by the
routing table at the end. Laws here carry their justified exceptions: a rule,
its exception, why the exception is legitimate, and the boundary where it
becomes a violation again.

---

## Architecture

Modular Monolith. All dependencies converge on `CodenameAurora.Core`. See `specs/architecture/architecture.md` for the full contract reference.

```
CodenameAurora.App           ← WinExe, composition root, PipelineExecutor, DI wiring
CodenameAurora.Core          ← interfaces, notifications, domain types (no outward deps)
CodenameAurora.OCR           → Core only  (WinRT Windows.Media.Ocr)
CodenameAurora.Translation   → Core only  (cascade: private.json → generic.json → verbatim)
CodenameAurora.UI            → Core only  (WPF overlay, system tray, hotkey manager)
CodenameAurora.Admin         → Core only  (settings r/w, auto-update)
```

Isolation rules enforced by NetArchTest — `CodenameAurora.App` is the sole exception (composition root):

| Module | Must NOT reference |
|---|---|
| `CodenameAurora.OCR` | Translation, UI, Admin |
| `CodenameAurora.Translation` | OCR, UI, Admin |
| `CodenameAurora.UI` | OCR, Translation, Admin |
| `CodenameAurora.Admin` | OCR, Translation, UI |
| `CodenameAurora.Core` | any other module |

Rules that apply across all modules:

- `#nullable enable` is mandatory on every `.cs` file.
- Depend on abstractions (interfaces) across layer boundaries, never concretions.
- No static mutable state outside of configuration bootstrapping.
- All I/O is async (`Task`/`ValueTask`). No `.Result`, `.Wait()`, or
  `.GetAwaiter().GetResult()`. Exception: top-level entry points where an async
  host is unavailable — document the reason inline above the call.
- Exception handling belongs at layer boundaries, not scattered through use cases.
- Write operations must be idempotent: calling the same operation twice with the
  same arguments must not accumulate side effects (settings save, dict reload, label setup).
- Comments explain WHY, not WHAT. English only.
  - XML doc (`/// <summary>`) required above all non-trivial public declarations.
  - Inline `//` allowed only for non-obvious invariants or workarounds; one line.
  - Zero `//` inside method bodies for anything the code already makes obvious.

**Known debt** (do not extend; do not use as licence):

[none yet]

---

## Branch Strategy

GitHub Flow — `main` is always stable, every change goes through a PR. No exceptions.

| Branch type | Naming | Created by |
|---|---|---|
| Default | `main` | Always exists; protected |
| Feature | `feature/issue-N-slug` | Bootstrap workflow (auto on `status: active` label) |
| Hotfix | `hotfix/issue-N-slug` | Manual checkout from `main` |

**Flow (identical for 1 developer or N):**
1. Label Issue `status: active` → bootstrap creates `feature/issue-N-slug` automatically
2. `git fetch --all && git checkout feature/issue-N-slug`
3. Implement: remove `[Fact(Skip)]`, write tests first, then code
4. `git push origin feature/issue-N-slug` → open PR to `main`
5. CI must pass: `build`, `test`, `lint`
6. Review (self in solo mode / peer in team mode) → merge with **squash**
7. Branch deleted after merge

**Releases:** tag on `main` → `v0.1.0`, `v1.0.0`. The `publish` CI job fires automatically on `refs/tags/v*`.

**Required approvals:** 0 in solo mode; 1 when architects team has 2+ members. Re-run `setup-repo.sh` after adding a second member — it auto-calibrates.

**Never push directly to `main`.** Not even as the repo owner. The PR gate is the only path.

---

## Feature Completeness (what "done" means)

A feature is INCOMPLETE until every item is true. No exceptions without a
justification recorded in the PR description.

### Mandatory (blocking)

- [ ] All Gherkin scenarios in the corresponding `.feature` file pass (Reqnroll)
- [ ] All xUnit tests pass — `Skip` attribute removed
- [ ] `dotnet build --configuration Release` is clean: zero errors, zero new warnings
- [ ] `dotnet csharpier --check .` passes — no formatting drift
- [ ] Error and empty states handled explicitly — no silent failures
- [ ] No hardcoded secrets, connection strings, or magic numbers in code

### Required

- [ ] New code follows the existing patterns for its layer (cite a file:line precedent)
- [ ] Interfaces used at layer boundaries, not concretions
- [ ] No new `async void` (except event handlers — state the reason)
- [ ] No `.Result` or `.Wait()` introduced

---

## Security & Git Hygiene

These rules exist to prevent sensitive data from reaching a remote repository.
Violations in this area are treated as blocking regardless of feature state.

### What never goes into a commit

| File / pattern | Reason |
|---|---|
| `.env`, `.env.*` | Environment secrets |
| `appsettings.*.json` with real values | DB passwords, API keys, connection strings |
| `secrets.json` (User Secrets) | Local dev secrets — stays on the machine |
| `*.pfx`, `*.p12`, `*.key`, `*.pem` | Certificates and private keys |
| Any file matching `*secret*`, `*credential*`, `*password*` | Obvious |

### .gitignore must include (minimum)

```
.env
.env.*
secrets.json
*.pfx
*.p12
*.key
*.pem
appsettings.Production.json
appsettings.Staging.json
```

### Configuration pattern

Secrets never live in code or committed config files. The chain is:

```
Environment variable / Secrets Manager
        ↓
IConfiguration (injected)
        ↓
Strongly-typed options class
        ↓
Consumer (via DI, never via static access)
```

`appsettings.json` holds only structure and non-sensitive defaults.
`appsettings.Development.json` holds local dev overrides with placeholder values
(e.g. `"ConnectionString": "REPLACE_ME"`) — committed, never with real values.

### Before every push

- Run `git diff --staged` and scan for strings matching: password, secret, key,
  token, connectionstring, apikey (case-insensitive).
- The `NO_HARDCODED_CONNECTION_STRING` smell rule catches some of this at
  write time, but it does not replace a manual scan before push.

### Code-level rules (enforced by smell-guard)

- No hardcoded connection strings or credentials in `.cs` files.
- `ILogger<T>` for all logging — never `Console.WriteLine` outside Presentation.
- No secrets passed as plain string arguments across layer boundaries.

---

## Fragile Zones (elevated evidence bar)

Code-reading alone is never sufficient evidence of correctness in these areas.
Each change requires a stated verification method.

| Zone | Risk | Required verification |
|---|---|---|
| WinRT OCR calls (`Windows.Media.Ocr.OcrEngine`) | Requires STA thread; Windows-only; cannot be mocked meaningfully | Integration test on a real Windows runner with a language pack installed |
| Local dictionary reads (`private.json`, `generic.json`) | Path-sensitive; silent fallback can mask a missing file | Integration test with actual file fixtures on disk |
| WPF Dispatcher operations | Cross-thread access throws `InvalidOperationException` without a clear async stack trace | Integration test; explicitly verify non-UI-thread call sites |
| Settings file round-trip (`%APPDATA%\CodenameAurora\settings.json`) | User data; JSON corruption is silent; malformed input must not crash | Integration test: write → read → verify value parity |

---

## Structural Index

- Entry point + DI wiring: `src/CodenameAurora.App/Program.cs`
- Core interfaces: `src/CodenameAurora.Core/Interfaces/`
- Core notifications: `src/CodenameAurora.Core/Notifications/`
- Domain types: `src/CodenameAurora.Core/` (`TranslationResult`, `SettingsData`)
- Unit tests: `tests/CodenameAurora.Tests.Unit/`
- Architecture tests: `tests/CodenameAurora.Tests.Architecture/`
- Gherkin scenarios: `tests/features/`
- Full contract reference: `specs/architecture/architecture.md`

---

## Routing (touching X → read Y first)

| You are about to...                              | Load this skill first                  |
|--------------------------------------------------|----------------------------------------|
| Scope a new feature or change (uncertain blast radius) | `investigate`                    |
| Write or modify any `.cs` file                  | `architecture` then `code-quality`     |
| Read an Issue, write tests or `.feature` files  | `feature-spec`                         |
| Run builds, tests, push, or open a PR            | `ci-workflow`                          |
| Push or open a PR                                | `pre-release-validation`               |

---

## Non-Negotiables (apply in every session)

### No destructive commands without explicit permission

`dotnet ef database drop`, deleting migration files, `git reset --hard`,
`git push --force` — always ask before running.

### Only the orchestrator runs builds and pushes

Subagents read and write code. `dotnet build`, `dotnet test`, `git commit`,
`git push` belong to the orchestrator alone. A subagent that concludes it
needs a build stops and reports that; the orchestrator runs the one gate.

### A green build is not verification

`dotnet build` does not compile test projects. State exactly which command ran
and what it covered. Say what was checked and what was not.

### Research precedent before asking

Most decisions already have an answer in the repo. Find it first. Ask only for
genuine forks — destructive operations, scope changes, or decisions where two
defensible readings lead somewhere materially different. Name the precedents
you checked and why they did not settle it.

### Essential and clear

Every file, comment, and line of code must earn its place. No redundancy, no
padding, no acronyms unless universally known. One clear sentence beats three
vague ones. If it doesn't need to be read, don't write it. This applies to
documentation, specs, comments, and PR descriptions equally.

Agent communication follows the same rule: no filler phrases, no hedging
("I think", "maybe", "perhaps"), no human-courtesy openers. State facts.
If something is uncertain, say exactly what is unknown and why. If something
is wrong, state what is wrong and the fix. Declarative and unambiguous always.
