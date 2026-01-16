# KSP Capcom

An in-game **CAPCOM-style chat console** for **Kerbal Space Program 1 (Windows)** that gives **craft-aware guidance** in the VAB/SPH and **flight-ops coaching** in orbit — and can generate **grounded, rocket-specific kOS scripts** (LKO ascent, rendezvous, docking helpers) using an **offline kOS docs index**.

**Chat-first. Teach-or-Do. No feature sprawl. No autopilot (this mod does not fly your ship).**

---

## Why this exists

KSP is amazing—and also famously easy to get stuck:
- orbital mechanics are hard to internalize mid-flight
- craft design mistakes are expensive to learn by trial and error
- rendezvous/docking are where a lot of runs die
- kOS is powerful, but writing correct scripts requires digging through docs while you’re trying to play

KSP Capcom brings help **into the game**, in the moment, in a consistent “Mission Control” voice.

---

## Project Status

🚧 **Early prototype / planned**  
Most features below are planned. The initial goal is a minimal mod that:
1) loads in-game  
2) shows a chat panel  
3) returns a basic “echo / hello world” response  
4) supports an easy dev workflow that auto-copies builds to `GameData/`

---

## Core Principles

- **Chat-first UX**: everything is available through conversation; buttons are shortcuts into chat workflows.
- **Context-aware**: uses craft snapshot in VAB/SPH and vessel/orbit snapshot in flight.
- **Grounded kOS scripting**: scripts are generated using retrieved kOS docs snippets to avoid hallucinated APIs/suffixes.
- **Teach vs Do**:
  - **Teach Mode**: explanations + mental models + step-by-step coaching
  - **Do Mode**: concise steps + checklists + script output
- **Fail gracefully**: timeouts/offline states show readable messages; no hard freezes.

---

## What it does

### ✅ Planned MVP (first shippable slice)
- In-game **chat panel** (dockable/resizable)
- Basic **“echo / hello world”** responder (no network)
- Clear **error UI** (timeouts/offline) once networking is added
- Dev workflow: **build → auto-copy to KSP `GameData/`**

### ✅ Planned: LLM-backed chat
- Pluggable LLM endpoint (local or cloud)
- Streaming or chunked responses
- Stop/cancel generation
- CAPCOM + light Kerbal voice prompt

### ✅ Planned: VAB/SPH Craft Awareness
- Editor craft snapshot (parts/resources/staging summary)
- Derived “readiness” metrics (examples):
  - rough launch TWR
  - stage/total delta-V estimates (where feasible)
  - basic control authority checks (gimbal/reaction wheels/fins)
  - staging sanity warnings
- **CAPCOM Readiness Panel** shown next to chat

### ✅ Planned: kOS “Grounded Mode” + Offline Docs Index
- Bundle an **offline index** of kOS documentation
- Retrieval tool that injects relevant snippets into prompts
- Optional “References” panel showing which docs sections were used

### ✅ Planned Hero Buttons (Chat Quick Actions)
These are *not separate systems*—they’re “intent buttons” that gather context and ask Capcom to produce an answer/script.

#### VAB/SPH
- **Generate LKO Ascent Script (for this rocket)**
  - Profiles: Safe / Efficient / Heavy Lifter
  - Output: script + config block + assumptions + short explanation
- **Design Critique**
  - Top issues + suggested improvements grounded in craft metrics

#### Flight
- **Circularize at Ap/Pe**
- **Deorbit / Reentry checklist**
- **Transfer to Mun** (plan + optional kOS script)
- **Rendezvous Wizard**
  - Mission “cards”: match planes → phase/intercept → close approach → kill relative velocity
- **Docking helpers**
  - Port alignment guidance + optional kOS script generation

---

## Non-Goals (Anti-Goals)

- ❌ This mod is **not an autopilot** and does not directly pilot your craft.
- ❌ No sprawling “do everything” feature set.
- ❌ No replacement for kOS / MechJeb / other flight-control mods.
- ✅ It can **generate kOS scripts** that *you choose* to run.

---

## Roadmap (high-level)

### Milestone 0 — Bootstrap + Auto-Deploy to GameData
- Repo structure
- Build output goes to `GameData/KSPCapcom/Plugins/`
- Script/tooling to copy build into your KSP install automatically

### Milestone 1 — Chat Panel + Echo Responder
- Open/close UI
- Send message → receive “CAPCOM echo”

### Milestone 2 — LLM Integration + Reliability
- Endpoint config
- Timeouts/cancel/retries
- Teach vs Do mode

### Milestone 3 — VAB Craft Snapshot + Readiness Panel
- Craft scan
- Derived metrics + basic critique

### Milestone 4 — Offline kOS Docs Index + Grounded Mode
- Docs ingestion/indexing
- Retrieval at runtime
- Script references

### Milestone 5 — “Generate LKO Ascent Script”
- VAB quick action
- Script viewer/editor + save-to-kOS archive path

### Milestone 6 — Flight Snapshot + Orbit Ops Helpers
- Orbit-aware guidance
- Contextual quick actions

### Milestone 7 — Rendezvous Wizard + Docking Helpers
- Target selection
- Mission cards + kOS scripts per step

---

## Installation (Planned)

This project will be a standard KSP plugin layout:

Kerbal Space Program/GameData/KSPCapcom/Plugins/KSPCapcom.dll
(assets...)


A proper release/install guide will be added once the first DLL is producing UI in-game.

---

## Development (Planned)

Primary dev goals:
- **fast iteration loop**: build → auto-copy to `GameData/` → run KSP
- minimal friction for first-time KSP modding

Planned tooling:
- a small deploy script/tool that:
  - takes your KSP install path
  - deletes old `GameData/KSPCapcom/`
  - copies the fresh build output

---

## Configuration (Planned)

- LLM endpoint URL (local/cloud)
- Model name (if applicable)
- Teach vs Do default mode
- Grounded mode toggle
- Path to kOS archive folder (optional convenience for saving scripts)

---

## Contributing

Contributions will be welcome once the initial scaffolding is in place. Planned contribution areas:
- UI/UX polish (chat panel, script viewer)
- craft/orbit snapshot data contracts
- deterministic math helpers (TWR/dV/burn timing)
- kOS docs ingestion/indexing
- reliability improvements (timeouts, caching, retries)

---

## License

TBD.

---

## Acknowledgements

- Squad / KSP community modders for keeping KSP1 thriving
- kOS community for the scripting ecosystem and documentation

---

