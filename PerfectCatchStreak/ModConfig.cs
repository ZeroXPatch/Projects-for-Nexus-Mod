namespace PerfectCatchStreak
{
    public class ModConfig
    {
        public int BaseBonusXP { get; set; } = 5;
        public int XPPerStreakLevel { get; set; } = 2;
        public bool ShowHUDNotification { get; set; } = true;
        public bool PlaySound { get; set; } = true;

        // Tracks your all-time high score
        public int MaxStreak { get; set; } = 0;
    }
}