using StardewModdingAPI;

namespace TextureCachePurge
{
    public class ModConfig
    {
        public bool AutoClearAtSleep { get; set; } = true;

        public int PurgeFrequencyDays { get; set; } = 3;
        public int MinimumRamForSleepPurge { get; set; } = 3072;

        public SButton ManualClearKey { get; set; } = SButton.F5;

        public bool EnableRamThreshold { get; set; } = false;
        public int RamThresholdMB { get; set; } = 2048;

        // NEW: Safety setting. Default true, but crash-prone users can set to false.
        public bool ForceGarbageCollection { get; set; } = true;
    }
}