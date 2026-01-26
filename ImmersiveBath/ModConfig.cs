namespace ImmersiveBath
{
    public class ModConfig
    {
        // UI (Integers)
        public bool ShowUI { get; set; } = true;
        public int UI_X { get; set; } = 20;
        public int UI_Y { get; set; } = 150;

        // Decay (Floats)
        public float DecayMultiplier { get; set; } = 1.0f;
        public float ToolUseDecay { get; set; } = 0.2f;

        // Buffs (Integers)
        public int CleanLuckBuff { get; set; } = 1;
        public int CleanFriendshipBonus { get; set; } = 5;
        public int DirtySpeedBuff { get; set; } = 1;
        public int DirtyDefenseBuff { get; set; } = 1;
        public int DirtyAttackBuff { get; set; } = 1;
        public int RevitalizedMaxEnergy { get; set; } = 30;
    }
}