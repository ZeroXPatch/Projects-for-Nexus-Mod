using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace SunriseTransitions
{
    public class ModEntry : Mod
    {
        public static ModConfig Config = null!;
        private Texture2D OverlayTexture = null!;

        private VisualSettings ActiveSettings;
        private const string ModDataKey = "Zero.SunriseTransitions/CurrentPreset";

        private struct VisualSettings
        {
            public int R, G, B;
            public float Intensity;
            public int Duration;
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

                if (shouldRoll) RollRandomPreset();
            }

            UpdateActiveSettings();
        }

        private void RollRandomPreset()
        {
            Random rnd = new Random(Guid.NewGuid().GetHashCode());
            int choice = rnd.Next(0, 4);

            string seasonKey = choice switch
            {
                0 => "spring",
                1 => "summer",
                2 => "fall",
                3 => "winter",
                _ => "spring"
            };

            Game1.player.modData[ModDataKey] = seasonKey;
        }

        private void UpdateActiveSettings()
        {
            string targetSeason = Game1.currentSeason;

            if (Config.EnableRandomMode)
            {
                if (Game1.player != null && Game1.player.modData.TryGetValue(ModDataKey, out string storedSeason))
                {
                    targetSeason = storedSeason;
                }
            }

            switch (targetSeason)
            {
                case "summer":
                    ActiveSettings = new VisualSettings { R = Config.SummerR, G = Config.SummerG, B = Config.SummerB, Intensity = Config.SummerIntensity, Duration = Config.SummerDuration };
                    break;
                case "fall":
                    ActiveSettings = new VisualSettings { R = Config.FallR, G = Config.FallG, B = Config.FallB, Intensity = Config.FallIntensity, Duration = Config.FallDuration };
                    break;
                case "winter":
                    ActiveSettings = new VisualSettings { R = Config.WinterR, G = Config.WinterG, B = Config.WinterB, Intensity = Config.WinterIntensity, Duration = Config.WinterDuration };
                    break;
                default: // spring
                    ActiveSettings = new VisualSettings { R = Config.SpringR, G = Config.SpringG, B = Config.SpringB, Intensity = Config.SpringIntensity, Duration = Config.SpringDuration };
                    break;
            }
        }

        private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.currentLocation == null) return;
            if (!Game1.currentLocation.IsOutdoors) return;

            float opacity = GetSunriseOpacity();

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

        private float GetSunriseOpacity()
        {
            int currentTime = Game1.timeOfDay;
            int startTime = 600; // 6:00 AM

            int currentMinutes = (currentTime / 100 * 60) + (currentTime % 100);
            int startMinutes = (startTime / 100 * 60) + (startTime % 100);

            int elapsed = currentMinutes - startMinutes;

            if (elapsed < 0) return ActiveSettings.Intensity;
            if (elapsed > ActiveSettings.Duration) return 0f;

            float progress = (float)elapsed / ActiveSettings.Duration;

            return MathHelper.Lerp(ActiveSettings.Intensity, 0f, progress);
        }

        // --- GMCM ---
        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null) return;

            configMenu.Register(
                mod: ModManifest,
                reset: () => Config = new ModConfig(),
                save: () => {
                    Helper.WriteConfig(Config);
                    UpdateActiveSettings();
                }
            );

            // Random
            configMenu.AddSectionTitle(mod: ModManifest, text: () => Helper.Translation.Get("config.section.random"));
            configMenu.AddBoolOption(mod: ModManifest, getValue: () => Config.EnableRandomMode, setValue: v => Config.EnableRandomMode = v, name: () => Helper.Translation.Get("config.random_mode.name"), tooltip: () => Helper.Translation.Get("config.random_mode.desc"));
            configMenu.AddTextOption(mod: ModManifest, getValue: () => Config.Frequency.ToString(), setValue: v => Config.Frequency = (RandomFrequency)Enum.Parse(typeof(RandomFrequency), v), name: () => Helper.Translation.Get("config.frequency.name"), allowedValues: Enum.GetNames(typeof(RandomFrequency)), formatAllowedValue: v => Helper.Translation.Get($"config.frequency.{v.ToLower()}"));

            // Presets
            AddSeasonConfig(configMenu, "spring",
                () => Config.SpringR, v => Config.SpringR = v, () => Config.SpringG, v => Config.SpringG = v, () => Config.SpringB, v => Config.SpringB = v,
                () => Config.SpringIntensity, v => Config.SpringIntensity = v, () => Config.SpringDuration, v => Config.SpringDuration = v);

            AddSeasonConfig(configMenu, "summer",
                () => Config.SummerR, v => Config.SummerR = v, () => Config.SummerG, v => Config.SummerG = v, () => Config.SummerB, v => Config.SummerB = v,
                () => Config.SummerIntensity, v => Config.SummerIntensity = v, () => Config.SummerDuration, v => Config.SummerDuration = v);

            AddSeasonConfig(configMenu, "fall",
                () => Config.FallR, v => Config.FallR = v, () => Config.FallG, v => Config.FallG = v, () => Config.FallB, v => Config.FallB = v,
                () => Config.FallIntensity, v => Config.FallIntensity = v, () => Config.FallDuration, v => Config.FallDuration = v);

            AddSeasonConfig(configMenu, "winter",
                () => Config.WinterR, v => Config.WinterR = v, () => Config.WinterG, v => Config.WinterG = v, () => Config.WinterB, v => Config.WinterB = v,
                () => Config.WinterIntensity, v => Config.WinterIntensity = v, () => Config.WinterDuration, v => Config.WinterDuration = v);
        }

        private void AddSeasonConfig(IGenericModConfigMenuApi menu, string seasonKey,
            Func<int> getR, Action<int> setR, Func<int> getG, Action<int> setG, Func<int> getB, Action<int> setB,
            Func<float> getInt, Action<float> setInt, Func<int> getDur, Action<int> setDur)
        {
            menu.AddSectionTitle(mod: ModManifest, text: () => Helper.Translation.Get($"config.preset.{seasonKey}"));
            menu.AddNumberOption(mod: ModManifest, getValue: getR, setValue: setR, name: () => Helper.Translation.Get("config.red"), min: 0, max: 255);
            menu.AddNumberOption(mod: ModManifest, getValue: getG, setValue: setG, name: () => Helper.Translation.Get("config.green"), min: 0, max: 255);
            menu.AddNumberOption(mod: ModManifest, getValue: getB, setValue: setB, name: () => Helper.Translation.Get("config.blue"), min: 0, max: 255);
            menu.AddNumberOption(mod: ModManifest, getValue: () => (int)(getInt() * 100), setValue: v => setInt(v / 100f), name: () => Helper.Translation.Get("config.intensity.name"), tooltip: () => Helper.Translation.Get("config.intensity.desc"), min: 0, max: 100);
            menu.AddNumberOption(mod: ModManifest, getValue: getDur, setValue: setDur, name: () => Helper.Translation.Get("config.duration.name"), tooltip: () => Helper.Translation.Get("config.duration.desc"), min: 0, max: 360, interval: 10);
        }
    }
}