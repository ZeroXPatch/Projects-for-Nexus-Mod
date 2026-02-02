using System;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using Microsoft.Xna.Framework; // <-- Added this to fix GameTime error

namespace BackgroundTickThrottler
{
    public class ModEntry : Mod
    {
        public static ModConfig Config = new();
        public static IMonitor SMonitor = null!;

        public override void Entry(IModHelper helper)
        {
            SMonitor = Monitor;
            Config = helper.ReadConfig<ModConfig>();

            // Apply Harmony Patches
            var harmony = new Harmony(ModManifest.UniqueID);
            harmony.Patch(
                original: AccessTools.Method(typeof(NPC), nameof(NPC.update), new[] { typeof(GameTime), typeof(GameLocation) }),
                prefix: new HarmonyMethod(typeof(NPCPatch), nameof(NPCPatch.Prefix))
            );

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;

            // Debug logging on game update (only if debug enabled)
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            // Only log debug info every 60 ticks (once per second) and if debug is enabled
            if (Config.EnableDebug && e.IsMultipleOf(60))
            {
                NPCPatch.LogDebugInfo();
            }
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            // GMCM Integration
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null) return;

            configMenu.Register(
                mod: ModManifest,
                reset: () => Config = new ModConfig(),
                save: () => Helper.WriteConfig(Config)
            );

            // Section Title
            configMenu.AddSectionTitle(
                mod: ModManifest,
                text: () => Helper.Translation.Get("config.title")
            );

            // Enabled Toggle
            configMenu.AddBoolOption(
                mod: ModManifest,
                getValue: () => Config.Enabled,
                setValue: value => Config.Enabled = value,
                name: () => Helper.Translation.Get("config.enabled.name"),
                tooltip: () => Helper.Translation.Get("config.enabled.tooltip")
            );

            // Update Interval Slider
            configMenu.AddNumberOption(
                mod: ModManifest,
                getValue: () => Config.UpdateInterval,
                setValue: value => Config.UpdateInterval = value,
                name: () => Helper.Translation.Get("config.interval.name"),
                tooltip: () => Helper.Translation.Get("config.interval.tooltip"),
                min: 1,
                max: 10
            );

            // Force Villagers Toggle
            configMenu.AddBoolOption(
                mod: ModManifest,
                getValue: () => Config.AlwaysUpdateVillagers,
                setValue: value => Config.AlwaysUpdateVillagers = value,
                name: () => Helper.Translation.Get("config.force-villagers.name"),
                tooltip: () => Helper.Translation.Get("config.force-villagers.tooltip")
            );

            // Debug Toggle
            configMenu.AddBoolOption(
                mod: ModManifest,
                getValue: () => Config.EnableDebug,
                setValue: value => Config.EnableDebug = value,
                name: () => Helper.Translation.Get("config.debug.name"),
                tooltip: () => Helper.Translation.Get("config.debug.tooltip")
            );
        }
    }
}