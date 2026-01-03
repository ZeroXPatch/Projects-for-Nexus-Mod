using System;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;

namespace LateNightGrace
{
    public class ModEntry : Mod
    {
        private bool hasTriggeredGrace = false;

        public override void Entry(IModHelper helper)
        {
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.TimeChanged += OnTimeChanged;
        }

        /// <summary>Resets the grace period flag at the start of every day.</summary>
        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            this.hasTriggeredGrace = false;
        }

        /// <summary>Checks time and location to apply grace period.</summary>
        private void OnTimeChanged(object? sender, TimeChangedEventArgs e)
        {
            // Trigger at 1:50 AM (2550) to prevent hitting 2:00 AM (2600)
            if (e.NewTime != 2550)
                return;

            // Only trigger once per night
            if (this.hasTriggeredGrace)
                return;

            if (IsPlayerInSafeLocation())
            {
                // Rewind time to 1:30 AM (2530)
                // This gives exactly 20 minutes before it hits 1:50 AM again
                Game1.timeOfDay = 2530;

                this.hasTriggeredGrace = true;

                Game1.addHUDMessage(new HUDMessage("Safe at home! +20 mins to sleep.", HUDMessage.newQuest_type));
                this.Monitor.Log("Grace period triggered. Time rewound to 1:30 AM.", LogLevel.Debug);
            }
        }

        private bool IsPlayerInSafeLocation()
        {
            GameLocation location = Game1.currentLocation;
            if (location == null) return false;

            // Farm (Outside)
            if (location is Farm) return true;

            // FarmHouse (Inside Home/Cabins)
            if (location is FarmHouse) return true;

            // Check for modded farm names just in case
            if (location.Name.StartsWith("Farm", StringComparison.OrdinalIgnoreCase)) return true;

            return false;
        }
    }
}