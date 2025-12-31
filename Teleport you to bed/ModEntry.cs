using System;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace GoToBed
{
    /// <summary>The mod entry point.</summary>
    public class ModEntry : Mod
    {
        private ModConfig Config;

        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            // Read the config file
            this.Config = helper.ReadConfig<ModConfig>();

            // Hook into the TimeChanged event
            helper.Events.GameLoop.TimeChanged += this.OnTimeChanged;
        }

        /// <summary>Raised after the in-game clock changes.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnTimeChanged(object sender, TimeChangedEventArgs e)
        {
            // Only run if the world is loaded
            if (!Context.IsWorldReady)
                return;

            // Check if it's the warning time
            if (e.NewTime == this.Config.WarningTime)
            {
                Game1.addHUDMessage(new HUDMessage(this.Config.WarningMessage, 2)); // 2 is the 'type' for warnings (often red/yellow)
            }

            // Check if it's the warp time
            if (e.NewTime == this.Config.WarpTime)
            {
                this.WarpHome();
            }
        }

        /// <summary>Teleports the player to the configured location.</summary>
        private void WarpHome()
        {
            // Avoid warping if already in the target location to prevent loops or odd behavior
            if (Game1.currentLocation.Name.Equals(this.Config.TargetLocation, StringComparison.OrdinalIgnoreCase))
                return;

            this.Monitor.Log($"It is {this.Config.WarpTime}! Warping player to {this.Config.TargetLocation}...", LogLevel.Info);

            // Warp the player
            // arguments: LocationName, TileX, TileY, isStructure (false usually works for maps)
            Game1.warpFarmer(this.Config.TargetLocation, this.Config.TargetX, this.Config.TargetY, false);
            
            // Optional: Face up after warping
            Game1.player.faceDirection(0);
        }
    }
}