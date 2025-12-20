using System;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Characters;
using StardewValley.GameData.Characters;
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
    private bool _nightHoldLogged;

    public override void Entry(IModHelper helper)
    {
        Config = helper.ReadConfig<ModConfig>();
        Helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        Helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        Helper.Events.GameLoop.OneSecondUpdateTicking += OnOneSecondUpdate;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        if (Config.ShowCompatibilityWarnings)
            CheckCompatibilityWarnings();

        var api = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (api is null)
            return;

        api.Register(ModManifest, ResetConfig, SaveConfig);

        api.AddBoolOption(ModManifest, () => Config.Enable, value => Config.Enable = value, () => "Enable");
        api.AddTextOption(ModManifest, () => Config.RoundMode.ToString(), value => Config.RoundMode = Enum.TryParse<RoundMode>(value, out var r) ? r : Config.RoundMode, () => "Rounding Mode", () => "How to round real-world minutes to Stardew time (10 minute increments).", Enum.GetNames(typeof(RoundMode)));
        api.AddNumberOption(ModManifest, () => Config.SyncIntervalSeconds, value => Config.SyncIntervalSeconds = Math.Max(1, value), () => "Sync Interval (seconds)");
        api.AddNumberOption(ModManifest, () => Config.SmallDriftMinutes, value => Config.SmallDriftMinutes = Math.Max(0, value), () => "Small Drift Minutes");
        api.AddNumberOption(ModManifest, () => Config.LargeDriftMinutes, value => Config.LargeDriftMinutes = Math.Max(Config.SmallDriftMinutes, value), () => "Large Drift Minutes");
        api.AddTextOption(ModManifest, () => Config.LargeDriftBehavior.ToString(), value => Config.LargeDriftBehavior = Enum.TryParse<LargeDriftBehavior>(value, out var b) ? b : Config.LargeDriftBehavior, () => "Large Drift Behavior", null, Enum.GetNames(typeof(LargeDriftBehavior)));
        api.AddBoolOption(ModManifest, () => Config.AllowRewind, value => Config.AllowRewind = value, () => "Allow Rewind", () => "Allow time to move backwards to match real time.");
        api.AddBoolOption(ModManifest, () => Config.PauseWhenPaused, value => Config.PauseWhenPaused = value, () => "Pause When Game Paused");
        api.AddBoolOption(ModManifest, () => Config.ClampAtNight, value => Config.ClampAtNight = value, () => "Clamp At Night", () => "Keep time within safe night bounds to avoid pass-out.");
        api.AddBoolOption(ModManifest, () => Config.PauseSyncAtMaxNight, value => Config.PauseSyncAtMaxNight = value, () => "Pause Sync At Night Max", () => "When clamped at night max, pause syncing until morning to avoid endless midnight.");
        api.AddNumberOption(ModManifest, () => Config.ResumeSyncHour, value => Config.ResumeSyncHour = Math.Clamp(value, 0, 23), () => "Resume Sync Hour", () => "Real-world hour to resume syncing after night clamp (0-23).");
        api.AddBoolOption(ModManifest, () => Config.TreatMidnightAsLateNight, value => Config.TreatMidnightAsLateNight = value, () => "Treat Midnight As Late Night", () => "Map 12:00-1:59 AM to 24:00-25:59 to avoid rewind blocks.");
        api.AddNumberOption(ModManifest, () => Config.ClampMinTime, value => Config.ClampMinTime = value, () => "Clamp Min Time");
        api.AddNumberOption(ModManifest, () => Config.ClampMaxTime, value => Config.ClampMaxTime = Math.Clamp(value, 600, 2600), () => "Clamp Max Time");
        api.AddBoolOption(ModManifest, () => Config.RunTenMinuteUpdateForLargeForwardJumps, value => Config.RunTenMinuteUpdateForLargeForwardJumps = value, () => "Run 10-Minute Updates On Large Jumps", () => "Step through 10-minute updates when jumping forward to keep world state consistent.");
        api.AddNumberOption(ModManifest, () => Config.MaxTenMinuteCatchupSteps, value => Config.MaxTenMinuteCatchupSteps = Math.Max(1, value), () => "Max 10-Minute Catch-up Steps");
        api.AddBoolOption(ModManifest, () => Config.ForceStepCatchupForLargeJumps, value => Config.ForceStepCatchupForLargeJumps = value, () => "Force Step Catch-up", () => "Always use stepped 10-minute catch-up instead of instant snaps for large jumps.");
        api.AddBoolOption(ModManifest, () => Config.UseShouldTimePassCheck, value => Config.UseShouldTimePassCheck = value, () => "Use ShouldTimePass()", () => "Use the game's shouldTimePass() to detect pauses instead of only menu checks.");
        api.AddNumberOption(ModManifest, () => Config.NpcWakeEarliestTime, value => Config.NpcWakeEarliestTime = Math.Clamp(value, 600, 2600), () => "NPC Wake Earliest Time", () => "Earliest in-game time to forcibly wake sleeping NPCs when correcting schedules.");
        api.AddBoolOption(ModManifest, () => Config.NPCFixOnLoad, value => Config.NPCFixOnLoad = value, () => "NPC Fix On Load");
        api.AddBoolOption(ModManifest, () => Config.NPCFixOnLargeCorrection, value => Config.NPCFixOnLargeCorrection = value, () => "NPC Fix On Large Correction");
        api.AddNumberOption(ModManifest, () => Config.NpcFixCooldownSeconds, value => Config.NpcFixCooldownSeconds = Math.Max(0, value), () => "NPC Fix Cooldown (seconds)");
        api.AddTextOption(ModManifest, () => Config.NPCWarpAggressiveness.ToString(), value => Config.NPCWarpAggressiveness = Enum.TryParse<NPCWarpAggressiveness>(value, out var a) ? a : Config.NPCWarpAggressiveness, () => "NPC Warp Aggressiveness", null, Enum.GetNames(typeof(NPCWarpAggressiveness)));
        api.AddBoolOption(ModManifest, () => Config.ShowCompatibilityWarnings, value => Config.ShowCompatibilityWarnings = value, () => "Show Compatibility Warnings");
        api.AddBoolOption(ModManifest, () => Config.DebugLogging, value => Config.DebugLogging = value, () => "Debug Logging");
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        _pendingInitialSync = true;
        _initialSyncDone = false;
        _lastSyncCheck = DateTime.MinValue;
    }

    private void OnOneSecondUpdate(object? sender, OneSecondUpdateTickingEventArgs e)
    {
        try
        {
            if (!Context.IsWorldReady || !Config.Enable)
                return;

            if (_pendingInitialSync && !_initialSyncDone)
            {
                ApplyInitialSync();
                return;
            }

            var now = DateTime.Now;
            if ((now - _lastSyncCheck).TotalSeconds < Math.Max(1, Config.SyncIntervalSeconds))
                return;

            _lastSyncCheck = now;

            bool isPaused = IsGamePaused();
            if (Config.PauseWhenPaused && isPaused)
            {
                _wasPaused = true;
                return;
            }

            bool justUnpaused = _wasPaused && !isPaused;
            _wasPaused = isPaused;

            if (IsTimeLockedUnsafe())
                return;

            int targetTime = GetTargetTimeOfDay(DateTime.Now, Config.RoundMode);

            if (Config.PauseSyncAtMaxNight && targetTime >= Config.ClampMaxTime && DateTime.Now.Hour < Config.ResumeSyncHour)
            {
                if (!_nightHoldLogged && Config.DebugLogging)
                {
                    Monitor.Log($"Sync paused at night max {Config.ClampMaxTime} until real hour {Config.ResumeSyncHour}.", LogLevel.Debug);
                    _nightHoldLogged = true;
                }
                return;
            }

            _nightHoldLogged = false;
            int currentTime = Game1.timeOfDay;
            int driftMinutes = TimeToMinutes(targetTime) - TimeToMinutes(currentTime);

            if (driftMinutes == 0)
                return;

            bool isBackward = driftMinutes < 0;
            if (isBackward && !Config.AllowRewind)
            {
                if (Config.DebugLogging)
                    Monitor.Log($"Skipping rewind: target {targetTime} < current {currentTime}", LogLevel.Trace);
                return;
            }

            int absDrift = Math.Abs(driftMinutes);
            if (absDrift >= Config.LargeDriftMinutes || (justUnpaused && absDrift >= Config.SmallDriftMinutes))
            {
                HandleLargeDrift(targetTime, currentTime, absDrift);
                return;
            }

            if (absDrift > Config.SmallDriftMinutes)
            {
                int direction = Math.Sign(driftMinutes);
                int steps = Math.Clamp(absDrift / 10, 1, 2);
                int newTime = StepTime(Game1.timeOfDay, direction, steps);
                ApplyTime(newTime, runNpcFix: false);
                return;
            }

            int smallStepDirection = Math.Sign(driftMinutes);
            int smallStepTime = StepTime(Game1.timeOfDay, smallStepDirection, 1);
            ApplyTime(smallStepTime, runNpcFix: false);
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

    private void ApplyInitialSync()
    {
        if (!Context.IsWorldReady || IsTimeLockedUnsafe())
            return;

        int target = GetTargetTimeOfDay(DateTime.Now, Config.RoundMode);
        ApplyTime(target, Config.NPCFixOnLoad);
        _initialSyncDone = true;
        _pendingInitialSync = false;
    }

    private void HandleLargeDrift(int targetTime, int currentTime, int absDrift)
    {
        switch (Config.LargeDriftBehavior)
        {
            case LargeDriftBehavior.HardSnap:
                ApplyTime(targetTime, Config.NPCFixOnLargeCorrection);
                break;
            case LargeDriftBehavior.SoftCatchup:
                int direction = Math.Sign(TimeToMinutes(targetTime) - TimeToMinutes(currentTime));
                int steps = Math.Min(3, Math.Max(1, absDrift / 10));
                int newTime = StepTime(currentTime, direction, steps);
                bool reached = newTime == targetTime;
                ApplyTime(newTime, runNpcFix: reached && Config.NPCFixOnLargeCorrection);
                break;
        }
    }

    private int StepTime(int startTime, int direction, int steps)
    {
        int minutes = TimeToMinutes(startTime);
        minutes += direction * steps * 10;
        int maxTime = Config.ClampAtNight ? Config.ClampMaxTime : 2600;
        int clamped = Math.Clamp(minutes, TimeToMinutes(Config.ClampMinTime), TimeToMinutes(maxTime));
        return MinutesToTime(clamped);
    }

    public int GetTargetTimeOfDay(DateTime now, RoundMode mode)
    {
        int minutes = now.Hour * 60 + now.Minute;
        if (Config.TreatMidnightAsLateNight && now.Hour < 2)
            minutes += 24 * 60;

        int rounded = RoundTo10Min(MinutesToTime(minutes), mode);
        int roundedMinutes = TimeToMinutes(rounded);

        int clampedMin = TimeToMinutes(Math.Max(0, Config.ClampMinTime));
        int clampedMax = Config.ClampAtNight
            ? TimeToMinutes(Math.Clamp(Config.ClampMaxTime, Config.ClampMinTime, 2600))
            : TimeToMinutes(2600);

        int finalMinutes = Math.Clamp(roundedMinutes, clampedMin, clampedMax);
        return MinutesToTime(finalMinutes);
    }

    public int RoundTo10Min(int rawTime, RoundMode mode)
    {
        int minutes = TimeToMinutes(rawTime);
        int remainder = minutes % 10;
        return mode switch
        {
            RoundMode.Down10 => MinutesToTime(minutes - remainder),
            RoundMode.Up10 => MinutesToTime(minutes + (remainder == 0 ? 0 : 10 - remainder)),
            _ => MinutesToTime(minutes + (remainder >= 5 ? 10 - remainder : -remainder))
        };
    }

    public bool IsGamePaused()
    {
        if (Config.UseShouldTimePassCheck)
        {
            try
            {
                return !Game1.shouldTimePass();
            }
            catch
            {
                // fall back to menu-based pause detection
            }
        }

        return Game1.paused || Game1.activeClickableMenu is not null;
    }

    public bool IsTimeLockedUnsafe()
    {
        if (Game1.eventUp || Game1.CurrentEvent is not null)
            return true;
        if (Game1.isFestival())
            return true;
        if (Game1.currentLocation?.NameOrUniqueName?.Contains("Temp", StringComparison.OrdinalIgnoreCase) == true)
            return true;
        return false;
    }

    public void ApplyTime(int newTime, bool runNpcFix)
    {
        int maxTime = Config.ClampAtNight ? Config.ClampMaxTime : 2600;
        int clamped = Math.Clamp(newTime, Config.ClampMinTime, maxTime);
        if (Game1.timeOfDay == clamped)
            return;

        if (Config.DebugLogging)
            Monitor.Log($"Setting time from {Game1.timeOfDay} to {clamped}", LogLevel.Trace);

        int current = Game1.timeOfDay;
        int currentMinutes = TimeToMinutes(current);
        int targetMinutes = TimeToMinutes(clamped);
        bool forward = targetMinutes > currentMinutes;
        bool shouldStep = Config.ForceStepCatchupForLargeJumps || (forward && Config.RunTenMinuteUpdateForLargeForwardJumps);
        int diffMinutes = Math.Abs(targetMinutes - currentMinutes);

        if (shouldStep && diffMinutes >= 20)
        {
            int stepDirection = Math.Sign(targetMinutes - currentMinutes);
            int steps = Math.Min(diffMinutes / 10, Config.MaxTenMinuteCatchupSteps);
            int minutesWalker = currentMinutes;
            for (int i = 0; i < steps; i++)
            {
                minutesWalker += stepDirection * 10;
                int steppedTime = MinutesToTime(minutesWalker);
                Game1.timeOfDay = steppedTime;
                try
                {
                    Game1.performTenMinuteClockUpdate(Game1.timeOfDay);
                }
                catch (Exception ex)
                {
                    if (Config.DebugLogging)
                        Monitor.Log($"Ten-minute update failed at {Game1.timeOfDay}: {ex}", LogLevel.Trace);
                }
            }

            Game1.timeOfDay = clamped;
        }
        else
        {
            Game1.timeOfDay = clamped;
        }

        if (runNpcFix)
            RunNpcCatchUp();
    }

    public void RunNpcCatchUp()
    {
        if (!Context.IsWorldReady || IsTimeLockedUnsafe())
            return;

        if ((DateTime.UtcNow - _lastNpcFix).TotalSeconds < Config.NpcFixCooldownSeconds)
            return;

        _lastNpcFix = DateTime.UtcNow;
        int adjustedCount = 0;

        foreach (var location in Game1.locations)
        {
            if (location is null)
                continue;

            foreach (var character in location.characters.ToList())
            {
                if (character is not NPC npc)
                    continue;

                if (!ShouldNudgeNpc(npc))
                    continue;

                if (TryRefreshSchedule(npc))
                {
                    adjustedCount++;
                    continue;
                }

                if (Config.NPCWarpAggressiveness == NPCWarpAggressiveness.Aggressive && npc.currentLocation == location)
                {
                    npc.controller = null;
                    npc.Halt();
                    npc.faceDirection(2);
                    npc.isMovingOnPathFindPath.Value = false;
                    adjustedCount++;
                }
            }
        }

        if (Config.DebugLogging)
            Monitor.Log($"NPC catch-up ran; adjusted {adjustedCount} NPCs.", LogLevel.Debug);
    }

    private bool ShouldNudgeNpc(NPC npc)
    {
        if (npc is Horse or Pet)
            return false;

        if (Game1.timeOfDay < Config.NpcWakeEarliestTime)
            return false;

        if (npc.currentLocation is null)
            return false;

        return npc.isSleeping.Value;
    }

    private bool TryRefreshSchedule(NPC npc)
    {
        try
        {
            bool wasSleeping = npc.isSleeping.Value;
            if (wasSleeping)
            {
                npc.isSleeping.Value = false;
                npc.sleepIfOutdoors.Value = false;
                npc.controller = null;
                npc.Halt();
                npc.isMovingOnPathFindPath.Value = false;
            }

            npc.Schedule ??= npc.getSchedule(Game1.dayOfMonth);
            bool applied = npc.checkSchedule(Game1.timeOfDay);
            if (applied)
                return true;

            if (npc.isSleeping.Value)
            {
                npc.isSleeping.Value = false;
                npc.controller = null;
                npc.Halt();
                npc.isMovingOnPathFindPath.Value = false;
            }

            npc.Schedule = npc.getSchedule(Game1.dayOfMonth);
            if (npc.checkSchedule(Game1.timeOfDay))
                return true;

            if (Config.NPCWarpAggressiveness == NPCWarpAggressiveness.Aggressive && npc.currentLocation is GameLocation loc)
            {
                if (MoveNpcToNearbyTile(npc, loc))
                {
                    // one more try after un-sticking
                    if (npc.checkSchedule(Game1.timeOfDay))
                        return true;
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            if (Config.DebugLogging)
                Monitor.Log($"NPC catch-up failed for {npc.Name}: {ex}", LogLevel.Trace);
        }

        return false;
    }

    private bool MoveNpcToNearbyTile(NPC npc, GameLocation location)
    {
        var start = npc.getTileLocationPoint();
        Point[] offsets =
        {
            new(1, 0),
            new(-1, 0),
            new(0, 1),
            new(0, -1)
        };

        foreach (var offset in offsets)
        {
            var candidate = new Point(start.X + offset.X, start.Y + offset.Y);
            if (!location.isTileOnMap(candidate))
                continue;
            if (!location.isTilePassable(new Location(candidate.X, candidate.Y), Game1.viewport))
                continue;

            npc.setTilePosition(candidate);
            npc.Halt();
            npc.isMovingOnPathFindPath.Value = false;
            return true;
        }

        return false;
    }

    private int TimeToMinutes(int time)
    {
        int hours = time / 100;
        int minutes = time % 100;
        return hours * 60 + minutes;
    }

    private int MinutesToTime(int minutes)
    {
        int hours = minutes / 60;
        int mins = minutes % 60;
        return hours * 100 + mins;
    }

    private void ResetConfig()
    {
        Config = new ModConfig();
    }

    private void SaveConfig()
    {
        Helper.WriteConfig(Config);
    }

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
            {
                Monitor.Log($"Detected potential time-control mod '{modId}'. Clock syncing may conflict.", LogLevel.Warn);
            }
        }
    }
}
