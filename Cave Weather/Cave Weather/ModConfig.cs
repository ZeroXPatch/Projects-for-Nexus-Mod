namespace CaveWeather
{
    public class ModConfigWeather
    {
        public bool Enabled { get; set; } = true;

        public bool EnableInMines { get; set; } = true;
        public bool EnableInSkullCavern { get; set; } = true;
        public bool EnableInVolcanoDungeon { get; set; } = true;
    }

    public sealed class ModConfig
    {
        public double DailyCaveWeatherChance { get; set; } = 0.25;

        public FungalHarvestConfig FungalHarvest { get; set; } = new();
        public TemporalFluxConfig TemporalFlux { get; set; } = new();
        public BerserkerDayConfig BerserkerDay { get; set; } = new();
        public FrenzyFogConfig FrenzyFog { get; set; } = new();
        public BloodthirstWindsConfig BloodthirstWinds { get; set; } = new();
    }

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
        public int FogOpacity { get; set; } = 80;
    }

    public sealed class BloodthirstWindsConfig : ModConfigWeather
    {
        public int PlayerDefensePenalty { get; set; } = 2;
        public uint HealIntervalTicks { get; set; } = 120;
        public int HealAmount { get; set; } = 1;
    }
}
