using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;

namespace WoodPileStorage
{
    public class ModEntry : Mod
    {
        private Chest? virtualChest;
        private const string SaveKey = "wood-pile-inventory";

        // Configuration
        private ModConfig config = new();

        // Logic state
        private bool wasMenuOpen = false;
        private double cooldownTime = 0;

        public override void Entry(IModHelper helper)
        {
            this.config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.GameLoop.Saving += OnSaving;
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

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => this.Helper.Translation.Get("config.enable-auto-deposit.name"),
                tooltip: () => this.Helper.Translation.Get("config.enable-auto-deposit.tooltip"),
                getValue: () => this.config.EnableAutoDeposit,
                setValue: value => this.config.EnableAutoDeposit = value
            );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => this.Helper.Translation.Get("config.auto-deposit-range.name"),
                tooltip: () => this.Helper.Translation.Get("config.auto-deposit-range.tooltip"),
                getValue: () => this.config.AutoDepositRange,
                setValue: value => this.config.AutoDepositRange = value,
                min: 1, max: 10
            );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => this.Helper.Translation.Get("config.cooldown.name"),
                tooltip: () => this.Helper.Translation.Get("config.cooldown.tooltip"),
                getValue: () => this.config.AutoDepositCooldown,
                setValue: value => this.config.AutoDepositCooldown = value,
                min: 0, max: 60
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => this.Helper.Translation.Get("config.allow-resource.name"),
                tooltip: () => this.Helper.Translation.Get("config.allow-resource.tooltip"),
                getValue: () => this.config.EnableResourceStorage,
                setValue: value => this.config.EnableResourceStorage = value
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => this.Helper.Translation.Get("config.allow-trash.name"),
                tooltip: () => this.Helper.Translation.Get("config.allow-trash.tooltip"),
                getValue: () => this.config.EnableTrashStorage,
                setValue: value => this.config.EnableTrashStorage = value
            );
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            virtualChest = new Chest(playerChest: true);
            var savedItems = this.Helper.Data.ReadSaveData<List<Item>>(SaveKey);

            if (savedItems != null)
            {
                foreach (var item in savedItems)
                {
                    virtualChest.Items.Add(item);
                }
            }
        }

        private void OnSaving(object? sender, SavingEventArgs e)
        {
            if (virtualChest == null) return;

            for (int i = virtualChest.Items.Count - 1; i >= 0; i--)
            {
                if (virtualChest.Items[i] == null)
                    virtualChest.Items.RemoveAt(i);
            }
            this.Helper.Data.WriteSaveData(SaveKey, virtualChest.Items);
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || !Context.IsPlayerFree) return;
            if (Game1.currentLocation is not Farm) return;

            bool isMenuOpen = Game1.activeClickableMenu is ItemGrabMenu menu && menu.sourceItem == virtualChest;

            if (wasMenuOpen && !isMenuOpen)
            {
                cooldownTime = Game1.currentGameTime.TotalGameTime.TotalSeconds + config.AutoDepositCooldown;
            }
            wasMenuOpen = isMenuOpen;

            if (!config.EnableAutoDeposit) return;
            if (isMenuOpen || Game1.currentGameTime.TotalGameTime.TotalSeconds < cooldownTime) return;

            if (IsNearWoodPile(Game1.player.Tile))
            {
                if (IsWoodPile(this.Helper.Input.GetCursorPosition().Tile))
                {
                    Game1.mouseCursor = Game1.cursor_grab;
                }

                if (e.IsMultipleOf(15))
                {
                    AutoStoreItems();
                }
            }
        }

        private void AutoStoreItems()
        {
            if (virtualChest == null) return;

            bool movedAnything = false;
            int totalCountMoved = 0;

            for (int i = Game1.player.Items.Count - 1; i >= 0; i--)
            {
                Item item = Game1.player.Items[i];
                if (item == null) continue;

                if (IsAllowedItem(item))
                {
                    Item leftover = virtualChest.addItem(item);

                    int amountMoved = item.Stack;
                    if (leftover != null) amountMoved -= leftover.Stack;

                    if (amountMoved > 0)
                    {
                        movedAnything = true;
                        totalCountMoved += amountMoved;

                        if (leftover == null)
                            Game1.player.Items[i] = null;
                        else
                            Game1.player.Items[i] = leftover;
                    }
                }
            }

            if (movedAnything)
            {
                Game1.playSound("coin");
                string msg = this.Helper.Translation.Get("hud.items-stored", new { count = totalCountMoved });
                Game1.addHUDMessage(new HUDMessage(msg, 1));
            }
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady || !Context.IsPlayerFree) return;
            if (!e.Button.IsActionButton()) return;
            if (Game1.currentLocation is not Farm) return;

            Vector2 clickedTile = e.Cursor.GrabTile;

            if (IsWoodPile(clickedTile))
            {
                if (Utility.distance(Game1.player.Tile.X, clickedTile.X, Game1.player.Tile.Y, clickedTile.Y) <= 2f)
                {
                    this.Helper.Input.Suppress(e.Button);
                    OpenWoodPileMenu();
                }
            }
        }

        private bool IsNearWoodPile(Vector2 playerTile)
        {
            Point entry = Game1.getFarm().GetMainFarmHouseEntry();
            float range = (float)config.AutoDepositRange;
            return Utility.distance(playerTile.X, entry.X - 4, playerTile.Y, entry.Y) <= range;
        }

        private bool IsWoodPile(Vector2 tile)
        {
            Point farmhouseEntry = Game1.getFarm().GetMainFarmHouseEntry();
            bool validX = tile.X >= farmhouseEntry.X - 7 && tile.X <= farmhouseEntry.X - 1;
            bool validY = tile.Y >= farmhouseEntry.Y - 1 && tile.Y <= farmhouseEntry.Y;
            return validX && validY;
        }

        // --- UPDATED LOGIC ---
        private bool IsAllowedItem(Item item)
        {
            if (item == null) return false;

            // 1. Always Allow: Wood & Hardwood
            if (item.ItemId == "388" || item.ItemId == "709") return true;

            // 2. Allow Resources if enabled
            if (config.EnableResourceStorage)
            {
                // Category -16 (Stone, Fiber, Clay)
                // Category -15 (Coal, Copper, Iron, Gold, Iridium)
                if (item.Category == -16 || item.Category == -15) return true;
            }

            // 3. Allow Trash if enabled (Category -20)
            if (config.EnableTrashStorage && item.Category == -20) return true;

            return false;
        }

        private void OpenWoodPileMenu()
        {
            if (virtualChest == null) virtualChest = new Chest(playerChest: true);
            Game1.playSound("woodWhack");

            var menu = new ItemGrabMenu(
                virtualChest.Items,
                false,
                true,
                InventoryHighlight,
                virtualChest.grabItemFromInventory,
                null,
                null,
                false,
                true,
                true,
                true,
                true,
                1,
                virtualChest,
                -1,
                this
            );

            Game1.activeClickableMenu = menu;
            wasMenuOpen = true;
        }

        private bool InventoryHighlight(Item item)
        {
            if (virtualChest != null && virtualChest.Items.Contains(item)) return true;
            if (IsAllowedItem(item)) return true;
            return false;
        }
    }
}