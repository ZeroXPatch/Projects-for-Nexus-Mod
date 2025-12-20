namespace StardewPriority
{
    public sealed class ModConfig
    {
        public bool Enabled { get; set; } = true;

        /// <summary>Allowed: "Normal", "AboveNormal", "High"</summary>
        public string FocusedPriority { get; set; } = "High";

        /// <summary>Allowed: "Normal", "AboveNormal", "High"</summary>
        public string UnfocusedPriority { get; set; } = "Normal";

        public bool LogSuccess { get; set; } = true;
        public bool LogFailure { get; set; } = true;
    }
}
