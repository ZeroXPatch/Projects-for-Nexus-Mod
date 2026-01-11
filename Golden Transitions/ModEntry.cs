using DynamicDusk;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System;

namespace GoldenTransitions
{
    public class ModEntry : Mod
    {
        public static ModConfig Config = null!;
        private Texture2D OverlayTexture = null!;

        // Holds the currently active visual settings (from Season or Random Roll)
        private VisualSettings ActiveSettings;
        private const string ModDataKey = "Zero.GoldenTransitions/CurrentPreset";

        private struct VisualSettings
        {
            public int R, G, B;
            public float Intensity;
            public int BuildUp, FadeOut;
        }

        public override void Entry(IModHelper helper)
        {
            Config = helper.ReadConfig<ModConfig>();

            OverlayTexture = new Texture2D(Game1.graphics.GraphicsDevice, 1, 1);
            OverlayTexture.SetData(new[] { Color.White });

            helper.Events.Display.RenderedWorld += OnRenderedWorld;
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            UpdateActiveSettings();
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            if (Config.EnableRandomMode)
            {
                bool shouldRoll = false;

                // Check if we need to roll a new preset
                if (!Game1.player.modData.ContainsKey(ModDataKey))
                {
                    shouldRoll = true;
                }
                else
                {
                    switch (Config.Frequency)
                    {
                        case RandomFrequency.Daily: shouldRoll = true; break;
                        case RandomFrequency.Weekly: if (Game1.dayOfMonth % 7 == 1) shouldRoll = true; break;
                        case RandomFrequency.Seasonal: if (Game1.dayOfMonth == 1) shouldRoll = true; break;
                    }
                }

                if (shouldRoll)
                {
                    RollRandomPreset();
                }
            }

            UpdateActiveSettings();
        }

        private void RollRandomPreset()
        {
            Random rnd = new Random(Guid.NewGuid().GetHashCode());
            int choice = rnd.Next(0, 4); // 0 to 3

            string seasonKey = choice switch
            {
                0 => "spring",
                1 => "summer",
                2 => "fall",
                3 => "winter",
                _ => "spring"
            };

            Game1.player.modData[ModDataKey] = seasonKey;
            // Monitor.Log($"[Golden Transitions] Randomizer Rolled: {seasonKey}", LogLevel.Info);
        }

        private void UpdateActiveSettings()
        {
            string targetSeason = Game1.currentSeason;

            // If Random Mode is ON, overwrite the natural season with the stored random season
            if (Config.EnableRandomMode)
            {
                if (Game1.player != null && Game1.player.modData.TryGetValue(ModDataKey, out string storedSeason))
                {
                    targetSeason = storedSeason;
                }
            }

            // Map Config to Active Settings
            switch (targetSeason)
            {
                case "summer":
                    ActiveSettings = new VisualSettings { R = Config.SummerR, G = Config.SummerG, B = Config.SummerB, Intensity = Config.SummerIntensity, BuildUp = Config.SummerBuildUp, FadeOut = Config.SummerFadeOut };
                    break;
                case "fall":
                    ActiveSettings = new VisualSettings { R = Config.FallR, G = Config.FallG, B = Config.FallB, Intensity = Config.FallIntensity, BuildUp = Config.FallBuildUp, FadeOut = Config.FallFadeOut };
                    break;
                case "winter":
                    ActiveSettings = new VisualSettings { R = Config.WinterR, G = Config.WinterG, B = Config.WinterB, Intensity = Config.WinterIntensity, BuildUp = Config.WinterBuildUp, FadeOut = Config.WinterFadeOut };
                    break;
                default: // spring
                    ActiveSettings = new VisualSettings { R = Config.SpringR, G = Config.SpringG, B = Config.SpringB, Intensity = Config.SpringIntensity, BuildUp = Config.SpringBuildUp, FadeOut = Config.SpringFadeOut };
                    break;
            }
        }

        private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.currentLocation == null) return;
            if (!Game1.currentLocation.IsOutdoors) return;

            int targetSunsetTime = Game1.getStartingToGetDarkTime(Game1.currentLocation);
            float opacity = GetSunlightOpacity(targetSunsetTime);

            if (opacity > 0f)
            {
                Color baseColor = new Color(ActiveSettings.R, ActiveSettings.G, ActiveSettings.B);
                Color renderColor = baseColor * opacity;

                e.SpriteBatch.Draw(
                    OverlayTexture,
                    new Rectangle(0, 0, Game1.viewport.Width, Game1.viewport.Height),
                    renderColor
                );
            }
        }

        private float GetSunlightOpacity(int targetSunsetTime)
        {
            int currentTime = Game1.timeOfDay;
            int currentMinutes = (currentTime / 100 * 60) + (currentTime % 100);
            int sunsetMinutes = (targetSunsetTime / 100 * 60) + (targetSunsetTime % 100);

            int startMinute = sunsetMinutes - ActiveSettings.BuildUp;
            int endMinute = sunsetMinutes + ActiveSettings.FadeOut;

            if (currentMinutes < startMinute || currentMinutes > endMinute) return 0f;

            if (currentMinutes <= sunsetMinutes)
            {
                if (ActiveSettings.BuildUp <= 0) return ActiveSettings.Intensity;
                float progress = (float)(currentMinutes - startMinute) / ActiveSettings.BuildUp;
                return MathHelper.Lerp(0f, ActiveSettings.Intensity, progress);
            }
            else
            {
                if (ActiveSettings.FadeOut <= 0) return 0f;
                float progress = (float)(currentMinutes - sunsetMinutes) / ActiveSettings.FadeOut;
                return MathHelper.Lerp(ActiveSettings.Intensity, 0f, progress);
            }
        }

        // --- GMCM MENU REGISTRATION ---
        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null) return;

            configMenu.Register(
                mod: ModManifest,
                reset: () => Config = new ModConfig(),
                save: () => {
                    Helper.WriteConfig(Config);
                    UpdateActiveSettings(); // Apply changes instantly
                }
            );

            // GENERAL SECTION
            configMenu.AddSectionTitle(mod: ModManifest, text: () => Helper.Translation.Get("config.section.random"));

            configMenu.AddBoolOption(
                mod: ModManifest,
                getValue: () => Config.EnableRandomMode,
                setValue: v => Config.EnableRandomMode = v,
                name: () => Helper.Translation.Get("config.random_mode.name"),
                tooltip: () => Helper.Translation.Get("config.random_mode.desc")
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                getValue: () => Config.Frequency.ToString(),
                setValue: v => Config.Frequency = (RandomFrequency)Enum.Parse(typeof(RandomFrequency), v),
                name: () => Helper.Translation.Get("config.frequency.name"),
                allowedValues: Enum.GetNames(typeof(RandomFrequency)),
                formatAllowedValue: v => Helper.Translation.Get($"config.frequency.{v.ToLower()}")
            );

            // SEASONS
            AddSeasonConfig(configMenu, "spring",
                () => Config.SpringR, v => Config.SpringR = v,
                () => Config.SpringG, v => Config.SpringG = v,
                () => Config.SpringB, v => Config.SpringB = v,
                () => Config.SpringIntensity, v => Config.SpringIntensity = v,
                () => Config.SpringBuildUp, v => Config.SpringBuildUp = v,
                () => Config.SpringFadeOut, v => Config.SpringFadeOut = v);

            AddSeasonConfig(configMenu, "summer",
                () => Config.SummerR, v => Config.SummerR = v,
                () => Config.SummerG, v => Config.SummerG = v,
                () => Config.SummerB, v => Config.SummerB = v,
                () => Config.SummerIntensity, v => Config.SummerIntensity = v,
                () => Config.SummerBuildUp, v => Config.SummerBuildUp = v,
                () => Config.SummerFadeOut, v => Config.SummerFadeOut = v);

            AddSeasonConfig(configMenu, "fall",
                () => Config.FallR, v => Config.FallR = v,
                () => Config.FallG, v => Config.FallG = v,
                () => Config.FallB, v => Config.FallB = v,
                () => Config.FallIntensity, v => Config.FallIntensity = v,
                () => Config.FallBuildUp, v => Config.FallBuildUp = v,
                () => Config.FallFadeOut, v => Config.FallFadeOut = v);

            AddSeasonConfig(configMenu, "winter",
                () => Config.WinterR, v => Config.WinterR = v,
                () => Config.WinterG, v => Config.WinterG = v,
                () => Config.WinterB, v => Config.WinterB = v,
                () => Config.WinterIntensity, v => Config.WinterIntensity = v,
                () => Config.WinterBuildUp, v => Config.WinterBuildUp = v,
                () => Config.WinterFadeOut, v => Config.WinterFadeOut = v);
        }

        private void AddSeasonConfig(IGenericModConfigMenuApi menu, string seasonKey,
            Func<int> getR, Action<int> setR, Func<int> getG, Action<int> setG, Func<int> getB, Action<int> setB,
            Func<float> getInt, Action<float> setInt, Func<int> getBuild, Action<int> setBuild, Func<int> getFade, Action<int> setFade)
        {
            menu.AddSectionTitle(mod: ModManifest, text: () => Helper.Translation.Get($"config.preset.{seasonKey}"));

            menu.AddNumberOption(mod: ModManifest, getValue: getR, setValue: setR, name: () => Helper.Translation.Get("config.red"), min: 0, max: 255);
            menu.AddNumberOption(mod: ModManifest, getValue: getG, setValue: setG, name: () => Helper.Translation.Get("config.green"), min: 0, max: 255);
            menu.AddNumberOption(mod: ModManifest, getValue: getB, setValue: setB, name: () => Helper.Translation.Get("config.blue"), min: 0, max: 255);

            menu.AddNumberOption(mod: ModManifest, getValue: () => (int)(getInt() * 100), setValue: v => setInt(v / 100f), name: () => Helper.Translation.Get("config.intensity"), min: 0, max: 100);

            menu.AddNumberOption(mod: ModManifest, getValue: getBuild, setValue: setBuild, name: () => Helper.Translation.Get("config.buildup"), min: 0, max: 120, interval: 10);
            menu.AddNumberOption(mod: ModManifest, getValue: getFade, setValue: setFade, name: () => Helper.Translation.Get("config.fadeout"), min: 0, max: 240, interval: 10);
        }
    }
}