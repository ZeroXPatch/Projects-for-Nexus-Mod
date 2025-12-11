using StardewModdingAPI;

namespace StartupOptimizer;

public class ModConfig
{
    public bool SkipLogosAndIntro { get; set; } = true;

    public bool SkipTitleIdle { get; set; } = true;

    public AutoOpenLoadMenuMode AutoOpenLoadMenu { get; set; } = AutoOpenLoadMenuMode.Off;

    public bool EnableQuickResume { get; set; }

    public string? QuickResumeSaveName { get; set; }

    public SButton QuickResumeInterruptKey { get; set; } = SButton.LeftShift;

    public bool EnableDiagnosticsLogging { get; set; }
}

public enum AutoOpenLoadMenuMode
{
    Off,
    OnFirstLaunch,
    Always
}
