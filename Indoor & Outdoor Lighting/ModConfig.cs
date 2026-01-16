namespace CustomNightLights
{
    public class ModConfig
    {
        // ============ OUTDOOR SETTINGS ============
        public bool EnableOutdoor { get; set; } = false;
        public bool OutdoorNightOnly { get; set; } = true;
        public int OutdoorRed { get; set; } = 255;
        public int OutdoorGreen { get; set; } = 0;
        public int OutdoorBlue { get; set; } = 0;
        public float OutdoorIntensity { get; set; } = 0.7f;
        public float OutdoorRadius { get; set; } = 1.5f;

        // ============ INDOOR SETTINGS ============
        public bool EnableIndoor { get; set; } = false;
        public bool IndoorNightOnly { get; set; } = true;
        public bool IndoorFarmHouseOnly { get; set; } = true;
        public string IndoorExcludedLocations { get; set; } = "Greenhouse, Cellar";
        public int IndoorRed { get; set; } = 0;
        public int IndoorGreen { get; set; } = 255;
        public int IndoorBlue { get; set; } = 0;
        public float IndoorIntensity { get; set; } = 1.0f;
        public float IndoorRadius { get; set; } = 1.0f;
    }
}