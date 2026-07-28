# ADR-006 — Admin-to-Core Settings Contract

**Date:** 2026-07-28
**Status:** Approved
**Deciders:** Founder / Architect

## Context

`Aurora.Admin` is the single writer of application-wide settings (hotkey bindings, language pairs, dictionary paths, update channel, model cache path). However, `Aurora.UI`, `Aurora.OCR`, and `Aurora.Translation` also need to read these settings at runtime. Passing settings through `Aurora.Admin` directly would require those modules to reference `Aurora.Admin`, creating forbidden horizontal dependencies (ADR-001).

## Decision

Define `IAppSettings` in `Aurora.Core`. `Aurora.Admin` provides the concrete `AppSettings` implementation; all other modules consume settings exclusively through the `IAppSettings` contract.

## Rationale

- Placing `IAppSettings` in `Aurora.Core` keeps dependency flow strictly unidirectional: every module depends only on `Core`.
- This is consistent with the Design by Contract principle already applied to `IOcrService` and `ITranslationEngine`.
- Alternatives considered:
  - **Static singleton in Core**: Breaks testability and couples all modules to a global state object.
  - **Settings passed as constructor parameters**: Works for simple cases but becomes unwieldy as the settings surface grows.
  - **Aurora.Admin exposed as a shared module**: Explicitly forbidden horizontal dependency — rejected.

## Consequences

- `IAppSettings` is defined in `Aurora.Core.Interfaces` with the following read-only properties: `SourceLanguage`, `TargetLanguage`, `HotkeyTrigger`, `HotkeyRullo`, `PrivateDictionaryPath`, `GenericDictionaryPath`, `ModelCachePath`, `UpdateChannel`.
- `Aurora.Admin` provides the concrete `AppSettings` class that reads from `%APPDATA%\Aurora\settings.json` via `System.Text.Json`. Write operations are performed by `Aurora.Admin` only — `IAppSettings` exposes no mutating methods.
- All dependency-injection registration happens in `Aurora.App` (composition root, see ADR-007); `AppSettings` is registered as a singleton implementing `IAppSettings`.
- Changes to `settings.json` at runtime are applied on next application restart (no hot-reload for settings, unlike dictionaries in ADR-003).

## Compliance

- `IAppSettings` must be defined in `Aurora.Core` before any implementation story that reads configuration.
- No module may read `settings.json` directly — only `Aurora.Admin.AppSettings` accesses the file.
- `IAppSettings` must remain a read-only interface; any property setter is an architectural violation.
