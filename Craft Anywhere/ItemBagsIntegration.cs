using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;

namespace CraftAnywhere
{
    /// <summary>
    /// Integration for Item Bags mod to include bag contents in crafting.
    /// </summary>
    internal class ItemBagsIntegration
    {
        private readonly IModHelper Helper;
        private readonly IMonitor Monitor;
        private bool IsLoaded = false;
        
        private Type? ItemBagType;
        private Type? ItemBagInventory;

        public ItemBagsIntegration(IModHelper helper, IMonitor monitor)
        {
            Helper = helper;
            Monitor = monitor;
            
            // Check if Item Bags mod is loaded
            var modInfo = helper.ModRegistry.Get("SlayerDharok.Item_Bags");
            if (modInfo == null || !modInfo.Manifest.Version.IsNewerThan("3.0.5"))
            {
                return;
            }

            try
            {
                ItemBagType = Type.GetType("ItemBags.Bags.ItemBag, ItemBags");
                ItemBagInventory = Type.GetType("ItemBags.ItemBagCraftingInventory, ItemBags");

                if (ItemBagType == null || ItemBagInventory == null)
                {
                    Monitor.Log("Item Bags types not found. Integration disabled.", LogLevel.Warn);
                    return;
                }

                IsLoaded = true;
                Monitor.Log("Item Bags integration enabled.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Monitor.Log($"Failed to load Item Bags integration: {ex.Message}", LogLevel.Warn);
            }
        }

        /// <summary>
        /// Gets all item bags from the player's inventory and returns their contents as chest-like objects.
        /// </summary>
        public List<object> GetItemBagInventories()
        {
            List<object> bagInventories = new List<object>();
            
            if (!IsLoaded || ItemBagInventory == null || ItemBagType == null)
                return bagInventories;

            try
            {
                foreach (var item in Game1.player.Items)
                {
                    if (item != null && item.GetType().IsAssignableTo(ItemBagType))
                    {
                        try
                        {
                            // Create ItemBagCraftingInventory wrapper for this bag
                            var inventory = Activator.CreateInstance(ItemBagInventory, item);
                            if (inventory != null)
                            {
                                bagInventories.Add(inventory);
                            }
                        }
                        catch (Exception ex)
                        {
                            Monitor.LogOnce($"Failed to create inventory for item bag: {ex.Message}", LogLevel.Trace);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Monitor.Log($"Error scanning item bags: {ex.Message}", LogLevel.Error);
            }

            return bagInventories;
        }

        public bool IsItemBagLoaded => IsLoaded;
    }
}