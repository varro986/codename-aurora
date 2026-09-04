# MVP

**Scope:** working end-to-end app — hotkey → OCR → translation → overlay.

---

## App Foundation

As an **operator**,
I want Aurora to start silently as a system tray icon, load my settings, and register the global hotkey,
so that the tool is ready without occupying the taskbar.

### AC

* [ ] `%APPDATA%\CodenameAurora\settings.json` created with defaults on first launch; malformed values fall back without crashing.
* [ ] No main window on startup — system tray icon only.
* [ ] Tray context menu: at least "About" and "Exit".
* [ ] "Exit": hotkey unregistered, icon removed, process terminates cleanly.
* [ ] `HotkeyTrigger` registered system-wide via Win32 `RegisterHotKey`; conflict → warning, not crash.

### Test

* F1 (Unit) `AppSettings` with valid `settings.json` → correct values.
* F2 (Unit) `settings.json` missing → defaults, no throw.
* F3 (Unit) "Exit" publishes `ShutdownRequested`.
* F4 (Unit) `HotkeyManager.Register()` → `RegisterHotKey`; false → warning, no throw.
* F5 (Arch) `CodenameAurora.Admin` does not reference OCR, UI, Translation.
* F6 (Arch) `CodenameAurora.UI` does not reference OCR, Translation, Admin.

---

## OCR Pipeline

As an **operator**,
I want pressing the hotkey to capture the screen, OCR the text, translate it, and show it in the overlay,
so that I can read the translated text without switching windows.

### AC

* [ ] `ICaptureService.CaptureScreen()` → `byte[]`; if the active window is Aurora → `Array.Empty<byte>()`.
* [ ] `IOcrService.RecognizeAsync(byte[], string)` → text via WinRT `Windows.Media.Ocr`; missing language pack → `OcrLanguageNotAvailableException`; empty byte array → empty string.
* [ ] `ITranslationEngine.TranslateAsync(string)` → cascade: `private.json` → `generic.json` → verbatim; missing/malformed file → warning + skip, no crash.
* [ ] Overlay: transparent, borderless, always-on-top, click-through (`WS_EX_TRANSPARENT | WS_EX_LAYERED`).
* [ ] Translated text visible within 500 ms of the hotkey; auto-dismissed after `IAppSettings.OverlayDismissTimeout`.
* [ ] Pipeline: `HotkeyTriggered` → `PipelineExecutor` → capture → OCR → translate → `TranslationReady` → overlay.

### Test

* P1 (Unit) Bitmap with text + `en-US` installed → non-empty string.
* P2 (Unit) Empty byte array → empty string, no throw.
* P3 (Unit) Missing language pack → `OcrLanguageNotAvailableException`.
* P4 (Unit) Token in `private.json` → private mapping (`SourceLevel = Private`).
* P5 (Unit) Token only in generic → generic mapping (`SourceLevel = Generic`).
* P6 (Unit) Token absent from both → verbatim (`SourceLevel = Verbatim`).
* P7 (Unit) `private.json` missing → warning, fallback to generic, no throw.
* P8 (Unit) `TranslationReady` published → overlay updated within 500 ms.
* P9 (Integration) Overlay has `WS_EX_TRANSPARENT | WS_EX_LAYERED` after creation.
* P10 (Arch) `CodenameAurora.OCR` does not reference Translation, UI, Admin.
* P11 (Arch) `CodenameAurora.Translation` does not reference OCR, UI, Admin.
* P12 (Arch) `CodenameAurora.UI` does not reference OCR, Translation, Admin.
