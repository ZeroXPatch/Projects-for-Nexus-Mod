using System;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Tools; // Required to check for "Wand"

namespace SafeReturnScepter
{
    public class ModEntry : Mod
    {
        // Time in seconds allowed between clicks to register as a double click
        private const double DoubleClickWindow = 0.5;

        // Track the last time the button was pressed
        private double _lastAttemptTime;

        public override void Entry(IModHelper helper)
        {
            helper.Events.Input.ButtonPressed += OnButtonPressed;
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            // 1. Basic checks: World must be ready, player must be free to move
            if (!Context.IsWorldReady || !Context.IsPlayerFree)
                return;

            // 2. Only run logic if the player pressed a "Use Tool" button (Left Click, Controller X, etc.)
            // We ignore other buttons so you can still open menus/inventory comfortably.
            if (!e.Button.IsUseToolButton())
                return;

            // 3. Robustly check if the player is holding the Return Scepter.
            // The Return Scepter is the only item of type "Wand" in the vanilla game.
            // This is safer and faster than comparing strings.
            if (Game1.player.CurrentItem is not Wand)
                return;

            // 4. Double-click logic
            double currentTime = Game1.currentGameTime.TotalGameTime.TotalSeconds;

            // Check if this click is "too late" to be a double click (or is the very first click)
            if (currentTime - _lastAttemptTime > DoubleClickWindow)
            {
                // -- FIRST CLICK (SUPPRESS) --

                // Update the last attempt time
                _lastAttemptTime = currentTime;

                // Visual Feedback: Show notification
                Game1.addHUDMessage(new HUDMessage("Double-click to warp", 3));

                // Audio Feedback: Play a small "dud" sound so the player knows input was caught
                // "refuse" is the standard 'bloop' sound when you can't do something.
                Game1.playSound("refuse");

                // SUPPRESS the input. This prevents the warp.
                this.Helper.Input.Suppress(e.Button);
            }
            else
            {
                // -- SECOND CLICK (ALLOW) --

                // The user clicked fast enough. We do NOTHING here.
                // By NOT suppressing, the game receives the input and performs the warp naturally.

                // Reset timer to ensure a 3rd click doesn't accidentally count as a 2nd click for a future event.
                _lastAttemptTime = 0;
            }
        }
    }
}