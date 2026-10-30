# Aurora

A Windows desktop overlay for real-time OCR and translation of whatever is on screen.

## Goal

Remove the language barrier from whatever is on screen: read text in a language you do not know, in any application,
without copying it anywhere.

## Description

Press a global hotkey from any application: Aurora captures a region of the screen, extracts the text with the native
Windows OCR engine (no API key), translates it with a local dictionary cascade (private, then generic, then verbatim)
and shows the result in a transparent, always-on-top, click-through overlay that never interrupts the work. It lives
in the system tray, with no main window.

The project restarted from this README: specs and code are being rebuilt from scratch.

## Technologies

- C# on .NET 8, Windows 11 (build 22621+).
- **Windows only, on purpose**: the OCR is the native Windows engine, which is free, offline and needs no key; the
  exception to "portable across Windows and Linux" is written here as the common rules require.

## Dependencies

| From | What it uses | Type |
|---|---|---|
| Windows 11 | native OCR engine, global hotkeys, overlay windows | platform |

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
|-- src/              source code
|-- tests/            tests, from the acceptance criteria
|-- tools/            deterministic tools for agents and people
|-- .githooks/        pre-commit: gitleaks on staged files
|-- .github/          CI (gitleaks), issue and PR templates, Dependabot
|-- .gitleaks.toml    secret scan rules
|-- .gitignore
\-- .editorconfig     shared editor settings
```

## Rules

- **Local only**: OCR and translation run on the machine; no text leaves it.
- **Never in the way**: the overlay is click-through and never takes the focus.

## Miscellaneous

Nothing yet.

## To do

- Write `specs/vision.md` and the first feature (capture, OCR, overlay), then the stack ADR (.NET version, UI framework).
