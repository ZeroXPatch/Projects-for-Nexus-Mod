namespace DynamicDusk
{
    public enum RandomFrequency { Daily, Weekly, Seasonal }

    public class ModConfig
    {
        public bool EnableRandomMode { get; set; } = true;

        // NEW: Option to enable the visual effect
        public bool EnableVibrantSunset { get; set; } = true;

        public RandomFrequency Frequency { get; set; } = RandomFrequency.Daily;

        // Spring
        public int SpringMinTime { get; set; } = 1700;
        public int SpringMaxTime { get; set; } = 1900;
        // Summer
        public int SummerMinTime { get; set; } = 1800;
        public int SummerMaxTime { get; set; } = 2030;
        // Fall
        public int FallMinTime { get; set; } = 1630;
        public int FallMaxTime { get; set; } = 1830;
        // Winter
        public int WinterMinTime { get; set; } = 1530;
        public int WinterMaxTime { get; set; } = 1700;

        // Manual
        public int ManualSpringTime { get; set; } = 1800;
        public int ManualSummerTime { get; set; } = 1900;
        public int ManualFallTime { get; set; } = 1730;
        public int ManualWinterTime { get; set; } = 1630;
    }
}