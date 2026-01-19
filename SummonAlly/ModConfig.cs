using StardewModdingAPI.Utilities;

namespace GhostAlly
{
    public class ModConfig
    {
        public KeybindList SummonKey { get; set; } = KeybindList.Parse("V");
        public int EnergyCost { get; set; } = 20;
        public int DurationSeconds { get; set; } = 120; // Default 2 minutes (~2 in-game hours)

        // Ghost Stats
        public int GhostMaxHP { get; set; } = 200;
        public int GhostDamage { get; set; } = 25;
        public float AttackCooldown { get; set; } = 1.0f;
        public float DamageTakenMultiplier { get; set; } = 1.0f; // 1.0 = Normal, 0.5 = Half Damage

        public bool ShowMessage { get; set; } = true;
    }
}