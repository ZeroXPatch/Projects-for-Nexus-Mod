namespace WildSwimmingDucks
{
    public class ModConfig
    {
        public int SpawnChancePercent { get; set; } = 30;
        public int MinDucks { get; set; } = 1;
        public int MaxDucks { get; set; } = 5;
        public bool EnableInOcean { get; set; } = true;
        public bool EnableInRiver { get; set; } = true;
        public bool EnableInLake { get; set; } = true;
    }
}