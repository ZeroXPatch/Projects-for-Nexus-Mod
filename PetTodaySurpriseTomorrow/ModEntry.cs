using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Netcode;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Characters;
using StardewValley.ItemRegistry;

namespace PetTodaySurpriseTomorrow;

internal sealed class ModEntry : Mod
{
    private const string SaveDataKey = "state";

    private static readonly FieldInfo PetBowlWateredField = AccessTools.Field(typeof(Farm), "petBowlWatered");
    private static readonly PropertyInfo? PetBowlWateredProperty = AccessTools.Property(typeof(Farm), "PetBowlWatered");
    private static readonly FieldInfo PetBowlPositionField = AccessTools.Field(typeof(Farm), "petBowlPosition");
    private static readonly PropertyInfo? PetBowlPositionProperty = AccessTools.Property(typeof(Farm), "petBowlPosition");
    private static readonly MethodInfo? PetBowlPositionMethod = AccessTools.Method(typeof(Farm), "GetPetBowlTile");
    private static readonly FieldInfo PetLastPetDayField = AccessTools.Field(typeof(Pet), "lastPetDay");
    private static readonly PropertyInfo? PetLastPetDayProperty = AccessTools.Property(typeof(Pet), "LastPetDay");

    private Harmony? harmony;

    public static ModEntry? Instance { get; private set; }

    private ModConfig Config { get; set; } = new();

    private SaveData SaveState { get; set; } = new();

    public override void Entry(IModHelper helper)
    {
        Instance = this;
        this.Config = helper.ReadConfig<ModConfig>() ?? new ModConfig();

        helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
        helper.Events.GameLoop.Saving += this.OnSaving;
        helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
        helper.Events.GameLoop.DayStarted += this.OnDayStarted;
        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        helper.Multiplayer.ModMessageReceived += this.OnModMessageReceived;

        this.harmony = new Harmony(this.ModManifest.UniqueID);
        this.PatchPettingDetection();
        this.PatchCrows();
    }

    private void PatchPettingDetection()
    {
        var target = AccessTools.Method(typeof(Pet), nameof(Pet.checkAction));
        if (target is null)
        {
            this.Monitor.Log("Failed to patch petting detection: Pet.checkAction not found.", LogLevel.Warn);
            return;
        }

        var postfix = new HarmonyMethod(typeof(ModEntry), nameof(AfterPetCheckAction));
        this.harmony!.Patch(target, postfix: postfix);
    }

    private void PatchCrows()
    {
        var target = AccessTools.Method(typeof(Farm), "addCrows");
        if (target is null)
        {
            this.Monitor.Log("Failed to patch crow protection: Farm.addCrows not found.", LogLevel.Warn);
            return;
        }

        var prefix = new HarmonyMethod(typeof(ModEntry), nameof(BeforeAddCrows));
        this.harmony!.Patch(target, prefix: prefix);
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (gmcm is null)
        {
            return;
        }

        gmcm.Register(this.ModManifest, () => this.Config = new ModConfig(), () => this.Helper.WriteConfig(this.Config));

        gmcm.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("gmcm.section.general"));
        gmcm.AddTextOption(
            this.ModManifest,
            () => this.Config.DropStyle.ToString(),
            value =>
            {
                if (Enum.TryParse<RewardDropStyle>(value, out var parsed))
                {
                    this.Config.DropStyle = parsed;
                }
            },
            () => this.Helper.Translation.Get("gmcm.action.dropStyle"),
            allowedValues: Enum.GetNames(typeof(RewardDropStyle))
        );
        gmcm.AddBoolOption(
            this.ModManifest,
            () => this.Config.HostOnlyPetting,
            value => this.Config.HostOnlyPetting = value,
            () => this.Helper.Translation.Get("gmcm.action.hostOnlyPetting")
        );

        gmcm.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("gmcm.section.actions"));
        gmcm.AddNumberOption(
            this.ModManifest,
            () => this.Config.BringItemWeight,
            value => this.Config.BringItemWeight = value,
            () => this.Helper.Translation.Get("gmcm.action.bring"),
            min: 0,
            max: 999
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            () => this.Config.FillBowlWeight,
            value => this.Config.FillBowlWeight = value,
            () => this.Helper.Translation.Get("gmcm.action.bowl"),
            min: 0,
            max: 999
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            () => this.Config.ScareCrowsWeight,
            value => this.Config.ScareCrowsWeight = value,
            () => this.Helper.Translation.Get("gmcm.action.crows"),
            min: 0,
            max: 999
        );
        gmcm.AddBoolOption(
            this.ModManifest,
            () => this.Config.EnableComedyItems,
            value => this.Config.EnableComedyItems = value,
            () => this.Helper.Translation.Get("gmcm.action.comedy")
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            () => (int)(this.Config.ComedyChance * 100),
            value => this.Config.ComedyChance = Math.Clamp(value / 100d, 0d, 1d),
            () => this.Helper.Translation.Get("gmcm.action.comedyChance"),
            min: 0,
            max: 100,
            interval: 1,
            formatValue: val => $"{val}%"
        );
        gmcm.AddBoolOption(
            this.ModManifest,
            () => this.Config.EnableWeatherSkews,
            value => this.Config.EnableWeatherSkews = value,
            () => this.Helper.Translation.Get("gmcm.action.weatherSkews")
        );

        gmcm.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("gmcm.section.items"));
        gmcm.AddNumberOption(
            this.ModManifest,
            () => this.Config.ResourcesWeight,
            value => this.Config.ResourcesWeight = value,
            () => this.Helper.Translation.Get("gmcm.items.resources"),
            min: 0,
            max: 999
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            () => this.Config.SeasonalForageWeight,
            value => this.Config.SeasonalForageWeight = value,
            () => this.Helper.Translation.Get("gmcm.items.forage"),
            min: 0,
            max: 999
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            () => this.Config.BeachFindsWeight,
            value => this.Config.BeachFindsWeight = value,
            () => this.Helper.Translation.Get("gmcm.items.beach"),
            min: 0,
            max: 999
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            () => this.Config.RainBeachSkew,
            value => this.Config.RainBeachSkew = value,
            () => this.Helper.Translation.Get("gmcm.items.rainBeach"),
            min: -999,
            max: 999
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            () => this.Config.RainResourceSkew,
            value => this.Config.RainResourceSkew = value,
            () => this.Helper.Translation.Get("gmcm.items.rainResource"),
            min: -999,
            max: 999
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            () => this.Config.RainForageSkew,
            value => this.Config.RainForageSkew = value,
            () => this.Helper.Translation.Get("gmcm.items.rainForage"),
            min: -999,
            max: 999
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            () => this.Config.WinterForageSkew,
            value => this.Config.WinterForageSkew = value,
            () => this.Helper.Translation.Get("gmcm.items.winterForage"),
            min: -999,
            max: 999
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            () => this.Config.WinterResourceSkew,
            value => this.Config.WinterResourceSkew = value,
            () => this.Helper.Translation.Get("gmcm.items.winterResource"),
            min: -999,
            max: 999
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            () => this.Config.WinterBeachSkew,
            value => this.Config.WinterBeachSkew = value,
            () => this.Helper.Translation.Get("gmcm.items.winterBeach"),
            min: -999,
            max: 999
        );

        gmcm.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("gmcm.section.resources"));
        gmcm.AddNumberOption(
            this.ModManifest,
            () => this.Config.WoodMin,
            value => this.Config.WoodMin = value,
            () => this.Helper.Translation.Get("gmcm.resources.woodMin"),
            min: 1,
            max: 999
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            () => this.Config.WoodMax,
            value => this.Config.WoodMax = value,
            () => this.Helper.Translation.Get("gmcm.resources.woodMax"),
            min: 1,
            max: 999
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            () => this.Config.StoneMin,
            value => this.Config.StoneMin = value,
            () => this.Helper.Translation.Get("gmcm.resources.stoneMin"),
            min: 1,
            max: 999
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            () => this.Config.StoneMax,
            value => this.Config.StoneMax = value,
            () => this.Helper.Translation.Get("gmcm.resources.stoneMax"),
            min: 1,
            max: 999
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            () => this.Config.FiberMin,
            value => this.Config.FiberMin = value,
            () => this.Helper.Translation.Get("gmcm.resources.fiberMin"),
            min: 1,
            max: 999
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            () => this.Config.FiberMax,
            value => this.Config.FiberMax = value,
            () => this.Helper.Translation.Get("gmcm.resources.fiberMax"),
            min: 1,
            max: 999
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            () => this.Config.MixedSeedsMin,
            value => this.Config.MixedSeedsMin = value,
            () => this.Helper.Translation.Get("gmcm.resources.mixedSeedsMin"),
            min: 1,
            max: 999
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            () => this.Config.MixedSeedsMax,
            value => this.Config.MixedSeedsMax = value,
            () => this.Helper.Translation.Get("gmcm.resources.mixedSeedsMax"),
            min: 1,
            max: 999
        );
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        this.SaveState = this.Helper.Data.ReadSaveData<SaveData>(SaveDataKey) ?? new SaveData();
    }

    private void OnSaving(object? sender, SavingEventArgs e)
    {
        this.WriteSaveData();
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        this.SaveState = new SaveData();
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        if (!Context.IsWorldReady)
        {
            return;
        }

        if (Context.IsMainPlayer && this.SaveState.PettedYesterday)
        {
            this.ResolveDailyAction();
        }

        this.SaveState.PettedYesterday = this.SaveState.PettedToday;
        this.SaveState.PettedToday = false;
        this.WriteSaveData();
    }

    private void ResolveDailyAction()
    {
        var farm = Game1.getFarm();
        if (farm is null)
        {
            return;
        }

        var actions = new List<(PetAction action, int weight)>
        {
            (PetAction.BringItem, this.Config.BringItemWeight),
            (PetAction.FillBowl, this.Config.FillBowlWeight),
            (PetAction.ScareCrows, this.Config.ScareCrowsWeight)
        };

        var selectable = actions.Where(a => a.weight > 0).ToList();
        if (selectable.Count == 0)
        {
            this.Monitor.Log("No actions are enabled (all weights are zero).", LogLevel.Info);
            return;
        }

        var selectedAction = this.WeightedPick(selectable);
        switch (selectedAction)
        {
            case PetAction.BringItem:
                this.GiveItem(farm);
                break;
            case PetAction.FillBowl:
                this.FillWaterBowl(farm);
                break;
            case PetAction.ScareCrows:
                this.EnableCrowProtection();
                break;
        }
    }

    private void GiveItem(Farm farm)
    {
        var random = Game1.random;
        Item? item;
        string messageKey;

        if (this.Config.EnableComedyItems && random.NextDouble() < this.Config.ComedyChance)
        {
            item = this.GetComedyItem(random);
            messageKey = random.Next(3) switch
            {
                0 => "message.comedy.1",
                1 => "message.comedy.2",
                _ => "message.comedy.3"
            };
        }
        else
        {
            item = this.GetStandardItem(farm, random);
            messageKey = "message.standard";
        }

        if (item is null)
        {
            return;
        }

        var tile = this.GetDropTile(farm);
        var position = tile * Game1.tileSize;
        Game1.createItemDebris(item, position, -1, farm);
        Game1.showGlobalMessage(this.Helper.Translation.Get(messageKey));
    }

    private void FillWaterBowl(Farm farm)
    {
        if (PetBowlWateredField?.GetValue(farm) is NetBool netBool)
        {
            if (netBool.Value)
            {
                Game1.showGlobalMessage(this.Helper.Translation.Get("message.fillBowlAlreadyFull"));
                return;
            }

            netBool.Value = true;
            Game1.showGlobalMessage(this.Helper.Translation.Get("message.fillBowl"));
            return;
        }

        if (PetBowlWateredProperty?.GetValue(farm) is bool isWatered)
        {
            if (isWatered)
            {
                Game1.showGlobalMessage(this.Helper.Translation.Get("message.fillBowlAlreadyFull"));
                return;
            }

            PetBowlWateredProperty.SetValue(farm, true);
            Game1.showGlobalMessage(this.Helper.Translation.Get("message.fillBowl"));
        }
        else
        {
            this.Monitor.Log("Unable to locate pet bowl water flag; no changes applied.", LogLevel.Warn);
        }
    }

    private void EnableCrowProtection()
    {
        this.SaveState.CrowProtectionTonight = true;
        Game1.showGlobalMessage(this.Helper.Translation.Get("message.crowProtection"));
        this.WriteSaveData();
    }

    private Item? GetComedyItem(Random random)
    {
        var comedyItems = new[]
        {
            ItemRegistry.QualifiedObjectId("Joja Cola"),
            ItemRegistry.QualifiedObjectId("Driftwood"),
            ItemRegistry.QualifiedObjectId("Broken Glasses"),
            ItemRegistry.QualifiedObjectId("Slime"),
            ItemRegistry.QualifiedObjectId("Sap")
        };

        var index = random.Next(comedyItems.Length);
        return ItemRegistry.Create(comedyItems[index], 1);
    }

    private Item? GetStandardItem(Farm farm, Random random)
    {
        var category = this.GetItemCategory(farm, random);
        return category switch
        {
            ItemCategory.Resources => this.GetResourceItem(random, farm),
            ItemCategory.SeasonalForage => this.GetSeasonalForage(random, farm),
            ItemCategory.BeachFinds => this.GetBeachFind(random),
            _ => null
        };
    }

    private ItemCategory GetItemCategory(Farm farm, Random random)
    {
        var season = Game1.GetSeasonForLocation(farm);

        var resources = this.Config.ResourcesWeight;
        var forage = this.Config.SeasonalForageWeight;
        var beach = this.Config.BeachFindsWeight;

        if (this.Config.EnableWeatherSkews)
        {
            var raining = Game1.isRaining || Game1.IsRainingHere(farm) || Game1.isLightning;
            if (raining)
            {
                resources += this.Config.RainResourceSkew;
                forage += this.Config.RainForageSkew;
                beach += this.Config.RainBeachSkew;
            }

            if (season.Equals("winter", StringComparison.OrdinalIgnoreCase))
            {
                resources += this.Config.WinterResourceSkew;
                forage += this.Config.WinterForageSkew;
                beach += this.Config.WinterBeachSkew;
            }
        }

        resources = Math.Max(0, resources);
        forage = Math.Max(0, forage);
        beach = Math.Max(0, beach);

        var options = new List<(ItemCategory category, int weight)>
        {
            (ItemCategory.Resources, resources),
            (ItemCategory.SeasonalForage, forage),
            (ItemCategory.BeachFinds, beach)
        };

        var selectable = options.Where(option => option.weight > 0).ToList();
        if (selectable.Count == 0)
        {
            this.Monitor.Log("All item category weights are zero; defaulting to resources.", LogLevel.Trace);
            return ItemCategory.Resources;
        }

        return this.WeightedPick(selectable);
    }

    private Item? GetResourceItem(Random random, Farm farm)
    {
        var entries = new List<ResourceEntry>
        {
            new(ItemRegistry.QualifiedObjectId("Wood"), this.Config.WoodMin, this.Config.WoodMax, 1f, false),
            new(ItemRegistry.QualifiedObjectId("Stone"), this.Config.StoneMin, this.Config.StoneMax, 1f, false),
            new(ItemRegistry.QualifiedObjectId("Fiber"), this.Config.FiberMin, this.Config.FiberMax, 1f, true),
            new(ItemRegistry.QualifiedObjectId("Mixed Seeds"), this.Config.MixedSeedsMin, this.Config.MixedSeedsMax, 1f, false)
        };

        var season = Game1.GetSeasonForLocation(farm);
        if (season.Equals("winter", StringComparison.OrdinalIgnoreCase))
        {
            entries = entries.Where(e => !e.SkipInWinter).ToList();
        }

        if (entries.Count == 0)
        {
            return null;
        }

        var weights = entries.Select(e => (entry: e, weight: (int)(e.Weight * 100))).ToList();
        var chosen = this.WeightedPick(weights);
        var stack = random.Next(chosen.MinStack, chosen.MaxStack + 1);
        return ItemRegistry.Create(chosen.ItemId, stack);
    }

    private Item? GetSeasonalForage(Random random, Farm farm)
    {
        var season = Game1.GetSeasonForLocation(farm).ToLowerInvariant();
        var pool = season switch
        {
            "spring" => new[]
            {
                ItemRegistry.QualifiedObjectId("Wild Horseradish"),
                ItemRegistry.QualifiedObjectId("Daffodil"),
                ItemRegistry.QualifiedObjectId("Leek"),
                ItemRegistry.QualifiedObjectId("Dandelion")
            },
            "summer" => new[]
            {
                ItemRegistry.QualifiedObjectId("Spice Berry"),
                ItemRegistry.QualifiedObjectId("Sweet Pea"),
                ItemRegistry.QualifiedObjectId("Grape")
            },
            "fall" => new[]
            {
                ItemRegistry.QualifiedObjectId("Common Mushroom"),
                ItemRegistry.QualifiedObjectId("Wild Plum"),
                ItemRegistry.QualifiedObjectId("Hazelnut"),
                ItemRegistry.QualifiedObjectId("Blackberry")
            },
            "winter" => new[]
            {
                ItemRegistry.QualifiedObjectId("Winter Root"),
                ItemRegistry.QualifiedObjectId("Crystal Fruit"),
                ItemRegistry.QualifiedObjectId("Snow Yam"),
                ItemRegistry.QualifiedObjectId("Crocus")
            },
            _ => Array.Empty<string>()
        };

        if (pool.Length == 0)
        {
            return null;
        }

        var itemId = pool[random.Next(pool.Length)];
        return ItemRegistry.Create(itemId, 1);
    }

    private Item? GetBeachFind(Random random)
    {
        var pool = new[]
        {
            ItemRegistry.QualifiedObjectId("Cockle"),
            ItemRegistry.QualifiedObjectId("Mussel"),
            ItemRegistry.QualifiedObjectId("Oyster"),
            ItemRegistry.QualifiedObjectId("Clam"),
            ItemRegistry.QualifiedObjectId("Coral"),
            ItemRegistry.QualifiedObjectId("Sea Urchin")
        };

        var itemId = pool[random.Next(pool.Length)];
        return ItemRegistry.Create(itemId, 1);
    }

    private Vector2 GetDropTile(Farm farm)
    {
        switch (this.Config.DropStyle)
        {
            case RewardDropStyle.NearBowl:
                return this.GetBowlTile(farm) ?? Game1.player.getTileLocation();
            case RewardDropStyle.NearPet:
                var petTile = farm.characters.OfType<Pet>().FirstOrDefault()?.getTileLocation();
                if (petTile.HasValue)
                {
                    return petTile.Value;
                }

                break;
            case RewardDropStyle.NearPlayer:
                return Game1.player.getTileLocation();
        }

        return this.GetBowlTile(farm) ?? Game1.player.getTileLocation();
    }

    private Vector2? GetBowlTile(Farm farm)
    {
        if (PetBowlPositionProperty?.GetValue(farm) is NetPoint netPoint)
        {
            return new Vector2(netPoint.X, netPoint.Y);
        }

        if (PetBowlPositionField?.GetValue(farm) is NetPoint fieldPoint)
        {
            return new Vector2(fieldPoint.X, fieldPoint.Y);
        }

        if (PetBowlPositionMethod != null)
        {
            var value = PetBowlPositionMethod.Invoke(farm, Array.Empty<object>());
            switch (value)
            {
                case Vector2 vec:
                    return vec;
                case Point pt:
                    return new Vector2(pt.X, pt.Y);
            }
        }

        return null;
    }

    private void WriteSaveData()
    {
        if (Context.IsWorldReady)
        {
            this.Helper.Data.WriteSaveData(SaveDataKey, this.SaveState);
        }
    }

    private TWeighted WeightedPick<TWeighted>(IEnumerable<(TWeighted item, int weight)> items)
    {
        var pairs = items.Where(p => p.weight > 0).ToList();
        if (pairs.Count == 0)
        {
            return default!;
        }

        var total = pairs.Sum(p => p.weight);
        var roll = Game1.random.Next(total);
        foreach (var pair in pairs)
        {
            roll -= pair.weight;
            if (roll < 0)
            {
                return pair.item;
            }
        }

        return pairs.Last().item;
    }

    private void OnModMessageReceived(object? sender, ModMessageReceivedEventArgs e)
    {
        if (!Context.IsMainPlayer || e.FromModID != this.ModManifest.UniqueID)
        {
            return;
        }

        if (string.Equals(e.Type, "petted-today", StringComparison.OrdinalIgnoreCase))
        {
            this.SaveState.PettedToday = true;
            this.WriteSaveData();
        }
    }

    private static bool WasActuallyPetted(Pet pet)
    {
        if (PetLastPetDayField?.GetValue(pet) is NetInt netLastPet)
        {
            return netLastPet.Value == Game1.Date.TotalDays;
        }

        if (PetLastPetDayProperty?.GetValue(pet) is int lastPetDay)
        {
            return lastPetDay == Game1.Date.TotalDays;
        }

        return true;
    }

    private static void AfterPetCheckAction(Pet __instance, Farmer who, ref bool __result)
    {
        if (!__result || Instance is null || __instance is null || who is null)
        {
            return;
        }

        if (!WasActuallyPetted(__instance))
        {
            return;
        }

        if (!Context.IsMainPlayer)
        {
            if (Instance.Config.HostOnlyPetting)
            {
                return;
            }

            Instance.Helper.Multiplayer.SendMessage(
                data: true,
                messageType: "petted-today",
                modIDs: new[] { Instance.ModManifest.UniqueID },
                playerIDs: new[] { Game1.MasterPlayer.UniqueMultiplayerID }
            );
            return;
        }

        Instance.SaveState.PettedToday = true;
        Instance.WriteSaveData();
    }

    private static bool BeforeAddCrows(Farm __instance)
    {
        if (Instance?.SaveState.CrowProtectionTonight == true)
        {
            Instance.Monitor.Log("Crow protection active. Skipping crow routine for tonight.", LogLevel.Debug);
            Instance.SaveState.CrowProtectionTonight = false;
            Instance.WriteSaveData();
            return false;
        }

        return true;
    }

    private enum PetAction
    {
        BringItem,
        FillBowl,
        ScareCrows
    }

    private enum ItemCategory
    {
        Resources,
        SeasonalForage,
        BeachFinds
    }
}

internal sealed class ModConfig
{
    public int BringItemWeight { get; set; } = 70;

    public int FillBowlWeight { get; set; } = 20;

    public int ScareCrowsWeight { get; set; } = 10;

    public bool EnableComedyItems { get; set; } = true;

    public double ComedyChance { get; set; } = 0.08;

    public bool EnableWeatherSkews { get; set; } = true;

    public RewardDropStyle DropStyle { get; set; } = RewardDropStyle.NearBowl;

    public bool HostOnlyPetting { get; set; } = true;

    public int ResourcesWeight { get; set; } = 50;

    public int SeasonalForageWeight { get; set; } = 35;

    public int BeachFindsWeight { get; set; } = 15;

    public int RainBeachSkew { get; set; } = 25;

    public int RainResourceSkew { get; set; } = -10;

    public int RainForageSkew { get; set; } = -15;

    public int WinterForageSkew { get; set; } = 20;

    public int WinterResourceSkew { get; set; } = -10;

    public int WinterBeachSkew { get; set; } = -10;

    public int WoodMin { get; set; } = 5;

    public int WoodMax { get; set; } = 20;

    public int StoneMin { get; set; } = 5;

    public int StoneMax { get; set; } = 20;

    public int FiberMin { get; set; } = 5;

    public int FiberMax { get; set; } = 15;

    public int MixedSeedsMin { get; set; } = 1;

    public int MixedSeedsMax { get; set; } = 5;
}

internal sealed class SaveData
{
    public bool PettedToday { get; set; }

    public bool PettedYesterday { get; set; }

    public bool CrowProtectionTonight { get; set; }
}

int sealed record ResourceEntry(string ItemId, int MinStack, int MaxStack, float Weight, bool SkipInWinter);

internal enum RewardDropStyle
{
    NearBowl,
    NearPet,
    NearPlayer
}

public interface IGenericModConfigMenuApi
{
    void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);

    void AddSectionTitle(IManifest mod, Func<string> text, Func<string>? tooltip = null);

    void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue, Func<string> name, Func<string>? tooltip = null, string? fieldId = null);

    void AddNumberOption(IManifest mod, Func<int> getValue, Action<int> setValue, Func<string> name, Func<string>? tooltip = null, int? min = null, int? max = null, int? interval = null, Func<int, string>? formatValue = null, string? fieldId = null);

    void AddTextOption(IManifest mod, Func<string> getValue, Action<string> setValue, Func<string> name, Func<string>? tooltip = null, string[]? allowedValues = null, Func<string, string>? formatAllowedValue = null, string? fieldId = null);
}
