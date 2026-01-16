using System;
using StardewModdingAPI;

namespace CustomNightLights
{
    public static class ModGMCM
    {
        public static void Setup(IModHelper helper, IManifest manifest, ModConfig config, Action saveConfig)
        {
            var configMenu = helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            configMenu.Register(
                mod: manifest,
                reset: () =>
                {
                    var defaults = new ModConfig();

                    // --- Outdoor Defaults ---
                    config.EnableOutdoor = defaults.EnableOutdoor;
                    config.OutdoorNightOnly = defaults.OutdoorNightOnly;
                    config.OutdoorRed = defaults.OutdoorRed;
                    config.OutdoorGreen = defaults.OutdoorGreen;
                    config.OutdoorBlue = defaults.OutdoorBlue;
                    config.OutdoorIntensity = defaults.OutdoorIntensity;
                    config.OutdoorRadius = defaults.OutdoorRadius;

                    // --- Indoor Defaults ---
                    config.EnableIndoor = defaults.EnableIndoor;
                    config.IndoorNightOnly = defaults.IndoorNightOnly;
                    config.IndoorFarmHouseOnly = defaults.IndoorFarmHouseOnly;
                    config.IndoorExcludedLocations = defaults.IndoorExcludedLocations;
                    config.IndoorRed = defaults.IndoorRed;
                    config.IndoorGreen = defaults.IndoorGreen;
                    config.IndoorBlue = defaults.IndoorBlue;
                    config.IndoorIntensity = defaults.IndoorIntensity;
                    config.IndoorRadius = defaults.IndoorRadius;
                },
                save: saveConfig
            );

            // ===============================================
            // OUTDOOR SECTION
            // ===============================================
            configMenu.AddSectionTitle(mod: manifest, text: () => helper.Translation.Get("config.outdoor.section"));

            configMenu.AddBoolOption(
                mod: manifest,
                getValue: () => config.EnableOutdoor,
                setValue: v => config.EnableOutdoor = v,
                name: () => helper.Translation.Get("config.outdoor.enable"),
                tooltip: () => helper.Translation.Get("config.outdoor.enable.desc")
            );

            configMenu.AddBoolOption(
                mod: manifest,
                getValue: () => config.OutdoorNightOnly,
                setValue: v => config.OutdoorNightOnly = v,
                name: () => helper.Translation.Get("config.nightonly"),
                tooltip: () => helper.Translation.Get("config.nightonly.desc")
            );

            configMenu.AddNumberOption(
                mod: manifest,
                getValue: () => config.OutdoorRadius,
                setValue: v => config.OutdoorRadius = v,
                name: () => helper.Translation.Get("config.radius"),
                min: 0.1f, max: 10.0f, interval: 0.1f
            );

            configMenu.AddNumberOption(
                mod: manifest,
                getValue: () => config.OutdoorIntensity,
                setValue: v => config.OutdoorIntensity = v,
                name: () => helper.Translation.Get("config.intensity"),
                min: 0.0f, max: 2.0f, interval: 0.1f
            );

            configMenu.AddNumberOption(mod: manifest, getValue: () => config.OutdoorRed, setValue: v => config.OutdoorRed = v, name: () => helper.Translation.Get("config.red"), min: 0, max: 255);
            configMenu.AddNumberOption(mod: manifest, getValue: () => config.OutdoorGreen, setValue: v => config.OutdoorGreen = v, name: () => helper.Translation.Get("config.green"), min: 0, max: 255);
            configMenu.AddNumberOption(mod: manifest, getValue: () => config.OutdoorBlue, setValue: v => config.OutdoorBlue = v, name: () => helper.Translation.Get("config.blue"), min: 0, max: 255);

            // ===============================================
            // INDOOR SECTION
            // ===============================================
            configMenu.AddSectionTitle(mod: manifest, text: () => helper.Translation.Get("config.indoor.section"));

            configMenu.AddBoolOption(
                mod: manifest,
                getValue: () => config.EnableIndoor,
                setValue: v => config.EnableIndoor = v,
                name: () => helper.Translation.Get("config.indoor.enable"),
                tooltip: () => helper.Translation.Get("config.indoor.enable.desc")
            );

            configMenu.AddBoolOption(
                mod: manifest,
                getValue: () => config.IndoorNightOnly,
                setValue: v => config.IndoorNightOnly = v,
                name: () => helper.Translation.Get("config.nightonly"),
                tooltip: () => helper.Translation.Get("config.nightonly.desc")
            );

            configMenu.AddBoolOption(
                mod: manifest,
                getValue: () => config.IndoorFarmHouseOnly,
                setValue: v => config.IndoorFarmHouseOnly = v,
                name: () => helper.Translation.Get("config.indoor.farmhouseonly"),
                tooltip: () => helper.Translation.Get("config.indoor.farmhouseonly.desc")
            );

            // Text Option for Excluded Locations
            configMenu.AddTextOption(
                mod: manifest,
                getValue: () => config.IndoorExcludedLocations,
                setValue: v => config.IndoorExcludedLocations = v,
                name: () => helper.Translation.Get("config.indoor.excluded"),
                tooltip: () => helper.Translation.Get("config.indoor.excluded.desc")
            );

            configMenu.AddNumberOption(
                mod: manifest,
                getValue: () => config.IndoorRadius,
                setValue: v => config.IndoorRadius = v,
                name: () => helper.Translation.Get("config.radius"),
                min: 0.1f, max: 10.0f, interval: 0.1f
            );

            configMenu.AddNumberOption(
                mod: manifest,
                getValue: () => config.IndoorIntensity,
                setValue: v => config.IndoorIntensity = v,
                name: () => helper.Translation.Get("config.intensity"),
                min: 0.0f, max: 2.0f, interval: 0.1f
            );

            configMenu.AddNumberOption(mod: manifest, getValue: () => config.IndoorRed, setValue: v => config.IndoorRed = v, name: () => helper.Translation.Get("config.red"), min: 0, max: 255);
            configMenu.AddNumberOption(mod: manifest, getValue: () => config.IndoorGreen, setValue: v => config.IndoorGreen = v, name: () => helper.Translation.Get("config.green"), min: 0, max: 255);
            configMenu.AddNumberOption(mod: manifest, getValue: () => config.IndoorBlue, setValue: v => config.IndoorBlue = v, name: () => helper.Translation.Get("config.blue"), min: 0, max: 255);
        }
    }
}