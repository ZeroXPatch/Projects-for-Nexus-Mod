namespace ZeroXPatch
{
    /// <summary>The mod configuration.</summary>
    public class ModConfig
    {
        /// <summary>Only process automation when player is idle (not moving).</summary>
        public bool OnlyProcessWhenIdle { get; set; } = true;

        /// <summary>Number of ticks player must be stationary before considered idle (60 ticks = 1 second).</summary>
        public int IdleTicksThreshold { get; set; } = 480; // 8 seconds default

        /// <summary>Don't count idle time during cutscenes, events, or dialogue with NPCs.</summary>
        public bool PauseTimerDuringCutscenes { get; set; } = true;

        /// <summary>Pause idle timer when any input is detected (mouse clicks, keyboard, controller, touch).</summary>
        public bool PauseTimerOnInput { get; set; } = true;

        /// <summary>Enable comprehensive debug logging to understand what the mod is doing.</summary>
        public bool DebugMode { get; set; } = false;
    }
}