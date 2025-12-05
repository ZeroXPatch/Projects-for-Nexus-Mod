using System;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;

namespace SafeLightning
{
    public sealed class ModEntry : Mod
    {
        internal static ModEntry Instance { get; private set; } = null!;

        internal ModConfig Config { get; private set; } = null!;

        private Harmony? harmony;

        public override void Entry(IModHelper helper)
        {
            Instance = this;

            // load config
            this.Config = this.Helper.ReadConfig<ModConfig>();

            // patch lightning on the Farm
            this.harmony = new Harmony(this.ModManifest.UniqueID);

            var original = AccessTools.Method(typeof(Farm), "lightningStrike");
            if (original is null)
            {
                this.Monitor.Log("Failed to find Farm.lightningStrike to patch. Safe Lightning will be disabled.", LogLevel.Error);
            }
            else
            {
                this.harmony.Patch(
                    original: original,
                    prefix: new HarmonyMethod(typeof(ModEntry), nameof(BeforeLightningStrike))
                );

                this.Monitor.Log(this.Helper.Translation.Get("log.patched"), LogLevel.Info);
            }

            // GMCM hookup
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        }

        /// <summary>Register config with Generic Mod Config Menu, if installed.</summary>
        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm is null)
                return;

            gmcm.Register(
                this.ModManifest,
                reset: () => this.Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.Config),
                titleScreenOnly: false
            );

            gmcm.AddSectionTitle(
                this.ModManifest,
                text: () => this.Helper.Translation.Get("config.section.general")
            );

            gmcm.AddBoolOption(
                this.ModManifest,
                getValue: () => this.Config.OnlyStrikeLightningRods,
                setValue: value => this.Config.OnlyStrikeLightningRods = value,
                name: () => this.Helper.Translation.Get("config.onlyStrikeLightningRods.name"),
                tooltip: () => this.Helper.Translation.Get("config.onlyStrikeLightningRods.tooltip"),
                fieldId: "OnlyStrikeLightningRods"
            );
        }

        /// <summary>
        /// Harmony prefix for Farm.lightningStrike.
        /// Return true to let vanilla run, false to skip vanilla.
        /// </summary>
        private static bool BeforeLightningStrike(Farm __instance, Vector2 tileLocation)
        {
            // sanity checks
            if (!Context.IsWorldReady || Game1.player is null)
                return true; // let vanilla run

            // if feature is disabled, let vanilla lightning behavior happen
            if (!Instance.Config.OnlyStrikeLightningRods)
                return true;

            // allow lightning if it's striking a lightning rod (big craftable index 9)
            if (__instance.objects.TryGetValue(tileLocation, out var obj)
                && obj.bigCraftable.Value
                && obj.ParentSheetIndex == 9)
            {
                return true; // let vanilla lightningStrike run (rod + battery)
            }

            // otherwise, block the lightning strike
            return false;
        }
    }

    /// <summary>Mod configuration.</summary>
    public sealed class ModConfig
    {
        /// <summary>
        /// If true, lightning can only strike lightning rods; all other targets are blocked.
        /// </summary>
        public bool OnlyStrikeLightningRods { get; set; } = true;
    }

    /// <summary>
    /// Minimal Generic Mod Config Menu API interface with nullable-friendly signatures.
    /// </summary>
    public interface IGenericModConfigMenuApi
    {
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);

        void AddSectionTitle(
            IManifest mod,
            Func<string> text,
            Func<string>? tooltip = null
        );

        void AddBoolOption(
            IManifest mod,
            Func<bool> getValue,
            Action<bool> setValue,
            Func<string> name,
            Func<string>? tooltip = null,
            string? fieldId = null
        );

        // Included in case you want it later; safe with nullable refs:
        void AddNumberOption(
            IManifest mod,
            Func<int> getValue,
            Action<int> setValue,
            Func<string> name,
            Func<string>? tooltip = null,
            int? min = null,
            int? max = null,
            int? interval = null,
            Func<int, string>? formatValue = null,
            string? fieldId = null
        );
    }
}
