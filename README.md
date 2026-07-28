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
src/Aurora.Core          ← pure interfaces (IOcrService, ITranslationEngine, IAppSettings)
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

## RACI

| Phase | Funzionale | Architect / Founder | Lead Dev | AI |
|---|---|---|---|---|
| Write user story | R | A | C | I |
| Approve DoR | I | R/A | C | C |
| Code implementation | I | I | R | R |
| Approve DoD / merge | I | A | R | C |

_R = Responsible, A = Accountable, C = Consulted, I = Informed_

## Workflow

User stories are tracked as GitHub Issues with the following label states:

| Label | Meaning | Gate |
|---|---|---|
| `status:draft` | Story being written | — |
| `status:ready` | DoR met, ready for dev | Architect approval |
| `status:active` | In development | — |
| `status:review` | PR open, DoD check | Lead Dev + Architect merge |

See `.github/ISSUE_TEMPLATE/feature.yml` for the story template and `.github/setup-labels.sh` to initialise the GitHub label set.
