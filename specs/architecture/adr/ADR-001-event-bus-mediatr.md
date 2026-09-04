# ADR-001 — Inter-Module Event Bus: MediatR

**Date:** 2026-08-02  
**Status:** Accepted

## Decision

MediatR as the inter-module event bus. Notifications are `INotification` implementations defined in `CodenameAurora.Core`. Publishers call `IPublisher.Publish()`; handlers implement `INotificationHandler<T>` and are registered exclusively in `CodenameAurora.App`.

## Why not the alternatives

- **C# events/delegates:** publisher must hold a reference to the subscriber — breaks module isolation.
- **Custom IEventBus:** ~50 lines to maintain with no benefit over MediatR here.
- **CommunityToolkit Messenger:** good for intra-layer MVVM, not idiomatic for cross-module orchestration.

## Constraints

- `CodenameAurora.Core` references `MediatR.Contracts` only (not the full MediatR package).
- `CodenameAurora.OCR`, `CodenameAurora.Translation`, `CodenameAurora.Admin` do NOT reference MediatR — they fire C# events; `CodenameAurora.App.WireEvents()` bridges them to MediatR publications.
- `CodenameAurora.UI` and `CodenameAurora.App` reference the full `MediatR` package.
- Handler registration covers both `CodenameAurora.App` and `CodenameAurora.UI` assemblies.

## Notification Catalogue

| Notification | Published by | Consumed by |
|---|---|---|
| `HotkeyTriggered` | CodenameAurora.UI | CodenameAurora.App (starts pipeline) |
| `RulloToggleRequested` | CodenameAurora.UI | CodenameAurora.App → IContinuousModeController |
| `TranslationReady(TranslationResult)` | CodenameAurora.App | CodenameAurora.UI |
| `ShutdownRequested` | CodenameAurora.UI | CodenameAurora.App |
| `OpenSettingsRequested` | CodenameAurora.UI | CodenameAurora.Admin |
| `UpdateAvailable` | CodenameAurora.App (via AdminService C# event) | CodenameAurora.UI |
| `DictionaryReloaded` | CodenameAurora.App (via TranslationEngine C# event) | CodenameAurora.UI |
| `WordDetailRequested(string Word)` | CodenameAurora.UI | CodenameAurora.App |
| `WordDetailReady(string Word, TranslationResult)` | CodenameAurora.App | CodenameAurora.UI |
| `CaptureTick` | IContinuousModeController (C# event, NOT MediatR) | CodenameAurora.App.WireEvents() |

## Package Versions

| Package | Version | Used by |
|---|---|---|
| `MediatR.Contracts` | 2.0.1 | CodenameAurora.Core |
| `MediatR` | 12.4.1 | CodenameAurora.UI, CodenameAurora.App |

When upgrading: bump both packages together — type-identity mismatch causes handlers to silently not fire.
