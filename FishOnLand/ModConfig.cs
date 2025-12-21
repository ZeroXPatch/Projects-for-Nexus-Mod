#nullable enable

namespace LandFishSwimmers
{
    internal sealed class ModConfig
    {
        public bool Enabled { get; set; } = true;

        // Daily trigger chance (percent)
        public int DailyChancePercent { get; set; } = 5;

        // Active window (10pm to 2am default)
        public int StartTime { get; set; } = 2200;
        public int EndTime { get; set; } = 2600;

        // Buff amount during the active window
        public int FishingSkillBonus { get; set; } = 2;

        // If true, show a HUD popup when the event activates
        public bool ShowActivationMessage { get; set; } = true;

        // Default ON: allow catching every fish in any water during the event window
        public bool AllFishEverywhere { get; set; } = true;

        // Visual fish settings (now distributed across map, not around player)
        public int FishCount { get; set; } = 20;

        // If false, fish sprites only appear outdoors
        public bool SpawnIndoors { get; set; } = false;

        // Visual update frequency
        public int UpdateTicks { get; set; } = 4;

        // Fish movement/animation tuning
        public float SpeedTilesPerSecond { get; set; } = 1.25f;
        public float TurnChancePerUpdate { get; set; } = 0.10f;

        public float Scale { get; set; } = 1.0f;
        public float Opacity { get; set; } = 1.0f;

        public float BobPixels { get; set; } = 2.5f;
        public float BobSpeed { get; set; } = 0.25f;
        public float WiggleRadians { get; set; } = 0.10f;
    }
}
