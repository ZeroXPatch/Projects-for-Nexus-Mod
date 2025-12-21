#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buffs;
using StardewValley.GameData.Locations;
using StardewValley.ItemTypeDefinitions;

namespace LandFishSwimmers
{
    internal sealed class ModEntry : Mod
    {
        private const string BuffId = "LandFishSwimmers.DailyFishingBoost";
        private const string MsgStateRequest = "DayStateRequest";
        private const string MsgStateSync = "DayStateSync";

        // Requested name
        private const string EventName = "Fish Weather: All Fish Day";

        private ModConfig Config = new();

        // Host-synced day state
        private DayState? Today;
        private bool IsActive;
        private bool ActivationMessageShown;
        private bool LocationsOverrideActive;

        // Visual fish stored PER location for this active window
        private readonly Dictionary<string, List<LandFish>> FishByLocation = new(StringComparer.Ordinal);
        private string? CurrentLocationKey;

        // Movement RNG (VISUAL ONLY)
        private readonly Random VisualRng = new();

        // Cache fish sprites (supports modded fish)
        private readonly Dictionary<string, (Texture2D Tex, Rectangle Src)> SpriteCache
            = new(StringComparer.OrdinalIgnoreCase);

        // fish IDs from Data/Fish (includes modded)
        private List<string> FishIds = new();

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;

            helper.Events.Display.RenderedWorld += this.OnRenderedWorld;

            helper.Events.Multiplayer.ModMessageReceived += this.OnModMessageReceived;
            helper.Events.Multiplayer.PeerConnected += this.OnPeerConnected;

            helper.Events.Content.AssetRequested += this.OnAssetRequested;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            this.SetupGmcm();
        }

        private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
        {
            this.Today = null;
            this.IsActive = false;
            this.ActivationMessageShown = false;
            this.LocationsOverrideActive = false;

            this.FishByLocation.Clear();
            this.CurrentLocationKey = null;
            this.SpriteCache.Clear();
            this.FishIds.Clear();
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.player is null)
                return;

            this.IsActive = false;
            this.ActivationMessageShown = false;
            this.LocationsOverrideActive = false;

            this.FishByLocation.Clear();
            this.CurrentLocationKey = null;

            this.RebuildFishIdCache();

            if (!this.Config.Enabled)
            {
                this.Today = null;
                return;
            }

            if (Context.IsMainPlayer)
            {
                this.Today = this.RollToday();

                this.Helper.Multiplayer.SendMessage(
                    message: this.Today.Value,
                    messageType: MsgStateSync,
                    modIDs: new[] { this.ModManifest.UniqueID }
                );
            }
            else
            {
                this.Today = null;

                this.Helper.Multiplayer.SendMessage(
                    message: new DayStateRequest(),
                    messageType: MsgStateRequest,
                    modIDs: new[] { this.ModManifest.UniqueID }
                );
            }
        }

        private void OnPeerConnected(object? sender, PeerConnectedEventArgs e)
        {
            if (!Context.IsWorldReady || !Context.IsMainPlayer || this.Today is null)
                return;

            this.Helper.Multiplayer.SendMessage(
                message: this.Today.Value,
                messageType: MsgStateSync,
                modIDs: new[] { this.ModManifest.UniqueID },
                playerIDs: new[] { e.Peer.PlayerID }
            );
        }

        private void OnModMessageReceived(object? sender, ModMessageReceivedEventArgs e)
        {
            if (e.FromModID != this.ModManifest.UniqueID)
                return;

            if (e.Type == MsgStateRequest)
            {
                if (!Context.IsMainPlayer || this.Today is null)
                    return;

                this.Helper.Multiplayer.SendMessage(
                    message: this.Today.Value,
                    messageType: MsgStateSync,
                    modIDs: new[] { this.ModManifest.UniqueID },
                    playerIDs: new[] { e.FromPlayerID }
                );
                return;
            }

            if (e.Type == MsgStateSync)
            {
                DayState state = e.ReadAs<DayState>();
                if (!IsToday(state))
                    return;

                this.Today = state;
            }
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.player is null)
                return;

            if (!this.Config.Enabled || this.Today is null)
            {
                this.ForceDeactivate();
                return;
            }

            DayState state = this.Today.Value;

            int start = NormalizeTime(state.StartTime);
            int end = NormalizeTime(state.EndTime);

            // If end is after midnight (00:00–05:50) and less than start, treat as crossing midnight.
            if (end < start && end < 600)
                end = NormalizeTime(end + 2400);

            bool shouldBeActive = state.Triggered && IsTimeInWindow(Game1.timeOfDay, start, end);

            if (shouldBeActive && !this.IsActive)
                this.Activate(state);
            else if (!shouldBeActive && this.IsActive)
                this.Deactivate();

            if (!this.IsActive)
                return;

            // Ensure fish exist for the current location (distributed across map)
            this.EnsureFishForCurrentLocation(Game1.currentLocation);

            // Update motion (throttled)
            if (!e.IsMultipleOf((uint)Math.Max(1, this.Config.UpdateTicks)))
                return;

            this.UpdateFishMotionForCurrentLocation();
        }

        private void Activate(DayState state)
        {
            this.IsActive = true;

            this.ApplyFishingBuff(state.FishingSkillBonus);

            if (!this.ActivationMessageShown && this.Config.ShowActivationMessage)
            {
                this.ActivationMessageShown = true;
                Game1.addHUDMessage(new HUDMessage(EventName));
            }

            if (state.AllFishEverywhere)
            {
                this.LocationsOverrideActive = true;
                this.Helper.GameContent.InvalidateCache("Data/Locations");
            }

            this.FishByLocation.Clear();
            this.CurrentLocationKey = null;

            this.EnsureFishForCurrentLocation(Game1.currentLocation);
        }

        private void Deactivate()
        {
            this.IsActive = false;
            this.RemoveFishingBuff();

            if (this.LocationsOverrideActive)
            {
                this.LocationsOverrideActive = false;
                this.Helper.GameContent.InvalidateCache("Data/Locations");
            }

            this.FishByLocation.Clear();
            this.CurrentLocationKey = null;
        }

        private void ForceDeactivate()
        {
            this.IsActive = false;
            this.RemoveFishingBuff();

            if (this.LocationsOverrideActive)
            {
                this.LocationsOverrideActive = false;
                this.Helper.GameContent.InvalidateCache("Data/Locations");
            }

            this.FishByLocation.Clear();
            this.CurrentLocationKey = null;
        }

        private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.player is null)
                return;

            if (!this.Config.Enabled || !this.IsActive)
                return;

            GameLocation loc = Game1.currentLocation;
            if (!this.Config.SpawnIndoors && !loc.IsOutdoors)
                return;

            if (this.CurrentLocationKey is null)
                return;

            if (!this.FishByLocation.TryGetValue(this.CurrentLocationKey, out List<LandFish>? fishList))
                return;

            SpriteBatch b = e.SpriteBatch;

            foreach (LandFish fish in fishList)
            {
                if (!TryGetFishSprite(fish.FishQualifiedId, out Texture2D tex, out Rectangle src))
                    continue;

                float bob = (float)Math.Sin(fish.BobPhase) * this.Config.BobPixels;

                Vector2 world = new(fish.Position.X, fish.Position.Y + bob);
                Vector2 screen = Game1.GlobalToLocal(Game1.viewport, world);

                float scale = Math.Max(0.1f, this.Config.Scale) * 4f;
                float depth = Math.Max(0f, (world.Y + 16f) / 10000f);

                float rotation = fish.Facing + (float)Math.Sin(fish.BobPhase * 0.5f) * this.Config.WiggleRadians;
                Vector2 origin = new(src.Width / 2f, src.Height / 2f);

                b.Draw(
                    tex,
                    screen,
                    src,
                    Color.White * this.Config.Opacity,
                    rotation,
                    origin,
                    scale,
                    SpriteEffects.None,
                    depth
                );
            }
        }

        private void EnsureFishForCurrentLocation(GameLocation loc)
        {
            if (!this.Config.SpawnIndoors && !loc.IsOutdoors)
            {
                this.CurrentLocationKey = loc.NameOrUniqueName;
                return;
            }

            string key = loc.NameOrUniqueName;
            this.CurrentLocationKey = key;

            if (this.FishByLocation.ContainsKey(key))
                return;

            List<LandFish> list = this.GenerateFishAcrossMap(loc, key, Math.Max(0, this.Config.FishCount));
            this.FishByLocation[key] = list;
        }

        private List<LandFish> GenerateFishAcrossMap(GameLocation loc, string locationKey, int count)
        {
            var list = new List<LandFish>(count);

            if (count <= 0 || this.FishIds.Count == 0 || loc.Map is null || loc.Map.Layers.Count == 0)
                return list;

            // Deterministic per day + location so it looks stable (and similar in MP).
            int seed = unchecked((int)(Game1.uniqueIDForThisGame ^ (uint)Game1.Date.TotalDays) ^ locationKey.GetHashCode());
            var rng = new Random(seed);

            int mapW = loc.Map.Layers[0].LayerWidth;
            int mapH = loc.Map.Layers[0].LayerHeight;

            const int attemptsPerFish = 80;

            for (int i = 0; i < count; i++)
            {
                bool spawned = false;

                for (int attempt = 0; attempt < attemptsPerFish; attempt++)
                {
                    int x = rng.Next(0, mapW);
                    int y = rng.Next(0, mapH);
                    Vector2 tile = new(x, y);

                    if (!IsGoodLandTile(loc, tile))
                        continue;

                    string id = this.FishIds[rng.Next(this.FishIds.Count)];
                    string qualified = QualifyObjectId(id);

                    if (!TryGetFishSprite(qualified, out _, out _))
                        continue;

                    Vector2 pos = tile * 64f + new Vector2(32f, 32f);

                    float ang = (float)(rng.NextDouble() * Math.PI * 2);
                    float speedPx = Math.Max(0f, this.Config.SpeedTilesPerSecond) * 64f;
                    Vector2 vel = new((float)Math.Cos(ang), (float)Math.Sin(ang));
                    vel *= speedPx;

                    list.Add(new LandFish(qualified, pos, vel));
                    spawned = true;
                    break;
                }

                if (!spawned)
                    break;
            }

            return list;
        }

        private void UpdateFishMotionForCurrentLocation()
        {
            if (this.CurrentLocationKey is null)
                return;

            if (!this.FishByLocation.TryGetValue(this.CurrentLocationKey, out List<LandFish>? list))
                return;

            GameLocation loc = Game1.currentLocation;

            float dt = (float)(Game1.currentGameTime?.ElapsedGameTime.TotalSeconds ?? 1.0 / 60.0);
            float speedPx = Math.Max(0f, this.Config.SpeedTilesPerSecond) * 64f;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                LandFish fish = list[i];

                // ✅ VisualRng exists now (fixes your CS1061 errors)
                if (this.VisualRng.NextDouble() < this.Config.TurnChancePerUpdate)
                {
                    float ang = (float)(this.VisualRng.NextDouble() * Math.PI * 2);
                    fish.Velocity = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * speedPx;
                }

                Vector2 next = fish.Position + fish.Velocity * dt;
                Vector2 nextTile = new((int)(next.X / 64f), (int)(next.Y / 64f));

                if (!IsGoodLandTile(loc, nextTile))
                {
                    fish.Velocity *= -1f;
                    next = fish.Position + fish.Velocity * dt;
                }

                fish.Position = next;

                if (fish.Velocity.LengthSquared() > 0.01f)
                    fish.Facing = (float)Math.Atan2(fish.Velocity.Y, fish.Velocity.X);

                fish.BobPhase += this.Config.BobSpeed;

                list[i] = fish;
            }
        }

        private static bool IsGoodLandTile(GameLocation loc, Vector2 tile)
        {
            if (loc.Map is null || loc.Map.Layers.Count == 0)
                return false;

            int x = (int)tile.X;
            int y = (int)tile.Y;

            int w = loc.Map.Layers[0].LayerWidth;
            int h = loc.Map.Layers[0].LayerHeight;

            if (x < 0 || y < 0 || x >= w || y >= h)
                return false;

            if (loc.isWaterTile(x, y))
                return false;

            if (loc.Objects.ContainsKey(tile))
                return false;

            if (loc.terrainFeatures.ContainsKey(tile))
                return false;

            return true;
        }

        private void ApplyFishingBuff(int bonus)
        {
            bonus = Math.Max(0, bonus);

            if (bonus <= 0)
            {
                this.RemoveFishingBuff();
                return;
            }

            var effects = new BuffEffects();
            effects.FishingLevel.Add(bonus);

            Buff buff = new Buff(
                id: BuffId,
                displayName: EventName,
                iconTexture: Game1.buffsIcons,
                iconSheetIndex: 0,
                duration: Buff.ENDLESS,
                effects: effects
            );

            Game1.player.applyBuff(buff);
        }

        private void RemoveFishingBuff()
        {
            if (Game1.player is null)
                return;

            Game1.player.buffs.Remove(BuffId);
        }

        private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        {
            if (!this.Config.Enabled || !this.LocationsOverrideActive)
                return;

            if (!e.NameWithoutLocale.IsEquivalentTo("Data/Locations"))
                return;

            e.Edit(asset =>
            {
                var dict = asset.AsDictionary<string, LocationData>().Data;

                if (!dict.TryGetValue("Default", out LocationData? def) || def is null)
                    return;

                def.Fish ??= new List<SpawnFishData>();

                foreach (string fishId in this.FishIds)
                {
                    def.Fish.Add(new SpawnFishData
                    {
                        ItemId = QualifyObjectId(fishId),
                        Chance = 1f,
                        Precedence = 0,
                        IgnoreFishDataRequirements = true,
                        Season = null
                    });
                }
            }, AssetEditPriority.Late);
        }

        private void RebuildFishIdCache()
        {
            try
            {
                Dictionary<string, string> fish = this.Helper.GameContent.Load<Dictionary<string, string>>("Data/Fish");
                this.FishIds = fish.Keys
                    .Where(k => !string.IsNullOrWhiteSpace(k))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Could not load Data/Fish: {ex}", LogLevel.Warn);
                this.FishIds.Clear();
            }
        }

        private bool TryGetFishSprite(string qualifiedItemId, out Texture2D tex, out Rectangle src)
        {
            if (this.SpriteCache.TryGetValue(qualifiedItemId, out var cached))
            {
                tex = cached.Tex;
                src = cached.Src;
                return true;
            }

            ParsedItemData? data = ItemRegistry.GetData(qualifiedItemId);
            if (data is null)
            {
                tex = null!;
                src = Rectangle.Empty;
                return false;
            }

            tex = data.GetTexture();
            src = data.GetSourceRect();

            this.SpriteCache[qualifiedItemId] = (tex, src);
            return true;
        }

        private DayState RollToday()
        {
            int seasonIndex = SeasonIndex(Game1.currentSeason);
            int dayKey = (Game1.year * 1000) + (seasonIndex * 100) + Game1.dayOfMonth;
            int seed = unchecked((int)(Game1.uniqueIDForThisGame ^ (uint)dayKey));
            var rng = new Random(seed);

            double chance = Math.Clamp(this.Config.DailyChancePercent / 100.0, 0.0, 1.0);
            bool triggered = rng.NextDouble() < chance;

            int start = NormalizeTime(this.Config.StartTime);
            int end = NormalizeTime(this.Config.EndTime);

            if (end < 600)
                end = NormalizeTime(end + 2400);

            return new DayState(
                Year: Game1.year,
                Season: Game1.currentSeason,
                DayOfMonth: Game1.dayOfMonth,
                Triggered: triggered,
                StartTime: start,
                EndTime: end,
                FishingSkillBonus: this.Config.FishingSkillBonus,
                AllFishEverywhere: this.Config.AllFishEverywhere
            );
        }

        private static bool IsToday(DayState s)
        {
            return s.Year == Game1.year
                && string.Equals(s.Season, Game1.currentSeason, StringComparison.Ordinal)
                && s.DayOfMonth == Game1.dayOfMonth;
        }

        private static int SeasonIndex(string season)
        {
            return season switch
            {
                "spring" => 0,
                "summer" => 1,
                "fall" => 2,
                "winter" => 3,
                _ => 0
            };
        }

        private static bool IsTimeInWindow(int time, int start, int end)
        {
            time = NormalizeTime(time);
            start = NormalizeTime(start);
            end = NormalizeTime(end);

            if (end <= start)
                return false;

            return time >= start && time < end;
        }

        private static int NormalizeTime(int time)
        {
            if (time < 0) time = 0;

            int mins = time % 100;
            int hours = time / 100;

            mins = (mins / 10) * 10;
            if (mins >= 60)
            {
                hours++;
                mins = 0;
            }

            if (hours > 26)
                hours = 26;

            if (hours == 26 && mins > 0)
                mins = 0;

            return hours * 100 + mins;
        }

        private static string QualifyObjectId(string objectId)
        {
            return objectId.StartsWith("(O)", StringComparison.OrdinalIgnoreCase)
                ? objectId
                : $"(O){objectId}";
        }

        private void SetupGmcm()
        {
            var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm is null)
                return;

            gmcm.Register(
                mod: this.ModManifest,
                reset: () => this.Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.Config)
            );

            gmcm.AddSectionTitle(this.ModManifest, () => EventName);

            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => this.Config.Enabled,
                setValue: v => this.Config.Enabled = v,
                name: () => "Enable");

            gmcm.AddNumberOption(this.ModManifest,
                getValue: () => this.Config.DailyChancePercent,
                setValue: v => this.Config.DailyChancePercent = v,
                name: () => "Daily chance",
                min: 0,
                max: 100,
                interval: 1,
                formatValue: v => $"{v}%");

            gmcm.AddNumberOption(this.ModManifest,
                getValue: () => this.Config.FishingSkillBonus,
                setValue: v => this.Config.FishingSkillBonus = v,
                name: () => "Fishing skill bonus",
                min: 0,
                max: 10,
                interval: 1);

            gmcm.AddNumberOption(this.ModManifest,
                getValue: () => this.Config.StartTime,
                setValue: v => this.Config.StartTime = v,
                name: () => "Start time",
                min: 600,
                max: 2600,
                interval: 10,
                formatValue: v => FormatTime12h(v));

            gmcm.AddNumberOption(this.ModManifest,
                getValue: () => this.Config.EndTime,
                setValue: v => this.Config.EndTime = v,
                name: () => "End time",
                min: 0,
                max: 2600,
                interval: 10,
                formatValue: v =>
                {
                    int t = NormalizeTime(v);
                    if (t < 600) t = NormalizeTime(t + 2400);
                    return FormatTime12h(t);
                });

            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => this.Config.ShowActivationMessage,
                setValue: v => this.Config.ShowActivationMessage = v,
                name: () => "Show activation popup");

            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => this.Config.AllFishEverywhere,
                setValue: v => this.Config.AllFishEverywhere = v,
                name: () => "Catch all fish anywhere",
                tooltip: () => "During the active window, temporarily adds all fish to the global fishing pool and ignores season/time/weather restrictions.");

            gmcm.AddSectionTitle(this.ModManifest, () => "Visuals");

            gmcm.AddNumberOption(this.ModManifest,
                getValue: () => this.Config.FishCount,
                setValue: v => this.Config.FishCount = v,
                name: () => "Fish sprite count",
                min: 0,
                max: 120,
                interval: 1);

            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => this.Config.SpawnIndoors,
                setValue: v => this.Config.SpawnIndoors = v,
                name: () => "Spawn indoors too");
        }

        private static string FormatTime12h(int hhmm)
        {
            hhmm = NormalizeTime(hhmm);

            int hours = hhmm / 100;
            int mins = hhmm % 100;

            int display24 = hours % 24;
            bool pm = display24 >= 12;
            int h12 = display24 % 12;
            if (h12 == 0) h12 = 12;

            return $"{h12}:{mins:00} {(pm ? "PM" : "AM")}";
        }

        private readonly record struct DayStateRequest();

        private readonly record struct DayState(
            int Year,
            string Season,
            int DayOfMonth,
            bool Triggered,
            int StartTime,
            int EndTime,
            int FishingSkillBonus,
            bool AllFishEverywhere
        );

        private struct LandFish
        {
            public string FishQualifiedId;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Facing;
            public float BobPhase;

            public LandFish(string fishQualifiedId, Vector2 position, Vector2 velocity)
            {
                this.FishQualifiedId = fishQualifiedId;
                this.Position = position;
                this.Velocity = velocity;
                this.Facing = 0f;
                this.BobPhase = 0f;
            }
        }
    }
}
