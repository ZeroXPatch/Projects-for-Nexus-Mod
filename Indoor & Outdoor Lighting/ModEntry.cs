using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomNightLights
{
    public class ModEntry : Mod
    {
        public ModConfig Config = null!;
        private HashSet<string> ExcludedLocationIds = new();

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();
            UpdateExclusionList();

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.OneSecondUpdateTicked += OnOneSecondUpdateTicked;
            helper.Events.Player.Warped += OnWarped;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            ModGMCM.Setup(this.Helper, this.ModManifest, this.Config, () =>
            {
                Helper.WriteConfig(this.Config);
                UpdateExclusionList();
            });
        }

        private void UpdateExclusionList()
        {
            ExcludedLocationIds = this.Config.IndoorExcludedLocations
                .Split(',')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
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
            var location = Game1.currentLocation; // This variable is used below
            bool isOutdoors = location.IsOutdoors;

            // --- 1. Filter Logic (Location Level) ---
            if (!isOutdoors)
            {
                if (ExcludedLocationIds.Contains(location.Name) || ExcludedLocationIds.Contains(location.NameOrUniqueName))
                    return;

                if (this.Config.IndoorFarmHouseOnly && !(location is FarmHouse))
                    return;
            }

            // --- 2. Determine Profile ---
            bool active;
            bool nightOnly;
            int r, g, b;
            float intensity, radius;

            if (isOutdoors)
            {
                active = this.Config.EnableOutdoor;
                nightOnly = this.Config.OutdoorNightOnly;
                r = this.Config.OutdoorRed;
                g = this.Config.OutdoorGreen;
                b = this.Config.OutdoorBlue;
                intensity = this.Config.OutdoorIntensity;
                radius = this.Config.OutdoorRadius;
            }
            else
            {
                active = this.Config.EnableIndoor;
                nightOnly = this.Config.IndoorNightOnly;
                r = this.Config.IndoorRed;
                g = this.Config.IndoorGreen;
                b = this.Config.IndoorBlue;
                intensity = this.Config.IndoorIntensity;
                radius = this.Config.IndoorRadius;
            }

            if (!active) return;

            // --- 3. DYNAMIC TIME CHECK ---
            if (nightOnly)
            {
                // FIX: Pass 'location' to getStartingToGetDarkTime
                int sunsetTime = Game1.getStartingToGetDarkTime(location);

                // Add 2 hours (200 in SDV time format)
                int activationTime = sunsetTime + 200;

                if (Game1.timeOfDay < activationTime)
                {
                    return;
                }
            }

            // --- 4. Prepare Values ---
            float clampedIntensity = Math.Clamp(intensity, 0f, 1f);
            byte alphaValue = (byte)(clampedIntensity * 255);

            Color targetColor = new(r, g, b) { A = alphaValue };
            float targetRadius = Math.Max(0.1f, radius);

            // --- 5. Apply to Lights ---
            foreach (var light in Game1.currentLightSources.Values)
            {
                if (light.PlayerID != 0) continue;

                light.color.Value = targetColor;
                light.radius.Value = targetRadius;
            }
        }
    }
}