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
        private static int _nearbyDistanceUpdates = 0;
        private static int _eventUpdates = 0;
        private static int _villagerForceUpdates = 0;
        private static int _monsterUpdates = 0;
        private static int _animalUpdates = 0;
        private static int _horseUpdates = 0;
        private static int _junimoUpdates = 0;
        private static int _dialogueUpdates = 0;

        // Render distance in pixels (approximately 20 tiles)
        private const float RENDER_DISTANCE = 1500f;

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
            // Without this, combat becomes broken and unfair
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

            // 4. PETS - ALWAYS update (they're part of the player's home)
            if (__instance is Pet)
            {
                if (ModEntry.Config.EnableDebug) _animalUpdates++;
                return true;
            }

            // 5. FARM ANIMALS - ALWAYS update for proper production and behavior
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
                // Additionally check if NPC is within visible range
                // This prevents throttling NPCs you can actually see
                float distance = Vector2.Distance(Game1.player.Position, __instance.Position);

                if (ModEntry.Config.EnableDebug)
                {
                    if (distance < RENDER_DISTANCE)
                        _nearbyDistanceUpdates++;
                    else
                        _sameLocationUpdates++;
                }

                return true; // Always update if in same location
            }

            // 8. Events/Cutscenes/Festivals - ALWAYS update to prevent soft-locks
            if (Game1.eventUp || Game1.isFestival())
            {
                if (ModEntry.Config.EnableDebug) _eventUpdates++;
                return true;
            }

            // 9. Optional: Force update villagers (user preference)
            if (ModEntry.Config.AlwaysUpdateVillagers && __instance.isVillager())
            {
                if (ModEntry.Config.EnableDebug) _villagerForceUpdates++;
                return true;
            }

            // 10. NPCs with active dialogue or interactions
            // FIXED: Check Game1.activeClickableMenu instead of player.CurrentDialogueBox
            if (Game1.activeClickableMenu is DialogueBox dialogueBox &&
                dialogueBox.characterDialogue?.speaker == __instance)
            {
                if (ModEntry.Config.EnableDebug) _dialogueUpdates++;
                return true;
            }

            // ====================================================================
            // THROTTLING LOGIC - Only applied to background NPCs
            // ====================================================================

            // Use global tick count + NPC ID for distributed load
            // This ensures not all NPCs update on the same frame
            long currentTick = Game1.ticks;
            bool shouldUpdate = (currentTick + __instance.id) % ModEntry.Config.UpdateInterval == 0;

            if (shouldUpdate)
            {
                if (ModEntry.Config.EnableDebug) _allowedUpdates++;
                return true; // Run the original update
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
                    $"  ├─ Nearby/Visible (Always): {_nearbyDistanceUpdates}\n" +
                    $"  ├─ Event/Festival (Always): {_eventUpdates}\n" +
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
            _nearbyDistanceUpdates = 0;
            _eventUpdates = 0;
            _villagerForceUpdates = 0;
            _monsterUpdates = 0;
            _animalUpdates = 0;
            _horseUpdates = 0;
            _junimoUpdates = 0;
            _dialogueUpdates = 0;
        }
    }
}