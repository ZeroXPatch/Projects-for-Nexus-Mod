using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using xTile;

namespace LocationPreload
{
    internal sealed class ModEntry : Mod
    {
        private ModConfig Config = new();

        private readonly Queue<string> MapQueue = new();
        private readonly HashSet<string> QueuedOrLoaded = new(StringComparer.OrdinalIgnoreCase);

        private int NearestTickCounter;

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.Player.Warped += this.OnWarped;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            this.RegisterGmcm();
        }

        private void RegisterGmcm()
        {
            var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm is null)
                return;

            gmcm.Register(
                mod: this.ModManifest,
                reset: () => this.Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.Config)
            );

            gmcm.AddSectionTitle(
                mod: this.ModManifest,
                text: () => this.Helper.Translation.Get("gmcm.section.general"),
                tooltip: () => this.Helper.Translation.Get("gmcm.section.general.tooltip")
            );

            gmcm.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => this.Config.Enabled,
                setValue: v => this.Config.Enabled = v,
                name: () => this.Helper.Translation.Get("gmcm.enabled.name"),
                tooltip: () => this.Helper.Translation.Get("gmcm.enabled.tooltip"),
                fieldId: "Enabled"
            );

            string[] modes = Enum.GetNames(typeof(PreloadMode));

            gmcm.AddTextOption(
                mod: this.ModManifest,
                getValue: () => this.Config.Mode.ToString(),
                setValue: value =>
                {
                    if (Enum.TryParse(value, ignoreCase: true, out PreloadMode parsed))
                        this.Config.Mode = parsed;
                },
                name: () => this.Helper.Translation.Get("gmcm.mode.name"),
                tooltip: () => this.Helper.Translation.Get("gmcm.mode.tooltip"),
                allowedValues: modes,
                formatAllowedValue: v => v.ToLowerInvariant() switch
                {
                    "hub" => this.Helper.Translation.Get("gmcm.mode.value.hub"),
                    "nearest" => this.Helper.Translation.Get("gmcm.mode.value.nearest"),
                    "both" => this.Helper.Translation.Get("gmcm.mode.value.both"),
                    _ => v
                },
                fieldId: "Mode"
            );

            gmcm.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => this.Config.HubPreloadWarpTargetsFromCurrentLocation,
                setValue: v => this.Config.HubPreloadWarpTargetsFromCurrentLocation = v,
                name: () => this.Helper.Translation.Get("gmcm.hubPreloadWarpTargets.name"),
                tooltip: () => this.Helper.Translation.Get("gmcm.hubPreloadWarpTargets.tooltip"),
                fieldId: "HubPreloadWarpTargetsFromCurrentLocation"
            );

            gmcm.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => this.Config.ClearQueueOnWarp,
                setValue: v => this.Config.ClearQueueOnWarp = v,
                name: () => this.Helper.Translation.Get("gmcm.clearQueueOnWarp.name"),
                tooltip: () => this.Helper.Translation.Get("gmcm.clearQueueOnWarp.tooltip"),
                fieldId: "ClearQueueOnWarp"
            );

            gmcm.AddTextOption(
                mod: this.ModManifest,
                getValue: () => string.Join(", ", this.Config.CustomLocationPreloadList),
                setValue: v => this.Config.CustomLocationPreloadList = ParseLocationList(v),
                name: () => this.Helper.Translation.Get("gmcm.customLocations.name"),
                tooltip: () => this.Helper.Translation.Get("gmcm.customLocations.tooltip"),
                fieldId: "CustomLocationPreloadList"
            );

            gmcm.AddSectionTitle(
                mod: this.ModManifest,
                text: () => this.Helper.Translation.Get("gmcm.section.nearest"),
                tooltip: () => this.Helper.Translation.Get("gmcm.section.nearest.tooltip")
            );

            gmcm.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => this.Config.NearestWarpCount,
                setValue: v => this.Config.NearestWarpCount = v,
                name: () => this.Helper.Translation.Get("gmcm.nearestWarpCount.name"),
                tooltip: () => this.Helper.Translation.Get("gmcm.nearestWarpCount.tooltip"),
                min: 1,
                max: 5,
                interval: 1,
                formatValue: v => v.ToString(),
                fieldId: "NearestWarpCount"
            );

            gmcm.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => this.Config.NearestMaxDistance,
                setValue: v => this.Config.NearestMaxDistance = v,
                name: () => this.Helper.Translation.Get("gmcm.nearestMaxDistance.name"),
                tooltip: () => this.Helper.Translation.Get("gmcm.nearestMaxDistance.tooltip"),
                min: 0,
                max: 50,
                interval: 1,
                formatValue: v => v == 0
                    ? this.Helper.Translation.Get("gmcm.common.unlimited")
                    : v.ToString(),
                fieldId: "NearestMaxDistance"
            );

            gmcm.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => this.Config.NearestCheckIntervalTicks,
                setValue: v => this.Config.NearestCheckIntervalTicks = v,
                name: () => this.Helper.Translation.Get("gmcm.nearestCheckIntervalTicks.name"),
                tooltip: () => this.Helper.Translation.Get("gmcm.nearestCheckIntervalTicks.tooltip"),
                min: 1,
                max: 60,
                interval: 1,
                formatValue: v => v.ToString(),
                fieldId: "NearestCheckIntervalTicks"
            );

            gmcm.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => this.Config.PreferFacingWarp,
                setValue: v => this.Config.PreferFacingWarp = v,
                name: () => this.Helper.Translation.Get("gmcm.preferFacingWarp.name"),
                tooltip: () => this.Helper.Translation.Get("gmcm.preferFacingWarp.tooltip"),
                fieldId: "PreferFacingWarp"
            );

            gmcm.AddSectionTitle(
                mod: this.ModManifest,
                text: () => this.Helper.Translation.Get("gmcm.section.performance"),
                tooltip: () => this.Helper.Translation.Get("gmcm.section.performance.tooltip")
            );

            gmcm.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => this.Config.MaxMapsPerTick,
                setValue: v => this.Config.MaxMapsPerTick = v,
                name: () => this.Helper.Translation.Get("gmcm.maxMapsPerTick.name"),
                tooltip: () => this.Helper.Translation.Get("gmcm.maxMapsPerTick.tooltip"),
                min: 0,
                max: 10,
                interval: 1,
                formatValue: v => v.ToString(),
                fieldId: "MaxMapsPerTick"
            );

            gmcm.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => this.Config.MaxMillisecondsPerTick,
                setValue: v => this.Config.MaxMillisecondsPerTick = v,
                name: () => this.Helper.Translation.Get("gmcm.maxMillisecondsPerTick.name"),
                tooltip: () => this.Helper.Translation.Get("gmcm.maxMillisecondsPerTick.tooltip"),
                min: 0,
                max: 16,
                interval: 1,
                formatValue: v => v == 0
                    ? this.Helper.Translation.Get("gmcm.common.unlimited")
                    : this.Helper.Translation.Get("gmcm.common.ms", new { value = v }),
                fieldId: "MaxMillisecondsPerTick"
            );

            gmcm.AddSectionTitle(
                mod: this.ModManifest,
                text: () => this.Helper.Translation.Get("gmcm.section.debug"),
                tooltip: () => this.Helper.Translation.Get("gmcm.section.debug.tooltip")
            );

            gmcm.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => this.Config.VerboseLogging,
                setValue: v => this.Config.VerboseLogging = v,
                name: () => this.Helper.Translation.Get("gmcm.verboseLogging.name"),
                tooltip: () => this.Helper.Translation.Get("gmcm.verboseLogging.tooltip"),
                fieldId: "VerboseLogging"
            );
        }

        private static List<string> ParseLocationList(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return new List<string>();

            return value
                .Split(new[] { ',', ';', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            this.MapQueue.Clear();
            this.QueuedOrLoaded.Clear();
            this.NearestTickCounter = 0;
        }

        private void OnWarped(object? sender, WarpedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.player is null)
                return;

            if (!this.Config.Enabled)
                return;

            if (this.Config.ClearQueueOnWarp)
                this.MapQueue.Clear();

            // Hub/Both: queue all exits from the location we just entered.
            if (this.Config.Mode is PreloadMode.Hub or PreloadMode.Both)
                this.QueueHubTargets(e.NewLocation);

            // Optional: custom list (useful in any mode).
            if (this.Config.CustomLocationPreloadList.Count > 0)
                this.QueueCustomLocations();
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.player is null)
                return;

            if (!this.Config.Enabled)
                return;

            // Nearest/Both: periodically queue nearest warp(s).
            if (this.Config.Mode is PreloadMode.Nearest or PreloadMode.Both)
            {
                this.NearestTickCounter++;
                int interval = Math.Max(1, this.Config.NearestCheckIntervalTicks);

                if (this.NearestTickCounter >= interval)
                {
                    this.NearestTickCounter = 0;
                    this.QueueNearestWarps(Game1.player.currentLocation, Game1.player);
                }
            }

            this.ProcessPreloadQueue();
        }

        private void QueueHubTargets(GameLocation? location)
        {
            if (location is null)
                return;

            if (!this.Config.HubPreloadWarpTargetsFromCurrentLocation)
                return;

            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var warp in location.warps)
            {
                if (!string.IsNullOrWhiteSpace(warp?.TargetName))
                    targets.Add(warp.TargetName);
            }

            int enqueued = 0;
            foreach (string locName in targets)
            {
                if (this.TryEnqueueLocationMap(locName))
                    enqueued++;
            }

            if (this.Config.VerboseLogging && enqueued > 0)
                this.Monitor.Log($"[Hub] Queued {enqueued} map(s) from '{location.NameOrUniqueName}'.", LogLevel.Trace);
        }

        private void QueueCustomLocations()
        {
            int enqueued = 0;

            foreach (string locName in this.Config.CustomLocationPreloadList)
            {
                if (this.TryEnqueueLocationMap(locName))
                    enqueued++;
            }

            if (this.Config.VerboseLogging && enqueued > 0)
                this.Monitor.Log($"[Custom] Queued {enqueued} map(s) from custom list.", LogLevel.Trace);
        }

        private void QueueNearestWarps(GameLocation? location, Farmer player)
        {
            if (location is null)
                return;

            if (location.warps is null || location.warps.Count == 0)
                return;

            int count = Math.Max(1, this.Config.NearestWarpCount);
            int maxDist = Math.Max(0, this.Config.NearestMaxDistance);

            Vector2 playerTile = player.Tile;
            Vector2 facingTile = GetTileInFront(player);

            var candidates = new List<(Warp warp, int dist, bool facing)>(location.warps.Count);

            foreach (var warp in location.warps)
            {
                if (warp is null || string.IsNullOrWhiteSpace(warp.TargetName))
                    continue;

                int dx = Math.Abs(warp.X - (int)playerTile.X);
                int dy = Math.Abs(warp.Y - (int)playerTile.Y);
                int dist = dx + dy;

                if (maxDist > 0 && dist > maxDist)
                    continue;

                bool facing = this.Config.PreferFacingWarp
                              && warp.X == (int)facingTile.X
                              && warp.Y == (int)facingTile.Y;

                candidates.Add((warp, dist, facing));
            }

            if (candidates.Count == 0)
                return;

            candidates.Sort((a, b) =>
            {
                int c = a.dist.CompareTo(b.dist);
                if (c != 0) return c;

                if (a.facing == b.facing) return 0;
                return a.facing ? -1 : 1;
            });

            int enqueued = 0;
            foreach (var (warp, _, _) in candidates.Take(count))
            {
                if (this.TryEnqueueLocationMap(warp.TargetName))
                    enqueued++;
            }

            if (this.Config.VerboseLogging && enqueued > 0)
                this.Monitor.Log($"[Nearest] Queued {enqueued} map(s) from '{location.NameOrUniqueName}'.", LogLevel.Trace);
        }

        private void ProcessPreloadQueue()
        {
            if (this.MapQueue.Count == 0)
                return;

            int loadedThisTick = 0;
            Stopwatch sw = Stopwatch.StartNew();

            while (this.MapQueue.Count > 0)
            {
                if (this.Config.MaxMapsPerTick > 0 && loadedThisTick >= this.Config.MaxMapsPerTick)
                    break;

                if (this.Config.MaxMillisecondsPerTick > 0 && sw.ElapsedMilliseconds >= this.Config.MaxMillisecondsPerTick)
                    break;

                string mapAssetKey = this.MapQueue.Dequeue();

                try
                {
                    // CRITICAL: load maps as xTile.Map (prevents CP/SVE TMX type errors).
                    this.Helper.GameContent.Load<Map>(mapAssetKey);

                    loadedThisTick++;

                    if (this.Config.VerboseLogging)
                        this.Monitor.Log($"Preloaded map: {mapAssetKey}", LogLevel.Trace);
                }
                catch (Exception ex)
                {
                    this.Monitor.Log($"Failed to preload map '{mapAssetKey}'.", LogLevel.Debug);
                    this.Monitor.Log(ex.ToString(), LogLevel.Trace);
                }
            }
        }

        private bool TryEnqueueLocationMap(string locationName)
        {
            locationName = locationName?.Trim() ?? "";
            if (locationName.Length == 0)
                return false;

            GameLocation? loc = Game1.getLocationFromName(locationName);
            if (loc is null)
                return false;

            string? mapAssetKey = TryGetMapAssetKey(loc);
            if (string.IsNullOrWhiteSpace(mapAssetKey))
                return false;

            return this.TryEnqueueMap(mapAssetKey);
        }

        private bool TryEnqueueMap(string mapAssetKey)
        {
            mapAssetKey = mapAssetKey.Trim();

            if (this.QueuedOrLoaded.Contains(mapAssetKey))
                return false;

            this.QueuedOrLoaded.Add(mapAssetKey);
            this.MapQueue.Enqueue(mapAssetKey);
            return true;
        }

        private static string? TryGetMapAssetKey(GameLocation location)
        {
            try
            {
                string? mapPath = location.mapPath?.Value;
                if (string.IsNullOrWhiteSpace(mapPath))
                    return null;

                return mapPath.Replace('\\', '/').Trim();
            }
            catch
            {
                return null;
            }
        }

        private static Vector2 GetTileInFront(Farmer player)
        {
            Vector2 tile = player.Tile;
            return player.FacingDirection switch
            {
                0 => tile + new Vector2(0, -1),
                1 => tile + new Vector2(1, 0),
                2 => tile + new Vector2(0, 1),
                3 => tile + new Vector2(-1, 0),
                _ => tile
            };
        }
    }
}
