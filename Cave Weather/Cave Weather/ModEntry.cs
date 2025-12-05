using Microsoft.Xna.Framework;
using Netcode;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Buffs;
using StardewValley.Characters;
using StardewValley.Locations;
using StardewValley.Monsters;
using System;
using System.Collections.Generic;
using System.Linq;
using xTile.Dimensions;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;
// alias so we don't conflict with System.Numerics.Vector2 / xTile.Rectangle
using XnaVector2 = Microsoft.Xna.Framework.Vector2;

namespace CaveWeather
{
    internal enum CaveWeatherType
    {
        None,
        FungalHarvest,
        TemporalFlux,
        BerserkerDay,
        FrenzyFog,
        BloodthirstWinds
    }

    public sealed class ModEntry : Mod
    {
        private const string DayBuffId = "CaveWeather.DayBuff";

        private ModConfig Config = null!;
        private CaveWeatherType TodayCaveWeather = CaveWeatherType.None;
        private Random Random = null!;

        // track if we've already shown today's cave weather message
        private bool HasShownCaveMessageToday = false;

        // Berserker tracking
        private readonly Dictionary<Monster, int> MonsterLastHealth = new();
        private readonly HashSet<Monster> BerserkedMonsters = new();

        // Temporal Flux movement tracking
        private readonly Dictionary<Monster, int> TemporalBaseSpeed = new();
        private int TemporalFluxPhaseTicksRemaining;
        private bool TemporalFluxBurstPhase;

        // Frenzy Fog visual + speed tracking
        private bool DrawFrenzyFog;
        private readonly HashSet<Monster> FrenzyAdjustedMonsters = new();

        // mushroom types for Fungal Harvest
        private static readonly string[] FungalMushroomItemIds =
        {
            "(O)404", // Common Mushroom
            "(O)420", // Red Mushroom
            "(O)422"  // Purple Mushroom
        };

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();
            this.Random = new Random();

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.Player.Warped += this.OnWarped;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.Display.RenderedWorld += this.OnRenderedWorld;
        }

        // ------------------------
        // GMCM
        // ------------------------

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

            // main section
            gmcm.AddSectionTitle(
                mod: this.ModManifest,
                text: () => this.Helper.Translation.Get("config.section.main"),
                tooltip: () => this.Helper.Translation.Get("config.section.main.tooltip")
            );

            gmcm.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => (int)(this.Config.DailyCaveWeatherChance * 100),
                setValue: value =>
                {
                    value = Math.Clamp(value, 0, 100);
                    this.Config.DailyCaveWeatherChance = value / 100.0;
                },
                name: () => this.Helper.Translation.Get("config.dailyChance.name"),
                tooltip: () => this.Helper.Translation.Get("config.dailyChance.tooltip"),
                min: 0,
                max: 100,
                interval: 5,
                formatValue: v => $"{v}%",
                fieldId: "DailyChance"
            );

            // type toggles
            gmcm.AddSectionTitle(
                mod: this.ModManifest,
                text: () => this.Helper.Translation.Get("config.section.types"),
                tooltip: () => this.Helper.Translation.Get("config.section.types.tooltip")
            );

            gmcm.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => this.Config.EnableFungalHarvest,
                setValue: value => this.Config.EnableFungalHarvest = value,
                name: () => this.Helper.Translation.Get("config.enableFungalHarvest.name"),
                tooltip: () => this.Helper.Translation.Get("config.enableFungalHarvest.tooltip"),
                fieldId: "EnableFungalHarvest"
            );

            gmcm.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => this.Config.EnableTemporalFlux,
                setValue: value => this.Config.EnableTemporalFlux = value,
                name: () => this.Helper.Translation.Get("config.enableTemporalFlux.name"),
                tooltip: () => this.Helper.Translation.Get("config.enableTemporalFlux.tooltip"),
                fieldId: "EnableTemporalFlux"
            );

            gmcm.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => this.Config.EnableBerserkerDay,
                setValue: value => this.Config.EnableBerserkerDay = value,
                name: () => this.Helper.Translation.Get("config.enableBerserkerDay.name"),
                tooltip: () => this.Helper.Translation.Get("config.enableBerserkerDay.tooltip"),
                fieldId: "EnableBerserkerDay"
            );

            gmcm.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => this.Config.EnableFrenzyFog,
                setValue: value => this.Config.EnableFrenzyFog = value,
                name: () => this.Helper.Translation.Get("config.enableFrenzyFog.name"),
                tooltip: () => this.Helper.Translation.Get("config.enableFrenzyFog.tooltip"),
                fieldId: "EnableFrenzyFog"
            );

            gmcm.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => this.Config.EnableBloodthirstWinds,
                setValue: value => this.Config.EnableBloodthirstWinds = value,
                name: () => this.Helper.Translation.Get("config.enableBloodthirstWinds.name"),
                tooltip: () => this.Helper.Translation.Get("config.enableBloodthirstWinds.tooltip"),
                fieldId: "EnableBloodthirstWinds"
            );

            // intensity section
            gmcm.AddSectionTitle(
                mod: this.ModManifest,
                text: () => this.Helper.Translation.Get("config.section.intensity"),
                tooltip: () => this.Helper.Translation.Get("config.section.intensity.tooltip")
            );

            // Fungal Harvest
            gmcm.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => this.Config.FungalExtraMushroomsPerFloor,
                setValue: value => this.Config.FungalExtraMushroomsPerFloor = Math.Max(0, value),
                name: () => this.Helper.Translation.Get("config.fungalExtra.name"),
                tooltip: () => this.Helper.Translation.Get("config.fungalExtra.tooltip"),
                min: 0,
                max: 20,
                interval: 1,
                fieldId: "FungalExtra"
            );

            gmcm.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => (int)(this.Config.FungalSlimeDropChance * 100),
                setValue: value =>
                {
                    value = Math.Clamp(value, 0, 100);
                    this.Config.FungalSlimeDropChance = value / 100.0;
                },
                name: () => this.Helper.Translation.Get("config.fungalSlimeDropChance.name"),
                tooltip: () => this.Helper.Translation.Get("config.fungalSlimeDropChance.tooltip"),
                min: 0,
                max: 100,
                interval: 5,
                formatValue: v => $"{v}%",
                fieldId: "FungalSlimeDropChance"
            );

            // Temporal Flux
            gmcm.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => (int)(this.Config.TemporalFluxTimeScale * 100),
                setValue: value =>
                {
                    value = Math.Clamp(value, 10, 200);
                    this.Config.TemporalFluxTimeScale = value / 100.0;
                },
                name: () => this.Helper.Translation.Get("config.temporalTimeScale.name"),
                tooltip: () => this.Helper.Translation.Get("config.temporalTimeScale.tooltip"),
                min: 10,
                max: 200,
                interval: 10,
                formatValue: v => $"{v}%",
                fieldId: "TemporalTimeScale"
            );

            // Berserker Day
            gmcm.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => this.Config.BerserkerSpeedBonus,
                setValue: value => this.Config.BerserkerSpeedBonus = Math.Max(0, value),
                name: () => this.Helper.Translation.Get("config.berserkerSpeedBonus.name"),
                tooltip: () => this.Helper.Translation.Get("config.berserkerSpeedBonus.tooltip"),
                min: 0,
                max: 10,
                interval: 1,
                fieldId: "BerserkerSpeedBonus"
            );

            gmcm.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => this.Config.BerserkerAggroTilesBonus,
                setValue: value => this.Config.BerserkerAggroTilesBonus = Math.Max(0, value),
                name: () => this.Helper.Translation.Get("config.berserkerAggroBonus.name"),
                tooltip: () => this.Helper.Translation.Get("config.berserkerAggroBonus.tooltip"),
                min: 0,
                max: 10,
                interval: 1,
                fieldId: "BerserkerAggroBonus"
            );

            gmcm.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => this.Config.BerserkerPlayerDefenseBonus,
                setValue: value => this.Config.BerserkerPlayerDefenseBonus = value,
                name: () => this.Helper.Translation.Get("config.berserkerPlayerDefense.name"),
                tooltip: () => this.Helper.Translation.Get("config.berserkerPlayerDefense.tooltip"),
                min: -10,
                max: 10,
                interval: 1,
                fieldId: "BerserkerPlayerDefense"
            );

            // Frenzy Fog
            gmcm.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => this.Config.FrenzyFogMonsterSpeedBonus,
                setValue: value => this.Config.FrenzyFogMonsterSpeedBonus = Math.Max(0, value),
                name: () => this.Helper.Translation.Get("config.frenzyFogMonsterSpeedBonus.name"),
                tooltip: () => this.Helper.Translation.Get("config.frenzyFogMonsterSpeedBonus.tooltip"),
                min: 0,
                max: 5,
                interval: 1,
                fieldId: "FrenzyFogMonsterSpeedBonus"
            );

            gmcm.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => this.Config.FrenzyFogPlayerDefenseBonus,
                setValue: value => this.Config.FrenzyFogPlayerDefenseBonus = value,
                name: () => this.Helper.Translation.Get("config.frenzyFogPlayerDefense.name"),
                tooltip: () => this.Helper.Translation.Get("config.frenzyFogPlayerDefense.tooltip"),
                min: -10,
                max: 10,
                interval: 1,
                fieldId: "FrenzyFogPlayerDefense"
            );

            gmcm.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => this.Config.FrenzyFogOpacity,
                setValue: value => this.Config.FrenzyFogOpacity = Math.Clamp(value, 0, 255),
                name: () => this.Helper.Translation.Get("config.frenzyFogOpacity.name"),
                tooltip: () => this.Helper.Translation.Get("config.frenzyFogOpacity.tooltip"),
                min: 0,
                max: 255,
                interval: 15,
                fieldId: "FrenzyFogOpacity"
            );

            // Bloodthirst Winds
            gmcm.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => this.Config.BloodthirstPlayerDefensePenalty,
                setValue: value => this.Config.BloodthirstPlayerDefensePenalty = Math.Max(0, value),
                name: () => this.Helper.Translation.Get("config.bloodthirstDefensePenalty.name"),
                tooltip: () => this.Helper.Translation.Get("config.bloodthirstDefensePenalty.tooltip"),
                min: 0,
                max: 10,
                interval: 1,
                fieldId: "BloodthirstDefensePenalty"
            );

            gmcm.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => (int)this.Config.BloodthirstHealIntervalTicks,
                setValue: value => this.Config.BloodthirstHealIntervalTicks = (uint)Math.Max(1, value),
                name: () => this.Helper.Translation.Get("config.bloodthirstHealInterval.name"),
                tooltip: () => this.Helper.Translation.Get("config.bloodthirstHealInterval.tooltip"),
                min: 10,
                max: 600,
                interval: 10,
                fieldId: "BloodthirstHealInterval"
            );

            gmcm.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => this.Config.BloodthirstHealAmount,
                setValue: value => this.Config.BloodthirstHealAmount = Math.Max(0, value),
                name: () => this.Helper.Translation.Get("config.bloodthirstHealAmount.name"),
                tooltip: () => this.Helper.Translation.Get("config.bloodthirstHealAmount.tooltip"),
                min: 0,
                max: 10,
                interval: 1,
                fieldId: "BloodthirstHealAmount"
            );
        }

        // ------------------------
        // Events
        // ------------------------

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.player is null)
                return;

            this.HasShownCaveMessageToday = false;
            this.TodayCaveWeather = CaveWeatherType.None;

            this.MonsterLastHealth.Clear();
            this.BerserkedMonsters.Clear();
            this.TemporalBaseSpeed.Clear();
            this.FrenzyAdjustedMonsters.Clear();
            this.DrawFrenzyFog = false;
            this.ResetTemporalFluxPhase();

            if (this.Random.NextDouble() > this.Config.DailyCaveWeatherChance)
                return;

            var possible = new List<CaveWeatherType>();

            if (this.Config.EnableFungalHarvest)
                possible.Add(CaveWeatherType.FungalHarvest);
            if (this.Config.EnableTemporalFlux)
                possible.Add(CaveWeatherType.TemporalFlux);
            if (this.Config.EnableBerserkerDay)
                possible.Add(CaveWeatherType.BerserkerDay);
            if (this.Config.EnableFrenzyFog)
                possible.Add(CaveWeatherType.FrenzyFog);
            if (this.Config.EnableBloodthirstWinds)
                possible.Add(CaveWeatherType.BloodthirstWinds);

            if (possible.Count == 0)
                return;

            int index = this.Random.Next(possible.Count);
            this.TodayCaveWeather = possible[index];
        }

        private void OnWarped(object? sender, WarpedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.player is null)
                return;

            if (!ReferenceEquals(e.Player, Game1.player))
                return;

            if (!this.IsMineLikeLocation(e.NewLocation))
                return;

            if (this.TodayCaveWeather == CaveWeatherType.None)
                return;

            // --- MESSAGE: once per day on first entry to any mine-like location ---
            if (!this.HasShownCaveMessageToday)
            {
                string? messageKey = this.TodayCaveWeather switch
                {
                    CaveWeatherType.FungalHarvest => "message.fungal_harvest",
                    CaveWeatherType.TemporalFlux => "message.temporal_flux",
                    CaveWeatherType.BerserkerDay => "message.berserker_day",
                    CaveWeatherType.FrenzyFog => "message.frenzy_fog",
                    CaveWeatherType.BloodthirstWinds => "message.bloodthirst_winds",
                    _ => null
                };

                if (messageKey is not null)
                {
                    string text = this.Helper.Translation.Get(messageKey);
                    if (!string.IsNullOrWhiteSpace(text))
                        Game1.addHUDMessage(new HUDMessage(text, HUDMessage.newQuest_type));
                }

                this.HasShownCaveMessageToday = true;
            }

            // --- EFFECTS: every time you warp into a mine floor ---
            if (e.NewLocation is MineShaft mine)
            {
                switch (this.TodayCaveWeather)
                {
                    case CaveWeatherType.FungalHarvest:
                        this.SpawnMushroomsNearPlayer(mine, this.Config.FungalExtraMushroomsPerFloor);
                        break;

                    case CaveWeatherType.TemporalFlux:
                        this.TemporalBaseSpeed.Clear();
                        this.ResetTemporalFluxPhase();
                        break;

                    case CaveWeatherType.BerserkerDay:
                        this.MonsterLastHealth.Clear();
                        this.BerserkedMonsters.Clear();
                        break;

                    case CaveWeatherType.FrenzyFog:
                        this.FrenzyAdjustedMonsters.Clear();
                        this.DrawFrenzyFog = true;
                        break;

                    case CaveWeatherType.BloodthirstWinds:
                        // nothing special on enter, handled per tick
                        break;
                }
            }
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.player is null)
                return;

            GameLocation location = Game1.currentLocation;
            bool inMine = this.IsMineLikeLocation(location);

            // fog visual flag default
            this.DrawFrenzyFog = inMine && this.TodayCaveWeather == CaveWeatherType.FrenzyFog;

            // keep the day buff in sync
            this.UpdatePlayerDayBuff(inMine);

            if (!inMine)
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
            }
        }

        private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
        {
            if (!this.DrawFrenzyFog)
                return;

            // simple weak fog overlay using staminaRect
            var viewport = Game1.viewport;
            XnaRectangle rect = new XnaRectangle(0, 0, viewport.Width, viewport.Height);

            float alpha = this.Config.FrenzyFogOpacity / 255f;
            if (alpha <= 0f)
                return;

            e.SpriteBatch.Draw(
                Game1.staminaRect,
                rect,
                Color.White * alpha
            );
        }

        // ------------------------
        // Fungal Harvest
        // ------------------------

        private void SpawnMushroomsNearPlayer(GameLocation location, int count)
        {
            if (count <= 0)
                return;

            XnaVector2 playerTile = Game1.player!.Tile;

            for (int i = 0; i < count; i++)
            {
                // pick random tile in a small radius
                int attempts = 10;
                XnaVector2 tile = playerTile;

                while (attempts-- > 0)
                {
                    int x = (int)playerTile.X + this.Random.Next(-6, 7);
                    int y = (int)playerTile.Y + this.Random.Next(-4, 5);
                    XnaVector2 t = new XnaVector2(x, y);

                    if (location.isTileOnMap(t) &&
                        location.isTileLocationTotallyClearAndPlaceable(t) &&
                        !location.isTileOccupied(t))
                    {
                        tile = t;
                        break;
                    }
                }

                this.SpawnMushroomDebris(location, tile, 1);
            }
        }

        private void SpawnMushroomDebris(GameLocation location, XnaVector2 tile, int count)
        {
            if (count <= 0)
                return;

            for (int i = 0; i < count; i++)
            {
                string itemId = FungalMushroomItemIds[this.Random.Next(FungalMushroomItemIds.Length)];
                Item? item = ItemRegistry.Create(itemId);
                if (item is null)
                    continue;

                XnaVector2 offsetTile = tile + new XnaVector2(this.Random.Next(-1, 2), this.Random.Next(-1, 2));
                XnaVector2 pos = offsetTile * 64f;

                var debris = new Debris(item, pos);
                location.debris.Add(debris);
            }
        }

        private void UpdateFungalHarvest(GameLocation location, UpdateTickedEventArgs e)
        {
            if (!this.Config.EnableFungalHarvest)
                return;

            // ambient spores, every few ticks
            if (e.IsMultipleOf(this.Config.FungalSporeSpawnIntervalTicks))
            {
                XnaVector2 playerPos = Game1.player!.Position;

                for (int i = 0; i < this.Config.FungalSporeParticlesPerBurst; i++)
                {
                    float tileOffsetX = this.Random.Next(-8, 9);
                    float tileOffsetY = this.Random.Next(-4, 5);
                    XnaVector2 pos = playerPos + new XnaVector2(tileOffsetX * 64f, tileOffsetY * 64f);

                    var sprite = new TemporaryAnimatedSprite(
                        textureName: "LooseSprites\\Cursors",
                        sourceRect: new XnaRectangle(372, 1956, 8, 8),
                        animationInterval: 80f,
                        animationLength: 4,
                        numberOfLoops: this.Config.FungalSporeLoops,
                        position: pos,
                        flicker: false,
                        flipped: this.Random.NextDouble() < 0.5
                    )
                    {
                        motion = new XnaVector2(
                            0.25f * (this.Random.NextDouble() < 0.5 ? -1f : 1f),
                            (float)(this.Random.NextDouble() - 0.5) * 0.3f
                        ),
                        layerDepth = (pos.Y + 32f) / 10000f,
                        color = Color.LimeGreen
                    };

                    location.temporarySprites.Add(sprite);
                }
            }

            // slime mushroom "drops": once per second, chance per slime
            if (e.IsMultipleOf(60))
            {
                foreach (Monster monster in location.characters.OfType<Monster>())
                {
                    if (monster is GreenSlime or BigSlime)
                    {
                        if (this.Random.NextDouble() < this.Config.FungalSlimeDropChance)
                        {
                            XnaVector2 tile = monster.Tile;
                            this.SpawnMushroomDebris(location, tile, 1);
                        }
                    }
                }
            }
        }

        // ------------------------
        // Temporal Flux
        // ------------------------

        private void ResetTemporalFluxPhase()
        {
            this.TemporalFluxBurstPhase = true;
            this.TemporalFluxPhaseTicksRemaining = (int)this.Config.TemporalFluxPhaseDurationTicks;
        }

        private void UpdateTemporalFlux(GameLocation location, UpdateTickedEventArgs e)
        {
            if (!this.Config.EnableTemporalFlux)
                return;

            // 1) Time moves slower in the mines (approximation)
            if (this.Config.TemporalFluxTimeScale is > 0.0 and < 1.0)
            {
                var gameTime = Game1.currentGameTime;
                if (gameTime is not null)
                {
                    int elapsed = gameTime.ElapsedGameTime.Milliseconds;
                    int addBack = (int)(elapsed * (1.0 - this.Config.TemporalFluxTimeScale));
                    Game1.gameTimeInterval += addBack;
                }
            }

            // 2) Monster speed phases: burst vs slow
            if (this.TemporalFluxPhaseTicksRemaining <= 0)
            {
                this.TemporalFluxBurstPhase = !this.TemporalFluxBurstPhase;
                this.TemporalFluxPhaseTicksRemaining = (int)this.Config.TemporalFluxPhaseDurationTicks;
                this.TemporalBaseSpeed.Clear();
            }
            else
            {
                this.TemporalFluxPhaseTicksRemaining--;
            }

            double burstMult = this.Config.TemporalFluxBurstSpeedMultiplier;
            double slowMult = this.Config.TemporalFluxSlowSpeedMultiplier;

            foreach (Monster monster in location.characters.OfType<Monster>())
            {
                if (!this.TemporalBaseSpeed.TryGetValue(monster, out int baseSpeed))
                {
                    baseSpeed = monster.speed;
                    this.TemporalBaseSpeed[monster] = baseSpeed;
                }

                double mult = this.TemporalFluxBurstPhase ? burstMult : slowMult;
                int newSpeed = Math.Max(1, (int)Math.Round(baseSpeed * mult));
                monster.speed = newSpeed; // fixed: speed is an int, not NetInt
            }
        }

        // ------------------------
        // Berserker Day
        // ------------------------

        private void UpdateBerserkerDay(GameLocation location, UpdateTickedEventArgs e)
        {
            if (!this.Config.EnableBerserkerDay)
                return;

            // track health and detect "first time damaged"
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
                    // monster goes berserk
                    this.BerserkedMonsters.Add(monster);
                    monster.speed += this.Config.BerserkerSpeedBonus; // fixed

                    NetInt threshold = monster.moveTowardPlayerThreshold;
                    threshold.Value += this.Config.BerserkerAggroTilesBonus;
                }

                this.MonsterLastHealth[monster] = health;
            }

            // cleanup tracking for monsters that left the location
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

        // ------------------------
        // Frenzy Fog
        // ------------------------

        private void UpdateFrenzyFog(GameLocation location, UpdateTickedEventArgs e)
        {
            if (!this.Config.EnableFrenzyFog)
                return;

            // small flat speed boost to all monsters, only once per monster
            foreach (Monster monster in location.characters.OfType<Monster>())
            {
                if (this.FrenzyAdjustedMonsters.Contains(monster))
                    continue;

                monster.speed += this.Config.FrenzyFogMonsterSpeedBonus; // fixed
                this.FrenzyAdjustedMonsters.Add(monster);
            }
        }

        // ------------------------
        // Bloodthirst Winds
        // ------------------------

        private void UpdateBloodthirstWinds(GameLocation location, UpdateTickedEventArgs e)
        {
            if (!this.Config.EnableBloodthirstWinds)
                return;

            // mild self-heal aura on monsters
            if (this.Config.BloodthirstHealAmount > 0 &&
                e.IsMultipleOf(this.Config.BloodthirstHealIntervalTicks))
            {
                foreach (Monster monster in location.characters.OfType<Monster>())
                {
                    if (monster.Health <= 0)
                        continue;

                    int max = monster.MaxHealth;
                    if (monster.Health < max)
                    {
                        monster.Health = Math.Min(max, monster.Health + this.Config.BloodthirstHealAmount);
                    }
                }
            }

            // ambient red dust similar to spores, but rarer
            if (e.IsMultipleOf(90))
            {
                XnaVector2 playerPos = Game1.player!.Position;

                for (int i = 0; i < 4; i++)
                {
                    float tileOffsetX = this.Random.Next(-8, 9);
                    float tileOffsetY = this.Random.Next(-4, 5);
                    XnaVector2 pos = playerPos + new XnaVector2(tileOffsetX * 64f, tileOffsetY * 64f);

                    var sprite = new TemporaryAnimatedSprite(
                        textureName: "LooseSprites\\Cursors",
                        sourceRect: new XnaRectangle(372, 1956, 8, 8),
                        animationInterval: 80f,
                        animationLength: 4,
                        numberOfLoops: 2,
                        position: pos,
                        flicker: false,
                        flipped: this.Random.NextDouble() < 0.5
                    )
                    {
                        motion = new XnaVector2(
                            0.3f * (this.Random.NextDouble() < 0.5 ? -1f : 1f),
                            (float)(this.Random.NextDouble() - 0.5) * 0.4f
                        ),
                        layerDepth = (pos.Y + 32f) / 10000f,
                        color = Color.Red
                    };

                    location.temporarySprites.Add(sprite);
                }
            }
        }

        // ------------------------
        // Player buff per day
        // ------------------------

        private void UpdatePlayerDayBuff(bool inMine)
        {
            if (Game1.player is null)
                return;

            int defenseDelta = 0;

            if (inMine)
            {
                switch (this.TodayCaveWeather)
                {
                    case CaveWeatherType.BerserkerDay:
                        defenseDelta = this.Config.BerserkerPlayerDefenseBonus;
                        break;

                    case CaveWeatherType.FrenzyFog:
                        defenseDelta = this.Config.FrenzyFogPlayerDefenseBonus;
                        break;

                    case CaveWeatherType.BloodthirstWinds:
                        // penalty => negative defense
                        defenseDelta = -this.Config.BloodthirstPlayerDefensePenalty;
                        break;
                }
            }

            // always apply a buff with this ID; effects can be zero
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

        // ------------------------
        // Helpers
        // ------------------------

        private bool IsMineLikeLocation(GameLocation? location)
        {
            if (location is null)
                return false;

            if (location is MineShaft)
                return true;

            if (string.Equals(location.NameOrUniqueName, "VolcanoDungeon", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }
    }

    internal sealed class ModConfig
    {
        /// <summary>Chance per day (0–1) that any cave weather happens.</summary>
        public double DailyCaveWeatherChance { get; set; } = 0.25; // 25% per day

        public bool EnableFungalHarvest { get; set; } = true;
        public bool EnableTemporalFlux { get; set; } = true;
        public bool EnableBerserkerDay { get; set; } = true;
        public bool EnableFrenzyFog { get; set; } = true;
        public bool EnableBloodthirstWinds { get; set; } = true;

        // Fungal Harvest
        public int FungalExtraMushroomsPerFloor { get; set; } = 5;
        public double FungalSlimeDropChance { get; set; } = 0.20; // 20% per slime per second
        public uint FungalSporeSpawnIntervalTicks { get; set; } = 45;
        public int FungalSporeParticlesPerBurst { get; set; } = 4;
        public int FungalSporeLoops { get; set; } = 3;

        // Temporal Flux
        public double TemporalFluxTimeScale { get; set; } = 0.70; // 70% of normal speed
        public double TemporalFluxBurstSpeedMultiplier { get; set; } = 1.6;
        public double TemporalFluxSlowSpeedMultiplier { get; set; } = 0.5;
        public uint TemporalFluxPhaseDurationTicks { get; set; } = 60; // about 1 second per phase segment

        // Berserker Day
        public int BerserkerSpeedBonus { get; set; } = 2;
        public int BerserkerAggroTilesBonus { get; set; } = 4;
        public int BerserkerPlayerDefenseBonus { get; set; } = 2;

        // Frenzy Fog
        public int FrenzyFogMonsterSpeedBonus { get; set; } = 1;
        public int FrenzyFogPlayerDefenseBonus { get; set; } = 1;
        public int FrenzyFogOpacity { get; set; } = 80; // 0–255

        // Bloodthirst Winds
        public int BloodthirstPlayerDefensePenalty { get; set; } = 2;
        public uint BloodthirstHealIntervalTicks { get; set; } = 120;
        public int BloodthirstHealAmount { get; set; } = 1;
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

    // --------------------------------------------------------------------
    // GMCM API (minimal subset we use)
    // --------------------------------------------------------------------
    public interface IGenericModConfigMenuApi
    {
        void Register(
            IManifest mod,
            Action reset,
            Action save,
            bool titleScreenOnly = false
        );

        void AddSectionTitle(
            IManifest mod,
            Func<string> text,
            Func<string>? tooltip = null
        );

        void AddNumberOption(
            IManifest mod,
            Func<int> getValue,
            Action<int> setValue,
            Func<string> name,
            Func<string>? tooltip = null,
            int? min = null,
            int? max = null,
            int? interval = null,
            Func<int, string>? formatValue = null,
            string? fieldId = null
        );

        void AddBoolOption(
            IManifest mod,
            Func<bool> getValue,
            Action<bool> setValue,
            Func<string> name,
            Func<string>? tooltip = null,
            string? fieldId = null
        );
    }
}
