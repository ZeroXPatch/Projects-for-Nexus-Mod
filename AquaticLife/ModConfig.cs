using System.Collections.Generic;

namespace AquaticLife
{
    public class ModConfig
    {
        // Visuals
        public float FishOpacity { get; set; } = 0.85f; // Default slightly transparent for water effect
        public float FishScale { get; set; } = 1.0f;
        public bool EnableFadeEffects { get; set; } = true;
        public float FadeSpeed { get; set; } = 0.02f;

        // Population
        public int MinFishCount { get; set; } = 10;
        public int MaxFishCount { get; set; } = 40;
        public float SpawnChance { get; set; } = 0.15f;

        // Locations & Time
        public bool FarmOnly { get; set; } = false;
        public List<string> ExcludedLocations { get; set; } = new() { "Sewer", "BugLand", "WitchSwamp", "VolcanoCaldera" };
        public bool HideFishAtNight { get; set; } = true;
        public int HoursAfterSunset { get; set; } = 2;
    }
}