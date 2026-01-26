using System;
using GenericModConfigMenu;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace ToolMaster
{
    public class ModEntry : Mod
    {
        private ModConfig Config = new();
        private float _lastStamina = 0;
        private bool _wasUsingTool = false;

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.Display.RenderedWorld += OnRenderedWorld;
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.player == null || Game1.paused)
                return;

            var player = Game1.player;

            // Check if player is using a tool
            bool isUsingTool = player.UsingTool;

            // Detect when tool usage starts (transition from not using to using)
            if (isUsingTool && !_wasUsingTool)
            {
                _lastStamina = player.stamina;
            }
            // Detect when tool usage ends (check for stamina drain)
            else if (!isUsingTool && _wasUsingTool)
            {
                HandleStaminaCost(player);
            }

            // Handle tool animation speed
            if (isUsingTool && player.CurrentTool != null)
            {
                HandleToolSpeed(player);
            }

            _wasUsingTool = isUsingTool;
        }

        private void HandleStaminaCost(Farmer player)
        {
            int currentCost = this.Config.ConstantCost;

            if (this.Config.UseAdaptive)
            {
                currentCost = GetAdaptiveValue(Game1.timeOfDay, "cost");
            }

            if (currentCost != 100 && player.stamina < _lastStamina)
            {
                // Calculate actual stamina used
                float staminaUsed = _lastStamina - player.stamina;

                // Calculate adjusted cost
                float adjustedCost = staminaUsed * (currentCost / 100f);

                // Refund or charge the difference
                float difference = staminaUsed - adjustedCost;
                player.stamina += difference;

                // Clamp to valid range
                if (player.stamina > player.MaxStamina)
                    player.stamina = player.MaxStamina;
                if (player.stamina < -15)
                    player.stamina = -15;
            }
        }

        private void HandleToolSpeed(Farmer player)
        {
            int currentSpeed = this.Config.ConstantSpeed;

            if (this.Config.UseAdaptive)
            {
                currentSpeed = GetAdaptiveValue(Game1.timeOfDay, "speed");
            }

            if (currentSpeed != 100)
            {
                // Modify animation speed
                float speedMultiplier = currentSpeed / 100f;

                // Adjust the farmer sprite timer
                if (player.FarmerSprite.currentSingleAnimation != -1)
                {
                    // Speed up the animation by reducing the interval between frames
                    float baseInterval = 20f; // Default tool animation interval
                    player.FarmerSprite.interval = baseInterval / speedMultiplier;
                }

                // Also adjust the tool's animation timer directly
                if (player.CurrentTool != null)
                {
                    // This helps with charging tools like the hoe and watering can
                    var toolTimer = typeof(StardewValley.Tool).GetField("upgradeLevel",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    // For charged tools, we need to adjust how quickly they charge
                    // This is handled by the animation speed above
                }
            }
        }

        private int GetAdaptiveValue(int timeOfDay, string type)
        {
            if (type == "cost")
            {
                if (timeOfDay >= 600 && timeOfDay < 900) return this.Config.Cost_0600_to_0900;
                if (timeOfDay >= 900 && timeOfDay < 1700) return this.Config.Cost_0900_to_1700;
                if (timeOfDay >= 1700 && timeOfDay < 2400) return this.Config.Cost_1700_to_2400;
                return this.Config.Cost_2400_to_0600;
            }
            else // speed
            {
                if (timeOfDay >= 600 && timeOfDay < 900) return this.Config.Speed_0600_to_0900;
                if (timeOfDay >= 900 && timeOfDay < 1700) return this.Config.Speed_0900_to_1700;
                if (timeOfDay >= 1700 && timeOfDay < 2400) return this.Config.Speed_1700_to_2400;
                return this.Config.Speed_2400_to_0600;
            }
        }

        // --- DEBUG MODE ---
        private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
        {
            if (this.Config.DebugMode && Context.IsWorldReady && Game1.player != null && !Game1.eventUp)
            {
                Vector2 screenPos = Game1.GlobalToLocal(Game1.viewport, Game1.player.Position);
                Vector2 textPos = new Vector2(screenPos.X, screenPos.Y - 120);

                int cost = this.Config.UseAdaptive ? GetAdaptiveValue(Game1.timeOfDay, "cost") : this.Config.ConstantCost;
                int speed = this.Config.UseAdaptive ? GetAdaptiveValue(Game1.timeOfDay, "speed") : this.Config.ConstantSpeed;

                string debugText = $"Tool Master\nCost: {cost}%\nSpeed: {speed}%\nTime: {Game1.timeOfDay}\nUsing Tool: {Game1.player.UsingTool}\nStamina: {(int)Game1.player.stamina}";

                e.SpriteBatch.DrawString(Game1.dialogueFont, debugText, textPos + new Vector2(2, 2), Color.Black);
                e.SpriteBatch.DrawString(Game1.dialogueFont, debugText, textPos, Color.Orange);
            }
        }

        // --- GMCM Registration ---
        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null) return;

            configMenu.Register(this.ModManifest, () => this.Config = new ModConfig(), () => this.Helper.WriteConfig(this.Config));

            // General
            configMenu.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("config.section.general"));

            configMenu.AddBoolOption(this.ModManifest, () => this.Config.DebugMode, val => this.Config.DebugMode = val,
                () => this.Helper.Translation.Get("config.debug.name"),
                () => this.Helper.Translation.Get("config.debug.tooltip"));

            configMenu.AddBoolOption(this.ModManifest, () => this.Config.UseAdaptive, val => this.Config.UseAdaptive = val,
                () => this.Helper.Translation.Get("config.adaptive.name"),
                () => this.Helper.Translation.Get("config.adaptive.tooltip"));

            // Constant Mode
            configMenu.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("config.section.constant"));

            configMenu.AddNumberOption(this.ModManifest, () => this.Config.ConstantCost, val => this.Config.ConstantCost = val,
                () => this.Helper.Translation.Get("config.cost.name"),
                () => this.Helper.Translation.Get("config.cost.tooltip"),
                10, 200, 5, val => $"{val}%");

            configMenu.AddNumberOption(this.ModManifest, () => this.Config.ConstantSpeed, val => this.Config.ConstantSpeed = val,
                () => this.Helper.Translation.Get("config.speed.name"),
                () => this.Helper.Translation.Get("config.speed.tooltip"),
                10, 200, 5, val => $"{val}%");

            // Adaptive Mode
            configMenu.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("config.section.adaptive"));
            configMenu.AddParagraph(this.ModManifest, () => this.Helper.Translation.Get("config.adaptive.note"));

            AddTimeslotOptions(configMenu, "config.time.6to9",
                () => this.Config.Cost_0600_to_0900, v => this.Config.Cost_0600_to_0900 = v,
                () => this.Config.Speed_0600_to_0900, v => this.Config.Speed_0600_to_0900 = v);

            AddTimeslotOptions(configMenu, "config.time.9to5",
                () => this.Config.Cost_0900_to_1700, v => this.Config.Cost_0900_to_1700 = v,
                () => this.Config.Speed_0900_to_1700, v => this.Config.Speed_0900_to_1700 = v);

            AddTimeslotOptions(configMenu, "config.time.5to12",
                () => this.Config.Cost_1700_to_2400, v => this.Config.Cost_1700_to_2400 = v,
                () => this.Config.Speed_1700_to_2400, v => this.Config.Speed_1700_to_2400 = v);

            AddTimeslotOptions(configMenu, "config.time.12to2",
                () => this.Config.Cost_2400_to_0600, v => this.Config.Cost_2400_to_0600 = v,
                () => this.Config.Speed_2400_to_0600, v => this.Config.Speed_2400_to_0600 = v);
        }

        private void AddTimeslotOptions(IGenericModConfigMenuApi api, string timeKey,
            Func<int> getCost, Action<int> setCost,
            Func<int> getSpeed, Action<int> setSpeed)
        {
            api.AddSubHeader(this.ModManifest, () => this.Helper.Translation.Get(timeKey));

            api.AddNumberOption(this.ModManifest, getCost, setCost,
                () => this.Helper.Translation.Get("config.cost.name"),
                () => this.Helper.Translation.Get("config.cost.tooltip"),
                10, 200, 5, val => $"{val}%");

            api.AddNumberOption(this.ModManifest, getSpeed, setSpeed,
                () => this.Helper.Translation.Get("config.speed.name"),
                () => this.Helper.Translation.Get("config.speed.tooltip"),
                10, 200, 5, val => $"{val}%");
        }
    }
}