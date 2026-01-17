using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace AquaticLife
{
    public static class WaterPatches
    {
        public static void DrawWater_Prefix(GameLocation __instance, SpriteBatch b)
        {
            var manager = ModEntry.FishManagers.Value;
            if (manager != null && __instance == Game1.currentLocation)
            {
                manager.Draw(b);
            }
        }
    }
}