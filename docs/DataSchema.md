# Data Schema

This project keeps gameplay data in JSON files under `Assets/Game/Data/` and configuration classes
that describe how systems interpret those files. The following sections summarize the current
sources and the fields systems expect.

## Base Characters (`base_characters.json`)
* **Location:** `Assets/Game/Data/base_characters.json`
* **Structure:** Root object with a `Characters` array. Each character entry records identity,
  demographic fields, lifecycle flags, trait history, ambition, and civic milestones.【F:Assets/Game/Data/base_characters.json†L1-L60】
* **Usage:** Loaded during `CharacterSystem.Initialize` through `CharacterDataLoader`, then hydrated
  into repositories and services for daily simulation. New fields should default gracefully because
  the loader iterates properties dynamically before handing them to repositories.【F:Assets/Game/Scripts/Characters/CharacterSystem.cs†L39-L92】
* **Extensibility:** Additive changes are preferred. Optional arrays (e.g., `TraitRecords`,
  `CareerMilestones`) can be empty; null values are tolerated for unknown relatives.

## Magistrate Offices (`BaseOffices.json`)
* **Location:** `Assets/Game/Data/BaseOffices.json`
* **Structure:** Root `Offices` array with definitions that include age requirements, assembly,
  seat counts, reelection gaps, and prerequisite office IDs.【F:Assets/Game/Data/BaseOffices.json†L1-L63】
* **Usage:** Parsed by `OfficeSystem` via `IMagistrateOfficeRepository`, which validates seat
  structures and seeds historical holders. Additional attributes should remain optional to avoid
  breaking existing seeds.【F:Assets/Game/Scripts/Systems/Politics/Offices/OfficeSystem.cs†L19-L112】

## Population Simulation Overrides (`population_simulation.json`)
* **Location:** `Assets/Game/Data/population_simulation.json` (optional)
* **Structure:** JSON mirroring `PopulationSimulationConfig` with nested `Birth` and `Marriage`
  objects. Absent files or fields fall back to defaults defined in code.【F:Assets/Game/Scripts/Systems/Population/PopulationSimulationConfig.cs†L7-L86】
* **Usage:** Both Birth and Marriage systems load this file at initialization to override runtime
  configuration without recompiling. Missing sections are reconstructed with default values.【F:Assets/Game/Scripts/Systems/BirthSystem.cs†L55-L99】【F:Assets/Game/Scripts/Systems/MarriageSystem.cs†L55-L99】

## Simulation Defaults (`SimulationConfig`)
* **Location:** `Assets/Game/Scripts/Core/SimulationConfig.cs`
* **Structure:** Serialized Unity object with nested `CharacterSettings`, `BirthSettings`, and
  `MarriageSettings`. Provides baseline RNG seeds, eligibility thresholds, and data paths used by
  the bootstrap profile.【F:Assets/Game/Scripts/Core/SimulationConfig.cs†L5-L69】【F:Assets/Game/Scripts/Core/SystemBootstrapProfile.cs†L35-L75】
* **Usage:** Loaded during bootstrap to configure systems before any external overrides are applied.
  Treat the config as a mutable starting point: runtime overrides (e.g., population JSON) update the
  same instance so systems stay synchronized.【F:Assets/Game/Scripts/Systems/Population/PopulationSimulationConfig.cs†L35-L86】【F:Assets/Game/Scripts/Systems/BirthSystem.cs†L55-L132】

## Event Payloads
Game events inherit from `GameEvent` and may include simulation state snapshots. When adding new
fields, prefer structs or immutable records to make serialization straightforward. Events should
remain lean because the bus keeps an optional history buffer for debugging.【F:Assets/Game/Scripts/Core/GameEvent.cs†L1-L65】【F:Assets/Game/Scripts/Systems/EventBus/EventBus.cs†L15-L111】
