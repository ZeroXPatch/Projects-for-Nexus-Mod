namespace MinecartMaster
{
    public class ModConfig
    {
        // Master Switch
        public bool ModEnabled { get; set; } = true;

        // Unlock All Minecarts immediately (Bypass Community Center/Joja requirement)
        public bool UnlockMinecartsEarly { get; set; } = true;

        // Mode: False = Always Open (Fixed), True = Follow Schedule (Adaptive)
        public bool UseAdaptiveSchedule { get; set; } = true;

        // Travel Cost
        public int TravelCost { get; set; } = 0;

        // Schedule: True = Open, False = Closed
        public bool Open_0600_to_0900 { get; set; } = true;
        public bool Open_0900_to_1200 { get; set; } = true;
        public bool Open_1200_to_1700 { get; set; } = true;
        public bool Open_1700_to_2400 { get; set; } = true;
        public bool Open_2400_to_0200 { get; set; } = true;
    }
}