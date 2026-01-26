using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace FasterMenuLoad
{
    public static class MenuPatches
    {
        public static void Apply(Harmony harmony)
        {
            // Find GameMenu constructor with better Android compatibility
            ConstructorInfo? ctor = null;

            // Try PC constructor first (bool parameter)
            ctor = AccessTools.Constructor(typeof(GameMenu), new[] { typeof(bool) });

            // If that fails, try to find ANY constructor (Android compatibility)
            if (ctor == null)
            {
                var allCtors = AccessTools.GetDeclaredConstructors(typeof(GameMenu));
                if (allCtors != null && allCtors.Count > 0)
                {
                    // Prefer constructors with parameters (more likely to be the right one)
                    ctor = allCtors.FirstOrDefault(c => c.GetParameters().Length > 0) ?? allCtors[0];
                    ModEntry.ModMonitor.Log($"Using fallback constructor with {ctor.GetParameters().Length} parameters", LogLevel.Debug);
                }
            }

            if (ctor != null)
            {
                try
                {
                    harmony.Patch(
                        original: ctor,
                        postfix: new HarmonyMethod(typeof(MenuPatches), nameof(GameMenu_Ctor_Postfix))
                    );
                    ModEntry.ModMonitor.Log("Successfully patched GameMenu constructor", LogLevel.Debug);
                }
                catch (Exception ex)
                {
                    ModEntry.ModMonitor.Log($"Failed to patch GameMenu constructor: {ex}", LogLevel.Error);
                }
            }
            else
            {
                ModEntry.ModMonitor.Log("Could not find any GameMenu constructor! Mod will not work.", LogLevel.Error);
                return;
            }

            // Patch changeTab
            MethodInfo? changeTabMethod = AccessTools.Method(typeof(GameMenu), nameof(GameMenu.changeTab), new[] { typeof(int), typeof(bool) });
            if (changeTabMethod != null)
            {
                try
                {
                    harmony.Patch(
                        original: changeTabMethod,
                        prefix: new HarmonyMethod(typeof(MenuPatches), nameof(ChangeTab_Prefix))
                    );
                    ModEntry.ModMonitor.Log("Successfully patched GameMenu.changeTab", LogLevel.Debug);
                }
                catch (Exception ex)
                {
                    ModEntry.ModMonitor.Log($"Failed to patch changeTab: {ex}", LogLevel.Error);
                }
            }
            else
            {
                ModEntry.ModMonitor.Log("Could not find GameMenu.changeTab method!", LogLevel.Warn);
            }
        }

        private static void GameMenu_Ctor_Postfix(GameMenu __instance)
        {
            try
            {
                List<IClickableMenu> pages = __instance.pages;

                // Safety check for null pages list
                if (pages == null)
                {
                    ModEntry.ModMonitor.Log("GameMenu.pages is null, cannot apply lazy loading", LogLevel.Warn);
                    return;
                }

                // --- SMART SCANNING ---
                // Instead of assuming tab positions, we loop through all pages
                // and find them wherever they are (compatible with other mods).

                for (int i = 0; i < pages.Count; i++)
                {
                    IClickableMenu page = pages[i];
                    if (page == null) continue;

                    string pageType = page.GetType().Name;

                    // 1. Check for Skills Page (Tab 1)
                    if (ModEntry.Config.LazyLoadSkills && page is SkillsPage)
                    {
                        if (ModEntry.Config.EnableDebugLogging)
                            ModEntry.ModMonitor.Log($"[FasterMenuLoad] Found SkillsPage at Tab {i}. Replacing with LazyTab...", LogLevel.Alert);

                        pages[i] = new LazyTab(page.xPositionOnScreen, page.yPositionOnScreen, page.width, page.height,
                            (x, y, w, h) => new SkillsPage(x, y, w, h));
                    }

                    // 2. Check for Social Page (Tab 2)
                    else if (ModEntry.Config.LazyLoadSocial && page is SocialPage)
                    {
                        if (ModEntry.Config.EnableDebugLogging)
                            ModEntry.ModMonitor.Log($"[FasterMenuLoad] Found SocialPage at Tab {i}. Replacing with LazyTab...", LogLevel.Alert);

                        pages[i] = new LazyTab(page.xPositionOnScreen, page.yPositionOnScreen, page.width, page.height,
                            (x, y, w, h) => new SocialPage(x, y, w, h));
                    }

                    // 3. Check for Crafting Page (Tab 4 - Crafting & Cooking)
                    else if (ModEntry.Config.LazyLoadCrafting && page is CraftingPage cPage)
                    {
                        try
                        {
                            // We need to know if it's cooking or crafting to recreate it correctly
                            var cookingField = AccessTools.Field(typeof(CraftingPage), "cooking");
                            if (cookingField != null)
                            {
                                bool isCooking = (bool)cookingField.GetValue(cPage);
                                string typeName = isCooking ? "Cooking" : "Crafting";

                                if (ModEntry.Config.EnableDebugLogging)
                                    ModEntry.ModMonitor.Log($"[FasterMenuLoad] Found {typeName} Page at Tab {i}. Replacing with LazyTab...", LogLevel.Alert);

                                pages[i] = new LazyTab(page.xPositionOnScreen, page.yPositionOnScreen, page.width, page.height,
                                    (x, y, w, h) => new CraftingPage(x, y, w, h, isCooking));
                            }
                            else
                            {
                                ModEntry.ModMonitor.Log($"[FasterMenuLoad] Found CraftingPage at Tab {i} but couldn't determine cooking/crafting type", LogLevel.Warn);
                            }
                        }
                        catch (Exception ex)
                        {
                            ModEntry.ModMonitor.Log($"Error checking CraftingPage cooking field: {ex}", LogLevel.Warn);
                        }
                    }

                    // 4. Check for Animals Page (Tab 5 - New in 1.6)
                    else if (ModEntry.Config.LazyLoadAnimals && page is AnimalPage)
                    {
                        if (ModEntry.Config.EnableDebugLogging)
                            ModEntry.ModMonitor.Log($"[FasterMenuLoad] Found AnimalPage at Tab {i}. Replacing with LazyTab...", LogLevel.Alert);

                        pages[i] = new LazyTab(page.xPositionOnScreen, page.yPositionOnScreen, page.width, page.height,
                            (x, y, w, h) => new AnimalPage(x, y, w, h));
                    }

                    // 5. Check for Powers Page (Tab 6 - New in 1.6)
                    else if (ModEntry.Config.LazyLoadPowers && page is PowersTab)
                    {
                        if (ModEntry.Config.EnableDebugLogging)
                            ModEntry.ModMonitor.Log($"[FasterMenuLoad] Found PowersTab at Tab {i}. Replacing with LazyTab...", LogLevel.Alert);

                        pages[i] = new LazyTab(page.xPositionOnScreen, page.yPositionOnScreen, page.width, page.height,
                            (x, y, w, h) => new PowersTab(x, y, w, h));
                    }

                    // 6. Check for Collections Page (Tab 7)
                    else if (ModEntry.Config.LazyLoadCollections && page is CollectionsPage)
                    {
                        if (ModEntry.Config.EnableDebugLogging)
                            ModEntry.ModMonitor.Log($"[FasterMenuLoad] Found CollectionsPage at Tab {i}. Replacing with LazyTab...", LogLevel.Alert);

                        pages[i] = new LazyTab(page.xPositionOnScreen, page.yPositionOnScreen, page.width, page.height,
                            (x, y, w, h) => new CollectionsPage(x, y, w, h));
                    }
                }

                // Failsafe: If the game forced us to open on a LazyTab, wake it up immediately
                if (__instance.currentTab >= 0 && __instance.currentTab < pages.Count)
                {
                    if (pages[__instance.currentTab] is LazyTab lazy)
                    {
                        if (ModEntry.Config.EnableDebugLogging)
                            ModEntry.ModMonitor.Log($"[FasterMenuLoad] Menu opened directly to Tab {__instance.currentTab}. Waking it up immediately.", LogLevel.Info);

                        pages[__instance.currentTab] = lazy.CreateRealPage();
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.ModMonitor.Log($"Error in GameMenu Postfix: {ex}", LogLevel.Error);
            }
        }

        private static void ChangeTab_Prefix(GameMenu __instance, int whichTab)
        {
            try
            {
                if (__instance.pages == null)
                {
                    ModEntry.ModMonitor.Log("GameMenu.pages is null in ChangeTab", LogLevel.Warn);
                    return;
                }

                if (whichTab < 0 || whichTab >= __instance.pages.Count) return;

                if (__instance.pages[whichTab] is LazyTab lazyTab)
                {
                    if (ModEntry.Config.EnableDebugLogging)
                        ModEntry.ModMonitor.Log($"[FasterMenuLoad] User clicked Tab {whichTab}. Loading real content now...", LogLevel.Alert);

                    // Hydrate the tab
                    __instance.pages[whichTab] = lazyTab.CreateRealPage();

                    // Controller safety
                    if (Game1.options.gamepadControls && __instance.pages[whichTab] != null)
                    {
                        __instance.pages[whichTab].snapToDefaultClickableComponent();
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.ModMonitor.Log($"Error in ChangeTab Prefix: {ex}", LogLevel.Error);
            }
        }
    }
}