namespace ReelTensionFishing
{
    internal sealed class ModConfig
    {
        public bool Enabled { get; set; } = true;

        /// <summary>If false, fishing stays vanilla even if the mod is enabled.</summary>
        public bool UseReelTensionMinigame { get; set; } = true;

        /// <summary>If true, legendary/boss fish will use vanilla minigame.</summary>
        public bool UseVanillaForLegendary { get; set; } = true;

        /// <summary>Width of the safe zone (percent of the meter), before difficulty scaling.</summary>
        public int SafeZonePercent { get; set; } = 35;

        /// <summary>How fast tension rises while holding Use Tool.</summary>
        public int TensionUpRate { get; set; } = 10;

        /// <summary>How fast tension falls while not holding Use Tool.</summary>
        public int TensionDownRate { get; set; } = 12;

        /// <summary>Catch progress gain rate while in the safe zone.</summary>
        public int CatchGainRate { get; set; } = 14;

        /// <summary>Catch progress loss rate while outside the safe zone.</summary>
        public int CatchLossRate { get; set; } = 18;

        // Treasure
        public bool EnableTreasureMinigame { get; set; } = true;

        /// <summary>Seconds before treasure becomes available (if the fish has treasure).</summary>
        public int TreasureAppearSeconds { get; set; } = 2;

        public int TreasureGainRate { get; set; } = 18;
        public int TreasureLossRate { get; set; } = 6;
    }
}
