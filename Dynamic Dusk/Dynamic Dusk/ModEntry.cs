using System;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace DynamicDusk
{
    public class ModEntry : Mod
    {
        public static ModConfig Config = null!;
        public static IMonitor SMonitor = null!;
        public static int CurrentDailySunset;

        private const string ModDataKey = "Zero.DynamicDusk/CurrentSunset";

        public override void Entry(IModHelper helper)
        {
            Config = helper.ReadConfig<ModConfig>();
            SMonitor = Monitor;

            var harmony = new Harmony(this.ModManifest.UniqueID);

            // Harmony Patches
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

            // Events
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

            // 1. GENERAL
            configMenu.AddSectionTitle(mod: ModManifest, text: () => Helper.Translation.Get("config.section.general"));
            configMenu.AddBoolOption(mod: ModManifest, getValue: () => Config.EnableRandomMode, setValue: v => Config.EnableRandomMode = v, name: () => Helper.Translation.Get("config.random_mode.name"), tooltip: () => Helper.Translation.Get("config.random_mode.desc"));
            configMenu.AddTextOption(mod: ModManifest, getValue: () => Config.Frequency.ToString(), setValue: v => Config.Frequency = (RandomFrequency)Enum.Parse(typeof(RandomFrequency), v), name: () => Helper.Translation.Get("config.frequency.name"), tooltip: () => Helper.Translation.Get("config.frequency.desc"), allowedValues: Enum.GetNames(typeof(RandomFrequency)), formatAllowedValue: v => Helper.Translation.Get($"config.frequency.{v.ToLower()}"));

            // 2. RANDOM RANGES
            configMenu.AddSectionTitle(mod: ModManifest, text: () => Helper.Translation.Get("config.section.ranges"));
            AddRandomRangeOption(configMenu, "Spring", () => Config.SpringMinTime, v => Config.SpringMinTime = v, () => Config.SpringMaxTime, v => Config.SpringMaxTime = v);
            AddRandomRangeOption(configMenu, "Summer", () => Config.SummerMinTime, v => Config.SummerMinTime = v, () => Config.SummerMaxTime, v => Config.SummerMaxTime = v);
            AddRandomRangeOption(configMenu, "Fall", () => Config.FallMinTime, v => Config.FallMinTime = v, () => Config.FallMaxTime, v => Config.FallMaxTime = v);
            AddRandomRangeOption(configMenu, "Winter", () => Config.WinterMinTime, v => Config.WinterMinTime = v, () => Config.WinterMaxTime, v => Config.WinterMaxTime = v);

            // 3. MANUAL SCHEDULE
            configMenu.AddSectionTitle(mod: ModManifest, text: () => Helper.Translation.Get("config.section.manual"));

            // Spring Manual
            configMenu.AddSectionTitle(mod: ModManifest, text: () => Helper.Translation.Get("config.subtitle.spring"));
            AddWeeklyOption(configMenu, () => Config.ManualSpringWeek1, v => Config.ManualSpringWeek1 = v, 1);
            AddWeeklyOption(configMenu, () => Config.ManualSpringWeek2, v => Config.ManualSpringWeek2 = v, 2);
            AddWeeklyOption(configMenu, () => Config.ManualSpringWeek3, v => Config.ManualSpringWeek3 = v, 3);
            AddWeeklyOption(configMenu, () => Config.ManualSpringWeek4, v => Config.ManualSpringWeek4 = v, 4);

            // Summer Manual
            configMenu.AddSectionTitle(mod: ModManifest, text: () => Helper.Translation.Get("config.subtitle.summer"));
            AddWeeklyOption(configMenu, () => Config.ManualSummerWeek1, v => Config.ManualSummerWeek1 = v, 1);
            AddWeeklyOption(configMenu, () => Config.ManualSummerWeek2, v => Config.ManualSummerWeek2 = v, 2);
            AddWeeklyOption(configMenu, () => Config.ManualSummerWeek3, v => Config.ManualSummerWeek3 = v, 3);
            AddWeeklyOption(configMenu, () => Config.ManualSummerWeek4, v => Config.ManualSummerWeek4 = v, 4);

            // Fall Manual
            configMenu.AddSectionTitle(mod: ModManifest, text: () => Helper.Translation.Get("config.subtitle.fall"));
            AddWeeklyOption(configMenu, () => Config.ManualFallWeek1, v => Config.ManualFallWeek1 = v, 1);
            AddWeeklyOption(configMenu, () => Config.ManualFallWeek2, v => Config.ManualFallWeek2 = v, 2);
            AddWeeklyOption(configMenu, () => Config.ManualFallWeek3, v => Config.ManualFallWeek3 = v, 3);
            AddWeeklyOption(configMenu, () => Config.ManualFallWeek4, v => Config.ManualFallWeek4 = v, 4);

            // Winter Manual
            configMenu.AddSectionTitle(mod: ModManifest, text: () => Helper.Translation.Get("config.subtitle.winter"));
            AddWeeklyOption(configMenu, () => Config.ManualWinterWeek1, v => Config.ManualWinterWeek1 = v, 1);
            AddWeeklyOption(configMenu, () => Config.ManualWinterWeek2, v => Config.ManualWinterWeek2 = v, 2);
            AddWeeklyOption(configMenu, () => Config.ManualWinterWeek3, v => Config.ManualWinterWeek3 = v, 3);
            AddWeeklyOption(configMenu, () => Config.ManualWinterWeek4, v => Config.ManualWinterWeek4 = v, 4);
        }

        // Helpers
        private void AddRandomRangeOption(IGenericModConfigMenuApi menu, string seasonKey, Func<int> getMin, Action<int> setMin, Func<int> getMax, Action<int> setMax)
        {
            menu.AddNumberOption(mod: ModManifest, getValue: getMin, setValue: setMin, name: () => Helper.Translation.Get($"config.{seasonKey.ToLower()}_min.name"), tooltip: () => Helper.Translation.Get("config.range.desc"), min: 1200, max: 2600, interval: 10);
            menu.AddNumberOption(mod: ModManifest, getValue: getMax, setValue: setMax, name: () => Helper.Translation.Get($"config.{seasonKey.ToLower()}_max.name"), tooltip: () => Helper.Translation.Get("config.range.desc"), min: 1200, max: 2600, interval: 10);
        }

        private void AddWeeklyOption(IGenericModConfigMenuApi menu, Func<int> get, Action<int> set, int weekNum)
        {
            menu.AddNumberOption(mod: ModManifest, getValue: get, setValue: set, name: () => Helper.Translation.Get($"config.week{weekNum}"), tooltip: () => Helper.Translation.Get("config.manual.desc"), min: 1200, max: 2600, interval: 10);
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e) { UpdateDailySunsetTime(); }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            if (!Context.IsWorldReady) return;

            if (Config.EnableRandomMode)
            {
                bool shouldGenerateNewTime = false;
                if (!Game1.player.modData.ContainsKey(ModDataKey)) shouldGenerateNewTime = true;
                else
                {
                    switch (Config.Frequency)
                    {
                        case RandomFrequency.Daily: shouldGenerateNewTime = true; break;
                        case RandomFrequency.Weekly: if (Game1.dayOfMonth % 7 == 1) shouldGenerateNewTime = true; break;
                        case RandomFrequency.Seasonal: if (Game1.dayOfMonth == 1) shouldGenerateNewTime = true; break;
                    }
                }

                if (shouldGenerateNewTime)
                {
                    int newTime = GenerateSeasonalRandomTime();
                    Game1.player.modData[ModDataKey] = newTime.ToString();
                    SMonitor.Log($"[Dynamic Dusk] New random sunset: {newTime}", LogLevel.Info);
                }
            }
            UpdateDailySunsetTime();
        }

        private void UpdateDailySunsetTime()
        {
            // 1. Random Mode
            if (Config.EnableRandomMode)
            {
                if (Game1.player != null && Game1.player.modData.TryGetValue(ModDataKey, out string timeStr))
                {
                    if (int.TryParse(timeStr, out int storedTime)) { CurrentDailySunset = storedTime; return; }
                }
            }

            // 2. Manual Weekly Mode
            int day = Game1.dayOfMonth;
            string season = Game1.currentSeason;
            int week = (day - 1) / 7; // 0=Week1, 1=Week2, 2=Week3, 3=Week4

            if (week > 3) week = 3; // Safety clamp

            switch (season)
            {
                case "spring":
                    if (week == 0) CurrentDailySunset = Config.ManualSpringWeek1;
                    else if (week == 1) CurrentDailySunset = Config.ManualSpringWeek2;
                    else if (week == 2) CurrentDailySunset = Config.ManualSpringWeek3;
                    else CurrentDailySunset = Config.ManualSpringWeek4;
                    break;
                case "summer":
                    if (week == 0) CurrentDailySunset = Config.ManualSummerWeek1;
                    else if (week == 1) CurrentDailySunset = Config.ManualSummerWeek2;
                    else if (week == 2) CurrentDailySunset = Config.ManualSummerWeek3;
                    else CurrentDailySunset = Config.ManualSummerWeek4;
                    break;
                case "fall":
                    if (week == 0) CurrentDailySunset = Config.ManualFallWeek1;
                    else if (week == 1) CurrentDailySunset = Config.ManualFallWeek2;
                    else if (week == 2) CurrentDailySunset = Config.ManualFallWeek3;
                    else CurrentDailySunset = Config.ManualFallWeek4;
                    break;
                case "winter":
                    if (week == 0) CurrentDailySunset = Config.ManualWinterWeek1;
                    else if (week == 1) CurrentDailySunset = Config.ManualWinterWeek2;
                    else if (week == 2) CurrentDailySunset = Config.ManualWinterWeek3;
                    else CurrentDailySunset = Config.ManualWinterWeek4;
                    break;
                default:
                    CurrentDailySunset = 1800;
                    break;
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

            if (minTime > maxTime) { int t = minTime; minTime = maxTime; maxTime = t; }

            int minMins = (minTime / 100 * 60) + (minTime % 100);
            int maxMins = (maxTime / 100 * 60) + (maxTime % 100);
            int range = (maxMins - minMins) / 10;
            int res = minMins + (rnd.Next(0, range + 1) * 10);
            return (res / 60 * 100) + (res % 60);
        }

        // --- HARMONY ---
        public static void Postfix_GetStartingToGetDarkTime(ref int __result)
        {
            try { __result = CurrentDailySunset; } catch (Exception) { }
        }
        public static void Postfix_GetTrulyDarkTime(ref int __result)
        {
            try { __result = CurrentDailySunset + 200; } catch (Exception) { }
        }
    }
}