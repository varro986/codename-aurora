# US-004 — Aurora.UI

## Scope
Aurora.UI gestisce l'overlay trasparente always-on-top, il system tray, i global hotkey Win32, e il mini-overlay hover per l'ispezione del SourceLevel.

## Acceptance Criteria

**Overlay**
- Transparent, borderless, always-on-top window with `WS_EX_TRANSPARENT` + `WS_EX_LAYERED` extended styles.
- Click-through: mouse events pass to the window beneath; overlay never steals keyboard focus.
- On `TranslationReady` notification, displays translated text within 500 ms.
- Auto-dismisses after `IAppSettings.OverlayDismissTimeout` or on a second hotkey press.

**System Tray**
- Application starts with no main window; only the tray icon is visible.
- Tray context menu: "Settings", "About", "Exit".
- "Exit" publishes `ShutdownRequested`; Aurora.App handles the graceful shutdown sequence (services stop → hotkeys unregister → icon remove).
- Tray icon has two variants: active/running and paused.

**Global Hotkey**
- `HotkeyTrigger` and `HotkeyRullo` registered system-wide via `RegisterHotKey` (Win32 P/Invoke).
- Values read from `IAppSettings`; all P/Invoke declarations encapsulated in `HotkeyManager`.
- On `RegisterHotKey` conflict, a warning is logged and startup continues (no crash).
- On graceful shutdown, both hotkeys unregistered via `UnregisterHotKey`.
- `HotkeyTrigger` press → publishes `HotkeyTriggered` notification.
- `HotkeyRullo` press → publishes `RulloToggleRequested` notification.

**Hover Glossary**
- When the mouse dwells on a recognized word in the overlay for ≥ `IAppSettings.HoverDwellThreshold` ms, publishes `WordDetailRequested`.
- On `WordDetailReady`, a mini-overlay appears near the cursor showing `TranslationResult.SourceLevel`.
- Mini-overlay dismisses when the mouse moves away.
- Mini-overlay reuses the same click-through and always-on-top properties as the main overlay.

## Test Cases
- (Unit) `TranslationReady` published → overlay view-model updates `Text` within 500 ms.
- (Unit) Auto-dismiss timeout elapses → overlay visibility set to `Collapsed`.
- (Integration) Overlay window has `WS_EX_TRANSPARENT` + `WS_EX_LAYERED` after creation.
- (Unit) Startup → no main window shown, tray icon added.
- (Unit) "Exit" clicked → `ShutdownRequested` notification published.
- (Unit) App state → paused → tray icon switches to paused variant.
- (Integration) Graceful shutdown → tray icon no longer present.
- (Unit) Valid hotkey values in `IAppSettings` → `HotkeyManager.Register()` calls `RegisterHotKey` for both without throwing.
- (Unit) `RegisterHotKey` returns `false` → warning logged, no exception.
- (Unit) `HotkeyManager.Unregister()` calls `UnregisterHotKey` for all registered hotkeys.
- (Integration) `HotkeyTrigger` pressed → `HotkeyTriggered` notification published.
- (Unit) Cursor dwells ≥ threshold → `WordDetailRequested` published.
- (Unit) Cursor moves before threshold → no notification published.
