using System;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace GoToBed
{
    /// <summary>The mod entry point.</summary>
    public class ModEntry : Mod
    {
        // 'null!' tells the compiler this will be set in Entry()
        private ModConfig Config = null!;

        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            // Read the config file
            this.Config = helper.ReadConfig<ModConfig>();

            // Hook into events
            helper.Events.GameLoop.TimeChanged += this.OnTimeChanged;
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        }

        /// <summary>Raised after the game is launched, right before the first update tick.</summary>
        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            // Get the Generic Mod Config Menu API (if it's installed)
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            // Register the mod
            configMenu.Register(
                mod: this.ModManifest,
                reset: () => this.Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.Config)
            );

            // -- Warp Time Slider --
            // We map the slider (0-120) to valid 10-minute intervals from 6:00 AM to 2:00 AM
            configMenu.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => this.TimeToIndex(this.Config.WarpTime),
                setValue: value => this.Config.WarpTime = this.IndexToTime(value),
                name: () => this.Helper.Translation.Get("config.warp-time.name"),
                tooltip: () => this.Helper.Translation.Get("config.warp-time.desc"),
                min: 0,
                max: 120, // 20 hours * 6 slots per hour
                interval: 1,
                formatValue: (value) => this.FormatTime(this.IndexToTime(value))
            );

            // -- Warning Time Slider --
            configMenu.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => this.TimeToIndex(this.Config.WarningTime),
                setValue: value => this.Config.WarningTime = this.IndexToTime(value),
                name: () => this.Helper.Translation.Get("config.warning-time.name"),
                tooltip: () => this.Helper.Translation.Get("config.warning-time.desc"),
                min: 0,
                max: 120,
                interval: 1,
                formatValue: (value) => this.FormatTime(this.IndexToTime(value))
            );

            // -- Warning Message --
            configMenu.AddTextOption(
                mod: this.ModManifest,
                getValue: () => this.Config.WarningMessage ?? this.Helper.Translation.Get("warning.message"),
                setValue: value => this.Config.WarningMessage = value,
                name: () => this.Helper.Translation.Get("config.warning-msg.name"),
                tooltip: () => this.Helper.Translation.Get("config.warning-msg.desc")
            );

            configMenu.AddSectionTitle(
                mod: this.ModManifest,
                text: () => "Coordinates"
            );

            configMenu.AddTextOption(
                mod: this.ModManifest,
                getValue: () => this.Config.TargetLocation,
                setValue: value => this.Config.TargetLocation = value,
                name: () => this.Helper.Translation.Get("config.target-loc.name"),
                tooltip: () => this.Helper.Translation.Get("config.target-loc.desc")
            );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => this.Config.TargetX,
                setValue: value => this.Config.TargetX = value,
                name: () => this.Helper.Translation.Get("config.target-x.name"),
                tooltip: () => this.Helper.Translation.Get("config.target-x.desc")
            );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => this.Config.TargetY,
                setValue: value => this.Config.TargetY = value,
                name: () => this.Helper.Translation.Get("config.target-y.name"),
                tooltip: () => this.Helper.Translation.Get("config.target-y.desc")
            );
        }

        /// <summary>Raised after the in-game clock changes.</summary>
        private void OnTimeChanged(object? sender, TimeChangedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            if (e.NewTime == this.Config.WarningTime)
            {
                string message = !string.IsNullOrEmpty(this.Config.WarningMessage)
                    ? this.Config.WarningMessage
                    : this.Helper.Translation.Get("warning.message");

                Game1.addHUDMessage(new HUDMessage(message, 2));
            }

            if (e.NewTime == this.Config.WarpTime)
            {
                this.WarpHome();
            }
        }

        private void WarpHome()
        {
            if (Game1.currentLocation.Name.Equals(this.Config.TargetLocation, StringComparison.OrdinalIgnoreCase))
                return;

            this.Monitor.Log($"It is {this.Config.WarpTime}! Warping player to {this.Config.TargetLocation}...", LogLevel.Info);

            Game1.warpFarmer(this.Config.TargetLocation, this.Config.TargetX, this.Config.TargetY, false);
            Game1.player.faceDirection(0);
        }

        // --- Helper Methods for Time Formatting ---

        /// <summary>Converts a Stardew time (e.g. 600, 2540) to a linear index (0-120) for the slider.</summary>
        private int TimeToIndex(int time)
        {
            // Start at 6:00 AM (600)
            if (time < 600) time = 600;
            // Cap at 2:00 AM (2600)
            if (time > 2600) time = 2600;

            int hour = time / 100;
            int minute = time % 100;

            // Calculate hours passed since 6:00 AM
            int hoursSinceStart = hour - 6;

            // 6 slots per hour (00, 10, 20, 30, 40, 50)
            return (hoursSinceStart * 6) + (minute / 10);
        }

        /// <summary>Converts a linear index (0-120) back to Stardew time.</summary>
        private int IndexToTime(int index)
        {
            // 6 slots per hour
            int hoursToAdd = index / 6;
            int minutesToAdd = (index % 6) * 10;

            // Base time is 600
            return 600 + (hoursToAdd * 100) + minutesToAdd;
        }

        /// <summary>Formats the raw Stardew time integer into a readable string (e.g., "1:40 AM").</summary>
        private string FormatTime(int time)
        {
            int hour = time / 100;
            int minute = time % 100;

            // Determine AM/PM
            // In Stardew: 600-1150 is AM, 1200-2350 is PM, 2400+ is Next Day AM (Late Night)
            string period = (hour >= 12 && hour < 24) ? "PM" : "AM";

            // Convert 24h to 12h format
            int displayHour = hour % 12;
            if (displayHour == 0) displayHour = 12;

            return $"{displayHour}:{minute:00} {period}";
        }
    }

    /// <summary>The API interface for Generic Mod Config Menu.</summary>
    public interface IGenericModConfigMenuApi
    {
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);
        void AddSectionTitle(IManifest mod, Func<string> text, Func<string>? tooltip = null);
        void AddNumberOption(IManifest mod, Func<int> getValue, Action<int> setValue, Func<string> name, Func<string>? tooltip = null, int? min = null, int? max = null, int? interval = null, Func<int, string>? formatValue = null, string? fieldId = null);
        void AddTextOption(IManifest mod, Func<string> getValue, Action<string> setValue, Func<string> name, Func<string>? tooltip = null, string[]? allowedValues = null, Func<string, string>? formatAllowedValue = null, string? fieldId = null);
    }
}