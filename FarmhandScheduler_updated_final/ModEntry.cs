using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Locations;
using StardewValley.Objects;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FarmhandScheduler;

public class ModEntry : Mod
{
    private FarmhandConfig _config = new();
    private FarmhandState _state = new();
    private int _lastTaskExecution = -1;

    public override void Entry(IModHelper helper)
    {
        _config = helper.ReadConfig<FarmhandConfig>();

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.GameLoop.TimeChanged += OnTimeChanged;
        helper.Events.Input.ButtonReleased += OnButtonReleased;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        RegisterGmcm();
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        _state = new FarmhandState();
        _lastTaskExecution = -1;
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        _lastTaskExecution = -1;

        _state.HiredToday = false;
        _state.Plan = TodayPlan.Disabled();

        if (!_config.HelperEnabled)
            return;

        int todayBill = Math.Max(0, _config.CalculatePlannedBill());

        // Optional: one-line debug so you can confirm what it thinks the bill is.
        Monitor.Log($"[DayStarted] Bill={todayBill} | Water={_config.WaterCrops} Pet={_config.PetAnimals} Feed={_config.FeedAnimals} Harvest={_config.HarvestCrops} Chests={_config.OrganizeChests} | Costs: W={_config.CostWaterCrops} P={_config.CostPetAnimals} F={_config.CostFeedAnimals} H={_config.CostHarvestCrops} C={_config.CostOrganizeChests}", LogLevel.Trace);

        // If bill is 0, DO NOT show "Paid 0g".
        if (todayBill <= 0)
        {
            Game1.addHUDMessage(new HUDMessage(
                Helper.Translation.Get("hud.bill.free"),
                HUDMessage.newQuest_type
            ));

            _state.HiredToday = true;
            _state.Plan = TodayPlan.FromConfig(_config);

            Game1.addHUDMessage(new HUDMessage(
                Helper.Translation.Get("hud.hired.ok"),
                HUDMessage.newQuest_type
            ));
            return;
        }

        // Not enough money => no tasks today
        if (Game1.player.Money < todayBill)
        {
            string msg = string.Format(Helper.Translation.Get("hud.bill.unpaid"), todayBill);
            Game1.addHUDMessage(new HUDMessage(msg, HUDMessage.error_type));
            return;
        }

        // Pay & hire
        Game1.player.Money -= todayBill;

        {
            string msg = string.Format(Helper.Translation.Get("hud.bill.paid"), todayBill);
            Game1.addHUDMessage(new HUDMessage(msg, HUDMessage.newQuest_type));
        }

        _state.HiredToday = true;
        _state.Plan = TodayPlan.FromConfig(_config);

        Game1.addHUDMessage(new HUDMessage(
            Helper.Translation.Get("hud.hired.ok"),
            HUDMessage.newQuest_type
        ));
    }

    private void OnButtonReleased(object? sender, ButtonReleasedEventArgs e)
    {
        if (!Context.IsPlayerFree || e.Button != _config.PlannerMenuKey)
            return;

        Game1.activeClickableMenu = new PlannerMenu(
            _config,
            Helper.Translation,
            SaveConfig,
            ApplyLiveConfig
        );
    }

    private void OnTimeChanged(object? sender, TimeChangedEventArgs e)
    {
        if (!_state.HiredToday || Game1.eventUp)
            return;

        if (!IsWithinSchedule(Game1.timeOfDay))
            return;

        if (_lastTaskExecution == Game1.timeOfDay)
            return;

        _lastTaskExecution = Game1.timeOfDay;
        TryPerformTasks();
    }

    private bool IsWithinSchedule(int currentTime)
    {
        int start = _state.Plan.StartHour * 100;
        int end = _state.Plan.EndHour * 100;
        return currentTime >= start && currentTime <= end;
    }

    private void TryPerformTasks()
    {
        if (_state.Plan.WaterCrops)
            WaterCrops();

        if (_state.Plan.PetAnimals)
            PetAnimals();

        if (_state.Plan.FeedAnimals)
            FeedAnimals();

        if (_state.Plan.HarvestCrops)
            HarvestCrops(_state.Plan.HarvestLowTierOnly, _state.Plan.HarvestValueCap, _state.Plan.HarvestExcludeFlowers);

        if (_state.Plan.OrganizeChests)
            OrganizeChests();
    }

    // --------------------
    // TASKS
    // --------------------

    private void WaterCrops()
    {
        foreach (GameLocation location in Game1.locations)
        {
            if (location.terrainFeatures is null)
                continue;

            foreach (var pair in location.terrainFeatures.Pairs)
            {
                if (pair.Value is StardewValley.TerrainFeatures.HoeDirt dirt)
                {
                    if (dirt.needsWatering() &&
                        dirt.state.Value != StardewValley.TerrainFeatures.HoeDirt.watered)
                    {
                        dirt.state.Value = StardewValley.TerrainFeatures.HoeDirt.watered;
                    }
                }
            }
        }
    }

    private void PetAnimals()
    {
        var farm = Game1.getFarm();
        foreach (var animal in farm.getAllFarmAnimals())
        {
            if (!animal.wasPet.Value)
                animal.pet(Game1.player);
        }
    }

    private void FeedAnimals()
    {
        var farm = Game1.getFarm();

        foreach (Building building in farm.buildings)
        {
            if (building.indoors.Value is not AnimalHouse house)
                continue;

            int animals = house.animalsThatLiveHere.Count;
            if (animals <= 0)
                continue;

            int availableHay = farm.piecesOfHay.Value;
            if (availableHay <= 0)
                continue;

            int currentHay = house.piecesOfHay.Value;
            int hayNeeded = Math.Max(0, animals - currentHay);
            if (hayNeeded <= 0)
                continue;

            int hayToUse = Math.Min(availableHay, hayNeeded);
            farm.piecesOfHay.Value -= hayToUse;
            house.piecesOfHay.Value += hayToUse;
        }
    }

    private void HarvestCrops(bool lowTierOnly, int valueCap, bool excludeFlowers)
    {
        Farm farm = Game1.getFarm();
        if (farm.terrainFeatures is null)
            return;

        foreach (var pair in farm.terrainFeatures.Pairs.ToList())
        {
            if (pair.Value is not StardewValley.TerrainFeatures.HoeDirt dirt)
                continue;

            if (dirt.crop is null || !dirt.readyForHarvest())
                continue;

            string harvestId = dirt.crop.indexOfHarvest.Value;

            if (StardewValley.ItemRegistry.Create(harvestId, 1) is not StardewValley.Object obj)
                continue;

            if (excludeFlowers && obj.Category == StardewValley.Object.flowersCategory)
                continue;

            if (lowTierOnly && obj.Price > valueCap)
                continue;

            var cropData = dirt.crop.GetData();
            bool isRegrowingCrop = cropData != null && cropData.RegrowDays != -1;

            Vector2 tileLocation = pair.Key;

            bool success = dirt.crop.harvest((int)tileLocation.X, (int)tileLocation.Y, dirt, null, false);

            if (success && !isRegrowingCrop)
                dirt.destroyCrop(true);
        }
    }

    private void OrganizeChests()
    {
        IEnumerable<Chest> chests = Game1.locations
            .SelectMany(location => location.Objects.Values)
            .OfType<Chest>()
            .Where(c => c.playerChest.Value);

        foreach (Chest chest in chests)
        {
            var sorted = chest.Items
                .Where(i => i is not null)
                .OrderBy(i => i!.Category)
                .ThenBy(i => i.DisplayName)
                .ToList();

            chest.Items.Clear();
            foreach (var item in sorted)
                chest.Items.Add(item);
        }
    }

    // --------------------
    // CONFIG
    // --------------------

    private void SaveConfig(FarmhandConfig updated)
    {
        _config = updated;
        Helper.WriteConfig(_config);
    }

    private void ApplyLiveConfig(FarmhandConfig config)
    {
        _config = config;

        if (Context.IsWorldReady && _state.HiredToday)
        {
            Game1.addHUDMessage(new HUDMessage(
                Helper.Translation.Get("hud.changes.tomorrow"),
                HUDMessage.newQuest_type
            ));
        }
    }

    // --------------------
    // GMCM
    // --------------------

    private void RegisterGmcm()
    {
        IGenericModConfigMenuApi? gmcm =
            Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (gmcm is null)
            return;

        gmcm.Register(
            mod: ModManifest,
            reset: () => _config = new FarmhandConfig(),
            save: () => Helper.WriteConfig(_config)
        );

        gmcm.AddSectionTitle(ModManifest, () => Helper.Translation.Get("gmcm.section.main"));

        gmcm.AddBoolOption(
            ModManifest,
            () => _config.HelperEnabled,
            v => _config.HelperEnabled = v,
            name: () => Helper.Translation.Get("gmcm.helperEnabled.name"),
            tooltip: () => Helper.Translation.Get("gmcm.helperEnabled.tooltip")
        );

        gmcm.AddKeybind(
            ModManifest,
            () => _config.PlannerMenuKey,
            v => _config.PlannerMenuKey = v,
            name: () => Helper.Translation.Get("gmcm.key.name"),
            tooltip: () => Helper.Translation.Get("gmcm.key.tooltip")
        );

        gmcm.AddNumberOption(ModManifest, () => _config.StartHour, v => _config.StartHour = v,
            name: () => Helper.Translation.Get("gmcm.starthour.name"),
            tooltip: () => Helper.Translation.Get("gmcm.starthour.tooltip"),
            min: 4, max: 22, interval: 1);

        gmcm.AddNumberOption(ModManifest, () => _config.EndHour, v => _config.EndHour = v,
            name: () => Helper.Translation.Get("gmcm.endhour.name"),
            tooltip: () => Helper.Translation.Get("gmcm.endhour.tooltip"),
            min: 5, max: 24, interval: 1);

        gmcm.AddSectionTitle(ModManifest, () => Helper.Translation.Get("gmcm.section.tasks"));
        gmcm.AddBoolOption(ModManifest, () => _config.WaterCrops, v => _config.WaterCrops = v, () => Helper.Translation.Get("gmcm.water.name"));
        gmcm.AddBoolOption(ModManifest, () => _config.PetAnimals, v => _config.PetAnimals = v, () => Helper.Translation.Get("gmcm.pet.name"));
        gmcm.AddBoolOption(ModManifest, () => _config.FeedAnimals, v => _config.FeedAnimals = v, () => Helper.Translation.Get("gmcm.feed.name"));
        gmcm.AddBoolOption(ModManifest, () => _config.HarvestCrops, v => _config.HarvestCrops = v, () => Helper.Translation.Get("gmcm.harvest.name"));
        gmcm.AddBoolOption(ModManifest, () => _config.OrganizeChests, v => _config.OrganizeChests = v, () => Helper.Translation.Get("gmcm.chests.name"));

        gmcm.AddSectionTitle(ModManifest, () => Helper.Translation.Get("gmcm.section.harvest"));
        gmcm.AddBoolOption(ModManifest, () => _config.HarvestLowTierOnly, v => _config.HarvestLowTierOnly = v,
            name: () => Helper.Translation.Get("gmcm.lowtier.name"),
            tooltip: () => Helper.Translation.Get("gmcm.lowtier.tooltip"));

        gmcm.AddNumberOption(ModManifest, () => _config.HarvestValueCap, v => _config.HarvestValueCap = v,
            name: () => Helper.Translation.Get("gmcm.harvestcap.name"),
            tooltip: () => Helper.Translation.Get("gmcm.harvestcap.tooltip"),
            min: 0, max: 5000, interval: 10);

        gmcm.AddBoolOption(ModManifest, () => _config.HarvestExcludeFlowers, v => _config.HarvestExcludeFlowers = v,
            name: () => Helper.Translation.Get("gmcm.excludeflowers.name"),
            tooltip: () => Helper.Translation.Get("gmcm.excludeflowers.tooltip"));

        gmcm.AddSectionTitle(ModManifest, () => Helper.Translation.Get("gmcm.section.costs"));
        gmcm.AddParagraph(ModManifest, () => Helper.Translation.Get("gmcm.costs.note"));

        gmcm.AddNumberOption(ModManifest, () => _config.CostWaterCrops, v => _config.CostWaterCrops = v, () => Helper.Translation.Get("gmcm.cost.water"), min: 0, max: 10000, interval: 10);
        gmcm.AddNumberOption(ModManifest, () => _config.CostPetAnimals, v => _config.CostPetAnimals = v, () => Helper.Translation.Get("gmcm.cost.pet"), min: 0, max: 10000, interval: 10);
        gmcm.AddNumberOption(ModManifest, () => _config.CostFeedAnimals, v => _config.CostFeedAnimals = v, () => Helper.Translation.Get("gmcm.cost.feed"), min: 0, max: 10000, interval: 10);
        gmcm.AddNumberOption(ModManifest, () => _config.CostHarvestCrops, v => _config.CostHarvestCrops = v, () => Helper.Translation.Get("gmcm.cost.harvest"), min: 0, max: 10000, interval: 10);
        gmcm.AddNumberOption(ModManifest, () => _config.CostOrganizeChests, v => _config.CostOrganizeChests = v, () => Helper.Translation.Get("gmcm.cost.chests"), min: 0, max: 10000, interval: 10);
    }
}

public sealed class FarmhandState
{
    public bool HiredToday { get; set; }
    public TodayPlan Plan { get; set; } = TodayPlan.Disabled();
}

public readonly record struct TodayPlan(
    bool WaterCrops,
    bool PetAnimals,
    bool FeedAnimals,
    bool HarvestCrops,
    bool HarvestLowTierOnly,
    int HarvestValueCap,
    bool HarvestExcludeFlowers,
    bool OrganizeChests,
    int StartHour,
    int EndHour
)
{
    public static TodayPlan Disabled() => new(
        WaterCrops: false,
        PetAnimals: false,
        FeedAnimals: false,
        HarvestCrops: false,
        HarvestLowTierOnly: false,
        HarvestValueCap: 0,
        HarvestExcludeFlowers: false,
        OrganizeChests: false,
        StartHour: 6,
        EndHour: 18
    );

    public static TodayPlan FromConfig(FarmhandConfig c) => new(
        WaterCrops: c.WaterCrops,
        PetAnimals: c.PetAnimals,
        FeedAnimals: c.FeedAnimals,
        HarvestCrops: c.HarvestCrops,
        HarvestLowTierOnly: c.HarvestLowTierOnly,
        HarvestValueCap: c.HarvestValueCap,
        HarvestExcludeFlowers: c.HarvestExcludeFlowers,
        OrganizeChests: c.OrganizeChests,
        StartHour: c.StartHour,
        EndHour: c.EndHour
    );
}
