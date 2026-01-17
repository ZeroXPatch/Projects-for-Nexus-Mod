using System;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace StaticFishingBar;

public class ModEntry : Mod
{
    internal static ModConfig Config = new();
    internal static IMonitor ModMonitor = null!;

    public override void Entry(IModHelper helper)
    {
        ModMonitor = Monitor;
        Config = helper.ReadConfig<ModConfig>();

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;

        var harmony = new Harmony(ModManifest.UniqueID);

        // We only need a Prefix now. We just want to change the coordinates before it draws.
        // We don't need to mess with the SpriteBatch anymore since we aren't scaling.
        harmony.Patch(
            original: AccessTools.Method(typeof(BobberBar), nameof(BobberBar.draw), new[] { typeof(SpriteBatch) }),
            prefix: new HarmonyMethod(typeof(BobberBarPatch), nameof(BobberBarPatch.Prefix))
        );
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (configMenu is null)
            return;

        configMenu.Register(
            mod: ModManifest,
            reset: () => Config = new ModConfig(),
            save: () => Helper.WriteConfig(Config)
        );

        configMenu.AddSectionTitle(ModManifest,
            () => Helper.Translation.Get("config.section.settings"));

        configMenu.AddBoolOption(
            mod: ModManifest,
            name: () => Helper.Translation.Get("config.enabled.name"),
            tooltip: () => Helper.Translation.Get("config.enabled.tooltip"),
            getValue: () => Config.Enabled,
            setValue: value => Config.Enabled = value
        );

        configMenu.AddNumberOption(
            mod: ModManifest,
            name: () => Helper.Translation.Get("config.pos_x.name"),
            tooltip: () => Helper.Translation.Get("config.pos_x.tooltip"),
            getValue: () => Config.ScreenPositionX,
            setValue: value => Config.ScreenPositionX = value,
            min: 0f, max: 1f, interval: 0.01f
        );

        configMenu.AddNumberOption(
            mod: ModManifest,
            name: () => Helper.Translation.Get("config.pos_y.name"),
            tooltip: () => Helper.Translation.Get("config.pos_y.tooltip"),
            getValue: () => Config.ScreenPositionY,
            setValue: value => Config.ScreenPositionY = value,
            min: 0f, max: 1f, interval: 0.01f
        );
    }
}

public static class BobberBarPatch
{
    public static void Prefix(BobberBar __instance)
    {
        // If disabled, just let the original code run (which positions it over the player)
        if (!ModEntry.Config.Enabled)
            return;

        try
        {
            // 1. Calculate Target Position based on UI Viewport
            // Game1.uiViewport handles the global UI scale automatically for coordinates.
            int viewportWidth = Game1.uiViewport.Width;
            int viewportHeight = Game1.uiViewport.Height;

            int targetX = (int)(viewportWidth * ModEntry.Config.ScreenPositionX);
            int targetY = (int)(viewportHeight * ModEntry.Config.ScreenPositionY);

            // 2. Center the bar on that target
            // __instance.width/height are accurate for the UI layout
            targetX -= __instance.width / 2;
            targetY -= __instance.height / 2;

            // 3. Override the position
            // The BobberBar normally calculates this in update(), but we override it right before draw()
            __instance.xPositionOnScreen = targetX;
            __instance.yPositionOnScreen = targetY;
        }
        catch (Exception ex)
        {
            ModEntry.ModMonitor.Log($"Error in BobberBar Prefix: {ex.Message}", LogLevel.Error);
        }
    }
}