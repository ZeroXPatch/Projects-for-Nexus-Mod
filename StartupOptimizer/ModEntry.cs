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
    private bool cancelAutoOpenForCurrentTitle; // set by holding LeftShift

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
                this.cancelAutoOpenForCurrentTitle = false;
            }

            this.titleTicks++;

            // if player holds LeftShift at any point on this title visit,
            // cancel auto-open so they can access the Mods/GMCM menu.
            if (this.Helper.Input.IsDown(SButton.LeftShift))
                this.cancelAutoOpenForCurrentTitle = true;

            // wait a few ticks for the title to fully initialize
            if (this.titleTicks >= 5)
                this.HandleTitleMenu(titleMenu);
        }
        else
        {
            // left title screen
            this.isOnTitleScreen = false;
            this.titleTicks = 0;
            this.cancelAutoOpenForCurrentTitle = false;
        }
    }

    private void HandleTitleMenu(TitleMenu titleMenu)
    {
        if (TitleMenu.subMenu is not null)
            return;

        if (!this.ShouldAutoOpenLoadMenu())
            return;

        TitleMenu.subMenu = new LoadGameMenu();
    }

    private bool ShouldAutoOpenLoadMenu()
    {
        if (!this.Config.AutoOpenLoadMenu)
            return false;

        if (this.cancelAutoOpenForCurrentTitle)
            return false;

        return true;
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

        gmcm.AddBoolOption(
            this.ModManifest,
            () => this.Config.AutoOpenLoadMenu,
            value => this.Config.AutoOpenLoadMenu = value,
            () => this.Helper.Translation.Get("gmcm.autoLoad.name"),
            () => this.Helper.Translation.Get("gmcm.autoLoad.tooltip")
        );
    }
}
