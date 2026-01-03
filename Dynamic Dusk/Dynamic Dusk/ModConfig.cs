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

        public int RandomMinTime { get; set; } = 1600; // 4:00 PM
        public int RandomMaxTime { get; set; } = 2000; // 8:00 PM

        // Manual Settings
        public int ManualSpringTime { get; set; } = 1800;
        public int ManualSummerTime { get; set; } = 1900;
        public int ManualFallTime { get; set; } = 1730;
        public int ManualWinterTime { get; set; } = 1630;
    }
}