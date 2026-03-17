using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.GameData.Buildings;
using StardewValley.Buildings;

namespace BuildAtRobin
{
    [HarmonyPatch(typeof(CarpenterMenu))]
    internal static class CarpenterMenuPatch
    {
        private static Dictionary<string, int>? _preClickInventory;
        private static string? _cachedBuildingId;

        // Cache fields
        private static FieldInfo? _buildingField;
        private static PropertyInfo? _blueprintProperty;
        private static bool _searchComplete = false;

        // Prevent log spam
        private static string? _lastCheckedBuilding;

        // -----------------------------------------------------------------------
        // PATCH 1: Light up the "Build" button
        // -----------------------------------------------------------------------
        [HarmonyPatch("DoesFarmerHaveEnoughResourcesToBuild")]
        [HarmonyPrefix]
        public static bool Prefix_CheckResources(CarpenterMenu __instance, ref bool __result)
        {
            try
            {
                string? buildingId = GetBuildingId(__instance);

                if (buildingId != null && Game1.buildingData.TryGetValue(buildingId, out BuildingData? data))
                {
                    __result = CheckHasPermissions(data, buildingId);
                    return false; // Skip original method
                }
            }
            catch (Exception ex)
            {
                ModEntry.ModMonitor.LogOnce($"[BuildAtRobin] Error in resource check: {ex}", LogLevel.Error);
            }

            return true; // Run original method
        }

        // -----------------------------------------------------------------------
        // PATCH 1.5: Make items appear available for UI rendering
        // -----------------------------------------------------------------------
        [HarmonyPatch("draw", typeof(Microsoft.Xna.Framework.Graphics.SpriteBatch))]
        [HarmonyPrefix]
        public static void Prefix_Draw(CarpenterMenu __instance)
        {
            // Currently a no-op — kept as extension point for future green-text UI work.
            // Phantom item injection here is complex without more invasive patches.
        }

        // -----------------------------------------------------------------------
        // PATCH 2: Capture player inventory just before the placement confirmation
        //          click so we can measure what vanilla consumed.
        // -----------------------------------------------------------------------
        [HarmonyPatch("receiveLeftClick")]
        [HarmonyPrefix]
        public static void Prefix_Click(CarpenterMenu __instance, int x, int y)
        {
            try
            {
                // Reset on every click so we always have the freshest snapshot.
                _preClickInventory = null;
                _cachedBuildingId = null;

                string? buildingId = GetBuildingId(__instance);
                if (buildingId != null && Game1.buildingData.TryGetValue(buildingId, out BuildingData? data) && data.BuildMaterials != null)
                {
                    _cachedBuildingId = buildingId;
                    _preClickInventory = new Dictionary<string, int>();

                    foreach (var mat in data.BuildMaterials)
                    {
                        if (!_preClickInventory.ContainsKey(mat.ItemId))
                        {
                            _preClickInventory[mat.ItemId] = Game1.player.Items.CountId(mat.ItemId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.ModMonitor.Log($"[BuildAtRobin] Error in click prefix: {ex}", LogLevel.Error);
            }
        }

        // -----------------------------------------------------------------------
        // PATCH 3: Consume chest resources after a confirmed, successful build.
        //
        // WHY HERE and not in UpdateTicked / receiveLeftClick postfix:
        //   - returnToCarpentryMenuAfterSuccessfulBuild is ONLY called on real placements.
        //   - After a successful build the vanilla code calls this method, which then
        //     reopens CarpenterMenu — so checking "is not CarpenterMenu" one tick later
        //     is always FALSE and never fires. This patch avoids that entirely.
        //   - Prefix_Click above runs on the confirmation click, capturing the player's
        //     inventory counts before vanilla deducts anything, giving us the exact delta.
        // -----------------------------------------------------------------------
        [HarmonyPatch("returnToCarpentryMenuAfterSuccessfulBuild")]
        [HarmonyPostfix]
        public static void Postfix_SuccessfulBuild()
        {
            try
            {
                if (_preClickInventory == null || _cachedBuildingId == null)
                {
                    ModEntry.ModMonitor.Log($"[BuildAtRobin] Successful build detected but no snapshot available — skipping chest deduction.", LogLevel.Warn);
                    return;
                }

                if (!Game1.buildingData.TryGetValue(_cachedBuildingId, out BuildingData? data) || data.BuildMaterials == null)
                    return;

                foreach (var mat in data.BuildMaterials)
                {
                    string itemId = mat.ItemId;
                    int required = mat.Amount;

                    if (!_preClickInventory.TryGetValue(itemId, out int startCount))
                        continue;

                    int currentCount = Game1.player.Items.CountId(itemId);
                    int vanillaTook = startCount - currentCount; // what vanilla already removed from inventory
                    int remaining = required - vanillaTook;      // what still needs to come from chests

                    if (remaining > 0)
                    {
                        ModEntry.ModMonitor.Log($"[BuildAtRobin] Vanilla took {vanillaTook}/{required} of {itemId} from inventory — consuming {remaining} more from chests.", LogLevel.Info);
                        InventoryManager.ConsumeItemsFromChests(itemId, remaining);
                    }
                    else
                    {
                        ModEntry.ModMonitor.Log($"[BuildAtRobin] Vanilla covered full cost of {itemId} from inventory — no chest deduction needed.", LogLevel.Debug);
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.ModMonitor.Log($"[BuildAtRobin] Error consuming items after successful build: {ex}", LogLevel.Error);
            }
            finally
            {
                _preClickInventory = null;
                _cachedBuildingId = null;
            }
        }

        // -----------------------------------------------------------------------
        // PATCH 4: Clean up state if the player cancels placement.
        // -----------------------------------------------------------------------
        [HarmonyPatch("returnToCarpentryMenu")]
        [HarmonyPostfix]
        public static void Postfix_CancelPlacement()
        {
            _preClickInventory = null;
            _cachedBuildingId = null;
        }

        // --- Helper Logic ---

        private static string? GetBuildingId(CarpenterMenu menu)
        {
            try
            {
                // Find the field/property on first run
                if (!_searchComplete)
                {
                    // Try to find BlueprintName property first (newer versions)
                    _blueprintProperty = typeof(CarpenterMenu).GetProperty("BlueprintName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    if (_blueprintProperty == null)
                    {
                        // Look for Building field
                        var fields = typeof(CarpenterMenu).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        foreach (var field in fields)
                        {
                            if (typeof(Building).IsAssignableFrom(field.FieldType))
                            {
                                _buildingField = field;
                                ModEntry.ModMonitor.Log($"[BuildAtRobin] Found building field: {field.Name}", LogLevel.Debug);
                                break;
                            }
                        }
                    }
                    else
                    {
                        ModEntry.ModMonitor.Log($"[BuildAtRobin] Found BlueprintName property", LogLevel.Debug);
                    }

                    _searchComplete = true;
                }

                // Try BlueprintName property
                if (_blueprintProperty != null)
                {
                    var blueprintName = _blueprintProperty.GetValue(menu) as string;
                    if (!string.IsNullOrEmpty(blueprintName))
                    {
                        return blueprintName;
                    }
                }

                // Try Building field
                if (_buildingField != null)
                {
                    if (_buildingField.GetValue(menu) is Building building && !string.IsNullOrEmpty(building.buildingType.Value))
                    {
                        return building.buildingType.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.ModMonitor.LogOnce($"[BuildAtRobin] Error finding building: {ex.Message}", LogLevel.Error);
            }
            return null;
        }

        private static bool CheckHasPermissions(BuildingData data, string buildingId)
        {
            try
            {
                // Check money
                if (data.BuildCost > Game1.player.Money)
                {
                    return false;
                }

                // Check materials
                if (data.BuildMaterials != null)
                {
                    // Only log once per building to prevent spam
                    bool shouldLog = _lastCheckedBuilding != buildingId;
                    if (shouldLog)
                    {
                        _lastCheckedBuilding = buildingId;
                        InventoryManager.ResetLog();
                    }

                    foreach (var mat in data.BuildMaterials)
                    {
                        int total = InventoryManager.CountItems(mat.ItemId);
                        if (total < mat.Amount)
                        {
                            if (shouldLog)
                            {
                                ModEntry.ModMonitor.Log($"[BuildAtRobin] Missing {mat.ItemId}: Need {mat.Amount}, Found {total}", LogLevel.Info);
                            }
                            return false;
                        }
                    }

                    if (shouldLog)
                    {
                        ModEntry.ModMonitor.Log($"[BuildAtRobin] All materials available for {buildingId}!", LogLevel.Info);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                ModEntry.ModMonitor.LogOnce($"[BuildAtRobin] Error checking permissions: {ex}", LogLevel.Error);
                return false;
            }
        }
    }
}