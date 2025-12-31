using System;

namespace GoToBed
{
    public class ModConfig
    {
        // Time to warp (2540 = 1:40 AM)
        public int WarpTime { get; set; } = 2540;

        // Time to warn the player (2520 = 1:20 AM)
        public int WarningTime { get; set; } = 2520;

        // Message to display
        public string WarningMessage { get; set; } = "You will be teleported home in 20 mins. Please be ready...";

        // Target location name (FarmHouse is the standard name for the player's home interior)
        public string TargetLocation { get; set; } = "FarmHouse";

        // X Coordinate inside the house (Default 3 is safe for starter house)
        public int TargetX { get; set; } = 3;

        // Y Coordinate inside the house (Default 10 is just inside the door)
        public int TargetY { get; set; } = 10;
    }
}