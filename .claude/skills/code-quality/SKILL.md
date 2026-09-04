---
name: code-quality
description: Code quality standards. Load BEFORE writing or modifying any .cs file, after the architecture skill.
---

# Code Quality Standards

This skill covers HOW to write code within a layer. The `architecture` skill covers WHERE code goes.

---

## Pre-implementation questions

Answer these before writing any code. If you cannot answer them, investigate first.

1. **Scope**: What are ALL the places this feature touches? (domain, application, infrastructure, presentation, tests)
2. **States**: What are the error, loading/pending, empty, and success states?
3. **Async boundary**: Is every I/O call async end-to-end? Is there a `.Result` or `.Wait()` anywhere in the path?
4. **Consistency**: What existing pattern should this follow? Find and cite a `file:line` example.
5. **Size**: Will the class stay under its size limit? If not, name the decomposition move now.

---

## Core principles

### SOLID
- **Single Responsibility**: one class, one reason to change.
- **Open/Closed**: extend behavior without modifying existing code; use interfaces and composition.
- **Liskov Substitution**: subtypes must be substitutable for their base types without surprises.
- **Interface Segregation**: small focused interfaces over large catch-all ones.
- **Dependency Inversion**: depend on abstractions (interfaces), never concretions, across layer boundaries.

### DRY
Extract common logic into shared utilities. Check for existing patterns before implementing.

### KISS
Prefer simple readable solutions over clever ones. Avoid premature optimization.

### YAGNI
Only implement what is currently needed. Remove unused code rather than commenting it out.

---

## File size limits

| Class type | Soft limit | Action when exceeded |
|---|---|---|
| Use case (`Application/`) | ~150 lines | Extract domain logic to a domain service |
| Handler (`IRequestHandler`) | ~100 lines | Extract sub-handlers or a helper service |
| Service (`Infrastructure/`) | ~300 lines | Extract sub-services by concern |
| Repository | ~200 lines | Extract query/command objects |
| Presenter / ViewModel | ~200 lines | Extract handlers or delegates |

These are guidelines, not hard cuts. A class at 180 lines with a clear single responsibility is better than two artificial 90-line classes.

---

## Decomposition patterns

Named moves to use when a class grows beyond its limit.

| Pattern | When to use | Example |
|---|---|---|
| **Handler** | One specific command or event, one responsibility | `HotkeyTriggeredHandler : IRequestHandler<HotkeyTriggered>` |
| **Service** | Business logic for one domain concern | `OcrService`, `TranslationService` |
| **Facade** | Thin orchestrator combining multiple services | `PipelineExecutor` orchestrates capture → OCR → translate → display |
| **Delegate** | Sub-object handling a slice of a larger class | `ContinuousModeController` extracted from `CaptureService` |

Prefer composition over inheritance. A new abstraction layer requires a stronger justification than "the class is long".

---

## Completion criteria

A feature is NOT done until all of these are true.

### Blocking
- [ ] All Gherkin scenarios in `.feature` file pass (Reqnroll)
- [ ] All xUnit tests pass — `Skip` attribute removed
- [ ] `dotnet build --configuration Release` — zero errors, zero new warnings
- [ ] `dotnet csharpier --check .` — no formatting drift
- [ ] Error and empty states handled explicitly — no silent failures
- [ ] No hardcoded secrets, connection strings, or magic numbers

### Required
- [ ] Class stays within its size limit (or decomposition is named and justified)
- [ ] Interfaces used at every layer boundary, never concretions
- [ ] No new `async void` (except event handlers — state the reason)
- [ ] No `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` introduced
- [ ] New code cites an existing `file:line` precedent for its pattern
