using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using StardewValley;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ZeroXPatch
{
    /// <summary>Harmony patches for Automate mod.</summary>
    internal static class AutomatePatches
    {
        private static long updateCyclesChecked = 0;
        private static long updateCyclesSkipped = 0;
        private static long updateCyclesProcessed = 0;

        // Idle detection tracking
        private static Vector2 lastPlayerPosition = Vector2.Zero;
        private static int ticksSinceLastMove = 0;
        private static bool isPlayerIdle = false;

        // Input activity tracking
        private static MouseState lastMouseState;
        private static KeyboardState lastKeyboardState;
        private static GamePadState lastGamePadState;
        private static bool inputStatesInitialized = false;

        /// <summary>Apply all Harmony patches.</summary>
        public static void Apply(Harmony harmony)
        {
            try
            {
                // Find Automate's assembly
                var automateAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Automate");

                if (automateAssembly == null)
                {
                    ModEntry.ModMonitor.Log("Could not find Automate assembly", StardewModdingAPI.LogLevel.Warn);
                    return;
                }

                // PATCH AT THE HIGHEST LEVEL - MachineManager.AutomateAll()
                // This stops Automate from even iterating through machine groups
                var machineManagerType = automateAssembly.GetType("Pathoschild.Stardew.Automate.Framework.MachineManager");
                if (machineManagerType != null)
                {
                    // Try to find the automation method - it might be called different things in different versions
                    MethodInfo automateAllMethod = null;

                    // Try common method names
                    string[] possibleMethodNames = { "AutomateAll", "Automate", "OnUpdateTicked" };
                    foreach (var methodName in possibleMethodNames)
                    {
                        automateAllMethod = machineManagerType.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (automateAllMethod != null)
                        {
                            ModEntry.ModMonitor.Log($"Found automation method: {methodName}", StardewModdingAPI.LogLevel.Debug);
                            break;
                        }
                    }

                    if (automateAllMethod != null)
                    {
                        harmony.Patch(
                            original: automateAllMethod,
                            prefix: new HarmonyMethod(typeof(AutomatePatches), nameof(MachineManagerAutomatePrefix))
                        );
                        ModEntry.ModMonitor.Log($"Successfully patched MachineManager at the top level for ZERO overhead!", StardewModdingAPI.LogLevel.Info);
                    }
                    else
                    {
                        ModEntry.ModMonitor.Log("Could not find MachineManager automation method - trying fallback patch", StardewModdingAPI.LogLevel.Warn);

                        // FALLBACK: Patch MachineGroup.Automate if we can't find the manager method
                        var machineGroupType = automateAssembly.GetType("Pathoschild.Stardew.Automate.Framework.MachineGroup");
                        if (machineGroupType != null)
                        {
                            var automateGroupMethod = machineGroupType.GetMethod("Automate", BindingFlags.Public | BindingFlags.Instance);
                            if (automateGroupMethod != null)
                            {
                                harmony.Patch(
                                    original: automateGroupMethod,
                                    prefix: new HarmonyMethod(typeof(AutomatePatches), nameof(MachineGroupAutomatePrefix))
                                );
                                ModEntry.ModMonitor.Log("Applied fallback patch to MachineGroup.Automate", StardewModdingAPI.LogLevel.Info);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.ModMonitor.Log($"Error in Apply patches: {ex}", StardewModdingAPI.LogLevel.Error);
            }
        }

        // Cached menu types for performance
        private static Type dialogueBoxType = null;

        /// <summary>Check if player is in a cutscene, event, or dialogue.</summary>
        private static bool IsPlayerInCutsceneOrDialogue()
        {
            if (Game1.player == null) return false;

            // Check if in event/cutscene (cheapest check first)
            if (Game1.eventUp || Game1.CurrentEvent != null)
                return true;

            // Additional check for any NPC dialogue
            if (Game1.currentSpeaker != null)
                return true;

            // Check if in dialogue menu
            if (Game1.activeClickableMenu != null)
            {
                // Cache DialogueBox type on first use
                if (dialogueBoxType == null)
                {
                    dialogueBoxType = Type.GetType("StardewValley.Menus.DialogueBox, Stardew Valley");
                }

                if (dialogueBoxType != null && Game1.activeClickableMenu.GetType() == dialogueBoxType)
                    return true;
            }

            // Check if currently in location event
            if (Game1.player.currentLocation?.currentEvent != null)
                return true;

            return false;
        }

        /// <summary>Detect if there's been any input activity (mouse, keyboard, controller, touch).</summary>
        private static bool HasInputActivity()
        {
            try
            {
                // Initialize states on first run
                if (!inputStatesInitialized)
                {
                    lastMouseState = Mouse.GetState();
                    lastKeyboardState = Keyboard.GetState();
                    lastGamePadState = GamePad.GetState(PlayerIndex.One);
                    inputStatesInitialized = true;
                    return false; // No activity on first check
                }

                bool hasActivity = false;

                // Check mouse activity
                MouseState currentMouseState = Mouse.GetState();
                if (currentMouseState.LeftButton != lastMouseState.LeftButton ||
                    currentMouseState.RightButton != lastMouseState.RightButton ||
                    currentMouseState.MiddleButton != lastMouseState.MiddleButton ||
                    currentMouseState.XButton1 != lastMouseState.XButton1 ||
                    currentMouseState.XButton2 != lastMouseState.XButton2 ||
                    currentMouseState.X != lastMouseState.X ||
                    currentMouseState.Y != lastMouseState.Y ||
                    currentMouseState.ScrollWheelValue != lastMouseState.ScrollWheelValue)
                {
                    hasActivity = true;
                    if (ModEntry.Config.DebugMode)
                    {
                        ModEntry.ModMonitor.Log("[DEBUG] Mouse activity detected", StardewModdingAPI.LogLevel.Trace);
                    }
                }

                // Check keyboard activity
                KeyboardState currentKeyboardState = Keyboard.GetState();
                var currentPressedKeys = currentKeyboardState.GetPressedKeys();
                var lastPressedKeys = lastKeyboardState.GetPressedKeys();

                if (currentPressedKeys.Length != lastPressedKeys.Length ||
                    !currentPressedKeys.OrderBy(k => k).SequenceEqual(lastPressedKeys.OrderBy(k => k)))
                {
                    hasActivity = true;
                    if (ModEntry.Config.DebugMode)
                    {
                        ModEntry.ModMonitor.Log("[DEBUG] Keyboard activity detected", StardewModdingAPI.LogLevel.Trace);
                    }
                }

                // Check gamepad activity (all buttons and triggers)
                GamePadState currentGamePadState = GamePad.GetState(PlayerIndex.One);
                if (currentGamePadState.IsConnected)
                {
                    // Check all buttons
                    if (currentGamePadState.Buttons.A != lastGamePadState.Buttons.A ||
                        currentGamePadState.Buttons.B != lastGamePadState.Buttons.B ||
                        currentGamePadState.Buttons.X != lastGamePadState.Buttons.X ||
                        currentGamePadState.Buttons.Y != lastGamePadState.Buttons.Y ||
                        currentGamePadState.Buttons.Start != lastGamePadState.Buttons.Start ||
                        currentGamePadState.Buttons.Back != lastGamePadState.Buttons.Back ||
                        currentGamePadState.Buttons.LeftShoulder != lastGamePadState.Buttons.LeftShoulder ||
                        currentGamePadState.Buttons.RightShoulder != lastGamePadState.Buttons.RightShoulder ||
                        currentGamePadState.Buttons.LeftStick != lastGamePadState.Buttons.LeftStick ||
                        currentGamePadState.Buttons.RightStick != lastGamePadState.Buttons.RightStick ||
                        currentGamePadState.DPad.Up != lastGamePadState.DPad.Up ||
                        currentGamePadState.DPad.Down != lastGamePadState.DPad.Down ||
                        currentGamePadState.DPad.Left != lastGamePadState.DPad.Left ||
                        currentGamePadState.DPad.Right != lastGamePadState.DPad.Right)
                    {
                        hasActivity = true;
                        if (ModEntry.Config.DebugMode)
                        {
                            ModEntry.ModMonitor.Log("[DEBUG] Controller button activity detected", StardewModdingAPI.LogLevel.Trace);
                        }
                    }

                    // Check thumbsticks (significant movement threshold to avoid drift)
                    const float thumbstickThreshold = 0.1f;
                    if (Math.Abs(currentGamePadState.ThumbSticks.Left.X - lastGamePadState.ThumbSticks.Left.X) > thumbstickThreshold ||
                        Math.Abs(currentGamePadState.ThumbSticks.Left.Y - lastGamePadState.ThumbSticks.Left.Y) > thumbstickThreshold ||
                        Math.Abs(currentGamePadState.ThumbSticks.Right.X - lastGamePadState.ThumbSticks.Right.X) > thumbstickThreshold ||
                        Math.Abs(currentGamePadState.ThumbSticks.Right.Y - lastGamePadState.ThumbSticks.Right.Y) > thumbstickThreshold)
                    {
                        hasActivity = true;
                        if (ModEntry.Config.DebugMode)
                        {
                            ModEntry.ModMonitor.Log("[DEBUG] Controller thumbstick activity detected", StardewModdingAPI.LogLevel.Trace);
                        }
                    }

                    // Check triggers (significant movement threshold)
                    const float triggerThreshold = 0.1f;
                    if (Math.Abs(currentGamePadState.Triggers.Left - lastGamePadState.Triggers.Left) > triggerThreshold ||
                        Math.Abs(currentGamePadState.Triggers.Right - lastGamePadState.Triggers.Right) > triggerThreshold)
                    {
                        hasActivity = true;
                        if (ModEntry.Config.DebugMode)
                        {
                            ModEntry.ModMonitor.Log("[DEBUG] Controller trigger activity detected", StardewModdingAPI.LogLevel.Trace);
                        }
                    }
                }

                // Update last states
                lastMouseState = currentMouseState;
                lastKeyboardState = currentKeyboardState;
                lastGamePadState = currentGamePadState;

                return hasActivity;
            }
            catch (Exception ex)
            {
                if (ModEntry.Config.DebugMode)
                {
                    ModEntry.ModMonitor.Log($"[DEBUG] Error detecting input activity: {ex.Message}", StardewModdingAPI.LogLevel.Trace);
                }
                return false; // Fail safely - assume no activity
            }
        }

        /// <summary>Update idle detection tracking.</summary>
        public static void UpdateIdleDetection()
        {
            if (!ModEntry.Config.OnlyProcessWhenIdle)
            {
                isPlayerIdle = true; // Always consider idle if feature is disabled
                return;
            }

            if (Game1.player == null)
            {
                isPlayerIdle = false;
                return;
            }

            Vector2 currentPosition = Game1.player.Position;

            // Check if we should pause the timer during cutscenes
            if (ModEntry.Config.PauseTimerDuringCutscenes)
            {
                if (IsPlayerInCutsceneOrDialogue())
                {
                    // Don't reset the timer, just don't increment it
                    // This preserves any idle time already accumulated
                    if (ModEntry.Config.DebugMode && ticksSinceLastMove > 0 && ticksSinceLastMove % 180 == 0) // Log every 3 seconds to reduce spam
                    {
                        ModEntry.ModMonitor.Log($"[DEBUG] Player in cutscene/dialogue - idle timer paused at {ticksSinceLastMove} ticks", StardewModdingAPI.LogLevel.Trace);
                    }
                    return;
                }
            }

            // Check if we should pause the timer on any input activity
            if (ModEntry.Config.PauseTimerOnInput)
            {
                if (HasInputActivity())
                {
                    // Input detected - reset timer since player is actively interacting
                    ticksSinceLastMove = 0;
                    isPlayerIdle = false;
                    // Update last position to current to avoid double-reset from movement check
                    lastPlayerPosition = currentPosition;

                    if (ModEntry.Config.DebugMode)
                    {
                        ModEntry.ModMonitor.Log($"[DEBUG] Input activity detected - resetting idle timer", StardewModdingAPI.LogLevel.Trace);
                    }
                    return;
                }
            }

            // Check if player has moved
            if (currentPosition != lastPlayerPosition)
            {
                // Player moved - reset idle counter
                ticksSinceLastMove = 0;
                isPlayerIdle = false;
                lastPlayerPosition = currentPosition;

                if (ModEntry.Config.DebugMode)
                {
                    ModEntry.ModMonitor.Log($"[DEBUG] Player moved - resetting idle timer", StardewModdingAPI.LogLevel.Trace);
                }
            }
            else
            {
                // Player hasn't moved - increment counter
                ticksSinceLastMove++;

                // Check if player has been idle long enough
                if (!isPlayerIdle && ticksSinceLastMove >= ModEntry.Config.IdleTicksThreshold)
                {
                    isPlayerIdle = true;

                    if (ModEntry.Config.DebugMode)
                    {
                        ModEntry.ModMonitor.Log($"[DEBUG] Player is now IDLE (after {ticksSinceLastMove} ticks)", StardewModdingAPI.LogLevel.Debug);
                    }
                }
                // Handle case where threshold was lowered mid-game and timer already exceeded it
                else if (isPlayerIdle && ticksSinceLastMove < ModEntry.Config.IdleTicksThreshold)
                {
                    // Threshold was increased mid-game, reset idle state
                    isPlayerIdle = false;
                }
            }
        }

        /// <summary>Get whether the player is currently considered idle.</summary>
        public static bool IsPlayerIdle()
        {
            return isPlayerIdle;
        }

        /// <summary>
        /// Prefix for MachineManager automation methods.
        /// This stops ALL Automate processing before it even starts iterating through machine groups.
        /// ZERO OVERHEAD when player is moving!
        /// </summary>
        private static bool MachineManagerAutomatePrefix()
        {
            try
            {
                updateCyclesChecked++;

                // Check idle state (if enabled)
                if (ModEntry.Config.OnlyProcessWhenIdle && !isPlayerIdle)
                {
                    updateCyclesSkipped++;

                    if (ModEntry.Config.DebugMode && updateCyclesSkipped % 60 == 0) // Log every 60 skips to avoid spam
                    {
                        ModEntry.ModMonitor.Log($"[DEBUG] ⏸️ AUTOMATE FULLY STOPPED - Player is moving (skipped {updateCyclesSkipped} cycles)", StardewModdingAPI.LogLevel.Trace);
                    }

                    return false; // Stop Automate completely - don't even iterate through groups
                }

                // Player is idle (or feature disabled) - let Automate run normally
                updateCyclesProcessed++;

                if (ModEntry.Config.DebugMode && updateCyclesProcessed == 1)
                {
                    ModEntry.ModMonitor.Log($"[DEBUG] ✅ AUTOMATE RUNNING - Player is idle", StardewModdingAPI.LogLevel.Debug);
                }

                return true; // Let Automate run
            }
            catch (Exception ex)
            {
                ModEntry.ModMonitor.Log($"Error in MachineManagerAutomatePrefix: {ex}", StardewModdingAPI.LogLevel.Error);
                return true; // Fail safely - let Automate run normally
            }
        }

        /// <summary>
        /// FALLBACK PREFIX for MachineGroup.Automate (only used if manager patch fails).
        /// This is less efficient but still works.
        /// </summary>
        private static bool MachineGroupAutomatePrefix(object __instance)
        {
            try
            {
                updateCyclesChecked++;

                // Check idle state (if enabled)
                if (ModEntry.Config.OnlyProcessWhenIdle && !isPlayerIdle)
                {
                    updateCyclesSkipped++;

                    if (ModEntry.Config.DebugMode)
                    {
                        ModEntry.ModMonitor.Log($"[DEBUG] ⏸️ SKIPPED - Player is not idle (using fallback patch)", StardewModdingAPI.LogLevel.Trace);
                    }

                    return false; // Skip - player is moving
                }

                // Player is idle (or feature disabled) - process it
                updateCyclesProcessed++;

                if (ModEntry.Config.DebugMode)
                {
                    ModEntry.ModMonitor.Log($"[DEBUG] ✅ PROCESSING", StardewModdingAPI.LogLevel.Trace);
                }

                return true;
            }
            catch (Exception ex)
            {
                ModEntry.ModMonitor.Log($"Error in MachineGroupAutomatePrefix: {ex}", StardewModdingAPI.LogLevel.Error);
                return true; // Fail safely - let Automate process normally
            }
        }

        /// <summary>Get statistics for logging.</summary>
        public static void LogStatistics()
        {
            if (updateCyclesChecked == 0) return;

            var skipRate = (updateCyclesSkipped / (double)updateCyclesChecked) * 100;

            ModEntry.ModMonitor.Log($"=== Automate No More Lag - Statistics ===", StardewModdingAPI.LogLevel.Info);
            ModEntry.ModMonitor.Log($"Automate Cycles Checked: {updateCyclesChecked}", StardewModdingAPI.LogLevel.Info);
            ModEntry.ModMonitor.Log($"Automate Cycles Processed: {updateCyclesProcessed}", StardewModdingAPI.LogLevel.Info);
            ModEntry.ModMonitor.Log($"Automate Cycles FULLY STOPPED: {updateCyclesSkipped} ({skipRate:F1}%)", StardewModdingAPI.LogLevel.Info);

            if (ModEntry.Config.OnlyProcessWhenIdle)
            {
                string idleStatus = isPlayerIdle ? "IDLE ✅" : "MOVING 🏃";
                bool inCutscene = IsPlayerInCutsceneOrDialogue();

                if (inCutscene && ModEntry.Config.PauseTimerDuringCutscenes)
                {
                    idleStatus += " (In cutscene/dialogue - timer paused)";
                }

                ModEntry.ModMonitor.Log($"Player Idle Status: {idleStatus} (Ticks since move: {ticksSinceLastMove})", StardewModdingAPI.LogLevel.Info);
                ModEntry.ModMonitor.Log($"Input Activity Detection: {(ModEntry.Config.PauseTimerOnInput ? "ENABLED" : "DISABLED")}", StardewModdingAPI.LogLevel.Info);
                ModEntry.ModMonitor.Log($"Performance Mode: ZERO OVERHEAD when moving!", StardewModdingAPI.LogLevel.Info);
            }

            ModEntry.ModMonitor.Log($"==========================================", StardewModdingAPI.LogLevel.Info);
        }

        /// <summary>Reset statistics.</summary>
        public static void ResetStatistics()
        {
            updateCyclesChecked = 0;
            updateCyclesSkipped = 0;
            updateCyclesProcessed = 0;
            ticksSinceLastMove = 0;
            isPlayerIdle = false;
            lastPlayerPosition = Vector2.Zero;
            inputStatesInitialized = false;
        }
    }
}