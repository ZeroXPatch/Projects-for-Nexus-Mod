using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GenericModConfigMenu;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Tools;
using StardewObject = StardewValley.Object;

namespace PanningMaster
{
    public class ModEntry : Mod
    {
        private static ModConfig Config = new();
        private static IMonitor ModMonitor = null!;

        public override void Entry(IModHelper helper)
        {
            Config = helper.ReadConfig<ModConfig>();
            ModMonitor = Monitor;

            // Apply Harmony patches
            var harmony = new Harmony(this.ModManifest.UniqueID);

            try
            {
                // Patch ALL Pan types - base Pan class and any subclasses
                var assembly = typeof(Pan).Assembly;

                // Find all classes that are Pan or inherit from Pan
                var panTypes = assembly.GetTypes()
                    .Where(t => t.IsSubclassOf(typeof(Pan)) || t == typeof(Pan))
                    .ToList();

                ModMonitor.Log($"Found {panTypes.Count} Pan-related types", LogLevel.Debug);
                foreach (var panType in panTypes)
                {
                    ModMonitor.Log($"  - {panType.Name}", LogLevel.Debug);
                    PatchPanClass(harmony, panType);
                }
            }
            catch (Exception ex)
            {
                Monitor.Log($"Failed to apply Harmony patch: {ex}", LogLevel.Error);
            }

            // Events
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;

            // Commands
            helper.ConsoleCommands.Add("panning_spawn", "Spawns a panning spot near the player for testing.\nUsage: panning_spawn", this.SpawnPanningSpot);
            helper.ConsoleCommands.Add("panning_test", "Test reward spawn", this.TestReward);
        }

        private void PatchPanClass(Harmony harmony, Type panType)
        {
            try
            {
                var getPanItemsMethod = panType.GetMethod("getPanItems",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                    null,
                    new[] { typeof(GameLocation), typeof(Farmer) },
                    null);

                if (getPanItemsMethod != null && getPanItemsMethod.DeclaringType == panType)
                {
                    var postfix = typeof(ModEntry).GetMethod(nameof(GetPanItems_Postfix), BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(getPanItemsMethod, postfix: new HarmonyMethod(postfix));
                    ModMonitor.Log($"Successfully patched {panType.Name}.getPanItems", LogLevel.Info);
                }
            }
            catch (Exception ex)
            {
                ModMonitor.Log($"Failed to patch {panType.Name}: {ex.Message}", LogLevel.Debug);
            }
        }

        // Postfix for getPanItems - modifies the returned list of items
        public static void GetPanItems_Postfix(ref List<Item> __result, GameLocation location, Farmer who)
        {
            try
            {
                if (!Config.ModEnabled)
                    return;

                ModMonitor.Log("========================================", LogLevel.Alert);
                ModMonitor.Log("[Panning Master] Panning detected! Adding bonus items to treasure...", LogLevel.Alert);

                // Add our bonus items to the result list
                int day = (Game1.dayOfMonth - 1) % 7;
                ModMonitor.Log($"Day of Month: {Game1.dayOfMonth}, Day Index: {day}", LogLevel.Alert);

                __result ??= new List<Item>();

                switch (day)
                {
                    case 0: // Monday
                        ModMonitor.Log($"[MONDAY] Adding {Config.Mon_OreAmount} copper ore", LogLevel.Alert);
                        AddItemToList(__result, "(O)378", Config.Mon_OreAmount); // Copper
                        if (Config.Mon_CommonMaterials)
                        {
                            ModMonitor.Log($"[MONDAY] Adding common materials", LogLevel.Alert);
                            AddItemToList(__result, "(O)388", 1); // Wood
                            AddItemToList(__result, "(O)390", 1); // Stone
                            AddItemToList(__result, "(O)330", 1); // Clay
                        }
                        break;

                    case 1: // Tuesday
                        ModMonitor.Log($"[TUESDAY] Adding {Config.Tue_OreAmount} copper ore", LogLevel.Alert);
                        AddItemToList(__result, "(O)378", Config.Tue_OreAmount);
                        if (Config.Tue_BonusCoal)
                        {
                            ModMonitor.Log($"[TUESDAY] Adding {Config.Tue_CoalAmount} coal", LogLevel.Alert);
                            AddItemToList(__result, "(O)382", Config.Tue_CoalAmount); // Coal
                        }
                        break;

                    case 2: // Wednesday
                        ModMonitor.Log($"[WEDNESDAY] Adding {Config.Wed_OreAmount} copper ore", LogLevel.Alert);
                        AddItemToList(__result, "(O)378", Config.Wed_OreAmount);
                        if (Config.Wed_CopperIron)
                        {
                            ModMonitor.Log($"[WEDNESDAY] Adding copper/iron", LogLevel.Alert);
                            AddItemToList(__result, "(O)378", 1); // Copper
                            AddItemToList(__result, "(O)380", 2); // Iron
                        }
                        break;

                    case 3: // Thursday
                        ModMonitor.Log($"[THURSDAY] Adding {Config.Thu_OreAmount} iron ore", LogLevel.Alert);
                        AddItemToList(__result, "(O)380", Config.Thu_OreAmount); // Iron
                        if (Config.Thu_GoldChance)
                        {
                            ModMonitor.Log($"[THURSDAY] Adding 2 gold ore", LogLevel.Alert);
                            AddItemToList(__result, "(O)384", 2); // Gold
                        }
                        break;

                    case 4: // Friday
                        ModMonitor.Log($"[FRIDAY] Adding {Config.Fri_OreAmount} gold ore", LogLevel.Alert);
                        AddItemToList(__result, "(O)384", Config.Fri_OreAmount); // Gold
                        if (Config.Fri_IridiumChance)
                        {
                            ModMonitor.Log($"[FRIDAY] Adding 1 iridium ore", LogLevel.Alert);
                            AddItemToList(__result, "(O)386", 1); // Iridium
                        }
                        break;

                    case 5: // Saturday
                        ModMonitor.Log($"[SATURDAY] Adding {Config.Sat_OreAmount} gold ore", LogLevel.Alert);
                        AddItemToList(__result, "(O)384", Config.Sat_OreAmount);
                        double roll = Game1.random.NextDouble();
                        ModMonitor.Log($"[SATURDAY] Prismatic roll: {roll} vs {Config.Sat_PrismaticChance}", LogLevel.Alert);
                        if (roll < Config.Sat_PrismaticChance)
                        {
                            ModMonitor.Log($"[SATURDAY] Adding prismatic shard!", LogLevel.Alert);
                            AddItemToList(__result, "(O)74", 1); // Prismatic Shard
                        }
                        break;

                    case 6: // Sunday
                        ModMonitor.Log($"[SUNDAY] Adding {Config.Sun_OreAmount} iridium ore", LogLevel.Alert);
                        AddItemToList(__result, "(O)386", Config.Sun_OreAmount); // Iridium
                        if (Config.Sun_GuaranteedGem)
                        {
                            ModMonitor.Log($"[SUNDAY] Adding gem", LogLevel.Alert);
                            var gems = new[] { "(O)72", "(O)60", "(O)64", "(O)62" };
                            AddItemToList(__result, gems[Game1.random.Next(gems.Length)], 1);
                        }
                        break;
                }

                ModMonitor.Log($"Total items in treasure now: {__result.Count}", LogLevel.Alert);
                ModMonitor.Log("========================================", LogLevel.Alert);
            }
            catch (Exception ex)
            {
                ModMonitor.Log($"Error in GetPanItems_Postfix: {ex}", LogLevel.Error);
            }
        }

        private static void AddItemToList(List<Item> list, string itemId, int quantity)
        {
            try
            {
                var item = ItemRegistry.Create(itemId, quantity);
                if (item != null)
                {
                    list.Add(item);
                    ModMonitor.Log($"  Added {quantity}x {item.Name}", LogLevel.Debug);
                }
            }
            catch (Exception ex)
            {
                ModMonitor.Log($"Failed to create item {itemId}: {ex}", LogLevel.Warn);
            }
        }

        private void TestReward(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                Monitor.Log("Save not loaded!", LogLevel.Warn);
                return;
            }

            Monitor.Log("Testing reward spawn...", LogLevel.Alert);
            SpawnOre(10, "(O)378", Game1.player);
        }

        // --- Logic: Spawn Panning Spot for Testing ---
        private void SpawnPanningSpot(string command, string[] args)
        {
            if (!Context.IsWorldReady || Game1.currentLocation == null)
            {
                Monitor.Log("Save not loaded!", LogLevel.Warn);
                return;
            }

            var location = Game1.currentLocation;
            var playerTile = Game1.player.TilePoint;
            bool found = false;

            for (int x = playerTile.X - 10; x <= playerTile.X + 10; x++)
            {
                for (int y = playerTile.Y - 10; y <= playerTile.Y + 10; y++)
                {
                    if (location.isOpenWater(x, y))
                    {
                        location.orePanPoint.Value = new Point(x, y);
                        Monitor.Log($"Spawned panning spot at ({x}, {y})", LogLevel.Alert);
                        Game1.playSound("slosh");
                        found = true;
                        break;
                    }
                }
                if (found) break;
            }

            if (!found) Monitor.Log("No open water found near player!", LogLevel.Warn);
        }

        private static void SpawnOre(int amount, string itemId, Farmer who)
        {
            if (amount <= 0) return;
            Game1.createMultipleObjectDebris(itemId, (int)who.Tile.X, (int)who.Tile.Y, amount, who.UniqueMultiplayerID);
        }

        // --- GMCM ---
        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null) return;

            configMenu.Register(this.ModManifest, () => Config = new ModConfig(), () => this.Helper.WriteConfig(Config));

            configMenu.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("config.general"));
            configMenu.AddBoolOption(this.ModManifest, () => Config.ModEnabled, v => Config.ModEnabled = v,
                () => this.Helper.Translation.Get("config.enabled.name"), () => this.Helper.Translation.Get("config.enabled.tooltip"));
            configMenu.AddBoolOption(this.ModManifest, () => Config.DebugMode, v => Config.DebugMode = v,
                () => "Debug Log", () => "Print to console when reward triggers.");

            AddDayConfig(configMenu, "day.monday", () => Config.Mon_OreAmount, v => Config.Mon_OreAmount = v,
                () => Config.Mon_CommonMaterials, v => Config.Mon_CommonMaterials = v, "option.common_mats");

            AddDayConfig(configMenu, "day.tuesday", () => Config.Tue_OreAmount, v => Config.Tue_OreAmount = v,
                () => Config.Tue_BonusCoal, v => Config.Tue_BonusCoal = v, "option.bonus_coal");

            AddDayConfig(configMenu, "day.wednesday", () => Config.Wed_OreAmount, v => Config.Wed_OreAmount = v,
                () => Config.Wed_CopperIron, v => Config.Wed_CopperIron = v, "option.copper_iron");

            AddDayConfig(configMenu, "day.thursday", () => Config.Thu_OreAmount, v => Config.Thu_OreAmount = v,
                () => Config.Thu_GoldChance, v => Config.Thu_GoldChance = v, "option.gold_bonus");

            AddDayConfig(configMenu, "day.friday", () => Config.Fri_OreAmount, v => Config.Fri_OreAmount = v,
                () => Config.Fri_IridiumChance, v => Config.Fri_IridiumChance = v, "option.iridium_bonus");

            configMenu.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("day.saturday"));
            configMenu.AddNumberOption(this.ModManifest, () => Config.Sat_OreAmount, v => Config.Sat_OreAmount = v,
                () => this.Helper.Translation.Get("option.ore_amount"), null, 0, 50);
            configMenu.AddNumberOption(this.ModManifest, () => Config.Sat_PrismaticChance, v => Config.Sat_PrismaticChance = v,
                () => this.Helper.Translation.Get("option.prismatic_chance"), null, 0f, 1f, 0.01f);

            configMenu.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("day.sunday"));
            configMenu.AddNumberOption(this.ModManifest, () => Config.Sun_OreAmount, v => Config.Sun_OreAmount = v,
                () => this.Helper.Translation.Get("option.ore_amount"), null, 0, 50);
            configMenu.AddBoolOption(this.ModManifest, () => Config.Sun_GuaranteedGem, v => Config.Sun_GuaranteedGem = v,
                () => this.Helper.Translation.Get("option.rare_gem"));
        }

        private void AddDayConfig(IGenericModConfigMenuApi api, string dayKey, Func<int> getOre, Action<int> setOre, Func<bool> getBonus, Action<bool> setBonus, string bonusKey)
        {
            api.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get(dayKey));
            api.AddNumberOption(this.ModManifest, getOre, setOre, () => this.Helper.Translation.Get("option.ore_amount"), null, 0, 50);
            api.AddBoolOption(this.ModManifest, getBonus, setBonus, () => this.Helper.Translation.Get(bonusKey));
        }
    }
}