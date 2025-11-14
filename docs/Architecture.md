# Architecture Overview

The Cursus Honorum simulation is organized around a modular service layer that is loaded and
maintained by the `GameState`. On startup, the `GameState` uses a `SystemBootstrapProfile` to
register system descriptors and asks the `SystemRegistry` to materialize, order, and initialize
the modules. Each module implements `IGameSystem` through `GameSystemBase`, giving it a shared
lifecycle (`Initialize`, `Update`, `Shutdown`), activation toggles, and access to the common
`GameState` for cross-system lookup when absolutely necessary.【F:Assets/Game/Scripts/Core/GameState.cs†L6-L61】【F:Assets/Game/Scripts/Core/GameSystemBase.cs†L7-L88】【F:Assets/Game/Scripts/Core/SystemBootstrapProfile.cs†L7-L91】

## Modular Composition

Systems are instantiated from descriptors that declare their dependencies. The `SystemRegistry`
performs a topological sort over the dependency graph, ensuring that every system is built and
initialized after its prerequisites while detecting circular references early. Once active, the
registry ticks each system every frame and coordinates save/load requests across the roster. This
keeps the simulation structured as a set of cooperating services rather than a monolith.【F:Assets/Game/Scripts/Core/SystemRegistry.cs†L7-L132】【F:Assets/Game/Scripts/Core/SystemRegistry.cs†L183-L239】

The default bootstrap profile composes the current simulation by wiring the Event Bus, Time,
Character, Birth, Office, Marriage, Election, and umbrella Politics systems. Because descriptors
are data-driven, alternative profiles can swap or extend the stack without changing the core
wiring code.【F:Assets/Game/Scripts/Core/SystemBootstrapProfile.cs†L25-L83】

## Game Systems and Communication

A **Game System** is any long-lived module derived from `GameSystemBase`. Systems should be
self-contained, focused on a single simulation concern, and expose their capabilities through
published events or data pulled via the shared `GameState` when unavoidable.【F:Assets/Game/Scripts/Core/GameSystemBase.cs†L7-L88】

Communication between systems follows an **EventBus-first rule**. Systems publish `GameEvent`
derivatives onto the centralized `EventBus`, which queues, flushes, and logs delivery to
subscribers. Direct service calls between systems should be reserved for coarse-grained queries
that cannot be expressed as events, and even then should be mediated via the resolver provided by
the bootstrap (avoiding direct instantiation).【F:Assets/Game/Scripts/Systems/EventBus/EventBus.cs†L9-L111】【F:Assets/Game/Scripts/Core/SystemBootstrapProfile.cs†L35-L83】

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
