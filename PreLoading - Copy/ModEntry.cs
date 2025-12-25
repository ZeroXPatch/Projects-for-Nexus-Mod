using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace OffscreenAnimationFreezer
{
    internal sealed class ModEntry : Mod
    {
        private static ModEntry Instance = null!;
        private Harmony Harmony = null!;
        internal ModConfig Config = new();

        // debug counters (per second)
        private static int SkippedCritterUpdate;
        private static int SkippedCritterDraw;
        private static int SkippedTasUpdate;
        private static int SkippedTasDraw;

        public override void Entry(IModHelper helper)
        {
            Instance = this;
            this.Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.OneSecondUpdateTicked += this.OnOneSecondUpdateTicked;
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;

            this.Harmony = new Harmony(this.ModManifest.UniqueID);
            this.ApplyPatches();
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm is null)
                return;

            gmcm.Register(
                mod: this.ModManifest,
                reset: () => this.Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.Config)
            );

            gmcm.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("gmcm.section.general"));

            gmcm.AddBoolOption(
                this.ModManifest,
                getValue: () => this.Config.Enabled,
                setValue: v => this.Config.Enabled = v,
                name: () => this.Helper.Translation.Get("gmcm.enabled.name"),
                tooltip: () => this.Helper.Translation.Get("gmcm.enabled.tooltip")
            );

            gmcm.AddTextOption(
                this.ModManifest,
                getValue: () => this.Config.Mode,
                setValue: v => this.Config.Mode = v,
                name: () => this.Helper.Translation.Get("gmcm.mode.name"),
                tooltip: () => this.Helper.Translation.Get("gmcm.mode.tooltip"),
                allowedValues: new[] { FreezeMode.Safe, FreezeMode.Balanced },
                formatAllowedValue: v => v switch
                {
                    FreezeMode.Safe => this.Helper.Translation.Get("gmcm.mode.safe"),
                    FreezeMode.Balanced => this.Helper.Translation.Get("gmcm.mode.balanced"),
                    _ => v
                }
            );

            gmcm.AddNumberOption(
                this.ModManifest,
                getValue: () => this.Config.OffscreenMarginTiles,
                setValue: v => this.Config.OffscreenMarginTiles = v,
                name: () => this.Helper.Translation.Get("gmcm.margin.name"),
                tooltip: () => this.Helper.Translation.Get("gmcm.margin.tooltip"),
                min: 0,
                max: 20,
                interval: 1
            );

            gmcm.AddBoolOption(
                this.ModManifest,
                getValue: () => this.Config.DisableDuringEvents,
                setValue: v => this.Config.DisableDuringEvents = v,
                name: () => this.Helper.Translation.Get("gmcm.disableEvents.name"),
                tooltip: () => this.Helper.Translation.Get("gmcm.disableEvents.tooltip")
            );

            gmcm.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("gmcm.section.debug"));

            gmcm.AddBoolOption(
                this.ModManifest,
                getValue: () => this.Config.DebugLogging,
                setValue: v => this.Config.DebugLogging = v,
                name: () => this.Helper.Translation.Get("gmcm.debug.name"),
                tooltip: () => this.Helper.Translation.Get("gmcm.debug.tooltip")
            );

            gmcm.AddBoolOption(
                this.ModManifest,
                getValue: () => this.Config.FreezeAllTemporarySprites,
                setValue: v => this.Config.FreezeAllTemporarySprites = v,
                name: () => this.Helper.Translation.Get("gmcm.freezeAllTas.name"),
                tooltip: () => this.Helper.Translation.Get("gmcm.freezeAllTas.tooltip")
            );
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            if (e.Button == this.Config.ToggleDebugKey)
            {
                this.Config.DebugLogging = !this.Config.DebugLogging;
                this.Helper.WriteConfig(this.Config);
                Game1.addHUDMessage(new HUDMessage(
                    this.Helper.Translation.Get(this.Config.DebugLogging ? "hud.debugOn" : "hud.debugOff"),
                    HUDMessage.newQuest_type
                ));
            }
        }

        private void OnOneSecondUpdateTicked(object? sender, OneSecondUpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.player is null)
                return;

            if (!this.Config.DebugLogging)
                return;

            // Print counts and reset
            this.Monitor.Log(
                $"Skipped last second: Critter update={SkippedCritterUpdate}, Critter draw={SkippedCritterDraw}, TAS update={SkippedTasUpdate}, TAS draw={SkippedTasDraw}.",
                LogLevel.Info
            );

            SkippedCritterUpdate = 0;
            SkippedCritterDraw = 0;
            SkippedTasUpdate = 0;
            SkippedTasDraw = 0;
        }

        private void ApplyPatches()
        {
            int critterUpdatePatched = 0;
            int critterDrawPatched = 0;
            int tasUpdatePatched = 0;
            int tasDrawPatched = 0;

            // --- Critters ---
            var critterType = AccessTools.TypeByName("StardewValley.BellsAndWhistles.Critter");
            if (critterType is null)
            {
                this.Monitor.Log("Could not find type StardewValley.BellsAndWhistles.Critter. Critter freezing will not work.", LogLevel.Warn);
            }
            else
            {
                var updateMethods = critterType
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.Name == "update"
                        && m.ReturnType == typeof(void)
                        && m.GetParameters().Length == 2
                        && m.GetParameters()[0].ParameterType == typeof(GameTime))
                    .ToList();

                foreach (var m in updateMethods)
                {
                    this.Harmony.Patch(m, prefix: new HarmonyMethod(typeof(ModEntry), nameof(Critter_Update_Prefix)));
                    critterUpdatePatched++;
                }

                var drawMethods = critterType
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.Name == "draw"
                        && m.ReturnType == typeof(void)
                        && m.GetParameters().Length == 1
                        && m.GetParameters()[0].ParameterType == typeof(SpriteBatch))
                    .ToList();

                foreach (var m in drawMethods)
                {
                    this.Harmony.Patch(m, prefix: new HarmonyMethod(typeof(ModEntry), nameof(Critter_Draw_Prefix)));
                    critterDrawPatched++;
                }
            }

            // --- TemporaryAnimatedSprite ---
            var tasType = typeof(TemporaryAnimatedSprite);

            var tasUpdateMethods = tasType
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.Name == "update"
                    && m.ReturnType == typeof(bool)
                    && m.GetParameters().Length == 1
                    && m.GetParameters()[0].ParameterType == typeof(GameTime))
                .ToList();

            foreach (var m in tasUpdateMethods)
            {
                this.Harmony.Patch(m, prefix: new HarmonyMethod(typeof(ModEntry), nameof(TAS_Update_Prefix)));
                tasUpdatePatched++;
            }

            var tasDrawMethods = tasType
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.Name == "draw"
                    && m.ReturnType == typeof(void)
                    && m.GetParameters().Length >= 1
                    && m.GetParameters()[0].ParameterType == typeof(SpriteBatch))
                .ToList();

            foreach (var m in tasDrawMethods)
            {
                this.Harmony.Patch(m, prefix: new HarmonyMethod(typeof(ModEntry), nameof(TAS_Draw_Prefix)));
                tasDrawPatched++;
            }

            this.Monitor.Log(
                $"Patches applied. Critter update={critterUpdatePatched}, Critter draw={critterDrawPatched}, TAS update={tasUpdatePatched}, TAS draw={tasDrawPatched}.",
                LogLevel.Info
            );

            if (critterUpdatePatched == 0 && critterDrawPatched == 0)
                this.Monitor.Log("No Critter methods patched. Safe mode will likely appear to do nothing.", LogLevel.Warn);

            if (tasUpdatePatched == 0 && tasDrawPatched == 0)
                this.Monitor.Log("No TemporaryAnimatedSprite methods patched. Balanced mode will not work.", LogLevel.Warn);
        }

        // --------------------
        // Shared gating
        // --------------------

        private static bool ShouldBypassMod()
        {
            if (Instance is null)
                return true;

            if (!Instance.Config.Enabled)
                return true;

            if (!Context.IsWorldReady || Game1.player is null)
                return true;

            if (Instance.Config.DisableDuringEvents && (Game1.eventUp || Game1.CurrentEvent is not null))
                return true;

            return false;
        }

        private static Rectangle GetViewportWorldRectInflated()
        {
            int marginPx = Math.Max(0, Instance.Config.OffscreenMarginTiles) * Game1.tileSize;

            var vp = Game1.viewport; // xTile rectangle
            var r = new Rectangle(vp.X, vp.Y, vp.Width, vp.Height); // XNA rectangle
            r.Inflate(marginPx, marginPx);
            return r;
        }

        private static bool IsOffscreen(Rectangle worldBounds)
        {
            var view = GetViewportWorldRectInflated();
            return !view.Intersects(worldBounds);
        }

        private static Rectangle GuessWorldBoundsFromPosition(Vector2 pos, int sizePx = 64)
        {
            return new Rectangle((int)pos.X, (int)pos.Y, sizePx, sizePx);
        }

        // --------------------
        // Critter patches
        // --------------------

        private static bool Critter_Update_Prefix(object __instance)
        {
            if (ShouldBypassMod())
                return true;

            if (!TryGetCritterBounds(__instance, out Rectangle bounds))
                return true;

            if (IsOffscreen(bounds))
            {
                SkippedCritterUpdate++;
                return false;
            }

            return true;
        }

        private static bool Critter_Draw_Prefix(object __instance)
        {
            if (ShouldBypassMod())
                return true;

            if (!TryGetCritterBounds(__instance, out Rectangle bounds))
                return true;

            if (IsOffscreen(bounds))
            {
                SkippedCritterDraw++;
                return false;
            }

            return true;
        }

        private static bool TryGetCritterBounds(object critter, out Rectangle bounds)
        {
            bounds = default;

            var m = AccessTools.Method(critter.GetType(), "getBoundingBox");
            if (m is not null && m.ReturnType == typeof(Rectangle) && m.GetParameters().Length == 0)
            {
                try
                {
                    bounds = (Rectangle)m.Invoke(critter, null)!;
                    return true;
                }
                catch { }
            }

            var posField = AccessTools.Field(critter.GetType(), "position");
            if (posField is not null && posField.FieldType == typeof(Vector2))
            {
                try
                {
                    var pos = (Vector2)posField.GetValue(critter)!;
                    bounds = GuessWorldBoundsFromPosition(pos, 64);
                    return true;
                }
                catch { }
            }

            var posProp = AccessTools.Property(critter.GetType(), "position");
            if (posProp is not null && posProp.PropertyType == typeof(Vector2))
            {
                try
                {
                    var pos = (Vector2)posProp.GetValue(critter)!;
                    bounds = GuessWorldBoundsFromPosition(pos, 64);
                    return true;
                }
                catch { }
            }

            return false;
        }

        // --------------------
        // TemporaryAnimatedSprite patches
        // --------------------

        private static bool TAS_Update_Prefix(TemporaryAnimatedSprite __instance, ref bool __result)
        {
            if (ShouldBypassMod())
                return true;

            // only active in Balanced OR if the aggressive test toggle is on
            bool allow = Instance.Config.Mode == FreezeMode.Balanced || Instance.Config.FreezeAllTemporarySprites;
            if (!allow)
                return true;

            if (!Instance.Config.FreezeAllTemporarySprites)
            {
                // conservative: only freeze ones we think are looping cosmetic
                if (!IsProbablyCosmeticLoop(__instance))
                    return true;
            }

            if (!TryGetTempSpriteBounds(__instance, out Rectangle bounds))
                return true;

            if (IsOffscreen(bounds))
            {
                SkippedTasUpdate++;
                __result = false; // don't let it "finish" while frozen
                return false;
            }

            return true;
        }

        private static bool TAS_Draw_Prefix(TemporaryAnimatedSprite __instance)
        {
            if (ShouldBypassMod())
                return true;

            bool allow = Instance.Config.Mode == FreezeMode.Balanced || Instance.Config.FreezeAllTemporarySprites;
            if (!allow)
                return true;

            if (!Instance.Config.FreezeAllTemporarySprites)
            {
                if (!IsProbablyCosmeticLoop(__instance))
                    return true;
            }

            if (!TryGetTempSpriteBounds(__instance, out Rectangle bounds))
                return true;

            if (IsOffscreen(bounds))
            {
                SkippedTasDraw++;
                return false;
            }

            return true;
        }

        private static bool IsProbablyCosmeticLoop(TemporaryAnimatedSprite tas)
        {
            // Try to detect looping + not-attached.
            // If we cannot read fields/properties reliably, we assume "not safe to freeze".
            if (!TryGetBool(tas, new[] { "loop", "Loop" }, out bool loops) || !loops)
                return false;

            if (TryGetBool(tas, new[] { "attached", "Attached" }, out bool attached) && attached)
                return false;

            // parent field sometimes exists; if non-null treat as not cosmetic
            var parentField = AccessTools.Field(tas.GetType(), "parent");
            if (parentField is not null && parentField.GetValue(tas) is not null)
                return false;

            return true;
        }

        private static bool TryGetTempSpriteBounds(TemporaryAnimatedSprite tas, out Rectangle bounds)
        {
            bounds = default;

            if (!TryGetVector2(tas, new[] { "position", "Position" }, out Vector2 pos))
                return false;

            Rectangle src = new Rectangle(0, 0, 64, 64);
            if (TryGetRectangle(tas, new[] { "sourceRect", "SourceRect" }, out Rectangle got))
                src = got;

            int w = src.Width > 0 ? src.Width : 64;
            int h = src.Height > 0 ? src.Height : 64;

            float scale = 1f;
            if (TryGetFloat(tas, new[] { "scale", "Scale" }, out float s) && s > 0f)
                scale = s;

            bounds = new Rectangle((int)pos.X, (int)pos.Y, (int)(w * scale), (int)(h * scale));
            return true;
        }

        // --------------------
        // Reflection helpers
        // --------------------

        private static bool TryGetBool(object instance, string[] names, out bool value)
        {
            value = default;
            var t = instance.GetType();

            foreach (string n in names)
            {
                var f = AccessTools.Field(t, n);
                if (f is not null && f.FieldType == typeof(bool))
                {
                    value = (bool)f.GetValue(instance)!;
                    return true;
                }

                var p = AccessTools.Property(t, n);
                if (p is not null && p.PropertyType == typeof(bool))
                {
                    value = (bool)p.GetValue(instance)!;
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetFloat(object instance, string[] names, out float value)
        {
            value = default;
            var t = instance.GetType();

            foreach (string n in names)
            {
                var f = AccessTools.Field(t, n);
                if (f is not null && f.FieldType == typeof(float))
                {
                    value = (float)f.GetValue(instance)!;
                    return true;
                }

                var p = AccessTools.Property(t, n);
                if (p is not null && p.PropertyType == typeof(float))
                {
                    value = (float)p.GetValue(instance)!;
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetVector2(object instance, string[] names, out Vector2 value)
        {
            value = default;
            var t = instance.GetType();

            foreach (string n in names)
            {
                var f = AccessTools.Field(t, n);
                if (f is not null && f.FieldType == typeof(Vector2))
                {
                    value = (Vector2)f.GetValue(instance)!;
                    return true;
                }

                var p = AccessTools.Property(t, n);
                if (p is not null && p.PropertyType == typeof(Vector2))
                {
                    value = (Vector2)p.GetValue(instance)!;
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetRectangle(object instance, string[] names, out Rectangle value)
        {
            value = default;
            var t = instance.GetType();

            foreach (string n in names)
            {
                var f = AccessTools.Field(t, n);
                if (f is not null && f.FieldType == typeof(Rectangle))
                {
                    value = (Rectangle)f.GetValue(instance)!;
                    return true;
                }

                var p = AccessTools.Property(t, n);
                if (p is not null && p.PropertyType == typeof(Rectangle))
                {
                    value = (Rectangle)p.GetValue(instance)!;
                    return true;
                }
            }

            return false;
        }
    }

    internal static class FreezeMode
    {
        public const string Safe = "Safe";
        public const string Balanced = "Balanced";
    }
}
