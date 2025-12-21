namespace RealTimeValley;

public class ModConfig
{
    public bool Enable { get; set; } = true;

    /// <summary>
    /// Freeze vanilla time and sync the in-game clock to real time minute-by-minute.
    /// </summary>
    public bool MinuteAccurateClock { get; set; } = true;

    /// <summary>How often to sync to real time.</summary>
    public int SyncIntervalSeconds { get; set; } = 1;

    // World catch-up (10-min updates) so shops/NPCs update correctly after big jumps
    public bool RunWorldCatchupOnLoad { get; set; } = true;
    public bool RunWorldCatchupOnLargeForwardJump { get; set; } = true;
    public int LargeForwardJumpMinutes { get; set; } = 60;
    public int MaxWorldCatchupTenMinuteSteps { get; set; } = 200;

    // Festival handling (the new important part)
    public bool FestivalFriendlyMode { get; set; } = true;

    /// <summary>
    /// If true: when it's a festival day and you haven't triggered the festival event yet,
    /// the mod will hold time inside the festival window so you can enter and start it.
    /// </summary>
    public bool HoldTimeInsideFestivalWindow { get; set; } = true;

    /// <summary>Which time (HHMM) to hold at if current real time is outside the festival window.</summary>
    public int FestivalHoldTimeDay { get; set; } = 1200;   // noon
    public int FestivalHoldTimeNight { get; set; } = 2230; // night festivals

    public bool PauseWhenPaused { get; set; } = true;
    public bool UseShouldTimePassCheck { get; set; } = true;

    // Night clamp
    public bool ClampAtNight { get; set; } = true;
    public bool PauseSyncAtMaxNight { get; set; } = true;
    public int ResumeSyncHour { get; set; } = 6;

    /// <summary>Use 24xx/25xx for after midnight when enabled.</summary>
    public bool TreatMidnightAsLateNight { get; set; } = true;

    public int ClampMinTime { get; set; } = 600;
    public int ClampMaxTime { get; set; } = 2600;

    // NPC fix (light safety net)
    public int NpcWakeEarliestTime { get; set; } = 700;
    public bool NPCFixOnLoad { get; set; } = true;
    public bool NPCFixAfterWorldCatchup { get; set; } = true;
    public int NpcFixCooldownSeconds { get; set; } = 10;
    public NPCWarpAggressiveness NPCWarpAggressiveness { get; set; } = NPCWarpAggressiveness.Conservative;

    public bool ShowCompatibilityWarnings { get; set; } = true;
    public bool DebugLogging { get; set; } = false;
}

public enum NPCWarpAggressiveness
{
    Conservative,
    Aggressive
}
