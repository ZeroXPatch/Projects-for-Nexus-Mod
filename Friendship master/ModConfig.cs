namespace FriendshipMaster
{
    public class ModConfig
    {
        public bool DebugMode { get; set; } = false; // New: Shows logs for decay verification
        public bool UseAdaptiveMode { get; set; } = false;

        // --- Constant Mode ---
        public float Constant_DecayMultiplier { get; set; } = 1.0f;
        public int Constant_TalkBonus { get; set; } = 0;
        public int Constant_AddGifts { get; set; } = 0; // Changed from Bool to Int

        // --- Adaptive Mode (Mon=0 ... Sun=6) ---
        public float Mon_Decay { get; set; } = 1.0f;
        public int Mon_Talk { get; set; } = 0;
        public int Mon_AddGifts { get; set; } = 0;

        public float Tue_Decay { get; set; } = 1.0f;
        public int Tue_Talk { get; set; } = 0;
        public int Tue_AddGifts { get; set; } = 0;

        public float Wed_Decay { get; set; } = 1.0f;
        public int Wed_Talk { get; set; } = 0;
        public int Wed_AddGifts { get; set; } = 0;

        public float Thu_Decay { get; set; } = 1.0f;
        public int Thu_Talk { get; set; } = 0;
        public int Thu_AddGifts { get; set; } = 0;

        public float Fri_Decay { get; set; } = 1.0f;
        public int Fri_Talk { get; set; } = 0;
        public int Fri_AddGifts { get; set; } = 0;

        public float Sat_Decay { get; set; } = 1.0f;
        public int Sat_Talk { get; set; } = 0;
        public int Sat_AddGifts { get; set; } = 0;

        public float Sun_Decay { get; set; } = 1.0f;
        public int Sun_Talk { get; set; } = 0;
        public int Sun_AddGifts { get; set; } = 0;
    }
}