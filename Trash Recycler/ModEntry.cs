using System;
using System.Collections.Generic;
using System.Linq;
using GenericModConfigMenu;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Objects;
using Object = StardewValley.Object;

namespace TrashToTreasure
{
    public class ModEntry : Mod
    {
        private ModConfig Config = null!;
        private const string MachineId = "ZeroXPatch.TrashToTreasure_Machine";

        private List<string>? _cachedValidItemIds = null;

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.Content.AssetRequested += this.OnAssetRequested;
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;
            helper.Events.World.ObjectListChanged += this.OnObjectListChanged;
        }

        private void OnObjectListChanged(object? sender, ObjectListChangedEventArgs e)
        {
            // Hook into when objects are placed to override their behavior
            foreach (var pair in e.Added)
            {
                if (pair.Value.QualifiedItemId == $"(O){MachineId}")
                {
                    this.Monitor.Log($"Our machine was placed at {pair.Key}", LogLevel.Debug);
                }
            }
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null) return;

            configMenu.Register(
                mod: this.ModManifest,
                reset: () => {
                    this.Config = new ModConfig();
                    _cachedValidItemIds = null;
                },
                save: () => {
                    this.Helper.WriteConfig(this.Config);
                    _cachedValidItemIds = null;
                }
            );

            configMenu.AddSectionTitle(this.ModManifest, () => "Machine Settings");

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => this.Config.MaxItemValue,
                setValue: value => this.Config.MaxItemValue = value,
                name: () => "Max Item Value",
                tooltip: () => "The machine will output a random item worth LESS than this amount.",
                min: 10,
                max: 50000
            );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => this.Config.ProcessTimeHours,
                setValue: value => this.Config.ProcessTimeHours = value,
                name: () => "Process Time (Hours)",
                tooltip: () => "How many in-game hours it takes to process trash.",
                min: 0.1f,
                max: 24f
            );
        }

        private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        {
            if (e.Name.IsEquivalentTo($"Mods/{this.ModManifest.UniqueID}/MachineTexture"))
            {
                e.LoadFromModFile<Texture2D>("assets/machine.png", AssetLoadPriority.Medium);
            }

            if (e.NameWithoutLocale.IsEquivalentTo("Data/Objects"))
            {
                e.Edit(asset =>
                {
                    var data = asset.AsDictionary<string, ObjectData>().Data;
                    data[MachineId] = new ObjectData
                    {
                        Name = "Trash Recycler",
                        DisplayName = "Trash Recycler",
                        Description = "Insert ANY item to receive a random item.",
                        Type = "Crafting",
                        Category = Object.CraftingCategory,
                        Price = 100,
                        Texture = $"Mods/{this.ModManifest.UniqueID}/MachineTexture",
                        SpriteIndex = 0,
                        ContextTags = new List<string>() // REMOVED the machine_input_trash tag!
                    };
                });
            }

            if (e.NameWithoutLocale.IsEquivalentTo("Data/CraftingRecipes"))
            {
                e.Edit(asset =>
                {
                    var data = asset.AsDictionary<string, string>().Data;
                    data["Trash Recycler"] = $"388 10 335 5/Home/(O){MachineId}/false/default";
                });
            }
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady || !e.Button.IsActionButton()) return;

            Vector2 clickedTile = e.Cursor.Tile;
            this.Monitor.Log($"Button pressed at tile: {clickedTile}", LogLevel.Debug);

            if (Game1.currentLocation.Objects.TryGetValue(clickedTile, out Object machine))
            {
                this.Monitor.Log($"Found object: {machine.Name} with ID: {machine.QualifiedItemId}", LogLevel.Debug);

                if (machine.QualifiedItemId == $"(O){MachineId}")
                {
                    this.Monitor.Log("It's our machine! Suppressing input and calling logic", LogLevel.Debug);
                    this.Helper.Input.Suppress(e.Button);
                    PerformMachineLogic(machine);
                }
                else
                {
                    this.Monitor.Log($"Not our machine. Expected: (O){MachineId}", LogLevel.Debug);
                }
            }
            else
            {
                this.Monitor.Log("No object found at clicked tile", LogLevel.Debug);
            }
        }

        private void PerformMachineLogic(Object machine)
        {
            if (machine.heldObject.Value != null)
            {
                if (machine.MinutesUntilReady <= 0)
                {
                    Object result = machine.heldObject.Value;
                    if (Game1.player.addItemToInventoryBool(result))
                    {
                        machine.heldObject.Value = null;
                        machine.MinutesUntilReady = -1;
                        Game1.playSound("coin");
                    }
                    else
                    {
                        Game1.showRedMessage(Game1.content.LoadString("Strings\\StringsFromCSFiles:Crop.cs.588"));
                    }
                }
                return;
            }

            Item activeItem = Game1.player.CurrentItem;

            if (activeItem != null && (activeItem.QualifiedItemId == "(O)168" || activeItem.ParentSheetIndex == 168))
            {
                Game1.player.reduceActiveItemByOne();
                Game1.playSound("Ship");

                Item outputItem = GetRandomItem();

                machine.heldObject.Value = (Object)outputItem;
                machine.MinutesUntilReady = (int)(this.Config.ProcessTimeHours * 60);
            }
        }

        private Item GetRandomItem()
        {
            if (_cachedValidItemIds == null || _cachedValidItemIds.Count == 0)
            {
                _cachedValidItemIds = new List<string>();

                foreach (var kvp in Game1.objectData)
                {
                    if (kvp.Value.Price <= this.Config.MaxItemValue &&
                        kvp.Value.Price > 0 &&
                        kvp.Value.Category != Object.junkCategory &&
                        !kvp.Value.ExcludeFromRandomSale)
                    {
                        _cachedValidItemIds.Add(kvp.Key);
                    }
                }
            }

            if (_cachedValidItemIds.Count == 0) return ItemRegistry.Create("(O)388");

            string randomId = _cachedValidItemIds[Game1.random.Next(_cachedValidItemIds.Count)];
            return ItemRegistry.Create("(O)" + randomId);
        }
    }
}