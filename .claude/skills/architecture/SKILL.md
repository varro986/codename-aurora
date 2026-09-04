---
name: architecture
description: Architecture rules — layer boundaries, interface conventions, async rules, known debt. Load BEFORE writing or modifying any .cs file.
---

# Architecture Skill

Load before modifying interfaces, project dependencies, the composition root, or adding new modules.

## Pre-implementation questions

Answer these before writing a single line of code:

1. **Layer**: Which layer owns this class? Does it depend only on inward layers?
2. **Contract**: Is the cross-layer surface an interface defined in the right layer?
3. **Async**: Is all I/O async end-to-end? No `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`?
4. **Nullable**: Does every new `.cs` file open with `#nullable enable`?
5. **Precedent**: What existing `file:line` does this pattern follow?

If you cannot answer these, investigate before writing.

## Layer law

```
App  (composition root — wires DI, zero business logic)
         ↓ depends on
Core  (interfaces, notifications, domain types — no outward deps)
         ↑ implemented by
OCR | Translation | UI | Admin
(operational modules — depend on Core only, never on each other)
```

Cross-module communication: interfaces defined in Core only — never `using SiblingModule.*` across operational modules. `App` is the sole exception: it references all modules to wire DI.

## Rules (all layers, non-negotiable)

- `#nullable enable` mandatory on every `.cs` file
- Interfaces at every layer boundary — never expose concretions across layers
- No static mutable state outside of configuration bootstrapping
- All I/O async: `Task` / `ValueTask`. No `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`
  - Exception: top-level entry points where no async host is available — document the reason inline above the call
- Exception handling at layer boundaries only, not scattered through use cases
- `ILogger<T>` for all logging — never `Console.WriteLine` outside the presentation/entry-point layer

## Adding a new module

Existing modules: `CodenameAurora.OCR`, `CodenameAurora.Translation`, `CodenameAurora.UI`, `CodenameAurora.Admin`.
All depend on `CodenameAurora.Core` only. `CodenameAurora.App` is the sole composition root.

1. Create `src/CodenameAurora.<Name>/CodenameAurora.<Name>.csproj` referencing `CodenameAurora.Core` only.
2. Define any cross-module notifications/events in `CodenameAurora.Core/Notifications/` — never in the new module.
3. Register services in `CodenameAurora.App` DI wiring — never inside the module itself.
4. Add an architecture test in `tests/CodenameAurora.Tests.Architecture/` tagged `[Trait("Category", "Architecture")]`.

## Modifying a shared interface

1. `Grep "IInterfaceName"` — find every implementor and caller first
2. Update implementations and tests before changing the interface signature
3. Build: `dotnet build [SLN] --configuration Release`
4. Run architecture tests: `dotnet test [SLN] --filter "Category=Architecture"`

## Reference files

- `specs/architecture/architecture.md` — source of truth for structural decisions; read before any structural change
- `specs/architecture/adr/` — Architecture Decision Records (ADR-001 onwards)
- `tests/CodenameAurora.Tests.Architecture/` — architecture test project (NetArchTest enforcing dependency rules)
- `Directory.Build.props` — shared properties (`Nullable`, `TreatWarningsAsErrors`)

## Architecture test setup (NetArchTest)

Add to the test project:
```xml
<PackageReference Include="NetArchTest.Fluent" Version="1.*" />
```

Tag every architecture test with `[Trait("Category", "Architecture")]` so they can be run in isolation:
```bash
dotnet test --filter "Category=Architecture"
```

Write one test per architectural rule. The rule name IS the test name:
```csharp
[Trait("Category", "Architecture")]
public sealed class ArchitectureTests
{
    [Fact] public void Domain_must_not_reference_Infrastructure() { ... }
    [Fact] public void Application_must_not_reference_Infrastructure() { ... }
    [Fact] public void Infrastructure_must_not_reference_Presentation() { ... }
}
```

## Known debt

(none — list violations here as they accumulate; do not extend without justification in the PR)
