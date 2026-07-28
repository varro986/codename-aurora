# ADR-007 — Composition Root Isolation

**Date:** 2026-07-28
**Status:** Approved
**Deciders:** Founder / Architect

## Context

`Aurora.UI` was initially designated as both the WPF application module and the DI composition root (`WinExe` entry point). A composition root must reference all concrete implementations (`Aurora.OCR`, `Aurora.Translation`, `Aurora.Admin`) to wire the DI container. This conflicts directly with the architectural isolation rule in ADR-001: the NetArchTest for `Aurora.UI` would fail as soon as any DI registration referencing another operational module is added.

## Decision

Introduce `Aurora.App` as the sole `WinExe` entry point and DI composition root. `Aurora.UI` is demoted to a WPF class library.

## Rationale

- A dedicated composition root is the standard pattern in Clean Architecture and modular monolith implementations (Kamil Grzybek's reference architecture, Microsoft's eShop sample).
- `Aurora.App` is explicitly exempt from isolation rules: its only responsibility is to assemble the other modules — it contains no business logic, no domain types, and no UI logic.
- `Aurora.UI` retains its isolation guarantee unchanged; its NetArchTest continues to verify that WPF code never imports OCR, Translation, or Admin namespaces.
- Alternatives considered:
  - **Namespace exemption in NetArchTest**: Valid but punches a hole in an otherwise clean, unconditional rule. A dedicated project is self-documenting and requires no future annotation maintenance.

## Consequences

- `Aurora.App.csproj` is `WinExe`, `UseWPF=true`, references all other 5 modules (Core + 4 operational) — the only project allowed to do so.
- `Aurora.UI.csproj` is a WPF class library (`UseWPF=true`, no `OutputType`). `App.xaml` is compiled as `Page` (not `ApplicationDefinition`) to prevent the WPF SDK from auto-generating a competing `Main()`.
- `Aurora.App/Program.cs` contains `[STAThread] Main()`. Per ADR-005, `VelopackApp.Build().Run()` must be its very first call.
- All DI registrations (concrete service implementations, `IAppSettings`) live exclusively in `Aurora.App`.
- The architecture test project references `Aurora.App` for future governance tests on the composition root.
- This ADR raises the project count from 5 (as stated in ADR-001) to 6; ADR-001 is otherwise unaffected.

## Compliance

- `Aurora.App` must contain no business logic, no WPF windows, no domain types — only DI wiring and process bootstrap.
- No module other than `Aurora.App` may compile-time reference more than one operational module.
- `Program.Main()` must call `VelopackApp.Build().Run()` before any other statement (ADR-005).
