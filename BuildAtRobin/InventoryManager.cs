using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Inventories;
using StardewValley.Locations;
using StardewValley.Objects;

namespace BuildAtRobin
{
    internal static class InventoryManager
    {
        private const string AutoGrabberId = "(BC)165";
        private static bool _hasLoggedChests = false;

        public static int CountItems(string itemId)
        {
            try
            {
                // 1. Count Inventory
                int count = Game1.player.Items.CountId(itemId);

                // 2. Count Chests
                int chestCount = 0;
                int chestsFound = 0;

                foreach (var chest in GetAllChests())
                {
                    chestsFound++;
                    if (chest.Items != null)
                    {
                        chestCount += chest.Items.CountId(itemId);
                    }
                }

                // DEBUG LOG: Only print once per menu open to avoid spamming
                if (!_hasLoggedChests)
                {
                    ModEntry.ModMonitor.Log($"[BuildAtRobin] Scanned {chestsFound} containers. Player has {count} of {itemId}, Chests have {chestCount}", LogLevel.Debug);
                    _hasLoggedChests = true;
                }

                return count + chestCount;
            }
            catch (Exception ex)
            {
                ModEntry.ModMonitor.Log($"[BuildAtRobin] Error counting items: {ex}", LogLevel.Error);
                return Game1.player.Items.CountId(itemId); // Fallback to player inventory only
            }
        }

        public static void ResetLog()
        {
            _hasLoggedChests = false;
        }

        public static int CountItemsInChests(string itemId)
        {
            try
            {
                int chestCount = 0;

                foreach (var chest in GetAllChests())
                {
                    if (chest.Items != null)
                    {
                        chestCount += chest.Items.CountId(itemId);
                    }
                }

                return chestCount;
            }
            catch (Exception ex)
            {
                ModEntry.ModMonitor.Log($"[BuildAtRobin] Error counting items in chests: {ex}", LogLevel.Error);
                return 0;
            }
        }

        public static void ConsumeItems(string itemId, int amount)
        {
            try
            {
                // 1. Take from Inventory
                int inInventory = Game1.player.Items.CountId(itemId);
                int takeFromInv = Math.Min(inInventory, amount);

                if (takeFromInv > 0)
                {
                    Game1.player.Items.ReduceId(itemId, takeFromInv);
                    amount -= takeFromInv;
                    ModEntry.ModMonitor.Log($"[BuildAtRobin] Took {takeFromInv} of {itemId} from player inventory", LogLevel.Debug);
                }

                if (amount <= 0) return;

                // 2. Take remaining from Chests
                ConsumeItemsFromChests(itemId, amount);
            }
            catch (Exception ex)
            {
                ModEntry.ModMonitor.Log($"[BuildAtRobin] Error consuming items: {ex}", LogLevel.Error);
            }
        }

        public static void ConsumeItemsFromChests(string itemId, int amount)
        {
            try
            {
                int totalTaken = 0;

                foreach (var chest in GetAllChests())
                {
                    if (chest.Items == null) continue;

                    int inChest = chest.Items.CountId(itemId);
                    if (inChest > 0)
                    {
                        int take = Math.Min(inChest, amount);
                        chest.Items.ReduceId(itemId, take);
                        amount -= take;
                        totalTaken += take;

                        if (amount <= 0) break;
                    }
                }

                if (totalTaken > 0)
                {
                    ModEntry.ModMonitor.Log($"[BuildAtRobin] Took {totalTaken} of {itemId} from chests", LogLevel.Debug);
                }
            }
            catch (Exception ex)
            {
                ModEntry.ModMonitor.Log($"[BuildAtRobin] Error consuming from chests: {ex}", LogLevel.Error);
            }
        }

        private static IEnumerable<Chest> GetAllChests()
        {
            foreach (GameLocation location in Game1.locations)
            {
                if (location == null) continue;

                // Objects (Chests, Big Chests, Stone Chests, Auto-Grabbers)
                if (location.objects != null && location.objects.Values != null)
                {
                    foreach (StardewValley.Object obj in location.objects.Values)
                    {
                        if (obj == null) continue;

                        if (obj is Chest chest && chest.playerChest.Value)
                        {
                            yield return chest;
                        }
                        else if (obj.QualifiedItemId == AutoGrabberId && obj.heldObject.Value is Chest grabber)
                        {
                            yield return grabber;
                        }
                    }
                }

                // Buildings (Junimo Huts, Mills)
                if (location.buildings != null)
                {
                    foreach (Building building in location.buildings)
                    {
                        if (building == null) continue;

                        if (building is JunimoHut hut)
                        {
                            var output = hut.GetOutputChest();
                            if (output != null) yield return output;
                        }
                        else
                        {
                            Chest? output = building.GetBuildingChest("Output");
                            if (output != null) yield return output;
                        }
                    }
                }

                // Fridges
                if (location is FarmHouse fh && fh.fridge.Value != null && fh.fridgePosition != Point.Zero)
                {
                    yield return fh.fridge.Value;
                }

                if (location is IslandFarmHouse ifh && ifh.fridge.Value != null)
                {
                    yield return ifh.fridge.Value;
                }
            }
        }
    }
}