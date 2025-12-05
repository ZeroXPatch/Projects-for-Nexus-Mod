using System;
using System.Collections.Generic;
using System.Linq; // for Count()
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.TerrainFeatures;
using GenericModConfigMenu;

// XNA aliases to avoid conflicts with System.Drawing / System.Numerics
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace NaturalIrrigation
{
    /// <summary>
    /// Automatically waters tilled soil near natural water sources,
    /// with optional moisture bonuses and a debug overlay.
    /// </summary>
    public class ModEntry : Mod
    {
        public ModConfig Config { get; private set; } = new();

        /// <summary>Tiles watered by natural irrigation, grouped per location ID.</summary>
        private readonly Dictionary<string, HashSet<Vector2>> irrigatedTilesByLocation = new();

        /// <summary>Tiles in the highest moisture tier (very close to water), grouped per location ID.</summary>
        private readonly Dictionary<string, HashSet<Vector2>> highMoistureTilesByLocation = new();

        /// <summary>Whether the overlay is currently visible in-game.</summary>
        private bool overlayVisible;

        public override void Entry(IModHelper helper)
        {
            this.Config = this.Helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.Display.RenderedWorld += this.OnRenderedWorld;
            helper.Events.Input.ButtonsChanged += this.OnButtonsChanged;
        }

        /*********
        ** Events
        *********/

        /// <summary>Wire Generic Mod Config Menu.</summary>
        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm is null)
                return;

            gmcm.Register(
                mod: this.ModManifest,
                reset: () => this.Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.Config)
            );

            //
            // General section
            //
            gmcm.AddSectionTitle(
                this.ModManifest,
                () => this.Helper.Translation.Get("config.section.general"),
                () => this.Helper.Translation.Get("config.section.general.tooltip")
            );

            gmcm.AddBoolOption(
                this.ModManifest,
                () => this.Config.Enabled,
                value => this.Config.Enabled = value,
                () => this.Helper.Translation.Get("config.enabled.name"),
                () => this.Helper.Translation.Get("config.enabled.tooltip")
            );

            gmcm.AddNumberOption(
                this.ModManifest,
                () => this.Config.WaterSearchRadius,
                value => this.Config.WaterSearchRadius = value,
                () => this.Helper.Translation.Get("config.radius.name"),
                () => this.Helper.Translation.Get("config.radius.tooltip"),
                min: 0,
                max: 10,
                interval: 1
            );

            //
            // Moisture bonuses
            //
            gmcm.AddSectionTitle(
                this.ModManifest,
                () => this.Helper.Translation.Get("config.section.moisture"),
                () => this.Helper.Translation.Get("config.section.moisture.tooltip")
            );

            gmcm.AddNumberOption(
                this.ModManifest,
                () => this.Config.HighMoistureRadius,
                value => this.Config.HighMoistureRadius = value,
                () => this.Helper.Translation.Get("config.moisture.radius.name"),
                () => this.Helper.Translation.Get("config.moisture.radius.tooltip"),
                min: 0,
                max: 3,
                interval: 1
            );

            gmcm.AddNumberOption(
                this.ModManifest,
                () => this.Config.HighMoistureGrowthChancePercent,
                value => this.Config.HighMoistureGrowthChancePercent = value,
                () => this.Helper.Translation.Get("config.moisture.chance.name"),
                () => this.Helper.Translation.Get("config.moisture.chance.tooltip"),
                min: 0,
                max: 100,
                interval: 1,
                formatValue: v => $"{v}%"
            );

            //
            // Overlay
            //
            gmcm.AddSectionTitle(
                this.ModManifest,
                () => this.Helper.Translation.Get("config.section.overlay"),
                () => this.Helper.Translation.Get("config.section.overlay.tooltip")
            );

            gmcm.AddBoolOption(
                this.ModManifest,
                () => this.Config.ShowIrrigationOverlay,
                value => this.Config.ShowIrrigationOverlay = value,
                () => this.Helper.Translation.Get("config.overlay.show.name"),
                () => this.Helper.Translation.Get("config.overlay.show.tooltip")
            );

            gmcm.AddKeybind(
                this.ModManifest,
                () => this.Config.OverlayToggleKey,
                value => this.Config.OverlayToggleKey = value,
                () => this.Helper.Translation.Get("config.overlay.key.name"),
                () => this.Helper.Translation.Get("config.overlay.key.tooltip")
            );
        }

        /// <summary>Runs at the start of each in-game day.</summary>
        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.player is null)
                return;

            this.irrigatedTilesByLocation.Clear();
            this.highMoistureTilesByLocation.Clear();

            // do NOT show overlay by default
            this.overlayVisible = this.Config.ShowIrrigationOverlay && false;

            if (!this.Config.Enabled || this.Config.WaterSearchRadius <= 0)
                return;

            foreach (GameLocation location in Game1.locations)
                this.WaterNearbySoilInLocation(location);
        }

        /// <summary>Toggle irrigation overlay with the configured key.</summary>
        private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.player is null)
                return;

            if (!this.Config.ShowIrrigationOverlay || this.Config.OverlayToggleKey == SButton.None)
                return;

            if (e.Pressed.Contains(this.Config.OverlayToggleKey))
                this.overlayVisible = !this.overlayVisible;
        }

        /// <summary>Draw overlay for irrigated & high-moisture tiles.</summary>
        private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.player is null)
                return;

            if (!this.Config.ShowIrrigationOverlay || !this.overlayVisible)
                return;

            GameLocation? location = Game1.currentLocation;
            if (location is null)
                return;

            string key = location.NameOrUniqueName;

            if (!this.irrigatedTilesByLocation.TryGetValue(key, out var irrigated) || irrigated.Count == 0)
                return;

            this.highMoistureTilesByLocation.TryGetValue(key, out var highMoisture);

            SpriteBatch spriteBatch = e.SpriteBatch;

            foreach (Vector2 tile in irrigated)
            {
                Rectangle rect = new Rectangle(
                    (int)(tile.X * Game1.tileSize - Game1.viewport.X),
                    (int)(tile.Y * Game1.tileSize - Game1.viewport.Y),
                    Game1.tileSize,
                    Game1.tileSize
                );

                Color color = new Color(0, 0, 255, 90); // irrigated

                if (highMoisture != null && highMoisture.Contains(tile))
                    color = new Color(0, 255, 255, 120); // high moisture

                spriteBatch.Draw(Game1.staminaRect, rect, color);
            }
        }

        /*********
        ** Logic
        *********/

        /// <summary>Water soil near water and apply extra effects.</summary>
        private void WaterNearbySoilInLocation(GameLocation location)
        {
            if (location.terrainFeatures is null || location.terrainFeatures.Count() == 0)
                return;

            string key = location.NameOrUniqueName;

            if (!this.irrigatedTilesByLocation.TryGetValue(key, out var irrigated))
            {
                irrigated = new HashSet<Vector2>();
                this.irrigatedTilesByLocation[key] = irrigated;
            }

            HashSet<Vector2>? highMoisture = null;
            if (this.Config.HighMoistureRadius > 0)
            {
                if (!this.highMoistureTilesByLocation.TryGetValue(key, out highMoisture))
                {
                    highMoisture = new HashSet<Vector2>();
                    this.highMoistureTilesByLocation[key] = highMoisture;
                }
            }

            foreach (var pair in location.terrainFeatures.Pairs)
            {
                if (pair.Value is not HoeDirt dirt)
                    continue;

                Vector2 tile = pair.Key;

                int? distance = this.GetNearestWaterDistance(location, tile, this.Config.WaterSearchRadius);
                if (distance is null)
                    continue;

                irrigated.Add(tile);
                dirt.state.Value = 1; // watered

                if (highMoisture != null && distance.Value <= this.Config.HighMoistureRadius)
                {
                    highMoisture.Add(tile);

                    if (this.Config.HighMoistureGrowthChancePercent > 0 &&
                        Game1.random.Next(0, 100) < this.Config.HighMoistureGrowthChancePercent)
                    {
                        this.TryApplyGrowthBoost(dirt);
                    }
                }
            }
        }

        /// <summary>Give the crop on this soil an extra "day" of growth.</summary>
        private void TryApplyGrowthBoost(HoeDirt dirt)
        {
            var crop = dirt.crop;
            if (crop is null || crop.fullyGrown.Value)
                return;

            if (crop.phaseDays is null || crop.phaseDays.Count == 0)
                return;

            if (crop.currentPhase.Value >= crop.phaseDays.Count - 1)
                return;

            int phase = crop.currentPhase.Value;
            int daysInPhase = crop.phaseDays[phase];

            if (crop.dayOfCurrentPhase.Value >= daysInPhase - 1)
            {
                crop.currentPhase.Value = Math.Min(crop.currentPhase.Value + 1, crop.phaseDays.Count - 1);
                crop.dayOfCurrentPhase.Value = 0;
            }
            else
            {
                crop.dayOfCurrentPhase.Value++;
            }
        }

        /// <summary>Get distance to nearest water tile (Chebyshev distance), or <c>null</c> if none.</summary>
        private int? GetNearestWaterDistance(GameLocation location, Vector2 tile, int radius)
        {
            if (radius <= 0)
                return null;

            int originX = (int)tile.X;
            int originY = (int)tile.Y;

            int best = int.MaxValue;
            bool found = false;

            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    int x = originX + dx;
                    int y = originY + dy;

                    if (!location.isTileOnMap(x, y))
                        continue;

                    if (!this.IsWaterTile(location, x, y))
                        continue;

                    int distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    if (distance < best)
                    {
                        best = distance;
                        found = true;
                        if (best == 0)
                            break;
                    }
                }
            }

            return found && best != int.MaxValue ? best : (int?)null;
        }

        /// <summary>Is this tile considered open water?</summary>
        private bool IsWaterTile(GameLocation location, int x, int y)
        {
            if (location.isOpenWater(x * Game1.tileSize, y * Game1.tileSize))
                return true;

            if (location.doesTileHaveProperty(x, y, "Water", "Back") != null)
                return true;

            return false;
        }
    }

    /// <summary>Config model for Natural Irrigation.</summary>
    public class ModConfig
    {
        /// <summary>Whether the mod’s behavior is active.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Max distance from water in tiles for auto-watering.</summary>
        public int WaterSearchRadius { get; set; } = 4;

        /// <summary>Distance (in tiles) for the high-moisture zone.</summary>
        public int HighMoistureRadius { get; set; } = 1;

        /// <summary>% chance per day for high-moisture crops to get an extra growth tick.</summary>
        public int HighMoistureGrowthChancePercent { get; set; } = 15;

        /// <summary>If true, the overlay feature can be used (but it starts hidden by default).</summary>
        public bool ShowIrrigationOverlay { get; set; } = true;

        /// <summary>Key used to toggle the overlay visibility in-game.</summary>
        public SButton OverlayToggleKey { get; set; } = SButton.F8;
    }
}

namespace GenericModConfigMenu
{
    using System;
    using Microsoft.Xna.Framework;
    using Microsoft.Xna.Framework.Graphics;
    using StardewModdingAPI;
    using StardewModdingAPI.Utilities;
    using StardewValley;

    /// <summary>Minimal subset of GMCM's public API used by this mod.</summary>
    public interface IGenericModConfigMenuApi
    {
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);

        void AddSectionTitle(IManifest mod, Func<string> text, Func<string>? tooltip = null);

        void AddBoolOption(
            IManifest mod,
            Func<bool> getValue,
            Action<bool> setValue,
            Func<string> name,
            Func<string>? tooltip = null,
            string? fieldId = null
        );

        void AddNumberOption(
            IManifest mod,
            Func<int> getValue,
            Action<int> setValue,
            Func<string> name,
            Func<string>? tooltip = null,
            int? min = null,
            int? max = null,
            int? interval = null,
            Func<int, string>? formatValue = null,
            string? fieldId = null
        );

        void AddKeybind(
            IManifest mod,
            Func<SButton> getValue,
            Action<SButton> setValue,
            Func<string> name,
            Func<string>? tooltip = null,
            string? fieldId = null
        );
    }
}
