namespace PanningMaster
{
    public class ModConfig
    {
        public bool ModEnabled { get; set; } = true;
        public bool DebugMode { get; set; } = false;

        // Monday
        public int Mon_OreAmount { get; set; } = 2;
        public bool Mon_CommonMaterials { get; set; } = true;

        // Tuesday
        public int Tue_OreAmount { get; set; } = 3;
        public bool Tue_BonusCoal { get; set; } = true;
        public int Tue_CoalAmount { get; set; } = 3;

        // Wednesday
        public int Wed_OreAmount { get; set; } = 5;
        public bool Wed_CopperIron { get; set; } = true;

        // Thursday
        public int Thu_OreAmount { get; set; } = 7;
        public bool Thu_GoldChance { get; set; } = true;

        // Friday
        public int Fri_OreAmount { get; set; } = 8;
        public bool Fri_IridiumChance { get; set; } = true;

        // Saturday
        public int Sat_OreAmount { get; set; } = 9;
        public float Sat_PrismaticChance { get; set; } = 0.05f;

        // Sunday
        public int Sun_OreAmount { get; set; } = 10;
        public bool Sun_GuaranteedGem { get; set; } = true;
    }
}