using GMCMSearchBar.Framework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GMCMSearchBar
{
    public sealed class ModEntry : Mod
    {
        private ModConfig Config = new();
        private IGenericModConfigMenuApi? Gmcm;

        // cached list of GMCM-registered manifests
        private List<IManifest> Registered = new();

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            this.Gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (this.Gmcm is null)
            {
                this.Monitor.Log("GMCM not found. This mod requires Generic Mod Config Menu to function.", LogLevel.Warn);
                return;
            }

            this.Gmcm.Register(
                mod: this.ModManifest,
                reset: () => this.Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.Config)
            );

            this.Gmcm.AddSectionTitle(
                this.ModManifest,
                text: () => "GMCM Search",
                tooltip: () => "Search and open other mods' GMCM configs quickly."
            );

            this.Gmcm.AddKeybindList(
                this.ModManifest,
                getValue: () => this.Config.OpenSearchMenuKey,
                setValue: v => this.Config.OpenSearchMenuKey = v,
                name: () => "Open search menu",
                tooltip: () => "Hotkey to open the GMCM Search menu."
            );

            this.Gmcm.AddBoolOption(
                this.ModManifest,
                getValue: () => this.Config.ShowUniqueId,
                setValue: v => this.Config.ShowUniqueId = v,
                name: () => "Show UniqueID",
                tooltip: () => "Show each entry's UniqueID under its name."
            );

            this.Gmcm.AddBoolOption(
                this.ModManifest,
                getValue: () => this.Config.IncludeContentPacks,
                setValue: v => this.Config.IncludeContentPacks = v,
                name: () => "Include content packs",
                tooltip: () => "Show GMCM-registered content packs (e.g. Content Patcher configs)."
            );

            this.Gmcm.AddParagraph(
                this.ModManifest,
                text: () => "Tip: Type to filter. Enter opens the selected config. Esc closes."
            );
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!this.Config.OpenSearchMenuKey.JustPressed())
                return;

            // TOGGLE LOGIC: If the menu is already open, close it.
            if (Game1.activeClickableMenu is SearchMenu)
            {
                Game1.playSound("bigDeSelect");
                Game1.activeClickableMenu = null;
                return;
            }

            // Check if World is ready (or if we are on title screen)
            if (!Context.IsWorldReady && Game1.activeClickableMenu is not TitleMenu)
                return;

            // Check for incompatible menus, but ALLOW Generic Mod Config Menu to be open
            bool isTitle = (Game1.activeClickableMenu is TitleMenu);
            bool isGmcm = (Game1.activeClickableMenu?.GetType().FullName?.Contains("GenericModConfigMenu") == true);

            // If a menu is open that isn't Title or GMCM, don't open search
            if (Game1.activeClickableMenu is not null && !isTitle && !isGmcm)
                return;

            if (this.Gmcm is null)
            {
                Game1.showRedMessage("Generic Mod Config Menu is not installed.");
                return;
            }

            // Refresh list
            this.Registered = GMCMRegistryScanner.GetRegisteredModsOrFallback(
                helper: this.Helper,
                gmcmApiObj: this.Gmcm,
                monitor: this.Monitor,
                selfManifest: this.ModManifest,
                includeContentPacks: this.Config.IncludeContentPacks
            );

            if (this.Registered.Count == 0)
            {
                Game1.showRedMessage("No GMCM-registered configs found.");
                return;
            }

            // Workaround for GMCM crash: 
            // If GMCM is currently open, we must close it and wait 1 tick before opening our menu.
            // Opening directly over it causes GMCM's UpdateTicking to crash with a NullReference.
            if (isGmcm)
            {
                Game1.activeClickableMenu = null;
                this.Helper.Events.GameLoop.UpdateTicked += this.OpenMenuNextTick;
            }
            else
            {
                this.OpenMenu();
            }
        }

        private void OpenMenuNextTick(object? sender, UpdateTickedEventArgs e)
        {
            this.Helper.Events.GameLoop.UpdateTicked -= this.OpenMenuNextTick;
            this.OpenMenu();
        }

        private void OpenMenu()
        {
            Game1.activeClickableMenu = new SearchMenu(
                helper: this.Helper,
                monitor: this.Monitor,
                title: "GMCM Search",
                showUniqueId: this.Config.ShowUniqueId,
                mods: this.Registered,
                openMod: this.TryOpenMod
            );
        }

        private bool TryOpenMod(IManifest manifest)
        {
            if (this.Gmcm is null)
                return false;

            try
            {
                this.Gmcm.OpenModMenuAsChildMenu(manifest);
                return true;
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Failed to open GMCM menu for '{manifest.UniqueID}': {ex}", LogLevel.Warn);
                return false;
            }
        }
    }
}