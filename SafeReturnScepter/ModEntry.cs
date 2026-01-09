using System;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

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

            // 2. Only run logic if the player pressed a "Use Tool" button
            if (!e.Button.IsUseToolButton())
                return;

            // 3. Check specifically for the Return Scepter using its Unique ID
            if (Game1.player.CurrentItem?.QualifiedItemId != "(T)ReturnScepter")
                return;

            // 4. Double-click logic
            double currentTime = Game1.currentGameTime.TotalGameTime.TotalSeconds;

            if (currentTime - _lastAttemptTime > DoubleClickWindow)
            {
                // -- FIRST CLICK (SUPPRESS) --

                _lastAttemptTime = currentTime;

                // Visual Feedback: Get the text from i18n/default.json
                string message = this.Helper.Translation.Get("notification.double-click");
                Game1.addHUDMessage(new HUDMessage(message, 3));

                // Audio removed as requested

                // Prevent the warp
                this.Helper.Input.Suppress(e.Button);
            }
            else
            {
                // -- SECOND CLICK (ALLOW) --
                // Reset timer and allow the game to process the warp
                _lastAttemptTime = 0;
            }
        }
    }
}