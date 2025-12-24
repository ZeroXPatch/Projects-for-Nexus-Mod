using System.Collections.Generic;

namespace LocationPreload
{
    internal enum PreloadMode
    {
        Hub,
        Nearest,
        Both
    }

    internal sealed class ModConfig
    {
        public bool Enabled { get; set; } = true;

        public PreloadMode Mode { get; set; } = PreloadMode.Both;

        /// <summary>If true, Hub mode queues warp targets from the current location after each warp.</summary>
        public bool HubPreloadWarpTargetsFromCurrentLocation { get; set; } = true;

        /// <summary>Optional extra locations to preload (by location name, e.g. "Town", "Forest", "Mountain").</summary>
        public List<string> CustomLocationPreloadList { get; set; } = new();

        /// <summary>Clear the pending preload queue when you warp (doesn't forget what's already preloaded today).</summary>
        public bool ClearQueueOnWarp { get; set; } = true;

        /// <summary>Hard cap on number of maps loaded per tick (0 = no cap).</summary>
        public int MaxMapsPerTick { get; set; } = 1;

        /// <summary>Time budget per tick in ms (0 = no budget). If both limits are set, whichever hits first stops.</summary>
        public int MaxMillisecondsPerTick { get; set; } = 2;

        // Nearest mode tuning:

        /// <summary>How often to evaluate nearest warps (in ticks). Lower is more responsive; higher is cheaper.</summary>
        public int NearestCheckIntervalTicks { get; set; } = 6;

        /// <summary>How many nearest warp destinations to queue each check.</summary>
        public int NearestWarpCount { get; set; } = 1;

        /// <summary>Only consider warps within this Manhattan distance (0 = unlimited).</summary>
        public int NearestMaxDistance { get; set; } = 8;

        /// <summary>If true, prefer the warp tile directly in front of the player when ties exist.</summary>
        public bool PreferFacingWarp { get; set; } = true;

        public bool VerboseLogging { get; set; } = false;
    }
}
