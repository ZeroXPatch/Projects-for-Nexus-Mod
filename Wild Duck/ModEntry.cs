using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace WildSwimmingDucks
{
    public class ModEntry : Mod
    {
        private ModConfig Config = new();

        // The list of active ducks
        private List<WildDuck> ActiveDucks = new();

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.TimeChanged += OnTimeChanged;

            // New Events for updating and drawing the custom class
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.Display.RenderedWorld += OnRenderedWorld;
            helper.Events.Player.Warped += OnPlayerWarped;
        }

        // --- Event Handlers ---

        private void OnPlayerWarped(object? sender, WarpedEventArgs e)
        {
            // Clear ducks when changing maps so they don't carry over
            ActiveDucks.Clear();

            // Immediately try to spawn new ones for the new map
            TrySpawnDucks();
        }

        private void OnTimeChanged(object? sender, TimeChangedEventArgs e)
        {
            // Periodically add more ducks or refresh them
            TrySpawnDucks();
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || ActiveDucks.Count == 0) return;

            // Update every duck
            // We use a backwards loop in case we need to remove one (not implemented here, but good practice)
            for (int i = ActiveDucks.Count - 1; i >= 0; i--)
            {
                ActiveDucks[i].Update(Game1.currentLocation);
            }
        }

        private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
        {
            if (!Context.IsWorldReady || ActiveDucks.Count == 0) return;

            // Draw every duck
            foreach (var duck in ActiveDucks)
            {
                duck.Draw(e.SpriteBatch);
            }
        }

        // --- Spawning Logic ---

        private void TrySpawnDucks()
        {
            if (Game1.currentLocation == null || !Game1.currentLocation.IsOutdoors)
                return;

            // Don't overpopulate (limit per map)
            if (ActiveDucks.Count >= this.Config.MaxDucks) return;

            if (Game1.random.Next(1, 101) > this.Config.SpawnChancePercent) return;

            // Filter Locations (Ocean, River, Lake) - Same as previous code
            string locName = Game1.currentLocation.Name;
            bool isBeach = locName.Contains("Beach");
            bool isRiver = locName.Contains("River") || locName.Equals("Town");
            bool isLake = locName.Contains("Mountain") || locName.Contains("Forest");

            if (isBeach && !this.Config.EnableInOcean) return;
            if (isRiver && !this.Config.EnableInRiver) return;
            if (isLake && !this.Config.EnableInLake) return;

            // Determine how many to add this hour
            int countToAdd = Game1.random.Next(1, 3);

            SpawnDucks(countToAdd, Game1.currentLocation);
        }

        private void SpawnDucks(int count, GameLocation location)
        {
            int attempts = 0;
            int spawned = 0;

            while (spawned < count && attempts < 20)
            {
                attempts++;
                int x = Game1.random.Next(0, location.Map.Layers[0].LayerWidth);
                int y = Game1.random.Next(0, location.Map.Layers[0].LayerHeight);

                // Use 'isOpenWater' to ensure they aren't stuck in walls
                if (location.isWaterTile(x, y) && location.isOpenWater(x, y))
                {
                    ActiveDucks.Add(new WildDuck(new Vector2(x * 64, y * 64)));
                    spawned++;
                }
            }
        }

        // --- Config Menu Setup (Same as before) ---
        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            // Copy the GMCM code from the previous step here
            // It remains exactly the same.
        }
    }
}