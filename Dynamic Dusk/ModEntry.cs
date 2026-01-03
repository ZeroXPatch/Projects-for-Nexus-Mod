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

            // ---------------------------------------------------------
            // 1. GENERAL SETTINGS
            // ---------------------------------------------------------
            configMenu.AddSectionTitle(mod: ModManifest, text: () => Helper.Translation.Get("config.section.general"));

            configMenu.AddBoolOption(
                mod: ModManifest,
                getValue: () => Config.EnableRandomMode,
                setValue: value => Config.EnableRandomMode = value,
                name: () => Helper.Translation.Get("config.random_mode.name"),
                tooltip: () => Helper.Translation.Get("config.random_mode.desc")
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                getValue: () => Config.Frequency.ToString(),
                setValue: value => Config.Frequency = (RandomFrequency)Enum.Parse(typeof(RandomFrequency), value),
                name: () => Helper.Translation.Get("config.frequency.name"),
                tooltip: () => Helper.Translation.Get("config.frequency.desc"),
                allowedValues: Enum.GetNames(typeof(RandomFrequency)),
                formatAllowedValue: value => Helper.Translation.Get($"config.frequency.{value.ToLower()}")
            );

            // ---------------------------------------------------------
            // 2. RANDOM RANGES (Grouped by Season)
            // ---------------------------------------------------------
            configMenu.AddSectionTitle(mod: ModManifest, text: () => Helper.Translation.Get("config.section.ranges"));

            // Spring Range
            configMenu.AddNumberOption(mod: ModManifest, getValue: () => Config.SpringMinTime, setValue: v => Config.SpringMinTime = v, name: () => Helper.Translation.Get("config.spring_min.name"), tooltip: () => Helper.Translation.Get("config.range.desc"), min: 1200, max: 2600, interval: 10);
            configMenu.AddNumberOption(mod: ModManifest, getValue: () => Config.SpringMaxTime, setValue: v => Config.SpringMaxTime = v, name: () => Helper.Translation.Get("config.spring_max.name"), tooltip: () => Helper.Translation.Get("config.range.desc"), min: 1200, max: 2600, interval: 10);

            // Summer Range
            configMenu.AddNumberOption(mod: ModManifest, getValue: () => Config.SummerMinTime, setValue: v => Config.SummerMinTime = v, name: () => Helper.Translation.Get("config.summer_min.name"), tooltip: () => Helper.Translation.Get("config.range.desc"), min: 1200, max: 2600, interval: 10);
            configMenu.AddNumberOption(mod: ModManifest, getValue: () => Config.SummerMaxTime, setValue: v => Config.SummerMaxTime = v, name: () => Helper.Translation.Get("config.summer_max.name"), tooltip: () => Helper.Translation.Get("config.range.desc"), min: 1200, max: 2600, interval: 10);

            // Fall Range
            configMenu.AddNumberOption(mod: ModManifest, getValue: () => Config.FallMinTime, setValue: v => Config.FallMinTime = v, name: () => Helper.Translation.Get("config.fall_min.name"), tooltip: () => Helper.Translation.Get("config.range.desc"), min: 1200, max: 2600, interval: 10);
            configMenu.AddNumberOption(mod: ModManifest, getValue: () => Config.FallMaxTime, setValue: v => Config.FallMaxTime = v, name: () => Helper.Translation.Get("config.fall_max.name"), tooltip: () => Helper.Translation.Get("config.range.desc"), min: 1200, max: 2600, interval: 10);

            // Winter Range
            configMenu.AddNumberOption(mod: ModManifest, getValue: () => Config.WinterMinTime, setValue: v => Config.WinterMinTime = v, name: () => Helper.Translation.Get("config.winter_min.name"), tooltip: () => Helper.Translation.Get("config.range.desc"), min: 1200, max: 2600, interval: 10);
            configMenu.AddNumberOption(mod: ModManifest, getValue: () => Config.WinterMaxTime, setValue: v => Config.WinterMaxTime = v, name: () => Helper.Translation.Get("config.winter_max.name"), tooltip: () => Helper.Translation.Get("config.range.desc"), min: 1200, max: 2600, interval: 10);

            // ---------------------------------------------------------
            // 3. MANUAL SETTINGS (Grouped at the bottom)
            // ---------------------------------------------------------
            configMenu.AddSectionTitle(mod: ModManifest, text: () => Helper.Translation.Get("config.section.manual"));

            configMenu.AddNumberOption(mod: ModManifest, getValue: () => Config.ManualSpringTime, setValue: v => Config.ManualSpringTime = v, name: () => Helper.Translation.Get("config.manual_spring.name"), tooltip: () => Helper.Translation.Get("config.manual.desc"), min: 1200, max: 2600, interval: 10);
            configMenu.AddNumberOption(mod: ModManifest, getValue: () => Config.ManualSummerTime, setValue: v => Config.ManualSummerTime = v, name: () => Helper.Translation.Get("config.manual_summer.name"), tooltip: () => Helper.Translation.Get("config.manual.desc"), min: 1200, max: 2600, interval: 10);
            configMenu.AddNumberOption(mod: ModManifest, getValue: () => Config.ManualFallTime, setValue: v => Config.ManualFallTime = v, name: () => Helper.Translation.Get("config.manual_fall.name"), tooltip: () => Helper.Translation.Get("config.manual.desc"), min: 1200, max: 2600, interval: 10);
            configMenu.AddNumberOption(mod: ModManifest, getValue: () => Config.ManualWinterTime, setValue: v => Config.ManualWinterTime = v, name: () => Helper.Translation.Get("config.manual_winter.name"), tooltip: () => Helper.Translation.Get("config.manual.desc"), min: 1200, max: 2600, interval: 10);
        }

        // --- EVENTS ---
        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            UpdateDailySunsetTime();
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            if (!Context.IsWorldReady) return;

            if (Config.EnableRandomMode)
            {
                bool shouldGenerateNewTime = false;

                if (!Game1.player.modData.ContainsKey(ModDataKey))
                {
                    shouldGenerateNewTime = true;
                }
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
                    SMonitor.Log($"[Dynamic Dusk] New sunset time generated for {Game1.currentSeason}: {newTime}", LogLevel.Info);
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
                    if (int.TryParse(timeStr, out int storedTime))
                    {
                        CurrentDailySunset = storedTime;
                        return;
                    }
                }
            }

            switch (Game1.currentSeason)
            {
                case "spring": CurrentDailySunset = Config.ManualSpringTime; break;
                case "summer": CurrentDailySunset = Config.ManualSummerTime; break;
                case "fall": CurrentDailySunset = Config.ManualFallTime; break;
                case "winter": CurrentDailySunset = Config.ManualWinterTime; break;
                default: CurrentDailySunset = 1800; break;
            }
        }

        private int GenerateSeasonalRandomTime()
        {
            Random rnd = new Random(Guid.NewGuid().GetHashCode());
            int minTime = 1700;
            int maxTime = 1900;

            switch (Game1.currentSeason)
            {
                case "spring": minTime = Config.SpringMinTime; maxTime = Config.SpringMaxTime; break;
                case "summer": minTime = Config.SummerMinTime; maxTime = Config.SummerMaxTime; break;
                case "fall": minTime = Config.FallMinTime; maxTime = Config.FallMaxTime; break;
                case "winter": minTime = Config.WinterMinTime; maxTime = Config.WinterMaxTime; break;
            }

            if (minTime > maxTime)
            {
                int temp = minTime; minTime = maxTime; maxTime = temp;
            }

            // Convert to minutes from midnight
            int minMinutes = (minTime / 100 * 60) + (minTime % 100);
            int maxMinutes = (maxTime / 100 * 60) + (maxTime % 100);

            // Generate random step (10 min intervals)
            int range = (maxMinutes - minMinutes) / 10;
            int randomStep = rnd.Next(0, range + 1);
            int resultMinutes = minMinutes + (randomStep * 10);

            // Convert back to Military Time
            int hours = resultMinutes / 60;
            int mins = resultMinutes % 60;

            return (hours * 100) + mins;
        }

        // --- PATCHES ---
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