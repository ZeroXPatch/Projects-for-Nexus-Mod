namespace StartupOptimizer;

public class ModConfig
{
    // Default: OnFirstLaunch so it "just works" when people install the mod.
    public AutoOpenLoadMenuMode AutoOpenLoadMenu { get; set; } = AutoOpenLoadMenuMode.OnFirstLaunch;
}

public enum AutoOpenLoadMenuMode
{
    Off,
    OnFirstLaunch
}
