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

            PopulateFishCache(location);

            if (_possibleFishIds.Any())
            {
                int tilesToCheck = location.Map.Layers[0].LayerWidth * location.Map.Layers[0].LayerHeight;
                int initialSpawnCount = (int)Math.Min(ModEntry.Config.MaxFishCount, tilesToCheck * ModEntry.Config.SpawnChance * 0.1f);

                for (int i = 0; i < initialSpawnCount; i++)
                {
                    TrySpawnFish(forceRandomMapPosition: true);
                }
            }
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

                if (allData.ContainsKey(rawId))
                {
                    _possibleFishIds.Add(qualId);
                }
            }
            _possibleFishIds = _possibleFishIds.Distinct().ToList();
        }

        public void Update(UpdateTickedEventArgs e)
        {
            if (_currentLocation == null || _possibleFishIds.Count == 0) return;

            bool isNight = IsPastCurfew();

            for (int i = _shadows.Count - 1; i >= 0; i--)
            {
                if (isNight)
                {
                    _shadows[i].StartDespawn();
                }

                _shadows[i].Update(Game1.currentGameTime);

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

                if (e.IsMultipleOf(tickRate) && _shadows.Count < ModEntry.Config.MaxFishCount)
                {
                    if (_random.NextDouble() < ModEntry.Config.SpawnChance)
                    {
                        TrySpawnFish(forceRandomMapPosition: false);
                    }
                }
            }
        }

        private void TrySpawnFish(bool forceRandomMapPosition)
        {
            if (_currentLocation == null || _possibleFishIds.Count == 0) return;

            for (int i = 0; i < 3; i++)
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

                // CHECK: Only spawn if the specific tile is water
                if (_currentLocation.isOpenWater(x, y))
                {
                    string randomFishId = _possibleFishIds[_random.Next(_possibleFishIds.Count)];

                    // FIX: Spawn at Center of Tile (+32f, +32f) 
                    // Previously: (x * 64, y * 64) put it at top-left corner, causing land clipping.
                    Vector2 spawnPos = new Vector2(x * 64f + 32f, y * 64f + 32f);

                    _shadows.Add(new ShadowBoid(spawnPos, randomFishId, _currentLocation));
                    return;
                }
            }
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