namespace StardewHighPriority
{
    public sealed class ModConfig
    {
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Allowed: "Normal", "AboveNormal", "High"
        /// (Also accepts "Above Normal")
        /// </summary>
        public string Priority { get; set; } = "High";

        public bool LogSuccess { get; set; } = true;
        public bool LogFailure { get; set; } = true;
    }
}
