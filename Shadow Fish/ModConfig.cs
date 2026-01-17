using System.Collections.Generic;

namespace ShadowsOfTheDeep
{
    public class ModConfig
    {
        // Visuals
        public float ShadowOpacity { get; set; } = 0.6f;
        public float ShadowScale { get; set; } = 0.90f;
        public bool EnableFadeEffects { get; set; } = true;
        public float FadeSpeed { get; set; } = 0.02f;

        // BEHAVIOR
        public bool EnableFishPersonalities { get; set; } = true;
        public float ConstantSwimChance { get; set; } = 0.40f; // 30% default
        public float MoveSpeedMultiplier { get; set; } = 1.0f;
        public float MinIdleSeconds { get; set; } = 2.0f;
        public float MaxIdleSeconds { get; set; } = 6.0f;
        public float BurstChance { get; set; } = 0.4f;

        // Population
        public int MinFishCount { get; set; } = 200;
        public int MaxFishCount { get; set; } = 400;
        public float SpawnChance { get; set; } = 0.25f;

        // Locations & Time
        public bool FarmOnly { get; set; } = false;
        public List<string> ExcludedLocations { get; set; } = new() { };
        public bool HideFishAtNight { get; set; } = true;
        public int HoursAfterSunset { get; set; } = 2;
    }
}