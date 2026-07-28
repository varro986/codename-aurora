# [US-001] Application Settings

* **Status:** Draft
* **Author:** Business Analyst
* **Created:** 2026-07-28

---

## 1. Description

As an **operator**,  
I want the application to load its configuration (hotkeys, language pair, file paths) from a persistent settings file,  
so that my preferences are preserved across sessions without needing to reconfigure the tool each time.

## 2. Acceptance Criteria (DoD)

* [ ] **AC-01:** On first launch, a `settings.json` is created under `%APPDATA%\Aurora\` with default values if it does not already exist.
* [ ] **AC-02:** `IAppSettings` values are read from `settings.json` at startup; changes to the file take effect on next launch.
* [ ] **AC-03:** `SourceLanguage`, `TargetLanguage`, `HotkeyTrigger`, `HotkeyRullo`, `PrivateDictionaryPath`, `GenericDictionaryPath`, `ModelCachePath`, and `UpdateChannel` are all populated.
* [ ] **AC-04:** If `settings.json` is malformed or a required field is missing, the application logs the error and falls back to the default value for that field — it does not crash.
* [ ] **AC-05:** `settings.json` is never created inside the repository directory (must be `%APPDATA%\Aurora\`, not relative to the executable).

## 3. Architectural Guardrails

* **Modules involved:** `Aurora.Core` (interface), `Aurora.Admin` (implementation), `Aurora.App` (DI registration)
* **Related ADRs:** ADR-004 (local model — defines `%APPDATA%\Aurora\` as the config root), ADR-006 (Admin/Core contract), ADR-007 (composition root)
* **Isolation constraints:**
  - `IAppSettings` is defined in `Aurora.Core.Interfaces` — must not move.
  - The concrete implementation (`AppSettings`) lives exclusively in `Aurora.Admin` — no other module may reference it.
  - `Aurora.App` registers `AppSettings` as the `IAppSettings` singleton in the DI container.
  - `settings.json` path is hardcoded to `%APPDATA%\Aurora\settings.json` — never configurable via constructor argument (ADR-004).

## 4. Expected Test Cases

* [ ] **Test 1 (Unit):** Given a valid `settings.json`, `AppSettings` returns the correct values for all fields.
* [ ] **Test 2 (Unit):** Given a missing `settings.json`, `AppSettings` returns default values for all fields and does not throw.
* [ ] **Test 3 (Unit):** Given a malformed JSON file, `AppSettings` falls back to defaults for affected fields and logs a warning.
* [ ] **Test 4 (Architecture):** `Aurora.Admin` does not reference any module other than `Aurora.Core` (NetArchTest).
* [ ] **Test 5 (Architecture):** `IAppSettings` is defined in `Aurora.Core.Interfaces` and not duplicated elsewhere (NetArchTest).
