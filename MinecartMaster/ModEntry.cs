using System;
using System.Collections.Generic;
using GenericModConfigMenu;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace MinecartMaster
{
    public class ModEntry : Mod
    {
        private ModConfig Config = new();
        private bool wasMinecartInteraction = false;

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.Display.MenuChanged += OnMenuChanged;
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            if (!this.Config.ModEnabled)
                return;

            // If unlock early is enabled, add the mail flag
            if (this.Config.UnlockMinecartsEarly && !AreMinecartsUnlocked())
            {
                Game1.MasterPlayer.mailReceived.Add("ccBoilerRoom");
            }
        }

        private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
        {
            // Check if a minecart dialogue menu was opened
            if (e.NewMenu is StardewValley.Menus.DialogueBox dialogueBox)
            {
                string currentString = dialogueBox.getCurrentString() ?? "";

                // Check if this is a minecart question
                if (currentString.Contains(Game1.content.LoadString("Strings\\Locations:MineCart_ChooseDestination")) ||
                    currentString.ToLower().Contains("destination"))
                {
                    this.wasMinecartInteraction = true;

                    // Check schedule - if closed, replace the menu with a closed message
                    if (!IsMinecartOpenNow(Game1.timeOfDay))
                    {
                        Game1.activeClickableMenu = null;
                        Game1.drawObjectDialogue(this.Helper.Translation.Get("message.locked"));
                        this.wasMinecartInteraction = false;
                        return;
                    }

                    // If there's a travel cost, check if player can afford it
                    if (this.Config.TravelCost > 0)
                    {
                        if (Game1.player.Money < this.Config.TravelCost)
                        {
                            Game1.activeClickableMenu = null;
                            string message = this.Helper.Translation.Get("message.insufficient_funds");
                            message = string.Format(message, this.Config.TravelCost);
                            Game1.drawObjectDialogue(message);
                            this.wasMinecartInteraction = false;
                            return;
                        }

                        // Charge the player
                        Game1.player.Money -= this.Config.TravelCost;

                        // Play purchase sound
                        Game1.playSound("purchase");
                    }
                }
            }
            else if (e.OldMenu != null && e.NewMenu == null && this.wasMinecartInteraction)
            {
                // Menu closed after minecart interaction
                this.wasMinecartInteraction = false;
            }
        }

        private bool AreMinecartsUnlocked()
        {
            return Game1.MasterPlayer.mailReceived.Contains("ccBoilerRoom") ||
                   Game1.MasterPlayer.mailReceived.Contains("jojaBoilerRoom");
        }

        private bool IsMinecartOpenNow(int time)
        {
            if (!this.Config.UseAdaptiveSchedule) return true;
            if (time >= 600 && time < 900) return this.Config.Open_0600_to_0900;
            if (time >= 900 && time < 1200) return this.Config.Open_0900_to_1200;
            if (time >= 1200 && time < 1700) return this.Config.Open_1200_to_1700;
            if (time >= 1700 && time < 2400) return this.Config.Open_1700_to_2400;
            if (time >= 2400 || time < 600) return this.Config.Open_2400_to_0200;
            return true;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null) return;

            configMenu.Register(this.ModManifest, () => this.Config = new ModConfig(), () => this.Helper.WriteConfig(this.Config));

            configMenu.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("config.section.general"));

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => this.Config.ModEnabled,
                setValue: val => this.Config.ModEnabled = val,
                name: () => this.Helper.Translation.Get("config.enabled.name"),
                tooltip: () => this.Helper.Translation.Get("config.enabled.tooltip")
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => this.Config.UnlockMinecartsEarly,
                setValue: val => {
                    this.Config.UnlockMinecartsEarly = val;
                    // If enabling, add mail immediately if in a save
                    if (val && Context.IsWorldReady && !AreMinecartsUnlocked())
                    {
                        Game1.MasterPlayer.mailReceived.Add("ccBoilerRoom");
                    }
                },
                name: () => this.Helper.Translation.Get("config.unlock_early.name"),
                tooltip: () => this.Helper.Translation.Get("config.unlock_early.tooltip")
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => this.Config.UseAdaptiveSchedule,
                setValue: val => this.Config.UseAdaptiveSchedule = val,
                name: () => this.Helper.Translation.Get("config.adaptive.name"),
                tooltip: () => this.Helper.Translation.Get("config.adaptive.tooltip")
            );

            // Add Number Option for Travel Cost using reflection since GMCM doesn't expose AddNumberOption in the interface
            var gmcmType = configMenu.GetType();
            var addNumberOption = gmcmType.GetMethod("AddNumberOption");
            if (addNumberOption != null)
            {
                addNumberOption.Invoke(configMenu, new object[] {
                    this.ModManifest,
                    (Func<int>)(() => this.Config.TravelCost),
                    (Action<int>)(val => this.Config.TravelCost = val),
                    (Func<string>)(() => this.Helper.Translation.Get("config.travel_cost.name")),
                    (Func<string>)(() => this.Helper.Translation.Get("config.travel_cost.tooltip")),
                    0,     // min
                    10000, // max
                    1,     // interval
                    null   // fieldId
                });
            }

            configMenu.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("config.section.schedule"));
            configMenu.AddParagraph(this.ModManifest, () => this.Helper.Translation.Get("config.schedule.note"));

            configMenu.AddBoolOption(this.ModManifest, () => this.Config.Open_0600_to_0900, val => this.Config.Open_0600_to_0900 = val, () => this.Helper.Translation.Get("config.time.6to9"));
            configMenu.AddBoolOption(this.ModManifest, () => this.Config.Open_0900_to_1200, val => this.Config.Open_0900_to_1200 = val, () => this.Helper.Translation.Get("config.time.9to12"));
            configMenu.AddBoolOption(this.ModManifest, () => this.Config.Open_1200_to_1700, val => this.Config.Open_1200_to_1700 = val, () => this.Helper.Translation.Get("config.time.12to5"));
            configMenu.AddBoolOption(this.ModManifest, () => this.Config.Open_1700_to_2400, val => this.Config.Open_1700_to_2400 = val, () => this.Helper.Translation.Get("config.time.5to12"));
            configMenu.AddBoolOption(this.ModManifest, () => this.Config.Open_2400_to_0200, val => this.Config.Open_2400_to_0200 = val, () => this.Helper.Translation.Get("config.time.12to2"));
        }
    }
}