using System;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace AutoSwapBack
{
    public class ModEntry : Mod
    {
        // "null!" handles the warning since we load it in Entry
        private ModConfig Config = null!;

        // State tracking
        private int _returnToolIndex = 0;

        // Track previous tick state
        private Item? _lastTickItem;
        private int _lastTickStack;
        private int _lastTickSlot;

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu == null) return;

            configMenu.Register(
                mod: this.ModManifest,
                reset: () => this.Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.Config)
            );

            configMenu.AddSectionTitle(this.ModManifest, () => "Auto-Swap Settings");

            AddSwapOption(configMenu, "Seeds", () => this.Config.SeedsBehavior.ToString(), val => this.Config.SeedsBehavior = ParseEnum(val));
            AddSwapOption(configMenu, "Food", () => this.Config.FoodBehavior.ToString(), val => this.Config.FoodBehavior = ParseEnum(val));
            AddSwapOption(configMenu, "Bombs", () => this.Config.BombBehavior.ToString(), val => this.Config.BombBehavior = ParseEnum(val));
        }

        private static SwapTrigger ParseEnum(string val) => (SwapTrigger)Enum.Parse(typeof(SwapTrigger), val);

        private void AddSwapOption(IGenericModConfigMenuApi api, string name, Func<string> get, Action<string> set)
        {
            api.AddTextOption(
                mod: this.ModManifest,
                name: () => $"{name} Logic",
                tooltip: () => $"When to swap back to tool after using {name}.",
                getValue: get,
                setValue: set,
                allowedValues: new string[] { "Disabled", "UsedOnce", "Depleted" }
            );
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.player == null) return;

            Farmer player = Game1.player;
            Item? currentItem = player.CurrentItem;
            int currentSlot = player.CurrentToolIndex;
            int currentStack = currentItem?.Stack ?? 0;

            bool didSwap = false;

            // --- 1. CHECK FOR USAGE / SWAP ---
            if (currentSlot == _lastTickSlot)
            {
                // Check if the item we were holding LAST tick was a trigger item
                if (IsTriggerItem(_lastTickItem, out SwapTrigger triggerMode))
                {
                    bool usedOnce = false;
                    bool depleted = false;

                    // Case A: Item still exists, but stack decreased
                    if (currentItem != null && _lastTickItem != null && currentItem == _lastTickItem && currentStack < _lastTickStack)
                    {
                        usedOnce = true;
                    }
                    // Case B: Item is gone (Depleted) - Current is null, but Last was stack 1
                    else if (currentItem == null && _lastTickStack == 1)
                    {
                        usedOnce = true;
                        depleted = true;
                    }

                    // Perform Swap
                    if ((triggerMode == SwapTrigger.UsedOnce && usedOnce) ||
                        (triggerMode == SwapTrigger.Depleted && depleted))
                    {
                        player.CurrentToolIndex = _returnToolIndex;
                        didSwap = true;
                    }
                }
            }

            // --- 2. UPDATE RETURN TOOL TRACKER ---
            if (!didSwap)
            {
                // If we are holding a "trigger" item, do not update the return index.
                // This prevents the mod from saving "Seeds" as the tool to return to.
                bool holdingTriggerItem = IsTriggerItem(currentItem, out _);

                if (!holdingTriggerItem)
                {
                    _returnToolIndex = currentSlot;
                }
            }

            // --- 3. STORE STATE FOR NEXT TICK ---
            _lastTickItem = player.CurrentItem;
            _lastTickStack = player.CurrentItem?.Stack ?? 0;
            _lastTickSlot = player.CurrentToolIndex;
        }

        private bool IsTriggerItem(Item? item, out SwapTrigger mode)
        {
            mode = SwapTrigger.Disabled;
            if (item == null) return false;

            // 1. Seeds
            if (item.Category == StardewValley.Object.SeedsCategory)
            {
                mode = Config.SeedsBehavior;
                return mode != SwapTrigger.Disabled;
            }

            // 2. Bombs
            if (item.HasContextTag("category_bomb") || item.Name.Contains("Bomb") || item.ItemId == "286" || item.ItemId == "287" || item.ItemId == "288")
            {
                mode = Config.BombBehavior;
                return mode != SwapTrigger.Disabled;
            }

            // 3. Food
            // Check != -300 to include Sap, Void Mayo, etc.
            if (item is StardewValley.Object obj && obj.Edibility != -300 && item.Category != StardewValley.Object.SeedsCategory)
            {
                mode = Config.FoodBehavior;
                return mode != SwapTrigger.Disabled;
            }

            return false;
        }
    }
}