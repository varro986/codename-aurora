# US-002 — Aurora.OCR

## Scope
Aurora.OCR riconosce testo da bitmap via `Windows.Media.Ocr.OcrEngine` (WinRT) e ottimizza le chiamate con un cache basata su fingerprint visivo.

## Acceptance Criteria

**OCR Engine**
- `IOcrService.RecognizeAsync(Bitmap, string language)` returns the recognized text as `string`.
- Source language is read from `IAppSettings.SourceLanguage`; if the language pack is not installed, throws `OcrLanguageNotAvailableException` (not a generic exception).
- `null` bitmap input → returns empty string, no exception.
- Recognition completes within 300 ms on a 1080p screen region under normal load.
- No additional NuGet OCR packages — WinRT only (`<UseWindowsDesktopSdk>` / `<UseWinUI>`).
- Screen capture (Bitmap creation from the screen region) is the responsibility of `Aurora.App`; `IOcrService` only processes a pre-captured bitmap.

**Smart Caching**
- A visual fingerprint (perceptual hash or pixel checksum) is computed before each WinRT call.
- On fingerprint match with the cached entry, the cached text is returned immediately without calling `OcrEngine`.
- Cache is invalidated on: fingerprint change, active window HWND change, or screen region change.
- Cache holds at most one entry per screen region (no unbounded memory growth); in-process only, reset on restart.
- The fingerprint algorithm is encapsulated in a dedicated `VisualFingerprintProvider` class.

## Test Cases
- (Unit) Valid bitmap with ASCII text + `en-US` language pack → non-empty string with expected words.
- (Unit) `null` bitmap → empty string, no exception.
- (Unit) Language pack not installed → `OcrLanguageNotAvailableException`.
- (Unit) Same bitmap submitted twice → `OcrEngine` called once; second call returns cached result.
- (Unit) Two different bitmaps → `OcrEngine` called for both.
- (Unit) Focus change event → cache invalidated; next call invokes `OcrEngine` regardless of bitmap.
- (Unit) Cache never holds more than one entry per region simultaneously.
- (Architecture) `IOcrService` defined in `Aurora.Core.Interfaces` and not duplicated elsewhere (NetArchTest).
