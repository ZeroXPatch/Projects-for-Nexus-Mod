namespace TrashCanExpanded
{
    public class ModConfig
    {
        public bool Enabled { get; set; } = true;

        // --- "Weekend Cleaning" Lore Defaults ---
        // Mon-Thu: 10% (Standard)
        // Fri: 25% (Villagers cleaning up)
        // Sat: 5% (Bins emptied)
        // Sun: 30% (Leftovers/High Loot)

        public float ChanceMonday { get; set; } = 0.10f;
        public float ChanceTuesday { get; set; } = 0.10f;
        public float ChanceWednesday { get; set; } = 0.10f;
        public float ChanceThursday { get; set; } = 0.10f;
        public float ChanceFriday { get; set; } = 0.25f;
        public float ChanceSaturday { get; set; } = 0.05f;
        public float ChanceSunday { get; set; } = 0.30f;

        public int MaxItemValue { get; set; } = 500;
    }
}