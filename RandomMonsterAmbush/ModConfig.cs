using System.Collections.Generic;

namespace RandomMonsterAmbush
{
    /// <summary>
    /// Configuration options for the Random Monster Ambush mod.
    /// </summary>
    public class ModConfig
    {
        public bool EnableMod { get; set; } = true;

        public double SpawnChance { get; set; } = 0.25;

        public int CheckIntervalTicks { get; set; } = 120;

        public int MinSpawnDistance { get; set; } = 3;

        public int MaxSpawnDistance { get; set; } = 8;

        public int MaxMonstersPerSpawn { get; set; } = 2;

        public bool AllowDaytimeSpawns { get; set; } = false;

        public bool PreventDuringEvents { get; set; } = true;

        public List<string> DisallowedLocations { get; set; } = new()
        {
            "FarmHouse",
            "FarmHouse1",
            "FarmHouse2",
            "Cellar",
            "HaleyHouse",
            "ElliottHouse",
            "SebastianRoom",
            "HarveyRoom",
            "SamHouse",
            "SeedShop"
        };

        public bool ShowHudMessage { get; set; } = true;
    }
}
