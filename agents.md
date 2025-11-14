# Agent Operating Specification

## 1. High-Level Purpose
Cursus Honorum is a system-driven Roman Republic simulation in Unity. The simulation is modular, event-driven, and deterministic. All gameplay arises from systems, not scripts or story vignettes. Agents must preserve determinism, modularity, data-driven design, separation of concerns, subsystem PDF alignment, and must never add flavor events or emotional content.

## 2. Architectural Overview
### 2.1 System Framework
- All runtime logic is implemented as `IGameSystem` modules derived from `GameSystemBase`.
- Systems are registered, ordered, and lifecycle-managed by `SystemRegistry`.
- Lifecycle phases are `Initialize()`, `Tick(deltaTime)`, and `Shutdown()`; every system must respect this contract.
- Systems communicate only through the global `EventBus`; no direct cross-system calls beyond constructor-injected services provided at registration.

### 2.2 EventBus
- The `EventBus` is the global publish/subscribe hub for the simulation.
- Every major simulation beat (time progression, births, deaths, marriages, office changes, elections) is emitted as a strongly typed event.
- Systems subscribe only to events they actively consume and must unsubscribe on shutdown.
- Event ordering and delivery must remain deterministic; never introduce asynchronous dispatch or unordered handlers.

### 2.3 GameController & GameState
- `GameController` is the Unity entry point that instantiates `GameState`.
- `GameState` constructs the `SystemRegistry`, creates all systems, runs their lifecycle, exposes services to the UI, coordinates save/load, and owns shutdown.
- All runtime system access (including UI) goes through `GameState`; agents must not bypass this orchestration.

### 2.4 Time System
- The time system converts Unity `deltaTime` into deterministic days, months, and years.
- It publishes `OnNewDay`, `OnNewMonth`, and `OnNewYear` events that drive the rest of the simulation.
- Time cadence changes must remain deterministic and preserve downstream expectations.

### 2.5 SaveService
- `SaveService` serializes and restores the complete deterministic game state, including characters, families, births, marriages, office and election status, and time progression.
- Save/load flows rely on stable JSON contracts validated by `SaveSerializer`; compatibility must be preserved across revisions.

## 3. Current Implemented Systems
### Character System
Maintains the authoritative repository of every citizen, tracks lifecycle state, families, ages, mortality, and marriage relationships. Subscribes to time events to advance aging, apply deterministic mortality tables, and publish population metrics (`OnCharacterDied`, `OnPopulationTick`). Provides query services to other systems via `GameState` lookups and persists its state through JSON blobs produced by deterministic RNG sequences.

### Birth System
Schedules pregnancies for eligible married women, resolves births on due dates, and creates new characters via the character repository. Listens to `OnNewDay`, consumes deterministic fertility settings, and publishes `OnCharacterBorn` events. Saves and restores pending pregnancies and RNG state to keep outcomes reproducible.

### Marriage System
Performs daily matchmaking between eligible singles using deterministic weighted selections that respect class constraints. Subscribes to `OnNewDay`, invokes `CharacterSystem.Marry`, and emits `OnCharacterMarried` events. Persists RNG state so marriage outcomes remain stable across save/load cycles.

### Office System
Loads magistracy definitions from `BaseOffices.json`, manages seat availability, term limits, and incumbency history. Reacts to time and character lifecycle events, enforces eligibility rules, and publishes `OfficeAssignedEvent` whenever a seat changes hands. Provides data to elections and politics subsystems while maintaining deterministic office states for saves.

### Election System
Runs annual election cycles by opening seasons, evaluating candidate ambition, drawing winners, and allocating offices. Consumes time events, queries the character and office systems, and publishes `ElectionSeasonOpenedEvent`, `ElectionSeasonCompletedEvent`, and `OfficeAssignedEvent` outcomes. Stores per-year election state and RNG progress in its save data to guarantee repeatable results.

### Politics System
Coordinates high-level political state by tracking eligibility, term histories, and election-cycle summaries. Listens to time, population, election, and office events to refresh eligibility snapshots and maintain deterministic trackers. Exposes read-only snapshots for other consumers (UI, debug) without owning UI logic or direct state mutation outside of event responses.

### Debug Overlay
Unity UI component that queries `GameState` for time, character, office, and election snapshots, then renders diagnostic information. Contains no gameplay logic; only observes systems through adapters and must not change simulation data.

## 4. Systems Required by Design Documents (Not Implemented)
- **Senate System:** Full senate proceedings, sessions, and decrees.
- **Factions:** Persistent political blocs with systemic influence mechanics.
- **Rivalry System:** Structured interpersonal rivalries affecting eligibility and outcomes.
- **Systemic Indices:** Citywide indices (order, unrest, legitimacy) affecting mechanics.
- **Era System:** Era progression gates altering available systems and parameters.
- **Economy / Estates / Wealth:** Wealth, estate ownership, and economic flows.
- **Provinces:** Provincial governance, assignments, and regional data.
- **Military System:** Legions, commands, campaigns, and war resolution.
- **Full Game UI Vision:** Complete production UI beyond the current debug overlay.

Codex must not build any of these systems unless explicitly instructed.

## 5. Agent Modification Rules
### 5.1 Absolute Do-Not-Break
- Event-driven architecture and event contracts.
- `SystemRegistry` creation order and lifecycle management.
- Determinism, seeded randomness, and reproducible simulation results.
- JSON serialization contracts and schema expectations.
- Save/load compatibility managed by `SaveService` and `SaveSerializer`.
- Namespace organization under existing assemblies.
- Assembly definition boundaries and project structure.
- Time-driven cadence emitted by the time system.

### 5.2 Behavioral Rules
- No flavor events, narrative scripting, or bespoke story beats.
- All mechanics must remain systemic and data-driven.
- Keep UI logic outside of systems; keep simulation logic outside of UI.
- Systems may not directly couple to each other; all interaction flows through the `EventBus` and `GameState` accessors.

### 5.3 Editing Rules
- Keep every change tightly scoped to the requested area.
- Prefer adding new files over expanding unrelated ones.
- Never edit `.meta` files.
- Do not modify Unity packages unless explicitly told to do so.
- Leave debug tooling untouched unless the request targets it.

### 5.4 Data Rules
- JSON assets must remain human-editable and formatted for authors.
- Do not change JSON schemas or keys without explicit approval.
- Never embed JSON-equivalent data inside scripts; keep data in data files.

## 6. Workflow Guidance
- Resolve compile errors before adjusting simulation logic.
- Run and pass all available tests or checks before extending features.
- Work in small, reversible patches that maintain determinism at each step.
- Validate every change against the relevant subsystem PDF specification.
- Follow roadmap alignment:
  - Phase 0 – stabilization.
  - Phase 1 – character/family foundation.
  - Phase 2 – politics core.
  - Phase 3+ – senate, factions, rivalries.
  - Phase 4+ – UI.
  - Phase 5–7 – indices, economy, military.
  - Phase 8–10 – AI, integration, polish.

## 7. Versioning Rule
Regenerate this file only when the architecture or the list of implemented systems materially changes.
