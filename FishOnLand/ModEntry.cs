using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using xTile.Dimensions; // Location (for isTilePassable)

using XnaRectangle = Microsoft.Xna.Framework.Rectangle;

namespace LandFishSwimmers
{
    internal sealed class ModEntry : Mod
    {
        private ModConfig Config = new();
        private readonly Dictionary<string, List<LandFish>> FishByLocation = new(StringComparer.OrdinalIgnoreCase);
        private readonly Random Rng = new();

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.Display.RenderedWorld += this.OnRenderedWorld;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            this.RegisterGmcm();
        }

        private void RegisterGmcm()
        {
            var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm is null)
                return;

            string T(string key) => this.Helper.Translation.Get(key);

            gmcm.Register(
                mod: this.ModManifest,
                reset: () => this.Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.Config)
            );

            gmcm.AddSectionTitle(
                mod: this.ModManifest,
                text: () => T("gmcm.section.general"),
                tooltip: () => T("gmcm.section.general.tooltip")
            );

            gmcm.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => this.Config.Enabled,
                setValue: value => this.Config.Enabled = value,
                name: () => T("gmcm.enabled.name"),
                tooltip: () => T("gmcm.enabled.tooltip")
            );

            gmcm.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => this.Config.FishPerLocation,
                setValue: value => this.Config.FishPerLocation = value,
                name: () => T("gmcm.fishPerLocation.name"),
                tooltip: () => T("gmcm.fishPerLocation.tooltip"),
                min: 0,
                max: 200,
                interval: 1
            );

            gmcm.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => this.Config.SpawnIndoors,
                setValue: value => this.Config.SpawnIndoors = value,
                name: () => T("gmcm.spawnIndoors.name"),
                tooltip: () => T("gmcm.spawnIndoors.tooltip")
            );

            gmcm.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => this.Config.RespawnEachDay,
                setValue: value => this.Config.RespawnEachDay = value,
                name: () => T("gmcm.respawnEachDay.name"),
                tooltip: () => T("gmcm.respawnEachDay.tooltip")
            );

            gmcm.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => this.Config.UpdateTicks,
                setValue: value => this.Config.UpdateTicks = Math.Max(1, value),
                name: () => T("gmcm.updateTicks.name"),
                tooltip: () => T("gmcm.updateTicks.tooltip"),
                min: 1,
                max: 60,
                interval: 1
            );

            gmcm.AddSectionTitle(
                mod: this.ModManifest,
                text: () => T("gmcm.section.movement"),
                tooltip: () => T("gmcm.section.movement.tooltip")
            );

            gmcm.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => this.Config.WanderRadiusTiles,
                setValue: value => this.Config.WanderRadiusTiles = Math.Max(1, value),
                name: () => T("gmcm.wanderRadius.name"),
                tooltip: () => T("gmcm.wanderRadius.tooltip"),
                min: 1,
                max: 50,
                interval: 1
            );

            // Speed (float) via scaled int (x10)
            gmcm.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => (int)Math.Round(this.Config.SpeedPixelsPerUpdate * 10f),
                setValue: value => this.Config.SpeedPixelsPerUpdate = Math.Max(0.1f, value / 10f),
                name: () => T("gmcm.speed.name"),
                tooltip: () => T("gmcm.speed.tooltip"),
                min: 1,
                max: 200,
                interval: 1,
                formatValue: v => (v / 10f).ToString("0.0")
            );

            gmcm.AddSectionTitle(
                mod: this.ModManifest,
                text: () => T("gmcm.section.visuals"),
                tooltip: () => T("gmcm.section.visuals.tooltip")
            );

            // Scale (float) via percent
            gmcm.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => (int)Math.Round(this.Config.Scale * 100f),
                setValue: value => this.Config.Scale = Math.Max(0.1f, value / 100f),
                name: () => T("gmcm.scale.name"),
                tooltip: () => T("gmcm.scale.tooltip"),
                min: 25,
                max: 300,
                interval: 5,
                formatValue: v => $"{v}%"
            );

            // Opacity (float) via percent
            gmcm.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => (int)Math.Round(this.Config.Opacity * 100f),
                setValue: value => this.Config.Opacity = Math.Clamp(value / 100f, 0f, 1f),
                name: () => T("gmcm.opacity.name"),
                tooltip: () => T("gmcm.opacity.tooltip"),
                min: 0,
                max: 100,
                interval: 5,
                formatValue: v => $"{v}%"
            );

            // BobPixels (float) via x10
            gmcm.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => (int)Math.Round(this.Config.BobPixels * 10f),
                setValue: value => this.Config.BobPixels = Math.Max(0f, value / 10f),
                name: () => T("gmcm.bob.name"),
                tooltip: () => T("gmcm.bob.tooltip"),
                min: 0,
                max: 100,
                interval: 1,
                formatValue: v => (v / 10f).ToString("0.0")
            );

            // WiggleRadians (float) via x1000
            gmcm.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => (int)Math.Round(this.Config.WiggleRadians * 1000f),
                setValue: value => this.Config.WiggleRadians = Math.Max(0f, value / 1000f),
                name: () => T("gmcm.wiggle.name"),
                tooltip: () => T("gmcm.wiggle.tooltip"),
                min: 0,
                max: 500,
                interval: 5,
                formatValue: v => (v / 1000f).ToString("0.000")
            );
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            this.RebuildAllLocations();
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.player is null)
                return;

            if (this.Config.RespawnEachDay)
                this.RebuildAllLocations();
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.player is null)
                return;

            if (!this.Config.Enabled)
                return;

            if (!e.IsMultipleOf((uint)Math.Max(1, this.Config.UpdateTicks)))
                return;

            foreach (GameLocation loc in Game1.locations)
            {
                if (loc is null || loc.Map is null)
                    continue;

                if (!this.Config.SpawnIndoors && loc.IsOutdoors == false)
                    continue;

                if (!this.FishByLocation.TryGetValue(loc.NameOrUniqueName, out List<LandFish>? list))
                    continue;

                for (int i = list.Count - 1; i >= 0; i--)
                {
                    LandFish fish = list[i];

                    if (!fish.IsValidForLocation(loc))
                    {
                        list.RemoveAt(i);
                        continue;
                    }

                    fish.Update(loc, this.Config, this.Rng);
                }

                int targetCount = Math.Max(0, this.Config.FishPerLocation);
                while (list.Count < targetCount)
                {
                    LandFish? spawned = this.TrySpawnFish(loc, this.Rng);
                    if (spawned is null)
                        break;

                    list.Add(spawned);
                }

                while (list.Count > targetCount)
                    list.RemoveAt(list.Count - 1);
            }
        }

        private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.player is null)
                return;

            if (!this.Config.Enabled)
                return;

            GameLocation loc = Game1.currentLocation;
            if (loc is null || loc.Map is null)
                return;

            if (!this.Config.SpawnIndoors && loc.IsOutdoors == false)
                return;

            if (!this.FishByLocation.TryGetValue(loc.NameOrUniqueName, out List<LandFish>? list))
                return;

            SpriteBatch b = e.SpriteBatch;
            foreach (LandFish fish in list)
                fish.Draw(b, this.Config);
        }

        private void RebuildAllLocations()
        {
            this.FishByLocation.Clear();

            if (!Context.IsWorldReady || Game1.player is null)
                return;

            if (!this.Config.Enabled)
                return;

            foreach (GameLocation loc in Game1.locations)
            {
                if (loc is null || loc.Map is null)
                    continue;

                if (!this.Config.SpawnIndoors && loc.IsOutdoors == false)
                    continue;

                var list = new List<LandFish>();
                for (int i = 0; i < Math.Max(0, this.Config.FishPerLocation); i++)
                {
                    LandFish? spawned = this.TrySpawnFish(loc, this.Rng);
                    if (spawned is null)
                        break;

                    list.Add(spawned);
                }

                this.FishByLocation[loc.NameOrUniqueName] = list;
            }
        }

        private LandFish? TrySpawnFish(GameLocation loc, Random rng)
        {
            if (loc.Map is null || loc.Map.Layers.Count == 0)
                return null;

            int fishId = this.PickFishObjectId(rng);
            if (fishId <= 0)
                return null;

            int width = Math.Max(1, loc.Map.Layers[0].LayerWidth);
            int height = Math.Max(1, loc.Map.Layers[0].LayerHeight);

            for (int tries = 0; tries < 60; tries++)
            {
                int x = rng.Next(0, width);
                int y = rng.Next(0, height);
                Vector2 tile = new(x, y);

                if (!IsGoodLandTile(loc, tile))
                    continue;

                Vector2 pixel = tile * 64f + new Vector2(32f, 44f);
                var fish = new LandFish(fishId, pixel);
                fish.PickNewTarget(loc, this.Config, rng);
                return fish;
            }

            return null;
        }

        private int PickFishObjectId(Random rng)
        {
            if (this.Config.FishObjectIds is { Length: > 0 })
                return this.Config.FishObjectIds[rng.Next(this.Config.FishObjectIds.Length)];

            int[] defaults =
            {
                145, 132, 136, 137, 138,
                139, 142, 143, 146, 148
            };

            return defaults[rng.Next(defaults.Length)];
        }

        private static bool IsGoodLandTile(GameLocation loc, Vector2 tile)
        {
            if (loc?.Map is null || loc.Map.Layers.Count == 0)
                return false;

            if (!loc.isTileOnMap(tile))
                return false;

            int x = (int)tile.X;
            int y = (int)tile.Y;

            if (loc.isWaterTile(x, y))
                return false;

            if (!loc.isTilePassable(new Location(x, y), Game1.viewport))
                return false;

            if (loc.Objects.ContainsKey(tile))
                return false;

            return true;
        }

        private sealed class LandFish
        {
            private readonly int FishObjectId;

            private Vector2 Position;
            private Vector2 Target;
            private float BobPhase;
            private float Facing;
            private int StuckCounter;

            public LandFish(int fishObjectId, Vector2 startPixel)
            {
                this.FishObjectId = fishObjectId;
                this.Position = startPixel;
                this.Target = startPixel;
                this.BobPhase = (float)Game1.random.NextDouble() * 100f;
            }

            public bool IsValidForLocation(GameLocation loc)
            {
                return loc.Map?.Layers?.Count > 0;
            }

            public void PickNewTarget(GameLocation loc, ModConfig config, Random rng)
            {
                int radiusTiles = Math.Max(1, config.WanderRadiusTiles);
                Point originTile = new((int)(this.Position.X / 64f), (int)(this.Position.Y / 64f));

                for (int tries = 0; tries < 40; tries++)
                {
                    int dx = rng.Next(-radiusTiles, radiusTiles + 1);
                    int dy = rng.Next(-radiusTiles, radiusTiles + 1);

                    Vector2 tile = new(originTile.X + dx, originTile.Y + dy);
                    if (!IsGoodLandTile(loc, tile))
                        continue;

                    this.Target = tile * 64f + new Vector2(32f, 44f);
                    this.StuckCounter = 0;
                    return;
                }

                this.Target = this.Position;
            }

            public void Update(GameLocation loc, ModConfig config, Random rng)
            {
                float speed = Math.Max(0.1f, config.SpeedPixelsPerUpdate);

                Vector2 delta = this.Target - this.Position;
                float dist = delta.Length();

                if (dist < 6f)
                {
                    this.PickNewTarget(loc, config, rng);
                    delta = this.Target - this.Position;
                    dist = delta.Length();
                }

                Vector2 step = dist > 0.001f ? (delta / dist) * Math.Min(speed, dist) : Vector2.Zero;

                Vector2 nextPos = this.Position + step;
                Vector2 nextTile = new((int)(nextPos.X / 64f), (int)(nextPos.Y / 64f));
                if (!IsGoodLandTile(loc, nextTile))
                {
                    this.StuckCounter++;
                    if (this.StuckCounter >= 3)
                        this.PickNewTarget(loc, config, rng);
                    return;
                }

                this.Position = nextPos;

                if (step.LengthSquared() > 0.01f)
                    this.Facing = (float)Math.Atan2(step.Y, step.X);

                this.BobPhase += 0.25f;
            }

            public void Draw(SpriteBatch b, ModConfig config)
            {
                Texture2D tex = Game1.objectSpriteSheet;
                XnaRectangle src = Game1.getSourceRectForStandardTileSheet(tex, this.FishObjectId, 16, 16);

                float bob = (float)Math.Sin(this.BobPhase) * config.BobPixels;

                Vector2 world = new(this.Position.X, this.Position.Y + bob);
                Vector2 screen = Game1.GlobalToLocal(Game1.viewport, world);

                float scale = Math.Max(0.1f, config.Scale);
                float depth = Math.Max(0f, (world.Y + 16f) / 10000f);

                float rotation = this.Facing + (float)Math.Sin(this.BobPhase * 0.5f) * config.WiggleRadians;
                Vector2 origin = new(8f, 8f);

                b.Draw(
                    texture: tex,
                    position: screen,
                    sourceRectangle: src,
                    color: Color.White * config.Opacity,
                    rotation: rotation,
                    origin: origin,
                    scale: 4f * scale,
                    effects: SpriteEffects.None,
                    layerDepth: depth
                );
            }
        }
    }
}
