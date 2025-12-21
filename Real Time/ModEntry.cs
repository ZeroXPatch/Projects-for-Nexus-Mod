using System;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Locations;
using xTile.Dimensions;

namespace RealTimeValley;

public class ModEntry : Mod
{
    private ModConfig Config = new();

    private DateTime _lastSyncCheck = DateTime.MinValue;
    private DateTime _lastNpcFix = DateTime.MinValue;
    private DateTime _lastErrorLog = DateTime.MinValue;

    private bool _pendingInitialSync;
    private bool _initialSyncDone;
    private bool _wasPaused;

    private int _lastTenMinuteMarkApplied = -1;

    public override void Entry(IModHelper helper)
    {
        Config = helper.ReadConfig<ModConfig>();

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;

        helper.Events.GameLoop.OneSecondUpdateTicking += OnOneSecondUpdate;
        helper.Events.GameLoop.UpdateTicking += OnUpdateTicking;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        if (Config.ShowCompatibilityWarnings)
            CheckCompatibilityWarnings();

        var api = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (api is null)
            return;

        api.Register(ModManifest, ResetConfig, SaveConfig);

        api.AddSectionTitle(ModManifest, () => "Real-Time Valley");

        api.AddBoolOption(ModManifest, () => Config.Enable, v => Config.Enable = v, () => "Enable");
        api.AddBoolOption(ModManifest, () => Config.MinuteAccurateClock, v => Config.MinuteAccurateClock = v,
            () => "Minute-accurate clock",
            () => "Freeze vanilla time and set in-game time to your real clock minute-by-minute.");

        api.AddNumberOption(ModManifest, () => Config.SyncIntervalSeconds, v => Config.SyncIntervalSeconds = Math.Max(1, v),
            () => "Sync Interval (seconds)");

        api.AddSectionTitle(ModManifest, () => "World Catch-up");
        api.AddBoolOption(ModManifest, () => Config.RunWorldCatchupOnLoad, v => Config.RunWorldCatchupOnLoad = v,
            () => "Catch-up on load",
            () => "Simulates 10-minute updates to make shops/NPC schedules correct after time jumps.");
        api.AddBoolOption(ModManifest, () => Config.RunWorldCatchupOnLargeForwardJump, v => Config.RunWorldCatchupOnLargeForwardJump = v,
            () => "Catch-up on large forward jump");
        api.AddNumberOption(ModManifest, () => Config.LargeForwardJumpMinutes, v => Config.LargeForwardJumpMinutes = Math.Max(10, v),
            () => "Large jump threshold (minutes)");
        api.AddNumberOption(ModManifest, () => Config.MaxWorldCatchupTenMinuteSteps, v => Config.MaxWorldCatchupTenMinuteSteps = Math.Max(1, v),
            () => "Max catch-up steps (10-min)");

        api.AddSectionTitle(ModManifest, () => "Festivals (fixes empty festival maps)");
        api.AddBoolOption(ModManifest, () => Config.FestivalFriendlyMode, v => Config.FestivalFriendlyMode = v,
            () => "Festival-friendly mode",
            () => "Avoids syncing you past the festival window so festivals actually spawn.");
        api.AddBoolOption(ModManifest, () => Config.HoldTimeInsideFestivalWindow, v => Config.HoldTimeInsideFestivalWindow = v,
            () => "Hold time inside festival window");
        api.AddNumberOption(ModManifest, () => Config.FestivalHoldTimeDay, v => Config.FestivalHoldTimeDay = Math.Clamp(v, 600, 2600),
            () => "Festival hold time (day)");
        api.AddNumberOption(ModManifest, () => Config.FestivalHoldTimeNight, v => Config.FestivalHoldTimeNight = Math.Clamp(v, 1800, 2600),
            () => "Festival hold time (night)");

        api.AddSectionTitle(ModManifest, () => "NPC Fix");
        api.AddBoolOption(ModManifest, () => Config.NPCFixOnLoad, v => Config.NPCFixOnLoad = v, () => "NPC Fix On Load");
        api.AddBoolOption(ModManifest, () => Config.NPCFixAfterWorldCatchup, v => Config.NPCFixAfterWorldCatchup = v,
            () => "NPC Fix After Catch-up");
        api.AddNumberOption(ModManifest, () => Config.NpcFixCooldownSeconds, v => Config.NpcFixCooldownSeconds = Math.Max(0, v),
            () => "NPC Fix Cooldown (seconds)");

        api.AddTextOption(
            ModManifest,
            () => Config.NPCWarpAggressiveness.ToString(),
            v => Config.NPCWarpAggressiveness = Enum.TryParse<NPCWarpAggressiveness>(v, out var a) ? a : Config.NPCWarpAggressiveness,
            () => "NPC Warp Aggressiveness",
            () => "Aggressive may nudge sleeping NPCs slightly off tiles to unstick them.",
            Enum.GetNames(typeof(NPCWarpAggressiveness))
        );

        api.AddSectionTitle(ModManifest, () => "Debug");
        api.AddBoolOption(ModManifest, () => Config.PauseWhenPaused, v => Config.PauseWhenPaused = v, () => "Pause When Game Paused");
        api.AddBoolOption(ModManifest, () => Config.UseShouldTimePassCheck, v => Config.UseShouldTimePassCheck = v, () => "Use shouldTimePass()");
        api.AddBoolOption(ModManifest, () => Config.DebugLogging, v => Config.DebugLogging = v, () => "Debug Logging");
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        _pendingInitialSync = true;
        _initialSyncDone = false;
        _lastSyncCheck = DateTime.MinValue;
        _lastTenMinuteMarkApplied = -1;
    }

    /// <summary>Freeze vanilla time in minute-accurate mode.</summary>
    private void OnUpdateTicking(object? sender, UpdateTickingEventArgs e)
    {
        if (!Context.IsWorldReady || !Config.Enable || !Config.MinuteAccurateClock)
            return;

        if (IsHardTimeLocked())
            return;

        if (Config.PauseWhenPaused && IsGamePaused())
            return;

        try { Game1.gameTimeInterval = 0; }
        catch { /* drift may return if field changes */ }
    }

    private void OnOneSecondUpdate(object? sender, OneSecondUpdateTickingEventArgs e)
    {
        try
        {
            if (!Context.IsWorldReady || !Config.Enable)
                return;

            var now = DateTime.Now;
            if ((now - _lastSyncCheck).TotalSeconds < Math.Max(1, Config.SyncIntervalSeconds))
                return;

            _lastSyncCheck = now;

            bool paused = IsGamePaused();
            if (Config.PauseWhenPaused && paused)
            {
                _wasPaused = true;
                return;
            }

            bool justUnpaused = _wasPaused && !paused;
            _wasPaused = paused;

            if (IsHardTimeLocked())
                return;

            if (_pendingInitialSync && !_initialSyncDone)
            {
                ApplyInitialSync(now);
                return;
            }

            if (Config.MinuteAccurateClock)
                ApplyMinuteAccurateTime(now, justUnpaused);
        }
        catch (Exception ex)
        {
            if ((DateTime.UtcNow - _lastErrorLog).TotalSeconds > 10)
            {
                _lastErrorLog = DateTime.UtcNow;
                Monitor.Log($"Error during time sync: {ex}", LogLevel.Error);
            }
        }
    }

    private void ApplyInitialSync(DateTime now)
    {
        int target = GetTargetTimeOfDayExact(now);
        target = ApplyFestivalRules(target);
        target = ClampTime(target);

        int current = ClampTime(Game1.timeOfDay);

        _lastTenMinuteMarkApplied = TimeToMinutes(current) / 10;

        // Catch up world logic on load (unless we're mid-event)
        if (Config.RunWorldCatchupOnLoad && !IsHardTimeLocked())
        {
            int curMin = TimeToMinutes(current);
            int tgtMin = TimeToMinutes(target);

            if (tgtMin > curMin)
                WorldCatchUpForward(current, target);
        }

        SetTimeExact(target);

        // If you're already on the festival map, try to kick the festival event to start.
        TryKickFestivalEvent();

        if (Config.NPCFixOnLoad)
            RunNpcCatchUp();

        _initialSyncDone = true;
        _pendingInitialSync = false;
    }

    private void ApplyMinuteAccurateTime(DateTime now, bool justUnpaused)
    {
        int target = GetTargetTimeOfDayExact(now);

        // Festivals: stop syncing you past the entry window before you've attended
        target = ApplyFestivalRules(target);

        // Night clamp
        if (Config.ClampAtNight && Config.PauseSyncAtMaxNight)
        {
            int max = Math.Max(Config.ClampMinTime, Config.ClampMaxTime);
            int t = ClampTime(target);

            if (t >= max && now.Hour < Config.ResumeSyncHour)
            {
                SetTimeExact(max);
                return;
            }
        }

        int clampedTarget = ClampTime(target);
        int current = ClampTime(Game1.timeOfDay);

        if (current == clampedTarget)
            return;

        int currentMin = TimeToMinutes(current);
        int targetMin = TimeToMinutes(clampedTarget);
        int absDrift = Math.Abs(targetMin - currentMin);

        // Large forward jump during play: simulate missing 10-min updates
        if (Config.RunWorldCatchupOnLargeForwardJump
            && targetMin > currentMin
            && absDrift >= Config.LargeForwardJumpMinutes)
        {
            WorldCatchUpForward(current, clampedTarget);
            current = ClampTime(Game1.timeOfDay);
            currentMin = TimeToMinutes(current);
        }

        // Run 10-minute updates for boundaries we cross (forward)
        ApplyTenMinuteBoundaryUpdates(currentMin, targetMin);

        // Set exact minute time
        SetTimeExact(clampedTarget);

        // If on festival map and festival should be active, try to start it
        TryKickFestivalEvent();

        if (Config.NPCFixAfterWorldCatchup && absDrift >= Config.LargeForwardJumpMinutes)
            RunNpcCatchUp();
    }

    /// <summary>
    /// Festival fix: if it's a festival day and you're about to sync outside the festival window,
    /// hold time inside the window so the festival can spawn when you enter the area.
    /// </summary>
    private int ApplyFestivalRules(int targetTime)
    {
        if (!Config.FestivalFriendlyMode || !Config.HoldTimeInsideFestivalWindow)
            return targetTime;

        if (!Game1.isFestival())
            return targetTime;

        // If the festival event is already running, do nothing.
        if (Game1.eventUp || Game1.CurrentEvent is not null)
            return targetTime;

        // Determine typical festival time window (simple heuristic that covers most cases)
        GetFestivalWindow(out int start, out int end, out bool nightFestival);

        int t = targetTime;

        // If target outside window, hold inside window so festival can trigger
        if (TimeToMinutes(t) < TimeToMinutes(start) || TimeToMinutes(t) > TimeToMinutes(end))
        {
            int hold = nightFestival ? Config.FestivalHoldTimeNight : Config.FestivalHoldTimeDay;

            // Keep hold inside window just in case user sets something weird
            if (TimeToMinutes(hold) < TimeToMinutes(start)) hold = start;
            if (TimeToMinutes(hold) > TimeToMinutes(end)) hold = end;

            if (Config.DebugLogging)
                Monitor.Log($"Festival day detected. Holding time at {hold} (window {start}-{end}) so festival can spawn.", LogLevel.Debug);

            return hold;
        }

        return t;
    }

    /// <summary>
    /// Very small heuristic for festival windows.
    /// Most festivals: 0900-1400
    /// Night Market: 1700-2600 (Winter 15-17)
    /// Moonlight Jellies (Summer 28): night
    /// Spirit's Eve (Fall 27): night
    /// </summary>
    private void GetFestivalWindow(out int start, out int end, out bool nightFestival)
    {
        nightFestival = false;
        start = 900;
        end = 1400;

        string s = Game1.currentSeason;
        int d = Game1.dayOfMonth;

        // Night Market
        if (s == "winter" && d is >= 15 and <= 17)
        {
            start = 1700;
            end = 2600;
            nightFestival = true;
            return;
        }

        // Night festivals
        if (s == "summer" && d == 28) // Dance of the Moonlight Jellies
        {
            start = 2200;
            end = 2600;
            nightFestival = true;
            return;
        }

        if (s == "fall" && d == 27) // Spirit's Eve
        {
            start = 2200;
            end = 2600;
            nightFestival = true;
            return;
        }
    }

    /// <summary>
    /// If we're on a festival map and time is within the festival window, nudge the game to start the event.
    /// This helps cases where you arrive and it's empty due to timing sync.
    /// </summary>
    private void TryKickFestivalEvent()
    {
        if (!Config.FestivalFriendlyMode)
            return;

        if (!Game1.isFestival())
            return;

        if (Game1.eventUp || Game1.CurrentEvent is not null)
            return;

        // If the player is in a menu/event, don't touch.
        if (IsGamePaused())
            return;

        // Only attempt if within window
        GetFestivalWindow(out int start, out int end, out _);

        int t = Game1.timeOfDay;
        if (TimeToMinutes(t) < TimeToMinutes(start) || TimeToMinutes(t) > TimeToMinutes(end))
            return;

        try
        {
            // Many festivals start when entering the location; this helps if you're already there.
            Game1.currentLocation?.checkForEvents();
        }
        catch
        {
            // Method existence can vary; safe no-op.
        }
    }

    /// <summary>
    /// Simulate time passing in 10-minute steps and run performTenMinuteClockUpdate
    /// so NPC schedules/shops/world state catch up correctly.
    /// </summary>
    private void WorldCatchUpForward(int fromTime, int toTime)
    {
        int fromMin = TimeToMinutes(fromTime);
        int toMin = TimeToMinutes(toTime);

        if (toMin <= fromMin)
            return;

        int startMark = (fromMin / 10) * 10;
        int endMark = (toMin / 10) * 10;

        int steps = 0;

        for (int mark = startMark + 10; mark <= endMark; mark += 10)
        {
            if (steps++ >= Config.MaxWorldCatchupTenMinuteSteps)
                break;

            int stepTime = ClampTime(MinutesToTime(mark));
            Game1.timeOfDay = stepTime;

            try { Game1.performTenMinuteClockUpdate(); }
            catch (Exception ex)
            {
                if (Config.DebugLogging)
                    Monitor.Log($"Ten-minute update failed at {Game1.timeOfDay}: {ex}", LogLevel.Trace);
            }
        }

        _lastTenMinuteMarkApplied = TimeToMinutes(ClampTime(Game1.timeOfDay)) / 10;
    }

    private void ApplyTenMinuteBoundaryUpdates(int currentMin, int targetMin)
    {
        if (_lastTenMinuteMarkApplied < 0)
            _lastTenMinuteMarkApplied = currentMin / 10;

        if (targetMin <= currentMin)
        {
            _lastTenMinuteMarkApplied = targetMin / 10;
            return;
        }

        int targetMark = targetMin / 10;
        if (targetMark <= _lastTenMinuteMarkApplied)
            return;

        for (int mark = _lastTenMinuteMarkApplied + 1; mark <= targetMark; mark++)
        {
            int stepMinutes = mark * 10;
            int stepTime = ClampTime(MinutesToTime(stepMinutes));
            Game1.timeOfDay = stepTime;

            try { Game1.performTenMinuteClockUpdate(); }
            catch (Exception ex)
            {
                if (Config.DebugLogging)
                    Monitor.Log($"Ten-minute update failed at {Game1.timeOfDay}: {ex}", LogLevel.Trace);
            }
        }

        _lastTenMinuteMarkApplied = targetMark;
    }

    private void SetTimeExact(int time)
    {
        int clamped = ClampTime(time);
        if (Game1.timeOfDay != clamped)
            Game1.timeOfDay = clamped;
    }

    private int ClampTime(int time)
    {
        if (!Config.ClampAtNight)
            return time;

        int min = Config.ClampMinTime;
        int max = Math.Max(Config.ClampMinTime, Config.ClampMaxTime);
        return Math.Clamp(time, min, max);
    }

    private int GetTargetTimeOfDayExact(DateTime now)
    {
        int h = now.Hour;
        int m = now.Minute;

        if (Config.TreatMidnightAsLateNight && h < 2)
            h += 24;

        return h * 100 + m;
    }

    private bool IsGamePaused()
    {
        if (Config.UseShouldTimePassCheck)
        {
            try { return !Game1.shouldTimePass(); }
            catch { }
        }

        return Game1.paused || Game1.activeClickableMenu is not null;
    }

    /// <summary>
    /// ONLY block syncing for real event lock states.
    /// IMPORTANT: do NOT block just because it's a festival day.
    /// </summary>
    private bool IsHardTimeLocked()
    {
        if (Game1.eventUp || Game1.CurrentEvent is not null)
            return true;

        return false;
    }

    // NPC catch-up (small safety net)
    private void RunNpcCatchUp()
    {
        if (!Context.IsWorldReady || IsHardTimeLocked())
            return;

        if ((DateTime.UtcNow - _lastNpcFix).TotalSeconds < Config.NpcFixCooldownSeconds)
            return;

        _lastNpcFix = DateTime.UtcNow;

        foreach (var location in Game1.locations.Where(l => l is not null))
        {
            foreach (var character in location.characters.ToList())
            {
                if (character is not NPC npc)
                    continue;

                if (!ShouldNudgeNpc(npc))
                    continue;

                TryWakeNpcBestEffort(npc);
            }
        }
    }

    private bool ShouldNudgeNpc(NPC npc)
    {
        if (npc is Horse or Pet)
            return false;

        if (npc.currentLocation is null)
            return false;

        if (Game1.timeOfDay < Config.NpcWakeEarliestTime)
            return false;

        return npc.isSleeping.Value;
    }

    private bool TryWakeNpcBestEffort(NPC npc)
    {
        try
        {
            npc.isSleeping.Value = false;
            npc.controller = null;
            npc.Halt();
            npc.isMovingOnPathFindPath.Value = false;

            try { npc.checkSchedule(Game1.timeOfDay); } catch { }

            if (!npc.isSleeping.Value)
                return true;

            if (Config.NPCWarpAggressiveness == NPCWarpAggressiveness.Aggressive && npc.currentLocation is GameLocation loc)
                return MoveNpcToNearbyTile(npc, loc);

            return true;
        }
        catch (Exception ex)
        {
            if (Config.DebugLogging)
                Monitor.Log($"NPC catch-up failed for {npc.Name}: {ex}", LogLevel.Trace);
            return false;
        }
    }

    private bool MoveNpcToNearbyTile(NPC npc, GameLocation location)
    {
        int sx = (int)(npc.Position.X / 64f);
        int sy = (int)(npc.Position.Y / 64f);

        (int dx, int dy)[] offsets = { (1, 0), (-1, 0), (0, 1), (0, -1) };

        foreach (var (dx, dy) in offsets)
        {
            int x = sx + dx;
            int y = sy + dy;

            if (!location.isTilePassable(new Location(x, y), Game1.viewport))
                continue;

            try { npc.setTilePosition(x, y); }
            catch { npc.Position = new Vector2(x * 64f, y * 64f); }

            npc.Halt();
            npc.controller = null;
            npc.isMovingOnPathFindPath.Value = false;
            return true;
        }

        return false;
    }

    private static int TimeToMinutes(int time)
    {
        int hours = time / 100;
        int minutes = time % 100;
        return hours * 60 + minutes;
    }

    private static int MinutesToTime(int minutes)
    {
        int hours = minutes / 60;
        int mins = minutes % 60;
        return hours * 100 + mins;
    }

    private void ResetConfig() => Config = new ModConfig();
    private void SaveConfig() => Helper.WriteConfig(Config);

    private void CheckCompatibilityWarnings()
    {
        string[] timeMods =
        {
            "aedenthorn.TimeSpeed",
            "Bouhm.NightOwl",
            "aEnigma.Realtime",
            "tZed.TimeMultiplier"
        };

        foreach (string modId in timeMods)
        {
            if (Helper.ModRegistry.IsLoaded(modId))
                Monitor.Log($"Detected potential time-control mod '{modId}'. Real-time syncing may conflict.", LogLevel.Warn);
        }
    }
}
