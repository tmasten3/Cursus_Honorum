# Cursus Honorum — Project Map for AI Agents

## High-Level Overview
- **Project type:** Unity simulation game set in the Roman Republic, focused on generational character dynamics, civic offices, and political seasons.
- **Architecture:** Modular service-based systems composed of `IGameSystem` implementations managed by `SystemRegistry`. Systems communicate almost exclusively through the event-driven `EventBus`, keeping data, logic, and presentation layers decoupled.
- **Simulation focus:** Time progression, character lifecycle, family structures, births, marriages, magistrate offices, and elections. Debug tooling and logging provide runtime visibility without altering deterministic gameplay logic.

## Directory Structure Summary
- **Assets/Game/Core** – No standalone folder; core runtime code lives under `Assets/Game/Scripts/Core`, containing entry points (`GameController`, `GameState`), the modular system framework (`GameSystemBase`, `SystemRegistry`, bootstrap profiles), and the global `EventBus`/`GameEvent` definitions.
- **Assets/Game/Systems** – Located at `Assets/Game/Scripts/Systems`. Hosts deterministic simulation modules (Time, Birth, Marriage, Politics). Subfolders such as `Politics/Offices` and `Politics/Elections` encapsulate office management and election orchestration.
- **Assets/Game/Scripts/UI** – UI-specific behaviours like `DebugOverlayController` that render simulation stats and log output without owning gameplay logic.
- **Assets/Game/Data** – Authoritative JSON data (`base_characters.json`, `BaseOffices.json`) providing initial state for characters and political offices. Treated as read-only configuration.
- **Assets/Game/Utilities** – Shared infrastructure utilities (`Logger`, `LogBatch`) for diagnostics and cross-system helpers.
- **Assets/Game/Core (assembly)** – `Game.asmdef` defines the assembly for all game scripts to enforce namespace and dependency boundaries.
- **Packages/** – Unity package manifest and lock files managing editor/runtime packages. Do not edit manually unless package changes are requested.
- **ProjectSettings/** – Unity project configuration (input, quality, editor). Modify only when explicitly required for engine setup.

## System Definitions
### Event Bus
- **Purpose:** Central publish/subscribe hub for all gameplay events (`GameEvent` subclasses). Ensures loose coupling between systems.
- **Communication:** Systems inject `EventBus` and subscribe to relevant events (`OnNewDay`, `OnCharacterBorn`, election notifications). Queue-based processing flushes in `Update`.
- **Key files:** `Assets/Game/Scripts/Core/EventBus.cs`, `Assets/Game/Scripts/Core/GameEvent.cs`.
- **Notes:** Maintains delivery history, optional event whitelist, and duplicate subscription guards.

### Time System
- **Purpose:** Advances the simulation calendar, translating Unity `Time.deltaTime` into in-game days/months/years.
- **Communication:** Publishes `OnNewDayEvent`, `OnNewMonthEvent`, and `OnNewYearEvent` through the `EventBus`. Consumed by character, birth, marriage, and political systems.
- **Key files:** `Assets/Game/Scripts/Systems/TimeSystem.cs`.
- **Notes:** Supports pause/resume, configurable day length, deterministic stepping via explicit `StepDays`.

### Character System
- **Purpose:** Owns authoritative character repository including life state, families, ages, and mortality.
- **Communication:** Subscribes to time events for daily updates, publishes lifecycle events (deaths, population metrics). Consumed by birth, marriage, office, and election systems via direct service access.
- **Key files:** `Assets/Game/Scripts/Characters/*` (repository, services, factory) and `Assets/Game/Scripts/Systems/CharacterSystem/CharacterSystem.cs`.
- **Notes:** Loads initial population from `base_characters.json`, uses injected services for age/mortality/family logic, and persists via JSON save blobs.

### Birth System
- **Purpose:** Schedules pregnancies for eligible married women and resolves births (including multiples).
- **Communication:** Subscribes to daily time events; invokes `CharacterSystem` APIs to spawn children and publishes `OnCharacterBorn` events.
- **Key files:** `Assets/Game/Scripts/Systems/BirthSystem.cs`.
- **Notes:** Maintains in-memory pregnancy queue derived from deterministic RNG seeded via config.

### Marriage System
- **Purpose:** Matches single characters each day based on eligibility, class weighting, and chance.
- **Communication:** Listens for `OnNewDayEvent` and performs direct calls to `CharacterSystem.Marry`. Publishes `OnCharacterMarried` when unions succeed.
- **Key files:** `Assets/Game/Scripts/Systems/MarriageSystem.cs`.
- **Notes:** Caps matchmaking attempts per day and respects configurable social-class biases.

### Office System
- **Purpose:** Manages Roman magistrate offices, including definitions, seat occupancy, eligibility, and historical records.
- **Communication:** Loads office definitions from `BaseOffices.json`, responds to time/death events, publishes `OfficeAssignedEvent` via the `EventBus`, and feeds election data.
- **Key files:** `Assets/Game/Scripts/Systems/Politics/Offices/*` (definitions, state, eligibility, system).
- **Notes:** Seeds initial office holders deterministically, tracks active/pending terms, and enforces seat structures.

### Election System
- **Purpose:** Simulates annual election cycles, including candidate declarations, ambition scoring, and seat allocation outcomes.
- **Communication:** Consumes time events, queries `OfficeSystem` for open seats, `CharacterSystem` for candidate data, and publishes election lifecycle events (`ElectionSeasonOpenedEvent`, `ElectionSeasonCompletedEvent`, `OfficeAssignedEvent`).
- **Key files:** `Assets/Game/Scripts/Systems/Politics/Elections/*` (system, events, models).
- **Notes:** Maintains per-year declaration/results registries and uses deterministic RNG for weighted picks.

### Debug Overlay
- **Purpose:** Runtime UI overlay that surfaces current date, population stats, and recent logs for developers.
- **Communication:** Binds to `GameController` to access `GameState`, `TimeSystem`, `CharacterSystem`, `OfficeSystem`, and `ElectionSystem`. Pulls data directly and displays `Logger` output.
- **Key files:** `Assets/Game/Scripts/UI/DebugOverlayController.cs`.
- **Notes:** Pure presentation; avoid placing gameplay logic here.

### Logging / LogBatch
- **Purpose:** Provide centralized logging with console colorization, persistent file output, and batched warning helpers.
- **Communication:** Static `Logger` class used across systems; `LogBatch` collects related warnings before flushing to disk.
- **Key files:** `Assets/Game/Scripts/Utilities/Logger.cs`, `Assets/Game/Scripts/Utilities/LogBatch.cs`.
- **Notes:** Respects `MinimumLevel`, writes to Unity console and persistent files, supports debug overlay consumption.

## Constraints & Rules for AI Agents
1. Do **not** change gameplay or simulation logic unless explicitly requested.
2. Keep all systems deterministic; preserve seeded RNG usage and repeatable event ordering.
3. Maintain strict separation between data (`Assets/Game/Data`), logic (`Assets/Game/Scripts`), and UI (`Assets/Game/Scripts/UI`).
4. Never modify `.meta` files manually.
5. Do not import or modify Unity packages unless instructed.
6. Do not run Unity editor or automated Unity tests in this environment.
7. Adhere to existing namespaces under `Game.*`; follow the assembly definition boundaries.
8. Preserve JSON structures exactly when editing data files.
9. Prefer creating new focused files over bloating existing ones.
10. Avoid introducing circular dependencies; systems must remain decoupled via the `EventBus` and registry.
11. Respect `SystemRegistry` initialization ordering and dependency declarations.

## Recommended Workflow for Agents
- Perform structural refactoring before introducing new behaviour; align with modular `IGameSystem` patterns.
- Touch only the systems requested; avoid collateral edits across unrelated modules.
- Keep changes scoped and files small to ease code review and maintain determinism.
- Leverage dependency injection via constructors/descriptors already used throughout the project.
- Follow the existing event-driven architecture: publish through `EventBus`, subscribe minimally, and avoid direct cross-system coupling unless already established.
- Maintain developer tooling (`Logger`, debug overlay) separately from core simulation.

## Version
This agents.md file is automatically regenerated by Codex to reflect the latest project state.
Last updated: 2025-02-14
