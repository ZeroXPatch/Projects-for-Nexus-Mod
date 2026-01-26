using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace DynamicBrightness
{
    public class ModEntry : Mod
    {
        public static ModConfig Config = null!;
        public static IMonitor SMonitor = null!;

        // This holds the current day's adjustment (e.g., -10)
        public static int CurrentPercentAdjustment = 0;

        private const string ModDataKey = "Zero.DynamicBrightness/CurrentPercent";

        public override void Entry(IModHelper helper)
        {
            Config = helper.ReadConfig<ModConfig>();
            SMonitor = Monitor;

            // --- EVENTS ---
            // "RenderedWorld" runs after the ground/trees are drawn, but BEFORE the Interface (UI).
            // This allows us to draw a "Darkness Filter" over the game world without darkening your inventory.
            helper.Events.Display.RenderedWorld += OnRenderedWorld;

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
                save: () => {
                    Helper.WriteConfig(Config);
                    // Force an update immediately after saving so you can see the changes instantly
                    CalculateDailyPercentage();
                }
            );

            // --- GENERAL ---
            configMenu.AddSectionTitle(mod: ModManifest, text: () => Helper.Translation.Get("config.section.general"));
            configMenu.AddBoolOption(mod: ModManifest, getValue: () => Config.EnableMod, setValue: v => Config.EnableMod = v, name: () => Helper.Translation.Get("config.mod_enabled.name"), tooltip: () => Helper.Translation.Get("config.mod_enabled.desc"));

            // --- RANDOM MODE ---
            configMenu.AddSectionTitle(mod: ModManifest, text: () => Helper.Translation.Get("config.section.random"));
            configMenu.AddBoolOption(mod: ModManifest, getValue: () => Config.EnableRandomMode, setValue: v => Config.EnableRandomMode = v, name: () => Helper.Translation.Get("config.random_mode.name"), tooltip: () => Helper.Translation.Get("config.random_mode.desc"));

            configMenu.AddTextOption(mod: ModManifest, getValue: () => Config.Frequency.ToString(), setValue: v => Config.Frequency = (RandomFrequency)Enum.Parse(typeof(RandomFrequency), v), name: () => Helper.Translation.Get("config.frequency.name"), tooltip: () => Helper.Translation.Get("config.frequency.desc"), allowedValues: Enum.GetNames(typeof(RandomFrequency)), formatAllowedValue: v => Helper.Translation.Get($"config.frequency.{v.ToLower()}"));

            // Range: -50 to 0
            configMenu.AddNumberOption(mod: ModManifest, getValue: () => Config.RandomMinPercentage, setValue: v => Config.RandomMinPercentage = v, name: () => Helper.Translation.Get("config.min_percent.name"), tooltip: () => Helper.Translation.Get("config.min_percent.desc"), min: -50, max: 0);
            configMenu.AddNumberOption(mod: ModManifest, getValue: () => Config.RandomMaxPercentage, setValue: v => Config.RandomMaxPercentage = v, name: () => Helper.Translation.Get("config.max_percent.name"), tooltip: () => Helper.Translation.Get("config.max_percent.desc"), min: -50, max: 0);

            // --- MANUAL WEEKS ---
            configMenu.AddSectionTitle(mod: ModManifest, text: () => Helper.Translation.Get("config.section.manual"));

            // Spring
            configMenu.AddSectionTitle(mod: ModManifest, text: () => Helper.Translation.Get("config.subtitle.spring"));
            AddWeeklySliders(configMenu, () => Config.SpringWeek1, v => Config.SpringWeek1 = v, 1);
            AddWeeklySliders(configMenu, () => Config.SpringWeek2, v => Config.SpringWeek2 = v, 2);
            AddWeeklySliders(configMenu, () => Config.SpringWeek3, v => Config.SpringWeek3 = v, 3);
            AddWeeklySliders(configMenu, () => Config.SpringWeek4, v => Config.SpringWeek4 = v, 4);

            // Summer
            configMenu.AddSectionTitle(mod: ModManifest, text: () => Helper.Translation.Get("config.subtitle.summer"));
            AddWeeklySliders(configMenu, () => Config.SummerWeek1, v => Config.SummerWeek1 = v, 1);
            AddWeeklySliders(configMenu, () => Config.SummerWeek2, v => Config.SummerWeek2 = v, 2);
            AddWeeklySliders(configMenu, () => Config.SummerWeek3, v => Config.SummerWeek3 = v, 3);
            AddWeeklySliders(configMenu, () => Config.SummerWeek4, v => Config.SummerWeek4 = v, 4);

            // Fall
            configMenu.AddSectionTitle(mod: ModManifest, text: () => Helper.Translation.Get("config.subtitle.fall"));
            AddWeeklySliders(configMenu, () => Config.FallWeek1, v => Config.FallWeek1 = v, 1);
            AddWeeklySliders(configMenu, () => Config.FallWeek2, v => Config.FallWeek2 = v, 2);
            AddWeeklySliders(configMenu, () => Config.FallWeek3, v => Config.FallWeek3 = v, 3);
            AddWeeklySliders(configMenu, () => Config.FallWeek4, v => Config.FallWeek4 = v, 4);

            // Winter
            configMenu.AddSectionTitle(mod: ModManifest, text: () => Helper.Translation.Get("config.subtitle.winter"));
            AddWeeklySliders(configMenu, () => Config.WinterWeek1, v => Config.WinterWeek1 = v, 1);
            AddWeeklySliders(configMenu, () => Config.WinterWeek2, v => Config.WinterWeek2 = v, 2);
            AddWeeklySliders(configMenu, () => Config.WinterWeek3, v => Config.WinterWeek3 = v, 3);
            AddWeeklySliders(configMenu, () => Config.WinterWeek4, v => Config.WinterWeek4 = v, 4);
        }

        private void AddWeeklySliders(IGenericModConfigMenuApi menu, Func<int> get, Action<int> set, int weekNum)
        {
            menu.AddNumberOption(
                mod: ModManifest,
                getValue: get,
                setValue: set,
                name: () => Helper.Translation.Get($"config.week{weekNum}"),
                tooltip: () => Helper.Translation.Get("config.manual.desc"),
                min: -50, max: 0, interval: 1,
                formatValue: (val) => $"{val}%"
            );
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e) { CalculateDailyPercentage(); }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            if (!Context.IsWorldReady || !Config.EnableMod) return;

            if (Config.EnableRandomMode)
            {
                bool shouldGenerate = false;
                if (!Game1.player.modData.ContainsKey(ModDataKey)) shouldGenerate = true;
                else
                {
                    switch (Config.Frequency)
                    {
                        case RandomFrequency.Daily: shouldGenerate = true; break;
                        case RandomFrequency.Weekly: if (Game1.dayOfMonth % 7 == 1) shouldGenerate = true; break;
                        case RandomFrequency.Seasonal: if (Game1.dayOfMonth == 1) shouldGenerate = true; break;
                    }
                }

                if (shouldGenerate)
                {
                    Random rnd = new Random(Guid.NewGuid().GetHashCode());
                    int min = Math.Min(Config.RandomMinPercentage, Config.RandomMaxPercentage);
                    int max = Math.Max(Config.RandomMinPercentage, Config.RandomMaxPercentage);

                    if (min > 0) min = 0;
                    if (max > 0) max = 0;

                    int newPercent = rnd.Next(min, max + 1);
                    Game1.player.modData[ModDataKey] = newPercent.ToString();
                    SMonitor.Log($"[Dynamic Brightness] Generated new brightness: {newPercent}%", LogLevel.Info);
                }
            }

            CalculateDailyPercentage();
        }

        private void CalculateDailyPercentage()
        {
            if (!Config.EnableMod)
            {
                CurrentPercentAdjustment = 0;
                return;
            }

            int valueToUse = 0;

            if (Config.EnableRandomMode)
            {
                // In Random Mode, we usually stick to the saved random value.
                // However, if the user edits the range in GMCM and the current value is now 'invalid'
                // (e.g. current is -50 but user changed max to -10), we should probably clamp it visually?
                // For simplicity, we just trust the rolled value until the next day.
                if (Game1.player != null && Game1.player.modData.TryGetValue(ModDataKey, out string strVal))
                {
                    if (int.TryParse(strVal, out int val))
                    {
                        valueToUse = val;
                    }
                }
            }
            else
            {
                // Manual Mode Logic - Updates instantly when saving config
                int day = Game1.dayOfMonth;
                string season = Game1.currentSeason;
                int week = (day - 1) / 7;
                if (week > 3) week = 3;

                switch (season)
                {
                    case "spring":
                        valueToUse = week switch { 0 => Config.SpringWeek1, 1 => Config.SpringWeek2, 2 => Config.SpringWeek3, _ => Config.SpringWeek4 }; break;
                    case "summer":
                        valueToUse = week switch { 0 => Config.SummerWeek1, 1 => Config.SummerWeek2, 2 => Config.SummerWeek3, _ => Config.SummerWeek4 }; break;
                    case "fall":
                        valueToUse = week switch { 0 => Config.FallWeek1, 1 => Config.FallWeek2, 2 => Config.FallWeek3, _ => Config.FallWeek4 }; break;
                    case "winter":
                        valueToUse = week switch { 0 => Config.WinterWeek1, 1 => Config.WinterWeek2, 2 => Config.WinterWeek3, _ => Config.WinterWeek4 }; break;
                }
            }

            // Safety Clamp
            if (valueToUse > 0) valueToUse = 0;
            if (valueToUse < -80) valueToUse = -80; // Allow up to -80 for extreme darkness

            CurrentPercentAdjustment = valueToUse;
        }

        // --- NEW RENDERING LOGIC ---
        private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
        {
            if (!Config.EnableMod || CurrentPercentAdjustment == 0) return;

            // We use a simple 1x1 white pixel provided by the game, scaled up to cover the whole screen.
            // We color it Black, with transparency based on the percentage.

            // Calculate Opacity (Alpha)
            // -10% -> 0.1f
            // -50% -> 0.5f
            float opacity = Math.Abs(CurrentPercentAdjustment) / 100f;

            // Clamp max darkness to 90% so you can still see a little bit even at extreme settings
            if (opacity > 0.9f) opacity = 0.9f;

            // Get the screen size
            var screenRect = new Rectangle(0, 0, Game1.viewport.Width, Game1.viewport.Height);

            // Draw the black overlay
            e.SpriteBatch.Draw(
                Game1.staminaRect,
                screenRect,
                new Color(0, 0, 0, opacity)
            );
        }
    }
}