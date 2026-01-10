using System;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace GoToBed
{
    /// <summary>The mod entry point.</summary>
    public class ModEntry : Mod
    {
        private ModConfig Config = null!;

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.TimeChanged += this.OnTimeChanged;
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            configMenu.Register(
                mod: this.ModManifest,
                reset: () => this.Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.Config)
            );

            // Time Options
            configMenu.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => TimeToIndex(this.Config.WarpTime),
                setValue: value => this.Config.WarpTime = IndexToTime(value),
                name: () => this.Helper.Translation.Get("config.warp-time.name"),
                tooltip: () => this.Helper.Translation.Get("config.warp-time.desc"),
                min: 0, max: 120, interval: 1,
                formatValue: (value) => FormatTime(IndexToTime(value))
            );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => TimeToIndex(this.Config.WarningTime),
                setValue: value => this.Config.WarningTime = IndexToTime(value),
                name: () => this.Helper.Translation.Get("config.warning-time.name"),
                tooltip: () => this.Helper.Translation.Get("config.warning-time.desc"),
                min: 0, max: 120, interval: 1,
                formatValue: (value) => FormatTime(IndexToTime(value))
            );

            configMenu.AddTextOption(
                mod: this.ModManifest,
                getValue: () => this.Config.WarningMessage ?? this.Helper.Translation.Get("warning.message"),
                setValue: value => this.Config.WarningMessage = value,
                name: () => this.Helper.Translation.Get("config.warning-msg.name"),
                tooltip: () => this.Helper.Translation.Get("config.warning-msg.desc")
            );

            // Location Options
            configMenu.AddSectionTitle(
                mod: this.ModManifest,
                text: () => "Location Settings"
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => this.Config.WarpToBed,
                setValue: value => this.Config.WarpToBed = value,
                name: () => this.Helper.Translation.Get("config.warp-bed.name"),
                tooltip: () => this.Helper.Translation.Get("config.warp-bed.desc")
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

        private void OnTimeChanged(object? sender, TimeChangedEventArgs e)
        {
            if (!Context.IsWorldReady) return;

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
            // 1. Try Warping to Bed (Multiplayer Friendly)
            if (this.Config.WarpToBed)
            {
                GameLocation home = Utility.getHomeOfFarmer(Game1.player);
                if (home != null)
                {
                    // FIXED: explicitly cast Vector2 to Point
                    Vector2 bedVec = Game1.player.mostRecentBed;
                    Point bed = new Point((int)bedVec.X, (int)bedVec.Y);

                    // Safety check: Ensure bed coordinates are valid
                    if (bed.X > 0 || bed.Y > 0)
                    {
                        if (Game1.currentLocation == home && Game1.player.TilePoint.X == bed.X && Game1.player.TilePoint.Y == bed.Y)
                            return;

                        this.Monitor.Log($"It is {this.Config.WarpTime}! Warping player to bed in {home.Name}...", LogLevel.Info);
                        Game1.warpFarmer(home.Name, bed.X, bed.Y, false);
                        return;
                    }
                }
            }

            // 2. Fallback to Manual Config
            if (Game1.currentLocation.Name.Equals(this.Config.TargetLocation, StringComparison.OrdinalIgnoreCase))
                return;

            this.Monitor.Log($"It is {this.Config.WarpTime}! Warping player to manual target {this.Config.TargetLocation}...", LogLevel.Info);
            Game1.warpFarmer(this.Config.TargetLocation, this.Config.TargetX, this.Config.TargetY, false);
            Game1.player.faceDirection(0);
        }

        // --- Helper Methods ---
        private static int TimeToIndex(int time)
        {
            if (time < 600) time = 600;
            if (time > 2600) time = 2600;
            int hour = time / 100;
            int minute = time % 100;
            return ((hour - 6) * 6) + (minute / 10);
        }

        private static int IndexToTime(int index)
        {
            int hoursToAdd = index / 6;
            int minutesToAdd = (index % 6) * 10;
            return 600 + (hoursToAdd * 100) + minutesToAdd;
        }

        private static string FormatTime(int time)
        {
            int hour = time / 100;
            int minute = time % 100;
            string period = (hour >= 12 && hour < 24) ? "PM" : "AM";
            int displayHour = hour % 12;
            if (displayHour == 0) displayHour = 12;
            return $"{displayHour}:{minute:00} {period}";
        }
    }

    public interface IGenericModConfigMenuApi
    {
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);
        void AddSectionTitle(IManifest mod, Func<string> text, Func<string>? tooltip = null);
        void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue, Func<string> name, Func<string>? tooltip = null, string? fieldId = null);
        void AddNumberOption(IManifest mod, Func<int> getValue, Action<int> setValue, Func<string> name, Func<string>? tooltip = null, int? min = null, int? max = null, int? interval = null, Func<int, string>? formatValue = null, string? fieldId = null);
        void AddTextOption(IManifest mod, Func<string> getValue, Action<string> setValue, Func<string> name, Func<string>? tooltip = null, string[]? allowedValues = null, Func<string, string>? formatAllowedValue = null, string? fieldId = null);
    }
}