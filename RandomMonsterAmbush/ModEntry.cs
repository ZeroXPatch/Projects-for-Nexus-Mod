using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Monsters;

namespace RandomMonsterAmbush
{
    /// <summary>
    /// Entry point for the Random Monster Ambush mod. Periodically spawns a random monster near
    /// the player based on configurable rules to add surprise encounters to any location.
    /// </summary>
    public class ModEntry : Mod
    {
        private readonly Random _random = new();

        private readonly List<Func<Vector2, Monster>> _monsterFactories = new()
        {
            tile => new GreenSlime(tile * Game1.tileSize),
            tile => new Bat(tile * Game1.tileSize),
            tile => new DustSpirit(tile * Game1.tileSize),
            tile => new RockCrab(tile * Game1.tileSize),
            tile => new Skeleton(tile * Game1.tileSize),
            tile => new ShadowBrute(tile * Game1.tileSize)
        };

        private ModConfig _config = null!;

        public override void Entry(IModHelper helper)
        {
            LoadConfig();

            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        }

        /// <summary>
        /// Reload configuration each morning so player edits are respected without restarting the game.
        /// </summary>
        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            LoadConfig();
        }

        /// <summary>
        /// Perform the periodic spawn check.
        /// </summary>
        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || !_config.EnableMod)
            {
                return;
            }

            if (!e.IsMultipleOf(_config.CheckIntervalTicks))
            {
                return;
            }

            if (_config.PreventDuringEvents && (Game1.eventUp || Game1.isFestival()))
            {
                return;
            }

            if (!_config.AllowDaytimeSpawns && Game1.timeOfDay < 1800)
            {
                return;
            }

            GameLocation location = Game1.player.currentLocation;

            if (IsLocationBlocked(location))
            {
                if (_config.ShowHudMessage)
                {
                    Game1.addHUDMessage(new HUDMessage(Helper.Translation.Get("hud.blocked")));
                }

                return;
            }

            if (_random.NextDouble() > _config.SpawnChance)
            {
                return;
            }

            int spawned = SpawnMonstersAroundPlayer(location);
            if (spawned > 0 && _config.ShowHudMessage)
            {
                string message = Helper.Translation.Get("hud.spawned", new
                {
                    count = spawned,
                    location = location.DisplayName ?? location.Name
                });

                Game1.addHUDMessage(new HUDMessage(message));
            }
        }

        private int SpawnMonstersAroundPlayer(GameLocation location)
        {
            int spawnCount = _random.Next(1, _config.MaxMonstersPerSpawn + 1);
            int spawned = 0;

            for (int i = 0; i < spawnCount; i++)
            {
                if (!TryFindSpawnTile(location, out Vector2 tile))
                {
                    continue;
                }

                Monster monster = CreateRandomMonster(tile);
                monster.currentLocation = location;
                location.characters.Add(monster);
                spawned++;
            }

            return spawned;
        }

        private bool TryFindSpawnTile(GameLocation location, out Vector2 tile)
        {
            Vector2 origin = Game1.player.getTileLocation();
            int minDistance = Math.Max(1, _config.MinSpawnDistance);
            int maxDistance = Math.Max(minDistance, _config.MaxSpawnDistance);

            for (int attempt = 0; attempt < 30; attempt++)
            {
                int dx = _random.Next(-maxDistance, maxDistance + 1);
                int dy = _random.Next(-maxDistance, maxDistance + 1);

                if (Math.Abs(dx) < minDistance && Math.Abs(dy) < minDistance)
                {
                    continue;
                }

                tile = origin + new Vector2(dx, dy);

                if (IsValidSpawnTile(location, tile))
                {
                    return true;
                }
            }

            tile = Vector2.Zero;
            return false;
        }

        private bool IsValidSpawnTile(GameLocation location, Vector2 tile)
        {
            if (!location.isTileOnMap(tile))
            {
                return false;
            }

            if (location.isTileOccupiedForPlacement(tile))
            {
                return false;
            }

            if (!location.isTileLocationTotallyClearAndPlaceable(tile))
            {
                return false;
            }

            string? noSpawn = location.doesTileHaveProperty((int)tile.X, (int)tile.Y, "NoSpawn", "Back");
            return string.IsNullOrWhiteSpace(noSpawn);
        }

        private Monster CreateRandomMonster(Vector2 tile)
        {
            Func<Vector2, Monster> factory = _monsterFactories[_random.Next(_monsterFactories.Count)];
            return factory(tile);
        }

        private bool IsLocationBlocked(GameLocation location)
        {
            if (_config.DisallowedLocations.Any(name => string.Equals(name, location.NameOrUniqueName, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return location.IsFarmHouse;
        }

        private void LoadConfig()
        {
            _config = Helper.ReadConfig<ModConfig>();

            bool changed = false;

            if (_config.SpawnChance < 0 || _config.SpawnChance > 1)
            {
                _config.SpawnChance = Math.Clamp(_config.SpawnChance, 0.01, 1.0);
                changed = true;
            }

            if (_config.CheckIntervalTicks < 30)
            {
                _config.CheckIntervalTicks = 30;
                changed = true;
            }

            if (_config.MinSpawnDistance < 1)
            {
                _config.MinSpawnDistance = 1;
                changed = true;
            }

            if (_config.MaxSpawnDistance < _config.MinSpawnDistance)
            {
                _config.MaxSpawnDistance = _config.MinSpawnDistance + 2;
                changed = true;
            }

            if (_config.MaxMonstersPerSpawn < 1)
            {
                _config.MaxMonstersPerSpawn = 1;
                changed = true;
            }

            if (_config.DisallowedLocations == null)
            {
                _config.DisallowedLocations = new List<string>();
                changed = true;
            }

            if (changed)
            {
                Helper.WriteConfig(_config);
            }
        }
    }
}
