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
        private Texture2D OverlayTexture = null!;

        public override void Entry(IModHelper helper)
        {
            Config = helper.ReadConfig<ModConfig>();

            // Generate a 1x1 white texture to serve as our overlay canvas
            OverlayTexture = new Texture2D(Game1.graphics.GraphicsDevice, 1, 1);
            OverlayTexture.SetData(new[] { Color.White });

            // Hook into the rendering loop to draw on top of the world
            helper.Events.Display.RenderedWorld += OnRenderedWorld;
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            // Register with Generic Mod Config Menu
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null) return;

            configMenu.Register(
                mod: ModManifest,
                reset: () => Config = new ModConfig(),
                save: () => Helper.WriteConfig(Config)
            );

            // --- SECTION 1: APPEARANCE ---
            configMenu.AddSectionTitle(mod: ModManifest, text: () => Helper.Translation.Get("config.section.appearance"));

            configMenu.AddNumberOption(
                mod: ModManifest,
                getValue: () => (int)(Config.PeakOpacity * 100),
                setValue: value => Config.PeakOpacity = value / 100f,
                name: () => Helper.Translation.Get("config.intensity.name"),
                tooltip: () => Helper.Translation.Get("config.intensity.desc"),
                min: 0, max: 100
            );

            // --- SECTION 2: COLOR ---
            configMenu.AddSectionTitle(mod: ModManifest, text: () => Helper.Translation.Get("config.section.color"));

            configMenu.AddNumberOption(mod: ModManifest, getValue: () => Config.ColorRed, setValue: v => Config.ColorRed = v, name: () => Helper.Translation.Get("config.red.name"), min: 0, max: 255);
            configMenu.AddNumberOption(mod: ModManifest, getValue: () => Config.ColorGreen, setValue: v => Config.ColorGreen = v, name: () => Helper.Translation.Get("config.green.name"), min: 0, max: 255);
            configMenu.AddNumberOption(mod: ModManifest, getValue: () => Config.ColorBlue, setValue: v => Config.ColorBlue = v, name: () => Helper.Translation.Get("config.blue.name"), min: 0, max: 255);

            // --- SECTION 3: SCHEDULE ---
            configMenu.AddSectionTitle(mod: ModManifest, text: () => Helper.Translation.Get("config.section.schedule"));

            configMenu.AddNumberOption(
                mod: ModManifest,
                getValue: () => Config.BuildUpMinutes,
                setValue: value => Config.BuildUpMinutes = value,
                name: () => Helper.Translation.Get("config.buildup.name"),
                tooltip: () => Helper.Translation.Get("config.buildup.desc"),
                min: 0, max: 120, interval: 10
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                getValue: () => Config.FadeOutMinutes,
                setValue: value => Config.FadeOutMinutes = value,
                name: () => Helper.Translation.Get("config.fadeout.name"),
                tooltip: () => Helper.Translation.Get("config.fadeout.desc"),
                min: 0, max: 240, interval: 10
            );
        }

        private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
        {
            // Only render when the world is active and we are outdoors
            if (!Context.IsWorldReady || Game1.currentLocation == null) return;
            if (!Game1.currentLocation.IsOutdoors) return;

            // 1. Fetch the Sunset Time
            // This grabs the time calculated by the game logic (Vanilla or modified by Dynamic Dusk)
            int targetSunsetTime = Game1.getStartingToGetDarkTime(Game1.currentLocation);

            // 2. Calculate current overlay strength
            float currentOpacity = CalculateOverlayOpacity(targetSunsetTime);

            // 3. Draw the overlay if visible
            if (currentOpacity > 0f)
            {
                Color baseColor = new Color(Config.ColorRed, Config.ColorGreen, Config.ColorBlue);
                Color renderColor = baseColor * currentOpacity;

                // Draw the colored rectangle over the entire viewport
                e.SpriteBatch.Draw(
                    OverlayTexture,
                    new Rectangle(0, 0, Game1.viewport.Width, Game1.viewport.Height),
                    renderColor
                );
            }
        }

        private float CalculateOverlayOpacity(int sunsetTime)
        {
            int currentTime = Game1.timeOfDay;

            // Normalize Stardew time (100s) to Linear Minutes (60s) for accurate math
            int currentTotalMinutes = (currentTime / 100 * 60) + (currentTime % 100);
            int sunsetTotalMinutes = (sunsetTime / 100 * 60) + (sunsetTime % 100);

            // Define the active window for the effect
            int startMinute = sunsetTotalMinutes - Config.BuildUpMinutes;
            int endMinute = sunsetTotalMinutes + Config.FadeOutMinutes;

            // Optimization: If outside the window, return 0 immediately
            if (currentTotalMinutes < startMinute || currentTotalMinutes > endMinute)
                return 0f;

            // Logic: Linearly interpolate (Lerp) the opacity based on time position

            // Phase 1: Pre-Sunset (Rising Opacity)
            if (currentTotalMinutes <= sunsetTotalMinutes)
            {
                if (Config.BuildUpMinutes <= 0) return Config.PeakOpacity;

                float elapsed = currentTotalMinutes - startMinute;
                float progress = elapsed / Config.BuildUpMinutes;

                // Smoothly go from 0 -> Peak
                return MathHelper.Lerp(0f, Config.PeakOpacity, progress);
            }
            // Phase 2: Post-Sunset (Falling Opacity)
            else
            {
                if (Config.FadeOutMinutes <= 0) return 0f;

                float elapsed = currentTotalMinutes - sunsetTotalMinutes;
                float progress = elapsed / Config.FadeOutMinutes;

                // Smoothly go from Peak -> 0
                return MathHelper.Lerp(Config.PeakOpacity, 0f, progress);
            }
        }
    }
}