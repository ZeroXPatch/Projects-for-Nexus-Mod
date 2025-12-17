#nullable enable

namespace PetTodaySurpriseTomorrow;

internal sealed class ModConfig
{
    // Multiplayer
    public bool HostOnlyPettingDetection { get; set; } = true;

    // Action weights
    public int BringItemWeight { get; set; } = 70;
    public int FillBowlWeight { get; set; } = 20;
    public int ScareCrowsWeight { get; set; } = 10;

    // Comedy
    public bool EnableComedyItems { get; set; } = true;
    public double ComedyChance { get; set; } = 0.08;

    // Theming
    public bool EnableWeatherSkews { get; set; } = true;

    // Category weights
    public int ResourcesWeight { get; set; } = 50;
    public int SeasonalForageWeight { get; set; } = 35;
    public int BeachFindsWeight { get; set; } = 15;

    // Resource stacks
    public int WoodMin { get; set; } = 5;
    public int WoodMax { get; set; } = 20;

    public int StoneMin { get; set; } = 5;
    public int StoneMax { get; set; } = 20;

    public int FiberMin { get; set; } = 5;
    public int FiberMax { get; set; } = 15;

    public int MixedSeedsMin { get; set; } = 1;
    public int MixedSeedsMax { get; set; } = 5;

    public RewardDropStyle RewardDropStyle { get; set; } = RewardDropStyle.NearBowl;
}

internal enum RewardDropStyle
{
    NearBowl,
    NearPet,
    NearPlayer
}
