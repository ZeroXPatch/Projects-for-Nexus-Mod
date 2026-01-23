namespace NPCsTrashBack
{
    public class ModConfig
    {
        public bool Enabled { get; set; } = true;

        // Default 0.5 = 50%
        public float BaseChance { get; set; } = 0.50f;

        public bool EnableFriendshipScaling { get; set; } = true;

        // Default 0.03 = 3% reduction per heart
        public float ReductionPerHeart { get; set; } = 0.03f;
    }
}