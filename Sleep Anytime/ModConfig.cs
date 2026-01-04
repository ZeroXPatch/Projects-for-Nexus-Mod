using StardewModdingAPI;

namespace SleepAnywhereMod
{
    public class ModConfig
    {
        public bool ShowButton { get; set; } = true;
        public int ButtonXPosition { get; set; } = 64;
        public int ButtonYPosition { get; set; } = 200;
        public SButton SleepKey { get; set; } = SButton.F9;
    }
}