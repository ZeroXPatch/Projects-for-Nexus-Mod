using System;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace SlimeHutchAutoWater
{
    public class ModEntry : Mod
    {
        private ModConfig Config = null!;

        public override void Entry(IModHelper helper)
        {
            this.Config = this.Helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null) return;

            configMenu.Register(
                mod: this.ModManifest,
                reset: () => this.Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.Config)
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Enabled",
                tooltip: () => "Whether the troughs should be filled automatically every morning.",
                getValue: () => this.Config.Enabled,
                setValue: value => this.Config.Enabled = value
            );
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            if (!this.Config.Enabled) return;

            // In 1.6, Utility.ForEachLocation is the best way to find all locations.
            // SlimeHutch is recognized automatically because of 'using StardewValley;'
            Utility.ForEachLocation((location) =>
            {
                if (location is SlimeHutch hutch)
                {
                    this.WaterHutch(hutch);
                }
                return true;
            });
        }

        private void WaterHutch(SlimeHutch hutch)
        {
            for (int i = 0; i < hutch.waterSpots.Count; i++)
            {
                hutch.waterSpots[i] = true;
            }
        }
    }
}