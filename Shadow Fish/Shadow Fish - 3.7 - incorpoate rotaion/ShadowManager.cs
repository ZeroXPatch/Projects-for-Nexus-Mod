using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace ShadowsOfTheDeep
{
    public class ShadowManager
    {
        private readonly IModHelper _helper;
        private readonly List<ShadowBoid> _shadows = new();
        private List<string> _possibleFishIds = new();
        private GameLocation? _currentLocation;
        private readonly Random _random = new();
        private int _currentSessionCap = 50;

        public ShadowManager(IModHelper helper)
        {
            _helper = helper;
        }

        public void ChangeLocation(GameLocation location)
        {
            _shadows.Clear();
            _currentLocation = location;
            _possibleFishIds.Clear();

            if (location == null) return;
            if (ModEntry.Config.FarmOnly && !location.IsFarm) return;
            if (ModEntry.Config.ExcludedLocations.Contains(location.Name)) return;
            if (IsPastCurfew()) return;

            int min = Math.Min(ModEntry.Config.MinFishCount, ModEntry.Config.MaxFishCount);
            int max = Math.Max(ModEntry.Config.MinFishCount, ModEntry.Config.MaxFishCount);
            int userDesiredCap = _random.Next(min, max + 1);

            int waterTiles = CountWaterTiles(location);
            int densityCap = (int)(waterTiles * ModEntry.Config.SpawnChance * ModEntry.Config.DensityCapMultiplier);
            if (waterTiles > 0) densityCap = Math.Max(densityCap, 5);

            _currentSessionCap = Math.Min(userDesiredCap, densityCap);

            PopulateFishCache(location);

            if (_possibleFishIds.Any() && _currentSessionCap > 0)
            {
                int initialSpawn = (int)(_currentSessionCap * ModEntry.Config.InitialSpawnChance);
                if (initialSpawn < 1 && ModEntry.Config.InitialSpawnChance > 0) initialSpawn = 1;
                initialSpawn = Math.Min(initialSpawn, _currentSessionCap);

                for (int i = 0; i < initialSpawn; i++)
                {
                    TrySpawnFish(forceRandomMapPosition: true, retryCount: 10);
                }
            }
        }

        private int CountWaterTiles(GameLocation location)
        {
            int count = 0;
            int width = location.Map.Layers[0].LayerWidth;
            int height = location.Map.Layers[0].LayerHeight;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (IsTileWater(x, y)) count++;
                }
            }
            return count;
        }

        private bool IsPastCurfew()
        {
            if (!ModEntry.Config.HideFishAtNight) return false;
            if (_currentLocation == null) return false;
            int sunset = Game1.getStartingToGetDarkTime(_currentLocation);
            int cutoff = sunset + (ModEntry.Config.HoursAfterSunset * 100);
            return Game1.timeOfDay >= cutoff;
        }

        private void PopulateFishCache(GameLocation location)
        {
            var locData = location.GetData();
            if (locData?.Fish == null) return;
            var allData = DataLoader.Fish(Game1.content);
            foreach (var spawnData in locData.Fish)
            {
                string? itemId = spawnData.ItemId;
                if (string.IsNullOrEmpty(itemId)) continue;
                string qualId = itemId.StartsWith("(O)") ? itemId : "(O)" + itemId;
                string rawId = qualId.Replace("(O)", "");
                if (allData.ContainsKey(rawId)) _possibleFishIds.Add(qualId);
            }
            _possibleFishIds = _possibleFishIds.Distinct().ToList();
        }

        public void Update(UpdateTickedEventArgs e)
        {
            if (_currentLocation == null || _possibleFishIds.Count == 0) return;

            bool isNight = IsPastCurfew();

            for (int i = _shadows.Count - 1; i >= 0; i--)
            {
                if (isNight) _shadows[i].StartDespawn();

                // Pass _shadows for separation logic
                _shadows[i].Update(Game1.currentGameTime, _shadows);

                if (_shadows[i].IsDead)
                {
                    _shadows[i].Cleanup();
                    _shadows.RemoveAt(i);
                }
            }

            if (!isNight)
            {
                bool aggressive = _shadows.Count < 5;
                uint tickRate = aggressive ? 10u : 60u;

                if (e.IsMultipleOf(tickRate) && _shadows.Count < _currentSessionCap)
                {
                    if (_random.NextDouble() < ModEntry.Config.SpawnChance)
                        TrySpawnFish(forceRandomMapPosition: false, retryCount: 3);
                }
            }
        }

        private void TrySpawnFish(bool forceRandomMapPosition, int retryCount)
        {
            if (_currentLocation == null || _possibleFishIds.Count == 0) return;

            for (int i = 0; i < retryCount; i++)
            {
                int x, y;
                if (forceRandomMapPosition)
                {
                    x = _random.Next(0, _currentLocation.Map.Layers[0].LayerWidth);
                    y = _random.Next(0, _currentLocation.Map.Layers[0].LayerHeight);
                }
                else
                {
                    var vp = Game1.viewport;
                    int buffer = 4;
                    int rangeX = (vp.Width / 64) + (buffer * 2);
                    int rangeY = (vp.Height / 64) + (buffer * 2);
                    x = (vp.X / 64) - buffer + _random.Next(0, rangeX);
                    y = (vp.Y / 64) - buffer + _random.Next(0, rangeY);
                }

                Vector2 candidatePos = new Vector2(x * 64f + 32f, y * 64f + 32f);

                if (IsSafeSpawn(x, y))
                {
                    string randomFishId = _possibleFishIds[_random.Next(_possibleFishIds.Count)];
                    _shadows.Add(new ShadowBoid(candidatePos, randomFishId, _currentLocation));
                    return;
                }
            }
        }

        private bool IsTileWater(int x, int y)
        {
            if (_currentLocation == null) return false;
            if (_currentLocation.doesTileHaveProperty(x, y, "Water", "Back") == null) return false;
            if (_currentLocation.getTileIndexAt(x, y, "Buildings") != -1)
            {
                if (_currentLocation.doesTileHaveProperty(x, y, "Passable", "Buildings") == null)
                    return false;
            }
            return true;
        }

        private bool IsSafeSpawn(int tileX, int tileY)
        {
            if (!IsTileWater(tileX, tileY)) return false;
            if (!IsTileWater(tileX + 1, tileY) || !IsTileWater(tileX - 1, tileY) ||
                !IsTileWater(tileX, tileY + 1) || !IsTileWater(tileX, tileY - 1))
                return false;
            if (!IsTileWater(tileX + 1, tileY + 1) || !IsTileWater(tileX - 1, tileY - 1) ||
                !IsTileWater(tileX + 1, tileY - 1) || !IsTileWater(tileX - 1, tileY + 1))
                return false;
            return true;
        }

        public void Draw(SpriteBatch b)
        {
            if (_currentLocation == null) return;
            var viewport = Game1.viewport;
            foreach (var shadow in _shadows)
            {
                if (!shadow.IsVisible) continue;
                if (shadow.Position.X + 128 < viewport.X || shadow.Position.X - 128 > viewport.X + viewport.Width ||
                    shadow.Position.Y + 128 < viewport.Y || shadow.Position.Y - 128 > viewport.Y + viewport.Height)
                    continue;
                shadow.Draw(b);
            }
        }
    }
}