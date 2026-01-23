namespace CombatLevelScaling
{
    public class ModConfig
    {
        public bool ShowDebugInfo { get; set; } = false;

        // Stats
        public float StatIncreasePerLevel { get; set; } = 0.05f;

        // Locations (Wilderness Removed)
        public bool EnableInMines { get; set; } = true;
        public bool EnableInSkullCavern { get; set; } = true;
        public bool EnableInVolcano { get; set; } = true;

        // Spawn Rate
        public bool IncreaseSpawnRate { get; set; } = false;
        public float SpawnIncreasePerLevel { get; set; } = 0.03f;

        // Elite Monsters
        public bool EnableEliteMonsters { get; set; } = true;
        public float EliteChance { get; set; } = 0.01f;
        public float EliteStatMultiplier { get; set; } = 1.5f;
    }
}