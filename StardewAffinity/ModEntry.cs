using System;
using System.Diagnostics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace StardewAffinity
{
    public sealed class ModEntry : Mod
    {
        private const string GmcmId = "spacechase0.GenericModConfigMenu";

        private static readonly string[] ModeValues =
        {
            "ExcludeCpu0", // default
            "AllCores",
            "CustomMask"
        };

        private ModConfig Config = null!;

        private bool? LastIsFocused;
        private long? LastAppliedMask;

        // Original affinity captured before we change anything.
        private long? OriginalAffinityMask;

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;

            // Best-effort initial apply (assume focused; Game1.game1 may be null this early).
            this.TryCaptureOriginalAffinity();
            this.TryApplyFocusedAffinity(force: true);
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            this.RegisterGmcm();

            this.TryCaptureOriginalAffinity();
            this.TryApplyFocusedAffinity(force: true);
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!this.Config.Enabled)
                return;

            if (!OperatingSystem.IsWindows())
                return;

            if (Game1.game1 is null)
                return;

            bool isFocused = Game1.game1.IsActive;

            // only act when focus changes
            if (this.LastIsFocused is null || this.LastIsFocused.Value != isFocused)
            {
                this.LastIsFocused = isFocused;

                this.TryCaptureOriginalAffinity();

                if (isFocused)
                    this.TryApplyFocusedAffinity(force: false);
                else
                    this.TryRestoreOriginalAffinity(force: false);
            }
        }

        private void TryCaptureOriginalAffinity()
        {
            if (this.OriginalAffinityMask.HasValue)
                return;

            if (!this.Config.Enabled)
                return;

            if (!OperatingSystem.IsWindows())
                return;

            try
            {
                var proc = Process.GetCurrentProcess();
                // capture whatever the game is currently using before we modify it
                this.OriginalAffinityMask = proc.ProcessorAffinity.ToInt64();

                if (this.Config.LogInfo)
                {
                    this.Monitor.Log(
                        this.Helper.Translation.Get("log.captured_original", new { mask = FormatMaskHex(this.OriginalAffinityMask.Value) }),
                        LogLevel.Debug
                    );
                }
            }
            catch (Exception ex)
            {
                // Not fatal; we can fall back to "all cores" if needed.
                if (this.Config.LogInfo)
                {
                    this.Monitor.Log(
                        this.Helper.Translation.Get("log.capture_failed", new { error = ex.GetType().Name, message = ex.Message }),
                        LogLevel.Debug
                    );
                }
            }
        }

        private void TryApplyFocusedAffinity(bool force)
        {
            if (!this.Config.Enabled)
                return;

            if (!OperatingSystem.IsWindows())
            {
                if (this.Config.LogInfo)
                    this.Monitor.Log(this.Helper.Translation.Get("log.not_windows"), LogLevel.Debug);
                return;
            }

            int logicalCount = Environment.ProcessorCount;
            int maxMaskBits = IntPtr.Size * 8; // 64 on 64-bit
            int usableBits = Math.Min(logicalCount, maxMaskBits);

            if (usableBits <= 1)
            {
                if (this.Config.LogInfo)
                    this.Monitor.Log(this.Helper.Translation.Get("log.not_enough_cores", new { cores = logicalCount }), LogLevel.Info);
                return;
            }

            if (logicalCount > usableBits && this.Config.LogInfo)
            {
                this.Monitor.Log(
                    this.Helper.Translation.Get("log.too_many_cores", new { cores = logicalCount, usable = usableBits }),
                    LogLevel.Info
                );
            }

            if (!TryGetDesiredMask(this.Config, usableBits, out long desiredMask, out string? errorKey))
            {
                this.Monitor.Log(this.Helper.Translation.Get(errorKey ?? "log.invalid_config"), LogLevel.Warn);
                return;
            }

            // Avoid reapplying the same mask unless forced
            if (!force && this.LastAppliedMask.HasValue && this.LastAppliedMask.Value == desiredMask)
                return;

            this.TrySetAffinityMask(
                mask: desiredMask,
                logKey: "log.applied_focused",
                extraLogArgs: new { mode = this.Config.Mode, mask = FormatMaskHex(desiredMask), cores = logicalCount }
            );
        }

        private void TryRestoreOriginalAffinity(bool force)
        {
            if (!this.Config.Enabled)
                return;

            if (!this.Config.RestoreOnUnfocus)
                return;

            if (!OperatingSystem.IsWindows())
                return;

            int logicalCount = Environment.ProcessorCount;
            int maxMaskBits = IntPtr.Size * 8;
            int usableBits = Math.Min(logicalCount, maxMaskBits);

            long fallbackAll = MakeAllCoresMask(Math.Max(1, usableBits));
            long restoreMask = this.OriginalAffinityMask ?? fallbackAll;

            // Clamp to usable bits (avoid invalid bits)
            restoreMask &= fallbackAll;

            if (restoreMask == 0)
                restoreMask = fallbackAll;

            // Avoid reapplying the same mask unless forced
            if (!force && this.LastAppliedMask.HasValue && this.LastAppliedMask.Value == restoreMask)
                return;

            this.TrySetAffinityMask(
                mask: restoreMask,
                logKey: "log.restored_unfocused",
                extraLogArgs: new { mask = FormatMaskHex(restoreMask) }
            );
        }

        private void TrySetAffinityMask(long mask, string logKey, object extraLogArgs)
        {
            try
            {
                var proc = Process.GetCurrentProcess();
                proc.ProcessorAffinity = new IntPtr(mask);
                this.LastAppliedMask = mask;

                if (this.Config.LogInfo)
                    this.Monitor.Log(this.Helper.Translation.Get(logKey, extraLogArgs), LogLevel.Info);
            }
            catch (Exception ex)
            {
                this.Monitor.Log(
                    this.Helper.Translation.Get("log.failed", new { error = ex.GetType().Name, message = ex.Message }),
                    LogLevel.Warn
                );
            }
        }

        private static bool TryGetDesiredMask(ModConfig config, int usableBits, out long mask, out string? errorKey)
        {
            mask = 0;
            errorKey = null;

            long all = MakeAllCoresMask(usableBits);

            string mode = (config.Mode ?? "").Trim();

            if (mode.Equals("ExcludeCpu0", StringComparison.OrdinalIgnoreCase))
            {
                mask = all & ~1L; // clear CPU 0
                if (mask == 0)
                {
                    errorKey = "log.mask_zero";
                    return false;
                }
                return true;
            }

            if (mode.Equals("AllCores", StringComparison.OrdinalIgnoreCase))
            {
                mask = all;
                return true;
            }

            if (mode.Equals("CustomMask", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryParseMask(config.CustomMask, out long parsed))
                {
                    errorKey = "log.custommask_parse_fail";
                    return false;
                }

                // Clamp to usable bits
                parsed &= all;

                if (parsed == 0)
                {
                    errorKey = "log.mask_zero";
                    return false;
                }

                mask = parsed;
                return true;
            }

            errorKey = "log.unknown_mode";
            return false;
        }

        private static long MakeAllCoresMask(int usableBits)
        {
            if (usableBits >= 64)
                return -1L;

            return (1L << usableBits) - 1L;
        }

        private static bool TryParseMask(string? raw, out long value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            string s = raw.Trim();

            try
            {
                if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    value = Convert.ToInt64(s.Substring(2), 16);
                else
                    value = Convert.ToInt64(s, 10);

                return value > 0;
            }
            catch
            {
                return false;
            }
        }

        private static string FormatMaskHex(long mask)
            => "0x" + unchecked((ulong)mask).ToString("X");

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
                    this.TryCaptureOriginalAffinity();
                    this.TryApplyFocusedAffinity(force: true);
                }
            );

            gmcm.AddSectionTitle(
                mod: this.ModManifest,
                text: () => this.Helper.Translation.Get("gmcm.section.title"),
                tooltip: () => this.Helper.Translation.Get("gmcm.section.tooltip")
            );

            gmcm.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => this.Config.Enabled,
                setValue: v => this.Config.Enabled = v,
                name: () => this.Helper.Translation.Get("gmcm.enabled"),
                tooltip: () => this.Helper.Translation.Get("gmcm.enabled.tooltip")
            );

            gmcm.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => this.Config.RestoreOnUnfocus,
                setValue: v => this.Config.RestoreOnUnfocus = v,
                name: () => this.Helper.Translation.Get("gmcm.restoreOnUnfocus"),
                tooltip: () => this.Helper.Translation.Get("gmcm.restoreOnUnfocus.tooltip")
            );

            gmcm.AddTextOption(
                mod: this.ModManifest,
                getValue: () => this.Config.Mode,
                setValue: v => this.Config.Mode = v,
                name: () => this.Helper.Translation.Get("gmcm.mode"),
                tooltip: () => this.Helper.Translation.Get("gmcm.mode.tooltip"),
                allowedValues: ModeValues,
                formatAllowedValue: v => this.Helper.Translation.Get($"gmcm.mode.{v}")
            );

            gmcm.AddTextOption(
                mod: this.ModManifest,
                getValue: () => this.Config.CustomMask,
                setValue: v => this.Config.CustomMask = v,
                name: () => this.Helper.Translation.Get("gmcm.custommask"),
                tooltip: () => this.Helper.Translation.Get("gmcm.custommask.tooltip"),
                allowedValues: null,
                formatAllowedValue: null
            );

            gmcm.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => this.Config.LogInfo,
                setValue: v => this.Config.LogInfo = v,
                name: () => this.Helper.Translation.Get("gmcm.logInfo"),
                tooltip: () => this.Helper.Translation.Get("gmcm.logInfo.tooltip")
            );
        }
    }

    // GMCM API (minimal/current signatures we use).
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
