[![CI](https://github.com/varro986/codename-aurora/actions/workflows/ci.yml/badge.svg)](https://github.com/varro986/codename-aurora/actions/workflows/ci.yml)
[![Latest Release](https://img.shields.io/github/v/release/varro986/codename-aurora)](https://github.com/varro986/codename-aurora/releases/latest)
[![.NET](https://img.shields.io/badge/.NET-8%2B-512BD4)](https://dotnet.microsoft.com)
[![Platform](https://img.shields.io/badge/Windows%2011%2B-0078D4?logo=windows)](https://www.microsoft.com/windows)

# Codename Aurora

**Windows desktop overlay for real-time OCR and translation.**

Capture any region of your screen, extract text with the native Windows OCR engine, and get an instant translation — all triggered by a global hotkey, without leaving your current window.

## How it works

1. Press the configured hotkey from any application.
2. Aurora captures the screen region and runs OCR via the native Windows engine (no API key required).
3. Text is translated using a local dictionary cascade (private → generic → verbatim).
4. The result appears in a transparent, always-on-top overlay. Click-through — it never interrupts your workflow.

## Requirements

- Windows 11 (Build 22621+)
- .NET 8 runtime

## Quick start

1. Download the latest release from [GitHub Releases](https://github.com/varro986/codename-aurora/releases/latest).
2. Run `CodenameAurora.App.exe`.
3. Aurora starts in the system tray — no main window.
4. Press your configured hotkey to capture and translate.

## Project structure

```
specs/               ← source of truth: architecture, features, ADRs
  architecture/      ← C# contracts, modules, isolation rules
  features/          ← MVP and incremental user stories
  templates/         ← ADR and US templates
.claude/             ← Claude Code configuration (skills, hooks, settings)
.github/             ← CI, issue templates, label setup, workflows
scripts/             ← code quality rules (smell-guard, coupling-guard)
src/                 ← source code (created during development phase)
tests/               ← unit, architecture, Gherkin tests (created during development)
```

→ Workflow, quality gates, and process rules: [AGENTS.md](AGENTS.md) and [CONTRIBUTING.md](CONTRIBUTING.md).

## Contributing

Aurora is developed spec-first. See [AGENTS.md](AGENTS.md) for architecture laws, AI assistance policy, and PR process.

- [Report a bug](https://github.com/varro986/codename-aurora/issues/new?template=bug_report.yml)
- [Request a feature](https://github.com/varro986/codename-aurora/issues/new?template=feature.yml)

## License

MIT
