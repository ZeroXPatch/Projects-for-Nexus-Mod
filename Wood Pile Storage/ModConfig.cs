namespace WoodPileStorage
{
    public class ModConfig
    {
        public bool EnableAutoDeposit { get; set; } = true;
        public int AutoDepositRange { get; set; } = 1;
        public int AutoDepositCooldown { get; set; } = 15;

        // New Options
        public bool EnableResourceStorage { get; set; } = false;
        public bool EnableTrashStorage { get; set; } = false;
    }
}