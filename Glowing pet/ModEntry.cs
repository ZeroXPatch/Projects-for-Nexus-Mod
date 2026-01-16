using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Characters;
using System;

#nullable enable
namespace PetIlluminator;

public class ModEntry : Mod
{
    private ModConfig Config = new ModConfig();
    private readonly string LightId = "PetIlluminator_Glow";
    private LightSource? CurrentLight;

    public override void Entry(IModHelper helper)
    {
        this.Config = helper.ReadConfig<ModConfig>();
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        helper.Events.Player.Warped += OnPlayerWarped;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        var api = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (api == null)
            return;

        api.Register(
            this.ModManifest,
            reset: () => this.Config = new ModConfig(),
            save: () => this.Helper.WriteConfig(this.Config)
        );

        // Enabled
        api.AddBoolOption(
            this.ModManifest,
            getValue: () => this.Config.Enabled,
            setValue: value => this.Config.Enabled = value,
            name: () => this.Helper.Translation.Get("config.enabled.name"),
            tooltip: () => this.Helper.Translation.Get("config.enabled.tooltip")
        );

        // Late Activation (2 Hours after sunset)
        api.AddBoolOption(
            this.ModManifest,
            getValue: () => this.Config.UseLateActivation,
            setValue: value => this.Config.UseLateActivation = value,
            name: () => this.Helper.Translation.Get("config.delay.name"),
            tooltip: () => this.Helper.Translation.Get("config.delay.tooltip")
        );

        // Preset
        api.AddTextOption(
            this.ModManifest,
            getValue: () => this.Config.Preset,
            setValue: value => this.Config.Preset = value,
            name: () => this.Helper.Translation.Get("config.preset.name"),
            tooltip: () => this.Helper.Translation.Get("config.preset.tooltip"),
            allowedValues: new string[] { "Normal", "Moonlight", "Spooky", "Custom" },
            formatAllowedValue: value => this.Helper.Translation.Get("preset." + value)
        );

        // Radius
        api.AddNumberOption(
            this.ModManifest,
            getValue: () => this.Config.LightRadius,
            setValue: value => this.Config.LightRadius = value,
            name: () => this.Helper.Translation.Get("config.radius.name"),
            tooltip: () => this.Helper.Translation.Get("config.radius.tooltip"),
            min: 0.5f,
            max: 4f,
            interval: 0.1f
        );

        // Custom Colors Section
        api.AddSectionTitle(
            this.ModManifest,
            text: () => this.Helper.Translation.Get("config.section.custom")
        );
        api.AddParagraph(
            this.ModManifest,
            text: () => this.Helper.Translation.Get("config.section.custom.text")
        );

        api.AddNumberOption(this.ModManifest, () => this.Config.CustomRed, v => this.Config.CustomRed = v, () => "Red", min: 0, max: 255);
        api.AddNumberOption(this.ModManifest, () => this.Config.CustomGreen, v => this.Config.CustomGreen = v, () => "Green", min: 0, max: 255);
        api.AddNumberOption(this.ModManifest, () => this.Config.CustomBlue, v => this.Config.CustomBlue = v, () => "Blue", min: 0, max: 255);
    }

    private void OnPlayerWarped(object? sender, WarpedEventArgs e)
    {
        this.RemoveLight();
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady)
            return;

        if (!this.Config.Enabled)
        {
            this.RemoveLight();
            return;
        }

        Pet pet = Game1.player.getPet();

        if (pet == null || pet.currentLocation != Game1.currentLocation)
        {
            this.RemoveLight();
            return;
        }

        // Get the time the game thinks it is "dark" (usually 1800/6pm)
        int activationTime = Game1.getStartingToGetDarkTime(Game1.currentLocation);

        // If the 2-hour delay option is enabled, add 120 minutes
        if (this.Config.UseLateActivation)
        {
            activationTime = Utility.ModifyTime(activationTime, 120);
        }

        if (Game1.timeOfDay < activationTime)
        {
            this.RemoveLight();
        }
        else
        {
            this.UpdatePetLight(pet);
        }
    }

    private void UpdatePetLight(Pet pet)
    {
        Vector2 position = new Vector2(pet.Position.X + 32f, pet.Position.Y + 16f);
        Color colorFromPreset = this.GetColorFromPreset();

        if (this.CurrentLight != null && Game1.currentLocation.sharedLights.ContainsKey(this.LightId))
        {
            this.CurrentLight.position.Value = position;
            this.CurrentLight.color.Value = colorFromPreset;
            this.CurrentLight.radius.Value = this.Config.LightRadius;
        }
        else
        {
            this.CurrentLight = new LightSource(this.LightId, 4, position, this.Config.LightRadius, colorFromPreset, LightSource.LightContext.None, 0L);
            Game1.currentLocation.sharedLights[this.LightId] = this.CurrentLight;
        }
    }

    private void RemoveLight()
    {
        if (this.CurrentLight == null || Game1.currentLocation == null)
            return;

        if (Game1.currentLocation.sharedLights.ContainsKey(this.LightId))
        {
            Game1.currentLocation.sharedLights.Remove(this.LightId);
        }
        this.CurrentLight = null;
    }

    private Color GetColorFromPreset()
    {
        switch (this.Config.Preset)
        {
            case "Normal":
                // Standard warm lantern color (Orange/Yellow)
                return new Color(0, 0, 0);

            case "Moonlight":
                // Cool Blue
                return new Color(80, 80, 180);

            case "Spooky":
                // Bright Green
                return new Color(50, 255, 50);

            case "Custom":
                return new Color(this.Config.CustomRed, this.Config.CustomGreen, this.Config.CustomBlue);

            default:
                return new Color(255, 160, 60);
        }
    }
}