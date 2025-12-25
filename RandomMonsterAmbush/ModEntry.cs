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

        // master list of monster factories (key -> factory)
        private readonly Dictionary<string, Func<Vector2, Monster>> _allMonsterFactories = new()
        {
            ["GreenSlime"] = tile => new GreenSlime(tile * Game1.tileSize),
            ["DustSpirit"] = tile => new DustSpirit(tile * Game1.tileSize),
            ["Bat"] = tile => new Bat(tile * Game1.tileSize),
            ["RockCrab"] = tile => new RockCrab(tile * Game1.tileSize),
            ["Ghost"] = tile => new Ghost(tile * Game1.tileSize),
            ["Skeleton"] = tile => new Skeleton(tile * Game1.tileSize),
            ["SquidKid"] = tile => new SquidKid(tile * Game1.tileSize),
            ["ShadowBrute"] = tile => new ShadowBrute(tile * Game1.tileSize),
            ["ShadowShaman"] = tile => new ShadowShaman(tile * Game1.tileSize),
            ["Serpent"] = tile => new Serpent(tile * Game1.tileSize),
        };

        // boss pool is a subset of the above (and also filtered by toggles)
        private static readonly string[] BossMonsterIds =
        {
            "ShadowBrute",
            "ShadowShaman",
            "Serpent",
            "SquidKid",
            "Ghost"
        };

        private readonly List<Func<Vector2, Monster>> _enabledMonsterFactories = new();
        private readonly List<Func<Vector2, Monster>> _enabledBossFactories = new();

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

            var t = this.Helper.Translation;

            configMenu.Register(
                mod: this.ModManifest,
                reset: () =>
                {
                    _config = new ModConfig();
                    Helper.WriteConfig(_config);
                    RebuildMonsterPools();
                },
                save: () =>
                {
                    Helper.WriteConfig(_config);
                    RebuildMonsterPools();
                },
                titleScreenOnly: false
            );

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

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => _config.AmbushStartTime,
                setValue: value => _config.AmbushStartTime = value,
                name: () => t.Get("config.ambushStartTime.name"),
                tooltip: () => t.Get("config.ambushStartTime.tooltip"),
                min: 600,
                max: 2600,
                interval: 10,
                formatValue: FormatTime
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => _config.PreventDuringEvents,
                setValue: value => _config.PreventDuringEvents = value,
                name: () => t.Get("config.skipEvents.name"),
                tooltip: () => t.Get("config.skipEvents.tooltip")
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => _config.SkipWhileHoldingFishingRod,
                setValue: value => _config.SkipWhileHoldingFishingRod = value,
                name: () => t.Get("config.skipFishingRod.name"),
                tooltip: () => t.Get("config.skipFishingRod.tooltip")
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => _config.DisallowIndoors,
                setValue: value => _config.DisallowIndoors = value,
                name: () => t.Get("config.disallowIndoors.name"),
                tooltip: () => t.Get("config.disallowIndoors.tooltip")
            );

            // ==== MONSTER POOL SECTION ====
            configMenu.AddSectionTitle(
                mod: this.ModManifest,
                text: () => t.Get("config.section.monsters")
            );

            AddMonsterToggle(configMenu, "GreenSlime", () => _config.EnableGreenSlime, v => _config.EnableGreenSlime = v, t);
            AddMonsterToggle(configMenu, "DustSpirit", () => _config.EnableDustSpirit, v => _config.EnableDustSpirit = v, t);
            AddMonsterToggle(configMenu, "Bat", () => _config.EnableBat, v => _config.EnableBat = v, t);
            AddMonsterToggle(configMenu, "RockCrab", () => _config.EnableRockCrab, v => _config.EnableRockCrab = v, t);
            AddMonsterToggle(configMenu, "Ghost", () => _config.EnableGhost, v => _config.EnableGhost = v, t);
            AddMonsterToggle(configMenu, "Skeleton", () => _config.EnableSkeleton, v => _config.EnableSkeleton = v, t);
            AddMonsterToggle(configMenu, "SquidKid", () => _config.EnableSquidKid, v => _config.EnableSquidKid = v, t);
            AddMonsterToggle(configMenu, "ShadowBrute", () => _config.EnableShadowBrute, v => _config.EnableShadowBrute = v, t);
            AddMonsterToggle(configMenu, "ShadowShaman", () => _config.EnableShadowShaman, v => _config.EnableShadowShaman = v, t);
            AddMonsterToggle(configMenu, "Serpent", () => _config.EnableSerpent, v => _config.EnableSerpent = v, t);

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

            if (Game1.player is null)
                return;

            // SMAPI 4: IsMultipleOf takes uint
            if (!e.IsMultipleOf((uint)_config.CheckIntervalTicks))
                return;

            if (_enabledMonsterFactories.Count == 0)
                return;

            // skip ambushes while holding any fishing rod
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

            // start time logic:
            // - if AllowDaytimeSpawns is OFF, enforce >= 1800, but still allow customizing later start time (e.g. 2000)
            int earliest = _config.AmbushStartTime;
            if (!_config.AllowDaytimeSpawns)
                earliest = Math.Max(earliest, 1800);

            if (Game1.timeOfDay < earliest)
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
                _enabledBossFactories.Count > 0 &&
                _random.NextDouble() < _config.BossSpawnChance;

            int bossIndex = spawnBossThisAmbush ? _random.Next(spawnCount) : -1;

            for (int i = 0; i < spawnCount; i++)
            {
                if (!TryFindSpawnTile(location, out Vector2 tile))
                    continue;

                Monster monster = (spawnBossThisAmbush && i == bossIndex)
                    ? CreateRandomBoss(tile)
                    : CreateRandomMonster(tile);

                monster.modData[AmbushSpawnDayKey] = Game1.Date.TotalDays.ToString(CultureInfo.InvariantCulture);

                monster.currentLocation = location;
                location.characters.Add(monster);
                spawned++;
            }

            return spawned;
        }

        private bool TryFindSpawnTile(GameLocation location, out Vector2 tile)
        {
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

            if (!location.CanSpawnCharacterHere(tile))
                return false;

            string? noSpawn = location.doesTileHaveProperty((int)tile.X, (int)tile.Y, "NoSpawn", "Back");
            return string.IsNullOrWhiteSpace(noSpawn);
        }

        private Monster CreateRandomMonster(Vector2 tile)
        {
            Func<Vector2, Monster> factory = _enabledMonsterFactories[_random.Next(_enabledMonsterFactories.Count)];
            return factory(tile);
        }

        /// <summary>Create an elite boss monster by picking from the boss pool and scaling its stats.</summary>
        private Monster CreateRandomBoss(Vector2 tile)
        {
            Func<Vector2, Monster> factory = _enabledBossFactories[_random.Next(_enabledBossFactories.Count)];
            Monster boss = factory(tile);

            boss.MaxHealth = (int)(boss.MaxHealth * _config.BossHealthMultiplier);
            boss.Health = boss.MaxHealth;
            boss.DamageToFarmer = (int)(boss.DamageToFarmer * _config.BossDamageMultiplier);

            // ELITE LOOK: larger + glowing
            boss.Scale *= 1.8f;

            // Glow (1.6 uses plain bool/Color here, not .Value)
            boss.isGlowing = true;
            boss.glowingColor = Color.Gold;

            boss.Name = $"Elite {boss.Name}";
            return boss;
        }


        /// <summary>
        /// Returns true if ambushes should never happen in the given location.
        /// Includes user-configured disallowed locations plus hard-coded safety areas.
        /// </summary>
        private bool IsLocationBlocked(GameLocation location)
        {
            string locName = location.NameOrUniqueName;

            // disallow ALL indoor locations (default ON)
            if (_config.DisallowIndoors && !location.IsOutdoors)
                return true;

            // user-configurable blocked locations
            if (_config.DisallowedLocations.Any(name =>
                    string.Equals(name, locName, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            // player farmhouse
            if (location is FarmHouse)
                return true;

            // Harvey's clinic + apartment
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

                return true;
            });
        }

        private void LoadConfig()
        {
            _config = Helper.ReadConfig<ModConfig>();
            bool changed = false;

            // Defaults requested
            // (If player already has a config.json, we won't overwrite it unless invalid.
            // New installs will get these defaults from ModConfig.)
            if (_config.CheckIntervalTicks < 30)
            {
                _config.CheckIntervalTicks = 30;
                changed = true;
            }

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

            // Validate ambush start time
            int fixedTime = FixTime(_config.AmbushStartTime);
            if (fixedTime != _config.AmbushStartTime)
            {
                _config.AmbushStartTime = fixedTime;
                changed = true;
            }

            // If daytime spawns are OFF, enforce >= 1800
            if (!_config.AllowDaytimeSpawns && _config.AmbushStartTime < 1800)
            {
                _config.AmbushStartTime = 1800;
                changed = true;
            }

            if (_config.DisallowedLocations == null)
            {
                _config.DisallowedLocations = new List<string>();
                changed = true;
            }

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

            RebuildMonsterPools();
        }

        private void RebuildMonsterPools()
        {
            _enabledMonsterFactories.Clear();
            _enabledBossFactories.Clear();

            foreach (var pair in _allMonsterFactories)
            {
                if (IsMonsterEnabled(pair.Key))
                    _enabledMonsterFactories.Add(pair.Value);
            }

            foreach (string id in BossMonsterIds)
            {
                if (IsMonsterEnabled(id) && _allMonsterFactories.TryGetValue(id, out var factory))
                    _enabledBossFactories.Add(factory);
            }
        }

        private bool IsMonsterEnabled(string id)
        {
            return id switch
            {
                "GreenSlime" => _config.EnableGreenSlime,
                "DustSpirit" => _config.EnableDustSpirit,
                "Bat" => _config.EnableBat,
                "RockCrab" => _config.EnableRockCrab,
                "Ghost" => _config.EnableGhost,
                "Skeleton" => _config.EnableSkeleton,
                "SquidKid" => _config.EnableSquidKid,
                "ShadowBrute" => _config.EnableShadowBrute,
                "ShadowShaman" => _config.EnableShadowShaman,
                "Serpent" => _config.EnableSerpent,
                _ => true
            };
        }

        private static void AddMonsterToggle(
            IGenericModConfigMenuApi configMenu,
            string monsterId,
            Func<bool> getValue,
            Action<bool> setValue,
            ITranslationHelper t)
        {
            string keyBase = monsterId switch
            {
                "GreenSlime" => "config.monster.greenSlime",
                "DustSpirit" => "config.monster.dustSpirit",
                "Bat" => "config.monster.bat",
                "RockCrab" => "config.monster.rockCrab",
                "Ghost" => "config.monster.ghost",
                "Skeleton" => "config.monster.skeleton",
                "SquidKid" => "config.monster.squidKid",
                "ShadowBrute" => "config.monster.shadowBrute",
                "ShadowShaman" => "config.monster.shadowShaman",
                "Serpent" => "config.monster.serpent",
                _ => "config.monster.unknown"
            };

            configMenu.AddBoolOption(
                mod: ModEntryStatic.Manifest!,
                getValue: getValue,
                setValue: value =>
                {
                    setValue(value);
                    // pools rebuild on save/reset; live rebuild isn’t required, but we can do it on the fly if wanted
                },
                name: () => t.Get($"{keyBase}.name"),
                tooltip: () => t.Get($"{keyBase}.tooltip")
            );
        }

        // Helper: GMCM interface doesn't pass ModManifest into AddMonsterToggle, so we stash it.
        // This avoids changing your IGenericModConfigMenuApi.
        private static class ModEntryStatic
        {
            public static IManifest? Manifest { get; set; }
        }

        private static string FormatTime(int time)
        {
            time = FixTime(time);
            int hour24 = time / 100;
            int min = time % 100;

            // SDV uses 6..26
            int hour = hour24;
            string ampm = "AM";
            if (hour24 >= 12)
                ampm = "PM";

            int hour12 = hour24 % 12;
            if (hour12 == 0)
                hour12 = 12;

            return $"{hour12}:{min:00} {ampm}";
        }

        private static int FixTime(int time)
        {
            // clamp
            time = Math.Clamp(time, 600, 2600);

            // force to nearest 10
            time = (time / 10) * 10;

            int hour = time / 100;
            int min = time % 100;

            // if minutes invalid, clamp down
            if (min >= 60)
                min = 50;

            return hour * 100 + min;
        }

        // Hook Manifest stash when the class loads (Entry is too late for static helpers)
        public ModEntry()
        {
            ModEntryStatic.Manifest = this.ModManifest;
        }
    }
}
