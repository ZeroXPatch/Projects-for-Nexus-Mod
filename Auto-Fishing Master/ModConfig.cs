using StardewModdingAPI;

namespace AutoFishingMaster
{
    public class ModConfig
    {
        public bool EnableMod { get; set; } = false;
        public SButton ToggleKey { get; set; } = SButton.F3;

        // Core Features
        public bool AutoCast { get; set; } = true;
        public bool AutoHit { get; set; } = true;

        // Rewards
        public bool AutoLootTreasure { get; set; } = true;
        public bool AlwaysPerfect { get; set; } = true;
        public bool AlwaysMaxCastPower { get; set; } = true;

        // Safety
        public bool EnableStaminaCheck { get; set; } = true;
        public int StaminaThreshold { get; set; } = 15;

        // Debug
        public bool DebugMode { get; set; } = false;
    }
}