# Immersive Bath Mod - Update Summary

## Changes Made

### 1. Translation Support for Items
**Fixed:** Soap and Bath Sponge now use translatable strings from `default.json`

**Changes:**
- Updated `default.json`:
  - Changed `"item.name"` → `"item.sponge.name"`
  - Changed `"item.description"` → `"item.sponge.description"`
  - Soap name and description remain as `"item.soap.name"` and `"item.soap.description"`

- Updated `ModEntry.cs`:
  - Bath Sponge now uses `Helper.Translation.Get("item.sponge.name")` and `Helper.Translation.Get("item.sponge.description")`
  - Soap already used proper translation keys (no changes needed)

**Translation files:** You can now create language-specific versions in the mod's i18n folder (e.g., `i18n/es.json`, `i18n/zh.json`) with these keys.

---

### 2. Dirty Friendship Penalty with Emote & Debuff Icon
**Added:** NPCs react negatively when you talk to them while dirty (below 40% cleanliness)

**Changes:**
- Added `DirtyFriendshipPenalty` config option (default: -5)
- Added config translation: `"config.dirty-friendship.name": "Friendship Loss (Dirty)"`
- Updated `OnMenuChanged()` method:
  - When talking to NPCs while Clean: gain friendship (existing behavior)
  - When talking to NPCs while Dirty: lose friendship AND NPC shows angry emote (ID 12)
- Added new "Unclean" buff that displays when dirty (below 40%):
  - Shows custom sick emote icon in buff tray (16x16 pixels)
  - Buff name: "Unclean"
  - Description: "People find you off-putting. Friendship decreases when talking to NPCs."

**Configuration:**
- Min: -50, Max: 0 (negative values only)
- Set to 0 to disable the penalty
- Appears in "Dirty Buff (0-40%)" section of Generic Mod Config Menu

**NPC Emote:** Uses emote ID 12 (angry/frustrated animation with scribbles above head)

**Buff Icon:** Requires `sick_emote.png` (16x16 PNG) in assets folder.

---

### 3. Clean Buff with Heart Icon
**Added:** Custom heart buff icon for the Clean state (80-100% cleanliness)

**Changes:**
- Clean buff now displays custom heart icon (16x16 pixels)
- Added buff description: "You're fresh and clean! NPCs appreciate your hygiene. Friendship increases when talking to people."
- Icon shows in buff tray alongside luck bonus

**Buff Icon:** Uses provided `heart_buff.png` (16x16 PNG)

---

### 4. Water Slosh Sound Effect
**Added:** Vanilla "waterSlosh" sound plays during bath transition with configurable volume

**Changes:**
- When taking a bath, the game plays Stardew Valley's built-in "waterSlosh" sound effect
- Sound plays during the black screen fade transition
- Uses vanilla sound - no custom audio files needed
- Adds immersion to the bathing experience
- **Volume is now configurable** in the config menu

**Configuration:**
- `BathSoundVolume` option in "General Settings"
- Range: 0.0 (silent) to 2.0 (double volume)
- Default: 1.0 (normal volume)
- Interval: 0.1 for fine-tuning
- Set to 0.0 to disable the sound completely

**Sound:** Uses the game's existing "waterSlosh" audio cue

---

### 5. Bath Anywhere Option
**Added:** Optional config to allow bathing anywhere without needing a water source

**Changes:**
- Added `BathAnywhere` config option (default: false/off)
- Added new "General Settings" section in config menu
- Added config translations:
  - `"config.section.general": "General Settings"`
  - `"config.bath-anywhere.name": "Allow Bath Anywhere"`
  - `"config.bath-anywhere.tooltip": "Allow bathing anywhere instead of requiring water source"`

- Updated `OnButtonPressed()` method:
  - Now checks `Config.BathAnywhere || IsNearWater(Game1.player)`
  - If enabled, you can bathe anywhere on the map
  - If disabled (default), requires water source as before

**Configuration:**
- Boolean toggle (checkbox in Generic Mod Config Menu)
- Located in new "General Settings" section at the top
- Default: OFF (maintains original behavior)

---

## Files Modified

1. **default.json** - Translation strings (including buff descriptions)
2. **ModConfig.cs** - Configuration properties
3. **ModEntry.cs** - Main mod logic (added sound effect, buff icons, and features)

## New Assets Required

### 1. heart_buff.png (✓ Provided)
- **Purpose**: Custom buff icon for Clean state (80-100% cleanliness)
- **Size**: 16 x 16 pixels
- **Format**: PNG with transparency
- **Location**: `assets/heart_buff.png`
- **Status**: ✓ Included in download

### 2. sick_emote.png (Needs to be created)
- **Purpose**: Custom buff icon for Unclean debuff (below 40% cleanliness)
- **Size**: 16 x 16 pixels
- **Format**: PNG with transparency
- **Location**: `assets/sick_emote.png`
- **See**: **BUFF_ICON_INSTRUCTIONS.md** for how to create this from the Stardew Valley wiki emote

---

## Installation

1. Replace these files in your mod folder:
   - `ImmersiveBath/default.json`
   - `ImmersiveBath/ModConfig.cs`
   - `ImmersiveBath/ModEntry.cs`

2. Add the new assets:
   - `ImmersiveBath/assets/heart_buff.png` ✓ (provided)
   - `ImmersiveBath/assets/sick_emote.png` (follow BUFF_ICON_INSTRUCTIONS.md)

3. Recompile the mod or copy the compiled DLL to your Stardew Valley Mods folder

---

## CRITICAL: Buff Icon Size

⚠️ **Both buff icons MUST be exactly 16x16 pixels!**

The emotes from the Stardew Valley wiki are 56x56 pixels and will appear too large if not resized. Follow the instructions in BUFF_ICON_INSTRUCTIONS.md to properly resize the sick emote to 16x16 pixels.

---

## Compatibility

- Requires SMAPI 4.0.0+
- Compatible with Generic Mod Config Menu (optional but recommended)
- New assets required: 
  - heart_buff.png (16x16 PNG) ✓
  - sick_emote.png (16x16 PNG)
- Uses vanilla Stardew Valley sound effects (no custom audio needed)

---

## Testing Checklist

- [ ] Soap and Sponge display translated names/descriptions
- [ ] Clean buff (80-100%) shows heart icon in buff tray
- [ ] Clean buff description mentions friendship bonus
- [ ] Talking to NPCs while Clean gives friendship bonus
- [ ] Talking to NPCs while Dirty (below 40%) applies penalty and shows angry emote (ID 12)
- [ ] "Unclean" debuff appears in buff tray when dirty with sick emote icon (16x16)
- [ ] Buff icons are correct size (not covering other UI elements)
- [ ] Water slosh sound plays during bath black screen transition
- [ ] Bath sound volume can be adjusted in config (0.0 to 2.0)
- [ ] Setting volume to 0.0 silences the bath sound
- [ ] Bath Anywhere option works when enabled
- [ ] Water requirement still works when Bath Anywhere is disabled
- [ ] Config menu shows all new options including dirty friendship penalty
- [ ] Translation files can be added for other languages

---

## Buff System Summary

| Cleanliness | Buff Name | Icon | Effects | Description |
|------------|-----------|------|---------|-------------|
| 80-100% | Clean | Heart (16x16) | +Luck, +Friendship on NPC talk | "You're fresh and clean! NPCs appreciate your hygiene." |
| 40-79% | Neutral | None | None | "You feel okay." |
| 0-39% | Dirty | None | +Speed, +Defense, +Attack | "You feel worked up and dirty!" |
| 0-39% | Unclean | Sick emote (16x16) | -Friendship on NPC talk | "People find you off-putting. Friendship decreases when talking to NPCs." |

**Note:** When dirty (below 40%), you'll have BOTH the "Dirty" buff (combat bonuses) AND the "Unclean" debuff (friendship penalty with icon).

---

## Sound Effects Used

The mod uses the following vanilla Stardew Valley sound cues:
- **"waterSlosh"** - Plays during bathing transition for immersion

No custom audio files are required!
