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
        private GameLocation? GhostLocation = null; // Track WHICH location the ghost is in
        private double SummonTimeStart = 0;

        public override void Entry(IModHelper helper)
        {
            Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.Input.ButtonsChanged += OnButtonsChanged;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.Player.Warped += OnPlayerWarped;
            helper.Events.GameLoop.DayEnding += OnDayEnding;

            // FIX 1: Always remove the ghost before the save serializer runs.
            // This is the primary safety net — without it, AllyGhost in any
            // location's characters list will crash the XML serializer.
            helper.Events.GameLoop.Saving += OnSaving;
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

        // FIX 1 handler: remove ghost before XML serialization begins.
        private void OnSaving(object? sender, SavingEventArgs e)
        {
            RemoveCurrentGhost();
        }

        private void OnPlayerWarped(object? sender, WarpedEventArgs e)
        {
            // FIX 2: After a warp, Game1.currentLocation is already the NEW map.
            // We must remove the ghost from the OLD location it was actually added to.
            RemoveCurrentGhost(e.OldLocation);
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady) return;

            if (CurrentGhost != null)
            {
                if (CurrentGhost.Health <= 0 || GhostLocation == null || !GhostLocation.characters.Contains(CurrentGhost))
                {
                    CurrentGhost = null;
                    GhostLocation = null;
                    return;
                }

                double now = Game1.currentGameTime.TotalGameTime.TotalSeconds;
                if (now - SummonTimeStart > Config.DurationSeconds)
                {
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

            GhostLocation = Game1.currentLocation; // Remember where we placed it
            GhostLocation.characters.Add(CurrentGhost);

            Game1.player.Stamina -= Config.EnergyCost;
            SummonTimeStart = Game1.currentGameTime.TotalGameTime.TotalSeconds;

            if (Config.ShowMessage)
                Game1.addHUDMessage(new HUDMessage(this.Helper.Translation.Get("hud.summoned"), HUDMessage.achievement_type));
        }

        /// <summary>
        /// Removes the current ghost. Pass a specific location to target (e.g. on warp),
        /// or leave null to use the tracked GhostLocation.
        /// </summary>
        private void RemoveCurrentGhost(GameLocation? location = null)
        {
            if (CurrentGhost != null)
            {
                // Use the explicitly supplied location first, then fall back to the
                // tracked spawn location. Never blindly use Game1.currentLocation —
                // that's the bug that caused the crash on warp and during save.
                GameLocation? target = location ?? GhostLocation;

                if (target != null && target.characters.Contains(CurrentGhost))
                    target.characters.Remove(CurrentGhost);

                CurrentGhost = null;
                GhostLocation = null;
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
        private float DamageCooldownTimer = 0f;
        private const float DAMAGE_COOLDOWN = 1.0f; // seconds between taking hits from proximity

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

            if (DamageCooldownTimer > 0)
                DamageCooldownTimer -= (float)time.ElapsedGameTime.TotalSeconds;

            // Monsters never call takeDamage on non-Farmer characters — we handle it manually.
            TakeProximityDamage();

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

        private void TakeProximityDamage()
        {
            if (DamageCooldownTimer > 0) return;

            foreach (var npc in this.currentLocation.characters)
            {
                if (npc is Monster enemy && enemy != this && !(enemy is AllyGhost))
                {
                    if (Vector2.Distance(this.Position, enemy.Position) < 70f)
                    {
                        // Route through our takeDamage override so DamageTakenMultiplier applies.
                        // who=null signals "monster source" and bypasses the player-hit guard.
                        int raw = Math.Max(1, enemy.DamageToFarmer);
                        this.takeDamage(raw, 0, 0, false, 0.0, (Farmer)null!);
                        DamageCooldownTimer = DAMAGE_COOLDOWN;
                        break;
                    }
                }
            }
        }

        private void UpdateFacing(Vector2 dir)
        {
            // Ghost sprites only visually differ left/right (horizontal flip).
            // Up/down uses the same frame, so setting FacingDirection 0 or 2 just
            // makes the ghost appear to face away from travel direction.
            // Only update facing when horizontal movement clearly dominates AND
            // is above a small threshold — this prevents the Y sine-bob from
            // ever flipping the facing direction between frames.
            if (Math.Abs(dir.X) > Math.Abs(dir.Y) && Math.Abs(dir.X) > 0.15f)
                this.FacingDirection = dir.X > 0 ? 1 : 3;
            // If movement is vertical-dominant or near-zero, keep current facing unchanged.
        }
    }
}