# System Architecture — Codename Aurora

> **Pattern:** Modular Monolith (.NET 8, Windows 11+)

---

## Module Structure

```
CodenameAurora.App           ← WinExe, composition root, PipelineExecutor, DI wiring
CodenameAurora.Core          ← interfaces, notifications, TranslationResult, SettingsData
CodenameAurora.OCR           → Core only  (WinRT Windows.Media.Ocr)
CodenameAurora.Translation   → Core only  (cascade: private.json → generic.json → verbatim)
CodenameAurora.UI            → Core only  (WPF overlay, system tray, hotkey manager)
CodenameAurora.Admin         → Core only  (settings r/w, auto-update)
```

## Dependency Rules (enforced by NetArchTest)

| Module | Must NOT reference |
|---|---|
| `CodenameAurora.OCR` | Translation, UI, Admin |
| `CodenameAurora.Translation` | OCR, UI, Admin |
| `CodenameAurora.UI` | OCR, Translation, Admin |
| `CodenameAurora.Admin` | OCR, Translation, UI |
| `CodenameAurora.Core` | any other module |

`CodenameAurora.App` is the sole exception: composition root, references all modules, contains zero business logic.

## Key Decisions

**Event bus:** MediatR. Notifications defined in `CodenameAurora.Core.Notifications`. `CodenameAurora.OCR`, `CodenameAurora.Translation`, `CodenameAurora.Admin` do NOT reference MediatR — they fire C# events that `CodenameAurora.App.WireEvents()` bridges to MediatR publications. See `adr/ADR-001-event-bus-mediatr.md`.

**Capture:** `ICaptureService` implemented in `CodenameAurora.App` (composition root only). Returns `Array.Empty<byte>()` if the foreground window is Aurora itself.

**Pipeline:** `PipelineExecutor` (internal, `CodenameAurora.App`) — `HotkeyTriggered` → capture → OCR → translate → publish `TranslationReady`. Shared by hotkey handler and continuous-mode tick.

**Settings:** `%APPDATA%\CodenameAurora\settings.json`. Read via `IAppSettings` (Core interface, implemented in Admin). Written via `ISettingsWriter` (separate Core interface). Never inside the repo.

**Translation cascade (MVP):** private.json → generic.json → verbatim. No external APIs. No ONNX (post-MVP).

## Test Layout

```
tests/
  CodenameAurora.Tests.Architecture/   ← NetArchTest — one test per isolation rule
  CodenameAurora.Tests.Unit/           ← xUnit per module
```

---

## Core Contracts (`CodenameAurora.Core`)

### Interfaces

```csharp
// CodenameAurora.Core.Interfaces
public interface IAppSettings
{
    string SourceLanguage { get; }
    string TargetLanguage { get; }
    string HotkeyTrigger { get; }
    string HotkeyRullo { get; }
    string PrivateDictionaryPath { get; }
    string GenericDictionaryPath { get; }
    string ModelCachePath { get; }
    string UpdateChannel { get; }
    TimeSpan OverlayDismissTimeout { get; }
    string OverlayBackgroundColor { get; }
    string OverlayForegroundColor { get; }
    int HoverDwellThreshold { get; }
    int RulloSamplingInterval { get; }
}

public interface ISettingsWriter { void Save(SettingsData data); }
public interface IOcrService { Task<string> RecognizeAsync(byte[] imageBytes, string language, CancellationToken ct = default); }
public interface ITranslationEngine { Task<TranslationResult> TranslateAsync(string text, CancellationToken ct = default); }
public interface ICaptureService { byte[] CaptureScreen(); }

public interface IContinuousModeController : IDisposable
{
    bool IsActive { get; }
    void Toggle();
    void Stop();
    event EventHandler? CaptureTick;  // C# event, NOT MediatR
}

public interface IModelManager : IAsyncDisposable  // post-MVP
{
    bool IsLoaded { get; }
    Task EnsureLoadedAsync(CancellationToken ct = default);
}
```

### Domain Types

```csharp
// CodenameAurora.Core
public enum TranslationSourceLevel { Private, Generic, Model, Verbatim }
public sealed record TranslationResult(string Text, TranslationSourceLevel SourceLevel);

// CodenameAurora.Core.Settings
public sealed class SettingsData
{
    public string SourceLanguage { get; set; } = "it";
    public string TargetLanguage { get; set; } = "en";
    public string HotkeyTrigger { get; set; } = "Alt+F1";
    public string HotkeyRullo { get; set; } = "Alt+F2";
    public string PrivateDictionaryPath { get; set; } = "";
    public string GenericDictionaryPath { get; set; } = "";
    public string ModelCachePath { get; set; } = "";
    public string UpdateChannel { get; set; } = "stable";
    public int OverlayDismissTimeoutSeconds { get; set; } = 5;
    public string OverlayBackgroundColor { get; set; } = "#CC000000";
    public string OverlayForegroundColor { get; set; } = "#FFFFFFFF";
    public int HoverDwellThreshold { get; set; } = 300;
    public int RulloSamplingInterval { get; set; } = 500;
}
```

### MediatR Notifications (`CodenameAurora.Core.Notifications`)

```csharp
public sealed record HotkeyTriggered : INotification;
public sealed record RulloToggleRequested : INotification;
public sealed record ShutdownRequested : INotification;
public sealed record OpenSettingsRequested : INotification;
public sealed record WordDetailRequested(string Word) : INotification;
public sealed record TranslationReady(TranslationResult Result) : INotification;
public sealed record WordDetailReady(string Word, TranslationResult Detail) : INotification;
public sealed record UpdateAvailable(string Version, string ReleaseUrl) : INotification;
public sealed record DictionaryReloaded(string FilePath) : INotification;
```

### Ownership

| Contract | Implemented by |
|---|---|
| `IAppSettings` | `CodenameAurora.Admin.AppSettings` |
| `ISettingsWriter` | `CodenameAurora.Admin.SettingsWriter` |
| `IOcrService` | `CodenameAurora.OCR.OcrService` |
| `ITranslationEngine` | `CodenameAurora.Translation.TranslationEngine` |
| `ICaptureService` | `CodenameAurora.App.CaptureService` |
| `IContinuousModeController` | `CodenameAurora.Core.ContinuousModeController` |
| `IModelManager` | `CodenameAurora.Translation.ModelManager` (post-MVP) |
