using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using HarmonyLib;
using System.Reflection;

namespace DebrisOptimizer
{
    public class ModEntry : Mod
    {
        internal static ModConfig Config;
        internal static IMonitor ModMonitor;
        private int visibleDebrisCount = 0;
        private int totalDebrisCount = 0;
        internal static HashSet<Debris> hiddenDebris = new HashSet<Debris>();
        private IGenericModConfigMenuApi configMenu;

        public override void Entry(IModHelper helper)
        {
            Config = this.Helper.ReadConfig<ModConfig>();
            ModMonitor = this.Monitor;

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.Display.RenderedWorld += this.OnRenderedWorld;

            // Apply Harmony patches
            var harmony = new Harmony(this.ModManifest.UniqueID);
            
            try
            {
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                this.Monitor.Log("Harmony patches applied successfully!", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Error applying Harmony patches: {ex.Message}", LogLevel.Error);
            }
            
            this.Monitor.Log("Debris Performance Optimizer loaded! You'll receive all items, but excess debris will be hidden for performance.", LogLevel.Info);
        }

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            // Get Generic Mod Config Menu's API (if it's installed)
            configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            // Register mod configuration
            configMenu.Register(
                mod: this.ModManifest,
                reset: () => Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(Config)
            );

            // Add settings
            configMenu.AddSectionTitle(
                mod: this.ModManifest,
                text: () => "Debris Display Settings"
            );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => "Max Visible Debris",
                tooltip: () => "Maximum number of debris items shown on screen. Set to 0 to hide all debris. Excess debris will be hidden but you'll still receive all items. Lower = better performance.",
                getValue: () => Config.MaxVisibleDebris,
                setValue: value => Config.MaxVisibleDebris = value,
                min: 0,
                max: 500,
                interval: 10
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Enable Debris Hiding",
                tooltip: () => "When enabled, excess debris will be hidden for performance. You still get all items!",
                getValue: () => Config.EnableDebrisHiding,
                setValue: value => Config.EnableDebrisHiding = value
            );

            configMenu.AddSectionTitle(
                mod: this.ModManifest,
                text: () => "Performance Settings"
            );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => "Physics Distance",
                tooltip: () => "Distance in pixels at which debris physics are disabled. Debris far from you will freeze to save CPU.",
                getValue: () => (int)Config.PhysicsDisableDistance,
                setValue: value => Config.PhysicsDisableDistance = value,
                min: 200,
                max: 2000,
                interval: 100
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Disable Distant Physics",
                tooltip: () => "Freeze physics for debris far from the player to improve performance.",
                getValue: () => Config.DisableDistantDebrisPhysics,
                setValue: value => Config.DisableDistantDebrisPhysics = value
            );

            configMenu.AddSectionTitle(
                mod: this.ModManifest,
                text: () => "Debug Options"
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Show Debug Overlay",
                tooltip: () => "Display debris count on screen for monitoring.",
                getValue: () => Config.ShowDebugOverlay,
                setValue: value => Config.ShowDebugOverlay = value
            );
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            var location = Game1.currentLocation;
            if (location == null || location.debris == null)
                return;

            totalDebrisCount = location.debris.Count;

            // Update every tick to ensure responsiveness
            hiddenDebris.Clear();

            // Manage which debris to hide for visual performance
            if (Config.EnableDebrisHiding)
            {
                if (Config.MaxVisibleDebris == 0)
                {
                    // Hide ALL debris when set to 0
                    foreach (var debris in location.debris)
                    {
                        hiddenDebris.Add(debris);
                    }
                    visibleDebrisCount = 0;
                }
                else if (totalDebrisCount > Config.MaxVisibleDebris)
                {
                    // Sort debris by distance from player (hide furthest ones)
                    Vector2 playerPos = Game1.player.Position;
                    var sortedDebris = location.debris
                        .OrderBy(d => {
                            if (d.Chunks == null || d.Chunks.Count == 0) 
                                return float.MaxValue;
                            return Vector2.Distance(playerPos, d.Chunks[0].position.Value);
                        })
                        .ToList();
                    
                    // Hide debris beyond the max visible count (furthest ones)
                    for (int i = Config.MaxVisibleDebris; i < sortedDebris.Count; i++)
                    {
                        hiddenDebris.Add(sortedDebris[i]);
                    }

                    visibleDebrisCount = Config.MaxVisibleDebris;
                }
                else
                {
                    // Show all debris when count is below max
                    visibleDebrisCount = totalDebrisCount;
                }
            }
            else
            {
                // Debris hiding is disabled, show everything
                visibleDebrisCount = totalDebrisCount;
            }

            // Disable physics for distant debris to save CPU
            if (Config.DisableDistantDebrisPhysics && e.IsMultipleOf(10)) // Check every 10 ticks
            {
                Vector2 playerPos = Game1.player.Position;
                
                foreach (var debris in location.debris)
                {
                    if (debris.Chunks == null || debris.Chunks.Count == 0)
                        continue;

                    float distance = Vector2.Distance(playerPos, debris.Chunks[0].position.Value);
                    
                    // Freeze debris physics when far from player
                    if (distance > Config.PhysicsDisableDistance)
                    {
                        foreach (var chunk in debris.Chunks)
                        {
                            chunk.xVelocity.Value = 0f;
                            chunk.yVelocity.Value = 0f;
                            chunk.rotationVelocity = 0f;
                        }
                    }
                }
            }

            // IMPORTANT: We never remove debris from the list!
            // The game will automatically collect and remove debris when appropriate.
            // We just hide some visually for performance.
        }

        private void OnRenderedWorld(object sender, RenderedWorldEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            var location = Game1.currentLocation;
            if (location == null || location.debris == null)
                return;

            // Show debug overlay if enabled
            if (Config.ShowDebugOverlay)
            {
                string debugText = $"Debris: {visibleDebrisCount}/{totalDebrisCount} visible";
                if (hiddenDebris.Count > 0)
                {
                    debugText += $" ({hiddenDebris.Count} hidden)";
                }
                debugText += $"\nMax: {Config.MaxVisibleDebris}, Hiding: {(Config.EnableDebrisHiding ? "ON" : "OFF")}";

                // Draw debug text
                Vector2 position = new Vector2(10, Game1.viewport.Height - 80);
                
                // Draw background for readability
                var textSize = Game1.smallFont.MeasureString(debugText);
                e.SpriteBatch.Draw(
                    Game1.staminaRect,
                    new Rectangle((int)position.X - 5, (int)position.Y - 5, (int)textSize.X + 10, (int)textSize.Y + 10),
                    Color.Black * 0.7f
                );
                
                e.SpriteBatch.DrawString(
                    Game1.smallFont,
                    debugText,
                    position,
                    Color.White
                );
            }
        }
    }

    public class ModConfig
    {
        /// <summary>Maximum number of debris items to display on screen. Excess will be hidden but you still get all items!</summary>
        public int MaxVisibleDebris { get; set; } = 150;

        /// <summary>Enable hiding of excess debris for performance.</summary>
        public bool EnableDebrisHiding { get; set; } = true;

        /// <summary>Distance in pixels at which debris physics are disabled.</summary>
        public float PhysicsDisableDistance { get; set; } = 800f;

        /// <summary>Disable physics calculations for distant debris.</summary>
        public bool DisableDistantDebrisPhysics { get; set; } = true;

        /// <summary>Show debug overlay with debris count.</summary>
        public bool ShowDebugOverlay { get; set; } = false;
    }

    // Generic Mod Config Menu API interface
    public interface IGenericModConfigMenuApi
    {
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);
        void AddSectionTitle(IManifest mod, Func<string> text, Func<string> tooltip = null);
        void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue, Func<string> name, Func<string> tooltip = null, string fieldId = null);
        void AddNumberOption(IManifest mod, Func<int> getValue, Action<int> setValue, Func<string> name, Func<string> tooltip = null, int? min = null, int? max = null, int? interval = null, string fieldId = null);
    }

    // Harmony patch to skip rendering hidden debris
    [HarmonyPatch(typeof(Debris), "draw")]
    public class Debris_Draw_Patch
    {
        public static bool Prefix(Debris __instance, SpriteBatch b)
        {
            // If this debris is in the hidden set, skip rendering it
            if (ModEntry.hiddenDebris.Contains(__instance))
            {
                return false; // Skip the original draw method
            }
            return true; // Allow normal rendering
        }
    }
}

