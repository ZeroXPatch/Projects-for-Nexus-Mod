using System;
using System.Collections.Generic;
using GenericModConfigMenu;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace FriendshipMaster
{
    public class ModEntry : Mod
    {
        private ModConfig Config = new();

        // Tracks previous day's friendship to calculate decay
        private Dictionary<string, int> _friendshipSnapshot = new();

        // Tracks who we have already applied the "Talk Bonus" to today
        private HashSet<string> _talkedToToday = new();

        // Tracks how many EXTRA gifts we have given specific NPCs today
        private Dictionary<string, int> _extraGiftsGiven = new();

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.DayEnding += OnDayEnding;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        }

        // 1. Snapshot friendship BEFORE sleep
        private void OnDayEnding(object? sender, DayEndingEventArgs e)
        {
            _friendshipSnapshot.Clear();
            foreach (var name in Game1.player.friendshipData.Keys)
            {
                _friendshipSnapshot[name] = Game1.player.friendshipData[name].Points;
            }
        }

        // 2. Compare Snapshot AFTER sleep
        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            _talkedToToday.Clear();
            _extraGiftsGiven.Clear();

            if (!Context.IsWorldReady) return;

            float decayMult = GetDailyDecay();

            // If Debug is on, print the multiplier being used
            if (Config.DebugMode)
                Monitor.Log($"[Friendship Master] Day Started. Decay Multiplier: {decayMult}", LogLevel.Info);

            if (Math.Abs(decayMult - 1.0f) < 0.01f)
                return;

            foreach (string name in Game1.player.friendshipData.Keys)
            {
                if (!_friendshipSnapshot.ContainsKey(name)) continue;

                int yesterdaysPoints = _friendshipSnapshot[name];
                int currentPoints = Game1.player.friendshipData[name].Points;

                if (currentPoints < yesterdaysPoints)
                {
                    int loss = yesterdaysPoints - currentPoints;
                    int allowedLoss = (int)(loss * decayMult);
                    int amountToRestore = loss - allowedLoss;

                    if (amountToRestore > 0)
                    {
                        Game1.player.friendshipData[name].Points += amountToRestore;

                        // VERIFICATION LOG
                        if (Config.DebugMode)
                        {
                            Monitor.Log($"[Decay Check] {name}: Lost {loss} pts. Restore {amountToRestore} pts.", LogLevel.Info);
                        }
                    }
                }
            }
        }

        // 3. Real-time Logic
        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.player == null) return;
            if (!e.IsMultipleOf(30)) return; // Run every 0.5 seconds

            int talkBonus = GetDailyTalkBonus();
            int allowedExtraGifts = GetDailyAddGifts();

            if (talkBonus <= 0 && allowedExtraGifts <= 0) return;

            foreach (string npcName in Game1.player.friendshipData.Keys)
            {
                var data = Game1.player.friendshipData[npcName];

                // --- Talking Bonus Logic ---
                if (talkBonus > 0 && data.TalkedToToday && !_talkedToToday.Contains(npcName))
                {
                    NPC character = Game1.getCharacterFromName(npcName);
                    if (character != null)
                    {
                        Game1.player.changeFriendship(talkBonus, character);
                        _talkedToToday.Add(npcName);

                        if (Config.DebugMode)
                            Monitor.Log($"[Talk Bonus] Added {talkBonus} pts to {npcName}.", LogLevel.Trace);
                    }
                }

                // --- Additional Gift Logic ---
                // Stardew sets "GiftsToday" to 1 immediately after gifting.
                if (allowedExtraGifts > 0 && data.GiftsToday == 1)
                {
                    // Initialize tracker if missing
                    if (!_extraGiftsGiven.ContainsKey(npcName))
                        _extraGiftsGiven[npcName] = 0;

                    // Have we reached the custom limit?
                    if (_extraGiftsGiven[npcName] < allowedExtraGifts)
                    {
                        // Reset vanilla counters so we can gift again
                        data.GiftsToday = 0;
                        data.GiftsThisWeek = Math.Max(0, data.GiftsThisWeek - 1);

                        // Increment our custom tracker
                        _extraGiftsGiven[npcName]++;

                        if (Config.DebugMode)
                            Monitor.Log($"[Gift] {npcName}: Extra gift allowed. ({_extraGiftsGiven[npcName]}/{allowedExtraGifts})", LogLevel.Trace);
                    }
                }
            }
        }

        // --- Helper: Daily Settings ---
        private float GetDailyDecay()
        {
            if (!Config.UseAdaptiveMode) return Config.Constant_DecayMultiplier;
            int dayIndex = (Game1.dayOfMonth - 1) % 7;
            switch (dayIndex)
            {
                case 0: return Config.Mon_Decay;
                case 1: return Config.Tue_Decay;
                case 2: return Config.Wed_Decay;
                case 3: return Config.Thu_Decay;
                case 4: return Config.Fri_Decay;
                case 5: return Config.Sat_Decay;
                case 6: return Config.Sun_Decay;
                default: return 1.0f;
            }
        }

        private int GetDailyTalkBonus()
        {
            if (!Config.UseAdaptiveMode) return Config.Constant_TalkBonus;
            int dayIndex = (Game1.dayOfMonth - 1) % 7;
            switch (dayIndex)
            {
                case 0: return Config.Mon_Talk;
                case 1: return Config.Tue_Talk;
                case 2: return Config.Wed_Talk;
                case 3: return Config.Thu_Talk;
                case 4: return Config.Fri_Talk;
                case 5: return Config.Sat_Talk;
                case 6: return Config.Sun_Talk;
                default: return 0;
            }
        }

        private int GetDailyAddGifts()
        {
            if (!Config.UseAdaptiveMode) return Config.Constant_AddGifts;
            int dayIndex = (Game1.dayOfMonth - 1) % 7;
            switch (dayIndex)
            {
                case 0: return Config.Mon_AddGifts;
                case 1: return Config.Tue_AddGifts;
                case 2: return Config.Wed_AddGifts;
                case 3: return Config.Thu_AddGifts;
                case 4: return Config.Fri_AddGifts;
                case 5: return Config.Sat_AddGifts;
                case 6: return Config.Sun_AddGifts;
                default: return 0;
            }
        }

        // --- GMCM Registration ---
        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null) return;

            configMenu.Register(this.ModManifest, () => this.Config = new ModConfig(), () => this.Helper.WriteConfig(this.Config));

            // General
            configMenu.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("cfg.general"));

            configMenu.AddBoolOption(this.ModManifest, () => this.Config.DebugMode, v => this.Config.DebugMode = v,
                () => this.Helper.Translation.Get("cfg.debug"), () => this.Helper.Translation.Get("cfg.debug.tooltip"));

            configMenu.AddBoolOption(this.ModManifest, () => this.Config.UseAdaptiveMode, v => this.Config.UseAdaptiveMode = v,
                () => this.Helper.Translation.Get("cfg.adaptive.toggle"), () => this.Helper.Translation.Get("cfg.adaptive.tooltip"));

            // Constant Mode
            configMenu.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("cfg.constant.title"));
            AddDayConfig(configMenu, "cfg.constant",
                () => this.Config.Constant_DecayMultiplier, v => this.Config.Constant_DecayMultiplier = v,
                () => this.Config.Constant_TalkBonus, v => this.Config.Constant_TalkBonus = v,
                () => this.Config.Constant_AddGifts, v => this.Config.Constant_AddGifts = v
            );

            // Adaptive Mode
            configMenu.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("cfg.adaptive.title"));
            configMenu.AddParagraph(this.ModManifest, () => this.Helper.Translation.Get("cfg.adaptive.note"));

            AddDayConfig(configMenu, "day.mon", () => this.Config.Mon_Decay, v => this.Config.Mon_Decay = v, () => this.Config.Mon_Talk, v => this.Config.Mon_Talk = v, () => this.Config.Mon_AddGifts, v => this.Config.Mon_AddGifts = v);
            AddDayConfig(configMenu, "day.tue", () => this.Config.Tue_Decay, v => this.Config.Tue_Decay = v, () => this.Config.Tue_Talk, v => this.Config.Tue_Talk = v, () => this.Config.Tue_AddGifts, v => this.Config.Tue_AddGifts = v);
            AddDayConfig(configMenu, "day.wed", () => this.Config.Wed_Decay, v => this.Config.Wed_Decay = v, () => this.Config.Wed_Talk, v => this.Config.Wed_Talk = v, () => this.Config.Wed_AddGifts, v => this.Config.Wed_AddGifts = v);
            AddDayConfig(configMenu, "day.thu", () => this.Config.Thu_Decay, v => this.Config.Thu_Decay = v, () => this.Config.Thu_Talk, v => this.Config.Thu_Talk = v, () => this.Config.Thu_AddGifts, v => this.Config.Thu_AddGifts = v);
            AddDayConfig(configMenu, "day.fri", () => this.Config.Fri_Decay, v => this.Config.Fri_Decay = v, () => this.Config.Fri_Talk, v => this.Config.Fri_Talk = v, () => this.Config.Fri_AddGifts, v => this.Config.Fri_AddGifts = v);
            AddDayConfig(configMenu, "day.sat", () => this.Config.Sat_Decay, v => this.Config.Sat_Decay = v, () => this.Config.Sat_Talk, v => this.Config.Sat_Talk = v, () => this.Config.Sat_AddGifts, v => this.Config.Sat_AddGifts = v);
            AddDayConfig(configMenu, "day.sun", () => this.Config.Sun_Decay, v => this.Config.Sun_Decay = v, () => this.Config.Sun_Talk, v => this.Config.Sun_Talk = v, () => this.Config.Sun_AddGifts, v => this.Config.Sun_AddGifts = v);
        }

        private void AddDayConfig(IGenericModConfigMenuApi api, string labelKey,
            Func<float> getDecay, Action<float> setDecay,
            Func<int> getTalk, Action<int> setTalk,
            Func<int> getGift, Action<int> setGift)
        {
            api.AddSubHeader(this.ModManifest, () => this.Helper.Translation.Get(labelKey));

            api.AddNumberOption(this.ModManifest, getDecay, setDecay,
                () => this.Helper.Translation.Get("opt.decay"),
                () => this.Helper.Translation.Get("opt.decay.tooltip"),
                0.0f, 2.0f, 0.1f, val => $"{val * 100:0}%");

            api.AddNumberOption(this.ModManifest, getTalk, setTalk,
                () => this.Helper.Translation.Get("opt.talk"),
                () => this.Helper.Translation.Get("opt.talk.tooltip"),
                0, 250);

            // New Slider: 0 to 10
            api.AddNumberOption(this.ModManifest, getGift, setGift,
                () => this.Helper.Translation.Get("opt.gift"),
                () => this.Helper.Translation.Get("opt.gift.tooltip"),
                0, 10);
        }
    }
}