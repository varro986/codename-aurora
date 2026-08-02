# US-001 — Aurora.Admin

## Scope
Aurora.Admin persiste la configurazione in `%APPDATA%\Aurora\settings.json`, controlla gli aggiornamenti via Velopack allo startup, e ospita il pannello di configurazione accessibile dal tray.

## Acceptance Criteria

**Settings**
- On first launch, `settings.json` is created with all default values if absent.
- On malformed field or missing key, the application falls back to the field default and logs a warning — no crash.
- `settings.json` is never written inside the repository directory.
- Settings writes use `ISettingsWriter` (defined in `Aurora.Core`); `IAppSettings` remains read-only.

**Auto-Update**
- On startup, Aurora.Admin checks GitHub Releases for the channel in `IAppSettings.UpdateChannel` (non-blocking).
- If a newer version is available, publishes `UpdateAvailable`; Aurora.UI displays a tray notification (no modal dialog).
- The operator can trigger download + apply from the tray menu; the application restarts via Velopack.
- On update-check failure (network, rate limit), the error is logged and the app continues normally.

**Admin Panel**
- The panel opens via "Settings" in the tray context menu (`OpenSettingsRequested` notification).
- Editable fields: `OverlayBackgroundColor`, `OverlayForegroundColor`, `PrivateDictionaryPath`, `GenericDictionaryPath`, `UpdateChannel`.
- UI action to trigger ONNX model download to `IAppSettings.ModelCachePath`.
- All changes are persisted to `settings.json`; take effect on next restart.

## Test Cases
- (Unit) Valid `settings.json` → `AppSettings` returns correct values for all 13 properties.
- (Unit) Missing `settings.json` → default values returned, no exception.
- (Unit) Malformed JSON field → that field falls back to default, warning logged.
- (Unit) Mocked GitHub API returns newer version → `UpdateService.CheckAsync()` returns `UpdateAvailable = true`.
- (Unit) Network error during update check → returns `UpdateAvailable = false`, no exception.
- (Unit) Overlay color changed and saved → `settings.json` updated with new value.
- (Unit) Model download triggered → files written under `IAppSettings.ModelCachePath` (mocked).
- (Architecture) `settings.json` writes use only paths derived from `%APPDATA%\Aurora\` (path assertion).
- (Architecture) Velopack types used only within `Aurora.Admin` (NetArchTest).
