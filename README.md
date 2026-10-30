# Aurora

A Windows desktop overlay for real-time OCR and translation of whatever is on screen.

## Goal

Remove the language barrier from whatever is on screen: read text in a language you do not know, in any application,
without copying it anywhere.

## Description

Attach Aurora to a window, to all the windows of a process or to a monitor: it keeps reading the text with the native
Windows OCR engine (no API key), translates it with a local dictionary cascade (the application's entry, then the
generic one, then the text as it is) and shows each line in place in a transparent, click-through overlay that never
interrupts the work. It lives in the system tray, with no main window.

## Technologies

- C# on .NET 10, Avalonia for the interface, Windows 11 (build 22621+); see `specs/decisions/0001-stack.md`.
- **Windows first, Linux later**: the first OCR is the native Windows engine, which is free, offline and needs no key;
  OCR sits behind an interface, so Linux (with Tesseract) is added later without touching the rest. Until then this is
  the exception to "portable across Windows and Linux" that the common rules require to be written.

## Dependencies

| From | What it uses | Type |
|---|---|---|
| Windows 11 | native OCR engine, screen capture, global hotkeys, overlay windows | platform |
| .NET 10 | runtime, SDK, Microsoft.Testing.Platform | runtime |
| Avalonia 12 | overlay, pick layer, tray icon | library |
| Microsoft.Extensions (Configuration, Logging) | settings, logs | library |
| xUnit v3, Pester 5 | tests of the code and of the tools | library |

Nothing depends on Aurora.

## Architecture

```
codename-aurora/
|-- README.md         what the project is
|-- AGENTS.md         rules for coding assistants: common rules, then the product's own
|-- CLAUDE.md         shell for Claude Code (@AGENTS.md)
|-- CONTRIBUTING.md   how to contribute (specs first, GitHub Flow)
|-- LICENSE           MIT
|-- SECURITY.md       how to report a vulnerability
|-- CODE_OF_CONDUCT.md
|-- specs/            brainstorming.md; requirements, architecture and the rest as they come
|   |-- features/     one folder per feature: spec.md, plan.md, tasks.md
|   \-- decisions/    ADRs
|-- Aurora.slnx       the solution
|-- global.json       .NET SDK and test runner (Microsoft.Testing.Platform)
|-- Directory.Build.props     shared build settings: target frameworks, warnings as errors
|-- Directory.Packages.props  every package version, in one place
|-- src/              Core (contracts, pure logic), Windows (adapters), Overlay, App
|-- tests/            unit, adapter and end-to-end tests; targets/ holds the sample targets
|-- tools/            deterministic tools for agents and people
|-- cicd/             steps.json and run.ps1: the checks, local and in CI
|-- .githooks/        pre-commit: gitleaks on staged files
|-- .github/          CI (gitleaks), issue and PR templates, Dependabot
|-- .gitleaks.toml    secret scan rules
|-- .gitignore
\-- .editorconfig     shared editor settings
```

## Rules

- **Local only**: OCR and translation run on the machine; no text leaves it.
- **Never in the way**: the overlay is click-through and never takes the focus.
- **Screenshots never on disk**: captured frames stay in memory for OCR only; the recorder (IDEA-3) keeps only the
  proposed text, and its model is local.
- **Aurora is a codename**: the real name is not chosen yet; it changes only through `tools/rename-product.ps1`.

## Tests

The steps are in `cicd/steps.json` (build, unit, desktop, tools), run by one runner, on the machine before every push
and, without the desktop step, in CI on pull requests:

```
pwsh -NoProfile -File cicd/run.ps1                 # every step
pwsh -NoProfile -File cicd/run.ps1 -Step build,unit
dotnet test --project tests/Aurora.Core.Tests --filter-method "*Rejects_invalid_hotkey*"   # one test
```

- `unit`: the pure logic in `Aurora.Core`, with fakes.
- `desktop`: the Windows adapters and the app against the sample targets in `tests/targets/`; it needs an interactive
  Windows session, so CI skips it.
- `tools`: the tools in `tools/`, on throwaway repositories (Pester 5).

## Agents

The work of a feature is split in the tasks table of its `plan.md`: contracts first, written by the orchestrator, then
executors in parallel, each in its own worktree on the files its task declares. Executors never change the contracts
(`src/Aurora.Core/Contracts/`), project files or `Directory.Packages.props`: a missing package or reference goes back
to the orchestrator. Tests that use the desktop (foreground, focus, input) run one at a time, by the orchestrator.
Merges, releases and the real name stay with the owner.

## Miscellaneous

Nothing yet.
