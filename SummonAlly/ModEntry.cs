using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Monsters;

namespace GhostAlly
{
    public class ModEntry : Mod
    {
        // Initialize with default to fix CS8618 warning
        public static ModConfig Config = new ModConfig();

        private Monster? CurrentGhost = null;
        private double SummonTimeStart = 0;

        public override void Entry(IModHelper helper)
        {
            Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.Input.ButtonsChanged += OnButtonsChanged;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.Player.Warped += OnPlayerWarped;
            helper.Events.GameLoop.DayEnding += OnDayEnding;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null) return;

            configMenu.Register(
                mod: this.ModManifest,
                reset: () => Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(Config)
            );

            // SECTION 1: Summon
            configMenu.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("config.section.summon"));
            configMenu.AddKeybindList(mod: this.ModManifest, getValue: () => Config.SummonKey, setValue: value => Config.SummonKey = value, name: () => this.Helper.Translation.Get("config.key"));
            configMenu.AddNumberOption(mod: this.ModManifest, getValue: () => Config.EnergyCost, setValue: value => Config.EnergyCost = value, name: () => this.Helper.Translation.Get("config.energy"), min: 0, max: 200);
            configMenu.AddNumberOption(mod: this.ModManifest, getValue: () => Config.DurationSeconds, setValue: value => Config.DurationSeconds = value, name: () => this.Helper.Translation.Get("config.duration"), tooltip: () => this.Helper.Translation.Get("config.duration.tooltip"), min: 10, max: 600);

            // SECTION 2: Stats
            configMenu.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("config.section.stats"));
            configMenu.AddNumberOption(mod: this.ModManifest, getValue: () => Config.GhostMaxHP, setValue: value => Config.GhostMaxHP = value, name: () => this.Helper.Translation.Get("config.hp"), min: 10, max: 2000);
            configMenu.AddNumberOption(mod: this.ModManifest, getValue: () => Config.GhostDamage, setValue: value => Config.GhostDamage = value, name: () => this.Helper.Translation.Get("config.damage"), min: 1, max: 500);
            configMenu.AddNumberOption(mod: this.ModManifest, getValue: () => Config.AttackCooldown, setValue: value => Config.AttackCooldown = value, name: () => this.Helper.Translation.Get("config.cooldown"), min: 0.1f, max: 5.0f);
            configMenu.AddNumberOption(mod: this.ModManifest, getValue: () => Config.DamageTakenMultiplier, setValue: value => Config.DamageTakenMultiplier = value, name: () => this.Helper.Translation.Get("config.armor"), tooltip: () => this.Helper.Translation.Get("config.armor.tooltip"), min: 0.0f, max: 5.0f);

            configMenu.AddBoolOption(mod: this.ModManifest, getValue: () => Config.ShowMessage, setValue: value => Config.ShowMessage = value, name: () => this.Helper.Translation.Get("config.message"));
        }

        private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
        {
            if (!Context.IsWorldReady) return;
            if (Config.SummonKey.JustPressed()) AttemptSummon();
        }

        private void OnDayEnding(object? sender, DayEndingEventArgs e)
        {
            RemoveCurrentGhost();
        }

        private void OnPlayerWarped(object? sender, WarpedEventArgs e)
        {
            RemoveCurrentGhost();
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady) return;

            if (CurrentGhost != null)
            {
                if (CurrentGhost.Health <= 0 || !Game1.currentLocation.characters.Contains(CurrentGhost))
                {
                    CurrentGhost = null;
                    return;
                }

                double now = Game1.currentGameTime.TotalGameTime.TotalSeconds;
                if (now - SummonTimeStart > Config.DurationSeconds)
                {
                    Game1.currentLocation.playSound("wand");
                    RemoveCurrentGhost();
                }
            }
        }

        private void AttemptSummon()
        {
            if (Game1.player.Stamina < Config.EnergyCost)
            {
                Game1.addHUDMessage(new HUDMessage(this.Helper.Translation.Get("hud.no_energy"), HUDMessage.error_type));
                return;
            }

            RemoveCurrentGhost();

            Vector2 spawnPos = GetBehindPlayerPosition();
            CurrentGhost = new AllyGhost(spawnPos);
            CurrentGhost.MaxHealth = Config.GhostMaxHP;
            CurrentGhost.Health = Config.GhostMaxHP;

            Game1.currentLocation.characters.Add(CurrentGhost);

            Game1.player.Stamina -= Config.EnergyCost;
            SummonTimeStart = Game1.currentGameTime.TotalGameTime.TotalSeconds;

            if (Config.ShowMessage)
                Game1.addHUDMessage(new HUDMessage(this.Helper.Translation.Get("hud.summoned"), HUDMessage.achievement_type));

            Game1.currentLocation.playSound("wand");
        }

        private void RemoveCurrentGhost()
        {
            if (CurrentGhost != null)
            {
                if (Game1.currentLocation != null && Game1.currentLocation.characters.Contains(CurrentGhost))
                    Game1.currentLocation.characters.Remove(CurrentGhost);
                CurrentGhost = null;
            }
        }

        private Vector2 GetBehindPlayerPosition()
        {
            Vector2 offset = Game1.player.FacingDirection switch
            {
                0 => new Vector2(0, 64),
                1 => new Vector2(-64, 0),
                2 => new Vector2(0, -64),
                3 => new Vector2(64, 0),
                _ => new Vector2(0, -64)
            };
            return Game1.player.Position + offset;
        }
    }

    // ==========================================
    // CUSTOM GHOST CLASS
    // ==========================================

    public class AllyGhost : Ghost
    {
        public float AttackCooldownTimer { get; set; } = 0f;

        public AllyGhost() : base() { }
        public AllyGhost(Vector2 position) : base(position)
        {
            this.DamageToFarmer = 0;
        }

        public override int takeDamage(int damage, int xTrajectory, int yTrajectory, bool isBomb, double addedPrecision, Farmer who)
        {
            if (who != null) return 0;
            int finalDamage = (int)(damage * ModEntry.Config.DamageTakenMultiplier);
            return base.takeDamage(finalDamage, xTrajectory, yTrajectory, isBomb, addedPrecision, who);
        }

        public override void behaviorAtGameTick(GameTime time)
        {
            RunAllyLogic(time);
        }

        private void RunAllyLogic(GameTime time)
        {
            this.focusedOnFarmers = false;

            if (AttackCooldownTimer > 0)
                AttackCooldownTimer -= (float)time.ElapsedGameTime.TotalSeconds;

            NPC? target = FindTarget();

            if (target != null)
            {
                MoveToAndAttack(target, time);
            }
            else
            {
                FollowPlayer(time);
            }
        }

        private NPC? FindTarget()
        {
            NPC? bestTarget = null;
            double closestDist = 99999;

            foreach (var npc in this.currentLocation.characters)
            {
                if (npc is Monster enemy && enemy != this && !(enemy is AllyGhost))
                {
                    double dist = Vector2.Distance(this.Position, enemy.Position);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        bestTarget = enemy;
                    }
                }
            }
            return bestTarget;
        }

        private void MoveToAndAttack(NPC target, GameTime time)
        {
            float dist = Vector2.Distance(this.Position, target.Position);

            if (dist > 10)
            {
                Vector2 dir = target.Position - this.Position;
                if (dir != Vector2.Zero) dir.Normalize();

                this.position.X += dir.X * 6f;
                this.position.Y += dir.Y * 6f;

                UpdateFacing(dir);
            }

            if (dist < 64 && AttackCooldownTimer <= 0)
            {
                if (target is Monster m && !m.isInvincible())
                {
                    PerformAttack(m);
                    AttackCooldownTimer = ModEntry.Config.AttackCooldown;
                }
            }

            this.position.Y += (float)Math.Sin(time.TotalGameTime.TotalMilliseconds / 100.0) * 1.5f;
        }

        private void PerformAttack(Monster target)
        {
            int baseDamage = ModEntry.Config.GhostDamage;
            int damageToDeal = baseDamage + Math.Max(0, target.resilience.Value);
            int hpBefore = target.Health;

            // 1. Deal Damage (Attributed to Player)
            // By passing Game1.player, the game handles XP and Monster Slayer goals automatically.
            target.takeDamage(
                damage: damageToDeal,
                xTrajectory: 0,
                yTrajectory: 0,
                isBomb: false,
                addedPrecision: 999.0,
                who: Game1.player
            );

            // 2. Extra Loot Check
            // If the monster died, we verify loot dropped. 
            // In rare cases (distance), we force it using the correct 1.6 API (ItemRegistry).
            if (target.Health <= 0 && hpBefore > 0)
            {
                ForceLoot(target);
            }
        }

        private void ForceLoot(Monster monster)
        {
            try
            {
                // Force Loot Drops (FIXED for 1.6: Uses ItemRegistry)
                if (monster.objectsToDrop.Count > 0)
                {
                    foreach (string itemId in monster.objectsToDrop)
                    {
                        // Create item object from string ID
                        Item drop = ItemRegistry.Create(itemId);
                        Game1.createItemDebris(drop, monster.Position, -1, monster.currentLocation);
                    }
                }
            }
            catch (Exception)
            {
                // Safety catch
            }
        }

        private void FollowPlayer(GameTime time)
        {
            Vector2 offset = Game1.player.FacingDirection switch
            {
                0 => new Vector2(0, 96),
                1 => new Vector2(-96, 0),
                2 => new Vector2(0, -96),
                3 => new Vector2(96, 0),
                _ => Vector2.Zero
            };

            Vector2 targetPos = Game1.player.Position + offset;
            float dist = Vector2.Distance(this.Position, targetPos);

            if (dist > 10f)
            {
                Vector2 dir = targetPos - this.Position;
                if (dir != Vector2.Zero) dir.Normalize();

                float speed = dist > 200 ? 7f : 4f;

                this.position.X += dir.X * speed;
                this.position.Y += dir.Y * speed;

                UpdateFacing(dir);
            }

            this.position.Y += (float)Math.Sin(time.TotalGameTime.TotalMilliseconds / 150.0) * 0.5f;
        }

        private void UpdateFacing(Vector2 dir)
        {
            if (Math.Abs(dir.X) > Math.Abs(dir.Y))
                this.FacingDirection = dir.X > 0 ? 1 : 3;
            else
                this.FacingDirection = dir.Y > 0 ? 2 : 0;
        }
    }
}