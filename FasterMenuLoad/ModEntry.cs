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
        public static ITranslationHelper I18n = null!; // ADD THIS

        public override void Entry(IModHelper helper)
        {
            ModMonitor = Monitor;
            I18n = helper.Translation; // ADD THIS - Hook up i18n
            Config = helper.ReadConfig<ModConfig>();

            var harmony = new Harmony(this.ModManifest.UniqueID);
            MenuPatches.Apply(harmony);

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null) return;

            configMenu.Register(this.ModManifest, () => Config = new ModConfig(), () => this.Helper.WriteConfig(Config));

            // FIXED: Use i18n translations instead of hardcoded strings
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
                () => I18n.Get("config.lazy_skills.desc")
            );

            configMenu.AddBoolOption(
                this.ModManifest,
                () => Config.LazyLoadSocial,
                v => Config.LazyLoadSocial = v,
                () => I18n.Get("config.lazy_social.name"),
                () => I18n.Get("config.lazy_social.desc")
            );

            configMenu.AddBoolOption(
                this.ModManifest,
                () => Config.LazyLoadCrafting,
                v => Config.LazyLoadCrafting = v,
                () => I18n.Get("config.lazy_crafting.name"),
                () => I18n.Get("config.lazy_crafting.desc")
            );

            configMenu.AddBoolOption(
                this.ModManifest,
                () => Config.LazyLoadAnimals,
                v => Config.LazyLoadAnimals = v,
                () => I18n.Get("config.lazy_animals.name"),
                () => I18n.Get("config.lazy_animals.desc")
            );

            configMenu.AddBoolOption(
                this.ModManifest,
                () => Config.LazyLoadPowers,
                v => Config.LazyLoadPowers = v,
                () => I18n.Get("config.lazy_powers.name"),
                () => I18n.Get("config.lazy_powers.desc")
            );

            configMenu.AddBoolOption(
                this.ModManifest,
                () => Config.LazyLoadCollections,
                v => Config.LazyLoadCollections = v,
                () => I18n.Get("config.lazy_collections.name"),
                () => I18n.Get("config.lazy_collections.desc")
            );
        }
    }
}