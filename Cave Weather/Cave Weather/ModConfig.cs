namespace CaveWeather
{
    /// <summary>Common enable + per-location toggles for a cave weather.</summary>
    public class ModConfigWeather
    {
        public bool Enabled { get; set; } = true;

        public bool EnableInMines { get; set; } = true;
        public bool EnableInSkullCavern { get; set; } = true;
        public bool EnableInVolcanoDungeon { get; set; } = true;
    }

    public sealed class ModConfig
    {
        /// <summary>Chance per day (0–1) that any cave weather happens.</summary>
        public double DailyCaveWeatherChance { get; set; } = 0.25;

        // 5 preexisting
        public FungalHarvestConfig FungalHarvest { get; set; } = new();
        public TemporalFluxConfig TemporalFlux { get; set; } = new();
        public BerserkerDayConfig BerserkerDay { get; set; } = new();
        public FrenzyFogConfig FrenzyFog { get; set; } = new();
        public BloodthirstWindsConfig BloodthirstWinds { get; set; } = new();

        // 2 new
        public UnstableVeinsConfig UnstableVeins { get; set; } = new();
        public LuckyVeinsConfig LuckyVeins { get; set; } = new();
    }

    // ----------------
    // Individual configs
    // ----------------

    public sealed class FungalHarvestConfig : ModConfigWeather
    {
        public int ExtraMushroomsPerFloor { get; set; } = 5;
        public double SlimeDropChance { get; set; } = 0.20;

        public uint SporeSpawnIntervalTicks { get; set; } = 45;
        public int SporeParticlesPerBurst { get; set; } = 4;
        public int SporeLoops { get; set; } = 3;
    }

    public sealed class TemporalFluxConfig : ModConfigWeather
    {
        public double TimeScale { get; set; } = 0.70;
        public double BurstSpeedMultiplier { get; set; } = 1.6;
        public double SlowSpeedMultiplier { get; set; } = 0.5;
        public uint PhaseDurationTicks { get; set; } = 60;
    }

    public sealed class BerserkerDayConfig : ModConfigWeather
    {
        public int MonsterSpeedBonus { get; set; } = 2;
        public int MonsterAggroTilesBonus { get; set; } = 4;
        public int PlayerDefenseBonus { get; set; } = 2;
    }

    public sealed class FrenzyFogConfig : ModConfigWeather
    {
        public int MonsterSpeedBonus { get; set; } = 1;
        public int PlayerDefenseBonus { get; set; } = 1;
        public int FogOpacity { get; set; } = 80; // 0–255
    }

    public sealed class BloodthirstWindsConfig : ModConfigWeather
    {
        public int PlayerDefensePenalty { get; set; } = 2;
        public uint HealIntervalTicks { get; set; } = 120;
        public int HealAmount { get; set; } = 1;
    }

    public sealed class UnstableVeinsConfig : ModConfigWeather
    {
        public uint PulseIntervalTicks { get; set; } = 60;

        public double ExplosionPulseChance { get; set; } = 0.15; // 15% per pulse
        public int ExplosionDamage { get; set; } = 5;
        public int ExplosionRadiusTiles { get; set; } = 2;
        public float KnockbackStrength { get; set; } = 2.5f;

        public int MinExtraOreDrops { get; set; } = 1;
        public int MaxExtraOreDrops { get; set; } = 2;
    }

    public sealed class LuckyVeinsConfig : ModConfigWeather
    {
        public int ExtraOreFindsPerFloor { get; set; } = 2;

        public uint GlintIntervalTicks { get; set; } = 90;

        public uint CoalPulseIntervalTicks { get; set; } = 90;
        public double CoalFindChancePerPulse { get; set; } = 0.10; // 10% per pulse
    }
}
