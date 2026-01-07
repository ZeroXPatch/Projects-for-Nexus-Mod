using StardewModdingAPI;

namespace HorseFollower
{
    public class ModConfig
    {
        public SButton ToggleKey { get; set; } = SButton.H;
        public float MovementSpeed { get; set; } = 6.5f;

        // Distance in pixels. 64px = 1 tile.
        // 192f = 3 tiles, 256f = 4 tiles.
        public float FollowDistance { get; set; } = 256f;

        // If horse is further than this, it teleports.
        public float TeleportThreshold { get; set; } = 1500f;

        // Toggle the teleport sound effect
        public bool PlayTeleportSound { get; set; } = true;
    }
}