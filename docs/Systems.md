# Core Systems

The current bootstrap profile wires the following long-lived systems. Each section summarizes the
system's responsibilities, dependencies, and the events it produces or consumes.

## Event Bus
* **Location:** `Assets/Game/Scripts/Systems/EventBus/EventBus.cs`
* **Purpose:** Central publish/subscribe hub. Queues events, enforces optional history, and logs
  unhandled event types.【F:Assets/Game/Scripts/Systems/EventBus/EventBus.cs†L9-L111】
* **Notes:** Flushes synchronously when events are published. Systems should assume event delivery
  occurs within the same frame unless a queue is intentionally deferred.【F:Assets/Game/Scripts/Systems/EventBus/EventBus.cs†L52-L111】

## Time System
* **Dependencies:** `EventBus`
* **Role:** Converts Unity `deltaTime` into calendar days, months, and years; broadcasts
  `OnNewDayEvent`, `OnNewMonthEvent`, and `OnNewYearEvent` as time advances.【F:Assets/Game/Scripts/Systems/Time/TimeSystem.cs†L8-L83】
* **Key APIs:** Game speed controls, pause/resume, deterministic day stepping.【F:Assets/Game/Scripts/Systems/Time/TimeSystem.cs†L40-L78】

## Character System
* **Dependencies:** `EventBus`, `TimeSystem`
* **Role:** Loads the base population, manages lifecycle services (ageing, mortality, family
  structure), and publishes population ticks and lifecycle events in response to time updates.【F:Assets/Game/Scripts/Characters/CharacterSystem.cs†L13-L113】
* **Collaboration:** Serves as the authoritative data source for other systems via repositories and
  helper services; dispatches aggregated `DailyPopulationMetrics` through the event bus.【F:Assets/Game/Scripts/Characters/CharacterSystem.cs†L90-L138】

## Birth System
* **Dependencies:** `EventBus`, `CharacterSystem`
* **Role:** Reads fertility configuration, schedules pregnancies, and resolves births on due dates.
  Raises `OnCharacterBorn` events and maintains deterministic RNG state in its save data.【F:Assets/Game/Scripts/Systems/BirthSystem.cs†L10-L132】

## Marriage System
* **Dependencies:** `EventBus`, `CharacterSystem`
* **Role:** Performs daily matchmaking, applies configurable eligibility rules, and publishes
  `OnCharacterMarried` when unions succeed.【F:Assets/Game/Scripts/Systems/MarriageSystem.cs†L10-L121】

## Office System
* **Dependencies:** `EventBus`, `CharacterSystem`
* **Role:** Loads magistrate definitions, seeds initial office holders, tracks seat occupancy, and
  reacts to deaths and time to maintain eligibility. Issues `OfficeAssignedEvent` when seats change.
  Provides election data to higher-level political systems.【F:Assets/Game/Scripts/Systems/Politics/Offices/OfficeSystem.cs†L1-L129】

## Election System
* **Dependencies:** `EventBus`, `TimeSystem`, `CharacterSystem`, `OfficeSystem`
* **Role:** Drives the annual election calendar, evaluates candidate declarations, simulates voting,
  and publishes election lifecycle events that the politics system consumes.【F:Assets/Game/Scripts/Systems/Politics/Elections/ElectionSystem.cs†L1-L104】

## Politics System
* **Dependencies:** `EventBus`, `TimeSystem`, `CharacterSystem`, `OfficeSystem`, `ElectionSystem`
* **Role:** Aggregates political state, tracks eligibility over time, and reacts to election results
  to update term history and future eligibility.【F:Assets/Game/Scripts/Systems/Politics/PoliticsSystem.cs†L1-L104】

## Supporting Utilities
* **System Registry:** Hosts all systems, enforces dependency initialization order, and forwards
  update/save/load/shutdown calls.【F:Assets/Game/Scripts/Core/SystemRegistry.cs†L7-L156】
* **System Resolver:** Provides controlled cross-system lookup during bootstrap without encouraging
  direct, hard-wired dependencies at runtime.【F:Assets/Game/Scripts/Core/SystemBootstrapProfile.cs†L95-L140】

## Adding New Systems
When introducing a new module:
1. Implement `GameSystemBase` (or `IGameSystem`) and restrict external communication to events.
2. Register the system in a bootstrap profile with explicit dependencies.
3. Prefer publishing new `GameEvent` types over invoking other systems directly. Use the resolver
   only for read-heavy collaborations that cannot be modeled as events.
