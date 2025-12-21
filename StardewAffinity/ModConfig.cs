namespace StardewAffinity
{
    public sealed class ModConfig
    {
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// If true: when the game window loses focus, restore the game's original affinity.
        /// When refocused, re-apply the configured affinity.
        /// </summary>
        public bool RestoreOnUnfocus { get; set; } = true;

        /// <summary>
        /// Modes:
        /// - "ExcludeCpu0" (default): uses all CPUs except CPU 0 (matches Task Manager instructions)
        /// - "AllCores": uses all CPUs
        /// - "CustomMask": uses CustomMask (decimal or hex)
        /// </summary>
        public string Mode { get; set; } = "ExcludeCpu0";

        /// <summary>
        /// Only used when Mode = "CustomMask".
        /// Examples:
        /// - "15"  (decimal) = CPUs 0-3
        /// - "0xF" (hex)     = CPUs 0-3
        /// </summary>
        public string CustomMask { get; set; } = "0xFFFFFFFFFFFFFFFE";

        public bool LogInfo { get; set; } = true;
    }
}
