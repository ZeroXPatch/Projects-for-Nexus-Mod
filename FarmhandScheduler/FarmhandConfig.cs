using StardewModdingAPI;

namespace FarmhandScheduler;

public class FarmhandConfig
{
    public bool HelperEnabled { get; set; } = true;
    public int DailyCost { get; set; } = 250;
    public int StartHour { get; set; } = 6;
    public int EndHour { get; set; } = 18;
    public bool WaterCrops { get; set; } = true;
    public bool PetAnimals { get; set; } = true;
    public bool FeedAnimals { get; set; } = true;
    public bool HarvestCrops { get; set; } = false;
    public bool HarvestLowTierOnly { get; set; } = true;
    public int HarvestValueCap { get; set; } = 150;
    public bool OrganizeChests { get; set; } = true;
    public SButton PlannerMenuKey { get; set; } = SButton.P;

    public FarmhandConfig Clone() => (FarmhandConfig)MemberwiseClone();
}
