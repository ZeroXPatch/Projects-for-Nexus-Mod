using StardewModdingAPI.Utilities;

namespace MassAnimalBuyer
{
    public class ModConfig
    {
        // Default hotkey is 'P'
        public KeybindList OpenMenuKey { get; set; } = KeybindList.Parse("F7");
    }
}