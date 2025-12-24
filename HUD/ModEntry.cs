using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;

namespace PerformanceHud
{
    internal sealed class ModEntry : Mod
    {
        private ModConfig Config = null!;
        private bool OverlayVisible = true;

        // frame stats
        private readonly Stopwatch FrameStopwatch = Stopwatch.StartNew();
        private double LastFrameMs;
        private double AvgFrameMs;
        private int FramesThisSecond;
        private double FpsTimerMs;
        private double CurrentFps;

        // update load estimate (tick timing)
        private readonly Stopwatch TickStopwatch = Stopwatch.StartNew();
        private readonly Queue<double> TickSamplesMs = new();
        private double TickSamplesSum;
        private double TickSamplesMax;
        private const int MaxTickSamples = 120;

        // sampled world stats
        private int SampleTickCounter;
        private const int SampleEveryTicks = 15; // ~0.25s @ 60fps

        private string LastLocationId = "(none)";
        private int TempSpritesCount;

        // memory
        private long WorkingSetBytes;

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.Input.ButtonsChanged += this.OnButtonsChanged;

            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.Display.RenderedHud += this.OnRenderedHud;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            this.RegisterGmcm();
        }

        private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.player is null)
                return;

            if (!this.Config.Enabled)
                return;

            if (this.Config.ToggleOverlayKey is not null && this.Config.ToggleOverlayKey.IsBound && this.Config.ToggleOverlayKey.JustPressed())
                this.OverlayVisible = !this.OverlayVisible;
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            // Update load estimate (tick timing)
            double tickDt = this.TickStopwatch.Elapsed.TotalMilliseconds;
            this.TickStopwatch.Restart();
            this.AddTickSample(tickDt);

            if (!Context.IsWorldReady || Game1.player is null)
                return;

            if (!this.Config.Enabled)
                return;

            // Sample heavier stats periodically
            this.SampleTickCounter++;
            if (this.SampleTickCounter % SampleEveryTicks != 0)
                return;

            GameLocation? loc = Game1.currentLocation;
            if (loc is null)
                return;

            // Location ID
            this.LastLocationId = GetLocationId(loc);

            // Active animations / temporary sprites
            this.TempSpritesCount = TryGetCountFromMember(loc, "temporarySprites", "TemporarySprites");

            // Memory (working set)
            try { this.WorkingSetBytes = Process.GetCurrentProcess().WorkingSet64; }
            catch { this.WorkingSetBytes = 0; }
        }

        private void OnRenderedHud(object? sender, RenderedHudEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.player is null)
                return;

            // Frame stats update each draw
            this.UpdateFrameStats();

            if (!this.Config.Enabled || !this.OverlayVisible)
                return;

            string text = this.BuildHudText();
            if (string.IsNullOrWhiteSpace(text))
                return;

            SpriteFont font = Game1.smallFont;
            Vector2 textSize = font.MeasureString(text);

            const int pad = 6;
            int boxW = (int)textSize.X + pad * 2;
            int boxH = (int)textSize.Y + pad * 2;

            int vw = Game1.uiViewport.Width;
            int vh = Game1.uiViewport.Height;

            int x = Math.Clamp(this.Config.PositionX, 0, Math.Max(0, vw - boxW));
            int y = Math.Clamp(this.Config.PositionY, 0, Math.Max(0, vh - boxH));

            var bgRect = new Rectangle(x, y, boxW, boxH);
            var textPos = new Vector2(x + pad, y + pad);

            if (this.Config.DrawBackground)
                Game1.spriteBatch.Draw(Game1.fadeToBlackRect, bgRect, Color.Black * 0.55f);

            Game1.spriteBatch.DrawString(font, text, textPos + new Vector2(1f, 1f), Color.Black * 0.8f);
            Game1.spriteBatch.DrawString(font, text, textPos, Color.White);
        }

        private string BuildHudText()
        {
            var lines = new List<string>();

            // Performance
            if (this.Config.ShowFps)
                lines.Add($"FPS: {this.CurrentFps:0}");

            if (this.Config.ShowFrameTime)
                lines.Add($"Frame: {this.LastFrameMs:0.0} ms (avg {this.AvgFrameMs:0.0} ms)");

            if (this.Config.ShowMemory)
                lines.Add($"Memory: {FormatBytes(this.WorkingSetBytes)}");

            if (this.Config.ShowUpdateLoadEstimate)
            {
                (double avg, double max) = this.GetTickStats();
                lines.Add($"Update dt: {avg:0.0} ms (max {max:0.0} ms)");
            }

            // Location
            if (this.Config.ShowCurrentLocationId)
                lines.Add($"Location: {this.LastLocationId}");

            // Active animations / temporary sprites
            if (this.Config.ShowTemporarySprites)
                lines.Add($"Temp sprites: {this.TempSpritesCount}");

            // Debug QoL
            if (this.Config.ShowPlayerTile)
            {
                Vector2 tile = GetPlayerTile(Game1.player);
                lines.Add($"Player tile: {(int)tile.X}, {(int)tile.Y}");
            }

            if (this.Config.ShowPlayerFacing)
                lines.Add($"Facing: {FacingToString(Game1.player.FacingDirection)}");

            if (this.Config.ShowInGameDateTime)
            {
                string season = Game1.currentSeason ?? "?";
                int day = Game1.dayOfMonth;
                int year = Game1.year;
                lines.Add($"Date/Time: {season} {day} Y{year}  {FormatTime(Game1.timeOfDay)}");
            }

            if (this.Config.ShowWeather)
                lines.Add($"Weather: {GetWeatherString()}");

            if (this.Config.ShowMultiplayerInfo)
            {
                string mode = Context.IsMultiplayer
                    ? (Context.IsMainPlayer ? "Host" : "Farmhand")
                    : "Single";

                int players = GetPlayerCountSafe();
                lines.Add($"Multiplayer: {mode} (players {players})");
            }

            return string.Join("\n", lines);
        }

        // -----------------
        // Frame + tick stats
        // -----------------
        private void UpdateFrameStats()
        {
            double dt = this.FrameStopwatch.Elapsed.TotalMilliseconds;
            this.FrameStopwatch.Restart();

            if (dt <= 0 || dt > 1000)
                return;

            this.LastFrameMs = dt;
            this.AvgFrameMs = this.AvgFrameMs <= 0 ? dt : (this.AvgFrameMs * 0.90) + (dt * 0.10);

            this.FramesThisSecond++;
            this.FpsTimerMs += dt;

            if (this.FpsTimerMs >= 1000.0)
            {
                this.CurrentFps = this.FramesThisSecond * (1000.0 / this.FpsTimerMs);
                this.FramesThisSecond = 0;
                this.FpsTimerMs = 0;
            }
        }

        private void AddTickSample(double ms)
        {
            this.TickSamplesMs.Enqueue(ms);
            this.TickSamplesSum += ms;
            if (ms > this.TickSamplesMax)
                this.TickSamplesMax = ms;

            if (this.TickSamplesMs.Count > MaxTickSamples)
            {
                double removed = this.TickSamplesMs.Dequeue();
                this.TickSamplesSum -= removed;

                if (removed >= this.TickSamplesMax - 0.0001)
                    this.TickSamplesMax = this.TickSamplesMs.Count > 0 ? this.TickSamplesMs.Max() : 0;
            }
        }

        private (double avg, double max) GetTickStats()
        {
            if (this.TickSamplesMs.Count == 0)
                return (0, 0);

            double avg = this.TickSamplesSum / this.TickSamplesMs.Count;
            return (avg, this.TickSamplesMax);
        }

        // -----------------
        // GMCM
        // -----------------
        private void RegisterGmcm()
        {
            // IGenericModConfigMenuApi should be in its own file (as you added earlier)
            var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm is null)
                return;

            gmcm.Register(
                mod: this.ModManifest,
                reset: () => this.Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.Config)
            );

            // General
            gmcm.AddSectionTitle(this.ModManifest, () => T("gmcm.section.general"));
            gmcm.AddBoolOption(this.ModManifest, () => this.Config.Enabled, v => this.Config.Enabled = v, () => T("gmcm.enabled"));
            gmcm.AddBoolOption(this.ModManifest, () => this.Config.DrawBackground, v => this.Config.DrawBackground = v, () => T("gmcm.drawBackground"));
            gmcm.AddKeybindList(this.ModManifest, () => this.Config.ToggleOverlayKey, v => this.Config.ToggleOverlayKey = v, () => T("gmcm.toggleKey"));
            gmcm.AddNumberOption(this.ModManifest, () => this.Config.PositionX, v => this.Config.PositionX = v, () => T("gmcm.posX"), min: 0, max: 10000, interval: 1);
            gmcm.AddNumberOption(this.ModManifest, () => this.Config.PositionY, v => this.Config.PositionY = v, () => T("gmcm.posY"), min: 0, max: 10000, interval: 1);

            // Performance
            gmcm.AddSectionTitle(this.ModManifest, () => T("gmcm.section.performance"));
            gmcm.AddBoolOption(this.ModManifest, () => this.Config.ShowFps, v => this.Config.ShowFps = v, () => T("gmcm.showFps"));
            gmcm.AddBoolOption(this.ModManifest, () => this.Config.ShowFrameTime, v => this.Config.ShowFrameTime = v, () => T("gmcm.showFrameTime"));
            gmcm.AddBoolOption(this.ModManifest, () => this.Config.ShowMemory, v => this.Config.ShowMemory = v, () => T("gmcm.showMemory"));
            gmcm.AddBoolOption(this.ModManifest, () => this.Config.ShowUpdateLoadEstimate, v => this.Config.ShowUpdateLoadEstimate = v, () => T("gmcm.showUpdateLoad"));

            // Location
            gmcm.AddSectionTitle(this.ModManifest, () => T("gmcm.section.location"));
            gmcm.AddBoolOption(this.ModManifest, () => this.Config.ShowCurrentLocationId, v => this.Config.ShowCurrentLocationId = v, () => T("gmcm.showLocationId"));

            // Animations
            gmcm.AddSectionTitle(this.ModManifest, () => T("gmcm.section.animations"));
            gmcm.AddBoolOption(this.ModManifest, () => this.Config.ShowTemporarySprites, v => this.Config.ShowTemporarySprites = v, () => T("gmcm.showTempSprites"));

            // Debug QoL
            gmcm.AddSectionTitle(this.ModManifest, () => T("gmcm.section.debug"));
            gmcm.AddBoolOption(this.ModManifest, () => this.Config.ShowPlayerTile, v => this.Config.ShowPlayerTile = v, () => T("gmcm.showPlayerTile"));
            gmcm.AddBoolOption(this.ModManifest, () => this.Config.ShowPlayerFacing, v => this.Config.ShowPlayerFacing = v, () => T("gmcm.showPlayerFacing"));
            gmcm.AddBoolOption(this.ModManifest, () => this.Config.ShowInGameDateTime, v => this.Config.ShowInGameDateTime = v, () => T("gmcm.showDateTime"));
            gmcm.AddBoolOption(this.ModManifest, () => this.Config.ShowWeather, v => this.Config.ShowWeather = v, () => T("gmcm.showWeather"));
            gmcm.AddBoolOption(this.ModManifest, () => this.Config.ShowMultiplayerInfo, v => this.Config.ShowMultiplayerInfo = v, () => T("gmcm.showMultiplayer"));
        }

        private string T(string key) => this.Helper.Translation.Get(key);

        // -----------------
        // Multiplayer helper
        // -----------------
        private static int GetPlayerCountSafe()
        {
            try
            {
                var online = Game1.getOnlineFarmers();
                if (online is not null)
                    return online.Count;
            }
            catch { }

            try
            {
                var all = Game1.getAllFarmers();
                if (all is not null)
                    return all.Count();
            }
            catch { }

            return 1;
        }

        // -----------------
        // Reflection helpers
        // -----------------
        private static object? GetMemberValue(object instance, params string[] names)
        {
            Type t = instance.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (string name in names)
            {
                try
                {
                    FieldInfo? field = t.GetField(name, flags);
                    if (field is not null)
                        return field.GetValue(instance);

                    PropertyInfo? prop = t.GetProperty(name, flags);
                    if (prop is not null && prop.GetIndexParameters().Length == 0)
                        return prop.GetValue(instance);

                    MethodInfo? method = t.GetMethod(name, flags, binder: null, types: Type.EmptyTypes, modifiers: null);
                    if (method is not null)
                        return method.Invoke(instance, null);
                }
                catch { }
            }

            return null;
        }

        private static int TryGetCountFromMember(object instance, params string[] memberNames)
        {
            object? value = GetMemberValue(instance, memberNames);
            if (value is null)
                return 0;

            return TryGetCount(value);
        }

        private static int TryGetCount(object collectionLike)
        {
            try
            {
                if (collectionLike is ICollection coll)
                    return coll.Count;

                PropertyInfo? countProp = collectionLike.GetType().GetProperty("Count", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (countProp?.GetIndexParameters().Length == 0 && countProp.PropertyType == typeof(int))
                    return (int)(countProp.GetValue(collectionLike) ?? 0);
            }
            catch { }

            int count = 0;
            if (collectionLike is IEnumerable enumerable)
            {
                foreach (object? _ in enumerable)
                    count++;
            }
            return count;
        }

        private static string GetLocationId(GameLocation loc)
        {
            object? val =
                GetMemberValue(loc, "NameOrUniqueName")
                ?? GetMemberValue(loc, "UniqueName", "uniqueName")
                ?? GetMemberValue(loc, "Name", "name");

            return val?.ToString() ?? "(unknown)";
        }

        private static Vector2 GetPlayerTile(Farmer farmer)
        {
            object? tile = GetMemberValue(farmer, "Tile", "tile");
            if (tile is Vector2 v2)
                return v2;

            object? tilePoint = GetMemberValue(farmer, "TilePoint");
            if (tilePoint is Point p)
                return new Vector2(p.X, p.Y);

            object? methodResult = GetMemberValue(farmer, "getTileLocation", "GetTileLocation");
            if (methodResult is Vector2 mv2)
                return mv2;

            return farmer.Position / 64f;
        }

        // -----------------
        // Small utilities
        // -----------------
        private static string FacingToString(int dir) => dir switch
        {
            0 => "Up",
            1 => "Right",
            2 => "Down",
            3 => "Left",
            _ => dir.ToString()
        };

        private static string GetWeatherString()
        {
            if (Game1.isLightning) return "Storm";
            if (Game1.isSnowing) return "Snow";
            if (Game1.isRaining) return "Rain";
            if (Game1.isDebrisWeather) return "Wind";
            return "Sun";
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "0 B";

            string[] suffix = { "B", "KB", "MB", "GB", "TB" };
            double v = bytes;
            int i = 0;

            while (v >= 1024 && i < suffix.Length - 1)
            {
                v /= 1024;
                i++;
            }

            return $"{v:0.##} {suffix[i]}";
        }

        private static string FormatTime(int timeOfDay)
        {
            int hours24 = timeOfDay / 100;
            int minutes = timeOfDay % 100;

            string ampm = hours24 >= 12 ? "PM" : "AM";
            int hours12 = hours24 % 12;
            if (hours12 == 0) hours12 = 12;

            return $"{hours12}:{minutes:00} {ampm}";
        }
    }
}
