using System.Collections.Generic;

namespace ShadowsOfTheDeep
{
    public class ModConfig
    {
        // Visuals
        public float ShadowOpacity { get; set; } = 0.6f;
        public float ShadowScale { get; set; } = 0.9f;
        public bool EnableFadeEffects { get; set; } = true;
        public float FadeSpeed { get; set; } = 0.02f;

        // BEHAVIOR
        public bool EnableFishPersonalities { get; set; } = true;
        public float ConstantSwimChance { get; set; } = 0.30f;
        public float MoveSpeedMultiplier { get; set; } = 1.0f;
        public float MinIdleSeconds { get; set; } = 2.0f;
        public float MaxIdleSeconds { get; set; } = 6.0f;
        public float BurstChance { get; set; } = 0.4f;

        // POPULATION
        public int MinFishCount { get; set; } = 350;
        public int MaxFishCount { get; set; } = 500;
        public float SpawnChance { get; set; } = 0.8f;

        // NEW CONFIGS
        public float DensityCapMultiplier { get; set; } = 3f; // Allows user to override the "Safety Brake"
        public float InitialSpawnChance { get; set; } = 0.7f;   // How many spawn instantly

        // Locations & Time
        public bool FarmOnly { get; set; } = false;
        public List<string> ExcludedLocations { get; set; } = new() {};
        public bool HideFishAtNight { get; set; } = true;
        public int HoursAfterSunset { get; set; } = 2;
    }
}