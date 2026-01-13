using System;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace CustomNightLights
{
    public class ModEntry : Mod
    {
        public ModConfig Config = null!;

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.OneSecondUpdateTicked += OnOneSecondUpdateTicked;
            helper.Events.Player.Warped += OnWarped;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            // Pass logic to the GMCM helper
            ModGMCM.Setup(this.Helper, this.ModManifest, this.Config, () => Helper.WriteConfig(this.Config));
        }

        private void OnWarped(object? sender, WarpedEventArgs e)
        {
            ApplyLightSettings();
        }

        private void OnOneSecondUpdateTicked(object? sender, OneSecondUpdateTickedEventArgs e)
        {
            ApplyLightSettings();
        }

        private void ApplyLightSettings()
        {
            if (Game1.currentLocation == null) return;

            // 1. Determine Context (Indoors vs Outdoors)
            bool isOutdoors = Game1.currentLocation.IsOutdoors;

            // 2. Load the correct settings based on location
            bool active;
            int r, g, b;
            float intensity, radius;

            if (isOutdoors)
            {
                active = this.Config.EnableOutdoor;
                r = this.Config.OutdoorRed;
                g = this.Config.OutdoorGreen;
                b = this.Config.OutdoorBlue;
                intensity = this.Config.OutdoorIntensity;
                radius = this.Config.OutdoorRadius;
            }
            else
            {
                active = this.Config.EnableIndoor;
                r = this.Config.IndoorRed;
                g = this.Config.IndoorGreen;
                b = this.Config.IndoorBlue;
                intensity = this.Config.IndoorIntensity;
                radius = this.Config.IndoorRadius;
            }

            // If this specific section is disabled, do nothing (leave lights as default)
            if (!active) return;

            // 3. Prepare Data
            // Fix for "Light stays off": Ensure we don't accidentally set Alpha to 0 permanently if intensity is 0.
            // We use the intensity value directly.
            float clampedIntensity = Math.Clamp(intensity, 0f, 1f);
            byte alphaValue = (byte)(clampedIntensity * 255);

            Color targetColor = new(r, g, b)
            {
                A = alphaValue
            };

            // Fix for "Light deleted": Stardew deletes lights with Radius 0. 
            // We clamp the minimum to 0.1f so the light source persists even if "turned off" visually.
            float targetRadius = Math.Max(0.1f, radius);

            // 4. Apply to all lights in the list
            foreach (var light in Game1.currentLightSources.Values)
            {
                // Skip player lights (Glow Rings, Torches held)
                if (light.PlayerID != 0) continue;

                // Apply separate configuration
                light.color.Value = targetColor;
                light.radius.Value = targetRadius;
            }
        }
    }
}