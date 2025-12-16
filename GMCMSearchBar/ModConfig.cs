using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace GMCMSearchBar
{
    internal sealed class ModConfig
    {
        public KeybindList OpenSearchMenuKey { get; set; } = new KeybindList(SButton.F6);

        public bool ShowUniqueId { get; set; } = true;

        // IMPORTANT for “multiple configs”: many of those are content packs
        public bool IncludeContentPacks { get; set; } = true;
    }
}
