using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using SObject = StardewValley.Object;

namespace NPCsTrashBack
{
    public class ModEntry : Mod
    {
        public static ModEntry Instance;
        public ModConfig Config;
        private Random _rng = new Random();

        // -- State Machine --
        private NPC _pendingNpc;
        private bool _dialogueWasOpen;
        private bool _waitingForDelay;
        private double _timerSeconds;

        // -- Trash Cache --
        private List<string> _trashItems = new List<string>();

        // Track per day to avoid spamming logic on the same NPC
        private HashSet<string> _actionTakenToday = new HashSet<string>();

        public override void Entry(IModHelper helper)
        {
            Instance = this;
            Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;

            // Ensure your Patcher class is also updated!
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
                    reset: () => Config = new ModConfig(),
                    save: () => Helper.WriteConfig(Config)
                );

                configMenu.AddSectionTitle(ModManifest, () => Helper.Translation.Get("config.section.general"));

                configMenu.AddBoolOption(ModManifest,
                    () => Config.Enabled,
                    val => Config.Enabled = val,
                    () => Helper.Translation.Get("config.enabled.name"),
                    () => Helper.Translation.Get("config.enabled.desc"));

                configMenu.AddNumberOption(ModManifest,
                    () => Config.BaseChance,
                    val => Config.BaseChance = val,
                    () => Helper.Translation.Get("config.base_chance.name"),
                    () => Helper.Translation.Get("config.base_chance.desc"),
                    min: 0f, max: 1f, interval: 0.01f);

                configMenu.AddSectionTitle(ModManifest, () => Helper.Translation.Get("config.section.scaling"));

                configMenu.AddBoolOption(ModManifest,
                    () => Config.EnableFriendshipScaling,
                    val => Config.EnableFriendshipScaling = val,
                    () => Helper.Translation.Get("config.scaling.name"),
                    () => Helper.Translation.Get("config.scaling.desc"));

                configMenu.AddNumberOption(ModManifest,
                    () => Config.ReductionPerHeart,
                    val => Config.ReductionPerHeart = val,
                    () => Helper.Translation.Get("config.reduction.name"),
                    () => Helper.Translation.Get("config.reduction.desc"),
                    min: 0f, max: 0.1f, interval: 0.001f);
            }
        }

        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            _actionTakenToday.Clear();
            ResetState();
            BuildTrashCache();
        }

        // --- THIS IS THE METHOD THAT WAS MISSING/WRONG IN YOUR FILE ---
        public void OnGiftGiven(NPC npc, Item gift)
        {
            if (!Config.Enabled || npc == null || gift == null) return;
            if (_actionTakenToday.Contains(npc.Name)) return;

            // 1. Check Gift Taste: 4=Dislike, 6=Hate
            int taste = npc.getGiftTasteForThisItem(gift);
            if (taste != 4 && taste != 6) return;

            // 2. Calculate Chance
            float chance = Config.BaseChance;

            if (Config.EnableFriendshipScaling)
            {
                int hearts = Game1.player.getFriendshipHeartLevelForNPC(npc.Name);
                float reduction = hearts * Config.ReductionPerHeart;
                chance -= reduction;
            }

            if (chance < 0f) chance = 0f;

            // 3. Roll Dice
            if (_rng.NextDouble() < chance)
            {
                _pendingNpc = npc;
                _dialogueWasOpen = false;
                _waitingForDelay = false;
                _timerSeconds = 0;
            }
        }
        // -------------------------------------------------------------

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
                    GiveTrashBack();
                    ResetState();
                }
            }
        }

        private void GiveTrashBack()
        {
            if (_pendingNpc == null) return;

            _actionTakenToday.Add(_pendingNpc.Name);

            string trashId = GetRandomTrashItem();
            Item trashItem = ItemRegistry.Create(trashId);

            Game1.player.addItemByMenuIfNecessary(trashItem);
            Game1.playSound("trashcan");
        }

        private void BuildTrashCache()
        {
            _trashItems.Clear();
            if (Game1.objectData == null) return;

            foreach (var kvp in Game1.objectData)
            {
                // Junk Category is -20
                if (kvp.Value.Category == SObject.junkCategory)
                {
                    _trashItems.Add(kvp.Key);
                }
            }
            if (_trashItems.Count == 0) _trashItems.Add("168"); // Trash
        }

        private string GetRandomTrashItem()
        {
            if (_trashItems.Count == 0) BuildTrashCache();
            if (_trashItems.Count == 0) return "168";

            int index = _rng.Next(_trashItems.Count);
            return _trashItems[index];
        }

        private void ResetState()
        {
            _pendingNpc = null;
            _dialogueWasOpen = false;
            _waitingForDelay = false;
        }
    }
}