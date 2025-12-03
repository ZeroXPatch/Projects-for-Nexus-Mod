using System.Collections.Generic;

namespace RandomMonsterAmbush
{
    /// <summary>
    /// Configuration options for the Random Monster Ambush mod.
    /// </summary>
    public class ModConfig
    {
        /// <summary>Master on/off switch for the mod.</summary>
        public bool EnableMod { get; set; } = true;

        /// <summary>Chance (0–1) that an ambush happens when a check runs.</summary>
        public double SpawnChance { get; set; } = 0.25;

        /// <summary>How often to check for ambushes, in ticks (60 ticks = 1 second).</summary>
        public int CheckIntervalTicks { get; set; } = 120;

        /// <summary>Minimum tile distance from the player for spawns.</summary>
        public int MinSpawnDistance { get; set; } = 3;

        /// <summary>Maximum tile distance from the player for spawns.</summary>
        public int MaxSpawnDistance { get; set; } = 8;

        /// <summary>Maximum monsters spawned per ambush.</summary>
        public int MaxMonstersPerSpawn { get; set; } = 2;

        /// <summary>Allow ambushes during the day (before 6:00 PM).</summary>
        public bool AllowDaytimeSpawns { get; set; } = false;

        /// <summary>Block ambushes during events and festivals.</summary>
        public bool PreventDuringEvents { get; set; } = true;

        /// <summary>Locations where ambushes can never occur.</summary>
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

        /// <summary>Legacy toggle for HUD messages (currently unused).</summary>
        public bool ShowHudMessage { get; set; } = false;

        // ===== Boss settings =====

        /// <summary>Whether boss-style ambushes are enabled.</summary>
        public bool EnableBossSpawns { get; set; } = true;

        /// <summary>Chance (0–1) that an ambush includes one boss monster.</summary>
        public double BossSpawnChance { get; set; } = 0.05;

        /// <summary>How much to multiply boss health (1.0 = normal).</summary>
        public float BossHealthMultiplier { get; set; } = 2f;

        /// <summary>How much to multiply boss damage (1.0 = normal).</summary>
        public float BossDamageMultiplier { get; set; } = 2f;
    }
}
