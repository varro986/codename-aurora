# ADR-001: Adoption of the Modular Monolith Pattern

* **Status:** Approved
* **Date:** 2026-07-27
* **Author:** Architect

---

## 1. Context and Problem

Codename Aurora requires a rigorous, maintainable, and modular architecture with clear separation of responsibilities across OCR, Translation, UI, and Administration. As a desktop tool maintained by a single developer or small team, adopting a Microservices architecture would introduce unjustified network complexity, deployment overhead, and maintenance burden.

## 2. Options Considered

1. **Traditional Monolith (Spaghetti Code):**
   * *Pros:* Fast initial development.
   * *Cons:* High coupling, cross-cutting dependencies, hard to test and evolve.
2. **Microservices:**
   * *Pros:* Total isolation and independent deployment.
   * *Cons:* Unjustified operational complexity, network overhead, complex IPC management.
3. **Modular Monolith (.NET 10):**
   * *Pros:* Logical module isolation via C# projects, clear contracts via `Core`, single build/deploy pipeline.
   * *Cons:* Requires strict governance via NetArchTest to prevent unintended coupling.

## 3. Decision

Adopt the **Modular Monolith** pattern. The solution will consist of 5 separate .NET 10 projects. All modules depend exclusively on the `Core` module. No direct dependency between operational modules (`OCR`, `Translation`, `UI`, `Admin`) is permitted.

> **Note (ADR-007):** A dedicated composition root project `Aurora.App` was later introduced, bringing the total to 6 projects. See ADR-007.

## 4. Consequences

* **Positive impacts:** High maintainability, clear domain boundaries, easy unit and architectural testing.
* **Risks and trade-offs:** Risk of cyclic dependencies if not filtered in CI by *NetArchTest*.

## 5. References

* Architecture specification: `specs/archi.md`
