using HarmonyLib;
using StardewValley;
using StardewValley.Monsters;
using StardewValley.Characters;
using StardewValley.Menus;
using Microsoft.Xna.Framework;

namespace BackgroundTickThrottler
{
    public static class NPCPatch
    {
        // Debug tracking variables
        private static int _totalUpdateAttempts = 0;
        private static int _skippedUpdates = 0;
        private static int _allowedUpdates = 0;
        private static int _sameLocationUpdates = 0;
        private static int _eventUpdates = 0;
        private static int _festivalDayUpdates = 0;  // NEW - tracks pre-festival hours
        private static int _villagerForceUpdates = 0;
        private static int _monsterUpdates = 0;
        private static int _animalUpdates = 0;
        private static int _horseUpdates = 0;
        private static int _junimoUpdates = 0;
        private static int _dialogueUpdates = 0;

        public static bool Prefix(NPC __instance, GameTime time, GameLocation location)
        {
            // Track every update attempt for debug
            if (ModEntry.Config.EnableDebug)
            {
                _totalUpdateAttempts++;
            }

            // ====================================================================
            // CRITICAL SAFETY CHECKS - ALWAYS UPDATE THESE NPCs
            // ====================================================================

            // 1. Mod disabled or Interval is 1 (standard behavior)
            if (!ModEntry.Config.Enabled || ModEntry.Config.UpdateInterval <= 1)
            {
                if (ModEntry.Config.EnableDebug) _allowedUpdates++;
                return true;
            }

            // 2. MONSTERS - ALWAYS update for proper combat
            if (__instance is Monster)
            {
                if (ModEntry.Config.EnableDebug) _monsterUpdates++;
                return true;
            }

            // 3. HORSES - ALWAYS update for smooth riding
            if (__instance is Horse)
            {
                if (ModEntry.Config.EnableDebug) _horseUpdates++;
                return true;
            }

            // 4. PETS - ALWAYS update
            if (__instance is Pet)
            {
                if (ModEntry.Config.EnableDebug) _animalUpdates++;
                return true;
            }

            // 5. FARM ANIMALS - ALWAYS update for proper production
            if (__instance is FarmAnimal)
            {
                if (ModEntry.Config.EnableDebug) _animalUpdates++;
                return true;
            }

            // 6. JUNIMOS - ALWAYS update (Community Center functionality)
            if (__instance is Junimo)
            {
                if (ModEntry.Config.EnableDebug) _junimoUpdates++;
                return true;
            }

            // 7. Same location as player - ALWAYS update
            if (location == Game1.currentLocation)
            {
                if (ModEntry.Config.EnableDebug) _sameLocationUpdates++;
                return true;
            }

            // 8. FESTIVAL DAYS - ALWAYS update
            // CRITICAL FIX: Check both active festivals AND festival days
            // This prevents NPCs from being throttled on festival days before the event starts
            // (e.g., Moonlight Jelly festival starts at 10 PM, but shops need to open at 9 AM)
            bool isFestivalDay = Utility.isFestivalDay(Game1.dayOfMonth, Game1.season);
            if (Game1.eventUp || Game1.isFestival() || isFestivalDay)
            {
                if (ModEntry.Config.EnableDebug)
                {
                    // Track separately: festival day vs active festival
                    if (isFestivalDay && !Game1.isFestival())
                        _festivalDayUpdates++;  // Day of festival, but event hasn't started
                    else
                        _eventUpdates++;  // Active event/cutscene/festival
                }
                return true;
            }

            // 9. Optional: Force update villagers (user preference)
            if (ModEntry.Config.AlwaysUpdateVillagers && __instance.isVillager())
            {
                if (ModEntry.Config.EnableDebug) _villagerForceUpdates++;
                return true;
            }

            // 10. NPCs with active dialogue
            if (Game1.activeClickableMenu is DialogueBox dialogueBox &&
                dialogueBox.characterDialogue?.speaker == __instance)
            {
                if (ModEntry.Config.EnableDebug) _dialogueUpdates++;
                return true;
            }

            // ====================================================================
            // THROTTLING LOGIC - Only applied to background NPCs
            // ====================================================================

            long currentTick = Game1.ticks;
            bool shouldUpdate = (currentTick + __instance.id) % ModEntry.Config.UpdateInterval == 0;

            if (shouldUpdate)
            {
                if (ModEntry.Config.EnableDebug) _allowedUpdates++;
                return true;
            }

            // Skip this update to save CPU
            if (ModEntry.Config.EnableDebug) _skippedUpdates++;
            return false;
        }

        public static void LogDebugInfo()
        {
            if (_totalUpdateAttempts > 0)
            {
                float skipPercentage = ((float)_skippedUpdates / _totalUpdateAttempts) * 100f;
                float expectedReduction = (1f - 1f / ModEntry.Config.UpdateInterval) * 100f;

                ModEntry.SMonitor.Log(
                    $"[BackgroundTickThrottler Debug] Last Second Stats:\n" +
                    $"  Total Update Attempts: {_totalUpdateAttempts}\n" +
                    $"  ├─ Skipped (Throttled): {_skippedUpdates} ({skipPercentage:F1}% saved!)\n" +
                    $"  ├─ Same Location (Always): {_sameLocationUpdates}\n" +
                    $"  ├─ Event/Festival Active (Always): {_eventUpdates}\n" +
                    $"  ├─ Festival Day Pre-Event (Always): {_festivalDayUpdates}\n" +
                    $"  ├─ Villager Force (Always): {_villagerForceUpdates}\n" +
                    $"  ├─ Monsters (Always): {_monsterUpdates}\n" +
                    $"  ├─ Animals/Horses (Always): {_animalUpdates + _horseUpdates}\n" +
                    $"  ├─ Junimos (Always): {_junimoUpdates}\n" +
                    $"  ├─ Active Dialogue (Always): {_dialogueUpdates}\n" +
                    $"  └─ Background Allowed: {_allowedUpdates}\n" +
                    $"  Current Interval: {ModEntry.Config.UpdateInterval}x (Expected ~{expectedReduction:F0}% reduction)\n" +
                    $"  Actual vs Expected: {(skipPercentage >= expectedReduction * 0.8 ? "✓ GOOD" : "⚠ Lower than expected")}",
                    StardewModdingAPI.LogLevel.Info
                );
            }

            // Reset counters
            _totalUpdateAttempts = 0;
            _skippedUpdates = 0;
            _allowedUpdates = 0;
            _sameLocationUpdates = 0;
            _eventUpdates = 0;
            _festivalDayUpdates = 0;
            _villagerForceUpdates = 0;
            _monsterUpdates = 0;
            _animalUpdates = 0;
            _horseUpdates = 0;
            _junimoUpdates = 0;
            _dialogueUpdates = 0;
        }
    }
}