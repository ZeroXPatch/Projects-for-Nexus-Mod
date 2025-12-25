using System.Collections.Generic;

namespace RandomMonsterAmbush
{
    /// <summary>Configuration for RandomMonsterAmbush.</summary>
    public class ModConfig
    {
        public bool EnableMod { get; set; } = true;

        /// <summary>How many ticks between spawn checks (60 ticks ≈ 1 second).</summary>
        public int CheckIntervalTicks { get; set; } = 120; // requested default

        /// <summary>Maximum monsters per ambush spawn.</summary>
        public int MaxMonstersPerSpawn { get; set; } = 3;

        /// <summary>Minimum tile distance from the player for spawn.</summary>
        public int MinSpawnDistance { get; set; } = 3;

        /// <summary>Maximum tile distance from the player for spawn.</summary>
        public int MaxSpawnDistance { get; set; } = 10;

        /// <summary>Chance that an ambush happens when a spawn check runs (0–1).</summary>
        public double SpawnChance { get; set; } = 0.10; // requested default

        /// <summary>Allow ambushes during the day (before 6pm).</summary>
        public bool AllowDaytimeSpawns { get; set; } = false;

        /// <summary>
        /// Earliest time ambushes can happen (SDV time format, e.g. 1800 = 6:00 PM).
        /// If AllowDaytimeSpawns is false, the mod clamps this to 1800 or later.
        /// </summary>
        public int AmbushStartTime { get; set; } = 1800;

        /// <summary>Prevent ambushes during festivals, events, and minigames.</summary>
        public bool PreventDuringEvents { get; set; } = true;

        /// <summary>If true, ambushes won't spawn while the player is currently holding any fishing rod.</summary>
        public bool SkipWhileHoldingFishingRod { get; set; } = false;

        /// <summary>If true, ambushes are blocked in ALL indoor locations.</summary>
        public bool DisallowIndoors { get; set; } = true; // requested default ON

        /// <summary>Enable special boss-style ambush monsters.</summary>
        public bool EnableBossSpawns { get; set; } = true;

        /// <summary>Chance that an ambush includes a boss (0–1).</summary>
        public double BossSpawnChance { get; set; } = 0.10; // requested default

        /// <summary>Health multiplier applied to boss monsters.</summary>
        public float BossHealthMultiplier { get; set; } = 2f;

        /// <summary>Damage multiplier applied to boss monsters.</summary>
        public float BossDamageMultiplier { get; set; } = 2f;

        /// <summary>Locations where ambushes are never allowed.</summary>
        public List<string> DisallowedLocations { get; set; } = new();

        // Monster toggles (default ON)
        public bool EnableGreenSlime { get; set; } = true;
        public bool EnableDustSpirit { get; set; } = true;
        public bool EnableBat { get; set; } = true;
        public bool EnableRockCrab { get; set; } = true;
        public bool EnableGhost { get; set; } = true;
        public bool EnableSkeleton { get; set; } = true;
        public bool EnableSquidKid { get; set; } = true;
        public bool EnableShadowBrute { get; set; } = true;
        public bool EnableShadowShaman { get; set; } = true;
        public bool EnableSerpent { get; set; } = true;
    }
}
