using Microsoft.Xna.Framework;
using Netcode;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Buffs;
using StardewValley.Locations;
using StardewValley.Monsters;
using System;
using System.Collections.Generic;
using System.Linq;
using xTile.Dimensions;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;
using XnaVector2 = Microsoft.Xna.Framework.Vector2;

namespace CaveWeather
{
    internal enum CaveWeatherType
    {
        None,

        // preexisting 5
        FungalHarvest,
        TemporalFlux,
        BerserkerDay,
        FrenzyFog,
        BloodthirstWinds,

        // new 2
        UnstableVeins,
        LuckyVeins
    }

    public sealed class ModEntry : Mod
    {
        private const string DayBuffId = "CaveWeather.DayBuff";

        private ModConfig Config = null!;
        private Random Rng = null!;

        private CaveWeatherType TodayCaveWeather = CaveWeatherType.None;
        private bool HasShownCaveMessageToday;

        // Berserker
        private readonly Dictionary<Monster, int> MonsterLastHealth = new();
        private readonly HashSet<Monster> BerserkedMonsters = new();

        // Temporal Flux
        private readonly Dictionary<Monster, int> TemporalBaseSpeed = new();
        private int TemporalFluxPhaseTicksRemaining;
        private bool TemporalFluxBurstPhase;

        // Frenzy Fog
        private readonly HashSet<Monster> FrenzyAdjustedMonsters = new();
        private bool DrawFrenzyFog;

        // New: Unstable Veins / Lucky Veins helpers
        // FIX: use int (Game1.ticks is int)
        private int LastOrePulseTick;
        private int LastLuckyPulseTick;

        private static readonly string[] FungalMushroomItemIds =
        {
            "(O)404", // Common Mushroom
            "(O)420", // Red Mushroom
            "(O)422"  // Purple Mushroom
        };

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();
            this.Rng = new Random();

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.Player.Warped += this.OnWarped;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.Display.RenderedWorld += this.OnRenderedWorld;
        }

        // --------------------
        // Day roll
        // --------------------

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.player is null)
                return;

            this.HasShownCaveMessageToday = false;
            this.TodayCaveWeather = CaveWeatherType.None;

            // reset per-day trackers
            this.MonsterLastHealth.Clear();
            this.BerserkedMonsters.Clear();
            this.TemporalBaseSpeed.Clear();
            this.FrenzyAdjustedMonsters.Clear();
            this.DrawFrenzyFog = false;
            this.ResetTemporalFluxPhase();
            this.LastOrePulseTick = 0;
            this.LastLuckyPulseTick = 0;

            // roll today
            if (this.Rng.NextDouble() > this.Config.DailyCaveWeatherChance)
                return;

            List<CaveWeatherType> candidates = this.GetEnabledWeathersAnywhere();
            if (candidates.Count == 0)
                return;

            this.TodayCaveWeather = candidates[this.Rng.Next(candidates.Count)];
        }

        private List<CaveWeatherType> GetEnabledWeathersAnywhere()
        {
            var list = new List<CaveWeatherType>();

            void AddIfEnabled(CaveWeatherType t, ModConfigWeather w)
            {
                if (!w.Enabled)
                    return;

                // "Anywhere" means enabled in at least one mine-like location.
                if (w.EnableInMines || w.EnableInSkullCavern || w.EnableInVolcanoDungeon)
                    list.Add(t);
            }

            AddIfEnabled(CaveWeatherType.FungalHarvest, this.Config.FungalHarvest);
            AddIfEnabled(CaveWeatherType.TemporalFlux, this.Config.TemporalFlux);
            AddIfEnabled(CaveWeatherType.BerserkerDay, this.Config.BerserkerDay);
            AddIfEnabled(CaveWeatherType.FrenzyFog, this.Config.FrenzyFog);
            AddIfEnabled(CaveWeatherType.BloodthirstWinds, this.Config.BloodthirstWinds);
            AddIfEnabled(CaveWeatherType.UnstableVeins, this.Config.UnstableVeins);
            AddIfEnabled(CaveWeatherType.LuckyVeins, this.Config.LuckyVeins);

            return list;
        }

        // --------------------
        // Warps: show message once/day on first mine entry
        // --------------------

        private void OnWarped(object? sender, WarpedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.player is null)
                return;

            if (!ReferenceEquals(e.Player, Game1.player))
                return;

            if (!this.IsMineLikeLocation(e.NewLocation, out MineLocationKind kind))
                return;

            if (this.TodayCaveWeather == CaveWeatherType.None)
                return;

            if (!this.IsWeatherAllowedHere(this.TodayCaveWeather, kind))
                return;

            // message once per day
            if (!this.HasShownCaveMessageToday)
            {
                string? key = this.TodayCaveWeather switch
                {
                    CaveWeatherType.FungalHarvest => "message.fungal_harvest",
                    CaveWeatherType.TemporalFlux => "message.temporal_flux",
                    CaveWeatherType.BerserkerDay => "message.berserker_day",
                    CaveWeatherType.FrenzyFog => "message.frenzy_fog",
                    CaveWeatherType.BloodthirstWinds => "message.bloodthirst_winds",
                    CaveWeatherType.UnstableVeins => "message.unstable_veins",
                    CaveWeatherType.LuckyVeins => "message.lucky_veins",
                    _ => null
                };

                if (key is not null)
                {
                    string text = this.Helper.Translation.Get(key);
                    if (!string.IsNullOrWhiteSpace(text))
                        Game1.addHUDMessage(new HUDMessage(text, HUDMessage.newQuest_type));
                }

                this.HasShownCaveMessageToday = true;
            }

            // on-floor-entry effects
            if (this.TodayCaveWeather == CaveWeatherType.FungalHarvest)
            {
                this.SpawnMushroomsNearPlayer(e.NewLocation, this.Config.FungalHarvest.ExtraMushroomsPerFloor);
            }
            else if (this.TodayCaveWeather == CaveWeatherType.TemporalFlux)
            {
                this.TemporalBaseSpeed.Clear();
                this.ResetTemporalFluxPhase();
            }
            else if (this.TodayCaveWeather == CaveWeatherType.BerserkerDay)
            {
                this.MonsterLastHealth.Clear();
                this.BerserkedMonsters.Clear();
            }
            else if (this.TodayCaveWeather == CaveWeatherType.FrenzyFog)
            {
                this.FrenzyAdjustedMonsters.Clear();
                this.DrawFrenzyFog = true;
            }
            else if (this.TodayCaveWeather == CaveWeatherType.UnstableVeins)
            {
                // no special on-enter; handled per tick + pulses
            }
            else if (this.TodayCaveWeather == CaveWeatherType.LuckyVeins)
            {
                // simulate "more ore nodes" as extra ore finds on entry (safe, stable)
                this.SpawnLuckyVeinsOreFinds(e.NewLocation, kind, this.Config.LuckyVeins.ExtraOreFindsPerFloor);
            }
        }

        // --------------------
        // Update tick: run current weather logic
        // --------------------

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.player is null)
                return;

            GameLocation location = Game1.currentLocation;
            bool inMine = this.IsMineLikeLocation(location, out MineLocationKind kind);

            this.DrawFrenzyFog = inMine && this.TodayCaveWeather == CaveWeatherType.FrenzyFog
                                 && this.IsWeatherAllowedHere(this.TodayCaveWeather, kind);

            // keep buff synced
            this.UpdatePlayerDayBuff(inMine, kind);

            if (!inMine)
                return;

            if (this.TodayCaveWeather == CaveWeatherType.None)
                return;

            if (!this.IsWeatherAllowedHere(this.TodayCaveWeather, kind))
                return;

            switch (this.TodayCaveWeather)
            {
                case CaveWeatherType.FungalHarvest:
                    this.UpdateFungalHarvest(location, e);
                    break;

                case CaveWeatherType.TemporalFlux:
                    this.UpdateTemporalFlux(location, e);
                    break;

                case CaveWeatherType.BerserkerDay:
                    this.UpdateBerserkerDay(location, e);
                    break;

                case CaveWeatherType.FrenzyFog:
                    this.UpdateFrenzyFog(location, e);
                    break;

                case CaveWeatherType.BloodthirstWinds:
                    this.UpdateBloodthirstWinds(location, e);
                    break;

                case CaveWeatherType.UnstableVeins:
                    this.UpdateUnstableVeins(location, e);
                    break;

                case CaveWeatherType.LuckyVeins:
                    this.UpdateLuckyVeins(location, kind, e);
                    break;
            }
        }

        private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
        {
            if (!this.DrawFrenzyFog)
                return;

            var viewport = Game1.viewport;
            XnaRectangle rect = new XnaRectangle(0, 0, viewport.Width, viewport.Height);

            float alpha = this.Config.FrenzyFog.FogOpacity / 255f;
            if (alpha <= 0f)
                return;

            e.SpriteBatch.Draw(Game1.staminaRect, rect, Color.White * alpha);
        }

        // --------------------
        // Weather: Fungal Harvest
        // --------------------

        private void SpawnMushroomsNearPlayer(GameLocation location, int count)
        {
            if (count <= 0)
                return;

            XnaVector2 playerTile = Game1.player!.Tile;

            for (int i = 0; i < count; i++)
            {
                int attempts = 10;
                XnaVector2 tile = playerTile;

                while (attempts-- > 0)
                {
                    int x = (int)playerTile.X + this.Rng.Next(-6, 7);
                    int y = (int)playerTile.Y + this.Rng.Next(-4, 5);
                    XnaVector2 t = new XnaVector2(x, y);

                    if (location.isTileOnMap(t) &&
                        location.isTileLocationTotallyClearAndPlaceable(t) &&
                        !location.isTileOccupied(t))
                    {
                        tile = t;
                        break;
                    }
                }

                this.SpawnItemDebris(location, tile, 1, FungalMushroomItemIds);
            }
        }

        private void UpdateFungalHarvest(GameLocation location, UpdateTickedEventArgs e)
        {
            var cfg = this.Config.FungalHarvest;

            // ambient spores
            if (e.IsMultipleOf(cfg.SporeSpawnIntervalTicks))
            {
                XnaVector2 playerPos = Game1.player!.Position;

                for (int i = 0; i < cfg.SporeParticlesPerBurst; i++)
                {
                    float ox = this.Rng.Next(-8, 9);
                    float oy = this.Rng.Next(-4, 5);
                    XnaVector2 pos = playerPos + new XnaVector2(ox * 64f, oy * 64f);

                    var sprite = new TemporaryAnimatedSprite(
                        "LooseSprites\\Cursors",
                        new XnaRectangle(372, 1956, 8, 8),
                        80f,
                        4,
                        cfg.SporeLoops,
                        pos,
                        false,
                        this.Rng.NextDouble() < 0.5
                    )
                    {
                        motion = new XnaVector2(
                            0.25f * (this.Rng.NextDouble() < 0.5 ? -1f : 1f),
                            (float)(this.Rng.NextDouble() - 0.5) * 0.3f
                        ),
                        layerDepth = (pos.Y + 32f) / 10000f,
                        color = Color.LimeGreen
                    };

                    location.temporarySprites.Add(sprite);
                }
            }

            // slime mushroom "drops" (simulated)
            if (e.IsMultipleOf(60))
            {
                foreach (Monster monster in location.characters.OfType<Monster>())
                {
                    if (monster is GreenSlime or BigSlime)
                    {
                        if (this.Rng.NextDouble() < cfg.SlimeDropChance)
                        {
                            this.SpawnItemDebris(location, monster.Tile, 1, FungalMushroomItemIds);
                        }
                    }
                }
            }
        }

        // --------------------
        // Weather: Temporal Flux
        // --------------------

        private void ResetTemporalFluxPhase()
        {
            this.TemporalFluxBurstPhase = true;
            this.TemporalFluxPhaseTicksRemaining = (int)this.Config.TemporalFlux.PhaseDurationTicks;
        }

        private void UpdateTemporalFlux(GameLocation location, UpdateTickedEventArgs e)
        {
            var cfg = this.Config.TemporalFlux;

            // slow mine time (approx). May conflict with time mods; configurable by location toggles.
            if (cfg.TimeScale is > 0.0 and < 1.0)
            {
                var gameTime = Game1.currentGameTime;
                if (gameTime is not null)
                {
                    int elapsed = gameTime.ElapsedGameTime.Milliseconds;
                    int addBack = (int)(elapsed * (1.0 - cfg.TimeScale));
                    Game1.gameTimeInterval += addBack;
                }
            }

            if (this.TemporalFluxPhaseTicksRemaining <= 0)
            {
                this.TemporalFluxBurstPhase = !this.TemporalFluxBurstPhase;
                this.TemporalFluxPhaseTicksRemaining = (int)cfg.PhaseDurationTicks;
                this.TemporalBaseSpeed.Clear();
            }
            else
            {
                this.TemporalFluxPhaseTicksRemaining--;
            }

            double burstMult = cfg.BurstSpeedMultiplier;
            double slowMult = cfg.SlowSpeedMultiplier;

            foreach (Monster monster in location.characters.OfType<Monster>())
            {
                if (!this.TemporalBaseSpeed.TryGetValue(monster, out int baseSpeed))
                {
                    baseSpeed = monster.speed;
                    this.TemporalBaseSpeed[monster] = baseSpeed;
                }

                double mult = this.TemporalFluxBurstPhase ? burstMult : slowMult;
                int newSpeed = Math.Max(1, (int)Math.Round(baseSpeed * mult));
                monster.speed = newSpeed;
            }
        }

        // --------------------
        // Weather: Berserker Day
        // --------------------

        private void UpdateBerserkerDay(GameLocation location, UpdateTickedEventArgs e)
        {
            var cfg = this.Config.BerserkerDay;

            foreach (Monster monster in location.characters.OfType<Monster>())
            {
                int health = monster.Health;

                if (!this.MonsterLastHealth.TryGetValue(monster, out int last))
                {
                    this.MonsterLastHealth[monster] = health;
                    continue;
                }

                if (health > 0 && health < last && !this.BerserkedMonsters.Contains(monster))
                {
                    this.BerserkedMonsters.Add(monster);
                    monster.speed += cfg.MonsterSpeedBonus;

                    NetInt threshold = monster.moveTowardPlayerThreshold;
                    threshold.Value += cfg.MonsterAggroTilesBonus;
                }

                this.MonsterLastHealth[monster] = health;
            }

            if (e.IsMultipleOf(60))
            {
                var stillHere = new HashSet<Monster>(location.characters.OfType<Monster>());
                var toRemove = this.MonsterLastHealth.Keys.Where(m => !stillHere.Contains(m)).ToList();
                foreach (Monster m in toRemove)
                {
                    this.MonsterLastHealth.Remove(m);
                    this.BerserkedMonsters.Remove(m);
                }
            }
        }

        // --------------------
        // Weather: Frenzy Fog
        // --------------------

        private void UpdateFrenzyFog(GameLocation location, UpdateTickedEventArgs e)
        {
            var cfg = this.Config.FrenzyFog;

            foreach (Monster monster in location.characters.OfType<Monster>())
            {
                if (this.FrenzyAdjustedMonsters.Contains(monster))
                    continue;

                monster.speed += cfg.MonsterSpeedBonus;
                this.FrenzyAdjustedMonsters.Add(monster);
            }
        }

        // --------------------
        // Weather: Bloodthirst Winds
        // --------------------

        private void UpdateBloodthirstWinds(GameLocation location, UpdateTickedEventArgs e)
        {
            var cfg = this.Config.BloodthirstWinds;

            if (cfg.HealAmount > 0 && e.IsMultipleOf(cfg.HealIntervalTicks))
            {
                foreach (Monster monster in location.characters.OfType<Monster>())
                {
                    if (monster.Health <= 0)
                        continue;

                    int max = monster.MaxHealth;
                    if (monster.Health < max)
                        monster.Health = Math.Min(max, monster.Health + cfg.HealAmount);
                }
            }

            if (e.IsMultipleOf(90))
            {
                XnaVector2 playerPos = Game1.player!.Position;

                for (int i = 0; i < 4; i++)
                {
                    float ox = this.Rng.Next(-8, 9);
                    float oy = this.Rng.Next(-4, 5);
                    XnaVector2 pos = playerPos + new XnaVector2(ox * 64f, oy * 64f);

                    var sprite = new TemporaryAnimatedSprite(
                        "LooseSprites\\Cursors",
                        new XnaRectangle(372, 1956, 8, 8),
                        80f,
                        4,
                        2,
                        pos,
                        false,
                        this.Rng.NextDouble() < 0.5
                    )
                    {
                        motion = new XnaVector2(
                            0.3f * (this.Rng.NextDouble() < 0.5 ? -1f : 1f),
                            (float)(this.Rng.NextDouble() - 0.5) * 0.4f
                        ),
                        layerDepth = (pos.Y + 32f) / 10000f,
                        color = Color.Red
                    };

                    location.temporarySprites.Add(sprite);
                }
            }
        }

        // --------------------
        // Weather: Unstable Veins (new)
        // --------------------

        private void UpdateUnstableVeins(GameLocation location, UpdateTickedEventArgs e)
        {
            var cfg = this.Config.UnstableVeins;

            // pulse every N ticks: simulate "ore nodes sometimes explode"
            if (!e.IsMultipleOf(cfg.PulseIntervalTicks))
                return;

            // avoid double-pulsing if multiple handlers etc.
            if (Game1.ticks == this.LastOrePulseTick)
                return;
            this.LastOrePulseTick = Game1.ticks;

            // Find nearby "ore-ish" spots: we don't rely on fragile object IDs.
            // Instead we randomly trigger a small explosion near the player sometimes, and compensate with ore drops.
            if (this.Rng.NextDouble() > cfg.ExplosionPulseChance)
                return;

            XnaVector2 baseTile = Game1.player!.Tile;
            XnaVector2 tile = baseTile + new XnaVector2(this.Rng.Next(-5, 6), this.Rng.Next(-4, 5));

            if (!location.isTileOnMap(tile))
                return;

            // explosion effect
            this.DoSmallExplosion(location, tile, cfg.ExplosionDamage, cfg.ExplosionRadiusTiles, cfg.KnockbackStrength);

            // reward compensation: extra ore drops near explosion
            int oreCount = this.Rng.Next(cfg.MinExtraOreDrops, cfg.MaxExtraOreDrops + 1);
            if (oreCount > 0)
            {
                string oreId = this.ChooseOreForLocation(location);
                this.SpawnItemDebris(location, tile, oreCount, new[] { oreId });
            }
        }

        private void DoSmallExplosion(GameLocation location, XnaVector2 centerTile, int damage, int radiusTiles, float knockback)
        {
            if (damage < 0) damage = 0;
            if (radiusTiles < 1) radiusTiles = 1;

            // sound + small shake
            location.playSound("explosion");
            Game1.screenGlowOnce(Color.OrangeRed, false);
            Game1.currentLocation?.temporarySprites.Add(new TemporaryAnimatedSprite(362, centerTile * 64f, Color.White, 8, false, 60f));

            // apply to player if close
            float distPlayer = Vector2.Distance(Game1.player!.Tile, centerTile);
            if (distPlayer <= radiusTiles)
            {
                int finalDmg = Math.Max(1, damage);
                Game1.player.takeDamage(finalDmg, overrideParry: true, damager: null);
                ApplyKnockbackToFarmer(Game1.player, centerTile, knockback);
            }

            // apply to monsters
            foreach (Monster m in location.characters.OfType<Monster>())
            {
                float d = Vector2.Distance(m.Tile, centerTile);
                if (d <= radiusTiles)
                {
                    // light damage; Monster.takeDamage signature varies—use Health directly safely
                    m.Health = Math.Max(0, m.Health - Math.Max(1, damage));
                    ApplyKnockbackToMonster(m, centerTile, knockback);
                }
            }
        }

        private static void ApplyKnockbackToFarmer(Farmer farmer, XnaVector2 sourceTile, float strength)
        {
            if (strength <= 0f)
                return;

            XnaVector2 dir = farmer.Tile - sourceTile;
            if (dir.LengthSquared() < 0.001f)
                dir = new XnaVector2(0f, -1f);
            dir.Normalize();

            farmer.xVelocity += dir.X * strength;
            farmer.yVelocity += dir.Y * strength;
        }

        private static void ApplyKnockbackToMonster(Monster monster, XnaVector2 sourceTile, float strength)
        {
            if (strength <= 0f)
                return;

            XnaVector2 dir = monster.Tile - sourceTile;
            if (dir.LengthSquared() < 0.001f)
                dir = new XnaVector2(0f, -1f);
            dir.Normalize();

            monster.xVelocity += dir.X * strength;
            monster.yVelocity += dir.Y * strength;
        }

        // --------------------
        // Weather: Lucky Veins (new)
        // --------------------

        private void UpdateLuckyVeins(GameLocation location, MineLocationKind kind, UpdateTickedEventArgs e)
        {
            var cfg = this.Config.LuckyVeins;

            // occasional "glint" near the player to hint at ore
            if (e.IsMultipleOf(cfg.GlintIntervalTicks))
            {
                XnaVector2 playerPos = Game1.player!.Position;
                XnaVector2 pos = playerPos + new XnaVector2(this.Rng.Next(-7, 8) * 64f, this.Rng.Next(-4, 5) * 64f);

                var sprite = new TemporaryAnimatedSprite(
                    "LooseSprites\\Cursors",
                    new XnaRectangle(372, 1956, 8, 8),
                    60f,
                    4,
                    2,
                    pos,
                    false,
                    this.Rng.NextDouble() < 0.5
                )
                {
                    motion = new XnaVector2(
                        0.2f * (this.Rng.NextDouble() < 0.5 ? -1f : 1f),
                        (float)(this.Rng.NextDouble() - 0.5) * 0.2f
                    ),
                    layerDepth = (pos.Y + 32f) / 10000f,
                    color = Color.LightGoldenrodYellow
                };

                location.temporarySprites.Add(sprite);
            }

            // coal bonus (stable simulation): occasionally you "find" coal while mining around
            if (cfg.CoalFindChancePerPulse > 0 && e.IsMultipleOf(cfg.CoalPulseIntervalTicks))
            {
                if (Game1.ticks == this.LastLuckyPulseTick)
                    return;
                this.LastLuckyPulseTick = Game1.ticks;

                if (this.Rng.NextDouble() < cfg.CoalFindChancePerPulse)
                {
                    this.SpawnItemDebris(location, Game1.player!.Tile, 1, new[] { "(O)382" }); // coal
                    location.playSound("coin");
                }
            }
        }

        private void SpawnLuckyVeinsOreFinds(GameLocation location, MineLocationKind kind, int count)
        {
            if (count <= 0)
                return;

            string oreId = this.ChooseOreForLocation(location);
            this.SpawnItemDebris(location, Game1.player!.Tile, count, new[] { oreId });
        }

        // --------------------
        // Shared helpers
        // --------------------

        private void SpawnItemDebris(GameLocation location, XnaVector2 tile, int count, string[] possibleItemIds)
        {
            if (count <= 0 || possibleItemIds.Length == 0)
                return;

            for (int i = 0; i < count; i++)
            {
                string itemId = possibleItemIds[this.Rng.Next(possibleItemIds.Length)];
                Item? item = ItemRegistry.Create(itemId);
                if (item is null)
                    continue;

                XnaVector2 offsetTile = tile + new XnaVector2(this.Rng.Next(-1, 2), this.Rng.Next(-1, 2));
                XnaVector2 pos = offsetTile * 64f;

                location.debris.Add(new Debris(item, pos));
            }
        }

        private string ChooseOreForLocation(GameLocation location)
        {
            // safe heuristic based on mine type & depth (no brittle object constants)
            if (location is MineShaft shaft)
            {
                int level = shaft.mineLevel;
                if (level >= 80)
                    return "(O)384"; // gold ore
                if (level >= 40)
                    return "(O)380"; // iron ore
                return "(O)378";     // copper ore
            }

            if (string.Equals(location.NameOrUniqueName, "SkullCave", StringComparison.OrdinalIgnoreCase))
                return "(O)386"; // iridium ore (Skull Cavern vibe)

            if (string.Equals(location.NameOrUniqueName, "VolcanoDungeon", StringComparison.OrdinalIgnoreCase))
                return "(O)384"; // gold-ish baseline; can customize later

            return "(O)378";
        }

        // --------------------
        // Buff: defense compensation
        // --------------------

        private void UpdatePlayerDayBuff(bool inMine, MineLocationKind kind)
        {
            if (Game1.player is null)
                return;

            int defenseDelta = 0;

            if (inMine && this.TodayCaveWeather != CaveWeatherType.None && this.IsWeatherAllowedHere(this.TodayCaveWeather, kind))
            {
                switch (this.TodayCaveWeather)
                {
                    case CaveWeatherType.BerserkerDay:
                        defenseDelta = this.Config.BerserkerDay.PlayerDefenseBonus;
                        break;

                    case CaveWeatherType.FrenzyFog:
                        defenseDelta = this.Config.FrenzyFog.PlayerDefenseBonus;
                        break;

                    case CaveWeatherType.BloodthirstWinds:
                        defenseDelta = -this.Config.BloodthirstWinds.PlayerDefensePenalty;
                        break;
                }
            }

            BuffEffects effects = new BuffEffects();
            if (defenseDelta != 0)
                effects.Defense.Add(defenseDelta);

            Buff buff = new Buff(
                id: DayBuffId,
                displayName: this.Helper.Translation.Get("buff.daybuff.name"),
                iconTexture: Game1.buffsIcons,
                iconSheetIndex: 0,
                duration: Buff.ENDLESS,
                effects: effects
            )
            {
                visible = false
            };

            Game1.player.applyBuff(buff);
        }

        // --------------------
        // Location routing
        // --------------------

        internal enum MineLocationKind
        {
            Mines,
            SkullCavern,
            VolcanoDungeon
        }

        private bool IsMineLikeLocation(GameLocation location, out MineLocationKind kind)
        {
            kind = MineLocationKind.Mines;

            if (location is MineShaft)
            {
                // Skull Cavern is also MineShaft; identify by name
                if (string.Equals(location.NameOrUniqueName, "SkullCave", StringComparison.OrdinalIgnoreCase))
                {
                    kind = MineLocationKind.SkullCavern;
                    return true;
                }

                kind = MineLocationKind.Mines;
                return true;
            }

            if (string.Equals(location.NameOrUniqueName, "VolcanoDungeon", StringComparison.OrdinalIgnoreCase))
            {
                kind = MineLocationKind.VolcanoDungeon;
                return true;
            }

            return false;
        }

        private bool IsWeatherAllowedHere(CaveWeatherType type, MineLocationKind kind)
        {
            ModConfigWeather cfg = type switch
            {
                CaveWeatherType.FungalHarvest => this.Config.FungalHarvest,
                CaveWeatherType.TemporalFlux => this.Config.TemporalFlux,
                CaveWeatherType.BerserkerDay => this.Config.BerserkerDay,
                CaveWeatherType.FrenzyFog => this.Config.FrenzyFog,
                CaveWeatherType.BloodthirstWinds => this.Config.BloodthirstWinds,
                CaveWeatherType.UnstableVeins => this.Config.UnstableVeins,
                CaveWeatherType.LuckyVeins => this.Config.LuckyVeins,
                _ => this.Config.FungalHarvest
            };

            if (!cfg.Enabled)
                return false;

            return kind switch
            {
                MineLocationKind.Mines => cfg.EnableInMines,
                MineLocationKind.SkullCavern => cfg.EnableInSkullCavern,
                MineLocationKind.VolcanoDungeon => cfg.EnableInVolcanoDungeon,
                _ => false
            };
        }

        // --------------------
        // GMCM
        // --------------------

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm is null)
                return;

            gmcm.Register(
                mod: this.ModManifest,
                reset: () => this.Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.Config),
                titleScreenOnly: false
            );

            gmcm.AddSectionTitle(
                this.ModManifest,
                () => this.Helper.Translation.Get("config.section.main"),
                () => this.Helper.Translation.Get("config.section.main.tooltip")
            );

            gmcm.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => (int)(this.Config.DailyCaveWeatherChance * 100),
                setValue: v => this.Config.DailyCaveWeatherChance = Math.Clamp(v, 0, 100) / 100.0,
                name: () => this.Helper.Translation.Get("config.dailyChance.name"),
                tooltip: () => this.Helper.Translation.Get("config.dailyChance.tooltip"),
                min: 0,
                max: 100,
                interval: 5,
                formatValue: v => $"{v}%",
                fieldId: "DailyChance"
            );

            gmcm.AddSectionTitle(
                this.ModManifest,
                () => this.Helper.Translation.Get("config.section.weathers"),
                () => this.Helper.Translation.Get("config.section.weathers.tooltip")
            );

            // Add each weather block
            this.AddWeatherConfig(gmcm, CaveWeatherType.FungalHarvest, this.Config.FungalHarvest, "fungal");
            this.AddWeatherConfig(gmcm, CaveWeatherType.TemporalFlux, this.Config.TemporalFlux, "temporal");
            this.AddWeatherConfig(gmcm, CaveWeatherType.BerserkerDay, this.Config.BerserkerDay, "berserker");
            this.AddWeatherConfig(gmcm, CaveWeatherType.FrenzyFog, this.Config.FrenzyFog, "frenzy");
            this.AddWeatherConfig(gmcm, CaveWeatherType.BloodthirstWinds, this.Config.BloodthirstWinds, "bloodthirst");
            this.AddWeatherConfig(gmcm, CaveWeatherType.UnstableVeins, this.Config.UnstableVeins, "unstable");
            this.AddWeatherConfig(gmcm, CaveWeatherType.LuckyVeins, this.Config.LuckyVeins, "lucky");

            gmcm.AddSectionTitle(
                this.ModManifest,
                () => this.Helper.Translation.Get("config.section.tuning"),
                () => this.Helper.Translation.Get("config.section.tuning.tooltip")
            );

            // Tuning options (numbers)
            // Fungal
            gmcm.AddNumberOption(this.ModManifest,
                () => this.Config.FungalHarvest.ExtraMushroomsPerFloor,
                v => this.Config.FungalHarvest.ExtraMushroomsPerFloor = Math.Max(0, v),
                () => this.Helper.Translation.Get("config.fungal.extraMushrooms.name"),
                () => this.Helper.Translation.Get("config.fungal.extraMushrooms.tooltip"),
                0, 20, 1, null, "FungalExtraMushrooms");

            gmcm.AddNumberOption(this.ModManifest,
                () => (int)(this.Config.FungalHarvest.SlimeDropChance * 100),
                v => this.Config.FungalHarvest.SlimeDropChance = Math.Clamp(v, 0, 100) / 100.0,
                () => this.Helper.Translation.Get("config.fungal.slimeDropChance.name"),
                () => this.Helper.Translation.Get("config.fungal.slimeDropChance.tooltip"),
                0, 100, 5, v => $"{v}%", "FungalSlimeDropChance");

            // Temporal
            gmcm.AddNumberOption(this.ModManifest,
                () => (int)(this.Config.TemporalFlux.TimeScale * 100),
                v => this.Config.TemporalFlux.TimeScale = Math.Clamp(v, 10, 200) / 100.0,
                () => this.Helper.Translation.Get("config.temporal.timeScale.name"),
                () => this.Helper.Translation.Get("config.temporal.timeScale.tooltip"),
                10, 200, 10, v => $"{v}%", "TemporalTimeScale");

            // Berserker
            gmcm.AddNumberOption(this.ModManifest,
                () => this.Config.BerserkerDay.MonsterSpeedBonus,
                v => this.Config.BerserkerDay.MonsterSpeedBonus = Math.Max(0, v),
                () => this.Helper.Translation.Get("config.berserker.speedBonus.name"),
                () => this.Helper.Translation.Get("config.berserker.speedBonus.tooltip"),
                0, 10, 1, null, "BerserkerSpeedBonus");

            gmcm.AddNumberOption(this.ModManifest,
                () => this.Config.BerserkerDay.MonsterAggroTilesBonus,
                v => this.Config.BerserkerDay.MonsterAggroTilesBonus = Math.Max(0, v),
                () => this.Helper.Translation.Get("config.berserker.aggroBonus.name"),
                () => this.Helper.Translation.Get("config.berserker.aggroBonus.tooltip"),
                0, 10, 1, null, "BerserkerAggroBonus");

            gmcm.AddNumberOption(this.ModManifest,
                () => this.Config.BerserkerDay.PlayerDefenseBonus,
                v => this.Config.BerserkerDay.PlayerDefenseBonus = v,
                () => this.Helper.Translation.Get("config.berserker.playerDefense.name"),
                () => this.Helper.Translation.Get("config.berserker.playerDefense.tooltip"),
                -10, 10, 1, null, "BerserkerPlayerDefense");

            // Frenzy
            gmcm.AddNumberOption(this.ModManifest,
                () => this.Config.FrenzyFog.MonsterSpeedBonus,
                v => this.Config.FrenzyFog.MonsterSpeedBonus = Math.Max(0, v),
                () => this.Helper.Translation.Get("config.frenzy.monsterSpeed.name"),
                () => this.Helper.Translation.Get("config.frenzy.monsterSpeed.tooltip"),
                0, 5, 1, null, "FrenzyMonsterSpeed");

            gmcm.AddNumberOption(this.ModManifest,
                () => this.Config.FrenzyFog.PlayerDefenseBonus,
                v => this.Config.FrenzyFog.PlayerDefenseBonus = v,
                () => this.Helper.Translation.Get("config.frenzy.playerDefense.name"),
                () => this.Helper.Translation.Get("config.frenzy.playerDefense.tooltip"),
                -10, 10, 1, null, "FrenzyPlayerDefense");

            gmcm.AddNumberOption(this.ModManifest,
                () => this.Config.FrenzyFog.FogOpacity,
                v => this.Config.FrenzyFog.FogOpacity = Math.Clamp(v, 0, 255),
                () => this.Helper.Translation.Get("config.frenzy.fogOpacity.name"),
                () => this.Helper.Translation.Get("config.frenzy.fogOpacity.tooltip"),
                0, 255, 15, null, "FrenzyFogOpacity");

            // Bloodthirst
            gmcm.AddNumberOption(this.ModManifest,
                () => this.Config.BloodthirstWinds.PlayerDefensePenalty,
                v => this.Config.BloodthirstWinds.PlayerDefensePenalty = Math.Max(0, v),
                () => this.Helper.Translation.Get("config.bloodthirst.defPenalty.name"),
                () => this.Helper.Translation.Get("config.bloodthirst.defPenalty.tooltip"),
                0, 10, 1, null, "BloodthirstDefPenalty");

            gmcm.AddNumberOption(this.ModManifest,
                () => (int)this.Config.BloodthirstWinds.HealIntervalTicks,
                v => this.Config.BloodthirstWinds.HealIntervalTicks = (uint)Math.Max(1, v),
                () => this.Helper.Translation.Get("config.bloodthirst.healInterval.name"),
                () => this.Helper.Translation.Get("config.bloodthirst.healInterval.tooltip"),
                10, 600, 10, null, "BloodthirstHealInterval");

            gmcm.AddNumberOption(this.ModManifest,
                () => this.Config.BloodthirstWinds.HealAmount,
                v => this.Config.BloodthirstWinds.HealAmount = Math.Max(0, v),
                () => this.Helper.Translation.Get("config.bloodthirst.healAmount.name"),
                () => this.Helper.Translation.Get("config.bloodthirst.healAmount.tooltip"),
                0, 10, 1, null, "BloodthirstHealAmount");

            // Unstable
            gmcm.AddNumberOption(this.ModManifest,
                () => (int)(this.Config.UnstableVeins.ExplosionPulseChance * 100),
                v => this.Config.UnstableVeins.ExplosionPulseChance = Math.Clamp(v, 0, 100) / 100.0,
                () => this.Helper.Translation.Get("config.unstable.pulseChance.name"),
                () => this.Helper.Translation.Get("config.unstable.pulseChance.tooltip"),
                0, 100, 5, vv => $"{vv}%", "UnstablePulseChance");

            gmcm.AddNumberOption(this.ModManifest,
                () => this.Config.UnstableVeins.ExplosionDamage,
                v => this.Config.UnstableVeins.ExplosionDamage = Math.Max(0, v),
                () => this.Helper.Translation.Get("config.unstable.damage.name"),
                () => this.Helper.Translation.Get("config.unstable.damage.tooltip"),
                0, 50, 1, null, "UnstableDamage");

            gmcm.AddNumberOption(this.ModManifest,
                () => this.Config.UnstableVeins.MinExtraOreDrops,
                v => this.Config.UnstableVeins.MinExtraOreDrops = Math.Clamp(v, 0, 20),
                () => this.Helper.Translation.Get("config.unstable.minOre.name"),
                () => this.Helper.Translation.Get("config.unstable.minOre.tooltip"),
                0, 20, 1, null, "UnstableMinOre");

            gmcm.AddNumberOption(this.ModManifest,
                () => this.Config.UnstableVeins.MaxExtraOreDrops,
                v => this.Config.UnstableVeins.MaxExtraOreDrops = Math.Clamp(v, 0, 30),
                () => this.Helper.Translation.Get("config.unstable.maxOre.name"),
                () => this.Helper.Translation.Get("config.unstable.maxOre.tooltip"),
                0, 30, 1, null, "UnstableMaxOre");

            // Lucky
            gmcm.AddNumberOption(this.ModManifest,
                () => this.Config.LuckyVeins.ExtraOreFindsPerFloor,
                v => this.Config.LuckyVeins.ExtraOreFindsPerFloor = Math.Max(0, v),
                () => this.Helper.Translation.Get("config.lucky.extraOreFinds.name"),
                () => this.Helper.Translation.Get("config.lucky.extraOreFinds.tooltip"),
                0, 20, 1, null, "LuckyExtraOreFinds");

            gmcm.AddNumberOption(this.ModManifest,
                () => (int)(this.Config.LuckyVeins.CoalFindChancePerPulse * 100),
                v => this.Config.LuckyVeins.CoalFindChancePerPulse = Math.Clamp(v, 0, 100) / 100.0,
                () => this.Helper.Translation.Get("config.lucky.coalChance.name"),
                () => this.Helper.Translation.Get("config.lucky.coalChance.tooltip"),
                0, 100, 5, vv => $"{vv}%", "LuckyCoalChance");
        }

        private void AddWeatherConfig(IGenericModConfigMenuApi gmcm, CaveWeatherType t, ModConfigWeather w, string prefix)
        {
            // title line per weather
            gmcm.AddSectionTitle(
                this.ModManifest,
                () => this.Helper.Translation.Get($"config.weather.{prefix}.title"),
                () => this.Helper.Translation.Get($"config.weather.{prefix}.tooltip")
            );

            gmcm.AddBoolOption(
                this.ModManifest,
                () => w.Enabled,
                v => w.Enabled = v,
                () => this.Helper.Translation.Get($"config.weather.{prefix}.enabled.name"),
                () => this.Helper.Translation.Get($"config.weather.{prefix}.enabled.tooltip"),
                $"Enable_{prefix}"
            );

            gmcm.AddBoolOption(
                this.ModManifest,
                () => w.EnableInMines,
                v => w.EnableInMines = v,
                () => this.Helper.Translation.Get("config.location.mines"),
                () => this.Helper.Translation.Get("config.location.mines.tooltip"),
                $"LocMines_{prefix}"
            );

            gmcm.AddBoolOption(
                this.ModManifest,
                () => w.EnableInSkullCavern,
                v => w.EnableInSkullCavern = v,
                () => this.Helper.Translation.Get("config.location.skull"),
                () => this.Helper.Translation.Get("config.location.skull.tooltip"),
                $"LocSkull_{prefix}"
            );

            gmcm.AddBoolOption(
                this.ModManifest,
                () => w.EnableInVolcanoDungeon,
                v => w.EnableInVolcanoDungeon = v,
                () => this.Helper.Translation.Get("config.location.volcano"),
                () => this.Helper.Translation.Get("config.location.volcano.tooltip"),
                $"LocVolcano_{prefix}"
            );
        }
    }

    // --------------------------------------------------------------------
    // 1.6 compatibility helpers for removed tile methods
    // --------------------------------------------------------------------
    internal static class GameLocationExtensionsCompat
    {
        public static bool isTileOccupied(this GameLocation location, XnaVector2 tileLocation)
        {
            return location.IsTileOccupiedBy(tileLocation, CollisionMask.All);
        }

        public static bool isTileLocationTotallyClearAndPlaceable(this GameLocation location, XnaVector2 tileLocation)
        {
            if (!location.isTileOnMap(tileLocation))
                return false;

            if (location.IsTileOccupiedBy(tileLocation, CollisionMask.All))
                return false;

            var tile = new Location((int)tileLocation.X, (int)tileLocation.Y);
            if (!location.isTilePassable(tile, Game1.viewport))
                return false;

            return location.isTilePlaceable(tileLocation);
        }
    }
}
