namespace AutoFillPetBowl;

public sealed class ModConfig
{
    public bool Enabled { get; set; } = true;

    public bool OnlyFillIfEmpty { get; set; } = true;

    /// <summary>Best-effort duration if the bowl uses a "days remaining" field.</summary>
    public int FillDurationDays { get; set; } = 1;

    public bool ShowHudMessage { get; set; } = false;

    public bool DebugLogging { get; set; } = false;
}
