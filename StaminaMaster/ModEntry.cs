using System;
using GenericModConfigMenu;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace StaminaMaster
{
    public class ModEntry : Mod
    {
        private ModConfig Config = new();
        private float _lastStamina = 0;
        private bool _isFirstTick = true;

        // Counter for the frequency setting
        private int _secondsCounter = 0;

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.Display.RenderedWorld += OnRenderedWorld;
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.player == null || Game1.paused)
                return;

            // Initialize to prevent huge spikes on game load
            if (_isFirstTick)
            {
                _lastStamina = Game1.player.stamina;
                _isFirstTick = false;
                return;
            }

            int currentRegen = this.Config.ConstantRegen;
            int currentDrainReduction = this.Config.ConstantDrainReduction;

            if (this.Config.UseAdaptive)
            {
                currentRegen = GetAdaptiveValue(Game1.timeOfDay, "regen");
                currentDrainReduction = GetAdaptiveValue(Game1.timeOfDay, "drain");
            }

            // --- Drain Logic (Immediate) ---
            // We keep this running every tick because "Drain Reduction" needs to feel instant.
            if (Game1.player.stamina < _lastStamina)
            {
                float lostAmount = _lastStamina - Game1.player.stamina;

                if (currentDrainReduction > 0 && lostAmount > 0)
                {
                    float refund = lostAmount * (currentDrainReduction / 100f);
                    Game1.player.stamina += refund;
                }
            }

            // --- Regen Logic (Timed Interval) ---
            // Only count up if the game hits a full second
            if (e.IsOneSecond)
            {
                _secondsCounter++;

                // Check if we hit the user's set frequency (e.g., 5 seconds)
                if (_secondsCounter >= this.Config.RegenFrequencySeconds)
                {
                    if (currentRegen > 0 && Game1.player.stamina < Game1.player.MaxStamina)
                    {
                        Game1.player.stamina += currentRegen;

                        // Prevent overfilling
                        if (Game1.player.stamina > Game1.player.MaxStamina)
                            Game1.player.stamina = Game1.player.MaxStamina;
                    }

                    // Reset timer
                    _secondsCounter = 0;
                }
            }

            _lastStamina = Game1.player.stamina;
        }

        private int GetAdaptiveValue(int timeOfDay, string type)
        {
            if (timeOfDay >= 600 && timeOfDay < 900) return type == "regen" ? this.Config.Regen_0600_to_0900 : this.Config.Drain_0600_to_0900;
            if (timeOfDay >= 900 && timeOfDay < 1200) return type == "regen" ? this.Config.Regen_0900_to_1200 : this.Config.Drain_0900_to_1200;
            if (timeOfDay >= 1200 && timeOfDay < 1700) return type == "regen" ? this.Config.Regen_1200_to_1700 : this.Config.Drain_1200_to_1700;
            if (timeOfDay >= 1700 && timeOfDay < 2400) return type == "regen" ? this.Config.Regen_1700_to_2400 : this.Config.Drain_1700_to_2400;
            if (timeOfDay >= 2400 || timeOfDay < 600) return type == "regen" ? this.Config.Regen_2400_to_2600 : this.Config.Drain_2400_to_2600;
            return 0;
        }

        // --- DEBUG MODE ---
        private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
        {
            if (this.Config.DebugMode && Context.IsWorldReady && Game1.player != null && !Game1.eventUp)
            {
                Vector2 screenPos = Game1.GlobalToLocal(Game1.viewport, Game1.player.Position);
                Vector2 textPos = new Vector2(screenPos.X, screenPos.Y - 120);

                int reg = this.Config.UseAdaptive ? GetAdaptiveValue(Game1.timeOfDay, "regen") : this.Config.ConstantRegen;
                int drain = this.Config.UseAdaptive ? GetAdaptiveValue(Game1.timeOfDay, "drain") : this.Config.ConstantDrainReduction;

                // Calculate time left until next regen tick
                int timeLeft = this.Config.RegenFrequencySeconds - _secondsCounter;

                string debugText = $"STM: {(int)Game1.player.stamina}\nRegen: +{reg} (in {timeLeft}s)\nDrain: -{drain}%";

                e.SpriteBatch.DrawString(Game1.dialogueFont, debugText, textPos + new Vector2(2, 2), Color.Black);
                e.SpriteBatch.DrawString(Game1.dialogueFont, debugText, textPos, Color.Orange);
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

            configMenu.AddBoolOption(this.ModManifest, () => this.Config.DebugMode, val => this.Config.DebugMode = val,
                () => this.Helper.Translation.Get("config.debug.name"), () => this.Helper.Translation.Get("config.debug.tooltip"));

            configMenu.AddBoolOption(this.ModManifest, () => this.Config.UseAdaptive, val => this.Config.UseAdaptive = val,
                () => this.Helper.Translation.Get("config.adaptive.name"), () => this.Helper.Translation.Get("config.adaptive.tooltip"));

            // Global Frequency Setting
            configMenu.AddNumberOption(this.ModManifest, () => this.Config.RegenFrequencySeconds, val => this.Config.RegenFrequencySeconds = val,
                () => this.Helper.Translation.Get("config.frequency.name"), () => this.Helper.Translation.Get("config.frequency.tooltip"),
                1, 60); // Min 1 second, Max 60 seconds

            // Constant
            configMenu.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("config.section.constant"));
            configMenu.AddNumberOption(this.ModManifest, () => this.Config.ConstantRegen, val => this.Config.ConstantRegen = val,
                () => this.Helper.Translation.Get("config.regen.name"), () => this.Helper.Translation.Get("config.regen.tooltip"), 0, 10);
            configMenu.AddNumberOption(this.ModManifest, () => this.Config.ConstantDrainReduction, val => this.Config.ConstantDrainReduction = val,
                () => this.Helper.Translation.Get("config.drain.name"), () => this.Helper.Translation.Get("config.drain.tooltip"), 0, 100, 5, val => $"{val}%");

            // Adaptive
            configMenu.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("config.section.adaptive"));
            configMenu.AddParagraph(this.ModManifest, () => this.Helper.Translation.Get("config.adaptive.note"));

            AddTimeslotOptions(configMenu, "config.time.6to9",
                () => this.Config.Regen_0600_to_0900, v => this.Config.Regen_0600_to_0900 = v,
                () => this.Config.Drain_0600_to_0900, v => this.Config.Drain_0600_to_0900 = v);

            AddTimeslotOptions(configMenu, "config.time.9to12",
                () => this.Config.Regen_0900_to_1200, v => this.Config.Regen_0900_to_1200 = v,
                () => this.Config.Drain_0900_to_1200, v => this.Config.Drain_0900_to_1200 = v);

            AddTimeslotOptions(configMenu, "config.time.12to5",
                () => this.Config.Regen_1200_to_1700, v => this.Config.Regen_1200_to_1700 = v,
                () => this.Config.Drain_1200_to_1700, v => this.Config.Drain_1200_to_1700 = v);

            AddTimeslotOptions(configMenu, "config.time.5to12",
                () => this.Config.Regen_1700_to_2400, v => this.Config.Regen_1700_to_2400 = v,
                () => this.Config.Drain_1700_to_2400, v => this.Config.Drain_1700_to_2400 = v);

            AddTimeslotOptions(configMenu, "config.time.12to2",
                () => this.Config.Regen_2400_to_2600, v => this.Config.Regen_2400_to_2600 = v,
                () => this.Config.Drain_2400_to_2600, v => this.Config.Drain_2400_to_2600 = v);
        }

        private void AddTimeslotOptions(IGenericModConfigMenuApi api, string timeKey,
            Func<int> getRegen, Action<int> setRegen,
            Func<int> getDrain, Action<int> setDrain)
        {
            api.AddSubHeader(this.ModManifest, () => this.Helper.Translation.Get(timeKey));

            api.AddNumberOption(this.ModManifest, getRegen, setRegen,
                () => this.Helper.Translation.Get("config.regen.name"),
                () => this.Helper.Translation.Get("config.regen.tooltip"), 0, 10);

            api.AddNumberOption(this.ModManifest, getDrain, setDrain,
                () => this.Helper.Translation.Get("config.drain.name"),
                () => this.Helper.Translation.Get("config.drain.tooltip"), 0, 100, 5, val => $"{val}%");
        }
    }
}