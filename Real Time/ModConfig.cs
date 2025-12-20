using StardewModdingAPI;

namespace RealTimeValley;

public class ModConfig
{
    public bool Enable { get; set; } = true;
    public RoundMode RoundMode { get; set; } = RoundMode.Nearest10;
    public int SyncIntervalSeconds { get; set; } = 1;
    public int SmallDriftMinutes { get; set; } = 10;
    public int LargeDriftMinutes { get; set; } = 30;
    public LargeDriftBehavior LargeDriftBehavior { get; set; } = LargeDriftBehavior.SoftCatchup;
    public bool AllowRewind { get; set; } = false;
    public bool PauseWhenPaused { get; set; } = true;
    public bool ClampAtNight { get; set; } = true;
    public bool PauseSyncAtMaxNight { get; set; } = true;
    public int ResumeSyncHour { get; set; } = 6;
    public bool TreatMidnightAsLateNight { get; set; } = true;
    public int ClampMinTime { get; set; } = 600;
    public int ClampMaxTime { get; set; } = 2400;
    public bool RunTenMinuteUpdateForLargeForwardJumps { get; set; } = true;
    public int MaxTenMinuteCatchupSteps { get; set; } = 24;
    public bool ForceStepCatchupForLargeJumps { get; set; } = false;
    public bool UseShouldTimePassCheck { get; set; } = true;
    public int NpcWakeEarliestTime { get; set; } = 700;
    public bool NPCFixOnLoad { get; set; } = true;
    public bool NPCFixOnLargeCorrection { get; set; } = true;
    public int NpcFixCooldownSeconds { get; set; } = 10;
    public NPCWarpAggressiveness NPCWarpAggressiveness { get; set; } = NPCWarpAggressiveness.Conservative;
    public bool ShowCompatibilityWarnings { get; set; } = true;
    public bool DebugLogging { get; set; } = false;
}

public enum RoundMode
{
    Nearest10,
    Down10,
    Up10
}

public enum LargeDriftBehavior
{
    HardSnap,
    SoftCatchup
}

public enum NPCWarpAggressiveness
{
    Conservative,
    Aggressive
}
