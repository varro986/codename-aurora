# US-003 — Aurora.Translation

## Scope
Aurora.Translation traduce token via cascade a 4 livelli (private dict → generic dict → ONNX model → verbatim) e ricarica i dizionari automaticamente quando modificati su disco.

## Acceptance Criteria

**Translation Cascade**
- `ITranslationEngine.TranslateAsync(string text)` returns `TranslationResult { string Text, TranslationSourceLevel SourceLevel }`.
- `TranslationSourceLevel` enum: `Private`, `Generic`, `Model`, `Verbatim`.
- Cascade: (1) `private.json` → (2) `generic.json` → (3) `IModelManager.TranslateAsync()` → (4) verbatim (only if model unavailable).
- `private.json` loaded from `IAppSettings.PrivateDictionaryPath`; never bundled in the package.
- Missing or malformed `private.json` → warning logged, cascade advances to generic dict.
- Missing or malformed `generic.json` → warning logged, cascade advances to ONNX model.
- `TranslationEngine` receives `IModelManager` via constructor injection.

**Hot-Reload**
- `FileSystemWatcher` monitors the directories of both `PrivateDictionaryPath` and `GenericDictionaryPath`.
- On file change, the in-memory dictionary reloads transparently; ongoing translations use the old data until reload completes.
- Reload uses `Interlocked.Exchange` on an immutable reference (lock-free reader path).
- Malformed file on reload → warning logged, previous in-memory state retained.
- On successful reload, publishes `DictionaryReloaded` notification; `Aurora.UI` displays a tray notification.
- `FileSystemWatcher` disposed cleanly on application shutdown.

## Test Cases
- (Unit) Token in `private.json` → `{ Text = <mapping>, SourceLevel = Private }`.
- (Unit) Token absent from private, present in generic → `{ SourceLevel = Generic }`.
- (Unit) Token absent from both dicts → `IModelManager.TranslateAsync()` called → `{ SourceLevel = Model }`.
- (Unit) `IModelManager` unavailable → `{ Text = <original>, SourceLevel = Verbatim }`.
- (Unit) Missing `private.json` → warning logged, falls back to generic without throwing.
- (Unit) Malformed `private.json` → warning logged, falls back to generic without throwing.
- (Unit) Valid dict file modified → in-memory dict updated within 1 s.
- (Unit) Malformed dict written to disk → warning logged, previous dict remains active.
- (Unit) Successful reload → `DictionaryReloaded` notification published.
- (Architecture) `ITranslationEngine` and `TranslationResult` defined in `Aurora.Core`, not duplicated (NetArchTest).
