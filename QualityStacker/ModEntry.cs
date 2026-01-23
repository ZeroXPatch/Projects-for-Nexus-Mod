using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace QualityStacker
{
    public class ModEntry : Mod
    {
        public static ModConfig Config = new();
        public static IMonitor SMonitor = null!;
        public const string ModDataKey = "ZeroXPatch.QualityStacker.Data";

        // Track which quality was just taken in getOne
        private static int lastTakenQuality = -1;
        // Track to prevent multiple consume calls
        private static bool isConsuming = false;

        public override void Entry(IModHelper helper)
        {
            SMonitor = Monitor;
            Config = helper.ReadConfig<ModConfig>();

            var harmony = new Harmony(ModManifest.UniqueID);

            // Patch 1: Allow different qualities to attempt stacking
            harmony.Patch(
                original: AccessTools.Method(typeof(StardewValley.Item), nameof(StardewValley.Item.canStackWith)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(CanStackWith_Prefix))
            );

            // Patch 2: Capture data when stacking happens
            harmony.Patch(
                original: AccessTools.Method(typeof(StardewValley.Item), nameof(StardewValley.Item.addToStack)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(AddToStack_Prefix))
            );

            // Patch 3: Show contents in tooltip
            harmony.Patch(
                original: AccessTools.Method(typeof(StardewValley.Object), nameof(StardewValley.Object.getDescription)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(GetDescription_Postfix))
            );

            // Patch 4: Handle "Unstacking" (Right-Clicking) - track which quality
            harmony.Patch(
                original: AccessTools.Method(typeof(StardewValley.Item), nameof(StardewValley.Item.getOne)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(GetOne_Postfix))
            );

            // Patch 5: Handle stack consumption (when vanilla reduces the stack)
            // NOTE: We DON'T patch ConsumeStack - let vanilla handle it, we'll sync after
            // This prevents inventory validation issues
            /*
            harmony.Patch(
                original: AccessTools.Method(typeof(StardewValley.Item), nameof(StardewValley.Item.ConsumeStack)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(ConsumeStack_Prefix))
            );
            */
            harmony.Patch(
                original: AccessTools.Method(typeof(StardewValley.Item), nameof(StardewValley.Item.ConsumeStack)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(ConsumeStack_Postfix))
            );

            // Patch 6: Intercept inventory right-click to handle proper quality merging
            harmony.Patch(
                original: AccessTools.Method(typeof(StardewValley.Menus.InventoryMenu), nameof(StardewValley.Menus.InventoryMenu.rightClick)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(InventoryMenu_RightClick_Prefix))
            );

            // Patch 7: Draw Visual Indicator (Cyan Star)
            harmony.Patch(
                original: AccessTools.Method(typeof(StardewValley.Object), nameof(StardewValley.Object.drawInMenu),
                new[] { typeof(SpriteBatch), typeof(Vector2), typeof(float), typeof(float), typeof(float), typeof(StackDrawType), typeof(Color), typeof(bool) }),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(DrawInMenu_Postfix))
            );

            SMonitor.Log("Quality Stacker patches applied successfully!", LogLevel.Info);
        }

        // --- Helpers ---
        public static Dictionary<int, int> ParseQualities(Item item)
        {
            var dict = new Dictionary<int, int>();
            if (item.modData.TryGetValue(ModDataKey, out string data) && !string.IsNullOrEmpty(data))
            {
                var pairs = data.Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (var pair in pairs)
                {
                    var parts = pair.Split(':');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int q) && int.TryParse(parts[1], out int c))
                    {
                        if (dict.ContainsKey(q)) dict[q] += c;
                        else dict[q] = c;
                    }
                }
                SMonitor.Log($"[ParseQualities] Parsed from modData: {string.Join(", ", dict.Select(kv => $"Q{kv.Key}:{kv.Value}"))}", LogLevel.Debug);
            }
            else
            {
                // Item has no mod data, so it's a single-quality stack
                dict[item.Quality] = item.Stack;
                SMonitor.Log($"[ParseQualities] No modData, using item quality: Q{item.Quality}:{item.Stack}", LogLevel.Debug);
            }
            return dict;
        }

        public static void SaveQualities(Item item, Dictionary<int, int> dict)
        {
            SMonitor.Log($"[SaveQualities] BEFORE - Stack: {item.Stack}, Quality: {item.Quality}, ModData: {(item.modData.ContainsKey(ModDataKey) ? item.modData[ModDataKey] : "none")}", LogLevel.Debug);
            SMonitor.Log($"[SaveQualities] Dict contents: {string.Join(", ", dict.Select(kv => $"Q{kv.Key}:{kv.Value}"))}", LogLevel.Debug);

            var sb = new StringBuilder();
            foreach (var kvp in dict)
            {
                if (kvp.Value > 0) sb.Append($"{kvp.Key}:{kvp.Value};");
            }

            var distinct = dict.Where(k => k.Value > 0).Select(k => k.Key).ToList();

            if (distinct.Count == 1)
            {
                // Only one quality remains - convert back to normal item
                item.Quality = distinct[0];
                item.Stack = dict[distinct[0]];
                item.modData.Remove(ModDataKey);
                SMonitor.Log($"[SaveQualities] Single quality remaining - Quality: {item.Quality}, Stack: {item.Stack}", LogLevel.Debug);
            }
            else if (distinct.Count > 1)
            {
                // Multiple qualities - save as mixed
                item.modData[ModDataKey] = sb.ToString();
                item.Quality = 0; // Mixed bundles look like Regular quality
                item.Stack = dict.Values.Sum();
                SMonitor.Log($"[SaveQualities] Mixed qualities - Stack: {item.Stack}, ModData: {item.modData[ModDataKey]}", LogLevel.Debug);
            }
            else
            {
                // No items left
                item.Stack = 0;
                item.modData.Remove(ModDataKey);
                SMonitor.Log($"[SaveQualities] No items left - Stack: 0", LogLevel.Debug);
            }

            SMonitor.Log($"[SaveQualities] AFTER - Stack: {item.Stack}, Quality: {item.Quality}", LogLevel.Debug);
        }

        // --- Patches ---

        // PATCH 1: Logic to allow stacking
        public static bool CanStackWith_Prefix(StardewValley.Item __instance, ISalable other, ref bool __result)
        {
            if (!Config.Enabled || __instance is not StardewValley.Object objInstance || other is not StardewValley.Object otherObj)
                return true;

            if (objInstance.ParentSheetIndex == otherObj.ParentSheetIndex &&
                objInstance.Name == otherObj.Name &&
                !objInstance.bigCraftable.Value &&
                !otherObj.bigCraftable.Value)
            {
                if (objInstance is StardewValley.Objects.ColoredObject c1 && otherObj is StardewValley.Objects.ColoredObject c2)
                {
                    if (c1.color.Value != c2.color.Value) return true;
                }

                SMonitor.Log($"[CanStackWith] Allowing stack: {objInstance.Name} Q{objInstance.Quality} + Q{otherObj.Quality}", LogLevel.Debug);
                __result = true;
                return false;
            }
            return true;
        }

        // PATCH 2: Logic to save data when stacking
        public static bool AddToStack_Prefix(StardewValley.Item __instance, Item otherStack, ref int __result)
        {
            SMonitor.Log($"[AddToStack] CALLED - Checking if enabled and valid objects", LogLevel.Debug);

            if (!Config.Enabled)
            {
                SMonitor.Log($"[AddToStack] Config disabled, using vanilla", LogLevel.Debug);
                return true;
            }

            if (__instance is not StardewValley.Object objInstance || otherStack is not StardewValley.Object)
            {
                SMonitor.Log($"[AddToStack] Not both Objects, using vanilla", LogLevel.Debug);
                return true;
            }

            SMonitor.Log($"[AddToStack] START - Dest: {objInstance.Name} Stack:{objInstance.Stack} Q:{objInstance.Quality}, Source: Stack:{otherStack.Stack} Q:{otherStack.Quality}", LogLevel.Debug);
            SMonitor.Log($"[AddToStack] Dest modData: {(objInstance.modData.ContainsKey(ModDataKey) ? objInstance.modData[ModDataKey] : "none")}", LogLevel.Debug);
            SMonitor.Log($"[AddToStack] Source modData: {(otherStack.modData.ContainsKey(ModDataKey) ? otherStack.modData[ModDataKey] : "none")}", LogLevel.Debug);

            int max = objInstance.maximumStackSize();
            int spaceLeft = max - objInstance.Stack;

            if (spaceLeft <= 0)
            {
                __result = otherStack.Stack;
                SMonitor.Log($"[AddToStack] No space left, returning {__result}", LogLevel.Debug);
                return false;
            }

            int amountToAdd = Math.Min(spaceLeft, otherStack.Stack);

            var destDict = ParseQualities(objInstance);
            var srcDict = ParseQualities(otherStack);

            SMonitor.Log($"[AddToStack] Dest dict before: {string.Join(", ", destDict.Select(kv => $"Q{kv.Key}:{kv.Value}"))}", LogLevel.Debug);
            SMonitor.Log($"[AddToStack] Source dict: {string.Join(", ", srcDict.Select(kv => $"Q{kv.Key}:{kv.Value}"))}", LogLevel.Debug);

            // Merge the dictionaries - add each quality from source
            foreach (var kvp in srcDict)
            {
                // Only add the amount we're actually transferring
                int amountOfThisQuality = Math.Min(kvp.Value, amountToAdd);

                if (destDict.ContainsKey(kvp.Key))
                {
                    destDict[kvp.Key] += amountOfThisQuality;
                }
                else
                {
                    destDict[kvp.Key] = amountOfThisQuality;
                }

                SMonitor.Log($"[AddToStack] Added {amountOfThisQuality} of quality {kvp.Key} to destination", LogLevel.Debug);
            }

            SMonitor.Log($"[AddToStack] Dest dict after: {string.Join(", ", destDict.Select(kv => $"Q{kv.Key}:{kv.Value}"))}", LogLevel.Debug);

            SaveQualities(objInstance, destDict);

            __result = Math.Max(0, otherStack.Stack - amountToAdd);
            SMonitor.Log($"[AddToStack] END - New stack: {objInstance.Stack}, Remainder: {__result}", LogLevel.Debug);
            return false;
        }

        // PATCH 3: Tooltip text
        public static void GetDescription_Postfix(StardewValley.Object __instance, ref string __result)
        {
            if (!Config.Enabled || !__instance.modData.ContainsKey(ModDataKey)) return;

            var dict = ParseQualities(__instance);
            if (dict.Count <= 1) return;

            StringBuilder sb = new();
            sb.AppendLine();
            sb.AppendLine("--- Mixed Bundle ---");
            foreach (var kvp in dict.OrderByDescending(k => k.Key))
            {
                if (kvp.Value > 0)
                {
                    string qName = kvp.Key switch { 0 => "Regular", 1 => "Silver", 2 => "Gold", 4 => "Iridium", _ => "?" };
                    sb.AppendLine($"{qName}: {kvp.Value}");
                }
            }
            __result += sb.ToString();
        }

        // PATCH 4: Track which quality is being taken
        public static void GetOne_Postfix(StardewValley.Item __instance, ref Item __result)
        {
            if (!Config.Enabled || !__instance.modData.ContainsKey(ModDataKey)) return;
            if (__instance is not StardewValley.Object objInstance) return;

            SMonitor.Log($"[GetOne] START - {objInstance.Name} Stack:{objInstance.Stack} Q:{objInstance.Quality}", LogLevel.Debug);

            var dict = ParseQualities(__instance);
            if (dict.Count <= 1)
            {
                SMonitor.Log($"[GetOne] Single quality, skipping", LogLevel.Debug);
                return;
            }

            // Find highest quality item to give
            int bestQuality = -1;
            foreach (var q in dict.Keys.OrderByDescending(k => k))
            {
                if (dict[q] > 0) { bestQuality = q; break; }
            }

            if (bestQuality == -1)
            {
                SMonitor.Log($"[GetOne] No quality found with count > 0", LogLevel.Warn);
                return;
            }

            // IMPORTANT: Make sure the result is truly independent
            // Force it to be a fresh clone by creating a new object
            if (__result is StardewValley.Object resultObj)
            {
                // Set quality and stack on the cloned item
                resultObj.Quality = bestQuality;
                resultObj.Stack = 1;
                resultObj.modData.Remove(ModDataKey);

                // Remember this quality for ConsumeStack
                lastTakenQuality = bestQuality;

                SMonitor.Log($"[GetOne] Created independent clone with quality {bestQuality}, set lastTakenQuality={lastTakenQuality}", LogLevel.Debug);
                SMonitor.Log($"[GetOne] Clone Stack: {resultObj.Stack}, Source Stack: {objInstance.Stack}", LogLevel.Debug);
            }
        }

        // PATCH 5: Sync quality data after vanilla consumes the stack
        public static void ConsumeStack_Postfix(StardewValley.Item __instance, int amount)
        {
            if (!Config.Enabled || !__instance.modData.ContainsKey(ModDataKey))
            {
                lastTakenQuality = -1;
                return;
            }

            SMonitor.Log($"[ConsumeStack_Postfix] START - {__instance.Name} Stack:{__instance.Stack}, amount:{amount}, lastTakenQuality:{lastTakenQuality}", LogLevel.Debug);

            var dict = ParseQualities(__instance);
            if (dict.Count <= 1)
            {
                SMonitor.Log($"[ConsumeStack_Postfix] Single quality, nothing to sync", LogLevel.Debug);
                lastTakenQuality = -1;
                return;
            }

            // Vanilla already reduced the stack, now we need to sync our quality data
            int currentStack = __instance.Stack;
            int totalInDict = dict.Values.Sum();

            SMonitor.Log($"[ConsumeStack_Postfix] Current stack: {currentStack}, Dict total: {totalInDict}", LogLevel.Debug);

            // If they don't match, we need to remove the consumed amount from our dict
            if (totalInDict > currentStack)
            {
                int toRemove = totalInDict - currentStack;
                SMonitor.Log($"[ConsumeStack_Postfix] Need to remove {toRemove} from quality dict", LogLevel.Debug);

                // Remove from the quality we tracked (if available)
                if (lastTakenQuality != -1 && dict.ContainsKey(lastTakenQuality) && dict[lastTakenQuality] > 0)
                {
                    int removed = Math.Min(dict[lastTakenQuality], toRemove);
                    dict[lastTakenQuality] -= removed;
                    toRemove -= removed;
                    SMonitor.Log($"[ConsumeStack_Postfix] Removed {removed} from tracked quality {lastTakenQuality}", LogLevel.Debug);
                    lastTakenQuality = -1;
                }

                // Remove any remaining from highest quality
                foreach (var q in dict.Keys.OrderByDescending(k => k).ToList())
                {
                    if (toRemove <= 0) break;
                    if (dict[q] > 0)
                    {
                        int removed = Math.Min(dict[q], toRemove);
                        dict[q] -= removed;
                        toRemove -= removed;
                        SMonitor.Log($"[ConsumeStack_Postfix] Removed {removed} from quality {q}", LogLevel.Debug);
                    }
                }

                SaveQualities(__instance, dict);
                SMonitor.Log($"[ConsumeStack_Postfix] Synced - New stack: {__instance.Stack}", LogLevel.Debug);
            }
            else
            {
                SMonitor.Log($"[ConsumeStack_Postfix] Already in sync, no changes needed", LogLevel.Debug);
                lastTakenQuality = -1;
            }
        }

        // PATCH 6: Handle right-click in inventory to properly merge qualities
        public static bool InventoryMenu_RightClick_Prefix(InventoryMenu __instance, int x, int y, Item toAddTo, bool playSound)
        {
            if (!Config.Enabled) return true;

            // Get the clicked item from inventory
            Item clickedItem = __instance.getItemAt(x, y);

            if (clickedItem == null || toAddTo == null) return true;
            if (clickedItem is not StardewValley.Object || toAddTo is not StardewValley.Object) return true;

            // Check if either has mixed quality data
            bool clickedHasMixedQuality = clickedItem.modData.ContainsKey(ModDataKey);
            bool cursorHasMixedQuality = toAddTo.modData.ContainsKey(ModDataKey);

            if (!clickedHasMixedQuality && !cursorHasMixedQuality)
            {
                // Neither has mixed quality - but we still need to track if they're different qualities
                if (clickedItem.Quality != toAddTo.Quality && clickedItem.canStackWith(toAddTo))
                {
                    // They're stacking different qualities - we need to handle this
                    SMonitor.Log($"[InventoryRightClick] Stacking different single qualities: Clicked Q{clickedItem.Quality} + Cursor Q{toAddTo.Quality}", LogLevel.Debug);

                    var clickedDict = new Dictionary<int, int> { { clickedItem.Quality, clickedItem.Stack } };
                    var cursorDict = new Dictionary<int, int> { { toAddTo.Quality, toAddTo.Stack } };

                    // Find best quality from clicked
                    int qualityToTake = clickedDict.Keys.OrderByDescending(k => k).First();

                    // Remove from clicked
                    clickedDict[qualityToTake]--;

                    // Add to cursor  
                    if (cursorDict.ContainsKey(qualityToTake))
                        cursorDict[qualityToTake]++;
                    else
                        cursorDict[qualityToTake] = 1;

                    SaveQualities(clickedItem, clickedDict);
                    SaveQualities(toAddTo, cursorDict);

                    if (playSound)
                        Game1.playSound("dwop");

                    return false;
                }

                return true; // Same quality, use vanilla
            }

            SMonitor.Log($"[InventoryRightClick] Clicked: {clickedItem.Name} Stack:{clickedItem.Stack} Q:{clickedItem.Quality}", LogLevel.Debug);
            SMonitor.Log($"[InventoryRightClick] Cursor: {toAddTo.Name} Stack:{toAddTo.Stack} Q:{toAddTo.Quality}", LogLevel.Debug);

            // Check if they can stack
            if (!clickedItem.canStackWith(toAddTo)) return true;

            // Manually handle the stacking with quality preservation
            var clickedDictMixed = ParseQualities(clickedItem);
            var cursorDictMixed = ParseQualities(toAddTo);

            SMonitor.Log($"[InventoryRightClick] Clicked dict: {string.Join(", ", clickedDictMixed.Select(kv => $"Q{kv.Key}:{kv.Value}"))}", LogLevel.Debug);
            SMonitor.Log($"[InventoryRightClick] Cursor dict: {string.Join(", ", cursorDictMixed.Select(kv => $"Q{kv.Key}:{kv.Value}"))}", LogLevel.Debug);

            // Take 1 from clicked and add to cursor
            int qualityToTakeMixed = -1;
            foreach (var q in clickedDictMixed.Keys.OrderByDescending(k => k))
            {
                if (clickedDictMixed[q] > 0)
                {
                    qualityToTakeMixed = q;
                    break;
                }
            }

            if (qualityToTakeMixed == -1) return true;

            // Remove from clicked
            clickedDictMixed[qualityToTakeMixed]--;

            // Add to cursor
            if (cursorDictMixed.ContainsKey(qualityToTakeMixed))
                cursorDictMixed[qualityToTakeMixed]++;
            else
                cursorDictMixed[qualityToTakeMixed] = 1;

            // Save both
            SaveQualities(clickedItem, clickedDictMixed);
            SaveQualities(toAddTo, cursorDictMixed);

            if (playSound)
                Game1.playSound("dwop");

            SMonitor.Log($"[InventoryRightClick] After - Clicked: Stack:{clickedItem.Stack}, Cursor: Stack:{toAddTo.Stack}", LogLevel.Debug);

            return false; // Skip vanilla
        }

        // PATCH 7: Visual Indicator (Cyan Star)
        public static void DrawInMenu_Postfix(StardewValley.Object __instance, SpriteBatch spriteBatch, Vector2 location, float scaleSize, float layerDepth)
        {
            // Quick check: if no mod data, skip entirely (no parsing needed)
            if (!Config.Enabled || !__instance.modData.ContainsKey(ModDataKey)) return;

            // Simple check: if the raw data contains multiple semicolons, it's mixed
            // This avoids parsing on every draw call
            string data = __instance.modData[ModDataKey];
            int semicolonCount = data.Count(c => c == ';');
            if (semicolonCount <= 1) return; // Single quality or empty

            float scale = 4f * scaleSize;
            Vector2 position = location + new Vector2(12f, 52f);
            Rectangle sourceRect = new Rectangle(346, 400, 8, 8);

            spriteBatch.Draw(Game1.mouseCursors, position, sourceRect, Color.Cyan, 0f, new Vector2(4f, 4f), scale, SpriteEffects.None, layerDepth + 0.001f);
        }
    }
}