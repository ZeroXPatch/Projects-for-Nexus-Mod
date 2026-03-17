using System.Collections.Generic;

namespace ShadowsOfTheDeep
{
    public class ModConfig
    {
        // Visuals
        public float ShadowOpacity { get; set; } = 1.0f;
        public bool EnableFadeEffects { get; set; } = true;
        public float FadeSpeed { get; set; } = 0.02f;
        public bool ShowVisibleFish { get; set; } = true;
        public float MinFishScale { get; set; } = 0.6f;
        public float MaxFishScale { get; set; } = 1.0f;

        // ROTATION
        public float ModdedFishRotation { get; set; } = 45f;

        // BEHAVIOR
        public bool EnableFishPersonalities { get; set; } = true;
        public bool AvoidCrowding { get; set; } = true;
        public float ConstantSwimChance { get; set; } = 0.40f;
        public float MoveSpeedMultiplier { get; set; } = 0.3f;
        public float MinIdleSeconds { get; set; } = 1.0f;
        public float MaxIdleSeconds { get; set; } = 4.0f;
        public float BurstChance { get; set; } = 0.4f;

        // LIFESPAN
        public float MinLifespanSeconds { get; set; } = 60f;
        public float MaxLifespanSeconds { get; set; } = 200f;

        // POPULATION
        public int MinFishCount { get; set; } = 100;
        public int MaxFishCount { get; set; } = 400;
        public float SpawnChance { get; set; } = 0.50f;
        public float DensityCapMultiplier { get; set; } = 1.0f;
        public float InitialSpawnChance { get; set; } = 0.7f;

        // Locations & Time
        public bool FarmOnly { get; set; } = false;
        public List<string> ExcludedLocations { get; set; } = new() { };
        public bool HideFishAtNight { get; set; } = true;
        public int HoursAfterSunset { get; set; } = 2;
    }
}