namespace RealTimeValley;

public class ModConfig
{
    public bool Enable { get; set; } = true;

    // Real-time clock
    public bool MinuteAccurateClock { get; set; } = true;
    public int SyncIntervalSeconds { get; set; } = 1;

    // Festival-friendly (optional): prevents "empty festival" when real time is outside festival hours
    public bool FestivalFriendlyMode { get; set; } = true;
    public bool HoldTimeInsideFestivalWindow { get; set; } = true;
    public int FestivalHoldTimeDay { get; set; } = 1200;   // noon
    public int FestivalHoldTimeNight { get; set; } = 2230; // night festivals

    // Night clamp
    public bool ClampAtNight { get; set; } = true;
    public bool PauseSyncAtMaxNight { get; set; } = true;
    public int ResumeSyncHour { get; set; } = 6;
    public bool TreatMidnightAsLateNight { get; set; } = true;
    public int ClampMinTime { get; set; } = 600;
    public int ClampMaxTime { get; set; } = 2600;

    // World catch-up (recommended ON): runs 10-min updates forward on load so shops/NPC states aren't "stuck"
    public bool RunWorldCatchupOnLoad { get; set; } = true;
    public int MaxWorldCatchupTenMinuteSteps { get; set; } = 200;

    // Pause handling
    public bool PauseWhenPaused { get; set; } = true;
    public bool UseShouldTimePassCheck { get; set; } = true;

    // NPC fix (light safety net)
    public bool NPCFixOnLoad { get; set; } = true;
    public int NpcWakeEarliestTime { get; set; } = 700;
    public int NpcFixCooldownSeconds { get; set; } = 10;
    public NPCWarpAggressiveness NPCWarpAggressiveness { get; set; } = NPCWarpAggressiveness.Conservative;

    // NEW: Machine acceleration (the fix you asked for)
    public bool AccelerateMachines { get; set; } = true;

    public MachineSpeedMode MachineSpeedMode { get; set; } = MachineSpeedMode.MatchVanilla;

    /// <summary>
    /// Only used when MachineSpeedMode = CustomMultiplier.
    /// Multiplier is relative to real-time world minutes.
    /// Example: 10 => machines run 10x faster than real-time.
    /// To match vanilla, use ~85.714.
    /// </summary>
    public double MachineSpeedMultiplier { get; set; } = 85.71428571428571;

    /// <summary>
    /// Safety cap: max minutes applied to machines per second-tick.
    /// Prevents insane catch-ups if the game hitches.
    /// </summary>
    public int MachineMaxMinutesPerTick { get; set; } = 120;

    public bool DebugLogging { get; set; } = false;
    public bool ShowCompatibilityWarnings { get; set; } = true;
}

public enum MachineSpeedMode
{
    Off,
    MatchVanilla,
    CustomMultiplier
}

public enum NPCWarpAggressiveness
{
    Conservative,
    Aggressive
}
