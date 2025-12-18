namespace NegativeRelations
{
    public class ModConfig
    {
        public bool EnableMod { get; set; } = true;

        public int RecoveryPerDay { get; set; } = 5;

        public int BarkRadiusTiles { get; set; } = 3;

        public float BarkChance { get; set; } = 0.08f;

        public int BarkCooldownMinutes { get; set; } = 8;

        public float TalkOverrideChance { get; set; } = 0.30f;

        public bool EnableBarks { get; set; } = true;

        public bool EnableTalkOverride { get; set; } = true;
    }
}
