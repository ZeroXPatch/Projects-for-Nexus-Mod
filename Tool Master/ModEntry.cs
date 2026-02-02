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
        private float _baseInterval = 0f;
        private int _lastAnimationFrame = -1;

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
            float currentSpeed = this.Config.ConstantSpeed;

            if (this.Config.UseAdaptive)
            {
                currentSpeed = GetAdaptiveSpeed(Game1.timeOfDay);
            }

            if (player.FarmerSprite.currentSingleAnimation != -1)
            {
                // Detect if this is a new animation
                if (player.FarmerSprite.currentAnimationIndex != _lastAnimationFrame)
                {
                    // Store the base interval when animation starts/changes
                    _baseInterval = player.FarmerSprite.interval;
                    _lastAnimationFrame = player.FarmerSprite.currentAnimationIndex;
                }

                if (currentSpeed != 1.0f && _baseInterval > 0)
                {
                    // Clamp speed to safe range
                    currentSpeed = Math.Clamp(currentSpeed, 0.1f, 5f);

                    // currentSpeed is now a direct multiplier
                    // 0.1 = very slow, 1.0 = normal, 5.0 = 5x faster
                    // In Stardew, lower interval = faster, so we divide
                    float newInterval = _baseInterval / currentSpeed;

                    // Safety check: don't let interval go below 1ms to prevent issues
                    if (newInterval < 1f)
                        newInterval = 1f;

                    player.FarmerSprite.interval = newInterval;
                }
            }
            else
            {
                // Reset when no animation is playing
                _baseInterval = 0f;
                _lastAnimationFrame = -1;
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
            return 100; // Default for cost
        }

        private float GetAdaptiveSpeed(int timeOfDay)
        {
            if (timeOfDay >= 600 && timeOfDay < 900) return this.Config.Speed_0600_to_0900;
            if (timeOfDay >= 900 && timeOfDay < 1700) return this.Config.Speed_0900_to_1700;
            if (timeOfDay >= 1700 && timeOfDay < 2400) return this.Config.Speed_1700_to_2400;
            return this.Config.Speed_2400_to_0600;
        }

        // --- DEBUG MODE ---
        private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
        {
            if (this.Config.DebugMode && Context.IsWorldReady && Game1.player != null && !Game1.eventUp)
            {
                Vector2 screenPos = Game1.GlobalToLocal(Game1.viewport, Game1.player.Position);
                Vector2 textPos = new Vector2(screenPos.X, screenPos.Y - 120);

                int cost = this.Config.UseAdaptive ? GetAdaptiveValue(Game1.timeOfDay, "cost") : this.Config.ConstantCost;
                float speed = this.Config.UseAdaptive ? GetAdaptiveSpeed(Game1.timeOfDay) : this.Config.ConstantSpeed;

                string debugText = $"Tool Master\nCost: {cost}%\nSpeed: {speed:F1}x\nTime: {Game1.timeOfDay}\nUsing Tool: {Game1.player.UsingTool}\nStamina: {(int)Game1.player.stamina}";

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
                0.1f, 5f, 0.1f, val => $"{val:F1}x");

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
            Func<float> getSpeed, Action<float> setSpeed)
        {
            api.AddSubHeader(this.ModManifest, () => this.Helper.Translation.Get(timeKey));

            api.AddNumberOption(this.ModManifest, getCost, setCost,
                () => this.Helper.Translation.Get("config.cost.name"),
                () => this.Helper.Translation.Get("config.cost.tooltip"),
                10, 200, 5, val => $"{val}%");

            api.AddNumberOption(this.ModManifest, getSpeed, setSpeed,
                () => this.Helper.Translation.Get("config.speed.name"),
                () => this.Helper.Translation.Get("config.speed.tooltip"),
                0.1f, 5f, 0.1f, val => $"{val:F1}x");
        }
    }
}