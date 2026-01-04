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

        // Logic to track menu state and cooldowns
        private bool wasMenuOpen = false;
        private double cooldownTime = 0; // When the cooldown expires (in TotalGameTime seconds)

        public override void Entry(IModHelper helper)
        {
            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.GameLoop.Saving += OnSaving;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
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

            // 1. Check if our specific menu is currently open
            bool isMenuOpen = Game1.activeClickableMenu is ItemGrabMenu menu && menu.sourceItem == virtualChest;

            // 2. Detect Menu Close: If it WAS open, but now is NOT, start the timer
            if (wasMenuOpen && !isMenuOpen)
            {
                // Set cooldown for 15 seconds (Approx 20 in-game minutes)
                cooldownTime = Game1.currentGameTime.TotalGameTime.TotalSeconds + 15;
            }

            // Update state for next tick
            wasMenuOpen = isMenuOpen;

            // 3. Stop here if menu is open OR we are in cooldown period
            if (isMenuOpen || Game1.currentGameTime.TotalGameTime.TotalSeconds < cooldownTime)
            {
                return;
            }

            Vector2 playerTile = Game1.player.Tile;

            if (IsNearWoodPile(playerTile))
            {
                // Visual Cursor effect
                if (IsWoodPile(this.Helper.Input.GetCursorPosition().Tile))
                {
                    Game1.mouseCursor = Game1.cursor_grab;
                }

                // Auto-Suck Logic (Run every 15 ticks)
                if (e.IsMultipleOf(15))
                {
                    AutoStoreWood();
                }
            }
        }

        private void AutoStoreWood()
        {
            if (virtualChest == null) return;

            bool movedAnything = false;
            int totalWoodMoved = 0;

            // Loop backwards to safely remove items from player
            for (int i = Game1.player.Items.Count - 1; i >= 0; i--)
            {
                Item item = Game1.player.Items[i];
                if (item == null) continue;

                // Check for Wood (388) or Hardwood (709)
                if (item.ItemId == "388" || item.ItemId == "709")
                {
                    Item leftover = virtualChest.addItem(item);

                    int amountMoved = item.Stack;
                    if (leftover != null) amountMoved -= leftover.Stack;

                    if (amountMoved > 0)
                    {
                        movedAnything = true;
                        totalWoodMoved += amountMoved;

                        if (leftover == null)
                            Game1.player.Items[i] = null;
                        else
                            Game1.player.Items[i] = leftover;
                    }
                }
            }

            if (movedAnything)
            {
                // CHANGED: Use "coin" sound to avoid "pickupItem" crash
                Game1.playSound("coin");
                Game1.addHUDMessage(new HUDMessage($"+{totalWoodMoved} Wood Stored", 1));
            }
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady || !Context.IsPlayerFree) return;
            if (!e.Button.IsActionButton()) return;
            if (Game1.currentLocation is not Farm) return;

            Vector2 clickedTile = e.Cursor.GrabTile;

            if (IsWoodPile(clickedTile) && IsNearWoodPile(Game1.player.Tile))
            {
                this.Helper.Input.Suppress(e.Button);
                OpenWoodPileMenu();
            }
        }

        private bool IsNearWoodPile(Vector2 playerTile)
        {
            Point entry = Game1.getFarm().GetMainFarmHouseEntry();
            return Utility.distance(playerTile.X, entry.X - 4, playerTile.Y, entry.Y) <= 3f;
        }

        private bool IsWoodPile(Vector2 tile)
        {
            Point farmhouseEntry = Game1.getFarm().GetMainFarmHouseEntry();
            bool validX = tile.X >= farmhouseEntry.X - 7 && tile.X <= farmhouseEntry.X - 1;
            bool validY = tile.Y >= farmhouseEntry.Y - 1 && tile.Y <= farmhouseEntry.Y;
            return validX && validY;
        }

        private void OpenWoodPileMenu()
        {
            if (virtualChest == null) virtualChest = new Chest(playerChest: true);
            Game1.playSound("woodWhack");

            var menu = new ItemGrabMenu(
                virtualChest.Items,     // Inventory
                false,                  // reverseGrab
                true,                   // showReceivingMenu
                InventoryHighlight,     // highlightFunction
                null,                   // behaviorOnItemSelect
                null,                   // message
                null,                   // behaviorOnItemGrab
                false,                  // snapToBottom
                true,                   // canBeExitedWithKey
                true,                   // playRightClickSound
                true,                   // allowRightClick
                true,                   // showOrganizeButton
                1,                      // source
                virtualChest,           // sourceItem
                -1,                     // whichSpecialButton
                this                    // context
            );

            Game1.activeClickableMenu = menu;

            // Mark menu as open so our timer logic knows
            wasMenuOpen = true;
        }

        private bool InventoryHighlight(Item item)
        {
            // If the item is inside the VIRTUAL CHEST (Top), allow clicking (return true)
            if (virtualChest != null && virtualChest.Items.Contains(item))
            {
                return true;
            }

            // If the item is in PLAYER inventory (Bottom), disable clicking (return false)
            return false;
        }
    }
}