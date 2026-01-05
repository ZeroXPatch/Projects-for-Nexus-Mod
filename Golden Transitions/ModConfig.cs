namespace GoldenTransitions
{
    public class ModConfig
    {
        // When does the effect start relative to the sunset time?
        // 0 = Starts exactly when the game begins to darken.
        // -60 = Starts 1 hour before sunset (Golden Hour).
        public int StartOffsetMinutes { get; set; } = 0;

        // The intensity (0% to 100%) at specific checkpoints of the 2-hour window.
        // The code smooths the values between these points automatically.
        public int IntensityAt0Min { get; set; } = 0;   // Start
        public int IntensityAt30Min { get; set; } = 30; // +30 mins
        public int IntensityAt60Min { get; set; } = 45; // +1 hour (Peak?)
        public int IntensityAt90Min { get; set; } = 30; // +1.5 hours
        public int IntensityAt120Min { get; set; } = 0; // +2 hours (End)
    }
}