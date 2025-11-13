# 🏛️ Cursus Honorum — Roman Republic Simulation

![Unity](https://img.shields.io/badge/Engine-Unity-blue?logo=unity)
![C#](https://img.shields.io/badge/Language-C%23-green?logo=csharp)
![License](https://img.shields.io/badge/License-MIT-yellow)
![Status](https://img.shields.io/badge/Build-Prototype-orange)

A modular, data-driven simulation of **Roman family, social, and political life** — inspired by *Crusader Kings* and *Democracy 4*.  
This prototype focuses on **generational characters**, **daily events**, and **emergent social dynamics** within the Roman Republic.

---

## ⚙️ Overview

The simulation models the **life cycles and lineages** of Roman citizens in a pulsed real-time framework.  
Each in-game day triggers births, deaths, and marriages — all coordinated through modular systems communicating via a global **EventBus**.

---

## 🧩 Core Architecture

Cursus Honorum is built on a **modular system framework**.  
Each system inherits from a shared `GameSystemBase` and communicates through the event-driven **EventBus**, ensuring decoupled and maintainable logic.

| System | Purpose |
|--------|----------|
| `GameController` | Unity entry point — initializes and updates the `GameState`. |
| `GameState` | Central coordinator for all game systems. |
| `SystemRegistry` | Manages dependency injection, initialization, and save/load. |
| `Logger` | Provides color-coded logs for debugging. |
| `GameSystemBase` / `IGameSystem` | Base class & interface for all modular subsystems. |

---

## 👥 Character Simulation

| File | Description |
|------|--------------|
| **BaseCharacter.cs** | Defines `Character` — identity, relationships, and core stats. |
| **CharacterFactory.cs** | Loads base JSON data, generates names, and creates new children. |
| **CharacterSystem.cs** | Manages living/dead characters, birthdays, and mortality. |
| **base_characters.json** | Defines the founding population at game start. |
| **RomanName.cs / RomanNamingRules.cs** | Authentic Roman naming conventions by gender and class. |

---

## 💍 Family Systems

| System | Description |
|---------|--------------|
| **MarriageSystem.cs** | Matches eligible singles daily, weighted by class and age. |
| **BirthSystem.cs** | Handles pregnancies, due dates, and births through event triggers. |

---

## ⏳ Time & Events

| System | Description |
|---------|--------------|
| **TimeSystem.cs** | Advances time, publishes `OnNewDay`, `OnNewMonth`, and `OnNewYear` events. |
| **EventBus.cs** | Central event dispatcher that systems subscribe to and publish through. |
| **CharacterEvents.cs** | Defines population lifecycle events (Birth, Death, Marriage, Tick). |

---

## 🧾 UI Layer

| File | Description |
|------|--------------|
| **CharacterLedgerSimple.cs** | Basic Unity UI that lists living characters and auto-refreshes on events. |

---

## 🧠 Simulation Flow

```mermaid
flowchart TD
    A[GameController Start] --> B[GameState Initialize]
    B --> C[Register Core Systems]
    C --> D[TimeSystem Publishes OnNewDay]
    D --> E[CharacterSystem Updates Ages & Mortality]
    D --> F[MarriageSystem Matches Couples]
    D --> G[BirthSystem Resolves Pregnancies]
    E & F & G --> H[EventBus Publishes PopulationTick]
    H --> I[UI Ledger Refreshes]

---

## 🛠 Simulation Configuration

All deterministic simulation knobs now live in `Assets/Game/Data/simulation_config.json`. Update this JSON to tweak seeds, mortality bands, fertility rates, and matchmaking behaviour without recompiling scripts.

### Character settings
| Field | Description |
|-------|-------------|
| `Character.RngSeed` | Seed used by the character lifecycle RNG (aging, mortality). |
| `Character.KeepDeadInMemory` | When `true`, deceased citizens remain cached for history lookups. |
| `Character.BaseDataPath` | Path to the starting population JSON. |
| `Character.Mortality.UseAgeBandHazards` | Enables age-band driven mortality. |
| `Character.Mortality.AgeBands[]` | Inclusive age ranges with yearly hazard rates. Values are converted to daily odds at runtime. |

### Birth settings
| Field | Description |
|-------|-------------|
| `Birth.RngSeed` | RNG seed for pregnancy checks and multiple births. |
| `Birth.FemaleMinAge` / `Birth.FemaleMaxAge` | Eligible age window for mothers. |
| `Birth.DailyBirthChanceIfMarried` | Daily probability that an eligible married couple conceives. |
| `Birth.GestationDays` | Pregnancy length used when scheduling due dates. |
| `Birth.MultipleBirthChance` | Chance for twins after the first child is born. |

### Marriage settings
| Field | Description |
|-------|-------------|
| `Marriage.RngSeed` | RNG seed for matchmaking and weighting rolls. |
| `Marriage.MinAgeMale` / `Marriage.MinAgeFemale` | Minimum ages for bachelors and bachelorettes. |
| `Marriage.DailyMatchmakingCap` | Maximum pair attempts processed each day. |
| `Marriage.DailyMarriageChanceWhenEligible` | Probability that a proposed pair actually marries. |
| `Marriage.PreferSameClassWeight` | Multiplier applied when partners share the same social class. |
| `Marriage.CrossClassAllowed` | When `false`, disallows marriages across social classes. |

> 💡 Tip: Keep `simulation_config.json` under version control so design tweaks stay reproducible across runs.
