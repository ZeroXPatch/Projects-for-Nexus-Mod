using StardewModdingAPI.Utilities;

namespace MassAnimalMover
{
    public class ModConfig
    {
        // Default is "Z". Users can change this by editing config.json manually.
        public KeybindList OpenMenuKey { get; set; } = KeybindList.Parse("Z");
    }
}