using System;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using HarmonyLib;

namespace FasterMenuLoad
{
    public class ModEntry : Mod
    {
        public static ModConfig Config = new();
        public static IMonitor ModMonitor = null!;
        public static ITranslationHelper I18n = null!;

        // Compatibility flags
        public static bool IsFullyDisabled { get; private set; }
        public static bool IsCraftingDisabled { get; private set; }

        public override void Entry(IModHelper helper)
        {
            ModMonitor = Monitor;
            I18n = helper.Translation;
            Config = helper.ReadConfig<ModConfig>();

            var harmony = new Harmony(this.ModManifest.UniqueID);
            MenuPatches.Apply(harmony);

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            // Check for UI Info Suite 2 - disables entire mod
            if (Helper.ModRegistry.IsLoaded("Annosz.UiInfoSuite2"))
            {
                var modInfo = Helper.ModRegistry.Get("Annosz.UiInfoSuite2");
                IsFullyDisabled = true;
                Monitor.Log(
                    $"Detected '{modInfo?.Manifest.Name}'. Faster Menu Load is fully disabled for compatibility. " +
                    "UI Info Suite 2 requires real menu pages to function properly.",
                    LogLevel.Warn
                );
            }

            // Check for Better Crafting - disables crafting page only
            if (Helper.ModRegistry.IsLoaded("leclair.bettercrafting") ||
                Helper.ModRegistry.IsLoaded("spacechase0.BetterCrafting"))
            {
                var modInfo = Helper.ModRegistry.Get("leclair.bettercrafting") ??
                             Helper.ModRegistry.Get("spacechase0.BetterCrafting");
                IsCraftingDisabled = true;
                Monitor.Log(
                    $"Detected '{modInfo?.Manifest.Name}'. Lazy loading disabled for Crafting page only. " +
                    "Other pages will still use lazy loading.",
                    LogLevel.Info
                );
            }

            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null) return;

            configMenu.Register(this.ModManifest, () => Config = new ModConfig(), () => this.Helper.WriteConfig(Config));

            // Add compatibility warning section if needed
            if (IsFullyDisabled)
            {
                configMenu.AddSectionTitle(
                    this.ModManifest,
                    () => "⚠️ MOD FULLY DISABLED",
                    () => "UI Info Suite 2 detected. All lazy loading features are disabled to prevent conflicts. The settings below will have no effect."
                );
            }
            else if (IsCraftingDisabled)
            {
                configMenu.AddSectionTitle(
                    this.ModManifest,
                    () => "ℹ️ Partial Compatibility Mode",
                    () => "Better Crafting detected. Crafting page lazy loading is disabled, but other pages will still benefit from lazy loading."
                );
            }

            configMenu.AddSectionTitle(
                this.ModManifest,
                () => I18n.Get("config.section.performance"),
                () => I18n.Get("config.section.performance.desc")
            );

            configMenu.AddBoolOption(
                this.ModManifest,
                () => Config.LazyLoadSkills,
                v => Config.LazyLoadSkills = v,
                () => I18n.Get("config.lazy_skills.name"),
                () => IsFullyDisabled ? "Disabled due to UI Info Suite 2" : I18n.Get("config.lazy_skills.desc")
            );

            configMenu.AddBoolOption(
                this.ModManifest,
                () => Config.LazyLoadSocial,
                v => Config.LazyLoadSocial = v,
                () => I18n.Get("config.lazy_social.name"),
                () => IsFullyDisabled ? "Disabled due to UI Info Suite 2" : I18n.Get("config.lazy_social.desc")
            );

            configMenu.AddBoolOption(
                this.ModManifest,
                () => Config.LazyLoadCrafting,
                v => Config.LazyLoadCrafting = v,
                () => I18n.Get("config.lazy_crafting.name"),
                () => IsFullyDisabled ? "Disabled due to UI Info Suite 2" :
                      IsCraftingDisabled ? "Disabled due to Better Crafting" :
                      I18n.Get("config.lazy_crafting.desc")
            );

            configMenu.AddBoolOption(
                this.ModManifest,
                () => Config.LazyLoadAnimals,
                v => Config.LazyLoadAnimals = v,
                () => I18n.Get("config.lazy_animals.name"),
                () => IsFullyDisabled ? "Disabled due to UI Info Suite 2" : I18n.Get("config.lazy_animals.desc")
            );

            configMenu.AddBoolOption(
                this.ModManifest,
                () => Config.LazyLoadPowers,
                v => Config.LazyLoadPowers = v,
                () => I18n.Get("config.lazy_powers.name"),
                () => IsFullyDisabled ? "Disabled due to UI Info Suite 2" : I18n.Get("config.lazy_powers.desc")
            );

            configMenu.AddBoolOption(
                this.ModManifest,
                () => Config.LazyLoadCollections,
                v => Config.LazyLoadCollections = v,
                () => I18n.Get("config.lazy_collections.name"),
                () => IsFullyDisabled ? "Disabled due to UI Info Suite 2" : I18n.Get("config.lazy_collections.desc")
            );

            // Add Debug section
            configMenu.AddSectionTitle(
                this.ModManifest,
                () => I18n.Get("config.section.debug"),
                () => I18n.Get("config.section.debug.desc")
            );

            configMenu.AddBoolOption(
                this.ModManifest,
                () => Config.EnableDebugLogging,
                v => Config.EnableDebugLogging = v,
                () => I18n.Get("config.debug_logging.name"),
                () => I18n.Get("config.debug_logging.desc")
            );
        }
    }
}