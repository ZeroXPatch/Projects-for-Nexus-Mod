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

        // We still track lights locally to clean them up, 
        // but we don't use this for scaling logic anymore.
        private readonly Dictionary<int, string> _eliteLights = new();

        public override void Entry(IModHelper helper)
        {
            Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.DayStarted += OnDayStarted;

            // Scaling Events
            helper.Events.World.NpcListChanged += OnNpcListChanged;
            helper.Events.Player.Warped += OnWarped;

            // Debug Draw
            helper.Events.Display.RenderedWorld += OnRenderedWorld;
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            _eliteLights.Clear();
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            SetupConfigMenu();
        }

        // --- 1. HANDLE MAP CHANGE (catch existing mobs) ---
        private void OnWarped(object? sender, WarpedEventArgs e)
        {
            if (!e.IsLocalPlayer || !ShouldScaleInCurrentLocation(e.NewLocation)) return;

            // Use a separate list to avoid modification errors while looping
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

        // --- 2. HANDLE SPAWNS (catch new mobs) ---
        private void OnNpcListChanged(object? sender, NpcListChangedEventArgs e)
        {
            // Cleanup lights for dead monsters
            foreach (var npc in e.Removed)
            {
                if (npc is Monster monster && _eliteLights.ContainsKey(monster.id))
                {
                    RemoveEliteLight(monster);
                }
            }

            // Process new monsters
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

        // --- CORE LOGIC ---

        private void ProcessMonster(Monster monster, GameLocation location)
        {
            // Mark as processed IMMEDIATELY using ModData (Persistent on the object)
            monster.modData[PROCESSED_KEY] = "true";

            ApplyScaling(monster);

            if (Config.IncreaseSpawnRate)
            {
                // Defer spawn slightly or handle carefully to avoid loops
                // Since the clone will be added to the list, OnNpcListChanged will fire for it.
                // The clone will NOT have modData set yet, so it will be processed.
                // WE MUST ensure the clone logic doesn't infinite loop.
                // The probability check inside TrySpawnClone stops the infinite explosion,
                // but strictly speaking, clones can clone clones. 
                // If you want to prevent clones from cloning, set a flag on the clone.
                TrySpawnClone(monster, location);
            }
        }

        private void ApplyScaling(Monster monster)
        {
            int combatLevel = Game1.player.CombatLevel;

            float multiplier = 1.0f + (combatLevel * Config.StatIncreasePerLevel);
            bool isElite = false;

            // Elite Check
            if (Config.EnableEliteMonsters && Game1.random.NextDouble() < Config.EliteChance)
            {
                isElite = true;
                multiplier *= Config.EliteStatMultiplier;
                MakeEliteVisuals(monster);
            }

            // Stats
            monster.MaxHealth = (int)(monster.MaxHealth * multiplier);
            monster.Health = monster.MaxHealth;
            monster.DamageToFarmer = (int)(monster.DamageToFarmer * multiplier);

            // Speed
            if (Config.StatIncreasePerLevel > 0)
            {
                float baseSpeed = monster.Speed;
                float finalSpeed = baseSpeed * multiplier;

                if (isElite) finalSpeed += 1.5f;

                float speedToAdd = finalSpeed - baseSpeed;

                // Stardew uses 'addedSpeed' as an integer offset.
                // If the boost is significant, apply it.
                if (speedToAdd >= 1.0f)
                {
                    monster.addedSpeed = (int)Math.Round(speedToAdd);
                }
            }
        }

        private void MakeEliteVisuals(Monster monster)
        {
            try
            {
                // 1. Resize
                monster.Scale = 1.5f;

                // 2. Light
                // Using Game1.random.Next() ensures unique ID even if Monster ID is reused
                string lightId = $"{ModManifest.UniqueID}_{monster.id}_{Game1.random.Next()}";

                LightSource light = new LightSource(
                    lightId,
                    4, // Texture index 4 is a soft glow circle
                    monster.Position,
                    2.0f,
                    new Color(255, 20, 20), // Strong Red
                    LightSource.LightContext.None,
                    monster.id
                );

                // Add to the location
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
                // Try removing from current location first
                if (monster.currentLocation != null)
                {
                    if (monster.currentLocation.sharedLights.ContainsKey(lightId))
                        monster.currentLocation.removeLightSource(lightId);
                }

                // Fallback: Try Game1.currentLocation just in case
                if (Game1.currentLocation != null && Game1.currentLocation.sharedLights.ContainsKey(lightId))
                {
                    Game1.currentLocation.removeLightSource(lightId);
                }

                _eliteLights.Remove(monster.id);
            }
        }

        private void TrySpawnClone(Monster original, GameLocation location)
        {
            double chance = Game1.player.CombatLevel * Config.SpawnIncreasePerLevel;

            if (Game1.random.NextDouble() < chance)
            {
                Vector2 spawnPos = FindOpenTile(location, original.Tile);
                if (spawnPos == Vector2.Zero) return;

                Monster? clone = CreateSafeClone(original, spawnPos * 64f);
                if (clone != null)
                {
                    // Give the clone a new ID
                    clone.id = Game1.random.Next();

                    // OPTIONAL: Mark clone as "Do Not Clone Again" to prevent chain reaction?
                    // If you want clones to spawn clones, leave this out.
                    // If you want to prevent chain reaction:
                    // clone.modData[PROCESSED_KEY] = "true"; 
                    // (But then we'd have to manually call ApplyScaling(clone) here)

                    // Let's manually scale it to be safe and avoid event timing issues
                    ApplyScaling(clone);
                    clone.modData[PROCESSED_KEY] = "true"; // Mark processed so OnNpcListChanged ignores it

                    location.characters.Add(clone);
                }
            }
        }

        private static Monster? CreateSafeClone(Monster original, Vector2 position)
        {
            try
            {
                Type type = original.GetType();

                // 1.6 Complex Types
                if (original is GreenSlime) return new GreenSlime(position, 0);
                if (original is Bat) return new Bat(position);
                if (original is Ghost) return new Ghost(position);
                if (original is RockCrab) return new RockCrab(position);
                if (original is Grub) return new Grub(position);
                if (original is Fly) return new Fly(position);
                if (original is DustSpirit) return new DustSpirit(position);
                if (original is Bug) return new Bug(position, 0);

                // Generic Fallback
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
            return Config.EnableInWilderness;
        }

        // --- DEBUG OVERLAY ---
        private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
        {
            if (!Config.ShowDebugInfo) return;
            if (Game1.currentLocation == null) return;

            foreach (var npc in Game1.currentLocation.characters)
            {
                if (npc is Monster monster)
                {
                    // Convert world position to screen position
                    Vector2 screenPos = Game1.GlobalToLocal(Game1.viewport, monster.Position);

                    // Offset text to be above the sprite
                    screenPos.Y -= 40;

                    string text = $"HP: {monster.Health}/{monster.MaxHealth}\nDMG: {monster.DamageToFarmer}\nSPD: {monster.Speed}+{monster.addedSpeed}";

                    if (monster.modData.ContainsKey(PROCESSED_KEY)) text += "\n[SCALED]";
                    if (_eliteLights.ContainsKey(monster.id)) text += "\n[ELITE]";

                    e.SpriteBatch.DrawString(
                        Game1.smallFont,
                        text,
                        screenPos,
                        Color.Yellow,
                        0f,
                        Vector2.Zero,
                        1f,
                        SpriteEffects.None,
                        0.99f
                    );
                }
            }
        }

        private void SetupConfigMenu()
        {
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu == null) return;

            configMenu.Register(ModManifest, () => Config = new ModConfig(), () => Helper.WriteConfig(Config));

            configMenu.AddSectionTitle(ModManifest, () => "Debug");
            configMenu.AddBoolOption(ModManifest, () => Config.ShowDebugInfo, val => Config.ShowDebugInfo = val, () => "Show Debug Info", () => "Shows stats above monsters heads");

            configMenu.AddSectionTitle(ModManifest, () => Helper.Translation.Get("config.title.stats"));
            configMenu.AddNumberOption(ModManifest, () => Config.StatIncreasePerLevel, val => Config.StatIncreasePerLevel = val,
                () => Helper.Translation.Get("config.stat.percent"), () => Helper.Translation.Get("config.stat.percent.tooltip"), 0f, 5f, 0.05f);

            configMenu.AddSectionTitle(ModManifest, () => Helper.Translation.Get("config.title.locations"));
            configMenu.AddBoolOption(ModManifest, () => Config.EnableInMines, val => Config.EnableInMines = val, () => Helper.Translation.Get("config.loc.mines"));
            configMenu.AddBoolOption(ModManifest, () => Config.EnableInSkullCavern, val => Config.EnableInSkullCavern = val, () => Helper.Translation.Get("config.loc.skull"));
            configMenu.AddBoolOption(ModManifest, () => Config.EnableInVolcano, val => Config.EnableInVolcano = val, () => Helper.Translation.Get("config.loc.volcano"));
            configMenu.AddBoolOption(ModManifest, () => Config.EnableInWilderness, val => Config.EnableInWilderness = val, () => Helper.Translation.Get("config.loc.wild"));

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