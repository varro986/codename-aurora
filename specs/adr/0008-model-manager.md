# ADR-008 — ONNX Model Lifecycle Contract

**Date:** 2026-07-28
**Status:** Approved
**Deciders:** Founder / Architect

## Context

`Aurora.Translation` uses ONNX Runtime with Helsinki-NLP MarianMT models (~300 MB per language pair). Two naive approaches are unworkable:
- **Load at startup** — blocks the process for several seconds before the user sees anything.
- **Load on every call** — prohibitive latency per translation request.

The model must be loaded lazily on first use and held in memory for the session. This lifecycle responsibility must not bleed into `Aurora.Core` as a hidden implementation detail, nor into `ITranslationEngine` as implicit statefulness.

## Decision

Introduce `IModelManager` in `Aurora.Core` with a single `EnsureLoadedAsync()` method and `IAsyncDisposable` for cleanup. `Aurora.Translation` is the sole implementor; `Aurora.App` wires it via DI as a singleton.

## Rationale

> Refines and formalises the lazy-load behaviour described informally in ADR-004.

- `ITranslationEngine` remains stateless from the caller's perspective: DI injects a pre-warmed `IModelManager` singleton, load happens once, subsequent calls return immediately.
- `IAsyncDisposable` is preferred over `IDisposable` because ONNX Runtime unloads GPU/CPU session objects asynchronously on .NET 10.
- The contract lives in `Aurora.Core` so that future modules (e.g. a diagnostics panel in `Aurora.Admin`) can inspect model status without taking a direct dependency on `Aurora.Translation`.
- Alternatives considered:
  - **Internal lifecycle in `Aurora.Translation`, no Core contract**: simpler today, but `ITranslationEngine` becomes implicitly stateful and untestable in isolation without ONNX present.
  - **`InitializeAsync()` on `ITranslationEngine`**: merges lifecycle and translation concerns, violates SRP.

## Consequences

- `Aurora.Core/Interfaces/IModelManager.cs` is added (contract only, no implementation).
- `Aurora.Translation` implements `IModelManager`; `EnsureLoadedAsync()` is idempotent — safe to call multiple times.
- `Aurora.App` registers `IModelManager` as a singleton; `ITranslationEngine` implementation receives it via constructor injection.
- A user story must be opened to drive the `Aurora.Translation` implementation (lazy load, path from `IAppSettings.ModelCachePath`, error handling).
- `IsLoaded` is exposed for diagnostics and startup health checks.

## Compliance

- `IModelManager` must remain in `Aurora.Core.Interfaces` — no implementation details leak into the contract.
- Only `Aurora.Translation` may implement `IModelManager`; no other module may hold a concrete reference to the implementation type.
- `EnsureLoadedAsync()` must be idempotent: calling it when the model is already loaded must be a no-op.
- `Aurora.App` must register `IModelManager` as a singleton to guarantee a single model instance per process.
