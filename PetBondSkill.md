# Pet Bond Skill (SpaceCore Design)

A skill for **Stardew Valley** built on SpaceCore that levels your bond with the farmer's pet. The player earns XP by caring for and working with the pet; the pet remains save-compatible with vanilla data.

## XP Sources

Balanced values so normal daily care progresses the skill without grind.

| Action | XP | Notes |
| --- | --- | --- |
| Pet your pet (1×/day) | +10 | Only first pet grants XP. |
| Fill water bowl | +5 | Pet must be able to path to bowl. |
| Give a treat item | +15 | Cap 2/day for XP; treat can be a dedicated object ID or a tagged cooked dish/sweet. |
| Pet outside for 6:00–18:00 | +10 | Pet must be outdoors for most of the day. |
| Pet finds a forage item | +8 | Triggered by Trail Scout branch jobs. |
| Pet scares off crow/wild animal | +6 | Counts one per encounter. |
| Pet finds a lost object (stone/coal/loot) | +10 | Triggered by Trail Scout jobs. |
| Reach 1 heart with pet | +20 | One-time milestone. |
| Reach 3 hearts with pet | +40 | One-time milestone. |
| Reach 5 hearts with pet | +80 | One-time milestone. |

## XP Curve

A soft curve aligned with SpaceCore skill expectations.

| Level | Total XP |
| --- | --- |
| 1 | 0 |
| 2 | 100 |
| 3 | 380 |
| 4 | 770 |
| 5 | 1,300 |
| 6 | 2,150 |
| 7 | 3,250 |
| 8 | 4,600 |
| 9 | 6,200 |
| 10 | 8,000 |

- Level 5 is reachable in ~1–1.5 seasons with daily care.
- Level 10 is reachable around Year 2 with consistent interaction.

## Passive Bonuses by Level

Always-on effects that reward keeping the pet around without being mandatory.

- **Level 1:** 5% morning chance for a `Comforted` buff (+1 Luck, 2h) if the pet slept in its bed/bowl area.
- **Level 2:** Slight pet speed/pathing boost; 2% chance to alert when crows appear (flavor popup only).
- **Level 3:** Standing near the pet for 2 in-game hours total grants `Emotional Support` (+1 Defense or +10 Max Energy) until evening.
- **Level 4:** Pet protection aura: -5% damage taken (or reduced monster aggro if pet is on Guard duty).
- **Level 5:** Unlock profession choice #1.
- **Level 6:** Morning chance for the pet to bring small flavor items (Fiber/Wood/mixed seeds/forage) if ≥2 hearts and same house.
- **Level 7:** Pet-related buffs last 25% longer; small extra friendship when talking to villagers near the pet.
- **Level 8:** Rainy day with full bowl grants `Cozy Morning` (+1 Farming, +1 Luck until noon).
- **Level 9:** Once per day, interacting with the pet removes one negative debuff (e.g., sluggishness, -Speed from exhaustion).
- **Level 10:** Unlock profession choice #2.

## Profession Choices

### Level 5 Perks

- **Trail Scout (Exploration)**
  - 15% morning chance for the pet to generate one forage item near the farmhouse (quality scales slightly with skill).
  - After petting outdoors, +1 Foraging buff for the day (or a few hours).
  - Pet occasionally paths to nearby forage/berry bushes and signals with a bark/meow.

- **Home Guardian (Defense)**
  - +1 Defense while on the farm when the pet is outside.
  - Small nightly chance to remove 1–2 debris tiles (weed/stone/branch) on the farm.
  - Slightly lowers crow crop losses near the farmhouse (weak scarecrow effect).

### Level 10 Perks (Branching)

If **Trail Scout** was chosen:

- **Pathfinder**
  - Daily forage chance becomes 30% and can roll higher-quality forage plus minerals (Quartz; rare Earth Crystal/Geode).
  - Opening the map on the farm shows highlighted tiles where the pet found items that day.

- **Treasure Sniffer**
  - Daily forage chance set to 20%, but drops can include Geodes/Omni Geodes, rare artifacts, and rare seeds (Ancient Seed extremely rare, gated by year/luck).
  - First pet interaction before entering Mines/Skull Cavern grants +1 Luck and +1 Mining for the day.

If **Home Guardian** was chosen:

- **Warden's Companion**
  - Farm Defense bonus becomes +2.
  - -10% damage taken from monsters on the first 5 floors of any dungeon entered that day.
  - Rare nightly event where the pet fends off wildlife and leaves a small gift (Hardwood/Coal/Refined Quartz).

- **Farm Sentinel**
  - Nightly cleanup improves: guaranteed 2–4 debris removed on the farm.
  - Pet fully prevents crows near the farmhouse (dynamic scarecrow radius).
  - On stormy days, modestly lowers lightning crop losses in a small radius.

## Implementation Notes for SpaceCore

- **Skill ID:** `petbond` (or similar). Register via SpaceCore skill API with the XP thresholds above.
- **Event hooks:**
  - `DayStarted` for daily buffs, forage rolls, cleanup, and bowl checks.
  - `UpdateTicked`/`OneSecondUpdateTicked` for proximity buffs and alert popups.
  - `LocationChanged`/`ReturnedToTitle` for resetting daily flags (forage roll, debuff cleanse).
  - `GameLocation` monster-spawn/aggro hooks for defensive aura and dungeon damage reduction.
- **Buff plumbing:** Use SpaceCore/Json Assets buff framework (or SDV `Buff`) for Comforted/Cozy Morning/Emotional Support/Mining+Luck buffs.
- **Treat detection:** Tag items via Data/Objects, a custom category, or a `HasContextTag` check for sweets/cooked dishes.
- **Heart checks:** Use pet friendship points (scaled to hearts) when determining forage quality or gift tables.
- **Localization:** Expose strings for skill name, descriptions, perk names, and buff text via `i18n` keys.
- **Save safety:** The pet remains the vanilla instance; only player skill data is stored via SpaceCore, keeping saves compatible.

## Balancing Guidelines

- Keep forage drops roughly on par with a modest Foraging skill day; treasure table should have low artifact/Ancient Seed odds and may be year-gated.
- Defensive bonuses stay small (+1–2 Defense, -5% damage) to avoid overshadowing combat gear.
- Make debris cleanup limited to the farmhouse area to preserve the need for tools and normal farm maintenance.
- Consider a pet "tired" flag to throttle repeated job triggers in a single day.
