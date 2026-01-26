namespace ToolMaster
{
    public class ModConfig
    {
        public bool DebugMode { get; set; } = false;
        public bool UseAdaptive { get; set; } = false;

        // --- Constant Mode ---
        public int ConstantCost { get; set; } = 100;
        public int ConstantSpeed { get; set; } = 100;

        // --- Adaptive Mode (Cost %) ---
        public int Cost_0600_to_0900 { get; set; } = 100;
        public int Cost_0900_to_1700 { get; set; } = 100;
        public int Cost_1700_to_2400 { get; set; } = 100;
        public int Cost_2400_to_0600 { get; set; } = 100;

        // --- Adaptive Mode (Speed %) ---
        public int Speed_0600_to_0900 { get; set; } = 100;
        public int Speed_0900_to_1700 { get; set; } = 100;
        public int Speed_1700_to_2400 { get; set; } = 100;
        public int Speed_2400_to_0600 { get; set; } = 100;
    }
}