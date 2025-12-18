using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using StardewModdingAPI;

namespace StardewHighPriority
{
    public sealed class ModEntry : Mod
    {
        private ModConfig Config = null!;

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            // Apply ASAP on mod load (i.e., when the game starts with SMAPI).
            this.TryApplyPriority();
        }

        private void TryApplyPriority()
        {
            if (!this.Config.Enabled)
            {
                if (this.Config.LogSuccess)
                    this.Monitor.Log(this.Helper.Translation.Get("log.disabled"), LogLevel.Info);
                return;
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                this.Monitor.Log(this.Helper.Translation.Get("log.not_windows"), LogLevel.Debug);
                return;
            }

            if (!TryParsePriority(this.Config.Priority, out ProcessPriorityClass desired))
            {
                this.Monitor.Log(
                    this.Helper.Translation.Get("log.invalid_priority", new { value = this.Config.Priority }),
                    LogLevel.Warn
                );
                return;
            }

            try
            {
                Process proc = Process.GetCurrentProcess();
                proc.PriorityClass = desired;

                if (this.Config.LogSuccess)
                {
                    this.Monitor.Log(
                        this.Helper.Translation.Get("log.set_priority", new { value = proc.PriorityClass.ToString() }),
                        LogLevel.Info
                    );
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

            // Accept a few friendly variants.
            string value = raw.Trim();

            if (value.Equals("Above Normal", StringComparison.OrdinalIgnoreCase))
                value = "AboveNormal";

            // Only allow safe/common choices (avoid RealTime).
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
    }
}
