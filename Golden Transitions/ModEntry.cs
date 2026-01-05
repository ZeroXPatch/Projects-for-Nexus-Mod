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
        private ModConfig Config = null!;
        private Texture2D Pixel = null!;

        // Deep Sunset Orange
        private readonly Color GoldenColor = new Color(255, 140, 20);

        public override void Entry(IModHelper helper)
        {
            Config = helper.ReadConfig<ModConfig>();

            Pixel = new Texture2D(Game1.graphics.GraphicsDevice, 1, 1);
            Pixel.SetData(new[] { Color.White });

            helper.Events.Display.RenderedWorld += OnRenderedWorld;
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

            configMenu.AddSectionTitle(mod: ModManifest, text: () => "Timing");

            configMenu.AddNumberOption(
                mod: ModManifest,
                getValue: () => Config.StartOffsetMinutes,
                setValue: value => Config.StartOffsetMinutes = value,
                name: () => "Start Offset (Minutes)",
                tooltip: () => "When to start the effect relative to sunset.\n0 = At Sunset.\n-60 = 1 Hour Before.",
                min: -120, max: 120, interval: 10
            );

            configMenu.AddSectionTitle(mod: ModManifest, text: () => "Intensity Timeline (0-100%)");

            configMenu.AddNumberOption(mod: ModManifest, getValue: () => Config.IntensityAt0Min, setValue: v => Config.IntensityAt0Min = v, name: () => "0 Minutes (Start)", min: 0, max: 100);
            configMenu.AddNumberOption(mod: ModManifest, getValue: () => Config.IntensityAt30Min, setValue: v => Config.IntensityAt30Min = v, name: () => "+ 30 Minutes", min: 0, max: 100);
            configMenu.AddNumberOption(mod: ModManifest, getValue: () => Config.IntensityAt60Min, setValue: v => Config.IntensityAt60Min = v, name: () => "+ 60 Minutes", min: 0, max: 100);
            configMenu.AddNumberOption(mod: ModManifest, getValue: () => Config.IntensityAt90Min, setValue: v => Config.IntensityAt90Min = v, name: () => "+ 90 Minutes", min: 0, max: 100);
            configMenu.AddNumberOption(mod: ModManifest, getValue: () => Config.IntensityAt120Min, setValue: v => Config.IntensityAt120Min = v, name: () => "+ 120 Minutes (End)", min: 0, max: 100);
        }

        private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.currentLocation == null) return;
            if (!Game1.currentLocation.IsOutdoors) return;

            // 1. Get Sunset Time (from Vanilla or Dynamic Dusk)
            int sunsetTime = Game1.getStartingToGetDarkTime(Game1.currentLocation);

            // 2. Calculate Intensity based on the Timeline
            float opacity = CalculateTimelineOpacity(sunsetTime);

            // 3. Draw Overlay
            if (opacity > 0f)
            {
                // This draws ON TOP of the game's lighting, adding our orange tint
                Color drawColor = GoldenColor * opacity;

                e.SpriteBatch.Draw(
                    Pixel,
                    new Rectangle(0, 0, Game1.viewport.Width, Game1.viewport.Height),
                    drawColor
                );
            }
        }

        private float CalculateTimelineOpacity(int sunsetTime)
        {
            int currentTime = Game1.timeOfDay;

            // Convert times to absolute minutes
            int currentMins = (currentTime / 100 * 60) + (currentTime % 100);
            int sunsetMins = (sunsetTime / 100 * 60) + (sunsetTime % 100);

            // Determine Start Time based on Config Offset
            int startMins = sunsetMins + Config.StartOffsetMinutes;
            int elapsed = currentMins - startMins;

            // Outside the 2-hour (120 min) window? Return 0.
            if (elapsed < 0 || elapsed > 120) return 0f;

            // Interpolate between the 30-minute keyframes
            // We use MathHelper.Lerp(StartVal, EndVal, Progress 0-1)

            float valA, valB, progress;

            if (elapsed <= 30)
            {
                // 0 to 30 mins
                valA = Config.IntensityAt0Min / 100f;
                valB = Config.IntensityAt30Min / 100f;
                progress = elapsed / 30f;
            }
            else if (elapsed <= 60)
            {
                // 30 to 60 mins
                valA = Config.IntensityAt30Min / 100f;
                valB = Config.IntensityAt60Min / 100f;
                progress = (elapsed - 30) / 30f;
            }
            else if (elapsed <= 90)
            {
                // 60 to 90 mins
                valA = Config.IntensityAt60Min / 100f;
                valB = Config.IntensityAt90Min / 100f;
                progress = (elapsed - 60) / 30f;
            }
            else
            {
                // 90 to 120 mins
                valA = Config.IntensityAt90Min / 100f;
                valB = Config.IntensityAt120Min / 100f;
                progress = (elapsed - 90) / 30f;
            }

            return MathHelper.Lerp(valA, valB, progress);
        }
    }
}