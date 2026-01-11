namespace GoldenTransitions
{
    public class ModConfig
    {
        // Default Color: Deep Sunset Red-Orange
        public int ColorRed { get; set; } = 255;
        public int ColorGreen { get; set; } = 90;
        public int ColorBlue { get; set; } = 40;

        // Visual Strength (0.0 to 1.0)
        public float PeakOpacity { get; set; } = 0.30f;

        // Visual Timing
        // Phase 1: Minutes BEFORE sunset to start glowing (Golden Hour)
        public int BuildUpMinutes { get; set; } = 45;

        // Phase 2: Minutes AFTER sunset to stop glowing (Twilight)
        public int FadeOutMinutes { get; set; } = 120;
    }
}