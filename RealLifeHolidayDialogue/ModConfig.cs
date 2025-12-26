using System.Collections.Generic;

namespace RealLifeHolidayDialogue
{
    internal sealed class ModConfig
    {
        public bool EnableMod { get; set; } = true;

        // "Pass by" detection settings
        public int TriggerRadiusTiles { get; set; } = 4;
        public int CooldownSeconds { get; set; } = 15;

        // Text bubble settings
        // style: 2 = normal, 0 = shaking (game behavior)
        public int BubbleStyle { get; set; } = 2;

        // milliseconds
        public int BubbleDurationMs { get; set; } = 3000;

        // Unlimited entries; players can add more dates + dialogue pools.
        public List<HolidayDefinition> Holidays { get; set; } = new()
        {
            new HolidayDefinition
            {
                Date = "12/25",
                Enabled = true,
                Dialogues = new List<string>
                {
                    "Merry Christmas!",
                    "Happy holidays!",
                    "Hope you’re having a cozy day, {player}!",
                    "Merry Christmas, {player}!"
                }
            },
            new HolidayDefinition
            {
                Date = "01/01",
                Enabled = true,
                Dialogues = new List<string>
                {
                    "Happy New Year!",
                    "Happy New Year, {player}!",
                    "New year, new adventures!",
                    "Cheers to a fresh start!"
                }
            }
        };
    }

    internal sealed class HolidayDefinition
    {
        // Format: "MM/DD" (also accepts "M/D", "MM-DD", etc.)
        public string Date { get; set; } = "12/25";

        // Toggle each date on/off (this covers Christmas + New Years too)
        public bool Enabled { get; set; } = true;

        // Random pool (unlimited lines)
        public List<string> Dialogues { get; set; } = new();
    }
}
