# US-005 — Aurora.Core / Rullo Continuo

## Scope
`IContinuousModeController` in `Aurora.Core` gestisce il loop throttled OCR+translate, azionato da `Aurora.App` su ogni `CaptureTick`. Lo stato vive in Core; Aurora.UI espone solo il toggle.

## Acceptance Criteria

- `HotkeyRullo` press → `IContinuousModeController.Toggle()` alternates between `Active` and `Inactive`.
- While active, fires `CaptureTick` at `IAppSettings.RulloSamplingInterval` (default: 500 ms); the next tick does not start until the previous pipeline (OCR → Translation → UI) completes.
- Tray icon (or overlay) shows a visible indicator when continuous mode is active.
- `IContinuousModeController.Stop()` deactivates continuous mode and stops the timer; called automatically on graceful shutdown.
- Sampling frequency is read on each tick from `IAppSettings.RulloSamplingInterval` — no restart required to change it.

## Test Cases
- (Unit) `Toggle()` called twice → state goes `Active` → `Inactive`.
- (Unit) While active, `CaptureTick` fires at configured interval; no new tick while previous is processing.
- (Unit) `Stop()` → continuous mode deactivated, no further ticks.
- (Integration) `HotkeyRullo` press → tray icon toggles between active and normal variants.
- (Architecture) `IContinuousModeController` defined in `Aurora.Core`, not in `Aurora.UI` or `Aurora.OCR` (NetArchTest).
