namespace LandFishSwimmers
{
    internal sealed class ModConfig
    {
        public bool Enabled { get; set; } = true;

        // How many fish to keep active in each location.
        public int FishPerLocation { get; set; } = 10;

        // If false, only outdoors locations will get land fish.
        public bool SpawnIndoors { get; set; } = false;

        // If true, rebuild fish spawns each morning.
        public bool RespawnEachDay { get; set; } = true;

        // How often to update movement logic (in ticks; 60 ticks = ~1 second).
        public int UpdateTicks { get; set; } = 6;

        // Movement tuning
        public int WanderRadiusTiles { get; set; } = 12;
        public float SpeedPixelsPerUpdate { get; set; } = 2.4f;

        // Visual tuning
        public float Scale { get; set; } = 1.0f;
        public float Opacity { get; set; } = 1.0f;
        public float BobPixels { get; set; } = 2.5f;
        public float WiggleRadians { get; set; } = 0.08f;

        // Optional: set this to force specific fish item IDs (object IDs).
        // Example: [145, 136, 143]
        public int[] FishObjectIds { get; set; } = System.Array.Empty<int>();
    }
}
