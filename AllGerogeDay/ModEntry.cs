using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace OopsAllGeorge;

public sealed class ModEntry : Mod
{
    private ModConfig Config = new();
    private readonly HashSet<string> Exclude = new(StringComparer.OrdinalIgnoreCase);

    private bool IsGeorgeDay;

    private const string GeorgeCharacterAsset = "Characters/George";
    private const string GeorgePortraitAsset = "Portraits/George";
    private const string MpMessageType = "GeorgeDayState";

    public override void Entry(IModHelper helper)
    {
        Config = helper.ReadConfig<ModConfig>();
        RebuildExcludeSet();

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;

        helper.Events.Multiplayer.ModMessageReceived += OnModMessageReceived;
        helper.Events.Content.AssetRequested += OnAssetRequested;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        SetupGmcm();
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        IsGeorgeDay = false;

        if (Context.IsMainPlayer)
            SyncGeorgeDayToFarmhands();
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        RebuildExcludeSet();

        if (!Config.Enabled)
        {
            SetGeorgeDay(false);
            if (Context.IsMainPlayer)
                SyncGeorgeDayToFarmhands();
            return;
        }

        if (Context.IsMainPlayer)
        {
            bool triggered = RollGeorgeDay();
            SetGeorgeDay(triggered);
            SyncGeorgeDayToFarmhands();

            if (triggered && Config.ShowStartMessage)
                Game1.addHUDMessage(new HUDMessage(Config.StartMessage, 2));
        }
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        IsGeorgeDay = false;
    }

    private void OnModMessageReceived(object? sender, ModMessageReceivedEventArgs e)
    {
        if (e.FromModID != ModManifest.UniqueID || e.Type != MpMessageType)
            return;

        try
        {
            bool state = e.ReadAs<bool>();
            SetGeorgeDay(state);

            if (!Context.IsMainPlayer && state && Config.ShowStartMessage)
                Game1.addHUDMessage(new HUDMessage(Config.StartMessage, 2));
        }
        catch (Exception ex)
        {
            Monitor.Log($"Failed reading MP message '{MpMessageType}': {ex}", LogLevel.Warn);
        }
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (!Config.Enabled || !IsGeorgeDay)
            return;

        // ONLY replace actual sprite textures: Characters/<NPC> or Characters/<NPC>_Beach
        if (Config.ReplaceCharacterSprites && IsNpcCharacterSpriteAsset(e.Name, out string npcName, out bool isBeachVariant))
        {
            if (!Exclude.Contains(npcName))
            {
                e.LoadFrom(
                    () => Helper.GameContent.Load<Texture2D>(GeorgeCharacterAsset),
                    AssetLoadPriority.Exclusive
                );
            }
            else if (npcName.Equals("George", StringComparison.OrdinalIgnoreCase) && isBeachVariant)
            {
                e.LoadFrom(
                    () => Helper.GameContent.Load<Texture2D>(GeorgeCharacterAsset),
                    AssetLoadPriority.Exclusive
                );
            }

            return;
        }

        // ONLY replace actual portrait textures: Portraits/<NPC>
        if (Config.ReplacePortraits && IsNpcPortraitAsset(e.Name, out npcName))
        {
            if (!Exclude.Contains(npcName))
            {
                e.LoadFrom(
                    () => Helper.GameContent.Load<Texture2D>(GeorgePortraitAsset),
                    AssetLoadPriority.Exclusive
                );
            }

            return;
        }
    }

    private bool RollGeorgeDay()
    {
        float chance = Clamp(Config.ChancePercent, 0f, 100f);

        int seed = (int)(Game1.uniqueIDForThisGame % int.MaxValue);
        seed = unchecked(seed * 31 + Game1.Date.TotalDays);
        seed = unchecked(seed * 31 + (int)(Game1.stats.DaysPlayed % int.MaxValue));

        Random rng = new(seed);
        double roll = rng.NextDouble() * 100.0;

        return roll < chance;
    }

    private void SetGeorgeDay(bool value)
    {
        if (IsGeorgeDay == value)
            return;

        IsGeorgeDay = value;

        // Invalidate only sprite/portrait roots (IMPORTANT: do NOT touch Characters/Dialogue/* etc)
        Helper.GameContent.InvalidateCache(asset =>
            (Config.ReplaceCharacterSprites && IsNpcCharacterSpritePath(asset.Name)) ||
            (Config.ReplacePortraits && IsNpcPortraitPath(asset.Name))
        );
    }

    private void SyncGeorgeDayToFarmhands()
    {
        if (!Context.IsMainPlayer)
            return;

        Helper.Multiplayer.SendMessage(
            message: IsGeorgeDay,
            messageType: MpMessageType,
            modIDs: new[] { ModManifest.UniqueID }
        );
    }

    private void RebuildExcludeSet()
    {
        Exclude.Clear();
        if (Config.ExcludeNpcNames is null)
            return;

        foreach (string name in Config.ExcludeNpcNames)
        {
            if (!string.IsNullOrWhiteSpace(name))
                Exclude.Add(name.Trim());
        }
    }

    // ---------------------------
    // Asset filtering (prevents "Characters/Dialogue/*" crashes)
    // ---------------------------

    // Characters/<NPC> or Characters/<NPC>_Beach only (no subfolders)
    private bool IsNpcCharacterSpriteAsset(IAssetName assetName, out string npcName, out bool isBeachVariant)
    {
        npcName = "";
        isBeachVariant = false;

        string raw = assetName.Name; // e.g. "Characters/Abigail" or "Characters/Dialogue/Leah"
        if (!raw.StartsWith("Characters/", StringComparison.OrdinalIgnoreCase))
            return false;

        string tail = raw["Characters/".Length..]; // e.g. "Abigail" or "Dialogue/Leah"
        if (string.IsNullOrWhiteSpace(tail))
            return false;

        // ignore subfolders (Dialogue, Schedules, etc)
        if (tail.Contains('/'))
            return false;

        if (Config.ReplaceBeachSpritesToo && tail.EndsWith("_Beach", StringComparison.OrdinalIgnoreCase))
        {
            isBeachVariant = true;
            npcName = tail[..^"_Beach".Length];
            return !string.IsNullOrWhiteSpace(npcName);
        }

        npcName = tail;
        return true;
    }

    // Portraits/<NPC> only (no subfolders)
    private static bool IsNpcPortraitAsset(IAssetName assetName, out string npcName)
    {
        npcName = "";

        string raw = assetName.Name; // e.g. "Portraits/Penny"
        if (!raw.StartsWith("Portraits/", StringComparison.OrdinalIgnoreCase))
            return false;

        string tail = raw["Portraits/".Length..];
        if (string.IsNullOrWhiteSpace(tail))
            return false;

        if (tail.Contains('/'))
            return false;

        npcName = tail;
        return true;
    }

    // For InvalidateCache: we receive an IAssetName
    private static bool IsNpcCharacterSpritePath(IAssetName assetName)
    {
        string raw = assetName.Name;
        if (!raw.StartsWith("Characters/", StringComparison.OrdinalIgnoreCase))
            return false;

        string tail = raw["Characters/".Length..];
        return tail.Length > 0 && !tail.Contains('/');
    }

    private static bool IsNpcPortraitPath(IAssetName assetName)
    {
        string raw = assetName.Name;
        if (!raw.StartsWith("Portraits/", StringComparison.OrdinalIgnoreCase))
            return false;

        string tail = raw["Portraits/".Length..];
        return tail.Length > 0 && !tail.Contains('/');
    }

    // ---------------------------
    // GMCM
    // ---------------------------

    private void SetupGmcm()
    {
        var api = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (api is null)
        {
            Monitor.Log("GMCM not found. Config can still be edited via config.json.", LogLevel.Info);
            return;
        }

        api.Register(
            mod: ModManifest,
            reset: () =>
            {
                Config = new ModConfig();
                Helper.WriteConfig(Config);
                RebuildExcludeSet();
            },
            save: () =>
            {
                Config.ChancePercent = Clamp(Config.ChancePercent, 0f, 100f);
                Helper.WriteConfig(Config);
                RebuildExcludeSet();

                if (!Config.Enabled)
                {
                    SetGeorgeDay(false);
                    if (Context.IsMainPlayer)
                        SyncGeorgeDayToFarmhands();
                }
            }
        );

        api.AddSectionTitle(ModManifest,
            text: () => "Oops All George",
            tooltip: () => "Random event settings + visual replacement options."
        );

        api.AddBoolOption(ModManifest,
            getValue: () => Config.Enabled,
            setValue: v => Config.Enabled = v,
            name: () => "Enable mod",
            tooltip: () => "If disabled, George Day never triggers."
        );

        api.AddNumberOption(ModManifest,
            getValue: () => Config.ChancePercent,
            setValue: v => Config.ChancePercent = v,
            name: () => "Daily trigger chance (%)",
            tooltip: () => "Chance each day that everyone becomes George.",
            min: 0f,
            max: 100f,
            interval: 0.5f
        );

        api.AddBoolOption(ModManifest,
            getValue: () => Config.ShowStartMessage,
            setValue: v => Config.ShowStartMessage = v,
            name: () => "Show start message",
            tooltip: () => "Display a HUD message when George Day begins."
        );

        api.AddTextOption(ModManifest,
            getValue: () => Config.StartMessage,
            setValue: v => Config.StartMessage = v,
            name: () => "Start message text",
            tooltip: () => "Text shown when the event triggers."
        );

        api.AddSectionTitle(ModManifest, text: () => "Replacement Options");

        api.AddBoolOption(ModManifest,
            getValue: () => Config.ReplaceCharacterSprites,
            setValue: v => Config.ReplaceCharacterSprites = v,
            name: () => "Replace character sprites",
            tooltip: () => "NPC walking sprites become George."
        );

        api.AddBoolOption(ModManifest,
            getValue: () => Config.ReplacePortraits,
            setValue: v => Config.ReplacePortraits = v,
            name: () => "Replace portraits",
            tooltip: () => "NPC dialogue portraits become George."
        );

        api.AddBoolOption(ModManifest,
            getValue: () => Config.ReplaceBeachSpritesToo,
            setValue: v => Config.ReplaceBeachSpritesToo = v,
            name: () => "Replace beach variants too",
            tooltip: () => "Also replaces Characters/<NPC>_Beach sprites when requested."
        );

        api.AddParagraph(ModManifest, () =>
            "Exclude list is edited in config.json (ExcludeNpcNames). Add NPC internal names there (case-insensitive). Default excludes George."
        );
    }

    private static float Clamp(float value, float min, float max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}
