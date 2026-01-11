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
        // --- RANDOM MODE SETTINGS ---
        public bool EnableRandomMode { get; set; } = true;
        public RandomFrequency Frequency { get; set; } = RandomFrequency.Daily;

        // Random Ranges (Used when Random Mode is ON)
        public int SpringMinTime { get; set; } = 1700;
        public int SpringMaxTime { get; set; } = 1900;
        public int SummerMinTime { get; set; } = 1800;
        public int SummerMaxTime { get; set; } = 2030;
        public int FallMinTime { get; set; } = 1630;
        public int FallMaxTime { get; set; } = 1830;
        public int WinterMinTime { get; set; } = 1530;
        public int WinterMaxTime { get; set; } = 1700;

        // --- MANUAL MODE SETTINGS (Used when Random Mode is OFF) ---
        // Spring
        public int ManualSpringWeek1 { get; set; } = 1730;
        public int ManualSpringWeek2 { get; set; } = 1740;
        public int ManualSpringWeek3 { get; set; } = 1750;
        public int ManualSpringWeek4 { get; set; } = 1800;

        // Summer
        public int ManualSummerWeek1 { get; set; } = 1830;
        public int ManualSummerWeek2 { get; set; } = 1900;
        public int ManualSummerWeek3 { get; set; } = 1930;
        public int ManualSummerWeek4 { get; set; } = 1900;

        // Fall
        public int ManualFallWeek1 { get; set; } = 1800;
        public int ManualFallWeek2 { get; set; } = 1730;
        public int ManualFallWeek3 { get; set; } = 1700;
        public int ManualFallWeek4 { get; set; } = 1630;

        // Winter
        public int ManualWinterWeek1 { get; set; } = 1600;
        public int ManualWinterWeek2 { get; set; } = 1530;
        public int ManualWinterWeek3 { get; set; } = 1530;
        public int ManualWinterWeek4 { get; set; } = 1600;
    }
}