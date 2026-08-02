# ADR-001 — Inter-Module Event Bus: MediatR

**Date:** 2026-08-02
**Status:** Accepted
**Deciders:** Founder / Architect

## Context

The modular monolith forbids direct references between sibling modules. Yet modules must exchange runtime events: UI needs to know when a translation is ready, Admin announces updates, Translation signals dictionary reloads, etc.

## Decision

**MediatR** as the inter-module event bus. Events are `INotification` implementations defined in `Aurora.Core`. Publishers call `IPublisher.Publish()`; handlers implement `INotificationHandler<T>` and are registered exclusively in `Aurora.App`.

## Rationale

- Well-established, dependency-light .NET library with no framework lock-in.
- C# `event`/delegate: publisher must hold a reference to the subscriber — breaks isolation.
- Custom `IEventBus`: ~50 lines to maintain with no benefit over MediatR here.
- CommunityToolkit Messenger: good for intra-layer MVVM, less idiomatic for cross-module orchestration.

## Consequences

- `Aurora.Core` references `MediatR.Contracts` NuGet only. Notification classes implement `INotification` directly in `Aurora.Core`.
- `Aurora.UI` references the full `MediatR` package: it is both a publisher (`HotkeyTriggered`, `RulloToggleRequested`, `ShutdownRequested`, `OpenSettingsRequested`, `WordDetailRequested`) and a handler host (handlers for `TranslationReady`, `UpdateAvailable`, `WordDetailReady`, `DictionaryReloaded`).
- `Aurora.App` references the full `MediatR` package and registers handlers from both its own assembly and `Aurora.UI` via `services.AddMediatR(...)`.
- `Aurora.OCR`, `Aurora.Translation`, `Aurora.Admin` do **not** reference MediatR directly — they fire C# events that `Aurora.App.WireEvents()` translates into MediatR publications.

**Notification catalogue** (`Aurora.Core.Notifications`):

| Class | Published by | Consumed by |
|---|---|---|
| `HotkeyTriggered` | `Aurora.UI` (on `HotkeyTrigger` press) | `Aurora.App` (starts OCR pipeline) |
| `RulloToggleRequested` | `Aurora.UI` (on `HotkeyRullo` press) | `Aurora.App` → `IContinuousModeController.Toggle()` |
| `TranslationReady` (carries `TranslationResult`) | `Aurora.App` | `Aurora.UI` |
| `UpdateAvailable` | `Aurora.App` (via `AdminService.UpdateFound` C# event) | `Aurora.UI` |
| `DictionaryReloaded` | `Aurora.App` (via `TranslationEngine.DictionaryHotReloaded` C# event) | `Aurora.UI` |
| `ShutdownRequested` | `Aurora.UI` | `Aurora.App` |
| `OpenSettingsRequested` | `Aurora.UI` | `Aurora.Admin` |
| `CaptureTick` | `Aurora.Core` (`IContinuousModeController`) C# event — not MediatR | `Aurora.App.WireEvents()` |
| `WordDetailRequested` | `Aurora.UI` | `Aurora.App` |
| `WordDetailReady` | `Aurora.App` | `Aurora.UI` |

## Compliance

- All cross-module communication must use MediatR notifications — direct calls between sibling modules are an architectural violation.
- `Aurora.Core` must not reference the full `MediatR` package.
- `Aurora.OCR`, `Aurora.Translation`, `Aurora.Admin` must not reference MediatR at all — use C# events bridged by `Aurora.App.WireEvents()`.
- Handler registration covers `Aurora.App` and `Aurora.UI` assemblies.

## Version Pinning

| Package | Version | Used by |
|---|---|---|
| `MediatR.Contracts` | 2.0.1 | `Aurora.Core` (INotification interface only) |
| `MediatR` | 12.4.1 | `Aurora.UI`, `Aurora.App` (full mediator + publisher) |

`MediatR 12.x` and `MediatR.Contracts 2.x` are compatible — `INotification` from Contracts 2.x is the same type identity as `INotification` in MediatR 12.x. **When upgrading, both packages must be bumped together** to avoid type-identity mismatches at runtime that cause handlers to silently not fire.
