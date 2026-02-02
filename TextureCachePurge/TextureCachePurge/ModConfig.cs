using StardewModdingAPI;

namespace TextureCachePurge
{
    public class ModConfig
    {
        public bool AutoClearAtSleep { get; set; } = true;

        // Default: 1 (Clean every night). 
        // Players with slow PCs can change this to 3 or 7.
        public int PurgeFrequencyDays { get; set; } = 3;

        // Default: 3072MB (3 GB). 
        // If RAM is lower than this, it won't freeze the game at night.
        public int MinimumRamForSleepPurge { get; set; } = 3072;

        public SButton ManualClearKey { get; set; } = SButton.F5;

        // Active settings (Disabled by default)
        public bool EnableRamThreshold { get; set; } = false;
        public int RamThresholdMB { get; set; } = 2048;
    }
}