
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System;

namespace CinematicWake
{
    public class ModEntry : Mod
    {
        private ModConfig Config = null!;
        private float FadeTimer = 0f;
        private bool IsWaking = false;

        public override void Entry(IModHelper helper)
        {
            Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.Display.RenderedHud += OnRenderedHud;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null) return;

            configMenu.Register(ModManifest, () => Config = new ModConfig(), () => Helper.WriteConfig(Config));

            configMenu.AddSectionTitle(ModManifest, () => Helper.Translation.Get("morning-section"));
            configMenu.AddNumberOption(ModManifest, () => Config.WakeUpDuration, (val) => Config.WakeUpDuration = val, () => Helper.Translation.Get("morning-duration"), min: 0.5f, max: 10f);

            // Preset Selection
            configMenu.AddTextOption(
                ModManifest,
                () => Config.SelectedPreset,
                (val) => Config.SelectedPreset = val,
                () => Helper.Translation.Get("morning-preset"),
                options: new string[] { "Sleepy Eyes", "Realistic Slate", "Golden Morning", "Blue Hour", "Custom" }
            );

            configMenu.AddSectionTitle(ModManifest, () => Helper.Translation.Get("morning-custom-header"));
            configMenu.AddNumberOption(ModManifest, () => Config.CustomRed, (val) => Config.CustomRed = val, () => Helper.Translation.Get("morning-red"), min: 0, max: 255);
            configMenu.AddNumberOption(ModManifest, () => Config.CustomGreen, (val) => Config.CustomGreen = val, () => Helper.Translation.Get("morning-green"), min: 0, max: 255);
            configMenu.AddNumberOption(ModManifest, () => Config.CustomBlue, (val) => Config.CustomBlue = val, () => Helper.Translation.Get("morning-blue"), min: 0, max: 255);
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            IsWaking = true;
            FadeTimer = Config.WakeUpDuration;
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || !IsWaking) return;
            FadeTimer -= (float)Game1.currentGameTime.ElapsedGameTime.TotalSeconds;
            if (FadeTimer <= 0) IsWaking = false;
        }

        private void OnRenderedHud(object? sender, RenderedHudEventArgs e)
        {
            if (!IsWaking || Game1.eventUp) return;

            float linearProgress = MathHelper.Clamp(FadeTimer / Config.WakeUpDuration, 0, 1);
            float easeAlpha = (float)Math.Pow(linearProgress, 2); // The Eye-Opening Math

            e.SpriteBatch.Draw(
                Game1.staminaRect,
                new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height),
                GetCurrentColor() * easeAlpha
            );
        }

        private Color GetCurrentColor()
        {
            return Config.SelectedPreset switch
            {
                "Sleepy Eyes" => new Color(25, 20, 15),    // Warm charcoal (eyelid mimic)
                "Realistic Slate" => new Color(175, 185, 200), // Gray-Blue dawn
                "Golden Morning" => new Color(255, 210, 150), // Sunrise
                "Blue Hour" => new Color(100, 150, 255), // Cinematic Blue
                "Custom" => new Color(Config.CustomRed, Config.CustomGreen, Config.CustomBlue),
                _ => new Color(25, 20, 15)
            };
        }
    }
}