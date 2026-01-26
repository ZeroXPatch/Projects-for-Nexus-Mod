using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Inventories;
using StardewValley.Menus;
using StardewValley.Objects;

namespace CraftAnywhere
{
    internal class ModEntry : Mod
    {
        private ChestScanner? Scanner;
        private IClickableMenu? LastInjectedMenu;
        private int LastTabId = -1;
        private IBetterCraftingApi? BetterCrafting;
        private ItemBagsIntegration? ItemBags;
        private ConvenientChestsIntegration? ConvenientChests;

        public override void Entry(IModHelper helper)
        {
            this.Scanner = new ChestScanner();

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.Display.MenuChanged += this.OnMenuChanged;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            // Initialize Better Crafting integration
            this.BetterCrafting = this.Helper.ModRegistry.GetApi<IBetterCraftingApi>("leclair.bettercrafting");

            if (this.BetterCrafting != null)
            {
                this.Monitor.Log("Better Crafting detected. Hooking into container population...", LogLevel.Info);

                // Subscribe to the event using += syntax
                this.BetterCrafting.MenuSimplePopulateContainers += this.OnBetterCraftingPopulate;
            }

            // Initialize Item Bags integration
            this.ItemBags = new ItemBagsIntegration(this.Helper, this.Monitor);

            // Initialize Convenient Chests integration
            this.ConvenientChests = new ConvenientChestsIntegration(this.Helper, this.Monitor);
        }

        private void OnBetterCraftingPopulate(ISimplePopulateContainersEvent e)
        {
            // Add all chests from the world
            var globalChests = this.Scanner!.GetAllChests();

            foreach (var chest in globalChests)
            {
                // Add the chest and its location to the containers list
                var containerData = new Tuple<object, GameLocation?>(chest, chest.Location);
                e.Containers.Add(containerData);
            }

            // Add item bags from player inventory
            if (this.ItemBags != null && this.ItemBags.IsItemBagLoaded)
            {
                var bagInventories = this.ItemBags.GetItemBagInventories();
                foreach (var bagInventory in bagInventories)
                {
                    var containerData = new Tuple<object, GameLocation?>(bagInventory, null);
                    e.Containers.Add(containerData);
                }

                if (bagInventories.Count > 0)
                {
                    this.Monitor.LogOnce($"Injected {bagInventories.Count} item bags into Better Crafting.", LogLevel.Trace);
                }
            }

            this.Monitor.LogOnce($"Injected {globalChests.Count} chests into Better Crafting.", LogLevel.Trace);
        }

        private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
        {
            this.LastInjectedMenu = null;
            this.LastTabId = -1;

            if (e.NewMenu is CraftingPage craftingPage)
                this.InjectChests(craftingPage);
            else if (e.NewMenu is GameMenu gameMenu && gameMenu.currentTab == GameMenu.craftingTab)
                if (gameMenu.GetCurrentPage() is CraftingPage page)
                    this.InjectChests(page);
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady) return;

            if (Game1.activeClickableMenu is GameMenu gameMenu)
            {
                if (gameMenu.currentTab == GameMenu.craftingTab &&
                   (gameMenu.currentTab != this.LastTabId || this.LastInjectedMenu != gameMenu))
                {
                    if (gameMenu.GetCurrentPage() is CraftingPage page)
                        this.InjectChests(page);
                }
                this.LastTabId = gameMenu.currentTab;
            }
        }

        private void InjectChests(CraftingPage page)
        {
            // If Better Crafting is installed, let it handle everything
            if (this.BetterCrafting != null)
                return;

            // If Convenient Chests is handling crafting, defer to it
            if (this.ConvenientChests != null && this.ConvenientChests.IsHandlingCrafting())
            {
                this.Monitor.LogOnce("Convenient Chests is handling crafting. Craft Anywhere will not inject chests.", LogLevel.Debug);
                return;
            }

            this.LastInjectedMenu = Game1.activeClickableMenu ?? page;

            List<Chest> globalChests = this.Scanner!.GetAllChests();

            var containersField = this.Helper.Reflection.GetField<List<IInventory>>(page, "_materialContainers");
            List<IInventory> currentContainers = containersField.GetValue();

            if (currentContainers == null)
            {
                currentContainers = new List<IInventory>();
                containersField.SetValue(currentContainers);
            }

            currentContainers.Clear();

            // Add all chests
            foreach (Chest chest in globalChests)
            {
                try
                {
                    if (chest.Items != null)
                        currentContainers.Add(chest.Items);
                }
                catch (Exception) { }
            }

            // Add item bags
            if (this.ItemBags != null && this.ItemBags.IsItemBagLoaded)
            {
                var bagInventories = this.ItemBags.GetItemBagInventories();
                foreach (var bagInventory in bagInventories)
                {
                    try
                    {
                        if (bagInventory is IInventory inventory)
                            currentContainers.Add(inventory);
                    }
                    catch (Exception) { }
                }
            }
        }
    }
}