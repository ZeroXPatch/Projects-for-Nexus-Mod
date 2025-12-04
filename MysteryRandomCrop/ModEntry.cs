using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Crops;
using StardewValley.GameData.Objects;
using StardewValley.GameData.Shops;
using StardewValley.Locations;
using StardewValley.Objects;

namespace MysteryHarvestCrop
{
    public class ModEntry : Mod
    {
        private const string SeedItemId = "OpenAI.MysteryHarvestCrop/RandomSeed";
        private const string CropId = "OpenAI.MysteryHarvestCrop/MysteryCrop";
        private const string CropSpriteTexture = "TileSheets/crops";
        private IMonitor _monitor = null!;
        private IModHelper _helper = null!;
        private Harmony _harmony = null!;
        private readonly List<string> _randomPool = new();
        private static ModEntry? Instance { get; set; }

        public override void Entry(IModHelper helper)
        {
            _monitor = Monitor;
            _helper = helper;
            _harmony = new Harmony(ModManifest.UniqueID);
            Instance = this;

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.Content.AssetRequested += OnAssetRequested;

            _harmony.Patch(
                original: AccessTools.Method(typeof(Crop), nameof(Crop.harvest)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(BeforeHarvest))
            );
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            _monitor.Log("Mystery Harvest Crop initialized.", LogLevel.Trace);
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            BuildRandomPool();
        }

        private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        {
            if (e.NameWithoutLocale.IsEquivalentTo("Data/Objects"))
            {
                e.Edit(asset =>
                {
                    var data = asset.AsDictionary<string, ObjectData>().Data;
                    if (!data.ContainsKey(SeedItemId))
                    {
                        data[SeedItemId] = new ObjectData
                        {
                            Name = SeedItemId,
                            DisplayName = _helper.Translation.Get("item.name"),
                            Description = _helper.Translation.Get("item.description"),
                            Type = "Seeds",
                            Category = -74,
                            Price = 100,
                            Edibility = StardewValley.Object.inedible,
                            Texture = "Maps/springobjects",
                            SpriteIndex = 770,
                            ContextTags = new List<string> { "color_brown" }
                        };
                    }
                });
            }
            else if (e.NameWithoutLocale.IsEquivalentTo("Data/Crops"))
            {
                e.Edit(asset =>
                {
                    var data = asset.AsDictionary<string, CropData>().Data;
                    if (!data.ContainsKey(CropId))
                    {
                        data[CropId] = new CropData
                        {
                            Seasons = new List<string> { "spring", "summer", "fall", "winter" },
                            PhaseDays = new List<int> { 10 },
                            RegrowAfterHarvest = -1,
                            HarvestItemId = "O:24",
                            HarvestMinStack = 1,
                            HarvestMaxStack = 1,
                            RaisedSeeds = false,
                            TrellisCrop = false,
                            Sprite = new CropSpriteData
                            {
                                Texture = CropSpriteTexture,
                                Index = 23
                            },
                            HarvestMethod = CropHarvestMethod.GrabWithHands,
                            SeedItemId = SeedItemId,
                            IgnoreSeasonsWhenOutdoors = true,
                            IgnoreSeasonsWhenIndoors = true
                        };
                    }
                });
            }
            else if (e.NameWithoutLocale.IsEquivalentTo("Data/Shops"))
            {
                e.Edit(asset =>
                {
                    var data = asset.AsDictionary<string, ShopData>().Data;
                    if (data.TryGetValue("SeedShop", out var shop))
                    {
                        shop.Items ??= new List<ShopItemData>();
                        if (!shop.Items.Any(item => string.Equals(item.ItemId, SeedItemId, StringComparison.OrdinalIgnoreCase)))
                        {
                            shop.Items.Add(new ShopItemData
                            {
                                ItemId = SeedItemId,
                                Quantity = int.MaxValue,
                                Price = 100
                            });
                        }
                    }
                });
            }
        }

        private void BuildRandomPool()
        {
            _randomPool.Clear();
            try
            {
                var objects = Game1.content.Load<Dictionary<string, ObjectData>>("Data/Objects");
                foreach (var entry in objects)
                {
                    if (entry.Value == null)
                        continue;

                    var category = entry.Value.Category;
                    if (category is -75 or -79 or -5 or -6 or -18 or -14)
                    {
                        _randomPool.Add(entry.Key);
                    }
                }
            }
            catch (Exception ex)
            {
                _monitor.Log($"Failed to build random item pool: {ex}", LogLevel.Error);
            }
        }

        public static bool BeforeHarvest(Crop __instance, int xTile, int yTile, HoeDirt soil, JunimoHarvester? junimoHarvester, bool isScythe, ref bool __result)
        {
            if (__instance == null)
                return true;

            var seedId = __instance.netSeedIndex.Value;
            if (!string.Equals(seedId, SeedItemId, StringComparison.OrdinalIgnoreCase))
                return true;

            try
            {
                var mod = Instance;
                if (mod == null)
                    return true;
                var location = junimoHarvester?.currentLocation ?? Game1.currentLocation;
                var randomItem = mod.CreateRandomItem();
                if (randomItem != null)
                {
                    if (junimoHarvester != null)
                    {
                        junimoHarvester.tryToAddItemToHut(randomItem);
                    }
                    else
                    {
                        if (!Game1.player.addItemToInventoryBool(randomItem))
                        {
                            Game1.createItemDebris(randomItem, new Vector2(xTile + 0.5f, yTile + 0.5f) * 64f, -1, location);
                        }
                    }
                }

                soil.destroyCrop(showAnimation: false);
                __result = true;
                return false;
            }
            catch (Exception ex)
            {
                Instance?._monitor.Log($"Failed to harvest mystery crop: {ex}", LogLevel.Error);
                return true;
            }
        }

        private Item? CreateRandomItem()
        {
            if (_randomPool.Count == 0)
                return null;

            var chosenId = _randomPool[Game1.random.Next(_randomPool.Count)];
            return ItemRegistry.Create(chosenId, 1);
        }
    }

}
