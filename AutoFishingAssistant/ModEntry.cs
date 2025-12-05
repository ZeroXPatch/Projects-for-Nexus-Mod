using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;

namespace AutoFishingAssistant
{
    public class ModEntry : Mod
    {
        private const int FishCategory = -4;
        private const int TrashCategory = -20;

        private ModConfig Config = new();
        private bool autoFishingEnabled;

        public override void Entry(IModHelper helper)
        {
            this.Config = this.Helper.ReadConfig<ModConfig>();
            this.autoFishingEnabled = this.Config.TriggerKeepAutoFish;

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.Input.ButtonsChanged += this.OnButtonsChanged;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm is null)
                return;

            gmcm.Register(
                this.ModManifest,
                () =>
                {
                    this.Config = new ModConfig();
                    this.autoFishingEnabled = this.Config.TriggerKeepAutoFish;
                },
                () => this.Helper.WriteConfig(this.Config)
            );

            gmcm.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("config.section.behavior"));

            gmcm.AddBoolOption(
                this.ModManifest,
                () => this.Config.TriggerKeepAutoFish,
                value =>
                {
                    this.Config.TriggerKeepAutoFish = value;
                    this.autoFishingEnabled = value;
                },
                () => this.Helper.Translation.Get("config.autofish.enabled.name"),
                () => this.Helper.Translation.Get("config.autofish.enabled.tooltip")
            );

            gmcm.AddKeybindList(
                this.ModManifest,
                () => this.Config.KeepAutoFishKey,
                value => this.Config.KeepAutoFishKey = value,
                () => this.Helper.Translation.Get("config.autofish.key.name"),
                () => this.Helper.Translation.Get("config.autofish.key.tooltip")
            );

            gmcm.AddBoolOption(
                this.ModManifest,
                () => this.Config.MaxCastPower,
                value => this.Config.MaxCastPower = value,
                () => this.Helper.Translation.Get("config.maxCastPower.name"),
                () => this.Helper.Translation.Get("config.maxCastPower.tooltip")
            );

            gmcm.AddBoolOption(
                this.ModManifest,
                () => this.Config.AutoHit,
                value => this.Config.AutoHit = value,
                () => this.Helper.Translation.Get("config.autoHit.name"),
                () => this.Helper.Translation.Get("config.autoHit.tooltip")
            );

            gmcm.AddBoolOption(
                this.ModManifest,
                () => this.Config.FastBite,
                value => this.Config.FastBite = value,
                () => this.Helper.Translation.Get("config.fastBite.name"),
                () => this.Helper.Translation.Get("config.fastBite.tooltip")
            );

            // Kept in UI for now, but we don't touch the bar anymore.
            gmcm.AddBoolOption(
                this.ModManifest,
                () => this.Config.FasterSpeed,
                value => this.Config.FasterSpeed = value,
                () => this.Helper.Translation.Get("config.fasterSpeed.name"),
                () => this.Helper.Translation.Get("config.fasterSpeed.tooltip")
            );

            gmcm.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("config.section.loot"));

            gmcm.AddBoolOption(
                this.ModManifest,
                () => this.Config.AutoLootFishAndTrash,
                value => this.Config.AutoLootFishAndTrash = value,
                () => this.Helper.Translation.Get("config.autoLootFishAndTrash.name"),
                () => this.Helper.Translation.Get("config.autoLootFishAndTrash.tooltip")
            );

            gmcm.AddBoolOption(
                this.ModManifest,
                () => this.Config.AutoLootTreasure,
                value => this.Config.AutoLootTreasure = value,
                () => this.Helper.Translation.Get("config.autoLootTreasure.name"),
                () => this.Helper.Translation.Get("config.autoLootTreasure.tooltip")
            );
        }

        private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            if (this.Config.KeepAutoFishKey.JustPressed())
            {
                this.autoFishingEnabled = !this.autoFishingEnabled;
                string message = this.Helper.Translation.Get(this.autoFishingEnabled ? "hud.enabled" : "hud.disabled");
                Game1.addHUDMessage(new HUDMessage(message));
            }
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.player is null)
                return;

            // auto-loot after catch
            this.TryHandleAutoLoot();

            if (!this.autoFishingEnabled)
                return;

            if (Game1.player.CurrentTool is FishingRod rod)
            {
                this.ApplyRodTweaks(rod);
            }

            // IMPORTANT:
            // We no longer touch BobberBar here.
            // Mini-game stays fully vanilla; you move the green bar yourself.
        }

        private void ApplyRodTweaks(FishingRod rod)
        {
            // max power cast
            if (this.Config.MaxCastPower && rod.isCasting && rod.castingPower < 1f)
            {
                rod.castingPower = 1f;
            }

            // faster bite: gently cap time until bite
            if (this.Config.FastBite && rod.isFishing && !rod.isNibbling && rod.timeUntilFishingBite > 50f)
            {
                rod.timeUntilFishingBite = Math.Min(rod.timeUntilFishingBite, 50f);
            }

            // auto-hit: only simulate the “hit” when nibbling, not spamming
            if (this.Config.AutoHit && rod.isNibbling && !rod.isReeling)
            {
                this.TriggerHit(rod);
            }
        }

        private void TriggerHit(FishingRod rod)
        {
            try
            {
                // This is effectively “use fishing rod” at nibble time.
                rod.DoFunction(
                    Game1.currentLocation,
                    (int)Game1.player.Position.X,
                    (int)Game1.player.Position.Y,
                    1,
                    Game1.player
                );
            }
            catch (Exception ex)
            {
                this.Monitor.LogOnce($"Failed to auto-hit: {ex}", LogLevel.Trace);
            }
        }

        private void TryHandleAutoLoot()
        {
            if (!(this.Config.AutoLootFishAndTrash || this.Config.AutoLootTreasure))
                return;

            if (!Context.IsPlayerFree || Game1.currentLocation is null)
                return;

            foreach (var debris in Game1.currentLocation.debris.ToList())
            {
                var item = debris.item;
                if (item is null)
                    continue;

                bool isFishOrTrash = false;
                if (item is StardewValley.Object obj)
                {
                    isFishOrTrash =
                        obj.Category == FishCategory ||
                        obj.Category == TrashCategory ||
                        obj.HasContextTag("fish_item") ||
                        obj.HasContextTag("trash_item");
                }

                bool shouldCollect =
                    (isFishOrTrash && this.Config.AutoLootFishAndTrash)
                    || (!isFishOrTrash && this.Config.AutoLootTreasure);

                if (!shouldCollect)
                    continue;

                if (Game1.player.addItemToInventoryBool(item, false))
                {
                    Game1.currentLocation.debris.Remove(debris);
                }
            }
        }
    }

    public class ModConfig
    {
        public bool MaxCastPower { get; set; } = true;
        public bool AutoHit { get; set; } = true;
        public bool FastBite { get; set; } = true;

        // Left for future use / GMCM; currently not used to modify the bar.
        public bool FasterSpeed { get; set; } = true;

        public bool TriggerKeepAutoFish { get; set; } = true;
        public KeybindList KeepAutoFishKey { get; set; } = new(SButton.Insert);

        public bool AutoLootTreasure { get; set; } = true;
        public bool AutoLootFishAndTrash { get; set; } = true;
    }

    public interface IGenericModConfigMenuApi
    {
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);

        void AddSectionTitle(IManifest mod, Func<string> text, Func<string>? tooltip = null);

        void AddBoolOption(
            IManifest mod,
            Func<bool> getValue,
            Action<bool> setValue,
            Func<string> name,
            Func<string>? tooltip = null,
            string? fieldId = null
        );

        void AddKeybindList(
            IManifest mod,
            Func<KeybindList> getValue,
            Action<KeybindList> setValue,
            Func<string> name,
            Func<string>? tooltip = null,
            string? fieldId = null
        );
    }
}
