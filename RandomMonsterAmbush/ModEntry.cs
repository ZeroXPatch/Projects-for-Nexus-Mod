using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Monsters;
using StardewValley.Tools;

namespace RandomMonsterAmbush
{
    /// <summary>
    /// Periodically spawns random monsters near the player using configurable rules.
    /// No HUD messages, just surprise ambushes.
    /// Ambush monsters are automatically removed at the start of the next in-game day.
    /// </summary>
    public class ModEntry : Mod
    {
        private readonly Random _random = new();

        private const string AmbushSpawnDayKey = "RandomMonsterAmbush/SpawnDay";

        /// <summary>Regular ambush monsters (only ones with simple Vector2 constructors).</summary>
        private readonly List<Func<Vector2, Monster>> _monsterFactories = new()
        {
            tile => new GreenSlime(tile * Game1.tileSize),
            tile => new DustSpirit(tile * Game1.tileSize),
            tile => new Bat(tile * Game1.tileSize),
            tile => new RockCrab(tile * Game1.tileSize),
            tile => new Ghost(tile * Game1.tileSize),
            tile => new Skeleton(tile * Game1.tileSize),
            tile => new SquidKid(tile * Game1.tileSize),
            tile => new ShadowBrute(tile * Game1.tileSize),
            tile => new ShadowShaman(tile * Game1.tileSize),
            tile => new Serpent(tile * Game1.tileSize),
        };

        /// <summary>Boss-like monsters; one of these can be chosen per ambush and then buffed.</summary>
        private readonly List<Func<Vector2, Monster>> _bossFactories = new()
        {
            tile => new ShadowBrute(tile * Game1.tileSize),
            tile => new ShadowShaman(tile * Game1.tileSize),
            tile => new Serpent(tile * Game1.tileSize),
            tile => new SquidKid(tile * Game1.tileSize),
            tile => new Ghost(tile * Game1.tileSize)
        };

        private ModConfig _config = null!;

        /*********
        ** Entry
        *********/
        public override void Entry(IModHelper helper)
        {
            LoadConfig();

            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        }

        /*********
        ** Events
        *********/
        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            // reload config each morning so edits to config.json are picked up
            LoadConfig();

            if (!Context.IsWorldReady)
                return;

            // remove any ambush monsters from previous days
            CleanupOldAmbushMonsters();
        }

        /// <summary>
        /// Register config UI with Generic Mod Config Menu (if installed).
        /// </summary>
        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            // register mod
            configMenu.Register(
                mod: this.ModManifest,
                reset: () =>
                {
                    _config = new ModConfig();
                    Helper.WriteConfig(_config);
                },
                save: () => Helper.WriteConfig(_config),
                titleScreenOnly: false
            );

            var t = Helper.Translation;

            // ==== GENERAL SECTION ====
            configMenu.AddSectionTitle(
                mod: this.ModManifest,
                text: () => t.Get("config.section.general")
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => _config.EnableMod,
                setValue: value => _config.EnableMod = value,
                name: () => t.Get("config.enableMod.name"),
                tooltip: () => t.Get("config.enableMod.tooltip")
            );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => _config.CheckIntervalTicks,
                setValue: value => _config.CheckIntervalTicks = value,
                name: () => t.Get("config.checkInterval.name"),
                tooltip: () => t.Get("config.checkInterval.tooltip"),
                min: 30,
                max: 3600,
                interval: 30
            );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => _config.MaxMonstersPerSpawn,
                setValue: value => _config.MaxMonstersPerSpawn = value,
                name: () => t.Get("config.maxMonsters.name"),
                tooltip: () => t.Get("config.maxMonsters.tooltip"),
                min: 1,
                max: 10,
                interval: 1
            );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => _config.MinSpawnDistance,
                setValue: value => _config.MinSpawnDistance = value,
                name: () => t.Get("config.minDistance.name"),
                tooltip: () => t.Get("config.minDistance.tooltip"),
                min: 1,
                max: 15,
                interval: 1
            );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => _config.MaxSpawnDistance,
                setValue: value => _config.MaxSpawnDistance = value,
                name: () => t.Get("config.maxDistance.name"),
                tooltip: () => t.Get("config.maxDistance.tooltip"),
                min: 1,
                max: 25,
                interval: 1
            );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => (float)_config.SpawnChance,
                setValue: value => _config.SpawnChance = value,
                name: () => t.Get("config.spawnChance.name"),
                tooltip: () => t.Get("config.spawnChance.tooltip"),
                min: 0f,
                max: 1f,
                interval: 0.01f,
                formatValue: v => $"{v:0.00}"
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => _config.AllowDaytimeSpawns,
                setValue: value => _config.AllowDaytimeSpawns = value,
                name: () => t.Get("config.allowDaytime.name"),
                tooltip: () => t.Get("config.allowDaytime.tooltip")
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => _config.PreventDuringEvents,
                setValue: value => _config.PreventDuringEvents = value,
                name: () => t.Get("config.skipEvents.name"),
                tooltip: () => t.Get("config.skipEvents.tooltip")
            );

            // NEW: skip ambushes while holding any fishing rod
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => _config.SkipWhileHoldingFishingRod,
                setValue: value => _config.SkipWhileHoldingFishingRod = value,
                name: () => t.Get("config.skipFishingRod.name"),
                tooltip: () => t.Get("config.skipFishingRod.tooltip")
            );

            // ==== BOSS SECTION ====
            configMenu.AddSectionTitle(
                mod: this.ModManifest,
                text: () => t.Get("config.section.boss")
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => _config.EnableBossSpawns,
                setValue: value => _config.EnableBossSpawns = value,
                name: () => t.Get("config.enableBoss.name"),
                tooltip: () => t.Get("config.enableBoss.tooltip")
            );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => (float)_config.BossSpawnChance,
                setValue: value => _config.BossSpawnChance = value,
                name: () => t.Get("config.bossChance.name"),
                tooltip: () => t.Get("config.bossChance.tooltip"),
                min: 0f,
                max: 1f,
                interval: 0.01f,
                formatValue: v => $"{v:0.00}"
            );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => _config.BossHealthMultiplier,
                setValue: value => _config.BossHealthMultiplier = value,
                name: () => t.Get("config.bossHealthMult.name"),
                tooltip: () => t.Get("config.bossHealthMult.tooltip"),
                min: 1f,
                max: 10f,
                interval: 0.5f,
                formatValue: v => $"{v:0.0}×"
            );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => _config.BossDamageMultiplier,
                setValue: value => _config.BossDamageMultiplier = value,
                name: () => t.Get("config.bossDamageMult.name"),
                tooltip: () => t.Get("config.bossDamageMult.tooltip"),
                min: 1f,
                max: 10f,
                interval: 0.5f,
                formatValue: v => $"{v:0.0}×"
            );
        }

        /// <summary>Periodic spawn check.</summary>
        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || !_config.EnableMod)
                return;

            // SMAPI 4: IsMultipleOf takes uint
            if (!e.IsMultipleOf((uint)_config.CheckIntervalTicks))
                return;

            // NEW: if holding any fishing rod, don't spawn
            if (_config.SkipWhileHoldingFishingRod && Game1.player.CurrentTool is FishingRod)
                return;

            // don't spawn during events / festivals / cutscenes / minigames
            if (_config.PreventDuringEvents)
            {
                if (Game1.eventUp || Game1.isFestival())
                    return;

                if (Game1.currentLocation?.currentEvent != null)
                    return;

                if (Game1.currentMinigame != null)
                    return;
            }

            if (!_config.AllowDaytimeSpawns && Game1.timeOfDay < 1800)
                return;

            GameLocation location = Game1.player.currentLocation;

            if (IsLocationBlocked(location))
                return;

            if (_random.NextDouble() > _config.SpawnChance)
                return;

            _ = SpawnMonstersAroundPlayer(location);
        }

        /*********
        ** Logic
        *********/
        /// <summary>Spawn a pack of monsters around the player, with a chance to include exactly one boss.</summary>
        private int SpawnMonstersAroundPlayer(GameLocation location)
        {
            int spawnCount = _random.Next(1, _config.MaxMonstersPerSpawn + 1);
            int spawned = 0;

            bool spawnBossThisAmbush =
                _config.EnableBossSpawns &&
                _bossFactories.Count > 0 &&
                _random.NextDouble() < _config.BossSpawnChance;

            // choose which index (0..spawnCount-1) will be boss, if any
            int bossIndex = spawnBossThisAmbush ? _random.Next(spawnCount) : -1;

            for (int i = 0; i < spawnCount; i++)
            {
                if (!TryFindSpawnTile(location, out Vector2 tile))
                    continue;

                Monster monster = (spawnBossThisAmbush && i == bossIndex)
                    ? CreateRandomBoss(tile)
                    : CreateRandomMonster(tile);

                // tag this as an ambush monster with its spawn day
                monster.modData[AmbushSpawnDayKey] = Game1.Date.TotalDays.ToString(CultureInfo.InvariantCulture);

                monster.currentLocation = location;
                location.characters.Add(monster);
                spawned++;
            }

            return spawned;
        }

        private bool TryFindSpawnTile(GameLocation location, out Vector2 tile)
        {
            // 1.6: use Tile property instead of getTileLocation()
            Vector2 origin = Game1.player.Tile;

            int minDistance = Math.Max(1, _config.MinSpawnDistance);
            int maxDistance = Math.Max(minDistance, _config.MaxSpawnDistance);

            for (int attempt = 0; attempt < 30; attempt++)
            {
                int dx = _random.Next(-maxDistance, maxDistance + 1);
                int dy = _random.Next(-maxDistance, maxDistance + 1);

                if (Math.Abs(dx) < minDistance && Math.Abs(dy) < minDistance)
                    continue;

                tile = origin + new Vector2(dx, dy);

                if (IsValidSpawnTile(location, tile))
                    return true;
            }

            tile = Vector2.Zero;
            return false;
        }

        /// <summary>Check if a tile is suitable for spawning a monster in 1.6.</summary>
        private static bool IsValidSpawnTile(GameLocation location, Vector2 tile)
        {
            if (!location.isTileOnMap(tile))
                return false;

            // 1.6 helper: handles walkability + collision + blocking objects
            if (!location.CanSpawnCharacterHere(tile))
                return false;

            string? noSpawn = location.doesTileHaveProperty((int)tile.X, (int)tile.Y, "NoSpawn", "Back");
            return string.IsNullOrWhiteSpace(noSpawn);
        }

        private Monster CreateRandomMonster(Vector2 tile)
        {
            Func<Vector2, Monster> factory = _monsterFactories[_random.Next(_monsterFactories.Count)];
            return factory(tile);
        }

        /// <summary>Create a boss monster by picking from the boss pool and scaling its stats.</summary>
        private Monster CreateRandomBoss(Vector2 tile)
        {
            Func<Vector2, Monster> factory = _bossFactories[_random.Next(_bossFactories.Count)];
            Monster boss = factory(tile);

            boss.MaxHealth = (int)(boss.MaxHealth * _config.BossHealthMultiplier);
            boss.Health = boss.MaxHealth;
            boss.DamageToFarmer = (int)(boss.DamageToFarmer * _config.BossDamageMultiplier);
            boss.Scale *= 1.3f;

            boss.Name = $"Ambush {boss.Name}";
            return boss;
        }

        /// <summary>
        /// Returns true if ambushes should never happen in the given location.
        /// Includes user-configured disallowed locations plus hard-coded safety areas.
        /// </summary>
        private bool IsLocationBlocked(GameLocation location)
        {
            string locName = location.NameOrUniqueName;

            // user-configurable blocked locations
            if (_config.DisallowedLocations.Any(name =>
                    string.Equals(name, locName, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            // player farmhouse
            if (location is FarmHouse)
                return true;

            // Harvey's clinic (where you wake up after dying) and his apartment
            if (string.Equals(locName, "Hospital", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(locName, "HarveyRoom", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Remove any ambush monsters that were spawned on a previous in-game day.
        /// That means they exist for the day they spawn, but are gone on the second day.
        /// </summary>
        private void CleanupOldAmbushMonsters()
        {
            int today = Game1.Date.TotalDays;

            Utility.ForEachLocation(location =>
            {
                var toRemove = location.characters
                    .OfType<Monster>()
                    .Where(monster =>
                        monster.modData.TryGetValue(AmbushSpawnDayKey, out string value) &&
                        int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out int spawnDay) &&
                        spawnDay < today)
                    .ToList();

                foreach (Monster monster in toRemove)
                    location.characters.Remove(monster);

                return true; // continue to next location
            });
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

            if (_config.BossSpawnChance < 0 || _config.BossSpawnChance > 1)
            {
                _config.BossSpawnChance = Math.Clamp(_config.BossSpawnChance, 0.0, 1.0);
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

            if (_config.BossHealthMultiplier < 1f)
            {
                _config.BossHealthMultiplier = 1f;
                changed = true;
            }

            if (_config.BossDamageMultiplier < 1f)
            {
                _config.BossDamageMultiplier = 1f;
                changed = true;
            }

            if (_config.DisallowedLocations == null)
            {
                _config.DisallowedLocations = new List<string>();
                changed = true;
            }

            // ensure hard safety locations are in the list for visibility in config
            void EnsureDisallowed(string name)
            {
                if (!_config.DisallowedLocations.Any(n =>
                        string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
                {
                    _config.DisallowedLocations.Add(name);
                    changed = true;
                }
            }

            EnsureDisallowed("Hospital");
            EnsureDisallowed("HarveyRoom");

            if (changed)
                Helper.WriteConfig(_config);
        }
    }

    /// <summary>Configuration for RandomMonsterAmbush.</summary>
    public class ModConfig
    {
        public bool EnableMod { get; set; } = true;

        /// <summary>How many ticks between spawn checks (60 ticks ≈ 1 second).</summary>
        public int CheckIntervalTicks { get; set; } = 60;

        /// <summary>Maximum monsters per ambush spawn.</summary>
        public int MaxMonstersPerSpawn { get; set; } = 3;

        /// <summary>Minimum tile distance from the player for spawn.</summary>
        public int MinSpawnDistance { get; set; } = 3;

        /// <summary>Maximum tile distance from the player for spawn.</summary>
        public int MaxSpawnDistance { get; set; } = 10;

        /// <summary>Chance that an ambush happens when a spawn check runs (0–1).</summary>
        public double SpawnChance { get; set; } = 0.25;

        /// <summary>Allow ambushes during the day (before 6pm).</summary>
        public bool AllowDaytimeSpawns { get; set; } = false;

        /// <summary>Prevent ambushes during festivals, events, and minigames.</summary>
        public bool PreventDuringEvents { get; set; } = true;

        /// <summary>
        /// If true, ambushes won't spawn while the player is currently holding any fishing rod.
        /// </summary>
        public bool SkipWhileHoldingFishingRod { get; set; } = false;

        /// <summary>Enable special boss-style ambush monsters.</summary>
        public bool EnableBossSpawns { get; set; } = true;

        /// <summary>Chance that an ambush includes a boss (0–1).</summary>
        public double BossSpawnChance { get; set; } = 0.2;

        /// <summary>Health multiplier applied to boss monsters.</summary>
        public float BossHealthMultiplier { get; set; } = 2f;

        /// <summary>Damage multiplier applied to boss monsters.</summary>
        public float BossDamageMultiplier { get; set; } = 2f;

        /// <summary>Locations where ambushes are never allowed.</summary>
        public List<string> DisallowedLocations { get; set; } = new();
    }
}
