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

        // --- SPRING: "Soft Peach" (Cozy, Romantic) ---
        // RGB: 255, 218, 185
        public int SpringR { get; set; } = 255;
        public int SpringG { get; set; } = 218;
        public int SpringB { get; set; } = 185;
        public float SpringIntensity { get; set; } = 0.35f;
        public int SpringDuration { get; set; } = 180; // 3 Hours

        // --- SUMMER: "Golden Hour" (Bright, Energetic) ---
        // RGB: 255, 204, 51
        public int SummerR { get; set; } = 255;
        public int SummerG { get; set; } = 204;
        public int SummerB { get; set; } = 51;
        public float SummerIntensity { get; set; } = 0.25f; // Lower intensity because yellow is very bright
        public int SummerDuration { get; set; } = 240; // 4 Hours (Long Summer Morning)

        // --- FALL: "Golden Hour" (Matches the leaves) ---
        // Uses the same Gold as Summer, but slightly stronger intensity for hazy harvest vibes.
        public int FallR { get; set; } = 255;
        public int FallG { get; set; } = 204;
        public int FallB { get; set; } = 51;
        public float FallIntensity { get; set; } = 0.30f;
        public int FallDuration { get; set; } = 180;

        // --- WINTER: "Blue Hour" (Cinematic Mist) ---
        // RGB: 224, 247, 250
        public int WinterR { get; set; } = 224;
        public int WinterG { get; set; } = 247;
        public int WinterB { get; set; } = 250;
        public float WinterIntensity { get; set; } = 0.35f;
        public int WinterDuration { get; set; } = 180;
    }
}