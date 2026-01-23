using System;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.TerrainFeatures;
using SObject = StardewValley.Object;

namespace FarmCleaner
{
    public class ModEntry : Mod
    {
        private ModConfig Config = null!;

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.Input.ButtonsChanged += OnButtonsChanged;
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

            // Note: We use lambda functions () => ... so the language updates instantly if the user changes it in-game.

            configMenu.AddSectionTitle(
                mod: this.ModManifest,
                text: () => this.Helper.Translation.Get("config.section.controls")
            );

            configMenu.AddKeybindList(
                mod: this.ModManifest,
                getValue: () => this.Config.CleanKey,
                setValue: value => this.Config.CleanKey = value,
                name: () => this.Helper.Translation.Get("config.key.name"),
                tooltip: () => this.Helper.Translation.Get("config.key.tooltip")
            );

            configMenu.AddSectionTitle(
                mod: this.ModManifest,
                text: () => this.Helper.Translation.Get("config.section.cleaning")
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => this.Config.ClearStones,
                setValue: val => this.Config.ClearStones = val,
                name: () => this.Helper.Translation.Get("config.stones.name"),
                tooltip: () => this.Helper.Translation.Get("config.stones.tooltip")
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => this.Config.ClearTwigs,
                setValue: val => this.Config.ClearTwigs = val,
                name: () => this.Helper.Translation.Get("config.twigs.name"),
                tooltip: () => this.Helper.Translation.Get("config.twigs.tooltip")
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => this.Config.ClearWeeds,
                setValue: val => this.Config.ClearWeeds = val,
                name: () => this.Helper.Translation.Get("config.weeds.name"),
                tooltip: () => this.Helper.Translation.Get("config.weeds.tooltip")
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => this.Config.ClearSaplings,
                setValue: val => this.Config.ClearSaplings = val,
                name: () => this.Helper.Translation.Get("config.saplings.name"),
                tooltip: () => this.Helper.Translation.Get("config.saplings.tooltip")
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => this.Config.ClearGrass,
                setValue: val => this.Config.ClearGrass = val,
                name: () => this.Helper.Translation.Get("config.grass.name"),
                tooltip: () => this.Helper.Translation.Get("config.grass.tooltip")
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => this.Config.ClearStumps,
                setValue: val => this.Config.ClearStumps = val,
                name: () => this.Helper.Translation.Get("config.stumps.name"),
                tooltip: () => this.Helper.Translation.Get("config.stumps.tooltip")
            );
        }

        private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
        {
            if (!Context.IsWorldReady) return;

            if (this.Config.CleanKey.JustPressed())
            {
                CleanFarm();
            }
        }

        private void CleanFarm()
        {
            Farm farm = Game1.getFarm();
            int itemsRemoved = 0;

            // Log message using translation
            this.Monitor.Log(this.Helper.Translation.Get("msg.scanning"), LogLevel.Info);

            // --- 1. Objects (Stones, Twigs, Weeds) ---
            for (int i = farm.objects.Count() - 1; i >= 0; i--)
            {
                var pair = farm.objects.Pairs.ElementAt(i);
                SObject obj = pair.Value;
                bool remove = false;

                if (this.Config.ClearWeeds && obj.IsWeeds()) remove = true;
                else if (this.Config.ClearStones && (obj.Name.Contains("Stone") || obj.ItemId == "343" || obj.ItemId == "450")) remove = true;
                else if (this.Config.ClearTwigs && (obj.Name.Contains("Twig") || obj.ItemId == "294" || obj.ItemId == "295")) remove = true;

                if (remove)
                {
                    farm.objects.Remove(pair.Key);
                    itemsRemoved++;
                }
            }

            // --- 2. Terrain Features (Trees, Grass) ---
            for (int i = farm.terrainFeatures.Count() - 1; i >= 0; i--)
            {
                var pair = farm.terrainFeatures.Pairs.ElementAt(i);
                TerrainFeature feature = pair.Value;
                bool remove = false;

                if (feature is HoeDirt) continue;

                if (this.Config.ClearSaplings && feature is Tree tree && tree.growthStage.Value < 5) remove = true;
                if (this.Config.ClearGrass && feature is Grass) remove = true;

                if (remove)
                {
                    farm.terrainFeatures.Remove(pair.Key);
                    itemsRemoved++;
                }
            }

            // --- 3. Resource Clumps (Large Stumps) ---
            if (this.Config.ClearStumps)
            {
                for (int i = farm.resourceClumps.Count - 1; i >= 0; i--)
                {
                    var clump = farm.resourceClumps[i];
                    bool remove = false;

                    if (clump.parentSheetIndex.Value == 600 || clump.parentSheetIndex.Value == 602) remove = true;
                    else if (clump.parentSheetIndex.Value == 672 && this.Config.ClearStones) remove = true;

                    if (remove)
                    {
                        farm.resourceClumps.RemoveAt(i);
                        itemsRemoved++;
                    }
                }
            }

            if (itemsRemoved > 0)
                // Pass the count 'itemsRemoved' to the translation: "Deleted {0} debris objects."
                Game1.addHUDMessage(new HUDMessage(this.Helper.Translation.Get("msg.cleared", new { count = itemsRemoved }), HUDMessage.achievement_type));
            else
                this.Monitor.Log(this.Helper.Translation.Get("msg.none_found"), LogLevel.Info);
        }
    }
}