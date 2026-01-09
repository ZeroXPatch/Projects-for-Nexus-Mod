using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using SObject = StardewValley.Object;

namespace GiftBack
{
    public class ModEntry : Mod
    {
        public static ModEntry Instance;
        public ModConfig Config;
        private Random _rng = new Random();

        // -- State Machine Variables --
        private NPC _pendingNpc;
        private bool _dialogueWasOpen;
        private bool _waitingForDelay;
        private double _timerSeconds;

        private HashSet<string> _giftedBackToday = new HashSet<string>();
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
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;

            var harmony = new Harmony(ModManifest.UniqueID);
            Patcher.Apply(harmony, Monitor);
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

                // General
                configMenu.AddSectionTitle(ModManifest, () => Helper.Translation.Get("config.section.general"));
                configMenu.AddBoolOption(ModManifest, () => Config.Enabled, val => Config.Enabled = val, 
                    () => Helper.Translation.Get("config.enabled.name"), () => Helper.Translation.Get("config.enabled.desc"));

                configMenu.AddNumberOption(ModManifest, 
                    () => Config.BaseChance, 
                    val => Config.BaseChance = val, 
                    () => Helper.Translation.Get("config.base_chance.name"), 
                    () => Helper.Translation.Get("config.base_chance.desc"), 
                    min: 0f, max: 1f, interval: 0.01f);
                
                // Friendship
                configMenu.AddSectionTitle(ModManifest, () => Helper.Translation.Get("config.section.friendship"));
                configMenu.AddBoolOption(ModManifest, () => Config.EnableFriendshipScaling, val => Config.EnableFriendshipScaling = val, 
                    () => Helper.Translation.Get("config.scaling.name"), () => Helper.Translation.Get("config.scaling.desc"));

                // UPDATED: Added interval 0.001f so you can select 0.005 in the menu
                configMenu.AddNumberOption(ModManifest, 
                    () => Config.ChancePerHeart, 
                    val => Config.ChancePerHeart = val, 
                    () => Helper.Translation.Get("config.heart_chance.name"), 
                    () => Helper.Translation.Get("config.heart_chance.desc"), 
                    min: 0f, max: 0.1f, interval: 0.001f);

                // Items
                configMenu.AddSectionTitle(ModManifest, () => Helper.Translation.Get("config.section.items"));
                configMenu.AddNumberOption(ModManifest, 
                    () => Config.MaxGiftValue, 
                    val => { Config.MaxGiftValue = val; _cacheNeedsRebuild = true; }, 
                    () => Helper.Translation.Get("config.max_value.name"), 
                    () => Helper.Translation.Get("config.max_value.desc"), 
                    min: 10, max: 5000);
            }
        }

        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            _giftedBackToday.Clear();
            ResetState();
            if (_cacheNeedsRebuild || _itemCache.Count == 0) BuildItemCache();
        }

        public void OnGiftGiven(NPC npc)
        {
            if (!Config.Enabled || npc == null) return;
            if (_giftedBackToday.Contains(npc.Name)) return;

            float chance = Config.BaseChance;
            if (Config.EnableFriendshipScaling)
            {
                int hearts = Game1.player.getFriendshipHeartLevelForNPC(npc.Name);
                chance += (hearts * Config.ChancePerHeart);
            }

            if (chance > 1.0f) chance = 1.0f;

            if (_rng.NextDouble() < chance)
            {
                _pendingNpc = npc;
                _dialogueWasOpen = false;
                _waitingForDelay = false;
                _timerSeconds = 0;
            }
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (_pendingNpc == null) return;

            if (Game1.activeClickableMenu is DialogueBox)
            {
                _dialogueWasOpen = true;
                return;
            }

            if (_dialogueWasOpen && Game1.activeClickableMenu == null && !_waitingForDelay)
            {
                _waitingForDelay = true;
                _timerSeconds = 0.5; 
                return;
            }

            if (_waitingForDelay)
            {
                _timerSeconds -= Game1.currentGameTime.ElapsedGameTime.TotalSeconds;

                if (_timerSeconds <= 0)
                {
                    ExecuteGiftBack();
                    ResetState();
                }
            }
        }

        private void ExecuteGiftBack()
        {
            if (_pendingNpc == null) return;
            _giftedBackToday.Add(_pendingNpc.Name);

            string itemId = GetRandomItemFromCache();
            if (string.IsNullOrEmpty(itemId)) return;

            Item item = ItemRegistry.Create(itemId);
            Game1.player.addItemByMenuIfNecessary(item);
            Game1.playSound("coin");
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

                if (price > 0 && price <= Config.MaxGiftValue && data.Type != "Quest")
                {
                    int weight = (Config.MaxGiftValue - price) + 10;
                    if (weight < 1) weight = 1;
                    _itemCache.Add(new WeightedItem { ItemId = kvp.Key, Weight = weight });
                }
            }
            _cacheNeedsRebuild = false;
        }

        private void ResetState()
        {
            _pendingNpc = null;
            _dialogueWasOpen = false;
            _waitingForDelay = false;
        }
    }
}