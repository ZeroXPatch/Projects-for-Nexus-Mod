using System;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace DynamicDusk
{
    public class ModEntry : Mod
    {
        // = null! handles the "Non-nullable field" warnings
        public static ModConfig Config = null!;
        public static IMonitor SMonitor = null!;

        // This variable locks the time for the current day so the sky doesn't flash
        public static int CurrentDailySunset;

        private const string ModDataKey = "Zero.DynamicDusk/CurrentSunset";

        public override void Entry(IModHelper helper)
        {
            Config = helper.ReadConfig<ModConfig>();
            SMonitor = Monitor;

            // Initialize Harmony
            var harmony = new Harmony(this.ModManifest.UniqueID);

            // PATCH 1: Start of Sunset
            // We use Priority.Last to override other mods like Dynamic Night Time
            harmony.Patch(
                original: AccessTools.Method(typeof(Game1), nameof(Game1.getStartingToGetDarkTime)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(Postfix_GetStartingToGetDarkTime))
                {
                    priority = Priority.Last
                }
            );

            // PATCH 2: Fully Dark Time
            harmony.Patch(
                original: AccessTools.Method(typeof(Game1), nameof(Game1.getTrulyDarkTime)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(Postfix_GetTrulyDarkTime))
                {
                    priority = Priority.Last
                }
            );

            // Hook Events
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            // Get GMCM API
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null) return;

            // Register Mod
            configMenu.Register(
                mod: ModManifest,
                reset: () => Config = new ModConfig(),
                save: () => Helper.WriteConfig(Config)
            );

            // --- SECTION 1: RANDOM SETTINGS ---
            configMenu.AddSectionTitle(mod: ModManifest, text: () => Helper.Translation.Get("config.section.random"));

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

            configMenu.AddNumberOption(
                mod: ModManifest,
                getValue: () => Config.RandomMinTime,
                setValue: value => Config.RandomMinTime = value,
                name: () => Helper.Translation.Get("config.min_time.name"),
                tooltip: () => Helper.Translation.Get("config.min_time.desc"),
                min: 1200, max: 2400, interval: 10
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                getValue: () => Config.RandomMaxTime,
                setValue: value => Config.RandomMaxTime = value,
                name: () => Helper.Translation.Get("config.max_time.name"),
                tooltip: () => Helper.Translation.Get("config.max_time.desc"),
                min: 1200, max: 2400, interval: 10
            );

            // --- SECTION 2: MANUAL SETTINGS ---
            configMenu.AddSectionTitle(mod: ModManifest, text: () => Helper.Translation.Get("config.section.manual"));

            configMenu.AddNumberOption(mod: ModManifest, getValue: () => Config.ManualSpringTime, setValue: value => Config.ManualSpringTime = value, name: () => Helper.Translation.Get("config.manual_spring.name"), tooltip: () => Helper.Translation.Get("config.manual_spring.desc"), min: 1200, max: 2400, interval: 10);
            configMenu.AddNumberOption(mod: ModManifest, getValue: () => Config.ManualSummerTime, setValue: value => Config.ManualSummerTime = value, name: () => Helper.Translation.Get("config.manual_summer.name"), tooltip: () => Helper.Translation.Get("config.manual_summer.desc"), min: 1200, max: 2400, interval: 10);
            configMenu.AddNumberOption(mod: ModManifest, getValue: () => Config.ManualFallTime, setValue: value => Config.ManualFallTime = value, name: () => Helper.Translation.Get("config.manual_fall.name"), tooltip: () => Helper.Translation.Get("config.manual_fall.desc"), min: 1200, max: 2400, interval: 10);
            configMenu.AddNumberOption(mod: ModManifest, getValue: () => Config.ManualWinterTime, setValue: value => Config.ManualWinterTime = value, name: () => Helper.Translation.Get("config.manual_winter.name"), tooltip: () => Helper.Translation.Get("config.manual_winter.desc"), min: 1200, max: 2400, interval: 10);
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            UpdateDailySunsetTime();
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            if (!Context.IsWorldReady) return;

            // 1. Handle Random Generation
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
                        case RandomFrequency.Daily:
                            shouldGenerateNewTime = true;
                            break;
                        case RandomFrequency.Weekly:
                            // Reset on Day 1, 8, 15, 22
                            if (Game1.dayOfMonth % 7 == 1) shouldGenerateNewTime = true;
                            break;
                        case RandomFrequency.Seasonal:
                            // Reset on Day 1
                            if (Game1.dayOfMonth == 1) shouldGenerateNewTime = true;
                            break;
                    }
                }

                if (shouldGenerateNewTime)
                {
                    int newTime = GenerateRandomTime();
                    Game1.player.modData[ModDataKey] = newTime.ToString();
                    SMonitor.Log($"[Dynamic Dusk] New sunset time generated: {newTime}", LogLevel.Info);
                }
            }

            // 2. Lock the value for today
            UpdateDailySunsetTime();
        }

        private void UpdateDailySunsetTime()
        {
            // If random mode is ON, try to read the saved random value
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

            // If random mode is OFF (or data missing), use Manual Config
            switch (Game1.currentSeason)
            {
                case "spring": CurrentDailySunset = Config.ManualSpringTime; break;
                case "summer": CurrentDailySunset = Config.ManualSummerTime; break;
                case "fall": CurrentDailySunset = Config.ManualFallTime; break;
                case "winter": CurrentDailySunset = Config.ManualWinterTime; break;
                default: CurrentDailySunset = 1800; break;
            }
        }

        private int GenerateRandomTime()
        {
            Random rnd = new Random(Guid.NewGuid().GetHashCode());

            int minHour = Config.RandomMinTime / 100;
            int maxHour = Config.RandomMaxTime / 100;

            int hour = rnd.Next(minHour, maxHour);
            int tenMinutes = rnd.Next(0, 6);

            int time = (hour * 100) + (tenMinutes * 10);

            // Safety Clamp
            if (time < Config.RandomMinTime) time = Config.RandomMinTime;
            if (time > Config.RandomMaxTime) time = Config.RandomMaxTime;

            return time;
        }

        // --- HARMONY PATCHES ---

        public static void Postfix_GetStartingToGetDarkTime(ref int __result)
        {
            try
            {
                __result = CurrentDailySunset;
            }
            catch (Exception) { /* ignore */ }
        }

        public static void Postfix_GetTrulyDarkTime(ref int __result)
        {
            try
            {
                // Always set full dark to StartTime + 200 (2 hours)
                // This ensures a smooth transition regardless of the start time
                __result = CurrentDailySunset + 200;
            }
            catch (Exception) { /* ignore */ }
        }
    }
}