using System;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace StartupOptimizer;

public class ModEntry : Mod
{
    private ModConfig Config = null!;

    // title-screen state
    private bool isOnTitleScreen;
    private int titleTicks;
    private bool hasAutoOpenedLoadMenu;

    public override void Entry(IModHelper helper)
    {
        this.Config = helper.ReadConfig<ModConfig>();

        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        this.RegisterGmcm();
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        // only care about title screen before a save is loaded
        if (!Context.IsGameLaunched || Context.IsWorldReady)
            return;

        if (Game1.activeClickableMenu is TitleMenu titleMenu)
        {
            if (!this.isOnTitleScreen)
            {
                // just arrived at title
                this.isOnTitleScreen = true;
                this.titleTicks = 0;
            }

            this.titleTicks++;

            // wait a few ticks for the title to fully initialize
            if (this.titleTicks >= 5)
                this.HandleTitleMenu(titleMenu);
        }
        else
        {
            // left title screen
            this.isOnTitleScreen = false;
            this.titleTicks = 0;
        }
    }

    private void HandleTitleMenu(TitleMenu titleMenu)
    {
        if (TitleMenu.subMenu is not null)
            return;

        if (!this.ShouldAutoOpenLoadMenu())
            return;

        this.hasAutoOpenedLoadMenu = true;
        TitleMenu.subMenu = new LoadGameMenu();
    }

    private bool ShouldAutoOpenLoadMenu()
    {
        // Only two states:
        // Off -> never
        // OnFirstLaunch -> once per SMAPI session
        if (this.Config.AutoOpenLoadMenu == AutoOpenLoadMenuMode.Off)
            return false;

        // OnFirstLaunch
        return !this.hasAutoOpenedLoadMenu;
    }

    private void RegisterGmcm()
    {
        var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (gmcm is null)
            return;

        gmcm.Register(
            this.ModManifest,
            () => this.Config = new ModConfig(),
            () => this.Helper.WriteConfig(this.Config),
            titleScreenOnly: true
        );

        gmcm.AddSectionTitle(this.ModManifest, () => "Startup Optimizer");

        gmcm.AddTextOption(
            this.ModManifest,
            () => this.Config.AutoOpenLoadMenu.ToString(),
            value => this.Config.AutoOpenLoadMenu = Enum.TryParse(value, out AutoOpenLoadMenuMode parsed)
                ? parsed
                : AutoOpenLoadMenuMode.OnFirstLaunch, // fallback to default behaviour
            () => this.Helper.Translation.Get("gmcm.autoLoad.name"),
            () => this.Helper.Translation.Get("gmcm.autoLoad.tooltip"),
            new[]
            {
                AutoOpenLoadMenuMode.Off.ToString(),
                AutoOpenLoadMenuMode.OnFirstLaunch.ToString()
            }
        );
    }
}
