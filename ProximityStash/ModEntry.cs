using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;
using StardewValley.Locations;

namespace ProximityStash
{
    public class ModEntry : Mod
    {
        private ModConfig config = new();

        private int soundCooldownTimer = 0;
        private bool wasInChestMenu = false;
        private double pauseLogicUntil = 0;

        public override void Entry(IModHelper helper)
        {
            this.config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu == null) return;

            configMenu.Register(
                mod: this.ModManifest,
                reset: () => this.config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.config)
            );

            // Toggle: Mod Enabled
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => this.Helper.Translation.Get("config.enabled.name"),
                tooltip: () => this.Helper.Translation.Get("config.enabled.tooltip"),
                getValue: () => this.config.ModEnabled,
                setValue: value => this.config.ModEnabled = value
            );

            // Toggle: HUD Messages
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => this.Helper.Translation.Get("config.hud-messages.name"),
                tooltip: () => this.Helper.Translation.Get("config.hud-messages.tooltip"),
                getValue: () => this.config.ShowHudMessages,
                setValue: value => this.config.ShowHudMessages = value
            );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => this.Helper.Translation.Get("config.range.name"),
                tooltip: () => this.Helper.Translation.Get("config.range.tooltip"),
                getValue: () => this.config.TriggerRange,
                setValue: value => this.config.TriggerRange = value,
                min: 1f, max: 10f, interval: 0.5f
            );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => this.Helper.Translation.Get("config.sound-cooldown.name"),
                tooltip: () => this.Helper.Translation.Get("config.sound-cooldown.tooltip"),
                getValue: () => this.config.SoundCooldown,
                setValue: value => this.config.SoundCooldown = value,
                min: 0, max: 300
            );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => this.Helper.Translation.Get("config.menu-cooldown.name"),
                tooltip: () => this.Helper.Translation.Get("config.menu-cooldown.tooltip"),
                getValue: () => this.config.MenuExitCooldownSeconds,
                setValue: value => this.config.MenuExitCooldownSeconds = value,
                min: 0.0f, max: 30.0f, interval: 0.5f
            );
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady) return;

            bool isInChestMenu = Game1.activeClickableMenu is ItemGrabMenu;

            if (wasInChestMenu && !isInChestMenu)
            {
                pauseLogicUntil = Game1.currentGameTime.TotalGameTime.TotalSeconds + config.MenuExitCooldownSeconds;
            }
            wasInChestMenu = isInChestMenu;

            if (!config.ModEnabled) return;
            if (!Context.IsPlayerFree) return;
            if (!e.IsMultipleOf(15)) return;

            if (soundCooldownTimer > 0) soundCooldownTimer -= 15;

            if (Game1.currentGameTime.TotalGameTime.TotalSeconds < pauseLogicUntil) return;

            AutoStashLogic();
        }

        private void AutoStashLogic()
        {
            var location = Game1.currentLocation;
            if (location == null) return;

            Vector2 playerPos = Game1.player.Tile;
            bool globalMovedAny = false;

            // Check Standard Chests
            foreach (var obj in location.objects.Values)
            {
                if (obj is Chest chest && chest.playerChest.Value)
                {
                    if (chest.GetMutex().IsLocked()) continue;

                    float distance = Vector2.Distance(playerPos, obj.TileLocation);
                    if (distance <= config.TriggerRange)
                    {
                        if (TryStashItems(chest)) globalMovedAny = true;
                    }
                }
            }

            // Check Fridge
            if (location is FarmHouse farmHouse && farmHouse.fridge.Value != null)
            {
                Vector2 fridgeTile = farmHouse.fridgePosition.ToVector2();
                if (fridgeTile == Vector2.Zero) fridgeTile = new Vector2(5, 4);

                float distance = Vector2.Distance(playerPos, fridgeTile);
                if (distance <= config.TriggerRange)
                {
                    if (!farmHouse.fridge.Value.GetMutex().IsLocked())
                    {
                        if (TryStashItems(farmHouse.fridge.Value)) globalMovedAny = true;
                    }
                }
            }

            if (globalMovedAny && soundCooldownTimer <= 0)
            {
                Game1.playSound("coin");
                soundCooldownTimer = config.SoundCooldown;
            }
        }

        private bool TryStashItems(Chest chest)
        {
            bool movedAny = false;
            var inventory = Game1.player.Items;

            Dictionary<string, int> transferLog = new Dictionary<string, int>();

            for (int i = inventory.Count - 1; i >= 0; i--)
            {
                Item item = inventory[i];
                if (item == null) continue;
                if (item is Tool) continue;

                bool chestHasMatch = chest.Items.Any(chestItem =>
                    chestItem != null && chestItem.canStackWith(item));

                if (chestHasMatch)
                {
                    string itemName = item.DisplayName;
                    int originalStack = item.Stack;
                    Item leftover = chest.addItem(item);

                    int amountMoved = originalStack;
                    if (leftover != null) amountMoved -= leftover.Stack;

                    if (amountMoved > 0)
                    {
                        if (transferLog.ContainsKey(itemName))
                            transferLog[itemName] += amountMoved;
                        else
                            transferLog[itemName] = amountMoved;

                        if (leftover == null)
                            Game1.player.Items[i] = null;
                        else
                            Game1.player.Items[i] = leftover;

                        movedAny = true;
                    }
                }
            }

            // Only show HUD messages if enabled in config
            if (config.ShowHudMessages)
            {
                foreach (var kvp in transferLog)
                {
                    string msg = this.Helper.Translation.Get("hud.items-moved", new
                    {
                        count = kvp.Value,
                        itemName = kvp.Key
                    });

                    Game1.addHUDMessage(new HUDMessage(msg, 1) { noIcon = true, timeLeft = 1000 });
                }
            }

            return movedAny;
        }
    }
}