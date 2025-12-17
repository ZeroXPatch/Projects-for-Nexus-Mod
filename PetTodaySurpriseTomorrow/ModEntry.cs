#nullable enable

using HarmonyLib;
using Microsoft.Xna.Framework;
using Netcode;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Locations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PetTodaySurpriseTomorrow;

namespace PetTodaySurpriseTomorrow;

internal sealed class ModEntry : Mod
{
    private const string SaveDataKey = "state";
    private const string MpMessageType = "petted-today";

    private Harmony? harmony;

    private ModConfig Config = new();
    private SaveData State = new();

    // Reflection helpers (bowl + tiles) to avoid version quirks.
    private static readonly MethodInfo? CharacterGetTileLocationMethod = AccessTools.Method(typeof(Character), "getTileLocation");
    private static readonly FieldInfo? FarmPetBowlWateredField = AccessTools.Field(typeof(Farm), "petBowlWatered");
    private static readonly PropertyInfo? FarmPetBowlWateredProperty = AccessTools.Property(typeof(Farm), "PetBowlWatered");
    private static readonly FieldInfo? FarmPetBowlPositionField = AccessTools.Field(typeof(Farm), "petBowlPosition");
    private static readonly PropertyInfo? FarmPetBowlPositionProperty = AccessTools.Property(typeof(Farm), "petBowlPosition");
    private static readonly MethodInfo? FarmGetPetBowlTileMethod = AccessTools.Method(typeof(Farm), "GetPetBowlTile");

    public static ModEntry? Instance { get; private set; }

    public override void Entry(IModHelper helper)
    {
        Instance = this;

        this.Config = helper.ReadConfig<ModConfig>() ?? new ModConfig();

        helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
        helper.Events.GameLoop.Saving += this.OnSaving;
        helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
        helper.Events.GameLoop.DayStarted += this.OnDayStarted;
        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;

        helper.Events.Multiplayer.ModMessageReceived += this.OnModMessageReceived;

        this.harmony = new Harmony(this.ModManifest.UniqueID);
        this.ApplyHarmonyPatches();
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (gmcm is null)
            return;

        gmcm.Register(this.ModManifest, () => this.Config = new ModConfig(), () => this.Helper.WriteConfig(this.Config));

        gmcm.AddSectionTitle(this.ModManifest, () => this.T("gmcm.section.general"));

        gmcm.AddBoolOption(
            this.ModManifest,
            getValue: () => this.Config.HostOnlyPettingDetection,
            setValue: v => this.Config.HostOnlyPettingDetection = v,
            name: () => this.T("gmcm.action.hostOnlyPetting")
        );

        gmcm.AddSectionTitle(this.ModManifest, () => this.T("gmcm.section.actions"));

        gmcm.AddNumberOption(this.ModManifest, () => this.Config.BringItemWeight, v => this.Config.BringItemWeight = v,
            () => this.T("gmcm.action.bring"), min: 0, max: 999);

        gmcm.AddNumberOption(this.ModManifest, () => this.Config.FillBowlWeight, v => this.Config.FillBowlWeight = v,
            () => this.T("gmcm.action.bowl"), min: 0, max: 999);

        gmcm.AddNumberOption(this.ModManifest, () => this.Config.ScareCrowsWeight, v => this.Config.ScareCrowsWeight = v,
            () => this.T("gmcm.action.crows"), min: 0, max: 999);

        gmcm.AddBoolOption(this.ModManifest,
            () => this.Config.EnableComedyItems,
            v => this.Config.EnableComedyItems = v,
            () => this.T("gmcm.action.comedy"));

        gmcm.AddNumberOption(this.ModManifest,
            () => (int)Math.Round(this.Config.ComedyChance * 100),
            v => this.Config.ComedyChance = Math.Clamp(v / 100d, 0d, 1d),
            () => this.T("gmcm.action.comedyChance"),
            min: 0, max: 100, interval: 1,
            formatValue: v => $"{v}%");

        gmcm.AddBoolOption(this.ModManifest,
            () => this.Config.EnableWeatherSkews,
            v => this.Config.EnableWeatherSkews = v,
            () => this.T("gmcm.action.weatherSkews"));
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        if (!Context.IsWorldReady || Game1.player is null)
            return;

        this.State = this.Helper.Data.ReadSaveData<SaveData>(SaveDataKey) ?? new SaveData();

        // Safety: never persist "today" across load.
        this.State.PettedToday = false;
        this.WriteSaveData();
    }

    private void OnSaving(object? sender, SavingEventArgs e)
    {
        this.WriteSaveData();
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        this.State = new SaveData();
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        if (!Context.IsWorldReady || Game1.player is null)
            return;

        // Host-only reward resolution (per spec).
        if (!Context.IsMainPlayer)
        {
            // still do the carry-forward locally to avoid stale flags
            this.State.PettedYesterday = this.State.PettedToday;
            this.State.PettedToday = false;
            this.WriteSaveData();
            return;
        }

        // Rule 2: if yesterday was petted, do exactly one action.
        if (this.State.PettedYesterday)
        {
            this.State.PettedYesterday = false;
            this.PerformOneAction();
        }

        // Rule 3: carry forward
        this.State.PettedYesterday = this.State.PettedToday;
        this.State.PettedToday = false;

        this.WriteSaveData();
    }

    private void OnModMessageReceived(object? sender, ModMessageReceivedEventArgs e)
    {
        if (!Context.IsWorldReady || !Context.IsMainPlayer)
            return;

        if (e.FromModID != this.ModManifest.UniqueID)
            return;

        if (string.Equals(e.Type, MpMessageType, StringComparison.OrdinalIgnoreCase))
        {
            this.MarkPettedToday();
        }
    }

    // -------------------------
    // Core action selection
    // -------------------------

    private void PerformOneAction()
    {
        Farm? farm = Game1.getFarm();
        if (farm is null)
            return;

        int bring = Math.Max(0, this.Config.BringItemWeight);
        int bowl = Math.Max(0, this.Config.FillBowlWeight);
        int crows = Math.Max(0, this.Config.ScareCrowsWeight);

        int total = bring + bowl + crows;
        if (total <= 0)
            return;

        // stable per-day randomness
        Random rng = Utility.CreateDaySaveRandom();
        int roll = rng.Next(total);

        if (roll < bring)
            this.DoBringItem(farm, rng);
        else if (roll < bring + bowl)
            this.DoFillBowl(farm);
        else
            this.DoScareCrows();
    }

    // -------------------------
    // Action A: Bring item
    // -------------------------

    private void DoBringItem(Farm farm, Random rng)
    {
        Item? item;
        string messageKey;

        if (this.Config.EnableComedyItems && rng.NextDouble() < this.Config.ComedyChance)
        {
            string[] comedyPool =
            {
                QO(167), // Joja Cola
                QO(169), // Driftwood
                QO(170), // Broken Glasses
                QO(766), // Slime
                QO(92)   // Sap
            };

            item = ItemRegistry.Create(comedyPool[rng.Next(comedyPool.Length)], 1);

            messageKey = rng.Next(3) switch
            {
                0 => "message.comedy.1",
                1 => "message.comedy.2",
                _ => "message.comedy.3"
            };
        }
        else
        {
            item = this.GetStandardItem(farm, rng);
            messageKey = "message.standard";
        }

        if (item is null)
            return;

        Vector2 dropTile = this.GetDropTile(farm);
        Vector2 dropPixel = dropTile * Game1.tileSize + new Vector2(Game1.tileSize / 2f, Game1.tileSize / 2f);

        Game1.createItemDebris(item, dropPixel, -1, farm);
        this.ShowHud(this.T(messageKey));
    }

    private Item? GetStandardItem(Farm farm, Random rng)
    {
        int resources = this.Config.ResourcesWeight;
        int forage = this.Config.SeasonalForageWeight;
        int beach = this.Config.BeachFindsWeight;

        if (this.Config.EnableWeatherSkews)
        {
            // FIX: no Game1.isStorming. Lightning is the "storm" indicator in 1.6.
            bool rainingOrStorming = Game1.isRaining || Game1.isLightning;

            if (rainingOrStorming)
            {
                beach += 25;
                resources -= 10;
                forage -= 15;
            }

            string season = GetSeasonId(farm);
            if (season.Equals("winter", StringComparison.OrdinalIgnoreCase))
            {
                forage += 20;
                resources -= 10;
                beach -= 10;
            }
        }

        resources = Math.Max(0, resources);
        forage = Math.Max(0, forage);
        beach = Math.Max(0, beach);

        int total = resources + forage + beach;
        if (total <= 0)
            return null;

        int roll = rng.Next(total);
        if (roll < resources)
            return this.GetResourceItem(farm, rng);
        if (roll < resources + forage)
            return this.GetSeasonalForage(farm, rng);

        return this.GetBeachFind(rng);
    }

    private Item? GetResourceItem(Farm farm, Random rng)
    {
        string season = GetSeasonId(farm);
        bool isWinter = season.Equals("winter", StringComparison.OrdinalIgnoreCase);

        // FIX: named arg must match parameter name exactly (SkipInWinter, not skipInWinter)
        var entries = new List<ResourceEntry>
        {
            new(QO(388), this.Config.WoodMin, this.Config.WoodMax, SkipInWinter: false),      // Wood
            new(QO(390), this.Config.StoneMin, this.Config.StoneMax, SkipInWinter: false),   // Stone
            new(QO(771), this.Config.FiberMin, this.Config.FiberMax, SkipInWinter: true),    // Fiber
            new(QO(770), this.Config.MixedSeedsMin, this.Config.MixedSeedsMax, SkipInWinter: false) // Mixed Seeds
        };

        if (isWinter)
            entries = entries.Where(e => !e.SkipInWinter).ToList();

        entries = entries.Where(e => e.MinStack > 0 && e.MaxStack > 0 && e.MaxStack >= e.MinStack).ToList();
        if (entries.Count == 0)
            return null;

        var chosen = entries[rng.Next(entries.Count)];
        int stack = rng.Next(chosen.MinStack, chosen.MaxStack + 1);

        return ItemRegistry.Create(chosen.ItemId, stack);
    }

    private Item? GetSeasonalForage(Farm farm, Random rng)
    {
        string season = GetSeasonId(farm).ToLowerInvariant();

        string[] pool = season switch
        {
            "spring" => new[] { QO(16), QO(18), QO(20), QO(22) },
            "summer" => new[] { QO(396), QO(402), QO(398) },
            "fall" => new[] { QO(404), QO(406), QO(408), QO(410) },
            "winter" => new[] { QO(412), QO(414), QO(416), QO(418) },
            _ => Array.Empty<string>()
        };

        if (pool.Length == 0)
            return null;

        return ItemRegistry.Create(pool[rng.Next(pool.Length)], 1);
    }

    private Item GetBeachFind(Random rng)
    {
        string[] pool = { QO(718), QO(719), QO(723), QO(372), QO(393), QO(397) };
        return ItemRegistry.Create(pool[rng.Next(pool.Length)], 1);
    }

    // -------------------------
    // Action B: Fill bowl
    // -------------------------

    private void DoFillBowl(Farm farm)
    {
        if (TryGetBowlWatered(farm, out bool watered))
        {
            if (watered)
            {
                this.ShowHud(this.T("message.fillBowlAlreadyFull"));
                return;
            }

            if (TrySetBowlWatered(farm, true))
                this.ShowHud(this.T("message.fillBowl"));
            else
                this.ShowHud(this.T("message.fillBowl"));
        }
        else
        {
            // If we can’t locate it (future-proof), just show message.
            this.ShowHud(this.T("message.fillBowl"));
        }
    }

    private bool TryGetBowlWatered(Farm farm, out bool watered)
    {
        watered = false;

        if (FarmPetBowlWateredField?.GetValue(farm) is NetBool netBool)
        {
            watered = netBool.Value;
            return true;
        }

        if (FarmPetBowlWateredProperty?.GetValue(farm) is bool b)
        {
            watered = b;
            return true;
        }

        return false;
    }

    private bool TrySetBowlWatered(Farm farm, bool value)
    {
        if (FarmPetBowlWateredField?.GetValue(farm) is NetBool netBool)
        {
            netBool.Value = value;
            return true;
        }

        if (FarmPetBowlWateredProperty is not null && FarmPetBowlWateredProperty.CanWrite)
        {
            FarmPetBowlWateredProperty.SetValue(farm, value);
            return true;
        }

        return false;
    }

    // -------------------------
    // Action C: Crow protection
    // -------------------------

    private void DoScareCrows()
    {
        this.State.CrowProtectionTonight = true;
        this.WriteSaveData();
        this.ShowHud(this.T("message.crowProtection"));
    }

    // -------------------------
    // Drop location helpers
    // -------------------------

    private Vector2 GetDropTile(Farm farm)
    {
        return this.Config.RewardDropStyle switch
        {
            RewardDropStyle.NearBowl => this.GetBowlTile(farm) ?? GetCharacterTile(Game1.player),
            RewardDropStyle.NearPet => this.TryGetFarmPetTile(farm, out Vector2 petTile) ? petTile : GetCharacterTile(Game1.player),
            _ => GetCharacterTile(Game1.player)
        };
    }

    private bool TryGetFarmPetTile(Farm farm, out Vector2 tile)
    {
        var pet = farm.characters.OfType<Pet>().FirstOrDefault();
        if (pet is not null)
        {
            tile = GetCharacterTile(pet);
            return true;
        }

        tile = Vector2.Zero;
        return false;
    }

    private Vector2? GetBowlTile(Farm farm)
    {
        if (FarmPetBowlPositionProperty?.GetValue(farm) is NetPoint netPoint)
            return new Vector2(netPoint.X, netPoint.Y);

        if (FarmPetBowlPositionField?.GetValue(farm) is NetPoint fieldPoint)
            return new Vector2(fieldPoint.X, fieldPoint.Y);

        if (FarmGetPetBowlTileMethod is not null)
        {
            object? value = FarmGetPetBowlTileMethod.Invoke(farm, Array.Empty<object>());
            if (value is Vector2 v2)
                return v2;
            if (value is Point p)
                return new Vector2(p.X, p.Y);
        }

        return null;
    }

    private static Vector2 GetCharacterTile(Character character)
    {
        // Prefer 1.6 Tile property if present
        var tileProp = AccessTools.Property(character.GetType(), "Tile");
        if (tileProp?.GetValue(character) is Vector2 tile)
            return tile;

        // Legacy method via reflection
        if (CharacterGetTileLocationMethod is not null)
        {
            var val = CharacterGetTileLocationMethod.Invoke(character, Array.Empty<object>());
            if (val is Vector2 v)
                return v;
        }

        // Fallback from pixel pos
        return character.Position / Game1.tileSize;
    }

    // -------------------------
    // Petting detection + crow patch
    // -------------------------

    private void ApplyHarmonyPatches()
    {
        var petCheckAction = AccessTools.Method(typeof(Pet), nameof(Pet.checkAction));
        if (petCheckAction is not null)
            this.harmony!.Patch(petCheckAction, postfix: new HarmonyMethod(typeof(ModEntry), nameof(Pet_CheckAction_Postfix)));
        else
            this.Monitor.Log("Couldn't find Pet.checkAction; petting detection may not work.", LogLevel.Warn);

        var addCrows = AccessTools.Method(typeof(Farm), "addCrows");
        if (addCrows is not null)
            this.harmony!.Patch(addCrows, prefix: new HarmonyMethod(typeof(ModEntry), nameof(Farm_AddCrows_Prefix)));
        else
            this.Monitor.Log("Couldn't find Farm.addCrows; crow protection will only show a message.", LogLevel.Warn);
    }

    public static void Pet_CheckAction_Postfix(Pet __instance, Farmer who, ref bool __result)
    {
        if (!__result || Instance is null)
            return;

        if (!Context.IsWorldReady || Game1.player is null)
            return;

        // only track once/day
        if (Instance.State.PettedToday)
            return;

        // Host-only detection?
        if (Instance.Config.HostOnlyPettingDetection)
        {
            if (!Context.IsMainPlayer)
                return;

            Instance.MarkPettedToday();
            return;
        }

        // Farmhand -> host message
        if (!Context.IsMainPlayer)
        {
            Instance.Helper.Multiplayer.SendMessage(
                message: true,
                messageType: MpMessageType,
                modIDs: new[] { Instance.ModManifest.UniqueID },
                playerIDs: new[] { Game1.MasterPlayer.UniqueMultiplayerID }
            );
            return;
        }

        Instance.MarkPettedToday();
    }

    public static bool Farm_AddCrows_Prefix()
    {
        if (Instance is null || !Context.IsWorldReady)
            return true;

        if (!Instance.State.CrowProtectionTonight)
            return true;

        // consume and skip exactly once
        Instance.State.CrowProtectionTonight = false;
        Instance.InstanceWriteSave();
        return false;
    }

    private void MarkPettedToday()
    {
        this.State.PettedToday = true;
        this.WriteSaveData();
    }

    private void InstanceWriteSave() => this.WriteSaveData();

    private void WriteSaveData()
    {
        if (Context.IsWorldReady)
            this.Helper.Data.WriteSaveData(SaveDataKey, this.State);
    }

    // -------------------------
    // Utility
    // -------------------------

    private static string GetSeasonId(GameLocation location)
    {
        // FIX: GetSeasonForLocation returns a Season type → ToString() gives "spring"/"winter"/etc.
        string season = Game1.GetSeasonForLocation(location).ToString();
        if (string.IsNullOrWhiteSpace(season))
            season = Game1.currentSeason ?? "spring";
        return season;
    }

    private static string QO(int objectId) => $"(O){objectId}";

    private string T(string key) => this.Helper.Translation.Get(key);

    private void ShowHud(string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
            Game1.addHUDMessage(new HUDMessage(text));
    }

    private sealed record ResourceEntry(string ItemId, int MinStack, int MaxStack, bool SkipInWinter);

    internal sealed class SaveData
    {
        public bool PettedToday { get; set; }
        public bool PettedYesterday { get; set; }
        public bool CrowProtectionTonight { get; set; }
    }

    // Minimal GMCM interface subset
    
}
