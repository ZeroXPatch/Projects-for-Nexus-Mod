namespace StaminaMaster
{
    public class ModConfig
    {
        public bool DebugMode { get; set; } = false;
        public bool UseAdaptive { get; set; } = false;

        // GLOBAL SETTING
        public int RegenFrequencySeconds { get; set; } = 5; // Regen happens every 5 seconds by default

        // --- Constant Mode ---
        public int ConstantRegen { get; set; } = 1;
        public int ConstantDrainReduction { get; set; } = 0;

        // --- Adaptive Mode (Regen) ---
        public int Regen_0600_to_0900 { get; set; } = 2;
        public int Regen_0900_to_1200 { get; set; } = 0;
        public int Regen_1200_to_1700 { get; set; } = 0;
        public int Regen_1700_to_2400 { get; set; } = 0;
        public int Regen_2400_to_2600 { get; set; } = 0;

        // --- Adaptive Mode (Drain Reduction %) ---
        public int Drain_0600_to_0900 { get; set; } = 0;
        public int Drain_0900_to_1200 { get; set; } = 0;
        public int Drain_1200_to_1700 { get; set; } = 0;
        public int Drain_1700_to_2400 { get; set; } = 20;
        public int Drain_2400_to_2600 { get; set; } = 50;
    }
}