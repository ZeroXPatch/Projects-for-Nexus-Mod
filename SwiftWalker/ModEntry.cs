using System;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace SwiftWalker
{
    public class ModEntry : Mod
    {
        public override void Entry(IModHelper helper)
        {
            var harmony = new Harmony(this.ModManifest.UniqueID);

            // Patch the Farmer.getMovementSpeed method
            harmony.Patch(
                original: AccessTools.Method(typeof(Farmer), nameof(Farmer.getMovementSpeed)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(Postfix_GetMovementSpeed))
            );
        }

        /// <summary>
        /// Postfix to add back the speed that was subtracted by grass/crops.
        /// </summary>
        private static void Postfix_GetMovementSpeed(Farmer __instance, ref float __result)
        {
            // If riding a horse, the game already ignores crops/grass, so do nothing.
            if (__instance.isRidingHorse())
                return;

            // Check if the farmer is currently standing in a crop or grass
            if (IsSlowedByTerrain(__instance))
            {
                // 1. Determine the penalty the vanilla game just applied.
                // The base penalty for crops/grass is 1.0f (speed reduction).
                float penalty = 1.0f;

                // 2. Check for the "Ol' Slitherlegs" book (ID: Book_Grass).
                // If the player has this book, the vanilla game only applies ~33% of the penalty (0.33f).
                // We check the stats to see if the book is active.
                if (__instance.stats.Get("Book_Grass") != 0)
                {
                    penalty = 0.33f;
                }

                // 3. Add the penalty back to the result to neutralize it.
                // Formula: (CurrentSpeed) + Penalty = OriginalSpeed (100% restoration)
                __result += penalty;
            }
        }

        /// <summary>
        /// Helper to detect if the farmer is on a tile that causes slowdown.
        /// </summary>
        private static bool IsSlowedByTerrain(Farmer farmer)
        {
            GameLocation loc = farmer.currentLocation;
            if (loc == null) return false;

            Vector2 tile = farmer.Tile;

            // Check if there is a TerrainFeature at the player's position
            if (loc.terrainFeatures.TryGetValue(tile, out TerrainFeature? feature))
            {
                // Check for Crops
                // Crops live inside "HoeDirt" features.
                if (feature is HoeDirt dirt && dirt.crop != null)
                {
                    return true;
                }

                // Check for Grass
                if (feature is Grass)
                {
                    return true;
                }
            }

            return false;
        }
    }
}