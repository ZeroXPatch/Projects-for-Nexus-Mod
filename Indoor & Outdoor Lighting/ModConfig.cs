namespace CustomNightLights
{
    public class ModConfig
    {
        // ============ OUTDOOR SETTINGS ============
        public bool EnableOutdoor { get; set; } = false;
        public int OutdoorRed { get; set; } = 100;
        public int OutdoorGreen { get; set; } = 255;
        public int OutdoorBlue { get; set; } = 100;
        public float OutdoorIntensity { get; set; } = 1.0f;
        public float OutdoorRadius { get; set; } = 1.5f;

        // ============ INDOOR SETTINGS ============
        public bool EnableIndoor { get; set; } = false; // Default off so houses look normal initially
        public int IndoorRed { get; set; } = 100;
        public int IndoorGreen { get; set; } = 255;
        public int IndoorBlue { get; set; } = 100;
        public float IndoorIntensity { get; set; } = 1.0f;
        public float IndoorRadius { get; set; } = 1.0f;
    }
}