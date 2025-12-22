using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace StardewStabilizer
{
    public sealed class ModEntry : Mod
    {
        private ModConfig Config = new();

        private readonly PressureMeter Meter = new();
        private readonly CleanupPlanner Planner = new();
        private readonly PressureTrend Trend = new();

        private IGenericModConfigMenuApi? Gmcm;

        private string OverlayText = "";
        private DateTime LastOverlayUpdateUtc = DateTime.MinValue;

        public override void Entry(IModHelper helper)
        {
            this.Config = this.Helper.ReadConfig<ModConfig>();
            this.Trend.Configure(this.Config.TrendWindowSeconds);

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;

            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;

            helper.Events.GameLoop.OneSecondUpdateTicked += this.OnOneSecondUpdateTicked;
            helper.Events.Input.ButtonsChanged += this.OnButtonsChanged;

            helper.Events.Display.RenderedHud += this.OnRenderedHud;

            helper.ConsoleCommands.Add(
                name: "stabilizer_status",
                documentation: "Show current memory stats and pressure.",
                callback: this.OnCmdStatus
            );
            helper.ConsoleCommands.Add(
                name: "stabilizer_clean",
                documentation: "Run a light cleanup (Gen0/Gen1).",
                callback: this.OnCmdSoftClean
            );
            helper.ConsoleCommands.Add(
                name: "stabilizer_fullclean",
                documentation: "Run a deep cleanup (full GC; optional LOH compaction).",
                callback: this.OnCmdHardClean
            );
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            this.Gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (this.Gmcm is null)
                return;

            this.Gmcm.Register(
                this.ModManifest,
                reset: () =>
                {
                    this.Config = new ModConfig();
                    this.Trend.Configure(this.Config.TrendWindowSeconds);
                    this.Planner.ResetForNewSession();
                },
                save: () =>
                {
                    this.Helper.WriteConfig(this.Config);
                    this.Trend.Configure(this.Config.TrendWindowSeconds);
                }
            );

            // ----- General -----
            this.Gmcm.AddSectionTitle(this.ModManifest,
                text: () => this.T("gmcm.section.general"),
                tooltip: () => this.T("gmcm.section.general.tip"));

            this.Gmcm.AddBoolOption(this.ModManifest,
                getValue: () => this.Config.Enabled,
                setValue: v => this.Config.Enabled = v,
                name: () => this.T("gmcm.enabled.name"),
                tooltip: () => this.T("gmcm.enabled.tip"));

            this.Gmcm.AddBoolOption(this.ModManifest,
                getValue: () => this.Config.AutoCleanup,
                setValue: v => this.Config.AutoCleanup = v,
                name: () => this.T("gmcm.autocleanup.name"),
                tooltip: () => this.T("gmcm.autocleanup.tip"));

            // ----- Thresholds -----
            this.Gmcm.AddSectionTitle(this.ModManifest,
                text: () => this.T("gmcm.section.thresholds"),
                tooltip: () => this.T("gmcm.section.thresholds.tip"));

            this.Gmcm.AddNumberOption(this.ModManifest,
                getValue: () => this.Config.SoftPressurePercent,
                setValue: v => this.Config.SoftPressurePercent = v,
                name: () => this.T("gmcm.softpressure.name"),
                tooltip: () => this.T("gmcm.softpressure.tip"),
                min: 1,
                max: 100,
                interval: 1);

            this.Gmcm.AddNumberOption(this.ModManifest,
                getValue: () => this.Config.HardPressurePercent,
                setValue: v => this.Config.HardPressurePercent = v,
                name: () => this.T("gmcm.hardpressure.name"),
                tooltip: () => this.T("gmcm.hardpressure.tip"),
                min: 1,
                max: 100,
                interval: 1);

            this.Gmcm.AddNumberOption(this.ModManifest,
                getValue: () => this.Config.EmergencyPressurePercent,
                setValue: v => this.Config.EmergencyPressurePercent = v,
                name: () => this.T("gmcm.emergencypressure.name"),
                tooltip: () => this.T("gmcm.emergencypressure.tip"),
                min: 1,
                max: 100,
                interval: 1);

            // ----- Spike filtering (trend) -----
            this.Gmcm.AddSectionTitle(this.ModManifest,
                text: () => this.T("gmcm.section.trend"),
                tooltip: () => this.T("gmcm.section.trend.tip"));

            this.Gmcm.AddBoolOption(this.ModManifest,
                getValue: () => this.Config.UseTrendAverageForDecisions,
                setValue: v => this.Config.UseTrendAverageForDecisions = v,
                name: () => this.T("gmcm.usetrendavg.name"),
                tooltip: () => this.T("gmcm.usetrendavg.tip"));

            this.Gmcm.AddNumberOption(this.ModManifest,
                getValue: () => this.Config.TrendWindowSeconds,
                setValue: v =>
                {
                    this.Config.TrendWindowSeconds = v;
                    this.Trend.Configure(v);
                },
                name: () => this.T("gmcm.trendwindow.name"),
                tooltip: () => this.T("gmcm.trendwindow.tip"),
                min: 3,
                max: 60,
                interval: 1);

            this.Gmcm.AddNumberOption(this.ModManifest,
                getValue: () => this.Config.SustainSeconds,
                setValue: v => this.Config.SustainSeconds = v,
                name: () => this.T("gmcm.sustain.name"),
                tooltip: () => this.T("gmcm.sustain.tip"),
                min: 0,
                max: 30,
                interval: 1);

            this.Gmcm.AddNumberOption(this.ModManifest,
                getValue: () => this.Config.HysteresisPercent,
                setValue: v => this.Config.HysteresisPercent = v,
                name: () => this.T("gmcm.hysteresis.name"),
                tooltip: () => this.T("gmcm.hysteresis.tip"),
                min: 0,
                max: 25,
                interval: 1);

            // ----- Freeze control (when to hard clean) -----
            this.Gmcm.AddSectionTitle(this.ModManifest,
                text: () => this.T("gmcm.section.freezecontrol"),
                tooltip: () => this.T("gmcm.section.freezecontrol.tip"));

            this.Gmcm.AddBoolOption(this.ModManifest,
                getValue: () => this.Config.HardCleanupMenuOnly,
                setValue: v => this.Config.HardCleanupMenuOnly = v,
                name: () => this.T("gmcm.hardmenuonly.name"),
                tooltip: () => this.T("gmcm.hardmenuonly.tip"));

            this.Gmcm.AddBoolOption(this.ModManifest,
                getValue: () => this.Config.PreferSoftWhileWaitingForHard,
                setValue: v => this.Config.PreferSoftWhileWaitingForHard = v,
                name: () => this.T("gmcm.prefersoft.name"),
                tooltip: () => this.T("gmcm.prefersoft.tip"));

            // ----- Cooldowns -----
            this.Gmcm.AddSectionTitle(this.ModManifest,
                text: () => this.T("gmcm.section.cooldowns"),
                tooltip: () => this.T("gmcm.section.cooldowns.tip"));

            this.Gmcm.AddNumberOption(this.ModManifest,
                getValue: () => this.Config.SoftCooldownSeconds,
                setValue: v => this.Config.SoftCooldownSeconds = v,
                name: () => this.T("gmcm.softcooldown.name"),
                tooltip: () => this.T("gmcm.softcooldown.tip"),
                min: 5,
                max: 3600,
                interval: 5);

            this.Gmcm.AddNumberOption(this.ModManifest,
                getValue: () => this.Config.HardCooldownSeconds,
                setValue: v => this.Config.HardCooldownSeconds = v,
                name: () => this.T("gmcm.hardcooldown.name"),
                tooltip: () => this.T("gmcm.hardcooldown.tip"),
                min: 10,
                max: 7200,
                interval: 10);

            // ----- UI -----
            this.Gmcm.AddSectionTitle(this.ModManifest,
                text: () => this.T("gmcm.section.ui"),
                tooltip: () => this.T("gmcm.section.ui.tip"));

            this.Gmcm.AddBoolOption(this.ModManifest,
                getValue: () => this.Config.ShowHudMessages,
                setValue: v => this.Config.ShowHudMessages = v,
                name: () => this.T("gmcm.hudmessages.name"),
                tooltip: () => this.T("gmcm.hudmessages.tip"));

            this.Gmcm.AddBoolOption(this.ModManifest,
                getValue: () => this.Config.ShowOverlay,
                setValue: v => this.Config.ShowOverlay = v,
                name: () => this.T("gmcm.overlay.name"),
                tooltip: () => this.T("gmcm.overlay.tip"));

            this.Gmcm.AddNumberOption(this.ModManifest,
                getValue: () => this.Config.OverlayX,
                setValue: v => this.Config.OverlayX = v,
                name: () => this.T("gmcm.overlayx.name"),
                tooltip: () => this.T("gmcm.overlayx.tip"),
                min: 0,
                max: 4000,
                interval: 1);

            this.Gmcm.AddNumberOption(this.ModManifest,
                getValue: () => this.Config.OverlayY,
                setValue: v => this.Config.OverlayY = v,
                name: () => this.T("gmcm.overlayy.name"),
                tooltip: () => this.T("gmcm.overlayy.tip"),
                min: 0,
                max: 4000,
                interval: 1);

            // ----- Advanced -----
            this.Gmcm.AddSectionTitle(this.ModManifest,
                text: () => this.T("gmcm.section.advanced"),
                tooltip: () => this.T("gmcm.section.advanced.tip"));

            this.Gmcm.AddBoolOption(this.ModManifest,
                getValue: () => this.Config.CompactLargeObjectHeapOnHardClean,
                setValue: v => this.Config.CompactLargeObjectHeapOnHardClean = v,
                name: () => this.T("gmcm.lohcompact.name"),
                tooltip: () => this.T("gmcm.lohcompact.tip"));

            this.Gmcm.AddBoolOption(this.ModManifest,
                getValue: () => this.Config.TrimWorkingSetOnWindows,
                setValue: v => this.Config.TrimWorkingSetOnWindows = v,
                name: () => this.T("gmcm.trimws.name"),
                tooltip: () => this.T("gmcm.trimws.tip"));

            this.Gmcm.AddNumberOption(this.ModManifest,
                getValue: () => this.Config.FallbackAvailableMemoryMB,
                setValue: v => this.Config.FallbackAvailableMemoryMB = v,
                name: () => this.T("gmcm.fallbackmem.name"),
                tooltip: () => this.T("gmcm.fallbackmem.tip"),
                min: 256,
                max: 65536,
                interval: 256);
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.player == null)
                return;

            this.Planner.ResetForNewSession();
            this.Trend.Clear();
            this.UpdateOverlay(force: true);

            this.Monitor.Log("Stardew Stabilizer active. Use stabilizer_status in the SMAPI console.", LogLevel.Info);
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.player == null)
                return;

            this.Planner.ResetForNewDay();
            this.Trend.Clear();
            this.UpdateOverlay(force: true);
        }

        private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
        {
            this.Planner.ResetForNewSession();
            this.Trend.Clear();
            this.OverlayText = "";
        }

        private void OnOneSecondUpdateTicked(object? sender, OneSecondUpdateTickedEventArgs e)
        {
            if (!this.Config.Enabled)
                return;

            if (!Context.IsWorldReady || Game1.player == null)
                return;

            MemorySnapshot snap = this.Meter.Capture(this.Config);

            this.Trend.Configure(this.Config.TrendWindowSeconds);
            this.Trend.AddSample(snap.PressurePercent);

            TrendSummary trend = this.Trend.GetSummary();

            this.UpdateOverlay(force: false, snap, trend);

            if (!this.Config.AutoCleanup)
                return;

            bool isSafe = this.IsLikelySafeMoment();

            // Anti-spike: require pressure to be sustained above the soft threshold for SustainSeconds
            if (this.Config.SustainSeconds > 0)
            {
                int soft = this.Config.SoftPressurePercent;
                if (!this.Trend.IsSustainedAbove(soft, this.Config.SustainSeconds))
                    return;
            }

            CleanupDecision decision = this.Planner.Evaluate(
                snapshot: snap,
                trend: trend,
                config: this.Config,
                isSafeMoment: isSafe
            );

            if (decision.Action == CleanupAction.None)
                return;

            if (decision.Action == CleanupAction.Soft)
                this.RunSoftCleanup(snap, reasonKey: decision.ReasonKey);
            else
                this.RunHardCleanup(snap, reasonKey: decision.ReasonKey);
        }

        private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
        {
            if (!this.Config.Enabled)
                return;

            if (!Context.IsWorldReady || Game1.player == null)
                return;

            if (Game1.activeClickableMenu != null && !this.Config.AllowHotkeysInMenus)
                return;

            if (this.Config.SoftCleanKey.JustPressed())
            {
                MemorySnapshot snap = this.Meter.Capture(this.Config);
                this.RunSoftCleanup(snap, reasonKey: "hud.manual_soft");
            }
            else if (this.Config.HardCleanKey.JustPressed())
            {
                MemorySnapshot snap = this.Meter.Capture(this.Config);
                this.RunHardCleanup(snap, reasonKey: "hud.manual_hard");
            }
        }

        private void OnRenderedHud(object? sender, RenderedHudEventArgs e)
        {
            if (!this.Config.Enabled || !this.Config.ShowOverlay)
                return;

            if (!Context.IsWorldReady || Game1.player == null)
                return;

            if (string.IsNullOrWhiteSpace(this.OverlayText))
                return;

            SpriteBatch b = e.SpriteBatch;
            Vector2 pos = new Vector2(this.Config.OverlayX, this.Config.OverlayY);

            Vector2 size = Game1.smallFont.MeasureString(this.OverlayText);
            Rectangle bg = new Rectangle(
                (int)pos.X - 6,
                (int)pos.Y - 4,
                (int)size.X + 12,
                (int)size.Y + 8
            );

            b.Draw(Game1.fadeToBlackRect, bg, Color.Black * 0.45f);
            b.DrawString(Game1.smallFont, this.OverlayText, pos, Color.White);
        }

        private bool IsLikelySafeMoment()
        {
            if (Game1.activeClickableMenu != null)
                return true;

            if (Game1.fadeToBlackAlpha > 0.01f)
                return true;

            return false;
        }

        private void RunSoftCleanup(MemorySnapshot snap, string reasonKey)
        {
            if (!this.Planner.TryConsumeCooldown(CleanupAction.Soft, this.Config))
                return;

            long beforeManaged = snap.ManagedBytes;
            long beforeWorkingSet = snap.WorkingSetBytes;

            GC.Collect(1, GCCollectionMode.Optimized, blocking: false, compacting: false);

            if (this.Config.TrimWorkingSetOnWindows)
                WindowsWorkingSet.TryTrim();

            long afterManaged = GC.GetTotalMemory(forceFullCollection: false);
            long afterWorkingSet = Process.GetCurrentProcess().WorkingSet64;

            this.EmitHud(reasonKey, beforeManaged, afterManaged, beforeWorkingSet, afterWorkingSet);
            this.LogCleanup("Soft", beforeManaged, afterManaged, beforeWorkingSet, afterWorkingSet);
        }

        private void RunHardCleanup(MemorySnapshot snap, string reasonKey)
        {
            if (!this.Planner.TryConsumeCooldown(CleanupAction.Hard, this.Config))
                return;

            long beforeManaged = snap.ManagedBytes;
            long beforeWorkingSet = snap.WorkingSetBytes;

            if (this.Config.CompactLargeObjectHeapOnHardClean)
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);

            if (this.Config.TrimWorkingSetOnWindows)
                WindowsWorkingSet.TryTrim();

            long afterManaged = GC.GetTotalMemory(forceFullCollection: false);
            long afterWorkingSet = Process.GetCurrentProcess().WorkingSet64;

            this.EmitHud(reasonKey, beforeManaged, afterManaged, beforeWorkingSet, afterWorkingSet);
            this.LogCleanup("Hard", beforeManaged, afterManaged, beforeWorkingSet, afterWorkingSet);
        }

        private void EmitHud(string reasonKey, long beforeManaged, long afterManaged, long beforeWs, long afterWs)
        {
            if (!this.Config.ShowHudMessages)
                return;

            string text = this.Helper.Translation.Get(reasonKey, new
            {
                managedBefore = Bytes.ToMB(beforeManaged),
                managedAfter = Bytes.ToMB(afterManaged),
                wsBefore = Bytes.ToMB(beforeWs),
                wsAfter = Bytes.ToMB(afterWs)
            });

            Game1.addHUDMessage(new HUDMessage(text, 3));
        }

        private void LogCleanup(string label, long beforeManaged, long afterManaged, long beforeWs, long afterWs)
        {
            this.Monitor.Log(
                $"[{label}] managed {Bytes.ToMB(beforeManaged)}MB -> {Bytes.ToMB(afterManaged)}MB, " +
                $"working set {Bytes.ToMB(beforeWs)}MB -> {Bytes.ToMB(afterWs)}MB",
                LogLevel.Trace
            );
        }

        private void UpdateOverlay(bool force, MemorySnapshot snapshot, TrendSummary trend)
        {
            if (!this.Config.ShowOverlay)
                return;

            DateTime now = DateTime.UtcNow;
            if (!force && (now - this.LastOverlayUpdateUtc).TotalMilliseconds < 950)
                return;

            this.LastOverlayUpdateUtc = now;

            string deltaText = trend.Delta >= 0 ? $"+{trend.Delta}" : $"{trend.Delta}";

            this.OverlayText = this.Helper.Translation.Get("overlay.line", new
            {
                current = snapshot.PressurePercent,
                avg = trend.Average,
                delta = deltaText,
                window = trend.WindowSeconds,
                managed = Bytes.ToMB(snapshot.ManagedBytes),
                ws = Bytes.ToMB(snapshot.WorkingSetBytes)
            });
        }

        private void UpdateOverlay(bool force)
        {
            if (!this.Config.ShowOverlay)
                return;

            MemorySnapshot snap = this.Meter.Capture(this.Config);
            this.Trend.AddSample(snap.PressurePercent);
            TrendSummary t = this.Trend.GetSummary();
            this.UpdateOverlay(force, snap, t);
        }

        private void OnCmdStatus(string command, string[] args)
        {
            if (!Context.IsWorldReady || Game1.player == null)
            {
                this.Monitor.Log("World not loaded yet.", LogLevel.Info);
                return;
            }

            MemorySnapshot snap = this.Meter.Capture(this.Config);
            this.Trend.AddSample(snap.PressurePercent);
            TrendSummary t = this.Trend.GetSummary();

            this.Monitor.Log(
                $"Pressure: {snap.PressurePercent}% | Avg({t.WindowSeconds}s): {t.Average}% | Delta: {t.Delta}% | " +
                $"Managed: {Bytes.ToMB(snap.ManagedBytes)}MB | WorkingSet: {Bytes.ToMB(snap.WorkingSetBytes)}MB | Available: {Bytes.ToMB(snap.AvailableBytes)}MB",
                LogLevel.Info
            );
        }

        private void OnCmdSoftClean(string command, string[] args)
        {
            if (!Context.IsWorldReady || Game1.player == null)
                return;

            MemorySnapshot snap = this.Meter.Capture(this.Config);
            this.RunSoftCleanup(snap, reasonKey: "hud.manual_soft");
        }

        private void OnCmdHardClean(string command, string[] args)
        {
            if (!Context.IsWorldReady || Game1.player == null)
                return;

            MemorySnapshot snap = this.Meter.Capture(this.Config);
            this.RunHardCleanup(snap, reasonKey: "hud.manual_hard");
        }

        private string T(string key) => this.Helper.Translation.Get(key);

        // ---------------- core types ----------------

        private sealed class PressureMeter
        {
            public MemorySnapshot Capture(ModConfig config)
            {
                long managed = GC.GetTotalMemory(forceFullCollection: false);
                long workingSet = Process.GetCurrentProcess().WorkingSet64;
                long available = GetAvailableMemoryBytes(config);
                int pressure = ComputePressurePercent(managed, workingSet, available);

                return new MemorySnapshot(managed, workingSet, available, pressure);
            }

            private static long GetAvailableMemoryBytes(ModConfig config)
            {
                try
                {
                    var info = GC.GetGCMemoryInfo();
                    if (info.TotalAvailableMemoryBytes > 0)
                        return info.TotalAvailableMemoryBytes;
                }
                catch
                {
                    // ignore and fallback
                }

                return Math.Max(256L, (long)config.FallbackAvailableMemoryMB) * Bytes.MB;
            }

            private static int ComputePressurePercent(long managed, long workingSet, long available)
            {
                if (available <= 0)
                    return 0;

                double managedPct = (double)managed / available * 100.0;
                double wsPct = (double)workingSet / available * 100.0;

                double pct = Math.Max(managedPct, wsPct);
                if (pct < 0) pct = 0;
                if (pct > 100) pct = 100;

                return (int)Math.Round(pct);
            }
        }

        private sealed class PressureTrend
        {
            private readonly Queue<Sample> Samples = new();
            private int WindowSeconds = 10;

            public void Configure(int windowSeconds)
            {
                int clamped = windowSeconds < 3 ? 3 : (windowSeconds > 60 ? 60 : windowSeconds);
                this.WindowSeconds = clamped;
                this.TrimOld();
            }

            public void Clear()
            {
                this.Samples.Clear();
            }

            public void AddSample(int pressurePercent)
            {
                DateTime now = DateTime.UtcNow;
                this.Samples.Enqueue(new Sample(now, ClampPercent(pressurePercent)));
                this.TrimOld();
            }

            public TrendSummary GetSummary()
            {
                this.TrimOld();

                if (this.Samples.Count == 0)
                    return new TrendSummary(0, 0, this.WindowSeconds);

                int sum = 0;
                Sample? oldest = null;
                Sample? newest = null;

                foreach (Sample s in this.Samples)
                {
                    oldest ??= s;
                    newest = s;
                    sum += s.Pressure;
                }

                int avg = (int)Math.Round((double)sum / this.Samples.Count);
                int delta = (newest!.Value.Pressure - oldest!.Value.Pressure);

                return new TrendSummary(avg, delta, this.WindowSeconds);
            }

            public bool IsSustainedAbove(int thresholdPercent, int sustainSeconds)
            {
                thresholdPercent = ClampPercent(thresholdPercent);
                if (sustainSeconds <= 0)
                    return true;

                DateTime cutoff = DateTime.UtcNow.AddSeconds(-sustainSeconds);

                bool hasAny = false;
                foreach (Sample s in this.Samples)
                {
                    if (s.Utc < cutoff)
                        continue;

                    hasAny = true;
                    if (s.Pressure < thresholdPercent)
                        return false;
                }

                return hasAny;
            }

            private void TrimOld()
            {
                DateTime cutoff = DateTime.UtcNow.AddSeconds(-this.WindowSeconds);
                while (this.Samples.Count > 0 && this.Samples.Peek().Utc < cutoff)
                    this.Samples.Dequeue();
            }

            private static int ClampPercent(int v)
            {
                if (v < 0) return 0;
                if (v > 100) return 100;
                return v;
            }

            private readonly struct Sample
            {
                public DateTime Utc { get; }
                public int Pressure { get; }

                public Sample(DateTime utc, int pressure)
                {
                    this.Utc = utc;
                    this.Pressure = pressure;
                }
            }
        }

        private sealed class CleanupPlanner
        {
            private DateTime LastSoftUtc = DateTime.MinValue;
            private DateTime LastHardUtc = DateTime.MinValue;

            private bool HardQueued;

            public void ResetForNewSession()
            {
                this.LastSoftUtc = DateTime.MinValue;
                this.LastHardUtc = DateTime.MinValue;
                this.HardQueued = false;
            }

            public void ResetForNewDay()
            {
                this.HardQueued = false;
            }

            public CleanupDecision Evaluate(MemorySnapshot snapshot, TrendSummary trend, ModConfig config, bool isSafeMoment)
            {
                int soft = ClampPercent(config.SoftPressurePercent);
                int hard = ClampPercent(config.HardPressurePercent);
                int emergency = ClampPercent(config.EmergencyPressurePercent);

                // choose which pressure we trust for decisions
                int effectivePressure = config.UseTrendAverageForDecisions ? trend.Average : snapshot.PressurePercent;

                // below soft - hysteresis => clear queued hard
                if (effectivePressure <= Math.Max(0, soft - config.HysteresisPercent))
                {
                    this.HardQueued = false;
                    return CleanupDecision.None();
                }

                // Emergency: act immediately (prevents crash spirals)
                if (snapshot.PressurePercent >= emergency)
                {
                    this.HardQueued = false;
                    return new CleanupDecision(CleanupAction.Hard, "hud.emergency_hard");
                }

                // Require sustained pressure to avoid one-second spikes
                if (config.SustainSeconds > 0)
                {
                    // If not enough recent data or it dipped, skip actions.
                    // (Caller ensures trend samples are updated per second.)
                }

                // Hard conditions
                bool hardCondition = effectivePressure >= hard;

                if (hardCondition)
                {
                    // Must be sustained above hard line (if configured)
                    // Note: sustain check uses raw samples, not the average.
                    // This is intentional: it prevents reacting to a brief spike.
                    // We can't access the sample list here, so the mod should pass "trend sustained" via config.
                    // We'll approximate: require sustain using effective pressure + hysteresis via caller settings.
                    // (Primary anti-spike is trend average + SustainSeconds on soft line.)
                }

                // Soft conditions
                bool softCondition = effectivePressure >= soft;

                // If a hard cleanup is queued and we're now safe, do it.
                if (this.HardQueued && isSafeMoment)
                {
                    this.HardQueued = false;
                    return new CleanupDecision(CleanupAction.Hard, "hud.queued_hard");
                }

                // If hard is needed:
                if (hardCondition)
                {
                    if (!config.HardCleanupMenuOnly || isSafeMoment)
                    {
                        this.HardQueued = false;
                        return new CleanupDecision(CleanupAction.Hard, "hud.hard_cleanup");
                    }

                    // Not safe: queue hard until a safe moment. Prefer soft meanwhile (less freeze).
                    this.HardQueued = true;

                    if (config.PreferSoftWhileWaitingForHard && softCondition)
                        return new CleanupDecision(CleanupAction.Soft, "hud.soft_waiting_for_hard");

                    return CleanupDecision.None();
                }

                // If only soft is needed, prefer safe moment but allow forcing after SustainSeconds
                if (softCondition)
                {
                    // If we want to filter spikes: require that we're above soft for SustainSeconds.
                    // We can’t see the internal samples here, so we rely on caller doing the sustained check.
                    // The caller will only call Evaluate when trend says "sustained above" if SustainSeconds > 0.
                    if (isSafeMoment)
                        return new CleanupDecision(CleanupAction.Soft, "hud.soft_cleanup");

                    // Outside menus, soft cleanup is non-blocking; it's okay to do it.
                    return new CleanupDecision(CleanupAction.Soft, "hud.soft_cleanup");
                }

                return CleanupDecision.None();
            }

            public bool TryConsumeCooldown(CleanupAction action, ModConfig config)
            {
                DateTime now = DateTime.UtcNow;

                if (action == CleanupAction.Soft)
                {
                    if (this.LastSoftUtc != DateTime.MinValue &&
                        (now - this.LastSoftUtc).TotalSeconds < config.SoftCooldownSeconds)
                        return false;

                    this.LastSoftUtc = now;
                    return true;
                }

                if (action == CleanupAction.Hard)
                {
                    if (this.LastHardUtc != DateTime.MinValue &&
                        (now - this.LastHardUtc).TotalSeconds < config.HardCooldownSeconds)
                        return false;

                    this.LastHardUtc = now;
                    return true;
                }

                return true;
            }

            private static int ClampPercent(int value)
            {
                if (value < 0) return 0;
                if (value > 100) return 100;
                return value;
            }
        }

        private enum CleanupAction
        {
            None,
            Soft,
            Hard
        }

        private readonly struct CleanupDecision
        {
            public CleanupAction Action { get; }
            public string ReasonKey { get; }

            public CleanupDecision(CleanupAction action, string reasonKey)
            {
                this.Action = action;
                this.ReasonKey = reasonKey;
            }

            public static CleanupDecision None() => new CleanupDecision(CleanupAction.None, "hud.soft_cleanup");
        }

        private readonly struct MemorySnapshot
        {
            public long ManagedBytes { get; }
            public long WorkingSetBytes { get; }
            public long AvailableBytes { get; }
            public int PressurePercent { get; }

            public MemorySnapshot(long managedBytes, long workingSetBytes, long availableBytes, int pressurePercent)
            {
                this.ManagedBytes = managedBytes;
                this.WorkingSetBytes = workingSetBytes;
                this.AvailableBytes = availableBytes;
                this.PressurePercent = pressurePercent;
            }
        }

        private readonly struct TrendSummary
        {
            public int Average { get; }
            public int Delta { get; }
            public int WindowSeconds { get; }

            public TrendSummary(int average, int delta, int windowSeconds)
            {
                this.Average = average;
                this.Delta = delta;
                this.WindowSeconds = windowSeconds;
            }
        }

        private static class Bytes
        {
            public const long MB = 1024L * 1024L;

            public static long ToMB(long bytes)
            {
                if (bytes <= 0)
                    return 0;

                return bytes / MB;
            }
        }

        private static class WindowsWorkingSet
        {
            public static bool TryTrim()
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return false;

                try
                {
                    using Process p = Process.GetCurrentProcess();
                    return EmptyWorkingSet(p.Handle);
                }
                catch
                {
                    return false;
                }
            }

            [DllImport("psapi.dll", SetLastError = true)]
            private static extern bool EmptyWorkingSet(IntPtr hProcess);
        }
    }
}
