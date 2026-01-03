using System;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace DynamicDusk
{
    public class ModEntry : Mod
    {
        public static ModConfig Config = null!;
        public static IMonitor SMonitor = null!;

        // Default to 1800 (6 PM)
        public static int CurrentDailySunset = 1800;

        private const string ModDataKey = "Zero.DynamicDusk/CurrentSunset";

        public override void Entry(IModHelper helper)
        {
            Config = helper.ReadConfig<ModConfig>();
            SMonitor = Monitor;

            // STEP 1: Aggressively Fix Broken Configs
            // If the user's config file has "0"s, fix them and SAVE the file immediately.
            bool fixNeeded = false;
            if (Config.SpringMinTime < 600) { Config.SpringMinTime = 1700; fixNeeded = true; }
            if (Config.SpringMaxTime < 600) { Config.SpringMaxTime = 1900; fixNeeded = true; }
            if (Config.SummerMinTime < 600) { Config.SummerMinTime = 1800; fixNeeded = true; }
            if (Config.SummerMaxTime < 600) { Config.SummerMaxTime = 2030; fixNeeded = true; }
            if (Config.FallMinTime < 600) { Config.FallMinTime = 1630; fixNeeded = true; }
            if (Config.FallMaxTime < 600) { Config.FallMaxTime = 1830; fixNeeded = true; }
            if (Config.WinterMinTime < 600) { Config.WinterMinTime = 1530; fixNeeded = true; }
            if (Config.WinterMaxTime < 600) { Config.WinterMaxTime = 1700; fixNeeded = true; }

            if (Config.ManualSpringTime < 600) { Config.ManualSpringTime = 1800; fixNeeded = true; }
            if (Config.ManualSummerTime < 600) { Config.ManualSummerTime = 1900; fixNeeded = true; }
            if (Config.ManualFallTime < 600) { Config.ManualFallTime = 1730; fixNeeded = true; }
            if (Config.ManualWinterTime < 600) { Config.ManualWinterTime = 1630; fixNeeded = true; }

            if (fixNeeded)
            {
                Monitor.Log("[Dynamic Dusk] Detected corrupted config (Zeros). repairing and saving...", LogLevel.Warn);
                helper.WriteConfig(Config);
            }

            var harmony = new Harmony(this.ModManifest.UniqueID);

            // --- TIME PATCHES ---
            harmony.Patch(
                original: AccessTools.Method(typeof(Game1), nameof(Game1.getStartingToGetDarkTime)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(Postfix_GetStartingToGetDarkTime))
                { priority = Priority.Last }
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(Game1), nameof(Game1.getTrulyDarkTime)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(Postfix_GetTrulyDarkTime))
                { priority = Priority.Last }
            );

            // --- VISUAL PATCH ---
            harmony.Patch(
                original: AccessTools.Method(typeof(Game1), "Update", new Type[] { typeof(GameTime) }),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(Postfix_Update))
                { priority = Priority.Last }
            );

            // --- EVENTS ---
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null) return;

            configMenu.Register(
                mod: ModManifest,
                reset: () => Config = new ModConfig(),
                save: () => Helper.WriteConfig(Config)
            );

            // GENERAL
            configMenu.AddSectionTitle(mod: ModManifest, text: () => Helper.Translation.Get("config.section.general"));
            configMenu.AddBoolOption(mod: ModManifest, getValue: () => Config.EnableRandomMode, setValue: v => Config.EnableRandomMode = v, name: () => Helper.Translation.Get("config.random_mode.name"), tooltip: () => Helper.Translation.Get("config.random_mode.desc"));
            configMenu.AddBoolOption(mod: ModManifest, getValue: () => Config.EnableVibrantSunset, setValue: v => Config.EnableVibrantSunset = v, name: () => Helper.Translation.Get("config.vibrant_sunset.name"), tooltip: () => Helper.Translation.Get("config.vibrant_sunset.desc"));
            configMenu.AddTextOption(mod: ModManifest, getValue: () => Config.Frequency.ToString(), setValue: v => Config.Frequency = (RandomFrequency)Enum.Parse(typeof(RandomFrequency), v), name: () => Helper.Translation.Get("config.frequency.name"), tooltip: () => Helper.Translation.Get("config.frequency.desc"), allowedValues: Enum.GetNames(typeof(RandomFrequency)), formatAllowedValue: v => Helper.Translation.Get($"config.frequency.{v.ToLower()}"));

            // RANGES
            configMenu.AddSectionTitle(mod: ModManifest, text: () => Helper.Translation.Get("config.section.ranges"));
            AddSeasonOptions(configMenu, () => Config.SpringMinTime, v => Config.SpringMinTime = v, () => Config.SpringMaxTime, v => Config.SpringMaxTime = v, "spring");
            AddSeasonOptions(configMenu, () => Config.SummerMinTime, v => Config.SummerMinTime = v, () => Config.SummerMaxTime, v => Config.SummerMaxTime = v, "summer");
            AddSeasonOptions(configMenu, () => Config.FallMinTime, v => Config.FallMinTime = v, () => Config.FallMaxTime, v => Config.FallMaxTime = v, "fall");
            AddSeasonOptions(configMenu, () => Config.WinterMinTime, v => Config.WinterMinTime = v, () => Config.WinterMaxTime, v => Config.WinterMaxTime = v, "winter");

            // MANUAL
            configMenu.AddSectionTitle(mod: ModManifest, text: () => Helper.Translation.Get("config.section.manual"));
            configMenu.AddNumberOption(mod: ModManifest, getValue: () => Config.ManualSpringTime, setValue: v => Config.ManualSpringTime = v, name: () => Helper.Translation.Get("config.manual_spring.name"), tooltip: () => Helper.Translation.Get("config.manual.desc"), min: 1200, max: 2600, interval: 10);
            configMenu.AddNumberOption(mod: ModManifest, getValue: () => Config.ManualSummerTime, setValue: v => Config.ManualSummerTime = v, name: () => Helper.Translation.Get("config.manual_summer.name"), tooltip: () => Helper.Translation.Get("config.manual.desc"), min: 1200, max: 2600, interval: 10);
            configMenu.AddNumberOption(mod: ModManifest, getValue: () => Config.ManualFallTime, setValue: v => Config.ManualFallTime = v, name: () => Helper.Translation.Get("config.manual_fall.name"), tooltip: () => Helper.Translation.Get("config.manual.desc"), min: 1200, max: 2600, interval: 10);
            configMenu.AddNumberOption(mod: ModManifest, getValue: () => Config.ManualWinterTime, setValue: v => Config.ManualWinterTime = v, name: () => Helper.Translation.Get("config.manual_winter.name"), tooltip: () => Helper.Translation.Get("config.manual.desc"), min: 1200, max: 2600, interval: 10);
        }

        private void AddSeasonOptions(IGenericModConfigMenuApi menu, Func<int> getMin, Action<int> setMin, Func<int> getMax, Action<int> setMax, string season)
        {
            menu.AddNumberOption(mod: ModManifest, getValue: getMin, setValue: setMin, name: () => Helper.Translation.Get($"config.{season}_min.name"), tooltip: () => Helper.Translation.Get("config.range.desc"), min: 1200, max: 2600, interval: 10);
            menu.AddNumberOption(mod: ModManifest, getValue: getMax, setValue: setMax, name: () => Helper.Translation.Get($"config.{season}_max.name"), tooltip: () => Helper.Translation.Get("config.range.desc"), min: 1200, max: 2600, interval: 10);
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e) { UpdateDailySunsetTime(); }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            if (!Context.IsWorldReady) return;

            if (Config.EnableRandomMode)
            {
                bool shouldGenerate = false;
                if (!Game1.player.modData.ContainsKey(ModDataKey)) shouldGenerate = true;
                else switch (Config.Frequency)
                    {
                        case RandomFrequency.Daily: shouldGenerate = true; break;
                        case RandomFrequency.Weekly: if (Game1.dayOfMonth % 7 == 1) shouldGenerate = true; break;
                        case RandomFrequency.Seasonal: if (Game1.dayOfMonth == 1) shouldGenerate = true; break;
                    }

                if (shouldGenerate)
                {
                    int newTime = GenerateSeasonalRandomTime();
                    Game1.player.modData[ModDataKey] = newTime.ToString();
                    SMonitor.Log($"[Dynamic Dusk] New sunset time generated: {newTime}", LogLevel.Info);
                }
            }
            UpdateDailySunsetTime();
        }

        private void UpdateDailySunsetTime()
        {
            // 1. Try to load from ModData
            bool foundValidData = false;

            if (Config.EnableRandomMode && Game1.player != null)
            {
                if (Game1.player.modData.TryGetValue(ModDataKey, out string timeStr) && int.TryParse(timeStr, out int storedTime))
                {
                    // SAFETY: Only allow valid times (e.g. 6 AM or later). If it's 0, it's trash.
                    if (storedTime >= 600)
                    {
                        CurrentDailySunset = storedTime;
                        foundValidData = true;
                    }
                    else
                    {
                        SMonitor.Log($"[Dynamic Dusk] Found invalid/corrupted time in save file ({storedTime}). Discarding it.", LogLevel.Warn);
                    }
                }
            }

            // 2. Fallback
            if (!foundValidData)
            {
                int manualTime = 1800;
                switch (Game1.currentSeason)
                {
                    case "spring": manualTime = Config.ManualSpringTime; break;
                    case "summer": manualTime = Config.ManualSummerTime; break;
                    case "fall": manualTime = Config.ManualFallTime; break;
                    case "winter": manualTime = Config.ManualWinterTime; break;
                }

                // Extra Safety: If config is still somehow 0, force 1800
                if (manualTime < 600) manualTime = 1800;

                CurrentDailySunset = manualTime;
                SMonitor.Log($"[Dynamic Dusk] Using fallback/manual sunset time: {CurrentDailySunset}", LogLevel.Info);
            }
        }

        private int GenerateSeasonalRandomTime()
        {
            Random rnd = new Random(Guid.NewGuid().GetHashCode());
            int minTime = 1700, maxTime = 1900;

            switch (Game1.currentSeason)
            {
                case "spring": minTime = Config.SpringMinTime; maxTime = Config.SpringMaxTime; break;
                case "summer": minTime = Config.SummerMinTime; maxTime = Config.SummerMaxTime; break;
                case "fall": minTime = Config.FallMinTime; maxTime = Config.FallMaxTime; break;
                case "winter": minTime = Config.WinterMinTime; maxTime = Config.WinterMaxTime; break;
            }

            // --- CRITICAL SAFETY FIX ---
            // Ensure we never generate a "0" or tiny number
            if (minTime < 600) minTime = 1700;
            if (maxTime < 600) maxTime = 1900;
            if (minTime > maxTime) { int t = minTime; minTime = maxTime; maxTime = t; }

            int minMins = (minTime / 100 * 60) + (minTime % 100);
            int maxMins = (maxTime / 100 * 60) + (maxTime % 100);
            int range = (maxMins - minMins) / 10;
            int resultMinutes = minMins + (rnd.Next(0, range + 1) * 10);

            return ((resultMinutes / 60) * 100) + (resultMinutes % 60);
        }

        // --- TIME PATCHES ---
        public static void Postfix_GetStartingToGetDarkTime(ref int __result)
        {
            try
            {
                // EMERGENCY OVERRIDE
                // If the logic somehow failed and CurrentDailySunset is 0,
                // do NOT pass 0 to the game engine. Force 1800.
                if (CurrentDailySunset < 600)
                {
                    CurrentDailySunset = 1800;
                    // SMonitor.Log("Emergency override triggered: Sunset was 0", LogLevel.Trace);
                }
                __result = CurrentDailySunset;
            }
            catch (Exception) { }
        }

        public static void Postfix_GetTrulyDarkTime(ref int __result)
        {
            try
            {
                if (CurrentDailySunset < 600) CurrentDailySunset = 1800;
                __result = CurrentDailySunset + 200;
            }
            catch (Exception) { }
        }

        // --- VISUAL PATCH ---
        public static void Postfix_Update()
        {
            if (CurrentDailySunset < 600) return;

            if (!Config.EnableVibrantSunset || Game1.currentLocation == null || !Game1.currentLocation.IsOutdoors || Game1.isRaining)
                return;

            try
            {
                int currentMins = (Game1.timeOfDay / 100 * 60) + (Game1.timeOfDay % 100);
                int sunsetMins = (CurrentDailySunset / 100 * 60) + (CurrentDailySunset % 100);
                int diff = currentMins - sunsetMins;

                // Window: -45 mins to +75 mins (Starts while sun is still up!)
                if (diff >= -45 && diff <= 75)
                {
                    float intensity = 0f;
                    float normalizedTime = diff + 45; // 0 to 120

                    if (normalizedTime <= 60) intensity = normalizedTime / 60f;
                    else intensity = 1f - ((normalizedTime - 60) / 60f);

                    float blendFactor = intensity * 0.45f;
                    Color goldenColor = new Color(255, 170, 50);

                    Game1.ambientLight = Color.Lerp(Game1.ambientLight, goldenColor, blendFactor);
                }
            }
            catch (Exception) { }
        }
    }
}