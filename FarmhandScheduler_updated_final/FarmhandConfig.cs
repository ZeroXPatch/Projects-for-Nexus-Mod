using StardewModdingAPI;
using System;

namespace FarmhandScheduler;

public class FarmhandConfig
{
    public bool HelperEnabled { get; set; } = true;

    public int StartHour { get; set; } = 6;
    public int EndHour { get; set; } = 18;

    // Tasks
    public bool WaterCrops { get; set; } = true;
    public bool PetAnimals { get; set; } = true;
    public bool FeedAnimals { get; set; } = true;
    public bool HarvestCrops { get; set; } = false;
    public bool OrganizeChests { get; set; } = true;

    // Harvest modifiers (no extra cost)
    public bool HarvestLowTierOnly { get; set; } = true;
    public int HarvestValueCap { get; set; } = 150;

    /// <summary>If true, the farmhand won't harvest flowers (keeps beehouse honey boosts).</summary>
    public bool HarvestExcludeFlowers { get; set; } = false;

    // Keybind
    public SButton PlannerMenuKey { get; set; } = SButton.P;

    // Per-task costs (configurable in GMCM/config.json)
    public int CostWaterCrops { get; set; } = 50;
    public int CostPetAnimals { get; set; } = 50;
    public int CostFeedAnimals { get; set; } = 50;
    public int CostHarvestCrops { get; set; } = 50;
    public int CostOrganizeChests { get; set; } = 50;

    public FarmhandConfig Clone() => (FarmhandConfig)MemberwiseClone();

    public int CalculatePlannedBill()
    {
        int total = 0;

        if (WaterCrops) total += Math.Max(0, CostWaterCrops);
        if (PetAnimals) total += Math.Max(0, CostPetAnimals);
        if (FeedAnimals) total += Math.Max(0, CostFeedAnimals);
        if (HarvestCrops) total += Math.Max(0, CostHarvestCrops);
        if (OrganizeChests) total += Math.Max(0, CostOrganizeChests);

        return total;
    }

    public int GetCostForTask(TaskKind kind) => kind switch
    {
        TaskKind.WaterCrops => CostWaterCrops,
        TaskKind.PetAnimals => CostPetAnimals,
        TaskKind.FeedAnimals => CostFeedAnimals,
        TaskKind.HarvestCrops => CostHarvestCrops,
        TaskKind.OrganizeChests => CostOrganizeChests,
        _ => 0
    };
}

public enum TaskKind
{
    None,
    WaterCrops,
    PetAnimals,
    FeedAnimals,
    HarvestCrops,
    OrganizeChests,

    // modifiers (no cost)
    HarvestLowTierOnly,
    HarvestExcludeFlowers
}
