namespace DynamicDusk
{
    public enum RandomFrequency
    {
        Daily,
        Weekly,
        Seasonal
    }

    public class ModConfig
    {
        public bool EnableRandomMode { get; set; } = true;
        public RandomFrequency Frequency { get; set; } = RandomFrequency.Daily;

        // --- SPRING RANGE (Default: 5:00 PM - 7:00 PM) ---
        public int SpringMinTime { get; set; } = 1700;
        public int SpringMaxTime { get; set; } = 1900;

        // --- SUMMER RANGE (Default: 6:00 PM - 8:30 PM) ---
        public int SummerMinTime { get; set; } = 1800;
        public int SummerMaxTime { get; set; } = 2030;

        // --- FALL RANGE (Default: 4:30 PM - 6:30 PM) ---
        public int FallMinTime { get; set; } = 1630;
        public int FallMaxTime { get; set; } = 1830;

        // --- WINTER RANGE (Default: 3:30 PM - 5:00 PM) ---
        public int WinterMinTime { get; set; } = 1530;
        public int WinterMaxTime { get; set; } = 1700;

        // --- MANUAL FIXED SETTINGS (Only used if Random Mode is OFF) ---
        public int ManualSpringTime { get; set; } = 1800;
        public int ManualSummerTime { get; set; } = 1900;
        public int ManualFallTime { get; set; } = 1730;
        public int ManualWinterTime { get; set; } = 1630;
    }
}