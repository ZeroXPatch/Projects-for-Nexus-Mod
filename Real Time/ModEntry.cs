using System;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Locations;
using xTile.Dimensions;
using SObject = StardewValley.Object;

namespace RealTimeValley;

public class ModEntry : Mod
{
    private ModConfig Config = new();

    private DateTime _lastSyncCheck = DateTime.MinValue;
    private DateTime _lastNpcFix = DateTime.MinValue;

    // World catch-up tracking
    private int _lastTenMinuteMarkApplied = -1;

    // Machine acceleration tracking
    private DateTime _lastMachineReal = DateTime.MinValue;
    private double _machineMinuteRemainder = 0;

    // Reflection: Object.minutesElapsed(int minutes, GameLocation env)
    private MethodInfo? _minutesElapsedMethod;

    public override void Entry(IModHelper helper)
    {
        Config = helper.ReadConfig<ModConfig>();

        _minutesElapsedMethod = typeof(SObject).GetMethod(
            "minutesElapsed",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(int), typeof(GameLocation) },
            modifiers: null
        );

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;

        helper.Events.GameLoop.UpdateTicking += OnUpdateTicking;
        helper.Events.GameLoop.OneSecondUpdateTicking += OnOneSecondUpdate;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        if (Config.ShowCompatibilityWarnings)
            CheckCompatibilityWarnings();

        var api = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (api is null) return;

        api.Register(ModManifest, ResetConfig, SaveConfig);

        api.AddSectionTitle(ModManifest, () => "Real-Time Valley");
        api.AddBoolOption(ModManifest, () => Config.Enable, v => Config.Enable = v, () => "Enable");
        api.AddBoolOption(ModManifest, () => Config.MinuteAccurateClock, v => Config.MinuteAccurateClock = v,
            () => "Minute-accurate clock",
            () => "Freeze vanilla time and set in-game time to your real clock (minute-by-minute).");

        api.AddNumberOption(ModManifest, () => Config.SyncIntervalSeconds, v => Config.SyncIntervalSeconds = Math.Max(1, v),
            () => "Sync interval (seconds)");

        api.AddSectionTitle(ModManifest, () => "Machines (fixes 'furnace takes forever')");
        api.AddBoolOption(ModManifest, () => Config.AccelerateMachines, v => Config.AccelerateMachines = v,
            () => "Accelerate machine processing",
            () => "Speeds up furnaces/kegs/jars/etc without speeding up NPCs or the world.");

        api.AddTextOption(
            ModManifest,
            () => Config.MachineSpeedMode.ToString(),
            v => Config.MachineSpeedMode = Enum.TryParse<MachineSpeedMode>(v, out var m) ? m : Config.MachineSpeedMode,
            () => "Machine speed mode",
            () => "MatchVanilla makes machines finish in the same real seconds as vanilla.",
            Enum.GetNames(typeof(MachineSpeedMode))
        );

        api.AddNumberOption(ModManifest, () => (int)Math.Round(Config.MachineSpeedMultiplier), v => Config.MachineSpeedMultiplier = Math.Max(1, v),
            () => "Custom multiplier (approx)",
            () => "Only used in CustomMultiplier mode. Vanilla-equivalent is about 86.",
            min: 1, max: 500, interval: 1
        );

        api.AddNumberOption(ModManifest, () => Config.MachineMaxMinutesPerTick, v => Config.MachineMaxMinutesPerTick = Math.Max(10, v),
            () => "Machine max minutes per tick",
            () => "Safety cap to avoid huge catch-ups if the game stutters.",
            min: 10, max: 1000, interval: 10
        );

        api.AddSectionTitle(ModManifest, () => "World catch-up");
        api.AddBoolOption(ModManifest, () => Config.RunWorldCatchupOnLoad, v => Config.RunWorldCatchupOnLoad = v,
            () => "Catch-up on load",
            () => "Simulates 10-min updates forward on load so shops/NPC states aren't stuck.");
        api.AddNumberOption(ModManifest, () => Config.MaxWorldCatchupTenMinuteSteps, v => Config.MaxWorldCatchupTenMinuteSteps = Math.Max(1, v),
            () => "Max catch-up steps (10-min)");

        api.AddSectionTitle(ModManifest, () => "Festivals");
        api.AddBoolOption(ModManifest, () => Config.FestivalFriendlyMode, v => Config.FestivalFriendlyMode = v,
            () => "Festival-friendly mode",
            () => "Prevents syncing you past festival hours so festivals aren't empty.");
        api.AddBoolOption(ModManifest, () => Config.HoldTimeInsideFestivalWindow, v => Config.HoldTimeInsideFestivalWindow = v,
            () => "Hold time inside festival window");
        api.AddNumberOption(ModManifest, () => Config.FestivalHoldTimeDay, v => Config.FestivalHoldTimeDay = Math.Clamp(v, 600, 2600),
            () => "Festival hold time (day)");
        api.AddNumberOption(ModManifest, () => Config.FestivalHoldTimeNight, v => Config.FestivalHoldTimeNight = Math.Clamp(v, 1800, 2600),
            () => "Festival hold time (night)");

        api.AddSectionTitle(ModManifest, () => "Night clamp");
        api.AddBoolOption(ModManifest, () => Config.ClampAtNight, v => Config.ClampAtNight = v, () => "Clamp at night");
        api.AddBoolOption(ModManifest, () => Config.PauseSyncAtMaxNight, v => Config.PauseSyncAtMaxNight = v, () => "Pause sync at night max");
        api.AddNumberOption(ModManifest, () => Config.ResumeSyncHour, v => Config.ResumeSyncHour = Math.Clamp(v, 0, 23), () => "Resume sync hour");
        api.AddBoolOption(ModManifest, () => Config.TreatMidnightAsLateNight, v => Config.TreatMidnightAsLateNight = v, () => "Treat midnight as late-night");
        api.AddNumberOption(ModManifest, () => Config.ClampMinTime, v => Config.ClampMinTime = Math.Clamp(v, 0, 2600), () => "Clamp min time");
        api.AddNumberOption(ModManifest, () => Config.ClampMaxTime, v => Config.ClampMaxTime = Math.Clamp(v, 600, 2600), () => "Clamp max time");

        api.AddSectionTitle(ModManifest, () => "NPC fix (optional)");
        api.AddBoolOption(ModManifest, () => Config.NPCFixOnLoad, v => Config.NPCFixOnLoad = v, () => "NPC fix on load");
        api.AddNumberOption(ModManifest, () => Config.NpcWakeEarliestTime, v => Config.NpcWakeEarliestTime = Math.Clamp(v, 600, 2600),
            () => "NPC wake earliest time");
        api.AddNumberOption(ModManifest, () => Config.NpcFixCooldownSeconds, v => Config.NpcFixCooldownSeconds = Math.Max(0, v),
            () => "NPC fix cooldown (seconds)");
        api.AddTextOption(
            ModManifest,
            () => Config.NPCWarpAggressiveness.ToString(),
            v => Config.NPCWarpAggressiveness = Enum.TryParse<NPCWarpAggressiveness>(v, out var a) ? a : Config.NPCWarpAggressiveness,
            () => "NPC warp aggressiveness",
            () => "Aggressive may nudge sleeping NPCs slightly off tiles to unstick them.",
            Enum.GetNames(typeof(NPCWarpAggressiveness))
        );

        api.AddSectionTitle(ModManifest, () => "Debug");
        api.AddBoolOption(ModManifest, () => Config.PauseWhenPaused, v => Config.PauseWhenPaused = v, () => "Pause when game paused");
        api.AddBoolOption(ModManifest, () => Config.UseShouldTimePassCheck, v => Config.UseShouldTimePassCheck = v, () => "Use shouldTimePass()");
        api.AddBoolOption(ModManifest, () => Config.DebugLogging, v => Config.DebugLogging = v, () => "Debug logging");
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        _lastSyncCheck = DateTime.MinValue;
        _lastNpcFix = DateTime.MinValue;
        _lastTenMinuteMarkApplied = -1;

        _lastMachineReal = DateTime.Now;
        _machineMinuteRemainder = 0;
    }

    // Freeze vanilla clock so it doesn't drift away from real time
    private void OnUpdateTicking(object? sender, UpdateTickingEventArgs e)
    {
        if (!Context.IsWorldReady || !Config.Enable || !Config.MinuteAccurateClock)
            return;

        if (IsHardTimeLocked())
            return;

        if (Config.PauseWhenPaused && IsGamePaused())
            return;

        try { Game1.gameTimeInterval = 0; } catch { }
    }

    private void OnOneSecondUpdate(object? sender, OneSecondUpdateTickingEventArgs e)
    {
        if (!Context.IsWorldReady || !Config.Enable)
            return;

        if (Config.PauseWhenPaused && IsGamePaused())
            return;

        if (IsHardTimeLocked())
            return;

        var now = DateTime.Now;
        if ((now - _lastSyncCheck).TotalSeconds < Math.Max(1, Config.SyncIntervalSeconds))
            return;

        _lastSyncCheck = now;

        if (Config.MinuteAccurateClock)
        {
            // 1) Sync clock
            int target = GetTargetTimeOfDayExact(now);
            target = ApplyFestivalRules(target);
            target = ClampTime(target);

            int current = ClampTime(Game1.timeOfDay);

            // 2) On load / big forward jump, run 10-minute updates so world state matches time
            if (Config.RunWorldCatchupOnLoad && _lastTenMinuteMarkApplied < 0)
            {
                _lastTenMinuteMarkApplied = TimeToMinutes(current) / 10;

                if (TimeToMinutes(target) > TimeToMinutes(current))
                    WorldCatchUpForward(current, target);

                SetTimeExact(target);

                if (Config.NPCFixOnLoad)
                    RunNpcCatchUp();
            }
            else
            {
                // Normal: apply boundary updates (forward only), then set exact minute time
                int curMin = TimeToMinutes(current);
                int tgtMin = TimeToMinutes(target);

                ApplyTenMinuteBoundaryUpdates(curMin, tgtMin);
                SetTimeExact(target);
            }

            // 3) NEW: accelerate machines independently of world time
            UpdateMachines(now);
        }
    }

    // ----------------------------
    // Machine acceleration
    // ----------------------------
    private void UpdateMachines(DateTime now)
    {
        if (!Config.AccelerateMachines || Config.MachineSpeedMode == MachineSpeedMode.Off)
            return;

        if (_lastMachineReal == DateTime.MinValue)
        {
            _lastMachineReal = now;
            return;
        }

        double realSeconds = (now - _lastMachineReal).TotalSeconds;
        if (realSeconds <= 0)
            return;

        // If the game hitch/alt-tab causes a huge gap, cap the seconds we process per tick
        realSeconds = Math.Min(realSeconds, 5.0);
        _lastMachineReal = now;

        // Desired machine minutes per real second
        // Vanilla: 10 in-game minutes per 7 real seconds => 10/7 = 1.428571 min/sec
        double minutesPerSecond = Config.MachineSpeedMode switch
        {
            MachineSpeedMode.MatchVanilla => (10.0 / 7.0),
            MachineSpeedMode.CustomMultiplier => (Config.MachineSpeedMultiplier / 60.0), // multiplier relative to real-time (1 game min per real min)
            _ => 0
        };

        if (minutesPerSecond <= 0)
            return;

        _machineMinuteRemainder += realSeconds * minutesPerSecond;

        int wholeMinutes = (int)Math.Floor(_machineMinuteRemainder);
        if (wholeMinutes <= 0)
            return;

        if (wholeMinutes > Config.MachineMaxMinutesPerTick)
            wholeMinutes = Config.MachineMaxMinutesPerTick;

        _machineMinuteRemainder -= wholeMinutes;

        foreach (var loc in Game1.locations.Where(l => l is not null))
        {
            // Objects placed in the world (most machines)
            foreach (var pair in loc.objects.Pairs.ToList())
            {
                var obj = pair.Value;
                if (obj is null)
                    continue;

                // Machine-like: countdown exists
                if (obj.MinutesUntilReady <= 0)
                    continue;

                // Only accelerate craftable/processing objects. (Conservative filter)
                if (!obj.bigCraftable.Value && obj.heldObject.Value is null)
                    continue;

                ApplyMinutesToObject(obj, wholeMinutes, loc);
            }
        }

        if (Config.DebugLogging)
            Monitor.Log($"Applied +{wholeMinutes} machine minutes this tick.", LogLevel.Trace);
    }

    private void ApplyMinutesToObject(SObject obj, int minutes, GameLocation loc)
    {
        try
        {
            if (_minutesElapsedMethod is not null)
            {
                _minutesElapsedMethod.Invoke(obj, new object[] { minutes, loc });
                return;
            }
        }
        catch
        {
            // fall back below
        }

        // Fallback: decrement the timer (less accurate, but safe)
        obj.MinutesUntilReady = Math.Max(0, obj.MinutesUntilReady - minutes);

        if (obj.MinutesUntilReady <= 0)
            obj.readyForHarvest.Value = true;
    }

    // ----------------------------
    // Festival rules
    // ----------------------------
    private int ApplyFestivalRules(int targetTime)
    {
        if (!Config.FestivalFriendlyMode || !Config.HoldTimeInsideFestivalWindow)
            return targetTime;

        if (!Game1.isFestival())
            return targetTime;

        // If an event is actively running, don't interfere
        if (Game1.eventUp || Game1.CurrentEvent is not null)
            return targetTime;

        GetFestivalWindow(out int start, out int end, out bool night);

        int tMin = TimeToMinutes(targetTime);
        if (tMin < TimeToMinutes(start) || tMin > TimeToMinutes(end))
        {
            int hold = night ? Config.FestivalHoldTimeNight : Config.FestivalHoldTimeDay;
            if (TimeToMinutes(hold) < TimeToMinutes(start)) hold = start;
            if (TimeToMinutes(hold) > TimeToMinutes(end)) hold = end;

            if (Config.DebugLogging)
                Monitor.Log($"Festival day: holding time at {hold} (window {start}-{end})", LogLevel.Debug);

            return hold;
        }

        return targetTime;
    }

    private void GetFestivalWindow(out int start, out int end, out bool nightFestival)
    {
        // Defaults for most festivals
        start = 900;
        end = 1400;
        nightFestival = false;

        string s = Game1.currentSeason;
        int d = Game1.dayOfMonth;

        // Night Market (winter 15-17)
        if (s == "winter" && d is >= 15 and <= 17)
        {
            start = 1700;
            end = 2600;
            nightFestival = true;
            return;
        }

        // Moonlight Jellies (summer 28), Spirit's Eve (fall 27)
        if (s == "summer" && d == 28)
        {
            start = 2200;
            end = 2600;
            nightFestival = true;
            return;
        }

        if (s == "fall" && d == 27)
        {
            start = 2200;
            end = 2600;
            nightFestival = true;
            return;
        }
    }

    // ----------------------------
    // World catch-up (10-minute updates)
    // ----------------------------
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

            Game1.timeOfDay = ClampTime(MinutesToTime(mark));
            try { Game1.performTenMinuteClockUpdate(); } catch { }
        }

        _lastTenMinuteMarkApplied = TimeToMinutes(ClampTime(Game1.timeOfDay)) / 10;
    }

    private void ApplyTenMinuteBoundaryUpdates(int currentMin, int targetMin)
    {
        if (_lastTenMinuteMarkApplied < 0)
            _lastTenMinuteMarkApplied = currentMin / 10;

        // Forward only
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
            Game1.timeOfDay = ClampTime(MinutesToTime(mark * 10));
            try { Game1.performTenMinuteClockUpdate(); } catch { }
        }

        _lastTenMinuteMarkApplied = targetMark;
    }

    // ----------------------------
    // Time helpers
    // ----------------------------
    private int GetTargetTimeOfDayExact(DateTime now)
    {
        int h = now.Hour;
        int m = now.Minute;

        if (Config.TreatMidnightAsLateNight && h < 2)
            h += 24;

        return h * 100 + m;
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

    // ----------------------------
    // Pause / lock checks
    // ----------------------------
    private bool IsGamePaused()
    {
        if (Config.UseShouldTimePassCheck)
        {
            try { return !Game1.shouldTimePass(); } catch { }
        }

        return Game1.paused || Game1.activeClickableMenu is not null;
    }

    // Only block during real events. Do NOT block just because "today is festival".
    private bool IsHardTimeLocked()
    {
        return Game1.eventUp || Game1.CurrentEvent is not null;
    }

    // ----------------------------
    // NPC fix (optional safety net)
    // ----------------------------
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

                if (Game1.timeOfDay < Config.NpcWakeEarliestTime)
                    continue;

                if (!npc.isSleeping.Value)
                    continue;

                try
                {
                    npc.isSleeping.Value = false;
                    npc.controller = null;
                    npc.Halt();
                    npc.isMovingOnPathFindPath.Value = false;

                    try { npc.checkSchedule(Game1.timeOfDay); } catch { }

                    if (Config.NPCWarpAggressiveness == NPCWarpAggressiveness.Aggressive && npc.currentLocation is GameLocation loc)
                        MoveNpcToNearbyTile(npc, loc);
                }
                catch { }
            }
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
