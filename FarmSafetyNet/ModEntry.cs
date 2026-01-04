using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;

// Alias SObject to avoid confusion with System.Object
using SObject = StardewValley.Object;

namespace FarmSafetyNet
{
    public class ModEntry : Mod
    {
        // Variable to track the last time the player tried to place a bomb
        private double lastAttemptTime = 0;

        // The time window (in seconds) for the double-click
        private const double DoubleClickWindow = 1.0;

        public override void Entry(IModHelper helper)
        {
            helper.Events.Input.ButtonPressed += OnButtonPressed;
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            // 1. Basic Checks
            if (!Context.IsWorldReady || !Context.IsPlayerFree)
                return;

            // 2. UI Protection
            // If clicking the Toolbar/Menu, we ignore everything so you can swap items safely.
            if (IsCursorOverUI())
                return;

            // 3. Button Check
            // We monitor Action (Right Click) and UseTool (Left Click)
            if (!e.Button.IsActionButton() && !e.Button.IsUseToolButton())
                return;

            // 4. Check Player Item
            Farmer player = Game1.player;
            Item currentItem = player.CurrentItem;

            if (currentItem == null)
                return;

            // 5. Identify if it is a bomb
            if (!IsBomb(currentItem))
                return;

            // 6. Check Location
            GameLocation loc = player.currentLocation;
            if (!IsSafeZone(loc))
                return;

            // --- DOUBLE CLICK LOGIC ---

            double currentTime = Game1.currentGameTime.TotalGameTime.TotalSeconds;

            // Check if the player clicked recently (within 1 second)
            if ((currentTime - lastAttemptTime) < DoubleClickWindow)
            {
                // This IS a double click! 
                // We return immediately. This stops the mod from suppressing the input.
                // The game will receive this click and place the bomb naturally.

                // Optional: Reset timer so a triple-click doesn't accidentaly trigger another logic
                lastAttemptTime = 0;
                return;
            }

            // --- FIRST CLICK (SAFETY TRIGGER) ---

            // 1. Suppress the input (Stop the bomb placement)
            this.Helper.Input.Suppress(e.Button);

            // 2. Update the timestamp
            lastAttemptTime = currentTime;

            // 3. Show Warning
            // Plays the "error" sound and shows text in bottom left
            Game1.showRedMessage("Confirm: Click again to detonate!");

            // Optional: You can play a specific sound if you want, e.g.,
            // loc.playSound("crit");
        }

        private bool IsCursorOverUI()
        {
            int x = Game1.getMouseX();
            int y = Game1.getMouseY();

            if (Game1.activeClickableMenu != null)
            {
                if (Game1.activeClickableMenu.isWithinBounds(x, y))
                    return true;
            }

            if (Game1.onScreenMenus != null)
            {
                foreach (var menu in Game1.onScreenMenus)
                {
                    if (menu.isWithinBounds(x, y))
                        return true;

                    if (menu is Toolbar toolbar)
                    {
                        foreach (var button in toolbar.buttons)
                        {
                            if (button.containsPoint(x, y))
                                return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool IsBomb(Item item)
        {
            if (item.Name != null && item.Name.Contains("Bomb")) return true;
            if (item.ItemId == "286" || item.ItemId == "287" || item.ItemId == "288") return true;
            return false;
        }

        private static bool IsSafeZone(GameLocation loc)
        {
            if (loc is Farm) return true;
            if (loc is FarmHouse) return true;
            if (loc.Name.Contains("Cellar")) return true;
            if (loc.Name.Contains("Greenhouse")) return true;
            if (loc.Name.Contains("IslandWest")) return true;
            if (loc is AnimalHouse || loc is SlimeHutch || loc.Name.Contains("Shed")) return true;
            return false;
        }
    }
}