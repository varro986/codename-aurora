# System Architecture — Codename Aurora

> **Pattern:** Modular Monolith (.NET 10)  
> **References:** Kamil Grzybek (Modular Monolith Architecture), Sam Newman (Monolith to Microservices).

---

## 1. Guiding Principles

1. **Unidirectionality:** All dependencies converge exclusively toward the `Core` module.
2. **Module Isolation:** No operational module (`OCR`, `Translation`, `UI`, `Admin`) may directly reference another horizontal module.
3. **Design by Contract:** All cross-module communication happens exclusively through abstract interfaces defined in `Aurora.Core`.

---

## 2. Component Diagram

```mermaid
graph TD
    User(["Operator / Legacy Software"])
    AppEntry["Aurora.App\n(Composition Root)"]
    Core["Aurora.Core\n(IOcrService, ITranslationEngine, IAppSettings, IModelManager)"]
    OCR["Aurora.OCR\n(WinRT OCR Engine)"]
    Translation["Aurora.Translation\n(Cascading JSON & Fallback)"]
    UI["Aurora.UI\n(WPF Overlay & Tray Icon)"]
    Admin["Aurora.Admin\n(Config & GitHub Releases)"]

    User -->|"Triggers Hotkey / Views Overlay"| UI
    AppEntry -->|"Bootstraps & wires DI"| UI
    AppEntry -->|"Bootstraps & wires DI"| OCR
    AppEntry -->|"Bootstraps & wires DI"| Translation
    AppEntry -->|"Bootstraps & wires DI"| Admin
    AppEntry -->|"Resolves contracts"| Core
    UI -->|"Uses contracts"| Core
    OCR -->|"Implements IOcrService"| Core
    Translation -->|"Implements ITranslationEngine, IModelManager"| Core
    Admin -->|"Configures global state"| Core
```

---

## 3. Package / Dependency Diagram

Compilation-time dependencies between .NET projects. Arrows point toward the depended-on project.

> **Rule:** No horizontal arrows are allowed. Any dependency not pointing to `Aurora.Core` is a build-breaking architectural violation enforced by `Aurora.Tests.Architecture`.

```mermaid
classDiagram
    direction BT
    class AppEntry["Aurora.App"]
    class Core["Aurora.Core"]
    class OCR["Aurora.OCR"]
    class Translation["Aurora.Translation"]
    class UI["Aurora.UI"]
    class Admin["Aurora.Admin"]

    OCR ..> Core
    Translation ..> Core
    UI ..> Core
    Admin ..> Core
    AppEntry ..> Core
    AppEntry ..> OCR
    AppEntry ..> Translation
    AppEntry ..> UI
    AppEntry ..> Admin
```
