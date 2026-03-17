using System;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace NPCMapLocationsPerformancePatch
{
    public class ModEntry : Mod
    {
        private static IMonitor ModMonitor;
        private static bool IsMapOpen = false;

        public override void Entry(IModHelper helper)
        {
            ModMonitor = Monitor;

            var harmony = new Harmony(ModManifest.UniqueID);

            try
            {
                PatchNPCMapLocations(harmony);
                Monitor.Log("Performance Patch for NPC Map Locations loaded successfully!", LogLevel.Info);
                Monitor.Log("NPC positions will only update when the map tab is open.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Monitor.Log($"Error during initialization: {ex.Message}", LogLevel.Error);
            }

            helper.Events.Display.MenuChanged += OnMenuChanged;
            helper.Events.Display.RenderedActiveMenu += OnRenderedActiveMenu;
        }

        private void OnRenderedActiveMenu(object sender, RenderedActiveMenuEventArgs e)
        {
            if (Game1.activeClickableMenu != null)
            {
                var menuType = Game1.activeClickableMenu.GetType().Name;

                if ((menuType == "MapPage" || menuType.Contains("Map")) && !IsMapOpen)
                {
                    IsMapOpen = true;
                }

                if (menuType == "GameMenu")
                {
                    CheckGameMenuTab();
                }
            }
            else if (IsMapOpen)
            {
                IsMapOpen = false;
            }
        }

        private void PatchNPCMapLocations(Harmony harmony)
        {
            var npcMod = Helper.ModRegistry.Get("Bouhm.NPCMapLocations");

            if (npcMod == null)
            {
                Monitor.Log("NPC Map Locations mod not found! This patch requires it to be installed.", LogLevel.Error);
                return;
            }

            Assembly assembly = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name == "NPCMapLocations")
                {
                    assembly = asm;
                    break;
                }
            }

            if (assembly == null)
            {
                Monitor.Log("Could not find NPC Map Locations assembly!", LogLevel.Error);
                return;
            }

            int patched = 0;

            // Only patch methods that are EXACTLY the update event handlers
            // This is much safer than broad pattern matching
            string[] targetMethods = new string[]
            {
                "GameLoop_UpdateTicked",      // SMAPI 3.0+ standard event handler
                "OnUpdateTicked",             // Older SMAPI versions
                "UpdateTicked",               // Direct handler name
            };

            foreach (var type in assembly.GetTypes())
            {
                foreach (var targetMethodName in targetMethods)
                {
                    var method = type.GetMethod(targetMethodName,
                        BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static);

                    if (method != null)
                    {
                        try
                        {
                            // Verify this looks like an event handler (takes 2 parameters)
                            var parameters = method.GetParameters();
                            if (parameters.Length == 2)
                            {
                                var prefix = typeof(NPCMapLocationsPatch).GetMethod(nameof(NPCMapLocationsPatch.UpdatePrefix));
                                harmony.Patch(method, prefix: new HarmonyMethod(prefix));

                                Monitor.Log($"Patched: {type.Name}.{method.Name}", LogLevel.Debug);
                                patched++;
                            }
                        }
                        catch (Exception ex)
                        {
                            Monitor.Log($"Failed to patch {targetMethodName}: {ex.Message}", LogLevel.Warn);
                        }
                    }
                }
            }

            if (patched > 0)
            {
                Monitor.Log($"Successfully patched {patched} update method(s).", LogLevel.Info);
            }
            else
            {
                Monitor.Log("Warning: Could not find update event handlers to patch.", LogLevel.Warn);
                Monitor.Log("NPC Map Locations may have been updated. Please report this issue.", LogLevel.Warn);
            }
        }

        private void OnMenuChanged(object sender, MenuChangedEventArgs e)
        {
            var newMenu = e.NewMenu;

            if (newMenu != null && newMenu.GetType().Name == "GameMenu")
            {
                CheckGameMenuTab();
            }

            if (newMenu != null && (newMenu.GetType().Name == "MapPage" || newMenu.GetType().Name.Contains("Map")))
            {
                IsMapOpen = true;
            }

            if (newMenu == null && IsMapOpen)
            {
                IsMapOpen = false;
            }
        }

        private void CheckGameMenuTab()
        {
            try
            {
                var menu = Game1.activeClickableMenu;
                if (menu == null) return;

                var gameMenuType = menu.GetType();
                var currentTabField = gameMenuType.GetField("currentTab", BindingFlags.Public | BindingFlags.Instance);

                if (currentTabField != null)
                {
                    int currentTab = (int)currentTabField.GetValue(menu);

                    // Tab 3 is the map tab
                    IsMapOpen = (currentTab == 3);
                }
            }
            catch
            {
                // Silently fail if we can't check the tab
            }
        }

        public static bool GetIsMapOpen() => IsMapOpen;
    }

    [HarmonyPatch]
    public class NPCMapLocationsPatch
    {
        public static bool UpdatePrefix()
        {
            // Return true to allow update when map is open
            // Return false to block update when map is closed
            return ModEntry.GetIsMapOpen();
        }
    }
}