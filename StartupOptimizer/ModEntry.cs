using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace StartupOptimizer;

public class ModEntry : Mod
{
    private ModConfig Config = null!;
    private readonly Stopwatch startupStopwatch = new();
    private bool pendingTitleActions;
    private int titleTicks;
    private bool skipRequestIssued;
    private bool hasAutoOpenedLoadMenu;
    private bool quickResumeAttempted;

    public override void Entry(IModHelper helper)
    {
        this.Config = helper.ReadConfig<ModConfig>();
        this.startupStopwatch.Start();

        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
        helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
        helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        this.RegisterGmcm();

        if (this.Config.EnableDiagnosticsLogging)
        {
            int modCount = this.Helper.ModRegistry.GetAll().Count();
            this.Monitor.Log(
                $"Startup Optimizer initialized ({modCount} mods detected).",
                LogLevel.Info);

            this.Monitor.Log(
                $"Config → SkipIntro: {this.Config.SkipLogosAndIntro}, SkipTitleIdle: {this.Config.SkipTitleIdle}, AutoOpenLoadMenu: {this.Config.AutoOpenLoadMenu}, QuickResume: {this.Config.EnableQuickResume}, Diagnostics: {this.Config.EnableDiagnosticsLogging}",
                LogLevel.Info);
        }
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        if (Game1.player is null)
        {
            return;
        }

        string saveName = Game1.GetSaveFileName(Game1.player);
        this.Config.QuickResumeSaveName = saveName;
        this.Helper.WriteConfig(this.Config);

        if (this.Config.EnableDiagnosticsLogging)
        {
            this.Monitor.Log($"Recorded last played save '{saveName}'.", LogLevel.Trace);
        }
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        this.pendingTitleActions = true;
        this.titleTicks = 0;
        this.quickResumeAttempted = false;
        this.skipRequestIssued = false;
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (this.Config.SkipLogosAndIntro && !this.skipRequestIssued && Game1.game1 is not null && Game1.gameMode != Game1.titleScreenGameMode)
        {
            Game1.game1.skipToTitle = true;
            this.skipRequestIssued = true;
        }

        if (!this.pendingTitleActions)
        {
            return;
        }

        this.titleTicks++;
        if (this.titleTicks < 5)
        {
            return;
        }

        if (Game1.activeClickableMenu is not TitleMenu titleMenu)
        {
            return;
        }

        this.pendingTitleActions = false;
        this.HandleTitleMenu(titleMenu);
    }

    private void HandleTitleMenu(TitleMenu titleMenu)
    {
        if (TitleMenu.subMenu is not null)
        {
            return;
        }

        if (this.Config.SkipTitleIdle)
        {
            TitleMenu.skipToTitle = true;
        }

        if (this.Config.EnableQuickResume && !this.quickResumeAttempted && this.CanQuickResume())
        {
            this.quickResumeAttempted = true;
            if (!this.Helper.Input.IsDown(this.Config.QuickResumeInterruptKey))
            {
                if (this.TryQuickResume())
                {
                    return;
                }
            }
        }

        if (this.ShouldAutoOpenLoadMenu())
        {
            this.hasAutoOpenedLoadMenu = true;
            TitleMenu.subMenu = new LoadGameMenu();
        }

        if (this.Config.EnableDiagnosticsLogging)
        {
            this.Monitor.Log($"Startup Optimizer reached title. Total startup time: {this.startupStopwatch.ElapsedMilliseconds} ms.", LogLevel.Info);
        }
    }

    private bool ShouldAutoOpenLoadMenu()
    {
        return this.Config.AutoOpenLoadMenu switch
        {
            AutoOpenLoadMenuMode.OnFirstLaunch => !this.hasAutoOpenedLoadMenu,
            AutoOpenLoadMenuMode.Always => true,
            _ => false,
        };
    }

    private bool CanQuickResume()
    {
        if (Context.IsMultiplayer)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(this.Config.QuickResumeSaveName))
        {
            return false;
        }

        string saveFolder = Path.Combine(SaveGame.savesPath, this.Config.QuickResumeSaveName!);
        if (!Directory.Exists(saveFolder))
        {
            return false;
        }

        return Directory.EnumerateFiles(saveFolder, "*.sav").Any();
    }

    private bool TryQuickResume()
    {
        if (SaveGame.IsProcessing)
        {
            return false;
        }

        try
        {
            if (this.Config.EnableDiagnosticsLogging)
            {
                this.Monitor.Log($"Attempting quick resume for save '{this.Config.QuickResumeSaveName}'.", LogLevel.Info);
            }

            SaveGame.Load(this.Config.QuickResumeSaveName);
            return true;
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"Quick resume failed: {ex.Message}", LogLevel.Warn);
            return false;
        }
    }

    private void RegisterGmcm()
    {
        var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (gmcm is null)
        {
            return;
        }

        gmcm.Register(
            this.ModManifest,
            () => this.Config = new ModConfig(),
            () => this.Helper.WriteConfig(this.Config),
            true);

        gmcm.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("gmcm.section.general"));
        gmcm.AddBoolOption(
            this.ModManifest,
            () => this.Config.SkipLogosAndIntro,
            value => this.Config.SkipLogosAndIntro = value,
            () => this.Helper.Translation.Get("gmcm.skipIntro.name"),
            () => this.Helper.Translation.Get("gmcm.skipIntro.tooltip"));
        gmcm.AddBoolOption(
            this.ModManifest,
            () => this.Config.SkipTitleIdle,
            value => this.Config.SkipTitleIdle = value,
            () => this.Helper.Translation.Get("gmcm.skipTitleIdle.name"),
            () => this.Helper.Translation.Get("gmcm.skipTitleIdle.tooltip"));
        gmcm.AddTextOption(
            this.ModManifest,
            () => this.Config.AutoOpenLoadMenu.ToString(),
            value => this.Config.AutoOpenLoadMenu = Enum.TryParse(value, out AutoOpenLoadMenuMode parsed) ? parsed : AutoOpenLoadMenuMode.Off,
            () => this.Helper.Translation.Get("gmcm.autoLoad.name"),
            () => this.Helper.Translation.Get("gmcm.autoLoad.tooltip"),
            new[] { AutoOpenLoadMenuMode.Off.ToString(), AutoOpenLoadMenuMode.OnFirstLaunch.ToString(), AutoOpenLoadMenuMode.Always.ToString() });

        gmcm.AddSectionTitle(
            this.ModManifest,
            () => this.Helper.Translation.Get("gmcm.section.quickResume"),
            () =>
            {
                if (string.IsNullOrWhiteSpace(this.Config.QuickResumeSaveName))
                {
                    return this.Helper.Translation.Get("gmcm.quickResume.lastSave.none");
                }

                return string.Format(this.Helper.Translation.Get("gmcm.quickResume.lastSave"), this.Config.QuickResumeSaveName);
            });
        gmcm.AddBoolOption(
            this.ModManifest,
            () => this.Config.EnableQuickResume,
            value => this.Config.EnableQuickResume = value,
            () => this.Helper.Translation.Get("gmcm.quickResume.name"),
            () => this.Helper.Translation.Get("gmcm.quickResume.tooltip"));
        gmcm.AddKeybind(
            this.ModManifest,
            () => this.Config.QuickResumeInterruptKey,
            value => this.Config.QuickResumeInterruptKey = value,
            () => this.Helper.Translation.Get("gmcm.quickResume.key.name"),
            () => this.Helper.Translation.Get("gmcm.quickResume.key.tooltip"));

        gmcm.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("gmcm.section.diagnostics"));
        gmcm.AddBoolOption(
            this.ModManifest,
            () => this.Config.EnableDiagnosticsLogging,
            value => this.Config.EnableDiagnosticsLogging = value,
            () => this.Helper.Translation.Get("gmcm.diagnostics.name"),
            () => this.Helper.Translation.Get("gmcm.diagnostics.tooltip"));
    }
}
