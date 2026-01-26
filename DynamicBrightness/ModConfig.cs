namespace DynamicBrightness
{
    public enum RandomFrequency
    {
        Daily,
        Weekly,
        Seasonal
    }

    public class ModConfig
    {
        // Global Toggle
        public bool EnableMod { get; set; } = true;

        // --- RANDOM SETTINGS ---
        public bool EnableRandomMode { get; set; } = true;
        public RandomFrequency Frequency { get; set; } = RandomFrequency.Daily;

        // Range restricted to negatives (Darker) or 0 (Normal)
        public int RandomMinPercentage { get; set; } = -20;
        public int RandomMaxPercentage { get; set; } = 0;

        // --- MANUAL WEEKLY SETTINGS ---
        // Spring (Mostly normal)
        public int SpringWeek1 { get; set; } = 0;
        public int SpringWeek2 { get; set; } = 0;
        public int SpringWeek3 { get; set; } = 0;
        public int SpringWeek4 { get; set; } = 0;

        // Summer (Normal)
        public int SummerWeek1 { get; set; } = 0;
        public int SummerWeek2 { get; set; } = 0;
        public int SummerWeek3 { get; set; } = 0;
        public int SummerWeek4 { get; set; } = 0;

        // Fall (Getting Darker)
        public int FallWeek1 { get; set; } = 0;
        public int FallWeek2 { get; set; } = -5;
        public int FallWeek3 { get; set; } = -10;
        public int FallWeek4 { get; set; } = -15;

        // Winter (Darkest)
        public int WinterWeek1 { get; set; } = -15;
        public int WinterWeek2 { get; set; } = -20;
        public int WinterWeek3 { get; set; } = -20;
        public int WinterWeek4 { get; set; } = -10;
    }
}