using System;
using System.Collections.Generic;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;

namespace ShadowsOfTheDeep
{
    public class ModEntry : Mod
    {
        internal static ModConfig Config = null!;
        internal static IMonitor ModMonitor = null!;

        // PerScreen ensures split-screen players have their own fish logic
        internal static readonly PerScreen<ShadowManager> ShadowManagers = new();

        public override void Entry(IModHelper helper)
        {
            ModMonitor = Monitor;
            Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.Player.Warped += OnWarped;

            var harmony = new Harmony(ModManifest.UniqueID);
            harmony.Patch(
                original: AccessTools.Method(typeof(GameLocation), "drawWater", new[] { typeof(Microsoft.Xna.Framework.Graphics.SpriteBatch) }),
                prefix: new HarmonyMethod(typeof(WaterPatches), nameof(WaterPatches.DrawWater_Prefix))
            );
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null) return;

            configMenu.Register(ModManifest, () => Config = new ModConfig(), () => Helper.WriteConfig(Config));

            // --- VISUALS SECTION ---
            configMenu.AddSectionTitle(ModManifest, () => Helper.Translation.Get("config.section.visuals"));

            configMenu.AddNumberOption(ModManifest, () => Config.ShadowOpacity, val => Config.ShadowOpacity = val,
                name: () => Helper.Translation.Get("config.opacity.name"),
                tooltip: () => Helper.Translation.Get("config.opacity.tooltip"), min: 0.1f, max: 1.0f);

            configMenu.AddNumberOption(ModManifest, () => Config.ShadowScale, val => Config.ShadowScale = val,
                name: () => Helper.Translation.Get("config.scale.name"),
                tooltip: () => Helper.Translation.Get("config.scale.tooltip"), min: 0.5f, max: 2.0f);

            configMenu.AddBoolOption(ModManifest, () => Config.EnableFadeEffects, val => Config.EnableFadeEffects = val,
                name: () => Helper.Translation.Get("config.fade.name"),
                tooltip: () => Helper.Translation.Get("config.fade.tooltip"));

            // --- POPULATION SECTION ---
            configMenu.AddSectionTitle(ModManifest, () => Helper.Translation.Get("config.section.population"));

            configMenu.AddNumberOption(ModManifest, () => Config.MaxFishCount, val => Config.MaxFishCount = val,
                name: () => Helper.Translation.Get("config.max-fish.name"),
                tooltip: () => Helper.Translation.Get("config.max-fish.tooltip"), min: 5, max: 500);

            configMenu.AddNumberOption(ModManifest, () => Config.SpawnChance, val => Config.SpawnChance = val,
                name: () => Helper.Translation.Get("config.density.name"),
                tooltip: () => Helper.Translation.Get("config.density.tooltip"), min: 0.01f, max: 1.0f);

            // --- LOCATIONS & TIME SECTION ---
            configMenu.AddSectionTitle(ModManifest, () => Helper.Translation.Get("config.section.locations"));

            configMenu.AddBoolOption(ModManifest, () => Config.FarmOnly, val => Config.FarmOnly = val,
                name: () => Helper.Translation.Get("config.farm-only.name"),
                tooltip: () => Helper.Translation.Get("config.farm-only.tooltip"));

            configMenu.AddBoolOption(ModManifest, () => Config.HideFishAtNight, val => Config.HideFishAtNight = val,
                name: () => Helper.Translation.Get("config.hide-night.name"),
                tooltip: () => Helper.Translation.Get("config.hide-night.tooltip"));

            configMenu.AddNumberOption(ModManifest, () => Config.HoursAfterSunset, val => Config.HoursAfterSunset = val,
                name: () => Helper.Translation.Get("config.sunset-offset.name"),
                tooltip: () => Helper.Translation.Get("config.sunset-offset.tooltip"), min: 0, max: 6);

            configMenu.AddTextOption(ModManifest,
                () => string.Join(",", Config.ExcludedLocations),
                val => Config.ExcludedLocations = new List<string>(val.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)),
                name: () => Helper.Translation.Get("config.excluded.name"),
                tooltip: () => Helper.Translation.Get("config.excluded.tooltip"));
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            ShadowManagers.Value = new ShadowManager(Helper);
        }

        private void OnWarped(object? sender, WarpedEventArgs e)
        {
            if (e.IsLocalPlayer)
            {
                if (ShadowManagers.Value == null)
                    ShadowManagers.Value = new ShadowManager(Helper);

                ShadowManagers.Value.ChangeLocation(e.NewLocation);
            }
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady) return;
            ShadowManagers.Value?.Update(e);
        }
    }
}