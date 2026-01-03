using System;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Pathfinding;

namespace LoyalHorse
{
    public class ModConfig
    {
        public SButton ToggleKey { get; set; } = SButton.H;
    }

    public class ModEntry : Mod
    {
        private ModConfig? Config;
        private bool IsFollowing = false;
        private Horse? PlayerHorse;

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady || this.Config == null) return;

            if (e.Button == this.Config.ToggleKey)
            {
                this.IsFollowing = !this.IsFollowing;
                string message = this.IsFollowing ? "Horse following!" : "Horse staying.";
                Game1.addHUDMessage(new HUDMessage(message, 3));

                if (!this.IsFollowing && this.PlayerHorse != null)
                {
                    this.PlayerHorse.Halt();
                    this.PlayerHorse.controller = null;
                }
            }
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || !this.IsFollowing) return;

            // 1. Find the horse (searching all locations if necessary)
            if (this.PlayerHorse == null || !this.PlayerHorse.Sprite.Texture.Name.Contains("horse"))
            {
                this.PlayerHorse = Utility.findHorse(Guid.Empty); // Finds the player's horse globally
            }

            if (this.PlayerHorse == null) return;

            // 2. Logic to follow across maps
            if (this.PlayerHorse.currentLocation != Game1.currentLocation)
            {
                // Teleport horse to player if they changed maps
                Game1.warpCharacter(this.PlayerHorse, Game1.currentLocation, Game1.player.Tile);
            }

            // 3. Movement Logic (Run every 20 ticks for smoother tracking)
            if (e.IsMultipleOf(20))
            {
                float distance = Vector2.Distance(Game1.player.Position, this.PlayerHorse.Position);

                // Teleport if extremely far away or stuck
                if (distance > 640f) // ~10 tiles
                {
                    this.PlayerHorse.Position = Game1.player.Position;
                }
                // Pathfind if moderately far away
                else if (distance > 128f && this.PlayerHorse.controller == null)
                {
                    this.PlayerHorse.controller = new PathFindController(
                        this.PlayerHorse,
                        Game1.currentLocation,
                        Game1.player.TilePoint,
                        -1 // Face any direction upon arrival
                    );
                }
            }
        }
    }
}