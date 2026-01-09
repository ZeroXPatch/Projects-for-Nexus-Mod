namespace GiftBack
{
    public class ModConfig
    {
        public bool Enabled { get; set; } = true;

        // Updated: 0.01 = 1%
        public float BaseChance { get; set; } = 0.01f;

        public bool EnableFriendshipScaling { get; set; } = true;

        // Updated: 0.005 = 0.5% extra per heart
        public float ChancePerHeart { get; set; } = 0.005f;

        public int MaxGiftValue { get; set; } = 500;
    }
}