# ADR-002 — Global Hotkey Mechanism

**Date:** 2026-07-28
**Status:** Approved
**Deciders:** Founder / Architect

## Context

Aurora must register two global hotkeys that work even when the application does not have focus (the operator is working inside AS/400, SAP, or any other foreground application):

1. **Single trigger** — capture screen region once and translate.
2. **Rullo mode** — continuous capture loop, toggled on/off.

The hotkeys must be user-configurable and must gracefully handle conflicts with other registered hotkeys.

## Decision

Use Win32 `RegisterHotKey` / `UnregisterHotKey` via P/Invoke from `Aurora.UI`.

## Rationale

- `RegisterHotKey` is a system-level API that fires a `WM_HOTKEY` message to the registering window regardless of which application is in the foreground.
- No additional NuGet dependency — pure P/Invoke with `[DllImport("user32.dll")]`.
- Alternatives considered:
  - **WPF `KeyDown` event**: fires only when the WPF window has keyboard focus — ruled out entirely.
  - **`SetWindowsHookEx(WH_KEYBOARD_LL)`**: requires a message-loop thread and raises additional UAC considerations in locked-down enterprise environments; overkill for two fixed hotkeys.

## Consequences

- Hotkeys are registered in `App.xaml.cs` on startup and unregistered on exit. Hotkey binding values are read from `IAppSettings` (ADR-006); DI wiring of any hotkey service is orchestrated by `Aurora.App` (ADR-007), while the actual `RegisterHotKey` P/Invoke calls remain within `Aurora.UI`.
- If `RegisterHotKey` returns `false` (conflict with another application), Aurora surfaces a warning notification via the tray icon and falls back to the default combination.
- Hotkey combinations are user-configurable; the bindings are stored and read through `IAppSettings` (see ADR-006).
- `Aurora.UI` is the sole owner of hotkey registration. P/Invoke declarations stay in `Aurora.UI` — never in `Aurora.Core`.

## Compliance

- No other module may call `RegisterHotKey` or handle `WM_HOTKEY` directly.
- Hotkey handling logic must not contain translation or OCR calls inline; it must dispatch through `Aurora.Core` contracts.
