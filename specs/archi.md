# Architettura del Sistema - Code Name Aurora

> **Pattern:** Modular Monolith (.NET 8.0)  
> **Riferimento Teorico:** Kamil Grzybek (Modular Monolith Architecture), Sam Newman (Monolith to Microservices).

---

## 1. Principi Guida

1. **Unidirezionalità:** Tutte le dipendenze convergono unicamente verso il modulo `Core`.
2. **Isolamento dei Moduli:** Nessun modulo operativo (`OCR`, `Translation`, `UI`, `Admin`) può referenziare direttamente un altro modulo orizzontale.
3. **Design by Contract:** Ogni comunicazione tra moduli avviene esclusivamente tramite interfacce astratte definite in `Aurora.Core`.

---

## 2. Diagramma dei Componenti (UML Component Diagram)

```mermaid
componentDiagram
    actor User as Operatore / Legacy Software

    package "Code Name Aurora Solution" {
        [Aurora.Core] as Core : Interfacce pure (IOcrService, ITranslationEngine)
        [Aurora.OCR] as OCR : WinRT OCR Engine
        [Aurora.Translation] as Translation : Cascading JSON & Fallback
        [Aurora.UI] as UI : WPF Overlay & Tray Icon
        [Aurora.Admin] as Admin : Config & GitHub Releases
    }

    User --> UI : Attiva Hotkey / Visualizza Overlay
    UI --> Core : Utilizza contratti
    OCR --> Core : Implementa IOcrService
    Translation --> Core : Implementa ITranslationEngine
    Admin --> Core : Configura stato globale