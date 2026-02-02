using System;
using System.Diagnostics;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace TextureCachePurge
{
    public class ModEntry : Mod
    {
        private ModConfig Config = null!;
        private int TicksSinceLastRamCheck = 0;
        private int CooldownTicks = 0;

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;
            helper.Events.GameLoop.DayEnding += this.OnDayEnding;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null) return;

            configMenu.Register(
                mod: this.ModManifest,
                reset: () => this.Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.Config)
            );

            // --- Sleep Settings ---
            configMenu.AddSectionTitle(this.ModManifest, () => "Sleep Settings");

            configMenu.AddBoolOption(
                this.ModManifest,
                () => this.Config.AutoClearAtSleep,
                v => this.Config.AutoClearAtSleep = v,
                () => this.Helper.Translation.Get("config.auto-sleep.name"),
                () => this.Helper.Translation.Get("config.auto-sleep.description")
            );

            configMenu.AddNumberOption(
                this.ModManifest,
                () => this.Config.PurgeFrequencyDays,
                v => this.Config.PurgeFrequencyDays = v,
                () => this.Helper.Translation.Get("config.frequency.name"),
                () => this.Helper.Translation.Get("config.frequency.description"),
                min: 1, max: 28, interval: 1
            );

            configMenu.AddNumberOption(
                this.ModManifest,
                () => this.Config.MinimumRamForSleepPurge,
                v => this.Config.MinimumRamForSleepPurge = v,
                () => this.Helper.Translation.Get("config.sleep-ram-min.name"),
                () => this.Helper.Translation.Get("config.sleep-ram-min.description"),
                min: 0, max: 16384, interval: 512
            );

            // --- Manual Settings ---
            configMenu.AddSectionTitle(this.ModManifest, () => "Manual Settings");

            configMenu.AddKeybind(
                this.ModManifest,
                () => this.Config.ManualClearKey,
                v => this.Config.ManualClearKey = v,
                () => this.Helper.Translation.Get("config.manual-key.name"),
                () => this.Helper.Translation.Get("config.manual-key.description")
            );

            // --- Active Settings ---
            configMenu.AddSectionTitle(this.ModManifest, () => "Active Play Settings");

            configMenu.AddBoolOption(
                this.ModManifest,
                () => this.Config.EnableRamThreshold,
                v => this.Config.EnableRamThreshold = v,
                () => this.Helper.Translation.Get("config.ram-enable.name"),
                () => this.Helper.Translation.Get("config.ram-enable.description")
            );

            configMenu.AddNumberOption(
                this.ModManifest,
                () => this.Config.RamThresholdMB,
                v => this.Config.RamThresholdMB = v,
                () => this.Helper.Translation.Get("config.ram-value.name"),
                () => this.Helper.Translation.Get("config.ram-value.description"),
                min: 512, max: 16384, interval: 128
            );
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (this.CooldownTicks > 0)
            {
                this.CooldownTicks--;
                return;
            }

            if (!this.Config.EnableRamThreshold || !Context.IsWorldReady) return;

            if (++this.TicksSinceLastRamCheck >= 1800)
            {
                this.TicksSinceLastRamCheck = 0;
                this.CheckRamUsage();
            }
        }

        private long GetRamUsageMB()
        {
            try
            {
                using Process proc = Process.GetCurrentProcess();
                return proc.PrivateMemorySize64 / (1024 * 1024);
            }
            catch
            {
                return -1;
            }
        }

        private void CheckRamUsage()
        {
            long currentRam = this.GetRamUsageMB();
            if (currentRam == -1) return;

            if (currentRam > this.Config.RamThresholdMB)
            {
                this.Monitor.Log($"RAM Usage ({currentRam} MB) exceeded limit ({this.Config.RamThresholdMB} MB). Purging...", LogLevel.Warn);
                this.PerformCacheClear("Auto-RAM");
                this.CooldownTicks = 18000;
            }
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (Context.IsWorldReady && e.Button == this.Config.ManualClearKey)
                this.PerformCacheClear("Manual");
        }

        private void OnDayEnding(object? sender, DayEndingEventArgs e)
        {
            if (!this.Config.AutoClearAtSleep) return;

            if (Game1.dayOfMonth % this.Config.PurgeFrequencyDays != 0)
                return;

            if (this.Config.MinimumRamForSleepPurge > 0)
            {
                long currentRam = this.GetRamUsageMB();
                if (currentRam != -1 && currentRam < this.Config.MinimumRamForSleepPurge)
                    return;
            }

            this.PerformCacheClear("Scheduled");
        }

        private void PerformCacheClear(string type)
        {
            try
            {
                this.Helper.GameContent.InvalidateCache(asset =>
                {
                    if (asset.DataType != typeof(Texture2D)) return false;

                    string name = asset.Name.Name;

                    // --- CRITICAL CRASH FIX ---
                    // "ObjectDisposedException" happens when we purge a texture that another mod 
                    // (like Alternative Textures) is still holding a direct reference to.
                    // We must EXCLUDE these specific folders to prevent crashes.

                    if (
                        // UI & System
                        name.StartsWith("LooseSprites/Cursors", StringComparison.OrdinalIgnoreCase) ||
                        name.StartsWith("LooseSprites/font", StringComparison.OrdinalIgnoreCase) ||
                        name.StartsWith("LooseSprites/ControllerMaps", StringComparison.OrdinalIgnoreCase) ||

                        // World Rendering (Crash Prevention for Alternative Textures/SpaceCore)
                        name.StartsWith("Buildings", StringComparison.OrdinalIgnoreCase) ||
                        name.StartsWith("TileSheets", StringComparison.OrdinalIgnoreCase) ||
                        name.StartsWith("Maps", StringComparison.OrdinalIgnoreCase) ||
                        name.StartsWith("TerrainFeatures", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        return false; // Do NOT purge these
                    }

                    return true; // Purge everything else (Characters, Portraits, etc.)
                });

                GC.Collect();
                GC.WaitForPendingFinalizers();

                if (type == "Manual" || type == "Auto-RAM")
                    Game1.addHUDMessage(new HUDMessage(this.Helper.Translation.Get("hud.purged"), 2));
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Cache purge failed: {ex.Message}", LogLevel.Error);
            }
        }
    }
}