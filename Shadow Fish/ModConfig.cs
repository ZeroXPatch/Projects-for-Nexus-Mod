using System.Collections.Generic;

namespace ShadowsOfTheDeep
{
    public class ModConfig
    {
        // Visuals
        public float ShadowOpacity { get; set; } = 0.6f;
        public float ShadowScale { get; set; } = 1.0f;

        // NEW: Fade Effects
        public bool EnableFadeEffects { get; set; } = true;
        public float FadeSpeed { get; set; } = 0.02f; // Internal config for smoothness

        // Population
        public int MaxFishCount { get; set; } = 100;
        public float SpawnChance { get; set; } = 0.35f;

        // Locations & Time
        public bool FarmOnly { get; set; } = false;
        public List<string> ExcludedLocations { get; set; } = new() { };

        // NEW: Night Logic
        public bool HideFishAtNight { get; set; } = true;
        public int HoursAfterSunset { get; set; } = 2; // e.g., Sunset 6pm + 2h = 8pm
    }
}