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

            // Spring
            configMenu.AddSectionTitle(mod: ModManifest, text: () => Helper.Translation.Get("config.subtitle.spring"));
            AddWeeklyOption(configMenu, () => Config.ManualSpringWeek1, v => Config.ManualSpringWeek1 = v, 1);
            AddWeeklyOption(configMenu, () => Config.ManualSpringWeek2, v => Config.ManualSpringWeek2 = v, 2);
            AddWeeklyOption(configMenu, () => Config.ManualSpringWeek3, v => Config.ManualSpringWeek3 = v, 3);
            AddWeeklyOption(configMenu, () => Config.ManualSpringWeek4, v => Config.ManualSpringWeek4 = v, 4);

            // Summer
            configMenu.AddSectionTitle(mod: ModManifest, text: () => Helper.Translation.Get("config.subtitle.summer"));
            AddWeeklyOption(configMenu, () => Config.ManualSummerWeek1, v => Config.ManualSummerWeek1 = v, 1);
            AddWeeklyOption(configMenu, () => Config.ManualSummerWeek2, v => Config.ManualSummerWeek2 = v, 2);
            AddWeeklyOption(configMenu, () => Config.ManualSummerWeek3, v => Config.ManualSummerWeek3 = v, 3);
            AddWeeklyOption(configMenu, () => Config.ManualSummerWeek4, v => Config.ManualSummerWeek4 = v, 4);

            // Fall
            configMenu.AddSectionTitle(mod: ModManifest, text: () => Helper.Translation.Get("config.subtitle.fall"));
            AddWeeklyOption(configMenu, () => Config.ManualFallWeek1, v => Config.ManualFallWeek1 = v, 1);
            AddWeeklyOption(configMenu, () => Config.ManualFallWeek2, v => Config.ManualFallWeek2 = v, 2);
            AddWeeklyOption(configMenu, () => Config.ManualFallWeek3, v => Config.ManualFallWeek3 = v, 3);
            AddWeeklyOption(configMenu, () => Config.ManualFallWeek4, v => Config.ManualFallWeek4 = v, 4);

            // Winter
            configMenu.AddSectionTitle(mod: ModManifest, text: () => Helper.Translation.Get("config.subtitle.winter"));
            AddWeeklyOption(configMenu, () => Config.ManualWinterWeek1, v => Config.ManualWinterWeek1 = v, 1);
            AddWeeklyOption(configMenu, () => Config.ManualWinterWeek2, v => Config.ManualWinterWeek2 = v, 2);
            AddWeeklyOption(configMenu, () => Config.ManualWinterWeek3, v => Config.ManualWinterWeek3 = v, 3);
            AddWeeklyOption(configMenu, () => Config.ManualWinterWeek4, v => Config.ManualWinterWeek4 = v, 4);
        }

        // --- SLIDER CONVERSION LOGIC ---
        // Stardew Time (1200-2600) is not linear.
        // We convert it to Minutes (720-1560) for the slider, then convert back.

        private int TimeToMinutes(int stardewTime)
        {
            int hours = stardewTime / 100;
            int minutes = stardewTime % 100;
            return (hours * 60) + minutes;
        }

        private int MinutesToTime(int totalMinutes)
        {
            int hours = totalMinutes / 60;
            int minutes = totalMinutes % 60;
            return (hours * 100) + minutes;
        }

        private string FormatMinutesAsTime(int totalMinutes)
        {
            int time = MinutesToTime(totalMinutes);
            int hour = time / 100;
            int min = time % 100;
            string ampm = (hour >= 12 && hour < 24) ? "PM" : "AM";

            int displayHour = hour % 12;
            if (displayHour == 0) displayHour = 12;

            return $"{displayHour}:{min:00} {ampm}";
        }

        private void AddRandomRangeOption(IGenericModConfigMenuApi menu, string seasonKey, Func<int> getMin, Action<int> setMin, Func<int> getMax, Action<int> setMax)
        {
            // Range: 12:00 PM (1200) to 2:00 AM (2600)
            int minVal = 720;  // 12 * 60
            int maxVal = 1560; // 26 * 60

            menu.AddNumberOption(
                mod: ModManifest,
                getValue: () => TimeToMinutes(getMin()),
                setValue: v => setMin(MinutesToTime(v)),
                name: () => Helper.Translation.Get($"config.{seasonKey.ToLower()}_min.name"),
                tooltip: () => Helper.Translation.Get("config.range.desc"),
                min: minVal, max: maxVal, interval: 10,
                formatValue: FormatMinutesAsTime
            );

            menu.AddNumberOption(
                mod: ModManifest,
                getValue: () => TimeToMinutes(getMax()),
                setValue: v => setMax(MinutesToTime(v)),
                name: () => Helper.Translation.Get($"config.{seasonKey.ToLower()}_max.name"),
                tooltip: () => Helper.Translation.Get("config.range.desc"),
                min: minVal, max: maxVal, interval: 10,
                formatValue: FormatMinutesAsTime
            );
        }

        private void AddWeeklyOption(IGenericModConfigMenuApi menu, Func<int> get, Action<int> set, int weekNum)
        {
            int minVal = 720;
            int maxVal = 1560;

            menu.AddNumberOption(
                mod: ModManifest,
                getValue: () => TimeToMinutes(get()),
                setValue: v => set(MinutesToTime(v)),
                name: () => Helper.Translation.Get($"config.week{weekNum}"),
                tooltip: () => Helper.Translation.Get("config.manual.desc"),
                min: minVal, max: maxVal, interval: 10,
                formatValue: FormatMinutesAsTime
            );
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
            if (Config.EnableRandomMode)
            {
                if (Game1.player != null && Game1.player.modData.TryGetValue(ModDataKey, out string timeStr))
                {
                    if (int.TryParse(timeStr, out int storedTime)) { CurrentDailySunset = storedTime; return; }
                }
            }

            int day = Game1.dayOfMonth;
            string season = Game1.currentSeason;
            int week = (day - 1) / 7;
            if (week > 3) week = 3;

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
                default: CurrentDailySunset = 1800; break;
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

            // Use the helpers here too for cleaner code
            int minMins = TimeToMinutes(minTime);
            int maxMins = TimeToMinutes(maxTime);

            int range = (maxMins - minMins) / 10;
            int res = minMins + (rnd.Next(0, range + 1) * 10);

            return MinutesToTime(res);
        }

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