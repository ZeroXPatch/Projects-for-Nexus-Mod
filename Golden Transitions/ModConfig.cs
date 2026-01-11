namespace GoldenTransitions
{
    public enum RandomFrequency
    {
        Daily,
        Weekly,
        Seasonal
    }

    public class ModConfig
    {
        // --- GENERAL ---
        public bool EnableRandomMode { get; set; } = false;
        public RandomFrequency Frequency { get; set; } = RandomFrequency.Daily;

        // --- SPRING SETTINGS ---
        public int SpringR { get; set; } = 255;
        public int SpringG { get; set; } = 150; // Soft Peach/Pink
        public int SpringB { get; set; } = 100;
        public float SpringIntensity { get; set; } = 0.35f;
        public int SpringBuildUp { get; set; } = 45;
        public int SpringFadeOut { get; set; } = 120;

        // --- SUMMER SETTINGS ---
        public int SummerR { get; set; } = 255;
        public int SummerG { get; set; } = 180; // Bright Gold
        public int SummerB { get; set; } = 40;
        public float SummerIntensity { get; set; } = 0.45f;
        public int SummerBuildUp { get; set; } = 60; // Longer days
        public int SummerFadeOut { get; set; } = 150;

        // --- FALL SETTINGS ---
        public int FallR { get; set; } = 255;
        public int FallG { get; set; } = 90;  // Deep Red-Orange
        public int FallB { get; set; } = 40;
        public float FallIntensity { get; set; } = 0.30f;
        public int FallBuildUp { get; set; } = 45;
        public int FallFadeOut { get; set; } = 120;

        // --- WINTER SETTINGS ---
        public int WinterR { get; set; } = 240;
        public int WinterG { get; set; } = 200; // Pale/Cold Yellow
        public int WinterB { get; set; } = 180;
        public float WinterIntensity { get; set; } = 0.30f;
        public int WinterBuildUp { get; set; } = 30; // Short days
        public int WinterFadeOut { get; set; } = 90;
    }
}