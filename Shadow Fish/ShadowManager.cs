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

            // 1. Validation Logic
            if (ModEntry.Config.FarmOnly && !location.IsFarm) return;
            if (ModEntry.Config.ExcludedLocations.Contains(location.Name)) return;

            // 2. Data Caching
            PopulateFishCache(location);

            // 3. Fast-Forward Spawning (Fill the map immediately)
            if (_possibleFishIds.Any())
            {
                int tilesToCheck = location.Map.Layers[0].LayerWidth * location.Map.Layers[0].LayerHeight;
                // Calculate safe limit based on map size vs config cap
                int initialSpawnCount = (int)Math.Min(ModEntry.Config.MaxFishCount, tilesToCheck * ModEntry.Config.SpawnChance * 0.1f);

                for (int i = 0; i < initialSpawnCount; i++)
                {
                    TrySpawnFish(forceRandomMapPosition: true);
                }
            }
        }

        private void PopulateFishCache(GameLocation location)
        {
            var locData = location.GetData();
            if (locData?.Fish == null) return;

            var allData = DataLoader.Fish(Game1.content);

            foreach (var spawnData in locData.Fish)
            {
                string? itemId = spawnData.ItemId;
                // Basic checks: must be defined
                if (string.IsNullOrEmpty(itemId)) continue;

                // Normalize Item ID (Stardew 1.6 logic)
                string qualId = itemId.StartsWith("(O)") ? itemId : "(O)" + itemId;
                string rawId = qualId.Replace("(O)", "");

                // Ensure it's actually a fish (exists in Data/Fish) to avoid spawning furniture/trash
                if (allData.ContainsKey(rawId))
                {
                    _possibleFishIds.Add(qualId);
                }
            }

            // Remove duplicates to ensure even spawn rates
            _possibleFishIds = _possibleFishIds.Distinct().ToList();
        }

        public void Update(UpdateTickedEventArgs e)
        {
            if (_currentLocation == null || _possibleFishIds.Count == 0) return;

            // Update Loop (Reverse for safe removal)
            for (int i = _shadows.Count - 1; i >= 0; i--)
            {
                _shadows[i].Update(Game1.currentGameTime);
                if (_shadows[i].ShouldDespawn)
                {
                    _shadows.RemoveAt(i);
                }
            }

            // Spawn Logic Throttling
            // If very few fish, spawn faster to fill up. If near cap, spawn slower.
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

        private void TrySpawnFish(bool forceRandomMapPosition)
        {
            if (_currentLocation == null || _possibleFishIds.Count == 0) return;

            // Attempt to find a valid water tile 3 times
            for (int i = 0; i < 3; i++)
            {
                int x, y;

                if (forceRandomMapPosition)
                {
                    // Global Map Spawn
                    x = _random.Next(0, _currentLocation.Map.Layers[0].LayerWidth);
                    y = _random.Next(0, _currentLocation.Map.Layers[0].LayerHeight);
                }
                else
                {
                    // Viewport Spawn (Optimization: Only spawn near player)
                    var vp = Game1.viewport;
                    // Add buffer so they spawn just off-screen
                    int buffer = 4;
                    int rangeX = (vp.Width / 64) + (buffer * 2);
                    int rangeY = (vp.Height / 64) + (buffer * 2);

                    x = (vp.X / 64) - buffer + _random.Next(0, rangeX);
                    y = (vp.Y / 64) - buffer + _random.Next(0, rangeY);
                }

                if (_currentLocation.isOpenWater(x, y))
                {
                    string randomFishId = _possibleFishIds[_random.Next(_possibleFishIds.Count)];
                    _shadows.Add(new ShadowBoid(new Vector2(x * 64, y * 64), randomFishId, _currentLocation));
                    return; // Spawn successful
                }
            }
        }

        public void Draw(SpriteBatch b)
        {
            if (_currentLocation == null) return;

            var viewport = Game1.viewport;

            // Iterate and Draw
            foreach (var shadow in _shadows)
            {
                // Culling: Efficiency Check
                // If the shadow is well outside the camera, don't ask GPU to draw it
                if (shadow.Position.X + 128 < viewport.X || shadow.Position.X - 128 > viewport.X + viewport.Width ||
                    shadow.Position.Y + 128 < viewport.Y || shadow.Position.Y - 128 > viewport.Y + viewport.Height)
                    continue;

                shadow.Draw(b);
            }
        }
    }
}