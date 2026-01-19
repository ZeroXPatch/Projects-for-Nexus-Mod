using System;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace MassAnimalMover
{
    public class ModEntry : Mod
    {
        private ModConfig Config;

        public override void Entry(IModHelper helper)
        {
            // Read config.json (creates it if missing, editable manually)
            this.Config = helper.ReadConfig<ModConfig>();

            // Only listen for button presses
            helper.Events.Input.ButtonPressed += OnButtonPressed;
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            // Safety checks
            if (!Context.IsWorldReady || !Context.IsPlayerFree)
                return;

            // Check if the configured key was pressed
            if (this.Config.OpenMenuKey.JustPressed())
            {
                // Ensure UI is not already active
                if (Game1.activeClickableMenu == null)
                {
                    Game1.activeClickableMenu = new UI.TransferMenu();
                }
            }
        }
    }
}