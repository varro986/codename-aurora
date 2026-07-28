# Codename Aurora

A Windows desktop tool that captures text from the screen via OCR and translates it on the fly, activated by a global hotkey and displayed as a WPF overlay.

## Features

- Global hotkey triggers screen capture and OCR recognition (WinRT engine)
- Cascading JSON translation with language fallback
- Lightweight WPF overlay + system tray icon
- Auto-update via GitHub Releases

## Architecture

Modular Monolith (.NET 10) — six projects, five of which depend exclusively on `Aurora.Core` contracts. `Aurora.App` is the composition root and the only project permitted to reference all modules (ADR-007). No direct dependencies between operational modules are allowed and are enforced at CI time by NetArchTest.

```
src/Aurora.App           ← WinExe entry point, DI composition root
src/Aurora.Core          ← pure interfaces (IOcrService, ITranslationEngine, IAppSettings, IModelManager)
src/Aurora.OCR           → WinRT OCR engine
src/Aurora.Translation   → cascading JSON translation with fallback
src/Aurora.UI            → WPF overlay + system tray
src/Aurora.Admin         → app configuration + GitHub Releases auto-update
tests/Aurora.Tests.Architecture  → NetArchTest architectural rules
```

See [`specs/archi.md`](specs/archi.md) for the full architecture document and [`specs/adr/`](specs/adr/) for decision records.

## Requirements

- Windows 10 22H2 (10.0.22621) or later
- .NET 10 SDK

## Building

```powershell
dotnet build CodenameAurora.slnx
dotnet test --filter "Category=Architecture"
```

CI runs automatically on every PR to `main` (`.github/workflows/ci.yml`) — architecture tests are a required gate.

## RACI

| Phase | Business Analyst | Architect / Founder | Lead Dev | AI |
|---|---|---|---|---|
| Write user story | R | A | C | I |
| Approve DoR | I | R/A | C | C |
| Code implementation | I | I | R | R |
| Approve DoD / merge | I | A | R | C |

_R = Responsible, A = Accountable, C = Consulted, I = Informed_

### GitHub Teams

Org-level teams, no project prefix — reusable across repos:

| Team slug | Repo permission | RACI role |
|---|---|---|
| `architects` | Admin | Architect / Founder |
| `lead-devs` | Maintain | Lead Dev |
| `analysts` | Triage | Business Analyst |
| `contributors` | Write | Contributor |

Teams are created idempotently by `.github/setup-repo.sh`.

## Workflow

User stories are tracked as GitHub Issues with the following label states:

| Label | Meaning | Gate |
|---|---|---|
| `status:draft` | Story being written | — |
| `status:ready` | DoR met, ready for dev | Architect approval |
| `status:active` | In development | — |
| `status:review` | PR open, DoD check | Lead Dev + Architect merge |

See `.github/ISSUE_TEMPLATE/feature.yml` for the story template and `.github/setup-labels.sh` to initialise the GitHub label set.

## Code Ownership

`.github/CODEOWNERS` maps repository paths to the GitHub teams responsible for reviewing changes. Branch protection enforces at least one code owner approval before any PR can be merged.

| Path | Required reviewer | Rationale |
|---|---|---|
| `*` (catch-all) | `lead-devs` | Any file not matched by a specific rule below |
| `/specs/adr/` | `architects` | ADRs are immutable architectural decisions |
| `/specs/archi.md` | `architects` | Architecture overview |
| `/.github/` | `architects` | CI pipelines and governance scripts |
| `/src/` | `lead-devs` | Source code |
| `/tests/` | `lead-devs` | Test projects |

Rules are evaluated top-to-bottom; the **last matching rule wins**. The catch-all ensures no file is ever unowned, even as new paths are added.

To activate: replace `YOUR-ORG` in `.github/CODEOWNERS` with the actual GitHub organisation slug.

## Dependency Management

Dependabot (`.github/dependabot.yml`) runs every Monday and opens PRs automatically for:

| Ecosystem | Scope | Grouping |
|---|---|---|
| NuGet | All `.csproj` dependencies | `dotnet-test` (xUnit, coverlet, Test.Sdk), `netarchtest` |
| GitHub Actions | All workflow `uses:` references | Individual per-action |

Dependabot PRs are labelled `type:dependency` and follow the standard review workflow. Action versions in workflows are pinned to full commit SHAs (not floating tags) to prevent supply-chain attacks; Dependabot keeps those SHAs up to date automatically.

## Governance Workflow

`.github/workflows/governance.yml` exposes `setup-repo.sh` as a manual GitHub Actions workflow. To apply governance changes to the repository:

1. Go to **Actions → Governance Setup → Run workflow**
2. Fill in `org`, `repo`, and optional parameters
3. An approval request is sent to the `governance` Environment reviewers
4. After approval, the script runs and logs everything in the Actions tab

**One-time setup** (org Owner, done once):
- Create Environment `governance` in repo **Settings → Environments**
- Add required reviewers (e.g. `architects` team)
- Add secret `ORG_ADMIN_TOKEN`: org Owner PAT with scopes `admin:org`, `repo`
