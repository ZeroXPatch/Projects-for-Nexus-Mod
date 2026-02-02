namespace BackgroundTickThrottler
{
    public class ModConfig
    {
        public bool Enabled { get; set; } = true;

        // Default set to 2 as requested (Safe balance between performance and NPC speed)
        public int UpdateInterval { get; set; } = 2;

        public bool AlwaysUpdateVillagers { get; set; } = false;

        // Debug option to track mod effectiveness
        public bool EnableDebug { get; set; } = false;
    }
}