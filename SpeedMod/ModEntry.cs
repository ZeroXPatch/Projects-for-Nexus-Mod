using System;
using GenericModConfigMenu;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buffs;

namespace SpeedMod
{
    public class ModEntry : Mod
    {
        private ModConfig Config = new();
        private const string BuffId = "SpeedMod.SpeedBuff";

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.Display.RenderedWorld += OnRenderedWorld;
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            // Run logic 4 times a second (every 15 ticks)
            if (!Context.IsWorldReady || Game1.player == null || !e.IsMultipleOf(15))
                return;

            ApplySpeedLogic();
        }

        private void ApplySpeedLogic()
        {
            // 1. Get the direct integer speed value
            int speedBonus = 0;

            if (this.Config.UseAdaptiveSpeed)
            {
                speedBonus = GetAdaptiveSpeed(Game1.timeOfDay);
            }
            else
            {
                speedBonus = this.Config.ConstantSpeed;
            }

            // 2. Manage the Buff
            // If the slider is at 0, remove the buff.
            if (speedBonus <= 0)
            {
                if (Game1.player.buffs.IsApplied(BuffId))
                {
                    Game1.player.buffs.Remove(BuffId);
                }
                return;
            }

            // 3. Check if we need to update the buff
            bool needsUpdate = true;
            if (Game1.player.buffs.IsApplied(BuffId))
            {
                var existingBuff = Game1.player.buffs.AppliedBuffs[BuffId];
                if (existingBuff.effects.Speed.Value == speedBonus)
                {
                    needsUpdate = false; // Already active with correct value
                }
            }

            // 4. Apply/Re-apply Buff
            if (needsUpdate)
            {
                Buff speedBuff = new Buff(
                    id: BuffId,
                    displayName: "Speed Mod",
                    iconTexture: null,
                    iconSheetIndex: 0,
                    duration: Buff.ENDLESS,
                    effects: new BuffEffects()
                    {
                        Speed = { speedBonus }
                    }
                );

                Game1.player.applyBuff(speedBuff);
            }
        }

        private int GetAdaptiveSpeed(int timeOfDay)
        {
            if (timeOfDay >= 600 && timeOfDay < 900) return this.Config.Speed_0600_to_0900;
            if (timeOfDay >= 900 && timeOfDay < 1200) return this.Config.Speed_0900_to_1200;
            if (timeOfDay >= 1200 && timeOfDay < 1700) return this.Config.Speed_1200_to_1700;
            if (timeOfDay >= 1700 && timeOfDay < 2400) return this.Config.Speed_1700_to_2400;
            if (timeOfDay >= 2400 || timeOfDay < 600) return this.Config.Speed_2400_to_2600;
            return 0;
        }

        private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
        {
            if (this.Config.DebugMode && Context.IsWorldReady && Game1.player != null && !Game1.eventUp)
            {
                Vector2 screenPos = Game1.GlobalToLocal(Game1.viewport, Game1.player.Position);
                Vector2 textPos = new Vector2(screenPos.X, screenPos.Y - 90);

                float currentBonus = 0;
                if (Game1.player.buffs.IsApplied(BuffId))
                {
                    currentBonus = Game1.player.buffs.AppliedBuffs[BuffId].effects.Speed.Value;
                }

                string debugText = $"Buff: +{currentBonus}\nReal: {Game1.player.getMovementSpeed():0.0}";
                e.SpriteBatch.DrawString(Game1.dialogueFont, debugText, textPos, Color.Red);
            }
        }

        // --- GMCM Registration ---
        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null) return;

            configMenu.Register(this.ModManifest, () => this.Config = new ModConfig(), () => this.Helper.WriteConfig(this.Config));

            // General
            configMenu.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("config.section.general"));

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => this.Config.DebugMode,
                setValue: val => this.Config.DebugMode = val,
                name: () => this.Helper.Translation.Get("config.debug.name"),
                tooltip: () => this.Helper.Translation.Get("config.debug.tooltip")
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => this.Config.UseAdaptiveSpeed,
                setValue: val => this.Config.UseAdaptiveSpeed = val,
                name: () => this.Helper.Translation.Get("config.adaptive.name"),
                tooltip: () => this.Helper.Translation.Get("config.adaptive.tooltip")
            );

            // Constant Mode
            configMenu.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("config.section.constant"));

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => this.Config.ConstantSpeed,
                setValue: val => this.Config.ConstantSpeed = val,
                name: () => this.Helper.Translation.Get("config.constant_speed.name"),
                tooltip: () => this.Helper.Translation.Get("config.constant_speed.tooltip"),
                min: 0, // Minimum +0 speed
                max: 20, // Maximum +20 speed
                interval: 1 // Steps of 1
            );

            // Adaptive Mode
            configMenu.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("config.section.adaptive"));

            AddSpeedSlider(configMenu, "config.time.6to9", () => this.Config.Speed_0600_to_0900, v => this.Config.Speed_0600_to_0900 = v);
            AddSpeedSlider(configMenu, "config.time.9to12", () => this.Config.Speed_0900_to_1200, v => this.Config.Speed_0900_to_1200 = v);
            AddSpeedSlider(configMenu, "config.time.12to5", () => this.Config.Speed_1200_to_1700, v => this.Config.Speed_1200_to_1700 = v);
            AddSpeedSlider(configMenu, "config.time.5to12", () => this.Config.Speed_1700_to_2400, v => this.Config.Speed_1700_to_2400 = v);
            AddSpeedSlider(configMenu, "config.time.12to2", () => this.Config.Speed_2400_to_2600, v => this.Config.Speed_2400_to_2600 = v);
        }

        // Updated Helper to use INT instead of FLOAT
        private void AddSpeedSlider(IGenericModConfigMenuApi api, string translationKey, Func<int> get, Action<int> set)
        {
            api.AddNumberOption(
                mod: this.ModManifest,
                getValue: get,
                setValue: set,
                name: () => this.Helper.Translation.Get(translationKey),
                min: 0,
                max: 20,
                interval: 1
            );
        }
    }
}