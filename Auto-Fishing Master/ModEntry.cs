using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;

namespace AutoFishingMaster
{
    public class ModEntry : Mod
    {
        private ModConfig Config;
        private IReflectedField<MouseState> _currentMouseState;
        private int _debugCounter = 0;
        private int _castCooldown = 0;
        private int _fishingRodSlot = -1;

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.Input.ButtonPressed += OnButtonPressed;

            this.Monitor.Log("Auto-Fishing Master initialized", LogLevel.Info);
        }

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            // Get mouse state reflection for dismissing fish screen
            this._currentMouseState = this.Helper.Reflection.GetField<MouseState>(
                (object)Game1.input,
                "_currentMouseState",
                false
            );

            this.Monitor.Log("Game launched, setting up config menu", LogLevel.Debug);

            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu != null)
            {
                configMenu.Register(
                    mod: this.ModManifest,
                    reset: () => this.Config = new ModConfig(),
                    save: () => this.Helper.WriteConfig(this.Config)
                );

                configMenu.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("config.general.title"));
                configMenu.AddBoolOption(
                    this.ModManifest,
                    () => this.Config.EnableMod,
                    (val) => this.Config.EnableMod = val,
                    () => this.Helper.Translation.Get("config.enableMod.name"),
                    () => this.Helper.Translation.Get("config.enableMod.description")
                );

                configMenu.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("config.controls.title"));
                configMenu.AddKeybind(
                    this.ModManifest,
                    () => this.Config.ToggleKey,
                    (val) => this.Config.ToggleKey = val,
                    () => this.Helper.Translation.Get("config.toggleKey.name"),
                    () => this.Helper.Translation.Get("config.toggleKey.description")
                );

                configMenu.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("config.automation.title"));
                configMenu.AddBoolOption(
                    this.ModManifest,
                    () => this.Config.AutoCast,
                    (val) => this.Config.AutoCast = val,
                    () => this.Helper.Translation.Get("config.autoCast.name"),
                    () => this.Helper.Translation.Get("config.autoCast.description")
                );
                configMenu.AddBoolOption(
                    this.ModManifest,
                    () => this.Config.AlwaysMaxCastPower,
                    (val) => this.Config.AlwaysMaxCastPower = val,
                    () => this.Helper.Translation.Get("config.alwaysMaxCastPower.name"),
                    () => this.Helper.Translation.Get("config.alwaysMaxCastPower.description")
                );
                configMenu.AddBoolOption(
                    this.ModManifest,
                    () => this.Config.AutoHit,
                    (val) => this.Config.AutoHit = val,
                    () => this.Helper.Translation.Get("config.autoHit.name"),
                    () => this.Helper.Translation.Get("config.autoHit.description")
                );

                configMenu.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("config.rewards.title"));
                configMenu.AddBoolOption(
                    this.ModManifest,
                    () => this.Config.AutoLootTreasure,
                    (val) => this.Config.AutoLootTreasure = val,
                    () => this.Helper.Translation.Get("config.autoLootTreasure.name"),
                    () => this.Helper.Translation.Get("config.autoLootTreasure.description")
                );
                configMenu.AddBoolOption(
                    this.ModManifest,
                    () => this.Config.AlwaysPerfect,
                    (val) => this.Config.AlwaysPerfect = val,
                    () => this.Helper.Translation.Get("config.alwaysPerfect.name"),
                    () => this.Helper.Translation.Get("config.alwaysPerfect.description")
                );

                configMenu.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("config.safety.title"));
                configMenu.AddBoolOption(
                    this.ModManifest,
                    () => this.Config.EnableStaminaCheck,
                    (val) => this.Config.EnableStaminaCheck = val,
                    () => this.Helper.Translation.Get("config.enableStaminaCheck.name"),
                    () => this.Helper.Translation.Get("config.enableStaminaCheck.description")
                );
                configMenu.AddNumberOption(
                    this.ModManifest,
                    () => this.Config.StaminaThreshold,
                    (val) => this.Config.StaminaThreshold = val,
                    () => this.Helper.Translation.Get("config.staminaThreshold.name"),
                    () => this.Helper.Translation.Get("config.staminaThreshold.description"),
                    min: 1,
                    max: 100,
                    interval: 1
                );

                configMenu.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("config.debug.title"));
                configMenu.AddBoolOption(
                    this.ModManifest,
                    () => this.Config.DebugMode,
                    (val) => this.Config.DebugMode = val,
                    () => this.Helper.Translation.Get("config.debugMode.name"),
                    () => this.Helper.Translation.Get("config.debugMode.description")
                );

                this.Monitor.Log("Config menu registered successfully", LogLevel.Debug);
            }
            else
            {
                this.Monitor.Log("Generic Mod Config Menu not found", LogLevel.Warn);
            }
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady) return;

            if (e.Button == this.Config.ToggleKey)
            {
                this.Config.EnableMod = !this.Config.EnableMod;
                string status = this.Config.EnableMod ? "ON" : "OFF";
                Game1.addHUDMessage(new HUDMessage($"Auto-Fishing Master: {status}", 2));
                this.Monitor.Log($"Mod toggled {status}", LogLevel.Info);
            }
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.player == null)
                return;

            // Decrement cast cooldown
            if (_castCooldown > 0)
                _castCooldown--;

            // Check stamina threshold and auto-disable if needed
            if (this.Config.EnableMod && this.Config.EnableStaminaCheck)
            {
                if (Game1.player.stamina <= this.Config.StaminaThreshold)
                {
                    this.Config.EnableMod = false;
                    Game1.addHUDMessage(new HUDMessage($"Auto-Fishing stopped: Low energy ({(int)Game1.player.stamina}/{this.Config.StaminaThreshold})", 3));
                    this.Monitor.Log($"Mod auto-disabled due to low stamina: {Game1.player.stamina:F1}/{this.Config.StaminaThreshold}", LogLevel.Info);
                    return;
                }
            }

            if (!this.Config.EnableMod)
                return;

            // Debug logging every 60 ticks (1 second) - only if debug mode is enabled
            if (this.Config.DebugMode)
            {
                _debugCounter++;
                if (_debugCounter >= 60)
                {
                    _debugCounter = 0;
                    LogCurrentState();
                }
            }

            // Get fishing rod
            if (Game1.player.CurrentTool is not FishingRod rod)
            {
                // If we had a rod before but now it's not selected, try to re-select it
                if (_fishingRodSlot >= 0 && Game1.player.Items[_fishingRodSlot] is FishingRod)
                {
                    Game1.player.CurrentToolIndex = _fishingRodSlot;
                    if (this.Config.DebugMode)
                        this.Monitor.Log($"Auto re-selected fishing rod in slot {_fishingRodSlot}", LogLevel.Debug);
                    return; // Wait for next tick to continue with the rod
                }
                return;
            }

            // Track the fishing rod's inventory slot
            int currentRodSlot = Game1.player.CurrentToolIndex;
            if (_fishingRodSlot != currentRodSlot)
            {
                _fishingRodSlot = currentRodSlot;
                if (this.Config.DebugMode)
                    this.Monitor.Log($"Fishing rod detected in slot {_fishingRodSlot}", LogLevel.Trace);
            }

            // Stop any stuck reeling sounds
            StopReelingSound(rod);

            // Handle treasure chest - do this first
            if (this.Config.AutoLootTreasure && Game1.activeClickableMenu is ItemGrabMenu itemMenu)
            {
                if (itemMenu.source == ItemGrabMenu.source_fishingChest)
                {
                    if (this.Config.DebugMode)
                        this.Monitor.Log("Treasure chest detected, collecting...", LogLevel.Debug);
                    CollectTreasure(itemMenu);
                    // Set cooldown to prevent immediate recast
                    _castCooldown = 60;
                    if (this.Config.DebugMode)
                        this.Monitor.Log("Cast cooldown set: 60 ticks (1s) after treasure", LogLevel.Debug);
                    return;
                }
            }

            // Dismiss fish caught screen
            if (ShowingFish(rod))
            {
                if (this.Config.DebugMode)
                    this.Monitor.Log("Fish caught screen detected, dismissing...", LogLevel.Debug);
                DismissFishScreen(rod);
                // Set cooldown to prevent immediate recast (45 ticks = 0.75 seconds)
                _castCooldown = 45;
                if (this.Config.DebugMode)
                    this.Monitor.Log("Cast cooldown set: 45 ticks (0.75s) after fish screen dismiss", LogLevel.Debug);
                return;
            }

            // Handle BobberBar (fishing minigame)
            // ONLY skip if AutoHit is enabled, otherwise let player play the minigame
            if (this.Config.AutoHit && Game1.activeClickableMenu is BobberBar bobberMenu)
            {
                if (this.Config.DebugMode)
                    this.Monitor.Log("BobberBar detected, instant catching...", LogLevel.Debug);
                InstantCatchFish(rod, bobberMenu);
                return;
            }

            // Auto Hook when fish bites - only if AutoHit is enabled
            if (this.Config.AutoHit && CanAutoHook(rod))
            {
                if (this.Config.DebugMode)
                    this.Monitor.Log("Fish nibbling detected, hooking...", LogLevel.Debug);
                HookFish(rod);
                return;
            }

            // Auto Cast
            if (this.Config.AutoCast && CanAutoCast(rod))
            {
                if (this.Config.DebugMode)
                    this.Monitor.Log("Auto-casting...", LogLevel.Debug);
                AutoCast(rod);
            }
            else if (this.Config.AutoCast && _castCooldown > 0 && this.Config.DebugMode)
            {
                // Only log this occasionally to avoid spam
                if (_castCooldown % 15 == 0)
                    this.Monitor.Log($"Auto-cast on cooldown: {_castCooldown} ticks remaining", LogLevel.Trace);
            }
        }

        private void LogCurrentState()
        {
            if (Game1.player.CurrentTool is FishingRod rod)
            {
                this.Monitor.Log($"=== STATE DEBUG ===", LogLevel.Trace);
                this.Monitor.Log($"Mod Enabled: {this.Config.EnableMod}", LogLevel.Trace);
                this.Monitor.Log($"Cast Cooldown: {_castCooldown} ticks", LogLevel.Trace);
                this.Monitor.Log($"Fishing Rod Slot: {_fishingRodSlot}, Current Slot: {Game1.player.CurrentToolIndex}", LogLevel.Trace);
                this.Monitor.Log($"AutoCast: {this.Config.AutoCast}, AutoHit: {this.Config.AutoHit}, MaxPower: {this.Config.AlwaysMaxCastPower}", LogLevel.Trace);
                this.Monitor.Log($"Rod States - isCasting: {rod.isCasting}, isFishing: {rod.isFishing}, isNibbling: {rod.isNibbling}, isReeling: {rod.isReeling}", LogLevel.Trace);
                this.Monitor.Log($"Rod States - pullingOut: {rod.pullingOutOfWater}, bobberInAir: {rod.castedButBobberStillInAir}, fishCaught: {rod.fishCaught}, hit: {rod.hit}", LogLevel.Trace);
                this.Monitor.Log($"Player - UsingTool: {Game1.player.UsingTool}, Stamina: {Game1.player.stamina}", LogLevel.Trace);
                this.Monitor.Log($"Context - CanPlayerMove: {Context.CanPlayerMove}, ActiveMenu: {Game1.activeClickableMenu != null}", LogLevel.Trace);
            }
        }

        private bool ShowingFish(FishingRod rod)
        {
            return !Context.CanPlayerMove &&
                   rod.fishCaught &&
                   rod.inUse() &&
                   !rod.isCasting &&
                   !rod.isReeling &&
                   !rod.pullingOutOfWater &&
                   !rod.showingTreasure;
        }

        private bool CanAutoCast(FishingRod rod)
        {
            // Check if cooldown is active
            if (_castCooldown > 0)
                return false;

            return Context.CanPlayerMove &&
                   Game1.activeClickableMenu == null &&
                   !Game1.isFestival() &&
                   !rod.castedButBobberStillInAir &&
                   !rod.hit &&
                   !rod.inUse() &&
                   !rod.isCasting &&
                   !rod.isFishing &&
                   !rod.isNibbling &&
                   !rod.isReeling &&
                   !rod.pullingOutOfWater &&
                   Game1.player.stamina > 1.0f;
        }

        private bool CanAutoHook(FishingRod rod)
        {
            return rod.isNibbling &&
                   !rod.isReeling &&
                   !rod.hit &&
                   !rod.pullingOutOfWater &&
                   !rod.fishCaught;
        }

        private void AutoCast(FishingRod rod)
        {
            try
            {
                // Get player standing position
                Vector2 playerPos = Game1.player.getStandingPosition();

                if (this.Config.DebugMode)
                    this.Monitor.Log($"Casting from position: {playerPos}", LogLevel.Trace);

                // Begin using - this starts the cast
                rod.beginUsing(Game1.currentLocation, (int)playerPos.X, (int)playerPos.Y, Game1.player);

                // Set casting power - the game will complete the cast automatically
                if (this.Config.AlwaysMaxCastPower)
                {
                    rod.castingPower = 1.01f;
                    if (this.Config.DebugMode)
                        this.Monitor.Log("Max casting power set", LogLevel.Trace);
                }

                if (this.Config.DebugMode)
                    this.Monitor.Log("Cast initiated successfully", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Error in AutoCast: {ex.Message}", LogLevel.Error);
            }
        }

        private void HookFish(FishingRod rod)
        {
            try
            {
                rod.timePerBobberBob = 1f;
                rod.timeUntilFishingNibbleDone = 600f;
                rod.DoFunction(Game1.player.currentLocation, (int)rod.bobber.X, (int)rod.bobber.Y, 1, Game1.player);

                if (this.Config.DebugMode)
                    this.Monitor.Log("Fish hooked successfully", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Error in HookFish: {ex.Message}", LogLevel.Error);
            }
        }

        private void InstantCatchFish(FishingRod rod, BobberBar bobberMenu)
        {
            try
            {
                if (this.Config.DebugMode)
                    this.Monitor.Log("Starting instant catch...", LogLevel.Debug);

                // Stop reeling sound
                StopReelingSound(rod);

                // Get fish data
                string whichFish = this.Helper.Reflection.GetField<string>(bobberMenu, "whichFish").GetValue();
                int fishSize = this.Helper.Reflection.GetField<int>(bobberMenu, "fishSize").GetValue();
                int fishQuality = this.Helper.Reflection.GetField<int>(bobberMenu, "fishQuality").GetValue(); // Always use actual quality
                float difficulty = this.Helper.Reflection.GetField<float>(bobberMenu, "difficulty").GetValue();
                bool treasure = this.Helper.Reflection.GetField<bool>(bobberMenu, "treasure").GetValue(); // Always pass actual treasure status
                bool perfect = this.Config.AlwaysPerfect || this.Helper.Reflection.GetField<bool>(bobberMenu, "perfect").GetValue(); // AlwaysPerfect only affects this
                bool fromFishPond = this.Helper.Reflection.GetField<bool>(bobberMenu, "fromFishPond").GetValue();
                string setFlagOnCatch = this.Helper.Reflection.GetField<string>(bobberMenu, "setFlagOnCatch").GetValue();
                bool isBossFish = this.Helper.Reflection.GetField<bool>(bobberMenu, "bossFish").GetValue();

                if (this.Config.DebugMode)
                    this.Monitor.Log($"Catching fish: {whichFish}, Quality: {fishQuality}, Treasure: {treasure}", LogLevel.Debug);

                // Get qualified item ID
                string qualifiedItemId = ItemRegistry.GetData(whichFish)?.QualifiedItemId ?? "(O)" + whichFish;

                // Calculate number caught based on bait
                int numCaught = 1;
                var bait = rod.GetBait();
                if (bait != null)
                {
                    string baitId = bait.QualifiedItemId;
                    string baitName = bait.Name;

                    if (this.Config.DebugMode)
                        this.Monitor.Log($"Bait ID: {baitId}, Bait Name: {baitName}, Perfect: {perfect}", LogLevel.Debug);

                    if (baitId == "(O)774" && Game1.random.NextDouble() < 0.25 + Game1.player.DailyLuck / 2.0)
                    {
                        numCaught = 2;
                        if (this.Config.DebugMode)
                            this.Monitor.Log("Wild Bait triggered: 2 fish", LogLevel.Debug);
                    }
                    else if (baitName.Contains("Challenge Bait") && perfect)
                    {
                        numCaught = 3;
                        if (this.Config.DebugMode)
                            this.Monitor.Log("Challenge Bait triggered: 3 fish", LogLevel.Debug);
                    }
                }
                else
                {
                    if (this.Config.DebugMode)
                        this.Monitor.Log("No bait attached", LogLevel.Debug);
                }

                // Handle festival perfect fishing
                if (Game1.isFestival())
                {
                    Game1.CurrentEvent?.perfectFishing();
                }

                if (this.Config.DebugMode)
                    this.Monitor.Log($"Pulling fish from water: numCaught={numCaught}, perfect={perfect}, quality={fishQuality}", LogLevel.Debug);

                // Pull fish from water
                rod.pullFishFromWater(
                    qualifiedItemId,
                    fishSize,
                    fishQuality,
                    (int)difficulty,
                    treasure,
                    perfect,
                    fromFishPond,
                    setFlagOnCatch,
                    isBossFish,
                    numCaught
                );

                // Close menu and reset
                Game1.exitActiveMenu();
                Game1.setRichPresence("location", Game1.currentLocation.Name);
                // Don't call resetState here - let the game handle it naturally
                // rod.resetState();

                if (this.Config.DebugMode)
                    this.Monitor.Log("Instant catch complete", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Error in InstantCatchFish: {ex.Message}", LogLevel.Error);
                Game1.exitActiveMenu();
            }
        }

        private void DismissFishScreen(FishingRod rod)
        {
            if (Game1.isFestival())
                return;

            try
            {
                if (this.Config.DebugMode)
                    this.Monitor.Log("Dismissing fish screen...", LogLevel.Debug);

                // Simulate left mouse click
                if (this._currentMouseState != null)
                {
                    MouseState clickState = new MouseState(
                        Game1.viewport.Width / 2,
                        Game1.viewport.Height / 2,
                        0,
                        ButtonState.Pressed,
                        ButtonState.Released,
                        ButtonState.Released,
                        ButtonState.Released,
                        ButtonState.Released
                    );

                    this._currentMouseState.SetValue(clickState);
                    Game1.oldKBState = Keyboard.GetState();
                }

                // Update rod
                rod.tickUpdate(Game1.currentGameTime, Game1.player);

                // Re-select the fishing rod after dismissing fish screen
                if (_fishingRodSlot >= 0 && Game1.player.CurrentToolIndex != _fishingRodSlot)
                {
                    Game1.player.CurrentToolIndex = _fishingRodSlot;
                    if (this.Config.DebugMode)
                        this.Monitor.Log($"Re-selected fishing rod in slot {_fishingRodSlot}", LogLevel.Debug);
                }

                if (this.Config.DebugMode)
                    this.Monitor.Log("Fish screen dismissed", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Error dismissing fish screen: {ex.Message}", LogLevel.Error);
            }
        }

        private void CollectTreasure(ItemGrabMenu itemMenu)
        {
            try
            {
                if (this.Config.DebugMode)
                    this.Monitor.Log("Starting treasure collection...", LogLevel.Debug);

                if (itemMenu.ItemsToGrabMenu?.actualInventory != null)
                {
                    int itemCount = 0;
                    foreach (Item item in itemMenu.ItemsToGrabMenu.actualInventory)
                    {
                        if (item != null)
                        {
                            itemCount++;
                            Item leftOver = Game1.player.addItemToInventory(item);
                            if (leftOver != null)
                            {
                                if (this.Config.DebugMode)
                                    this.Monitor.Log($"Inventory full, dropping: {leftOver.Name}", LogLevel.Debug);
                                Game1.createItemDebris(leftOver, Game1.player.getStandingPosition(), 2);
                            }
                            else
                            {
                                if (this.Config.DebugMode)
                                    this.Monitor.Log($"Collected: {item.Name}", LogLevel.Debug);
                            }
                        }
                    }
                    if (this.Config.DebugMode)
                        this.Monitor.Log($"Total items collected: {itemCount}", LogLevel.Debug);
                    itemMenu.ItemsToGrabMenu.actualInventory.Clear();
                }
                Game1.exitActiveMenu();

                // Re-select the fishing rod after menu closes
                if (_fishingRodSlot >= 0 && Game1.player.CurrentToolIndex != _fishingRodSlot)
                {
                    Game1.player.CurrentToolIndex = _fishingRodSlot;
                    if (this.Config.DebugMode)
                        this.Monitor.Log($"Re-selected fishing rod in slot {_fishingRodSlot}", LogLevel.Debug);
                }

                if (this.Config.DebugMode)
                    this.Monitor.Log("Treasure collection complete", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Error collecting treasure: {ex.Message}", LogLevel.Error);
                Game1.exitActiveMenu();
            }
        }

        private void StopReelingSound(FishingRod rod)
        {
            if (rod.isReeling || rod.pullingOutOfWater)
                return;

            try
            {
                var soundField = this.Helper.Reflection.GetField<object>(rod, "reelingSound", false);
                if (soundField != null)
                {
                    dynamic cue = soundField.GetValue();
                    if (cue != null && cue.IsPlaying)
                    {
                        cue.Stop(Microsoft.Xna.Framework.Audio.AudioStopOptions.Immediate);
                    }
                }
            }
            catch { }
        }
    }
}