using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Monsters;

namespace RealLifeHolidayDialogue
{
    internal sealed class ModEntry : Mod
    {
        private ModConfig Config = new();

        // Real-life date tracking
        private DateTime _lastRealDate = DateTime.MinValue.Date;
        private HolidayPool? _activeHoliday;

        // Proximity tracking (per location)
        private string _lastLocationName = "";
        private HashSet<string> _npcsInRangeLastTick = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, long> _lastShownTickByNpc = new(StringComparer.OrdinalIgnoreCase);

        // RNG
        private readonly Random _rng = new();

        // Reflection cache for Character.showTextAboveHead
        private MethodInfo? _showTextAboveHead_5;
        private MethodInfo? _showTextAboveHead_4;
        private MethodInfo? _showTextAboveHead_3;
        private MethodInfo? _showTextAboveHead_2;

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;

            this.CacheShowTextAboveHeadMethods();
            this.RefreshActiveHoliday(force: true);
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            this.RefreshActiveHoliday(force: true);
            this.ResetProximityState();
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            // Real-life date might not change with in-game days, but this is a safe refresh point
            this.RefreshActiveHoliday(force: false);
            this.ResetProximityState();
        }

        private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
        {
            this._activeHoliday = null;
            this._lastRealDate = DateTime.MinValue.Date;
            this._lastLocationName = "";
            this._npcsInRangeLastTick.Clear();
            this._lastShownTickByNpc.Clear();
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            if (!this.Config.EnableMod)
                return;

            if (Game1.player is null || Game1.currentLocation is null)
                return;

            if (Game1.eventUp)
                return;

            // Refresh active holiday if real-life date changed (e.g., played past midnight)
            this.RefreshActiveHoliday(force: false);
            if (this._activeHoliday is null || this._activeHoliday.Dialogues.Count == 0)
                return;

            // Run a bit less often
            if (!e.IsMultipleOf(10))
                return;

            // Location change handling
            string locName = Game1.currentLocation.NameOrUniqueName ?? "";
            if (!string.Equals(locName, this._lastLocationName, StringComparison.OrdinalIgnoreCase))
            {
                this._lastLocationName = locName;
                this._npcsInRangeLastTick.Clear();
            }

            int radius = Math.Max(1, this.Config.TriggerRadiusTiles);
            int radiusSq = radius * radius;

            long nowTick = GetGameTickSafe();
            long cooldownTicks = Math.Max(0, (long)this.Config.CooldownSeconds * 60L);

            // Stardew 1.6+: use TilePoint (getTileLocation was removed)
            var playerTile = Game1.player.TilePoint;
            var inRangeNow = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var npc in Game1.currentLocation.characters.OfType<NPC>())
            {
                if (!this.IsValidTargetNpc(npc))
                    continue;

                string key = npc.Name ?? npc.displayName ?? "NPC";

                var npcTile = npc.TilePoint;
                int dx = playerTile.X - npcTile.X;
                int dy = playerTile.Y - npcTile.Y;
                int distSq = dx * dx + dy * dy;

                if (distSq > radiusSq)
                    continue;

                inRangeNow.Add(key);

                // Only trigger when the player *enters* range (feels like "passing by")
                if (this._npcsInRangeLastTick.Contains(key))
                    continue;

                // Cooldown
                if (cooldownTicks > 0 && this._lastShownTickByNpc.TryGetValue(key, out long lastTick))
                {
                    if (nowTick - lastTick < cooldownTicks)
                        continue;
                }

                // Pick and show dialogue
                string line = this.PickDialogueLine(this._activeHoliday, npc);
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                this.ShowTextBubble(npc, line, durationMs: this.Config.BubbleDurationMs, style: this.Config.BubbleStyle, preTimerMs: 0);
                this._lastShownTickByNpc[key] = nowTick;
            }

            this._npcsInRangeLastTick = inRangeNow;
        }

        private void RefreshActiveHoliday(bool force)
        {
            DateTime today = DateTime.Today;

            if (!force && today == this._lastRealDate)
                return;

            this._lastRealDate = today;

            int month = today.Month;
            int day = today.Day;

            var pool = new List<string>();
            foreach (var h in this.Config.Holidays ?? new List<HolidayDefinition>())
            {
                if (h is null || !h.Enabled)
                    continue;

                if (!TryParseMonthDay(h.Date, out int hm, out int hd))
                    continue;

                if (hm == month && hd == day && h.Dialogues is not null && h.Dialogues.Count > 0)
                    pool.AddRange(h.Dialogues.Where(p => !string.IsNullOrWhiteSpace(p)));
            }

            this._activeHoliday = pool.Count > 0
                ? new HolidayPool(month, day, pool)
                : null;

            // Avoid weird “already in range” carryover when the date flips
            this.ResetProximityState();
        }

        private void ResetProximityState()
        {
            this._npcsInRangeLastTick.Clear();
            // Don't clear _lastShownTickByNpc; cooldown can persist across warps
        }

        private bool IsValidTargetNpc(NPC npc)
        {
            if (npc is null)
                return false;

            // Avoid monsters saying holiday greetings in mines etc.
            if (npc is Monster)
                return false;

            if (npc.IsInvisible)
                return false;

            return true;
        }

        private string PickDialogueLine(HolidayPool holiday, NPC npc)
        {
            if (holiday.Dialogues.Count == 0)
                return "";

            string raw = holiday.Dialogues[this._rng.Next(holiday.Dialogues.Count)];

            string farmerName = Game1.player?.Name ?? "Farmer";
            string npcName = npc.displayName ?? npc.Name ?? "Friend";

            return raw
                .Replace("{player}", farmerName, StringComparison.OrdinalIgnoreCase)
                .Replace("{farmer}", farmerName, StringComparison.OrdinalIgnoreCase)
                .Replace("{npc}", npcName, StringComparison.OrdinalIgnoreCase);
        }

        private void CacheShowTextAboveHeadMethods()
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var methods = typeof(Character).GetMethods(flags)
                .Where(m => m.Name == "showTextAboveHead")
                .ToArray();

            this._showTextAboveHead_5 = methods.FirstOrDefault(m =>
            {
                var p = m.GetParameters();
                return p.Length == 5
                    && p[0].ParameterType == typeof(string)
                    && p[1].ParameterType == typeof(int)
                    && p[2].ParameterType == typeof(int)
                    && p[3].ParameterType == typeof(int)
                    && p[4].ParameterType == typeof(int);
            });

            this._showTextAboveHead_4 = methods.FirstOrDefault(m =>
            {
                var p = m.GetParameters();
                return p.Length == 4
                    && p[0].ParameterType == typeof(string)
                    && p[1].ParameterType == typeof(int)
                    && p[2].ParameterType == typeof(int)
                    && p[3].ParameterType == typeof(int);
            });

            this._showTextAboveHead_3 = methods.FirstOrDefault(m =>
            {
                var p = m.GetParameters();
                return p.Length == 3
                    && p[0].ParameterType == typeof(string)
                    && p[1].ParameterType == typeof(int)
                    && p[2].ParameterType == typeof(int);
            });

            this._showTextAboveHead_2 = methods.FirstOrDefault(m =>
            {
                var p = m.GetParameters();
                return p.Length == 2
                    && p[0].ParameterType == typeof(string)
                    && p[1].ParameterType == typeof(int);
            });
        }

        private void ShowTextBubble(Character target, string text, int durationMs, int style, int preTimerMs)
        {
            if (target is null || string.IsNullOrWhiteSpace(text))
                return;

            durationMs = Math.Clamp(durationMs, 250, 20000);
            preTimerMs = Math.Clamp(preTimerMs, 0, 20000);

            // color param: -1 (game default)
            const int color = -1;

            try
            {
                if (this._showTextAboveHead_5 is not null)
                {
                    // (text, color, style, durationMs, preTimerMs)
                    this._showTextAboveHead_5.Invoke(target, new object[] { text, color, style, durationMs, preTimerMs });
                    return;
                }

                if (this._showTextAboveHead_4 is not null)
                {
                    // (text, color, style, durationMs)
                    this._showTextAboveHead_4.Invoke(target, new object[] { text, color, style, durationMs });
                    return;
                }

                if (this._showTextAboveHead_3 is not null)
                {
                    // fallback: (text, style, durationMs)
                    this._showTextAboveHead_3.Invoke(target, new object[] { text, style, durationMs });
                    return;
                }

                if (this._showTextAboveHead_2 is not null)
                {
                    // fallback: (text, durationMs)
                    this._showTextAboveHead_2.Invoke(target, new object[] { text, durationMs });
                    return;
                }

                this.Monitor.Log("Couldn't find Character.showTextAboveHead(...) via reflection; no bubbles will be shown.", LogLevel.Warn);
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Failed to show text bubble: {ex}", LogLevel.Trace);
            }
        }

        private static bool TryParseMonthDay(string? input, out int month, out int day)
        {
            month = 0;
            day = 0;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            // Accept: "12/25", "12-25", "12.25", "1/1", "01/01"
            char[] seps = new[] { '/', '-', '.', ' ' };
            string[] parts = input.Trim().Split(seps, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
                return false;

            if (!int.TryParse(parts[0], out month) || !int.TryParse(parts[1], out day))
                return false;

            if (month < 1 || month > 12)
                return false;

            int maxDay = DateTime.DaysInMonth(2024, month); // leap-year safe for Feb
            if (day < 1 || day > maxDay)
                return false;

            return true;
        }

        private static long GetGameTickSafe()
        {
            try
            {
                return Game1.ticks;
            }
            catch
            {
                return (long)(Game1.currentGameTime?.TotalGameTime.TotalSeconds * 60.0 ?? 0);
            }
        }

        private sealed record HolidayPool(int Month, int Day, List<string> Dialogues);
    }
}
