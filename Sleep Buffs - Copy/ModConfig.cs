namespace AutoSwapBack
{
    public enum SwapTrigger
    {
        Disabled,
        UsedOnce,
        Depleted
    }

    public class ModConfig
    {
        // Default set to Depleted as requested
        public SwapTrigger SeedsBehavior { get; set; } = SwapTrigger.Depleted;

        // These remain UsedOnce
        public SwapTrigger FoodBehavior { get; set; } = SwapTrigger.UsedOnce;
        public SwapTrigger BombBehavior { get; set; } = SwapTrigger.UsedOnce;
    }
}