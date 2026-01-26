namespace SpeedMod
{
    public class ModConfig
    {
        public bool DebugMode { get; set; } = false;
        public bool UseAdaptiveSpeed { get; set; } = false;

        // Changed from Multiplier (0.5) to direct Speed (+1, +2...)
        public int ConstantSpeed { get; set; } = 1;

        // Adaptive Schedules
        public int Speed_0600_to_0900 { get; set; } = 1;
        public int Speed_0900_to_1200 { get; set; } = 2;
        public int Speed_1200_to_1700 { get; set; } = 3;
        public int Speed_1700_to_2400 { get; set; } = 3;
        public int Speed_2400_to_2600 { get; set; } = 5;
    }
}