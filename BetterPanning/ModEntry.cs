using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Tools;
using xTile.Dimensions;

namespace BetterPanning
{
    internal sealed class ModEntry : Mod
    {
        private static ModEntry? Instance { get; set; }

        private readonly Harmony harmony = new("Example.BetterPanning");

        private FieldInfo? orePanField;

        private ModConfig Config = new();

        private readonly Dictionary<string, List<WeightedItem>> treasureTable = new();

        private readonly Dictionary<GameLocation, int> panDebrisCount = new();

        public override void Entry(IModHelper helper)
        {
            Instance = this;

            this.orePanField = typeof(GameLocation).GetField("orePanPoints", BindingFlags.Instance | BindingFlags.NonPublic);

            this.Config = helper.ReadConfig<ModConfig>();

            this.BuildTreasureTable();
            this.ApplyHarmonyPatches();

            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.Display.RenderedHud += this.OnRenderedHud;
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e) => this.RegisterGmcm();

        private void RegisterGmcm()
        {
            var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm is null)
                return;

            gmcm.Register(this.ModManifest, () => this.Config = new ModConfig(), () => this.Helper.WriteConfig(this.Config));

            gmcm.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("config.section.spots"));
            gmcm.AddNumberOption(
                this.ModManifest,
                () => this.Config.SpotsPerLocation,
                value => this.Config.SpotsPerLocation = Math.Max(0, value),
                () => this.Helper.Translation.Get("config.spotsPerLocation.name"),
                () => this.Helper.Translation.Get("config.spotsPerLocation.tooltip"),
                min: 0,
                max: 20
            );
            gmcm.AddNumberOption(
                this.ModManifest,
                () => this.Config.MaxPlacementAttempts,
                value => this.Config.MaxPlacementAttempts = Math.Max(10, value),
                () => this.Helper.Translation.Get("config.maxPlacementAttempts.name"),
                () => this.Helper.Translation.Get("config.maxPlacementAttempts.tooltip"),
                min: 10,
                max: 500
            );
            gmcm.AddBoolOption(
                this.ModManifest,
                () => this.Config.AllowBeach,
                value => this.Config.AllowBeach = value,
                () => this.Helper.Translation.Get("config.allowBeach.name"),
                () => this.Helper.Translation.Get("config.allowBeach.tooltip")
            );
            gmcm.AddBoolOption(
                this.ModManifest,
                () => this.Config.AllowGroundSpots,
                value => this.Config.AllowGroundSpots = value,
                () => this.Helper.Translation.Get("config.allowGroundSpots.name"),
                () => this.Helper.Translation.Get("config.allowGroundSpots.tooltip")
            );

            gmcm.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("config.section.treasure"));
            gmcm.AddBoolOption(
                this.ModManifest,
                () => this.Config.DropExtraTreasure,
                value => this.Config.DropExtraTreasure = value,
                () => this.Helper.Translation.Get("config.dropExtraTreasure.name"),
                () => this.Helper.Translation.Get("config.dropExtraTreasure.tooltip")
            );
            gmcm.AddBoolOption(
                this.ModManifest,
                () => this.Config.ReplaceVanillaTreasure,
                value => this.Config.ReplaceVanillaTreasure = value,
                () => this.Helper.Translation.Get("config.replaceVanillaTreasure.name"),
                () => this.Helper.Translation.Get("config.replaceVanillaTreasure.tooltip")
            );

            gmcm.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("config.section.indicator"));
            gmcm.AddBoolOption(
                this.ModManifest,
                () => this.Config.ShowIndicator,
                value => this.Config.ShowIndicator = value,
                () => this.Helper.Translation.Get("config.showIndicator.name"),
                () => this.Helper.Translation.Get("config.showIndicator.tooltip")
            );
            gmcm.AddNumberOption(
                this.ModManifest,
                () => this.Config.IndicatorScalePercent,
                value => this.Config.IndicatorScalePercent = Math.Clamp(value, 50, 300),
                () => this.Helper.Translation.Get("config.indicatorScale.name"),
                () => this.Helper.Translation.Get("config.indicatorScale.tooltip"),
                min: 50,
                max: 300
            );
            gmcm.AddNumberOption(
                this.ModManifest,
                () => this.Config.MaxIndicatorDistance,
                value => this.Config.MaxIndicatorDistance = Math.Max(4, value),
                () => this.Helper.Translation.Get("config.maxIndicatorDistance.name"),
                () => this.Helper.Translation.Get("config.maxIndicatorDistance.tooltip"),
                min: 4,
                max: 200
            );
        }

        private void ApplyHarmonyPatches()
        {
            var doFunction = AccessTools.Method(typeof(Pan), nameof(Pan.DoFunction));
            if (doFunction is not null)
            {
                this.harmony.Patch(doFunction, new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.BeforePan)), new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.AfterPan)));
            }
            else
            {
                this.Monitor.Log("Failed to find Pan.DoFunction; treasure hooks will be skipped.", LogLevel.Warn);
            }
        }

        private static void BeforePan(Pan __instance, GameLocation location, int x, int y, int power, Farmer who)
        {
            if (Instance is null || location is null)
                return;

            Instance.panDebrisCount[location] = location.debris.Count;
        }

        private static void AfterPan(Pan __instance, GameLocation location, int x, int y, int power, Farmer who)
        {
            if (Instance is null || location is null || who is null)
                return;

            Instance.HandlePanComplete(location, who);
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            foreach (var location in Game1.locations)
            {
                try
                {
                    this.TryAddPanSpots(location);
                }
                catch (Exception ex)
                {
                    this.Monitor.Log($"Failed to add pan spots to {location.Name}: {ex}", LogLevel.Error);
                }
            }
        }

        private void OnRenderedHud(object? sender, RenderedHudEventArgs e)
        {
            if (!this.Config.ShowIndicator || !Context.IsWorldReady || Game1.eventUp)
                return;

            if (Game1.player.CurrentTool is not Pan)
                return;

            var location = Game1.player.currentLocation;
            if (location is null)
                return;

            var panPoints = this.GetPanPoints(location);
            if (panPoints.Count == 0)
                return;

            var playerTile = Game1.player.getTileLocation();
            var nearest = panPoints
                .Select(tile => new { tile, distance = Vector2.Distance(tile, playerTile) })
                .OrderBy(pair => pair.distance)
                .FirstOrDefault();

            if (nearest is null)
                return;

            if (nearest.distance > this.Config.MaxIndicatorDistance)
                return;

            var direction = this.GetDirectionString(nearest.tile - playerTile);
            string text = this.Helper.Translation.Get("hud.nearest", new { distance = (int)Math.Ceiling(nearest.distance), direction });

            Vector2 position = new(Game1.viewport.Width / 2f, 64f * Game1.options.uiScale);
            var font = Game1.smallFont;
            float scale = this.Config.IndicatorScalePercent / 100f;
            Vector2 size = font.MeasureString(text) * scale;
            position.X -= size.X / 2f;

            e.SpriteBatch.DrawString(font, text, position, Color.Black * 0.35f, 0f, Vector2.Zero, scale, SpriteEffects.None, 1f);
            e.SpriteBatch.DrawString(font, text, position + new Vector2(2f, 2f), Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 1f);
        }

        private void HandlePanComplete(GameLocation location, Farmer who)
        {
            if (!Context.IsWorldReady)
                return;

            if (this.Config.ReplaceVanillaTreasure && this.panDebrisCount.TryGetValue(location, out int previousCount))
            {
                int toRemove = location.debris.Count - previousCount;
                if (toRemove > 0)
                    location.debris.RemoveRange(previousCount, toRemove);
            }

            if (!this.Config.DropExtraTreasure)
                return;

            var item = this.RollTreasure(location, who);
            if (item is null)
                return;

            Vector2 dropPosition = who.Position + new Vector2(0f, -32f);
            Game1.createItemDebris(item, dropPosition, who.FacingDirection, location);
        }

        private Item? RollTreasure(GameLocation location, Farmer who)
        {
            if (who is null)
                return null;

            if (!this.treasureTable.TryGetValue(this.GetSeasonKey(location), out var entries))
                entries = this.treasureTable["any"];

            double totalWeight = entries.Sum(entry => entry.Weight);
            if (totalWeight <= 0)
                return null;

            double roll = Game1.random.NextDouble() * totalWeight;
            foreach (var entry in entries)
            {
                if (roll < entry.Weight)
                {
                    int stack = Game1.random.Next(entry.MinStack, entry.MaxStack + 1);
                    return ItemRegistry.Create(entry.ItemId, stack);
                }

                roll -= entry.Weight;
            }

            return null;
        }

        private void BuildTreasureTable()
        {
            List<WeightedItem> shared = new()
            {
                new("(O)378", 5, 20, 6),
                new("(O)380", 5, 15, 6),
                new("(O)384", 4, 10, 5),
                new("(O)386", 2, 6, 4),
                new("(O)909", 1, 3, 2),
                new("(O)334", 1, 3, 3),
                new("(O)335", 1, 3, 3),
                new("(O)336", 1, 2, 3),
                new("(O)337", 1, 2, 2),
                new("(O)910", 1, 1, 1),
                new("(O)60", 1, 2, 2),
                new("(O)62", 1, 2, 2),
                new("(O)64", 1, 2, 2),
                new("(O)66", 1, 2, 2),
                new("(O)68", 1, 2, 2),
                new("(O)70", 1, 2, 2),
                new("(O)72", 1, 1, 3),
                new("(O)74", 1, 1, 1),
                new("(O)535", 1, 3, 3),
                new("(O)536", 1, 2, 2),
                new("(O)537", 1, 2, 2),
                new("(O)749", 1, 2, 3),
                new("(O)82", 1, 3, 3),
                new("(O)84", 1, 2, 3),
                new("(O)86", 1, 2, 3),
                new("(O)80", 1, 2, 4),
                new("(O)168", 1, 1, 1),
                new("(O)166", 1, 1, 1),
                new("(O)167", 1, 1, 1),
                new("(O)796", 1, 3, 2),
                new("(O)767", 2, 8, 3),
                new("(O)771", 2, 6, 3),
                new("(O)849", 1, 2, 2),
                new("(O)688", 1, 2, 2),
                new("(O)157", 1, 2, 3),
                new("(O)709", 1, 2, 3),
                new("(O)176", 1, 1, 2),
                new("(O)174", 1, 1, 1),
                new("(O)275", 1, 3, 3),
                new("(O)920", 1, 1, 1),
                new("(O)92", 1, 1, 1),
                new("(O)585", 1, 1, 1)
            };

            List<WeightedItem> springSeeds = new()
            {
                new("(O)472", 2, 6, 4),
                new("(O)473", 1, 3, 3),
                new("(O)474", 2, 5, 4),
                new("(O)475", 2, 5, 4),
                new("(O)427", 2, 5, 3),
                new("(O)476", 1, 3, 3)
            };

            List<WeightedItem> summerSeeds = new()
            {
                new("(O)479", 2, 5, 3),
                new("(O)480", 2, 5, 4),
                new("(O)481", 2, 6, 4),
                new("(O)482", 3, 6, 4),
                new("(O)483", 2, 4, 4),
                new("(O)489", 1, 3, 3),
                new("(O)490", 2, 4, 3)
            };

            List<WeightedItem> fallSeeds = new()
            {
                new("(O)484", 2, 5, 3),
                new("(O)487", 1, 3, 3),
                new("(O)488", 2, 4, 3),
                new("(O)492", 2, 5, 3),
                new("(O)493", 2, 4, 3),
                new("(O)494", 2, 5, 3),
                new("(O)745", 2, 4, 3)
            };

            List<WeightedItem> winterSeeds = new()
            {
                new("(O)495", 5, 10, 4),
                new("(O)496", 3, 6, 4),
                new("(O)497", 3, 6, 4),
                new("(O)498", 3, 6, 4)
            };

            List<WeightedItem> rareFinds = new()
            {
                new("(O)797", 1, 1, 1),
                new("(O)74", 1, 1, 1),
                new("(O)530", 1, 1, 2),
                new("(O)533", 1, 1, 1),
                new("(O)168", 1, 1, 1),
                new("(O)166", 1, 1, 1)
            };

            List<WeightedItem> tackleAndTotems = new()
            {
                new("(O)686", 1, 2, 3),
                new("(O)687", 1, 2, 3),
                new("(O)694", 1, 2, 3),
                new("(O)695", 1, 2, 3),
                new("(O)690", 1, 2, 2),
                new("(O)681", 1, 1, 2),
                new("(O)688", 1, 1, 2)
            };

            List<WeightedItem> junk = new()
            {
                new("(O)382", 1, 4, 5),
                new("(O)271", 1, 2, 3),
                new("(O)168", 1, 1, 1),
                new("(O)169", 1, 2, 2)
            };

            List<WeightedItem> springPool = new();
            springPool.AddRange(shared);
            springPool.AddRange(springSeeds);
            springPool.AddRange(tackleAndTotems);
            springPool.AddRange(rareFinds);
            springPool.AddRange(junk);

            List<WeightedItem> summerPool = new();
            summerPool.AddRange(shared);
            summerPool.AddRange(summerSeeds);
            summerPool.AddRange(tackleAndTotems);
            summerPool.AddRange(rareFinds);
            summerPool.AddRange(junk);

            List<WeightedItem> fallPool = new();
            fallPool.AddRange(shared);
            fallPool.AddRange(fallSeeds);
            fallPool.AddRange(tackleAndTotems);
            fallPool.AddRange(rareFinds);
            fallPool.AddRange(junk);

            List<WeightedItem> winterPool = new();
            winterPool.AddRange(shared);
            winterPool.AddRange(winterSeeds);
            winterPool.AddRange(tackleAndTotems);
            winterPool.AddRange(rareFinds);
            winterPool.AddRange(junk);

            this.treasureTable["spring"] = springPool;
            this.treasureTable["summer"] = summerPool;
            this.treasureTable["fall"] = fallPool;
            this.treasureTable["winter"] = winterPool;
            this.treasureTable["any"] = shared;
        }

        private string GetSeasonKey(GameLocation? location)
        {
            string season = Game1.currentSeason?.ToLowerInvariant() ?? "spring";

            if (location is IslandLocation)
                season = "summer";

            return season switch
            {
                "summer" => "summer",
                "fall" => "fall",
                "winter" => "winter",
                _ => "spring"
            };
        }

        private void TryAddPanSpots(GameLocation location)
        {
            if (this.orePanField is null)
                return;

            if (location.IsStructure)
                return;

            if (!location.IsOutdoors && location is not MineShaft)
                return;

            if (!this.Config.AllowBeach && location.NameOrUniqueName.Contains("Beach", StringComparison.OrdinalIgnoreCase))
                return;

            var panPoints = this.GetPanPoints(location);
            int attempts = 0;
            while (panPoints.Count < this.Config.SpotsPerLocation && attempts < this.Config.MaxPlacementAttempts)
            {
                attempts++;
                var tile = this.GetRandomTile(location);
                if (tile is null)
                    continue;

                if (panPoints.Contains(tile.Value))
                    continue;

                if (!this.IsValidPanTile(location, tile.Value))
                    continue;

                panPoints.Add(tile.Value);
            }
        }

        private List<Vector2> GetPanPoints(GameLocation location)
        {
            if (this.orePanField is null)
                return new();

            var value = this.orePanField.GetValue(location) as List<Vector2>;
            if (value is null)
            {
                value = new List<Vector2>();
                this.orePanField.SetValue(location, value);
            }

            return value;
        }

        private Vector2? GetRandomTile(GameLocation location)
        {
            if (location is null)
                return null;

            int width = location.Map.Layers[0].LayerWidth;
            int height = location.Map.Layers[0].LayerHeight;
            return new Vector2(Game1.random.Next(width), Game1.random.Next(height));
        }

        private bool IsValidPanTile(GameLocation location, Vector2 tile)
        {
            if (!location.isTileOnMap(tile))
                return false;

            bool isWater = location.isWaterTile((int)tile.X, (int)tile.Y);
            if (isWater)
            {
                if (!this.HasAdjacentPassableLand(location, tile))
                    return false;
            }
            else
            {
                if (!this.Config.AllowGroundSpots)
                    return false;

                if (!location.isTilePassable(new Location((int)tile.X, (int)tile.Y), Game1.viewport))
                    return false;

                if (!this.HasAdjacentWater(location, tile))
                    return false;
            }

            return true;
        }

        private bool HasAdjacentPassableLand(GameLocation location, Vector2 tile)
        {
            foreach (var direction in Utility.DirectionsTileVectors)
            {
                Vector2 adjacent = tile + direction;
                if (!location.isTileOnMap(adjacent))
                    continue;

                if (location.isWaterTile((int)adjacent.X, (int)adjacent.Y))
                    continue;

                if (location.isTilePassable(new Location((int)adjacent.X, (int)adjacent.Y), Game1.viewport))
                    return true;
            }

            return false;
        }

        private bool HasAdjacentWater(GameLocation location, Vector2 tile)
        {
            foreach (var direction in Utility.DirectionsTileVectors)
            {
                Vector2 adjacent = tile + direction;
                if (location.isWaterTile((int)adjacent.X, (int)adjacent.Y))
                    return true;
            }

            return false;
        }

        private string GetDirectionString(Vector2 delta)
        {
            if (delta == Vector2.Zero)
                return "here";

            string vertical = delta.Y < -0.5f ? "north" : delta.Y > 0.5f ? "south" : string.Empty;
            string horizontal = delta.X < -0.5f ? "west" : delta.X > 0.5f ? "east" : string.Empty;

            if (!string.IsNullOrWhiteSpace(vertical) && !string.IsNullOrWhiteSpace(horizontal))
                return $"{vertical}-{horizontal}";

            return vertical + horizontal;
        }
    }

    internal sealed class WeightedItem
    {
        public string ItemId { get; }

        public int MinStack { get; }

        public int MaxStack { get; }

        public double Weight { get; }

        public WeightedItem(string itemId, int minStack, int maxStack, double weight)
        {
            this.ItemId = itemId;
            this.MinStack = minStack;
            this.MaxStack = maxStack;
            this.Weight = weight;
        }
    }

    internal sealed class ModConfig
    {
        public int SpotsPerLocation { get; set; } = 2;

        public int MaxPlacementAttempts { get; set; } = 200;

        public bool AllowBeach { get; set; } = true;

        public bool AllowGroundSpots { get; set; } = true;

        public bool DropExtraTreasure { get; set; } = true;

        public bool ReplaceVanillaTreasure { get; set; } = false;

        public bool ShowIndicator { get; set; } = true;

        public int IndicatorScalePercent { get; set; } = 100;

        public int MaxIndicatorDistance { get; set; } = 80;
    }

    public interface IGenericModConfigMenuApi
    {
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);

        void AddSectionTitle(IManifest mod, Func<string> text, Func<string>? tooltip = null);

        void AddNumberOption(
            IManifest mod,
            Func<int> getValue,
            Action<int> setValue,
            Func<string> name,
            Func<string>? tooltip = null,
            int? min = null,
            int? max = null,
            int? interval = null,
            Func<int, string>? formatValue = null,
            string? fieldId = null
        );

        void AddBoolOption(
            IManifest mod,
            Func<bool> getValue,
            Action<bool> setValue,
            Func<string> name,
            Func<string>? tooltip = null,
            string? fieldId = null
        );
    }
}
