using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.TerrainFeatures;
using StardewValley.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace LazySprinkler
{
    internal sealed class ModEntry : Mod
    {
        private ModConfig Config = new();
        private readonly List<GameLocation> _locationCache = new();
        private Random _random = new();

        public override void Entry(IModHelper helper)
        {
            Config = helper.ReadConfig<ModConfig>();
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var api = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (api is null)
            {
                return;
            }

            api.Register(ModManifest, ResetConfig, () => Helper.WriteConfig(Config));
            api.AddSectionTitle(ModManifest, () => Helper.Translation.Get("gmcm.section.behavior"));

            api.AddNumberOption(
                ModManifest,
                () => (float)Config.ExtraWaterChance,
                value => Config.ExtraWaterChance = value,
                () => Helper.Translation.Get("gmcm.extraWaterChance.name"),
                () => Helper.Translation.Get("gmcm.extraWaterChance.desc"),
                0f,
                1f,
                0.01f
            );

            api.AddNumberOption(
                ModManifest,
                () => Config.ExtraWaterRadius,
                value => Config.ExtraWaterRadius = Math.Max(0, value),
                () => Helper.Translation.Get("gmcm.extraWaterRadius.name"),
                () => Helper.Translation.Get("gmcm.extraWaterRadius.desc"),
                0,
                5,
                1
            );

            api.AddNumberOption(
                ModManifest,
                () => Config.ExtraWaterTiles,
                value => Config.ExtraWaterTiles = Math.Max(0, value),
                () => Helper.Translation.Get("gmcm.extraWaterTiles.name"),
                () => Helper.Translation.Get("gmcm.extraWaterTiles.desc"),
                0,
                12,
                1
            );

            api.AddNumberOption(
                ModManifest,
                () => (float)Config.SkipWaterChance,
                value => Config.SkipWaterChance = value,
                () => Helper.Translation.Get("gmcm.skipWaterChance.name"),
                () => Helper.Translation.Get("gmcm.skipWaterChance.desc"),
                0f,
                1f,
                0.01f
            );

            api.AddNumberOption(
                ModManifest,
                () => Config.MaxSkippedTiles,
                value => Config.MaxSkippedTiles = Math.Max(0, value),
                () => Helper.Translation.Get("gmcm.maxSkippedTiles.name"),
                () => Helper.Translation.Get("gmcm.maxSkippedTiles.desc"),
                0,
                12,
                1
            );

            api.AddNumberOption(
                ModManifest,
                () => (float)Config.FertilizerChance,
                value => Config.FertilizerChance = value,
                () => Helper.Translation.Get("gmcm.fertilizerChance.name"),
                () => Helper.Translation.Get("gmcm.fertilizerChance.desc"),
                0f,
                1f,
                0.01f
            );

            api.AddNumberOption(
                ModManifest,
                () => Config.FertilizerItemId,
                value => Config.FertilizerItemId = Math.Max(0, value),
                () => Helper.Translation.Get("gmcm.fertilizerItemId.name"),
                () => Helper.Translation.Get("gmcm.fertilizerItemId.desc"),
                0,
                999,
                1
            );

            api.AddNumberOption(
                ModManifest,
                () => (float)Config.OverflowChance,
                value => Config.OverflowChance = value,
                () => Helper.Translation.Get("gmcm.overflowChance.name"),
                () => Helper.Translation.Get("gmcm.overflowChance.desc"),
                0f,
                1f,
                0.01f
            );

            api.AddNumberOption(
                ModManifest,
                () => Config.OverflowRadius,
                value => Config.OverflowRadius = Math.Max(0, value),
                () => Helper.Translation.Get("gmcm.overflowRadius.name"),
                () => Helper.Translation.Get("gmcm.overflowRadius.desc"),
                0,
                5,
                1
            );

            api.AddNumberOption(
                ModManifest,
                () => Config.OverflowTiles,
                value => Config.OverflowTiles = Math.Max(0, value),
                () => Helper.Translation.Get("gmcm.overflowTiles.name"),
                () => Helper.Translation.Get("gmcm.overflowTiles.desc"),
                0,
                8,
                1
            );

            api.AddNumberOption(
                ModManifest,
                () => (float)Config.GrowthSpurtChance,
                value => Config.GrowthSpurtChance = value,
                () => Helper.Translation.Get("gmcm.growthSpurtChance.name"),
                () => Helper.Translation.Get("gmcm.growthSpurtChance.desc"),
                0f,
                1f,
                0.01f
            );

            api.AddNumberOption(
                ModManifest,
                () => Config.GrowthSpurtTiles,
                value => Config.GrowthSpurtTiles = Math.Max(0, value),
                () => Helper.Translation.Get("gmcm.growthSpurtTiles.name"),
                () => Helper.Translation.Get("gmcm.growthSpurtTiles.desc"),
                0,
                12,
                1
            );

            api.AddBoolOption(
                ModManifest,
                () => Config.DebugLogging,
                value => Config.DebugLogging = value,
                () => Helper.Translation.Get("gmcm.debugLogging.name"),
                () => Helper.Translation.Get("gmcm.debugLogging.desc")
            );
        }

        private void ResetConfig()
        {
            Config = new ModConfig();
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            _random = CreateDailyRandom();
            _locationCache.Clear();
            Utility.ForEachLocation(location => _locationCache.Add(location));

            foreach (var location in _locationCache)
            {
                foreach (var sprinkler in location.Objects.Values)
                {
                    if (!sprinkler.IsSprinkler())
                    {
                        continue;
                    }

                    var tiles = sprinkler.GetSprinklerTiles().ToList();
                    if (tiles.Count == 0)
                    {
                        continue;
                    }

                    ApplyPersonality(location, sprinkler.TileLocation, tiles);
                }
            }
        }

        private void ApplyPersonality(GameLocation location, Vector2 sprinklerTile, IList<Vector2> coverage)
        {
            if (_random.NextDouble() < Config.SkipWaterChance)
            {
                SkipCoverage(location, coverage);
            }

            if (_random.NextDouble() < Config.ExtraWaterChance)
            {
                WaterExtraTiles(location, sprinklerTile, coverage);
            }

            if (_random.NextDouble() < Config.FertilizerChance)
            {
                ApplyFertilizer(location, coverage);
            }

            if (_random.NextDouble() < Config.OverflowChance)
            {
                OverflowWater(location, sprinklerTile, coverage);
            }

            if (_random.NextDouble() < Config.GrowthSpurtChance)
            {
                GiveGrowthSpurts(location, coverage);
            }
        }

        private void SkipCoverage(GameLocation location, IList<Vector2> coverage)
        {
            var skipCount = Math.Min(Config.MaxSkippedTiles, coverage.Count);
            if (skipCount <= 0)
            {
                return;
            }

            var shuffled = coverage.OrderBy(_ => _random.NextDouble()).Take(skipCount);
            foreach (var tile in shuffled)
            {
                if (location.terrainFeatures.TryGetValue(tile, out var feature) && feature is HoeDirt dirt)
                {
                    dirt.state.Value = HoeDirt.dry;
                    LogDebug($"Sprinkler skipped watering tile {tile} in {location.NameOrUniqueName}.");
                }
            }
        }

        private void WaterExtraTiles(GameLocation location, Vector2 sprinklerTile, IList<Vector2> coverage)
        {
            if (Config.ExtraWaterTiles <= 0)
            {
                return;
            }

            var candidateTiles = GetTilesInRadius(sprinklerTile, Config.ExtraWaterRadius)
                .Where(tile => !coverage.Contains(tile))
                .ToList();

            if (candidateTiles.Count == 0)
            {
                return;
            }

            var selected = candidateTiles.OrderBy(_ => _random.NextDouble()).Take(Config.ExtraWaterTiles);
            foreach (var tile in selected)
            {
                if (TryWaterTile(location, tile))
                {
                    LogDebug($"Sprinkler watered extra tile {tile} in {location.NameOrUniqueName}.");
                }
            }
        }

        private void ApplyFertilizer(GameLocation location, IList<Vector2> coverage)
        {
            var candidates = coverage
                .Where(tile => location.terrainFeatures.TryGetValue(tile, out var feature) && feature is HoeDirt dirt && dirt.fertilizer.Value == HoeDirt.noFertilizer)
                .ToList();

            if (candidates.Count == 0)
            {
                return;
            }

            var chosen = candidates[_random.Next(candidates.Count)];
            if (location.terrainFeatures.TryGetValue(chosen, out var feature) && feature is HoeDirt dirt)
            {
                dirt.fertilizer.Value = Config.FertilizerItemId;
                LogDebug($"Sprinkler handed out fertilizer on tile {chosen} in {location.NameOrUniqueName}.");
            }
        }

        private void OverflowWater(GameLocation location, Vector2 sprinklerTile, IList<Vector2> coverage)
        {
            if (Config.OverflowTiles <= 0)
            {
                return;
            }

            var candidateTiles = GetTilesInRadius(sprinklerTile, Config.OverflowRadius)
                .Where(tile => !coverage.Contains(tile))
                .ToList();

            if (candidateTiles.Count == 0)
            {
                return;
            }

            var selected = candidateTiles.OrderBy(_ => _random.NextDouble()).Take(Config.OverflowTiles);
            foreach (var tile in selected)
            {
                if (TryWaterTile(location, tile))
                {
                    LogDebug($"Sprinkler overflowed onto tile {tile} in {location.NameOrUniqueName}.");
                }
            }
        }

        private void GiveGrowthSpurts(GameLocation location, IList<Vector2> coverage)
        {
            if (Config.GrowthSpurtTiles <= 0)
            {
                return;
            }

            var candidates = coverage
                .Where(tile => location.terrainFeatures.TryGetValue(tile, out var feature) && feature is HoeDirt dirt && dirt.crop is { } crop && !crop.readyForHarvest())
                .ToList();

            if (candidates.Count == 0)
            {
                return;
            }

            var selected = candidates.OrderBy(_ => _random.NextDouble()).Take(Config.GrowthSpurtTiles);
            foreach (var tile in selected)
            {
                if (!location.terrainFeatures.TryGetValue(tile, out var feature) || feature is not HoeDirt dirt || dirt.crop is not { } crop || crop.readyForHarvest())
                {
                    continue;
                }

                if (crop.dayOfCurrentPhase.Value > 0)
                {
                    crop.dayOfCurrentPhase.Value = Math.Max(0, crop.dayOfCurrentPhase.Value - 1);
                }
                else if (crop.currentPhase.Value > 0 && crop.phaseDays.Count > 0)
                {
                    crop.currentPhase.Value = Math.Max(0, crop.currentPhase.Value - 1);
                    var newPhaseDays = crop.phaseDays[crop.currentPhase.Value];
                    crop.dayOfCurrentPhase.Value = Math.Max(0, newPhaseDays - 1);
                }

                LogDebug($"Sprinkler gave a growth spurt on tile {tile} in {location.NameOrUniqueName}.");
            }
        }

        private IEnumerable<Vector2> GetTilesInRadius(Vector2 center, int radius)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    if (dx == 0 && dy == 0)
                    {
                        continue;
                    }

                    yield return new Vector2(center.X + dx, center.Y + dy);
                }
            }
        }

        private bool TryWaterTile(GameLocation location, Vector2 tile)
        {
            if (location.terrainFeatures.TryGetValue(tile, out var feature) && feature is HoeDirt dirt)
            {
                dirt.state.Value = HoeDirt.watered;
                return true;
            }

            return false;
        }

        private void LogDebug(string message)
        {
            if (Config.DebugLogging)
            {
                Monitor.Log(message, LogLevel.Trace);
            }
        }

        private Random CreateDailyRandom()
        {
            // Base the seed on stable game values so the same day rolls the same personalities regardless of machine clock.
            var seed = (int)((Game1.uniqueIDForThisGame + Game1.stats.DaysPlayed + Game1.dayOfMonth + (Game1.year * 17)) % int.MaxValue);
            return new Random(seed);
        }
    }
}
