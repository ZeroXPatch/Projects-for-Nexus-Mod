# Pet Bond Mod Blueprint (SpaceCore + SMAPI)

A full implementation plan for a **Pet Bond** custom skill and pet-behavior overhaul using SpaceCore. This blueprint translates the design in `PetBondSkill.md` into concrete game content, optional assets, and code modules ready for SMAPI development.

## Gameplay Overview
- **Skill:** Custom SpaceCore skill `petbond` that levels by caring for your pet and letting it work on the farm.
- **Progression:** XP gains and level curve from `PetBondSkill.md`, with profession choices at levels 5 and 10.
- **Pet Jobs:** Contextual actions (forage runs, crow deterrence, debris cleanup, dungeon morale) gated by profession picks and daily stamina.
- **Buffs:** Comforted, Emotional Support, Cozy Morning, Mining Luck, and profession auras delivered as standard SDV `Buff` entries with `i18n` text.
- **Save Safety:** No new pet entity; all state is per-player SpaceCore skill data plus daily runtime flags.

## Content & Assets
- **Icons:** Skill icon (48×48), four profession icons (Trail Scout, Home Guardian, Pathfinder, Treasure Sniffer, Warden's Companion, Farm Sentinel), and a pet bowl icon for UI prompts.
- **Strings (i18n):** Skill name/desc, profession names/descs, buff names/descs, mailbox letters, config display strings, and mod menu labels.
- **Data Files:**
  - `i18n/<locale>.json` for text.
  - Optional `assets/mail/*.txt` for event letters (unlock notices, pet gift letters).
  - Optional `assets/icons/*.png` for UI (runtime-generated placeholders are used by default so no binaries are required).
  - `manifest.json` (standard SMAPI) and optional `config.json` defaults.

## Systems Breakdown
### 1) XP & Leveling
- **Sources:** Petting, filling bowl, treats (capped daily), pet outside duration, job completions, heart milestones (see `PetBondSkill.md`).
- **Implementation:**
  - Subscribe to `GameLoop.DayStarted`, `GameLoop.OneSecondUpdateTicked`, and `Input.ButtonReleased` (for petting interaction).
  - Track per-day flags: `hasPetted`, `treatsGiven`, `bowlFilled`, `petOutsideHours`, `jobsCompleted`.
  - Use SpaceCore `AddExperience` with XP values; guard with daily caps.

### 2) Buff Framework
- Define helper `BuffManager` to create/apply/remove buffs by string ID.
- Buffs: `Comforted`, `EmotionalSupport`, `CozyMorning`, `MiningLuck`, `FarmDefense` (for professions).
- Expiration tied to in-game time; use `GameLoop.UpdateTicked` to decrement or rely on vanilla buff timer.

### 3) Passive Effects by Level
- Morning comfort roll, pet pathing speed multiplier, proximity buff, damage reduction, longer buff duration, villager friendship bonus, debuff cleanse, etc.
- Most handled in `DayStarted` (rolls), `UpdateTicked` (proximity/duration), and `GameLocation` combat hooks (damage reduction/aggro tweak).
- Use `Farmer.team.sharedDailyLuck` and `farmer.resilience` adjustments carefully to avoid stacking bugs.

### 4) Profession Trees
- **Level 5:** `TrailScout` vs `HomeGuardian`.
- **Level 10 (branching):** `Pathfinder`/`TreasureSniffer` under Trail Scout; `WardensCompanion`/`FarmSentinel` under Home Guardian.
- Profession data registered via SpaceCore profession API with IDs `petbond_trailscout`, `petbond_homeguardian`, `petbond_pathfinder`, `petbond_treasuresniffer`, `petbond_wardenscompanion`, `petbond_farmsentinel`.

### 5) Pet Jobs & AI Helpers
- **Job Scheduler:** At `DayStarted`, roll eligibility based on profession, pet hearts, and stamina budget (e.g., 2 jobs/day). Store planned jobs in a queue.
- **Forage Runs:** Choose farm/forage tiles near farmhouse; spawn item with quality scaling on skill + hearts. Pathfinder perk draws markers on map UI (mini-map overlay or letter).
- **Treasure Rolls:** Treasure Sniffer replaces forage table with geodes/artifacts/rare seeds (with year/luck gates).
- **Guard Duty:** Home Guardian grants farm defense aura; Farm Sentinel upgrades crow deterrence + debris cleanup; Wardens Companion adds dungeon damage reduction and rare gift events.
- **Cleanup:** Nightly cleanup in `DayEnding` to remove debris tiles within a farmhouse radius.
- **Crow Deterrence:** During `Farm.animalsOnFarmUpdate`, inject temporary scarecrow radius check near house when professions active.

### 6) Event Hooks
- `DayStarted`: reset flags, roll comfort buff, roll forage/treasure job, roll cleanup from prior night, apply rainy-day Cozy Morning buff, set pet speed multiplier.
- `TimeChanged`: track outdoor hours for XP; trigger Cozy Morning expiration at noon.
- `OneSecondUpdateTicked`: check farmer proximity for Emotional Support buff; handle debuff cleanse availability; update pet pathing goal hints.
- `MenuChanged`: detect map open for Pathfinder highlights.
- `DayEnding`: apply debris cleanup and crow deterrence events; enqueue mail if gift triggered.

### 7) Data Structures
- `PerPlayerState` (stored via SpaceCore/ModData):
  - `Daily`: `petted`, `treats`, `bowlFilled`, `outsideMinutes`, `jobsDone`, `debuffCleared`, `buffsActive`.
  - `Persistent`: unlocked mail flags, tutorial shown, config overrides.
- `JobDefinition`: type (`Forage`, `Treasure`, `Cleanup`, `CrowPatrol`, `DungeonMorale`), `chance`, `minLevel`, `professionReq`, `lootTableId`.
- `LootTables`: JSON-driven entries with weight, min/max stack, quality rules, and conditions (year, hearts, luck, location).

### 8) Config Options
- Toggle skill XP gain multipliers.
- Enable/disable crow deterrence and debris cleanup (for compatibility with other mods).
- Cap on daily pet jobs (default 2).
- Toggle map highlights for Pathfinder.
- Debug mode to print rolls to SMAPI console.

### 9) Compatibility & Safety
- Works with vanilla pets and multiple pets (e.g., Generic Mod Config Menu allows per-farmer toggles; state remains per-farmer).
- Avoid edits to pet textures/animations to preserve mod compatibility; optional sprite overlays are cosmetic only.
- Guard against null pets on multiplayer farms; host authoritatively spawns items/cleanup to prevent desync.
- Respect other custom skills by namespacing IDs and avoiding global skill list rewrites.

## Implementation Roadmap
1. **Project Setup**
   - Create SMAPI mod scaffold with SpaceCore reference.
   - Register custom skill, XP table, professions, and buffs.
2. **Core Tracking**
   - Implement per-day flag tracking and XP award helpers.
   - Hook petting, bowl fill, treat usage, and outdoor time.
3. **Buff & Passive Systems**
   - Implement Comforted/Cozy Morning/Emotional Support buffs and damage reduction logic.
4. **Professions & Jobs**
   - Add Trail Scout/Home Guardian behaviors; implement branching perks and loot tables.
5. **Content & UX**
   - Add icons, i18n, config UI (GMCM), and optional map highlights.
6. **Polish & Balance**
   - Tune loot weights, debris radius, and buff durations; add mail/tutorial.
7. **Testing**
   - Single-player and multiplayer pet interactions; regression with other farm animals; check save/load stability.

## Testing Plan
- **Unit-style:** verify XP gains per action with debug logs; ensure daily caps respected.
- **Gameplay:** simulate 10-day loops per profession; confirm buffs expire at intended times; validate forage/treasure rolls.
- **Edge Cases:** pet missing or warped indoors, multiplayer host/client divergence, rain/storm interactions, dungeon entry buffs, and crow deterrence with/without scarecrows.

## Immediate Next Steps for a Playable Build
- **Add art + audio polish:** drop in the six profession icons and ensure the skill/profession textures reference the final assets. Procedural placeholders are used by default so the repo stays binary-free.
- **Finalize text:** confirm buff names/descriptions are localized in `i18n` and shown on HUD toasts (Comforted, Cozy Morning, Emotional Support, guardian gifts).
- **Balance numbers:** tune forage/treasure chances, debris cleanup counts, and buff durations with a short in-game playtest per profession branch.
- **Packaging:** create a SMAPI-ready `PetBondMod.dll` via `dotnet build` and bundle it with `manifest.json` and `i18n/` for quick drop into the `Mods` folder.
- **Optional config:** wire up a simple `config.json` (or GMCM) to toggle debris cleanup and adjust XP multipliers so players can opt out of strong perks.

## Deliverables
- SMAPI mod project with source, optional assets, and manifest.
- README with install/config instructions and profession summary (derived from `PetBondSkill.md`).
- Localization-ready strings for all text.
- Balanced loot tables ready for iteration based on playtesting.
