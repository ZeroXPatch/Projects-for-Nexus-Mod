using StardewModdingAPI.Utilities;

namespace StardewStabilizer
{
    public sealed class ModConfig
    {
        public bool Enabled { get; set; } = true;

        // How often to sample memory & make decisions (seconds).
        // Note: SMAPI's OneSecondUpdateTicked fires once per second, so the minimum effective value is 1.
        public int CheckIntervalSeconds { get; set; } = 1;

        // Auto cleanup behavior
        public bool AutoCleanup { get; set; } = true;

        // Pressure thresholds (0–100) based on max(managed, working set) vs available memory.
        public int SoftPressurePercent { get; set; } = 70;
        public int HardPressurePercent { get; set; } = 85;

        // Emergency hard cleanup can run even outside menus to prevent crashes.
        public int EmergencyPressurePercent { get; set; } = 95;

        // Freeze-control style options:
        // Hard cleanup is menu-only (or fade) unless emergency triggers.
        public bool HardCleanupMenuOnly { get; set; } = true;

        // While waiting for a safe moment to hard-clean, do soft cleanups instead.
        public bool PreferSoftWhileWaitingForHard { get; set; } = true;

        // Anti-spike filtering
        public bool UseTrendAverageForDecisions { get; set; } = true;
        public int TrendWindowSeconds { get; set; } = 10;

        // Require memory pressure to stay above the soft threshold for this many seconds before any automatic cleanup.
        // Set 0 to disable.
        public int SustainSeconds { get; set; } = 3;

        // Prevent cleanup spam oscillation.
        public int HysteresisPercent { get; set; } = 5;

        // Cooldowns (seconds)
        public int SoftCooldownSeconds { get; set; } = 120;
        public int HardCooldownSeconds { get; set; } = 600;

        // Hard cleanup options
        public bool CompactLargeObjectHeapOnHardClean { get; set; } = true;

        // Optional: ask Windows to trim the process working set.
        // Off by default because it can cause extra stutter on some PCs.
        public bool TrimWorkingSetOnWindows { get; set; } = false;

        // UI
        public bool ShowHudMessages { get; set; } = true;
        public bool ShowOverlay { get; set; } = false;
        public int OverlayX { get; set; } = 12;
        public int OverlayY { get; set; } = 12;

        // Hotkeys
        public bool AllowHotkeysInMenus { get; set; } = false;
        public KeybindList SoftCleanKey { get; set; } = KeybindList.Parse("F9");
        public KeybindList HardCleanKey { get; set; } = KeybindList.Parse("LeftShift + F9");

        // Fallback budget if runtime can't report TotalAvailableMemoryBytes
        public int FallbackAvailableMemoryMB { get; set; } = 4096;
    }
}
