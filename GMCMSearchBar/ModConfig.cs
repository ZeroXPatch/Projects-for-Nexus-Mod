using StardewModdingAPI.Utilities;

namespace GMCMSearchBar
{
    internal sealed class ModConfig
    {
        public KeybindList OpenSearchMenuKey { get; set; } = KeybindList.Parse("F2");
        public bool ShowUniqueId { get; set; } = true;
    }
}
