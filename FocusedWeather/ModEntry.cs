using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace FocusedWeather
{
    public class ModEntry : Mod
    {
        public static IMonitor Mon;
        public static bool DebugEnabled;
        private static FieldInfo debrisWeatherField;
        private static int populateCallCount = 0;
        private static int drawCallCount = 0;
        private static int drawSkipCount = 0;

        public override void Entry(IModHelper helper)
        {
            var config = helper.ReadConfig<ModConfig>();
            Mon = this.Monitor;
            DebugEnabled = config.EnableDebugLogging;

            // Find debrisWeather on Game1
            foreach (var f in typeof(Game1).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
            {
                if (f.FieldType == typeof(List<WeatherDebris>))
                {
                    debrisWeatherField = f;
                    this.Monitor.Log($"Found field: Game1.{f.Name} (static={f.IsStatic})", LogLevel.Info);
                    break;
                }
            }

            var harmony = new Harmony(this.ModManifest.UniqueID);

            // Patch drawWeather
            var drawMethod = typeof(Game1).GetMethod("drawWeather",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static,
                new[] { typeof(GameTime), typeof(RenderTarget2D) });
            if (drawMethod != null)
            {
                harmony.Patch(drawMethod, prefix: new HarmonyMethod(typeof(Patches), "DrawWeather_Prefix"));
                this.Monitor.Log($"Patched Game1.drawWeather (static={drawMethod.IsStatic})", LogLevel.Info);
            }
            else
            {
                this.Monitor.Log("ERROR: drawWeather not found.", LogLevel.Error);
            }

            // Patch populateDebrisWeatherArray
            var populateMethod = typeof(Game1).GetMethod("populateDebrisWeatherArray",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (populateMethod != null)
            {
                harmony.Patch(populateMethod, prefix: new HarmonyMethod(typeof(Patches), "PopulateDebris_Prefix"));
                this.Monitor.Log($"Patched Game1.populateDebrisWeatherArray (static={populateMethod.IsStatic})", LogLevel.Info);
            }
            else
            {
                this.Monitor.Log("ERROR: populateDebrisWeatherArray not found.", LogLevel.Error);
            }

            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            this.Monitor.Log("Focused Weather initialized.", LogLevel.Info);
        }

        private static void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || !DebugEnabled)
                return;
            if (!e.IsMultipleOf(120))
                return;

            var current = Game1.currentLocation;
            int listCount = -1;
            if (debrisWeatherField != null)
            {
                var list = (List<WeatherDebris>)(debrisWeatherField.IsStatic
                    ? debrisWeatherField.GetValue(null)
                    : null);
                listCount = list?.Count ?? -1;
            }

            Mon.Log(
                $"[Stats] current={current?.Name ?? "null"} | " +
                $"isRaining={Game1.IsRainingHere(current)} | " +
                $"listCount={listCount} | " +
                $"drawCalls={drawCallCount} | drawSkips={drawSkipCount} | " +
                $"populateCalls={populateCallCount}",
                LogLevel.Debug);

            // Reset counters each interval
            drawCallCount = 0;
            drawSkipCount = 0;
            populateCallCount = 0;
        }

        internal static class Patches
        {
            /// <summary>
            /// Prefix for Game1.drawWeather.
            /// In 1.6 this is called once per frame for the current location only.
            /// We skip it entirely if there's no weather here — that's the optimization.
            /// </summary>
            public static bool DrawWeather_Prefix()
            {
                drawCallCount++;

                var current = Game1.currentLocation;
                if (current == null)
                {
                    drawSkipCount++;
                    return false;
                }

                bool hasWeather = Game1.IsRainingHere(current) || Game1.IsSnowingHere(current);
                if (!hasWeather)
                    drawSkipCount++;

                return hasWeather;
            }

            /// <summary>
            /// Prefix for Game1.populateDebrisWeatherArray.
            /// This is what actually fills the particle list each frame.
            /// Skip it when there's no weather to avoid allocating/populating for nothing.
            /// </summary>
            public static bool PopulateDebris_Prefix()
            {
                populateCallCount++;

                var current = Game1.currentLocation;
                if (current == null)
                    return false;

                bool hasWeather = Game1.IsRainingHere(current) || Game1.IsSnowingHere(current);
                return hasWeather;
            }
        }
    }
}