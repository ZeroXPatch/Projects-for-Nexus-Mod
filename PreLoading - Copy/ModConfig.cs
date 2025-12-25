using StardewModdingAPI;

namespace OffscreenAnimationFreezer
{
    internal sealed class ModConfig
    {
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Safe = critters only.
        /// Balanced = critters + (likely) looping temporary animated sprites.
        /// </summary>
        public string Mode { get; set; } = FreezeMode.Safe;

        public int OffscreenMarginTiles { get; set; } = 4;

        public bool DisableDuringEvents { get; set; } = true;

        // Debug / testing
        public bool DebugLogging { get; set; } = false;

        /// <summary>
        /// VERY aggressive test option: freeze *all* TemporaryAnimatedSprites offscreen.
        /// Use to confirm the mod is working, then turn off.
        /// </summary>
        public bool FreezeAllTemporarySprites { get; set; } = false;

        public SButton ToggleDebugKey { get; set; } = SButton.F7;
    }
}
