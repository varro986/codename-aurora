---
name: feature-spec
description: How to translate an Issue into xUnit test stubs and Reqnroll Gherkin scenarios. Load before writing any test file or .feature file.
---

# Feature Spec Skill

Load before reading an Issue, writing test stubs, or writing `.feature` files.

---

## Before writing any spec

1. Read `specs/architecture/architecture.md` — specifically the module structure and isolation table.
2. For each AC you are about to write: state which modules it touches and which Core interface it exercises.
3. Verify no AC introduces a new dependency that violates the isolation table (no sibling module cross-reference).
4. If the feature requires a structural decision not covered by architecture.md: write an ADR first. Do not embed architectural decisions inside a US.

---

## Reading an Issue → test stub

An Issue created via `feature.yml` contains:
- **User Story** — who, what, why
- **Acceptance Criteria** — testable conditions in Gherkin form
- **C# constraints** — interfaces, method signatures, layer placement

### Workflow

1. Read the Issue fully before writing a single line of test code.
2. Map each acceptance criterion to one or more test methods.
3. Remove `[Fact(Skip = "...")]` from the generated stub — do NOT delete the class.
4. Write test bodies against the acceptance criteria, not the implementation.
5. Write the Reqnroll `.feature` file mirroring the same criteria (1:1 with facts).

---

## xUnit stub pattern

```csharp
#nullable enable
using Xunit;

namespace CodenameAurora.Tests.Unit;

public sealed class Issue<N>Tests
{
    [Fact]
    [Trait("US", "US-<N>")]
    public async Task AcceptanceCriterionInPascalCase()
    {
        // Arrange

        // Act

        // Assert
    }
}
```

Rules:
- Method name = acceptance criterion in PascalCase. No `Should` prefix.
- `[Trait("US", "US-<N>")]` on every test — traceability to the Issue.
- One `[Fact]` per criterion — no multi-assert omnibus tests.
- `[Theory]` when the criterion has multiple distinct data points.
- `[Fact(Skip = "...")]` never in committed code.

---

## Reqnroll setup

Add to the test project (align version with your NuGet sources):
```xml
<PackageReference Include="Reqnroll.xUnit" Version="2.*" />
```

Create `reqnroll.json` in the test project root:
```json
{
  "$schema": "https://schemas.reqnroll.net/reqnroll-config-latest.json",
  "bindingCulture": { "language": "en-US" }
}
```

Step definition pattern:
```csharp
#nullable enable
using Reqnroll;

namespace CodenameAurora.Tests.Unit.Steps;

[Binding]
public sealed class Issue<N>Steps
{
    [Given(@"<precondition>")]
    public void Given_Description() { }

    [When(@"<action>")]
    public void When_Description() { }

    [Then(@"<outcome>")]
    public void Then_Description() { }
}
```

---

## Gherkin pattern

```gherkin
Feature: <Feature name from Issue title>

  @US-<N>
  Scenario: <Acceptance criterion as a sentence>
    Given <precondition>
    When  <action>
    Then  <observable outcome>
```

Rules:
- `@US-<N>` tag on every scenario — traceability to the Issue.
- One scenario per acceptance criterion (mirrors the xUnit facts 1:1).
- `Given / When / Then` as openers only — `And` continues, never opens.
- Steps describe user-visible behavior, never implementation details.

---

## File locations

| Artifact | Path |
|---|---|
| Test stub | `tests/CodenameAurora.Tests.Unit/Issue<N>Tests.cs` |
| Feature file | `tests/features/Issue<N>.feature` |
| Step definitions | `tests/CodenameAurora.Tests.Unit/Steps/Issue<N>Steps.cs` |

---

## Architecture test pattern

Every PR that adds a layer dependency must pass:
```bash
dotnet test --filter "Category=Architecture"
```

NetArchTest example — add to the test project:
```csharp
#nullable enable
using NetArchTest.Rules;
using Xunit;

namespace CodenameAurora.Tests.Architecture;

[Trait("Category", "Architecture")]
public sealed class ArchitectureTests
{
    [Fact]
    public void Domain_must_not_reference_Infrastructure()
    {
        var result = Types.InAssembly(typeof(Domain.SomeType).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Infrastructure")
            .GetResult();
        Assert.True(result.IsSuccessful,
            string.Join('\n', result.FailingTypeNames ?? []));
    }
}
```

Add `NetArchTest.Fluent` to the test project:
```xml
<PackageReference Include="NetArchTest.Fluent" Version="1.*" />
```

---

## Definition of done (test perspective)

- [ ] `[Fact(Skip)]` removed from every test method
- [ ] All `[Fact]` / `[Theory]` bodies assert real behavior
- [ ] All Reqnroll `@US-<N>` scenarios pass
- [ ] `dotnet test --filter "Category=Architecture"` still green
