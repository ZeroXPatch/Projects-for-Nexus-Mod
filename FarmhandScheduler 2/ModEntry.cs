using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;
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

        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.GameLoop.TimeChanged += OnTimeChanged;
        helper.Events.Input.ButtonReleased += OnButtonReleased;
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        _state = new FarmhandState();
        _lastTaskExecution = -1;
    }

    /// <summary>
    /// Charge same morning for today's selected tasks. If cannot pay, no tasks run today.
    /// Locks today's plan after payment.
    /// </summary>
    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        _lastTaskExecution = -1;

        _state.HiredToday = false;
        _state.Plan = TodayPlan.Disabled();

        if (!_config.HelperEnabled)
            return;

        int todayBill = Math.Max(0, _config.CalculatePlannedBill());

        if (Game1.player.Money < todayBill)
        {
            Game1.addHUDMessage(new HUDMessage(
                Helper.Translation.Get("hud.bill.unpaid", new { amount = todayBill }),
                HUDMessage.error_type));
            return;
        }

        if (todayBill > 0)
        {
            Game1.player.Money -= todayBill;
            Game1.addHUDMessage(new HUDMessage(
                Helper.Translation.Get("hud.bill.paid", new { amount = todayBill }),
                HUDMessage.newQuest_type));
        }
        else
        {
            Game1.addHUDMessage(new HUDMessage(
                Helper.Translation.Get("hud.bill.free"),
                HUDMessage.newQuest_type));
        }

        _state.HiredToday = true;
        _state.Plan = TodayPlan.FromConfig(_config);

        Game1.addHUDMessage(new HUDMessage(
            Helper.Translation.Get("hud.hired.ok"),
            HUDMessage.newQuest_type));
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
            HarvestCrops(
                lowTierOnly: _state.Plan.HarvestLowTierOnly,
                valueCap: _state.Plan.HarvestValueCap,
                excludeFlowers: _state.Plan.HarvestExcludeFlowers
            );

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
        Farm farm = Game1.getFarm();

        foreach (FarmAnimal animal in farm.getAllFarmAnimals())
        {
            if (!animal.wasPet.Value)
                animal.pet(Game1.player);
        }
    }

    private void FeedAnimals()
    {
        Farm farm = Game1.getFarm();

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

            StardewValley.Object? obj = StardewValley.ItemRegistry.Create(harvestId, 1) as StardewValley.Object;

            // NEW: exclude flower crops (keep beehive boost)
            if (excludeFlowers && obj is not null && obj.Category == StardewValley.Object.flowersCategory)
                continue;

            // Low-tier filter uses price; unknown objects treated as expensive.
            int itemPrice = obj?.Price ?? int.MaxValue;
            if (lowTierOnly && itemPrice > valueCap)
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
        Monitor.Log("Planner saved.", LogLevel.Info);
    }

    private void ApplyLiveConfig(FarmhandConfig config)
    {
        _config = config;

        // Today is locked if already paid, so changes apply tomorrow.
        if (Context.IsWorldReady && _state.HiredToday)
        {
            Game1.addHUDMessage(new HUDMessage(
                Helper.Translation.Get("hud.changes.tomorrow"),
                HUDMessage.newQuest_type));
        }
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
