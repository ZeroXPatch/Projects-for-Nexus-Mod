using StardewModdingAPI;

namespace AutoFishingMaster
{
    public class ModConfig
    {
        public bool ToggleEnabled { get; set; } = true;
        public SButton ToggleKey { get; set; } = SButton.F5;

        // Core Features
        public bool AutoCast { get; set; } = true;
        public bool AutoHit { get; set; } = true;

        // Rewards
        public bool AutoLootTreasure { get; set; } = true;
        public bool AlwaysPerfect { get; set; } = true;
        public bool AlwaysMaxCastPower { get; set; } = true;

        // Debug
        public bool DebugMode { get; set; } = false;
    }
}