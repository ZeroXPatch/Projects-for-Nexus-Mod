using StardewModdingAPI.Utilities;

namespace HorseMaster
{
    public class ModConfig
    {
        public bool DebugMode { get; set; } = false;
        public bool UseAdaptiveSpeed { get; set; } = false;

        // New: Teleport Shortcut (Default: None)
        public KeybindList SummonKey { get; set; } = new KeybindList();

        public int ConstantSpeed { get; set; } = 2;

        public int Speed_100 { get; set; } = 4;
        public int Speed_99_to_70 { get; set; } = 3;
        public int Speed_69_to_40 { get; set; } = 2;
        public int Speed_39_to_10 { get; set; } = 1;
        public int Speed_09_to_00 { get; set; } = -2;
    }
}