# CLAUDE.md

Codename Aurora — Windows desktop overlay for real-time OCR and translation.
WPF + WinRT (.NET 8), Modular Monolith, Windows 11+.

## Tech Stack

- C# con `#nullable enable`, .NET 8, `net8.0-windows10.0.22621.0`
- WPF (`CodenameAurora.UI`) + WinRT OCR (`Windows.Media.Ocr`)
- xUnit + Reqnroll (Gherkin), NetArchTest
- CI: GitHub Actions on `windows-latest` (WPF/WinRT requires Windows)
- Formatter: CSharpier

## Specs

```
specs/
  architecture/
    architecture.md   ← source of truth: modules, rules, C# contracts
    adr/              ← architecture decision history
  features/
    features.md       ← MVP
    us/               ← future incremental user stories
  templates/          ← ADR and US templates
```

## Behaviour

@.claude/CLAUDE.md — Claude behavior and routing table.
@AGENTS.md — architecture law, DoD, fragile zones, security.

## Workflow

Issue → label `status: active` → branch `feature/issue-N-slug` + stub test → PR → merge
