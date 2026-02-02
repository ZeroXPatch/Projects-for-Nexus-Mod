namespace FocusedWeather
{
    /// <summary>The mod configuration settings.</summary>
    public class ModConfig
    {
        /// <summary>How often to check and clear weather debris (in ticks). Lower = more aggressive, higher = less overhead.</summary>
        public uint UpdateFrequency { get; set; } = 30;

        /// <summary>Whether to log debug information about weather debris management.</summary>
        public bool EnableDebugLogging { get; set; } = true;
    }
}