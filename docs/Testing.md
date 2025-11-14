# Testing Overview

Automated coverage is provided through NUnit edit-mode tests in the `Tests/` directory. The suite
focuses on validating system wiring, event delivery, and long-running simulation behaviour.

## Unit Tests
* **Event Bus:** Ensures event history behaves according to configured capacity, including disabling
  the buffer entirely.【F:Tests/EventBus/EventBusTests.cs†L7-L59】
* **System Registry:** Verifies dependency ordering and cycle detection when initializing systems
  based on their declared dependencies.【F:Tests/Core/SystemRegistryTests.cs†L8-L116】

## Simulation Harnesses
Population and politics simulations rely on lightweight harnesses that instantiate concrete systems
and advance the calendar deterministically.

* **PopulationSimulationTests:** Exercises marriage and birth flows over multi-decade spans and
  checks that JSON overrides propagate into runtime settings.【F:Tests/Simulation/PopulationSimulationTests.cs†L14-L183】
* **PoliticsSimulationTests:** Validates deferred office assignments, deterministic election results,
  and election cycle state transitions using the same harness infrastructure.【F:Tests/Simulation/PoliticsSimulationTests.cs†L17-L198】

## Authoring Guidelines
* Tests create their own temporary data directories and use the same bootstrap utilities as the
  game, so new systems should expose constructors friendly to harness creation.【F:Tests/Simulation/PopulationSimulationTests.cs†L153-L183】【F:Tests/Simulation/PoliticsSimulationTests.cs†L149-L193】
* When adding events, prefer writing focused unit tests that assert event sequencing and history
  behaviour before relying on long simulation runs.
* Keep Unity-specific behaviour mocked behind `UnityStubs.cs` where possible to allow execution in
  headless environments.【F:Tests/UnityStubs.cs†L1-L120】
