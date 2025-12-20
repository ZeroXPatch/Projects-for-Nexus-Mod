using System;
using System.Diagnostics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace StardewPriority
{
    public sealed class ModEntry : Mod
    {
        private const string GmcmId = "spacechase0.GenericModConfigMenu";
        private static readonly string[] AllowedPriorities = { "Normal", "AboveNormal", "High" };

        private ModConfig Config = null!;

        private bool? lastIsFocused;
        private ProcessPriorityClass? lastApplied;

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;

            // Best-effort initial apply (may be before the game instance exists; we guard inside).
            this.ApplyPriorityForCurrentFocus(force: true);
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            this.RegisterGmcm();
            this.ApplyPriorityForCurrentFocus(force: true);
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!this.Config.Enabled)
                return;

            if (!OperatingSystem.IsWindows())
                return;

            // Game instance should exist very early, but guard just in case.
            if (Game1.game1 is null)
                return;

            bool isFocused = Game1.game1.IsActive;

            // Only act on focus change.
            if (this.lastIsFocused is null || this.lastIsFocused.Value != isFocused)
            {
                this.lastIsFocused = isFocused;
                this.ApplyPriorityForCurrentFocus(force: false);
            }
        }

        private void ApplyPriorityForCurrentFocus(bool force)
        {
            if (!this.Config.Enabled)
                return;

            if (!OperatingSystem.IsWindows())
                return;

            bool isFocused = Game1.game1?.IsActive ?? true;

            string desiredText = isFocused ? this.Config.FocusedPriority : this.Config.UnfocusedPriority;
            if (!TryParsePriority(desiredText, out ProcessPriorityClass desired))
            {
                this.Monitor.Log(
                    this.Helper.Translation.Get("log.invalid_priority", new { value = desiredText }),
                    LogLevel.Warn
                );
                return;
            }

            if (!force && this.lastApplied.HasValue && this.lastApplied.Value == desired)
                return;

            try
            {
                Process proc = Process.GetCurrentProcess();
                proc.PriorityClass = desired;
                this.lastApplied = desired;

                if (this.Config.LogSuccess)
                {
                    string key = isFocused ? "log.set_priority_focused" : "log.set_priority_unfocused";
                    this.Monitor.Log(this.Helper.Translation.Get(key, new { value = proc.PriorityClass.ToString() }), LogLevel.Info);
                }
            }
            catch (Exception ex)
            {
                if (this.Config.LogFailure)
                {
                    this.Monitor.Log(
                        this.Helper.Translation.Get("log.failed", new { error = ex.GetType().Name, message = ex.Message }),
                        LogLevel.Warn
                    );
                }
            }
        }

        private static bool TryParsePriority(string? raw, out ProcessPriorityClass priority)
        {
            priority = ProcessPriorityClass.Normal;

            if (string.IsNullOrWhiteSpace(raw))
                return false;

            string value = raw.Trim();

            // Friendly variant
            if (value.Equals("Above Normal", StringComparison.OrdinalIgnoreCase))
                value = "AboveNormal";

            // Allow-list safe choices (no RealTime)
            if (value.Equals("Normal", StringComparison.OrdinalIgnoreCase))
            {
                priority = ProcessPriorityClass.Normal;
                return true;
            }

            if (value.Equals("AboveNormal", StringComparison.OrdinalIgnoreCase))
            {
                priority = ProcessPriorityClass.AboveNormal;
                return true;
            }

            if (value.Equals("High", StringComparison.OrdinalIgnoreCase))
            {
                priority = ProcessPriorityClass.High;
                return true;
            }

            return false;
        }

        private void RegisterGmcm()
        {
            var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>(GmcmId);
            if (gmcm is null)
                return;

            gmcm.Register(
                mod: this.ModManifest,
                reset: () => this.Config = new ModConfig(),
                save: () =>
                {
                    this.Helper.WriteConfig(this.Config);
                    this.ApplyPriorityForCurrentFocus(force: true);
                }
            );

            gmcm.AddSectionTitle(
                mod: this.ModManifest,
                text: () => this.Helper.Translation.Get("gmcm.section.general"),
                tooltip: () => this.Helper.Translation.Get("gmcm.section.general.tooltip")
            );

            gmcm.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => this.Config.Enabled,
                setValue: value => this.Config.Enabled = value,
                name: () => this.Helper.Translation.Get("gmcm.enabled"),
                tooltip: () => this.Helper.Translation.Get("gmcm.enabled.tooltip")
            );

            gmcm.AddTextOption(
                mod: this.ModManifest,
                getValue: () => this.Config.FocusedPriority,
                setValue: value => this.Config.FocusedPriority = value,
                name: () => this.Helper.Translation.Get("gmcm.focusedPriority"),
                tooltip: () => this.Helper.Translation.Get("gmcm.focusedPriority.tooltip"),
                allowedValues: AllowedPriorities
            );

            gmcm.AddTextOption(
                mod: this.ModManifest,
                getValue: () => this.Config.UnfocusedPriority,
                setValue: value => this.Config.UnfocusedPriority = value,
                name: () => this.Helper.Translation.Get("gmcm.unfocusedPriority"),
                tooltip: () => this.Helper.Translation.Get("gmcm.unfocusedPriority.tooltip"),
                allowedValues: AllowedPriorities
            );

            gmcm.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => this.Config.LogSuccess,
                setValue: value => this.Config.LogSuccess = value,
                name: () => this.Helper.Translation.Get("gmcm.logSuccess"),
                tooltip: () => this.Helper.Translation.Get("gmcm.logSuccess.tooltip")
            );

            gmcm.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => this.Config.LogFailure,
                setValue: value => this.Config.LogFailure = value,
                name: () => this.Helper.Translation.Get("gmcm.logFailure"),
                tooltip: () => this.Helper.Translation.Get("gmcm.logFailure.tooltip")
            );
        }
    }

    /// <summary>
    /// Minimal GMCM API we use (safe for SMAPI 4.x).
    /// </summary>
    public interface IGenericModConfigMenuApi
    {
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);

        void AddSectionTitle(IManifest mod, Func<string> text, Func<string>? tooltip = null);

        void AddBoolOption(
            IManifest mod,
            Func<bool> getValue,
            Action<bool> setValue,
            Func<string> name,
            Func<string>? tooltip = null,
            string? fieldId = null
        );

        void AddTextOption(
            IManifest mod,
            Func<string> getValue,
            Action<string> setValue,
            Func<string> name,
            Func<string>? tooltip = null,
            string[]? allowedValues = null,
            Func<string, string>? formatAllowedValue = null,
            string? fieldId = null
        );
    }
}
