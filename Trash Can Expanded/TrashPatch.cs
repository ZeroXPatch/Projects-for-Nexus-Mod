using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using System;
using xTile.Dimensions; // REQUIRED for the Location type

namespace TrashCanExpanded
{
    public static class TrashPatch
    {
        public static void Apply(Harmony harmony, IMonitor monitor)
        {
            try
            {
                // Hook into GameLocation.performAction
                harmony.Patch(
                    original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.performAction),
                        new Type[] { typeof(string), typeof(Farmer), typeof(Location) }),
                    postfix: new HarmonyMethod(typeof(TrashPatch), nameof(Postfix))
                );
            }
            catch (Exception ex)
            {
                monitor.Log($"Failed to patch GameLocation.performAction: {ex}", LogLevel.Error);
            }
        }

        // We capture 'tileLocation' here so we can pass it to the mod logic
        public static void Postfix(GameLocation __instance, string fullActionString, Location tileLocation, bool __result)
        {
            // 1. Check if game reported success (__result == true)
            // 2. Check if it was a Garbage action
            if (__result && !string.IsNullOrEmpty(fullActionString) && fullActionString.StartsWith("Garbage"))
            {
                try
                {
                    // Convert xTile.Location to Vector2
                    Vector2 coords = new Vector2(tileLocation.X, tileLocation.Y);

                    // Pass the Map and Coordinates to ModEntry
                    // It will handle the "One Per Day" check internally.
                    ModEntry.Instance.OnTrashRummaged(Game1.player, __instance, coords);
                }
                catch (Exception ex)
                {
                    ModEntry.Instance.Monitor.Log($"Error giving trash item: {ex}", LogLevel.Error);
                }
            }
        }
    }
}