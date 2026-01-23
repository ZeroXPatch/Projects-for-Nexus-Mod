using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace FarmCleaner
{
    public class ModConfig
    {
        // Default Key is 'O' for Organize/Cleanup
        public KeybindList CleanKey { get; set; } = KeybindList.Parse(SButton.Z.ToString());

        public bool ClearStones { get; set; } = true;
        public bool ClearTwigs { get; set; } = true;
        public bool ClearWeeds { get; set; } = true;
        public bool ClearGrass { get; set; } = false; // The grass animals eat
        public bool ClearSaplings { get; set; } = true; // Tree seeds/small trees
        public bool ClearStumps { get; set; } = false; // Large stumps
    }
}