namespace ProximityStash
{
    public class ModConfig
    {
        public bool ModEnabled { get; set; } = true;
        public bool ShowHudMessages { get; set; } = true; // New Option
        public float TriggerRange { get; set; } = 1.5f;
        public int SoundCooldown { get; set; } = 30;
        public float MenuExitCooldownSeconds { get; set; } = 5.0f;
    }
}