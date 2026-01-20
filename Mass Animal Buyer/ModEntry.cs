using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.FarmAnimals;
using MassAnimalBuyer.UI;

namespace MassAnimalBuyer
{
    public class ModEntry : Mod
    {
        private ModConfig Config;

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();
            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        }

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            // GMCM Setup
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu != null)
            {
                configMenu.Register(
                    mod: this.ModManifest,
                    reset: () => this.Config = new ModConfig(),
                    save: () => this.Helper.WriteConfig(this.Config)
                );
                configMenu.AddKeybindList(
                    mod: this.ModManifest,
                    name: () => "Open Menu Key",
                    tooltip: () => "Press this key to open the Mass Animal Catalogue.",
                    getValue: () => this.Config.OpenMenuKey,
                    setValue: value => this.Config.OpenMenuKey = value
                );
            }
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady || !Context.IsPlayerFree) return;

            if (this.Config.OpenMenuKey.JustPressed())
            {
                // Load all buyable animals from game data
                List<string> buyableAnimals = new List<string>();
                var data = Game1.content.Load<Dictionary<string, FarmAnimalData>>("Data/FarmAnimals");

                foreach (var kvp in data)
                {
                    // Only show animals that have a price (Buyable)
                    if (kvp.Value.PurchasePrice > 0)
                    {
                        buyableAnimals.Add(kvp.Key);
                    }
                }

                if (buyableAnimals.Count > 0)
                {
                    Game1.activeClickableMenu = new AnimalCatalogueMenu(buyableAnimals, this.Helper);
                }
                else
                {
                    Game1.addHUDMessage(new HUDMessage("No buyable animals found in Data/FarmAnimals!", 3));
                }
            }
        }
    }
}