using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;
using StardewValley.Locations;
using StardewValley.Objects;
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
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.TimeChanged += OnTimeChanged;
        helper.Events.Input.ButtonReleased += OnButtonReleased;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        Monitor.Log("Farmhand Scheduler ready.", LogLevel.Info);
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        _state = new FarmhandState();
        _lastTaskExecution = -1;
        Monitor.Log("Save loaded; farmhand reset for the day.", LogLevel.Trace);
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        _state.HiredToday = _config.HelperEnabled;

        if (!_state.HiredToday)
        {
            
            return;
        }

        if (Game1.player.Money < _config.DailyCost)
        {
            _state.HiredToday = false;

            // red “!” error-style popup
            Game1.addHUDMessage(
                new HUDMessage("Not enough gold for your farmhand today.", HUDMessage.error_type)
            );
            return;
        }

        Game1.player.Money -= _config.DailyCost;

        // yellow “!” quest-style popup
        Game1.addHUDMessage(
            new HUDMessage($"Farmhand hired for {_config.DailyCost}g.", HUDMessage.newQuest_type));

        _lastTaskExecution = -1;
    }

    private void OnButtonReleased(object? sender, ButtonReleasedEventArgs e)
    {
        // only react when the player is free and the configured key was released
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
        int start = _config.StartHour * 100;
        int end = _config.EndHour * 100;
        return currentTime >= start && currentTime <= end;
    }

    private void TryPerformTasks()
    {
        if (_config.WaterCrops)
            WaterCrops();

        if (_config.PetAnimals)
            PetAnimals();

        if (_config.FeedAnimals)
            FeedAnimals();

        if (_config.HarvestCrops)
            HarvestCrops();

        if (_config.OrganizeChests)
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
                        // UpdateNeighbors() doesn’t exist in 1.6, and isn’t required here.
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

    private void HarvestCrops()
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

            // 1.6: indexOfHarvest is a qualified item ID string, not an int
            string harvestId = dirt.crop.indexOfHarvest.Value;

            // create a sample item so we can read its price
            int itemPrice;
            if (StardewValley.ItemRegistry.Create(harvestId, 1) is StardewValley.Object obj)
            {
                itemPrice = obj.Price;
            }
            else
            {
                // non-object items are treated as "expensive" so we don't touch them
                itemPrice = int.MaxValue;
            }

            if (_config.HarvestLowTierOnly && itemPrice > _config.HarvestValueCap)
                continue;

            // use the built-in harvest logic so regrowth, quality, professions, etc all work
            // signature (1.5+/1.6): bool harvest(int x, int y, HoeDirt soil, JunimoHarvester? junimo, bool isScythe)
            _ = dirt.crop.harvest((int)pair.Key.X, (int)pair.Key.Y, dirt, null, false);
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
        _state.HiredToday = config.HelperEnabled;
    }
}

public sealed class FarmhandState
{
    public bool HiredToday { get; set; }
}
