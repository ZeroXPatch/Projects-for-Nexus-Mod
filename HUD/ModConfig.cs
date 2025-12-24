using StardewModdingAPI.Utilities;

namespace PerformanceHud
{
    internal sealed class ModConfig
    {
        // General
        public bool Enabled { get; set; } = true;
        public bool DrawBackground { get; set; } = true;

        // Position (UI pixels)
        public int PositionX { get; set; } = 16;
        public int PositionY { get; set; } = 16;

        // Shortcut
        public KeybindList ToggleOverlayKey { get; set; } = KeybindList.Parse("F8");

        // Performance
        public bool ShowFps { get; set; } = true;
        public bool ShowFrameTime { get; set; } = true;
        public bool ShowMemory { get; set; } = true;
        public bool ShowUpdateLoadEstimate { get; set; } = true;

        // Location
        public bool ShowCurrentLocationId { get; set; } = true;

        // Animations
        public bool ShowTemporarySprites { get; set; } = true;

        // Debug QoL
        public bool ShowPlayerTile { get; set; } = true;
        public bool ShowPlayerFacing { get; set; } = true;
        public bool ShowInGameDateTime { get; set; } = true;
        public bool ShowWeather { get; set; } = true;
        public bool ShowMultiplayerInfo { get; set; } = true;
    }
}
