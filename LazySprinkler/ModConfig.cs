namespace LazySprinkler
{
    internal class ModConfig
    {
        public double ExtraWaterChance { get; set; } = 0.18;
        public int ExtraWaterRadius { get; set; } = 1;
        public int ExtraWaterTiles { get; set; } = 2;
        public double SkipWaterChance { get; set; } = 0.1;
        public int MaxSkippedTiles { get; set; } = 2;
        public double FertilizerChance { get; set; } = 0.05;
        public int FertilizerItemId { get; set; } = 368;
        public double OverflowChance { get; set; } = 0.02;
        public int OverflowRadius { get; set; } = 2;
        public int OverflowTiles { get; set; } = 1;
        public double GrowthSpurtChance { get; set; } = 0.03;
        public int GrowthSpurtTiles { get; set; } = 2;
        public bool DebugLogging { get; set; } = false;
    }
}
