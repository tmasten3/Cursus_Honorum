# Architecture Overview

The Cursus Honorum simulation is organized around a modular service layer that is loaded and
maintained by the `GameState`. On startup, the `GameState` uses a `SystemBootstrapProfile` to
register system descriptors and asks the `SystemRegistry` to materialize, order, and initialize
the modules. Each module implements `IGameSystem` through `GameSystemBase`, which provides a
shared lifecycle, activation toggles, and limited access to the common `GameState` for
cross-system lookup when absolutely necessary.【F:Assets/Game/Scripts/Core/GameState.cs†L6-L61】【F:Assets/Game/Scripts/Core/GameSystemBase.cs†L7-L88】【F:Assets/Game/Scripts/Core/SystemBootstrapProfile.cs†L7-L91】

## Lifecycle of a Game System

`GameSystemBase` defines three explicit lifecycle phases that every concrete system honors:

1. **Initialize** – the registry injects the active `GameState`, validates dependencies, and lets
   the system subscribe to events or warm caches. Initialization must be idempotent; attempting to
   initialize twice throws to surface ordering issues.【F:Assets/Game/Scripts/Core/GameSystemBase.cs†L30-L48】
2. **Tick (Update)** – once active, the registry calls `Update` every frame (or simulated step).
   Systems should perform deterministic work here and rely on the EventBus to communicate changes
   outward.【F:Assets/Game/Scripts/Core/GameSystemBase.cs†L50-L58】【F:Assets/Game/Scripts/Core/SystemRegistry.cs†L55-L118】
3. **Shutdown** – invoked during teardown or profile swaps so systems can unsubscribe, release
   caches, and mark themselves inactive while preserving serialized state as needed.【F:Assets/Game/Scripts/Core/GameSystemBase.cs†L60-L75】

Systems that need to pause execution can use `Activate`/`Deactivate`, but lifecycle transitions
should remain the responsibility of the registry to keep ordering deterministic.【F:Assets/Game/Scripts/Core/GameSystemBase.cs†L77-L104】

## EventBus Domain Categories

All cross-system collaboration flows through the global `EventBus`. Events are grouped into
domain-focused categories to keep subscriptions intentional and to maintain a readable history
buffer.【F:Assets/Game/Scripts/Systems/EventBus/EventBus.cs†L9-L111】 The current catalog includes:

* **Time** – calendar progression events (`OnNewDayEvent`, `OnNewMonthEvent`, `OnNewYearEvent`) keep
  every system synchronized on the simulation clock.【F:Assets/Game/Scripts/Core/GameEvent.cs†L6-L43】
* **Character** – lifecycle transitions such as births, deaths, and marriages broadcast updates to
  dependent systems (`OnCharacterBorn`, `OnCharacterDied`, `OnCharacterMarried`).【F:Assets/Game/Scripts/Characters/CharacterEvents.cs†L5-L56】
* **Family** – demographic rollups and kinship-driven signals (`OnPopulationTick`) allow systems to
  react to family counts without direct repository queries.【F:Assets/Game/Scripts/Characters/CharacterEvents.cs†L58-L86】
* **Influence** – ambition and prestige shifts are surfaced through events like
  `OnCharacterAmbitionChanged`, `OnCharacterRetired`, and `OnCharacterTraitAdvanced`, providing
  hooks for politics or narrative layers.【F:Assets/Game/Scripts/Characters/CharacterEvents.cs†L88-L156】
* **Office** – magistracy assignments flow through `OfficeAssignedEvent`, enabling historical
  tracking and eligibility refreshes inside the politics stack.【F:Assets/Game/Scripts/Core/PoliticsEvents.cs†L1-L41】【F:Assets/Game/Scripts/Systems/Politics/PoliticsSystem.cs†L43-L87】
* **Election** – season milestones (`ElectionSeasonOpenedEvent`, `ElectionSeasonCompletedEvent`)
  coordinate campaigning, vote simulation, and UI reports.【F:Assets/Game/Scripts/Core/PoliticsEvents.cs†L1-L29】【F:Assets/Game/Scripts/Systems/Politics/PoliticsSystem.cs†L38-L79】
* **Senate** – the politics layer reserves capacity for senate-specific traffic by tracking office
  assemblies and term histories, allowing future consular or senatorial events to plug into the
  same bus without reworking dependencies.【F:Assets/Game/Scripts/Systems/Politics/Offices/OfficeDefinitions.cs†L1-L47】【F:Assets/Game/Scripts/Systems/Politics/PoliticsSystem.cs†L22-L123】
* **UI & Debug** – presentation systems such as the debug overlay consume EventBus history rather
  than owning gameplay logic, enabling tooling to observe state safely.【F:Assets/Game/Scripts/Systems/EventBus/EventBus.cs†L17-L70】【F:Assets/Game/Scripts/UI/DebugOverlayDataAdapter.cs†L189-L217】

These categories should remain loosely coupled. New events should align with an existing category
when possible; introduce new categories sparingly to maintain observability.

## Modular Composition and Load Order

Systems are instantiated from descriptors that declare their dependencies. The `SystemRegistry`
performs a topological sort over the dependency graph, ensuring that every system is built and
initialized after its prerequisites while detecting circular references early. Once active, the
registry ticks each system and coordinates save/load requests across the roster.【F:Assets/Game/Scripts/Core/SystemRegistry.cs†L7-L200】

The default bootstrap profile loads systems in dependency-friendly layers: Event Bus → Time →
Character → Birth → Office → Marriage → Election → umbrella Politics. This order primes the event
graph so that foundational services publish signals before dependent systems subscribe. Profiles
may be reordered or extended later, but every reconfiguration should continue honoring declared
dependencies so the registry’s sort remains authoritative.【F:Assets/Game/Scripts/Core/SystemBootstrapProfile.cs†L25-L83】

## Communication Philosophy

Communication between systems follows an **EventBus-first** rule. Systems publish `GameEvent`
derivatives onto the centralized bus, which queues, flushes, and logs delivery to subscribers.
Direct service calls should be reserved for coarse-grained queries that cannot be expressed as
events, and even then should be mediated through the resolver or shared `GameState`. Avoiding
tight coupling preserves determinism and makes it trivial to add or remove systems without
refactoring others.【F:Assets/Game/Scripts/Systems/EventBus/EventBus.cs†L9-L111】【F:Assets/Game/Scripts/Core/SystemBootstrapProfile.cs†L35-L83】【F:Assets/Game/Scripts/Core/GameSystemBase.cs†L19-L28】

## Naming Conventions

* Systems are suffixed with `System` (e.g., `TimeSystem`, `ElectionSystem`).
* Domain events inherit from `GameEvent` and are suffixed with `Event` (e.g., `OnNewDayEvent`,
  `ElectionSeasonOpenedEvent`).
* Persistent data records use the `Data` suffix when represented as DTOs (e.g., `OfficeDefinitionData`).
* Manager-style classes (`XxxManager`) are reserved for objects that orchestrate multiple
  collaborating helpers inside a single system; they should not replace systems themselves.

These conventions describe the prevailing patterns and are meant as guidelines rather than hard
rules—exceptions are acceptable when they improve clarity.

## Modular Direction

The long-term goal is to keep feature work additive: new functionality should arrive as new
systems, descriptors, or events instead of modifying existing modules extensively. This keeps the
simulation resilient, makes bootstrapping configurable, and encourages composition over coupling.
