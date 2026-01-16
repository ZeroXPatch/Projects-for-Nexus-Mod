using System.Collections.Generic;

namespace ShadowsOfTheDeep
{
    public class ModConfig
    {
        // Visuals
        public float ShadowOpacity { get; set; } = 0.6f;
        public float ShadowScale { get; set; } = 1.0f;

        // Population
        public int MaxFishCount { get; set; } = 100;
        public float SpawnChance { get; set; } = 0.35f;

        // Locations
        public bool FarmOnly { get; set; } = false;
        public List<string> ExcludedLocations { get; set; } = new() {  };
    }
}