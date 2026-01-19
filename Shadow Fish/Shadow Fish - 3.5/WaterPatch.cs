using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace ShadowsOfTheDeep
{
    public static class WaterPatches
    {
        // PREFIX PATCH:
        // We draw the shadows BEFORE the game draws the water tiles.
        // Result: The game draws the semi-transparent water tiles ON TOP of our black shadows.
        // This gives them a blue/green tint and obscures them, making them look deep underwater.
        public static void DrawWater_Prefix(GameLocation __instance, SpriteBatch b)
        {
            // Access the PerScreen value to ensure split-screen works correctly.
            // Harmony executes this on the thread rendering the current screen.
            var manager = ModEntry.ShadowManagers.Value;

            // Ensure we only draw for the actual current location being rendered
            if (manager != null && __instance == Game1.currentLocation)
            {
                manager.Draw(b);
            }
        }
    }
}