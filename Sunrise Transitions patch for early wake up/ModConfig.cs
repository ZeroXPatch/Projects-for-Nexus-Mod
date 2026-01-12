namespace SunriseTransitions
{
    public enum RandomFrequency
    {
        Daily,
        Weekly,
        Seasonal
    }

    public class ModConfig
    {
        public bool EnableRandomMode { get; set; } = false;
        public RandomFrequency Frequency { get; set; } = RandomFrequency.Daily;

        // --- SPRING ---
        public int SpringR { get; set; } = 255;
        public int SpringG { get; set; } = 218;
        public int SpringB { get; set; } = 185;
        public float SpringIntensity { get; set; } = 0.35f;
        public int SpringDuration { get; set; } = 180;
        public int SpringStartTime { get; set; } = 600; // 6:00 AM

        // --- SUMMER ---
        public int SummerR { get; set; } = 255;
        public int SummerG { get; set; } = 204;
        public int SummerB { get; set; } = 51;
        public float SummerIntensity { get; set; } = 0.25f;
        public int SummerDuration { get; set; } = 240;
        public int SummerStartTime { get; set; } = 600;

        // --- FALL ---
        public int FallR { get; set; } = 255;
        public int FallG { get; set; } = 204;
        public int FallB { get; set; } = 51;
        public float FallIntensity { get; set; } = 0.30f;
        public int FallDuration { get; set; } = 180;
        public int FallStartTime { get; set; } = 600;

        // --- WINTER ---
        public int WinterR { get; set; } = 224;
        public int WinterG { get; set; } = 247;
        public int WinterB { get; set; } = 250;
        public float WinterIntensity { get; set; } = 0.35f;
        public int WinterDuration { get; set; } = 180;
        public int WinterStartTime { get; set; } = 600;
    }
}