using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace TrashCanExpanded
{
    public class ModEntry : Mod
    {
        public static ModEntry Instance;
        public ModConfig Config;
        private Random _rng = new Random();

        // --- ABUSE PROTECTION TRACKER ---
        // This list remembers every trash can you touched today.
        private HashSet<string> _dailyLootedCans = new HashSet<string>();

        // Item Cache
        private List<WeightedItem> _itemCache = new List<WeightedItem>();
        private bool _cacheNeedsRebuild = true;

        private struct WeightedItem
        {
            public string ItemId;
            public int Weight;
        }

        public override void Entry(IModHelper helper)
        {
            Instance = this;
            Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.DayStarted += OnDayStarted;

            var harmony = new Harmony(ModManifest.UniqueID);
            TrashPatch.Apply(harmony, Monitor);
        }

        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            // NEW DAY: Clear the tracker so you can check trash cans again tomorrow.
            _dailyLootedCans.Clear();

            if (_cacheNeedsRebuild || _itemCache.Count == 0)
                BuildItemCache();
        }

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu != null)
            {
                configMenu.Register(
                    ModManifest,
                    reset: () => { Config = new ModConfig(); _cacheNeedsRebuild = true; },
                    save: () => { Helper.WriteConfig(Config); _cacheNeedsRebuild = true; }
                );

                configMenu.AddSectionTitle(ModManifest, () => Helper.Translation.Get("config.section.general"));
                configMenu.AddBoolOption(ModManifest, () => Config.Enabled, val => Config.Enabled = val,
                    () => Helper.Translation.Get("config.enabled.name"), () => Helper.Translation.Get("config.enabled.desc"));

                configMenu.AddNumberOption(ModManifest, () => Config.MaxItemValue, val => { Config.MaxItemValue = val; _cacheNeedsRebuild = true; },
                    () => Helper.Translation.Get("config.max_value.name"), () => Helper.Translation.Get("config.max_value.desc"), min: 10, max: 5000);

                configMenu.AddSectionTitle(ModManifest, () => Helper.Translation.Get("config.section.chances"));

                configMenu.AddNumberOption(ModManifest, () => Config.ChanceMonday, val => Config.ChanceMonday = val, () => Helper.Translation.Get("config.day.mon"), null, 0f, 1f, 0.01f);
                configMenu.AddNumberOption(ModManifest, () => Config.ChanceTuesday, val => Config.ChanceTuesday = val, () => Helper.Translation.Get("config.day.tue"), null, 0f, 1f, 0.01f);
                configMenu.AddNumberOption(ModManifest, () => Config.ChanceWednesday, val => Config.ChanceWednesday = val, () => Helper.Translation.Get("config.day.wed"), null, 0f, 1f, 0.01f);
                configMenu.AddNumberOption(ModManifest, () => Config.ChanceThursday, val => Config.ChanceThursday = val, () => Helper.Translation.Get("config.day.thu"), null, 0f, 1f, 0.01f);
                configMenu.AddNumberOption(ModManifest, () => Config.ChanceFriday, val => Config.ChanceFriday = val, () => Helper.Translation.Get("config.day.fri"), null, 0f, 1f, 0.01f);
                configMenu.AddNumberOption(ModManifest, () => Config.ChanceSaturday, val => Config.ChanceSaturday = val, () => Helper.Translation.Get("config.day.sat"), null, 0f, 1f, 0.01f);
                configMenu.AddNumberOption(ModManifest, () => Config.ChanceSunday, val => Config.ChanceSunday = val, () => Helper.Translation.Get("config.day.sun"), null, 0f, 1f, 0.01f);
            }
        }

        // --- MAIN LOGIC ---

        public void OnTrashRummaged(Farmer who, GameLocation location, Vector2 tile)
        {
            if (!Config.Enabled || who != Game1.player) return;

            // 1. Create a unique ID for this exact trash can (Map Name + X,Y Coordinates)
            string canId = $"{location.Name}:{tile.X},{tile.Y}";

            // 2. CHECK THE TRACKER
            // If this ID is already in the list, it means we already processed this can today.
            // We return immediately. No chance for loot. No abuse.
            if (_dailyLootedCans.Contains(canId))
            {
                return;
            }

            // 3. ADD TO TRACKER
            // We mark it as "used" right now. Even if the RNG fails below, 
            // this can is now burned for the day.
            _dailyLootedCans.Add(canId);

            float chance = GetChanceForToday();

            // 4. Roll the dice
            if (_rng.NextDouble() < chance)
            {
                SpawnItem(who);
            }
        }

        private float GetChanceForToday()
        {
            int dayMod = Game1.dayOfMonth % 7;
            switch (dayMod)
            {
                case 1: return Config.ChanceMonday;
                case 2: return Config.ChanceTuesday;
                case 3: return Config.ChanceWednesday;
                case 4: return Config.ChanceThursday;
                case 5: return Config.ChanceFriday;
                case 6: return Config.ChanceSaturday;
                case 0: return Config.ChanceSunday;
                default: return 0f;
            }
        }

        private void SpawnItem(Farmer who)
        {
            string itemId = GetRandomItemFromCache();
            if (string.IsNullOrEmpty(itemId)) return;

            Item item = ItemRegistry.Create(itemId);
            who.addItemByMenuIfNecessary(item);
            Game1.playSound("coin");
            Monitor.Log($"[TrashCanExpanded] You found a {item.DisplayName}!", LogLevel.Trace);
        }

        private string GetRandomItemFromCache()
        {
            if (_itemCache.Count == 0) BuildItemCache();
            if (_itemCache.Count == 0) return null;

            int totalWeight = _itemCache.Sum(x => x.Weight);
            int roll = _rng.Next(0, totalWeight);
            int current = 0;

            foreach (var wItem in _itemCache)
            {
                current += wItem.Weight;
                if (roll < current) return wItem.ItemId;
            }
            return _itemCache[0].ItemId;
        }

        private void BuildItemCache()
        {
            _itemCache.Clear();
            if (Game1.objectData == null) return;

            foreach (var kvp in Game1.objectData)
            {
                var data = kvp.Value;
                int price = data.Price;

                if (price > 0 && price <= Config.MaxItemValue && data.Type != "Quest")
                {
                    int weight = (Config.MaxItemValue - price) + 10;
                    if (weight < 1) weight = 1;
                    _itemCache.Add(new WeightedItem { ItemId = kvp.Key, Weight = weight });
                }
            }
            _cacheNeedsRebuild = false;
            Monitor.Log($"[TrashCanExpanded] Item Cache Rebuilt. {Config.MaxItemValue}g Max Value.", LogLevel.Trace);
        }
    }
}