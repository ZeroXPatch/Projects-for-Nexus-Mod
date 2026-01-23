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
        private static bool _buildingWasPlaced = false;

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
            try
            {
                string? buildingId = GetBuildingId(__instance);
                if (buildingId != null && Game1.buildingData.TryGetValue(buildingId, out BuildingData? data) && data.BuildMaterials != null)
                {
                    // Temporarily add phantom items to player's "count" for display purposes
                    // This is a hack but it makes the UI show green text
                    foreach (var mat in data.BuildMaterials)
                    {
                        int inInventory = Game1.player.Items.CountId(mat.ItemId);
                        int needed = mat.Amount - inInventory;

                        if (needed > 0)
                        {
                            int inChests = InventoryManager.CountItemsInChests(mat.ItemId);
                            if (inChests >= needed)
                            {
                                // The items exist in chests, so we're good
                                // But we can't easily make the text green without more invasive patches
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.ModMonitor.LogOnce($"[BuildAtRobin] Error in draw prefix: {ex}", LogLevel.Error);
            }
        }

        // -----------------------------------------------------------------------
        // PATCH 2: Track if building was actually placed
        // -----------------------------------------------------------------------
        [HarmonyPatch("returnToCarpentryMenu")]
        [HarmonyPrefix]
        public static void Prefix_ReturnToMenu()
        {
            // This is called when player cancels placement
            _buildingWasPlaced = false;
        }

        [HarmonyPatch("returnToCarpentryMenuAfterSuccessfulBuild")]
        [HarmonyPrefix]
        public static void Prefix_SuccessfulBuild()
        {
            // This is called after successful build
            _buildingWasPlaced = true;
        }

        // -----------------------------------------------------------------------
        // PATCH 3: Consume resources on click
        // -----------------------------------------------------------------------
        [HarmonyPatch("receiveLeftClick")]
        [HarmonyPrefix]
        public static void Prefix_Click(CarpenterMenu __instance, int x, int y)
        {
            try
            {
                _preClickInventory = null;
                _cachedBuildingId = null;
                _buildingWasPlaced = false;

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

        [HarmonyPatch("receiveLeftClick")]
        [HarmonyPostfix]
        public static void Postfix_Click(CarpenterMenu __instance, int x, int y)
        {
            try
            {
                if (_preClickInventory == null || _cachedBuildingId == null) return;

                // Wait a frame to see if building was placed
                ModEntry.ModHelper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            }
            catch (Exception ex)
            {
                ModEntry.ModMonitor.Log($"[BuildAtRobin] Error in click postfix: {ex}", LogLevel.Error);
                _preClickInventory = null;
                _cachedBuildingId = null;
            }
        }

        private static void OnUpdateTicked(object? sender, StardewModdingAPI.Events.UpdateTickedEventArgs e)
        {
            // Unsubscribe immediately
            ModEntry.ModHelper.Events.GameLoop.UpdateTicked -= OnUpdateTicked;

            try
            {
                if (_preClickInventory == null || _cachedBuildingId == null) return;

                // Only consume if we're no longer in the carpenter menu (building was placed)
                if (Game1.activeClickableMenu is not CarpenterMenu)
                {
                    if (Game1.buildingData.TryGetValue(_cachedBuildingId, out BuildingData? data) && data.BuildMaterials != null)
                    {
                        foreach (var mat in data.BuildMaterials)
                        {
                            string itemId = mat.ItemId;
                            int required = mat.Amount;

                            if (_preClickInventory.TryGetValue(itemId, out int startCount))
                            {
                                int currentCount = Game1.player.Items.CountId(itemId);
                                int vanillaTook = startCount - currentCount;
                                int remaining = required - vanillaTook;

                                if (remaining > 0)
                                {
                                    ModEntry.ModMonitor.Log($"[BuildAtRobin] Consuming {remaining} of {itemId} from chests", LogLevel.Info);
                                    InventoryManager.ConsumeItemsFromChests(itemId, remaining);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.ModMonitor.Log($"[BuildAtRobin] Error consuming items: {ex}", LogLevel.Error);
            }
            finally
            {
                _preClickInventory = null;
                _cachedBuildingId = null;
                _buildingWasPlaced = false;
            }
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