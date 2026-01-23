using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Monsters;

namespace CombatLevelScaling
{
    public class ModEntry : Mod
    {
        private ModConfig Config = new();
        private const string PROCESSED_KEY = "ZeroXPatch.CombatLevelScaling.Processed";

        private readonly Dictionary<int, string> _debugInfo = new();
        private readonly Dictionary<int, string> _eliteLights = new();

        public override void Entry(IModHelper helper)
        {
            Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.World.NpcListChanged += OnNpcListChanged;
            helper.Events.Player.Warped += OnWarped;
            helper.Events.Display.RenderedWorld += OnRenderedWorld;
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            _eliteLights.Clear();
            _debugInfo.Clear();
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            SetupConfigMenu();
        }

        // --- EVENTS ---

        private void OnWarped(object? sender, WarpedEventArgs e)
        {
            if (!e.IsLocalPlayer || !ShouldScaleInCurrentLocation(e.NewLocation)) return;

            _debugInfo.Clear();
            List<Monster> mobsToProcess = new();

            foreach (var npc in e.NewLocation.characters)
            {
                if (npc is Monster monster && !monster.modData.ContainsKey(PROCESSED_KEY))
                {
                    mobsToProcess.Add(monster);
                }
            }

            foreach (var monster in mobsToProcess)
            {
                ProcessMonster(monster, e.NewLocation);
            }
        }

        private void OnNpcListChanged(object? sender, NpcListChangedEventArgs e)
        {
            foreach (var npc in e.Removed)
            {
                if (npc is Monster monster)
                {
                    if (_eliteLights.ContainsKey(monster.id)) RemoveEliteLight(monster);
                    if (_debugInfo.ContainsKey(monster.id)) _debugInfo.Remove(monster.id);
                }
            }

            if (!Context.IsMainPlayer) return;

            if (ShouldScaleInCurrentLocation(e.Location))
            {
                foreach (var npc in e.Added)
                {
                    if (npc is Monster monster && !monster.modData.ContainsKey(PROCESSED_KEY))
                    {
                        ProcessMonster(monster, e.Location);
                    }
                }
            }
        }

        // --- LOGIC ---

        private void ProcessMonster(Monster monster, GameLocation location)
        {
            monster.modData[PROCESSED_KEY] = "true";
            ApplyScaling(monster);

            if (Config.IncreaseSpawnRate)
            {
                TrySpawnClone(monster, location);
            }
        }

        private void ApplyScaling(Monster monster)
        {
            int combatLevel = Game1.player.CombatLevel;

            float multiplier = 1.0f + (combatLevel * Config.StatIncreasePerLevel);
            bool isElite = false;

            if (Config.EnableEliteMonsters && Game1.random.NextDouble() < Config.EliteChance)
            {
                isElite = true;
                multiplier *= Config.EliteStatMultiplier;
                MakeEliteVisuals(monster);
            }

            string debugStats = $"Lvl:{combatLevel} x {Config.StatIncreasePerLevel:0.00} = {multiplier:0.0}x";
            if (isElite) debugStats += " (ELITE)";
            _debugInfo[monster.id] = debugStats;

            monster.MaxHealth = (int)(monster.MaxHealth * multiplier);
            monster.Health = monster.MaxHealth;
            monster.DamageToFarmer = (int)(monster.DamageToFarmer * multiplier);
        }

        private void MakeEliteVisuals(Monster monster)
        {
            try
            {
                monster.Scale = 1.5f;
                string lightId = $"{ModManifest.UniqueID}_{monster.id}_{Game1.random.Next()}";

                LightSource light = new LightSource(
                    lightId,
                    4,
                    monster.Position,
                    2.0f,
                    new Color(255, 20, 20),
                    LightSource.LightContext.None,
                    monster.id
                );

                monster.currentLocation.sharedLights.Add(lightId, light);
                _eliteLights[monster.id] = lightId;
            }
            catch (Exception ex)
            {
                Monitor.Log($"Elite Visual Error: {ex.Message}", LogLevel.Trace);
            }
        }

        private void RemoveEliteLight(Monster monster)
        {
            if (_eliteLights.TryGetValue(monster.id, out string? lightId))
            {
                if (monster.currentLocation != null)
                {
                    if (monster.currentLocation.sharedLights.ContainsKey(lightId))
                        monster.currentLocation.removeLightSource(lightId);
                }

                if (Game1.currentLocation != null && Game1.currentLocation.sharedLights.ContainsKey(lightId))
                {
                    Game1.currentLocation.removeLightSource(lightId);
                }

                _eliteLights.Remove(monster.id);
            }
        }

        private void TrySpawnClone(Monster original, GameLocation location)
        {
            // FTM Safety Check
            if (original.GetType().Assembly != typeof(Game1).Assembly) return;

            double chance = Game1.player.CombatLevel * Config.SpawnIncreasePerLevel;

            if (Game1.random.NextDouble() < chance)
            {
                Vector2 spawnPos = FindOpenTile(location, original.Tile);
                if (spawnPos == Vector2.Zero) return;

                // Determine Difficulty Level for Constructor
                int difficultyLevel = 0;
                if (location is MineShaft mine)
                {
                    difficultyLevel = mine.mineLevel;
                }
                else if (location is VolcanoDungeon volcano)
                {
                    difficultyLevel = volcano.level.Value;
                }

                Monster? clone = CreateSafeClone(original, spawnPos * 64f, difficultyLevel);
                if (clone != null)
                {
                    clone.id = Game1.random.Next();
                    ApplyScaling(clone);
                    clone.modData[PROCESSED_KEY] = "true";
                    location.characters.Add(clone);
                }
            }
        }

        private static Monster? CreateSafeClone(Monster original, Vector2 position, int difficultyLevel)
        {
            try
            {
                Type type = original.GetType();

                // Types that require Difficulty Level to spawn correct variants (Iridium vs Normal)
                if (original is GreenSlime) return new GreenSlime(position, difficultyLevel);
                if (original is Bat) return new Bat(position, difficultyLevel);
                if (original is Bug) return new Bug(position, difficultyLevel);
                if (original is BigSlime) return new BigSlime(position, difficultyLevel);
                // FIXED: Use difficultyLevel for MetalHead (determines if it's a HotHead or not)
                if (original is MetalHead) return new MetalHead(position, difficultyLevel);

                // Types that do NOT take difficulty level
                if (original is Ghost) return new Ghost(position);
                if (original is RockCrab) return new RockCrab(position);
                if (original is Grub) return new Grub(position, true);
                if (original is Fly) return new Fly(position, true);
                if (original is DustSpirit) return new DustSpirit(position);
                if (original is SquidKid) return new SquidKid(position);
                if (original is Skeleton) return new Skeleton(position, false);
                if (original is ShadowBrute) return new ShadowBrute(position);
                if (original is ShadowShaman) return new ShadowShaman(position);

                // Fallback
                return Activator.CreateInstance(type, position) as Monster;
            }
            catch
            {
                return null;
            }
        }

        private static Vector2 FindOpenTile(GameLocation loc, Vector2 origin)
        {
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0) continue;
                    Vector2 target = origin + new Vector2(x, y);

                    if (!loc.isTileOnMap(target)) continue;
                    if (loc.getTileIndexAt((int)target.X, (int)target.Y, "Buildings") != -1) continue;
                    if (loc.getObjectAtTile((int)target.X, (int)target.Y) != null) continue;
                    if (loc.getLargeTerrainFeatureAt((int)target.X, (int)target.Y) != null) continue;
                    if (loc.isCharacterAtTile(target) != null) continue;

                    return target;
                }
            }
            return Vector2.Zero;
        }

        private bool ShouldScaleInCurrentLocation(GameLocation loc)
        {
            if (loc is MineShaft mine)
            {
                if (mine.mineLevel < 120 && mine.mineLevel != 77377) return Config.EnableInMines;
                return Config.EnableInSkullCavern;
            }
            if (loc is VolcanoDungeon) return Config.EnableInVolcano;
            return false;
        }

        private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
        {
            if (!Config.ShowDebugInfo) return;
            if (Game1.currentLocation == null) return;

            foreach (var npc in Game1.currentLocation.characters)
            {
                if (npc is Monster monster)
                {
                    Vector2 screenPos = Game1.GlobalToLocal(Game1.viewport, monster.Position);
                    screenPos.Y -= 60;

                    string text = $"HP: {monster.Health}/{monster.MaxHealth}\nDMG: {monster.DamageToFarmer}";

                    if (monster.modData.ContainsKey(PROCESSED_KEY)) text += "\n[SCALED]";
                    if (_debugInfo.TryGetValue(monster.id, out string? debugStats))
                    {
                        text += $"\n{debugStats}";
                    }

                    e.SpriteBatch.DrawString(Game1.smallFont, text, screenPos, Color.Yellow, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0.99f);
                }
            }
        }

        private void SetupConfigMenu()
        {
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu == null) return;

            configMenu.Register(ModManifest, () => Config = new ModConfig(), () => Helper.WriteConfig(Config));

            configMenu.AddSectionTitle(ModManifest, () => Helper.Translation.Get("config.title.debug"));
            configMenu.AddBoolOption(ModManifest, () => Config.ShowDebugInfo, val => Config.ShowDebugInfo = val,
                () => Helper.Translation.Get("config.debug.enable"), () => Helper.Translation.Get("config.debug.tooltip"));

            configMenu.AddSectionTitle(ModManifest, () => Helper.Translation.Get("config.title.stats"));
            configMenu.AddNumberOption(ModManifest, () => Config.StatIncreasePerLevel, val => Config.StatIncreasePerLevel = val,
                () => Helper.Translation.Get("config.stat.percent"), () => Helper.Translation.Get("config.stat.percent.tooltip"), 0f, 5f, 0.01f);

            configMenu.AddSectionTitle(ModManifest, () => Helper.Translation.Get("config.title.locations"));
            configMenu.AddBoolOption(ModManifest, () => Config.EnableInMines, val => Config.EnableInMines = val, () => Helper.Translation.Get("config.loc.mines"));
            configMenu.AddBoolOption(ModManifest, () => Config.EnableInSkullCavern, val => Config.EnableInSkullCavern = val, () => Helper.Translation.Get("config.loc.skull"));
            configMenu.AddBoolOption(ModManifest, () => Config.EnableInVolcano, val => Config.EnableInVolcano = val, () => Helper.Translation.Get("config.loc.volcano"));

            configMenu.AddSectionTitle(ModManifest, () => Helper.Translation.Get("config.title.spawn"));
            configMenu.AddBoolOption(ModManifest, () => Config.IncreaseSpawnRate, val => Config.IncreaseSpawnRate = val, () => Helper.Translation.Get("config.spawn.enable"));
            configMenu.AddNumberOption(ModManifest, () => Config.SpawnIncreasePerLevel, val => Config.SpawnIncreasePerLevel = val,
                () => Helper.Translation.Get("config.spawn.percent"), () => Helper.Translation.Get("config.spawn.percent.tooltip"), 0f, 0.2f, 0.01f);

            configMenu.AddSectionTitle(ModManifest, () => Helper.Translation.Get("config.title.elite"));
            configMenu.AddBoolOption(ModManifest, () => Config.EnableEliteMonsters, val => Config.EnableEliteMonsters = val, () => Helper.Translation.Get("config.elite.enable"));
            configMenu.AddNumberOption(ModManifest, () => Config.EliteChance, val => Config.EliteChance = val,
                () => Helper.Translation.Get("config.elite.chance"), () => Helper.Translation.Get("config.elite.chance.tooltip"), 0f, 1f, 0.01f);
            configMenu.AddNumberOption(ModManifest, () => Config.EliteStatMultiplier, val => Config.EliteStatMultiplier = val,
                () => Helper.Translation.Get("config.elite.multi"), () => Helper.Translation.Get("config.elite.multi.tooltip"), 1f, 5f, 0.1f);
        }
    }
}