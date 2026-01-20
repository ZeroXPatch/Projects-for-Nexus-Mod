namespace CombatLevelScaling
{
    public class ModConfig
    {
        public bool ShowDebugInfo { get; set; } = false; // New Debug Toggle

        public float StatIncreasePerLevel { get; set; } = 0.03f;

        public bool EnableInMines { get; set; } = true;
        public bool EnableInSkullCavern { get; set; } = false;
        public bool EnableInVolcano { get; set; } = true;
        public bool EnableInWilderness { get; set; } = true;

        public bool IncreaseSpawnRate { get; set; } = true;
        public float SpawnIncreasePerLevel { get; set; } = 0.02f;

        public bool EnableEliteMonsters { get; set; } = true;
        public float EliteChance { get; set; } = 0.05f;
        public float EliteStatMultiplier { get; set; } = 1.5f;
    }
}