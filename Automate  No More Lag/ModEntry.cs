using System;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace ZeroXPatch
{
    /// <summary>The mod entry point.</summary>
    public class ModEntry : Mod
    {
        internal static IMonitor ModMonitor;
        internal static IModHelper ModHelper;
        internal static ModConfig Config;

        private Harmony harmony;

        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            ModMonitor = Monitor;
            ModHelper = helper;
            Config = helper.ReadConfig<ModConfig>();

            // Apply Harmony patches
            harmony = new Harmony(ModManifest.UniqueID);

            try
            {
                // Only patch if Automate is loaded
                if (helper.ModRegistry.IsLoaded("Pathoschild.Automate"))
                {
                    AutomatePatches.Apply(harmony);
                    Monitor.Log(helper.Translation.Get("log.patched-successfully"), LogLevel.Info);
                    Monitor.Log(helper.Translation.Get("log.idle-mode", new { enabled = Config.OnlyProcessWhenIdle ? helper.Translation.Get("common.enabled") : helper.Translation.Get("common.disabled") }), LogLevel.Info);

                    if (Config.OnlyProcessWhenIdle)
                    {
                        Monitor.Log(helper.Translation.Get("log.idle-threshold", new { ticks = Config.IdleTicksThreshold, seconds = Config.IdleTicksThreshold / 60.0 }), LogLevel.Info);
                        Monitor.Log(helper.Translation.Get("log.pause-cutscenes", new { enabled = Config.PauseTimerDuringCutscenes ? helper.Translation.Get("common.enabled") : helper.Translation.Get("common.disabled") }), LogLevel.Info);
                        Monitor.Log(helper.Translation.Get("log.pause-input", new { enabled = Config.PauseTimerOnInput ? helper.Translation.Get("common.enabled") : helper.Translation.Get("common.disabled") }), LogLevel.Info);
                        Monitor.Log(helper.Translation.Get("log.idle-warning"), LogLevel.Warn);
                    }

                    Monitor.Log(helper.Translation.Get("log.debug-mode", new { enabled = Config.DebugMode ? helper.Translation.Get("common.enabled") : helper.Translation.Get("common.disabled") }), LogLevel.Info);
                }
                else
                {
                    Monitor.Log(helper.Translation.Get("log.automate-not-found"), LogLevel.Warn);
                }
            }
            catch (Exception ex)
            {
                Monitor.Log($"Error applying Harmony patches: {ex}", LogLevel.Error);
            }

            // Register events
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        }

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            // Register Generic Mod Config Menu
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu != null)
            {
                RegisterConfigMenu(configMenu);
            }
        }

        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            AutomatePatches.ResetStatistics();
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            // Update idle detection every tick
            AutomatePatches.UpdateIdleDetection();

            // Log statistics every 60 seconds
            if (e.IsMultipleOf(3600)) // 60 seconds
            {
                AutomatePatches.LogStatistics();
            }
        }

        private void RegisterConfigMenu(IGenericModConfigMenuApi configMenu)
        {
            configMenu.Register(
                mod: ModManifest,
                reset: () => Config = new ModConfig(),
                save: () => Helper.WriteConfig(Config)
            );

            configMenu.AddSectionTitle(
                mod: ModManifest,
                text: () => Helper.Translation.Get("config.section.performance")
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.idle.name"),
                tooltip: () => Helper.Translation.Get("config.idle.tooltip"),
                getValue: () => Config.OnlyProcessWhenIdle,
                setValue: value => Config.OnlyProcessWhenIdle = value
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.idle-delay.name"),
                tooltip: () => Helper.Translation.Get("config.idle-delay.tooltip"),
                getValue: () => Config.IdleTicksThreshold / 60,
                setValue: value => Config.IdleTicksThreshold = value * 60,
                min: 0,
                max: 10,
                interval: 1,
                formatValue: value => Helper.Translation.Get("config.idle-delay.format", new { seconds = value })
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.pause-cutscenes.name"),
                tooltip: () => Helper.Translation.Get("config.pause-cutscenes.tooltip"),
                getValue: () => Config.PauseTimerDuringCutscenes,
                setValue: value => Config.PauseTimerDuringCutscenes = value
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.pause-input.name"),
                tooltip: () => Helper.Translation.Get("config.pause-input.tooltip"),
                getValue: () => Config.PauseTimerOnInput,
                setValue: value => Config.PauseTimerOnInput = value
            );

            configMenu.AddSectionTitle(
                mod: ModManifest,
                text: () => Helper.Translation.Get("config.section.debugging")
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.debug.name"),
                tooltip: () => Helper.Translation.Get("config.debug.tooltip"),
                getValue: () => Config.DebugMode,
                setValue: value => Config.DebugMode = value
            );
        }
    }
}