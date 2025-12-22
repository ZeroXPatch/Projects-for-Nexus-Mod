using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace ReelTensionFishing
{
    internal sealed class ModEntry : Mod
    {
        private const string HarmonyId = "YourName.ReelTensionFishing";

        internal static ModEntry Instance { get; private set; } = null!;
        internal ModConfig Config { get; private set; } = new();

        private Harmony? harmony;

        // One state per BobberBar menu instance.
        private static readonly ConditionalWeakTable<BobberBar, ReelTensionState> States = new();

        public override void Entry(IModHelper helper)
        {
            Instance = this;
            this.Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;

            this.harmony = new Harmony(HarmonyId);
            this.ApplyPatches(this.harmony);
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            this.RegisterGmcm();
        }

        private void ApplyPatches(Harmony harmony)
        {
            MethodInfo? update = AccessTools.Method(typeof(BobberBar), "update", new[] { typeof(GameTime) });
            MethodInfo? draw = AccessTools.Method(typeof(BobberBar), "draw", new[] { typeof(SpriteBatch) });

            if (update is null || draw is null)
            {
                this.Monitor.Log("Failed to find BobberBar methods to patch (update/draw).", LogLevel.Error);
                return;
            }

            harmony.Patch(update, prefix: new HarmonyMethod(typeof(ModEntry), nameof(BobberBar_Update_Prefix)));
            harmony.Patch(draw, prefix: new HarmonyMethod(typeof(ModEntry), nameof(BobberBar_Draw_Prefix)));

            this.Monitor.Log("Reel Tension minigame patches applied.", LogLevel.Info);
        }

        // ---------- Harmony: BobberBar.update ----------
        private static bool BobberBar_Update_Prefix(BobberBar __instance, GameTime time)
        {
            if (!Context.IsWorldReady || Game1.player is null)
                return true;

            var mod = Instance;
            if (!mod.Config.Enabled || !mod.Config.UseReelTensionMinigame)
                return true;

            // If another mod replaced activeClickableMenu, don't interfere.
            if (!ReferenceEquals(Game1.activeClickableMenu, __instance))
                return true;

            ReelTensionState state = States.GetOrCreateValue(__instance);
            if (!state.Initialized)
            {
                state.Initialize(__instance);

                // Unknown/unsupported => vanilla.
                if (state.ForceVanillaFallback)
                {
                    States.Remove(__instance);
                    return true;
                }

                // Boss/legendary exception => vanilla.
                if (mod.Config.UseVanillaForLegendary && state.IsLegendaryOrBossFish)
                {
                    States.Remove(__instance);
                    return true;
                }
            }

            state.Update(__instance, time);

            // If we finished this tick, push results into BobberBar and let vanilla run to finalize.
            if (state.FinishedThisTick)
            {
                if (!state.ApplyResultToBobberBar(__instance))
                {
                    States.Remove(__instance);
                    return true;
                }

                States.Remove(__instance);
                return true;
            }

            // Skip vanilla update while our minigame is active.
            return false;
        }

        // ---------- Harmony: BobberBar.draw ----------
        private static bool BobberBar_Draw_Prefix(BobberBar __instance, SpriteBatch b)
        {
            if (!Context.IsWorldReady || Game1.player is null)
                return true;

            var mod = Instance;
            if (!mod.Config.Enabled || !mod.Config.UseReelTensionMinigame)
                return true;

            if (!States.TryGetValue(__instance, out ReelTensionState? state))
                return true;

            if (state.ForceVanillaFallback)
                return true;

            if (mod.Config.UseVanillaForLegendary && state.IsLegendaryOrBossFish)
                return true;

            state.Draw(__instance, b);
            return false;
        }

        // ---------- GMCM ----------
        private void RegisterGmcm()
        {
            var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm is null)
                return;

            gmcm.Register(
                mod: this.ModManifest,
                reset: () => this.Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.Config)
            );

            gmcm.AddSectionTitle(this.ModManifest,
                text: () => this.Helper.Translation.Get("gmcm.section.general"),
                tooltip: () => this.Helper.Translation.Get("gmcm.section.general.tooltip")
            );

            gmcm.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => this.Config.Enabled ? 1 : 0,
                setValue: v => this.Config.Enabled = v != 0,
                name: () => this.Helper.Translation.Get("gmcm.enabled.name"),
                tooltip: () => this.Helper.Translation.Get("gmcm.enabled.tooltip"),
                min: 0, max: 1, interval: 1,
                formatValue: v => v == 0 ? this.Helper.Translation.Get("gmcm.off") : this.Helper.Translation.Get("gmcm.on")
            );

            gmcm.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => this.Config.UseReelTensionMinigame ? 1 : 0,
                setValue: v => this.Config.UseReelTensionMinigame = v != 0,
                name: () => this.Helper.Translation.Get("gmcm.useMinigame.name"),
                tooltip: () => this.Helper.Translation.Get("gmcm.useMinigame.tooltip"),
                min: 0, max: 1, interval: 1,
                formatValue: v => v == 0 ? this.Helper.Translation.Get("gmcm.off") : this.Helper.Translation.Get("gmcm.on")
            );

            gmcm.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => this.Config.UseVanillaForLegendary ? 1 : 0,
                setValue: v => this.Config.UseVanillaForLegendary = v != 0,
                name: () => this.Helper.Translation.Get("gmcm.legendaryVanilla.name"),
                tooltip: () => this.Helper.Translation.Get("gmcm.legendaryVanilla.tooltip"),
                min: 0, max: 1, interval: 1,
                formatValue: v => v == 0 ? this.Helper.Translation.Get("gmcm.off") : this.Helper.Translation.Get("gmcm.on")
            );

            gmcm.AddSectionTitle(this.ModManifest,
                text: () => this.Helper.Translation.Get("gmcm.section.tension"),
                tooltip: () => this.Helper.Translation.Get("gmcm.section.tension.tooltip")
            );

            gmcm.AddNumberOption(
                this.ModManifest,
                () => this.Config.SafeZonePercent,
                v => this.Config.SafeZonePercent = v,
                () => this.Helper.Translation.Get("gmcm.safeZone.name"),
                () => this.Helper.Translation.Get("gmcm.safeZone.tooltip"),
                min: 10, max: 80, interval: 1,
                formatValue: v => $"{v}%"
            );

            gmcm.AddNumberOption(
                this.ModManifest,
                () => this.Config.TensionUpRate,
                v => this.Config.TensionUpRate = v,
                () => this.Helper.Translation.Get("gmcm.tensionUp.name"),
                () => this.Helper.Translation.Get("gmcm.tensionUp.tooltip"),
                min: 1, max: 30, interval: 1
            );

            gmcm.AddNumberOption(
                this.ModManifest,
                () => this.Config.TensionDownRate,
                v => this.Config.TensionDownRate = v,
                () => this.Helper.Translation.Get("gmcm.tensionDown.name"),
                () => this.Helper.Translation.Get("gmcm.tensionDown.tooltip"),
                min: 1, max: 30, interval: 1
            );

            gmcm.AddNumberOption(
                this.ModManifest,
                () => this.Config.CatchGainRate,
                v => this.Config.CatchGainRate = v,
                () => this.Helper.Translation.Get("gmcm.catchGain.name"),
                () => this.Helper.Translation.Get("gmcm.catchGain.tooltip"),
                min: 1, max: 40, interval: 1
            );

            gmcm.AddNumberOption(
                this.ModManifest,
                () => this.Config.CatchLossRate,
                v => this.Config.CatchLossRate = v,
                () => this.Helper.Translation.Get("gmcm.catchLoss.name"),
                () => this.Helper.Translation.Get("gmcm.catchLoss.tooltip"),
                min: 1, max: 60, interval: 1
            );

            gmcm.AddSectionTitle(this.ModManifest,
                text: () => this.Helper.Translation.Get("gmcm.section.treasure"),
                tooltip: () => this.Helper.Translation.Get("gmcm.section.treasure.tooltip")
            );

            gmcm.AddNumberOption(
                this.ModManifest,
                () => this.Config.EnableTreasureMinigame ? 1 : 0,
                v => this.Config.EnableTreasureMinigame = v != 0,
                () => this.Helper.Translation.Get("gmcm.treasureEnable.name"),
                () => this.Helper.Translation.Get("gmcm.treasureEnable.tooltip"),
                min: 0, max: 1, interval: 1,
                formatValue: v => v == 0 ? this.Helper.Translation.Get("gmcm.off") : this.Helper.Translation.Get("gmcm.on")
            );

            gmcm.AddNumberOption(
                this.ModManifest,
                () => this.Config.TreasureAppearSeconds,
                v => this.Config.TreasureAppearSeconds = v,
                () => this.Helper.Translation.Get("gmcm.treasureDelay.name"),
                () => this.Helper.Translation.Get("gmcm.treasureDelay.tooltip"),
                min: 0, max: 10, interval: 1,
                formatValue: v => $"{v}s"
            );

            gmcm.AddNumberOption(
                this.ModManifest,
                () => this.Config.TreasureGainRate,
                v => this.Config.TreasureGainRate = v,
                () => this.Helper.Translation.Get("gmcm.treasureGain.name"),
                () => this.Helper.Translation.Get("gmcm.treasureGain.tooltip"),
                min: 1, max: 50, interval: 1
            );

            gmcm.AddNumberOption(
                this.ModManifest,
                () => this.Config.TreasureLossRate,
                v => this.Config.TreasureLossRate = v,
                () => this.Helper.Translation.Get("gmcm.treasureLoss.name"),
                () => this.Helper.Translation.Get("gmcm.treasureLoss.tooltip"),
                min: 0, max: 50, interval: 1
            );
        }

        // ---------- GMCM API (minimal) ----------
        public interface IGenericModConfigMenuApi
        {
            void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);
            void AddSectionTitle(IManifest mod, Func<string> text, Func<string>? tooltip = null);

            void AddNumberOption(
                IManifest mod,
                Func<int> getValue,
                Action<int> setValue,
                Func<string> name,
                Func<string>? tooltip = null,
                int? min = null,
                int? max = null,
                int? interval = null,
                Func<int, string>? formatValue = null,
                string? fieldId = null
            );
        }

        // ---------- State (the actual minigame) ----------
        private sealed class ReelTensionState
        {
            private static readonly HashSet<int> VanillaLegendaryFishIds = new()
            {
                159, // Crimsonfish
                160, // Angler
                163, // Legend
                682, // Mutant Carp
                775  // Glacierfish
            };

            private readonly Random rand = new();

            public bool Initialized { get; private set; }
            public bool FinishedThisTick { get; private set; }

            public bool ForceVanillaFallback { get; private set; }
            public bool IsLegendaryOrBossFish { get; private set; }

            // Required-ish: we try to find at least one float we can set that causes vanilla to finalize.
            private readonly List<FieldInfo> catchFinalizeFloats = new();
            private FieldInfo? fPerfect;

            // Treasure fields (optional)
            private FieldInfo? fTreasure;
            private FieldInfo? fTreasureCatchLevel;
            private FieldInfo? fTreasureCaught;

            private float tension = 0.5f;        // 0..1 controlled by player
            private float target = 0.5f;         // 0..1 moving safe zone center
            private float targetVel;

            private float catchProgress = 0.35f; // 0..1
            private bool perfectStillPossible = true;

            private bool hasTreasure;
            private bool isGoldenTreasure;
            private float treasureTimer;
            private bool treasureActive;
            private float treasureProgress;
            private bool treasureCaught;

            private bool hasTrapBobber;
            private bool hasTreasureHunter;

            private bool bubblesPresent;

            private float difficulty = 50f;
            private int fishId = -1;

            public void Initialize(BobberBar menu)
            {
                this.Initialized = true;
                this.ForceVanillaFallback = false;
                this.IsLegendaryOrBossFish = false;

                Type t = menu.GetType();

                // difficulty: required (tuning)
                if (!TryGetField(menu, "difficulty", out this.difficulty))
                {
                    this.ForceVanillaFallback = true;
                    return;
                }

                // fish id: required per your request (unknown fish => vanilla)
                if (TryGetField(menu, "whichFish", out int whichFish))
                    this.fishId = whichFish;
                else if (TryGetIntFieldContaining(menu, "fish", out int anyFishInt))
                    this.fishId = anyFishInt;
                else
                    this.fishId = -1;

                if (this.fishId <= 0)
                {
                    this.ForceVanillaFallback = true;
                    return;
                }

                // Boss/legendary detection
                if (VanillaLegendaryFishIds.Contains(this.fishId))
                    this.IsLegendaryOrBossFish = true;

                if (TryGetAnyBoolFieldContaining(menu, "boss", out bool boss) && boss)
                    this.IsLegendaryOrBossFish = true;
                if (TryGetAnyBoolFieldContaining(menu, "legendary", out bool legendary) && legendary)
                    this.IsLegendaryOrBossFish = true;
                if (TryGetAnyBoolFieldContaining(menu, "legend", out bool legend) && legend)
                    this.IsLegendaryOrBossFish = true;

                // Find "perfect" field (preferred exact, else contains)
                this.fPerfect =
                    t.GetField("perfect", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?? FindFirstBoolField(t, nameContains: "perfect");

                // Find fields that likely control whether the minigame ends.
                // We prefer exact "distanceFromCatching", but also accept any float containing both "catch" and ("distance" or "progress").
                FieldInfo? exactDistance = t.GetField("distanceFromCatching", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (exactDistance is not null && exactDistance.FieldType == typeof(float))
                    this.catchFinalizeFloats.Add(exactDistance);

                foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (f.FieldType != typeof(float))
                        continue;

                    string n = f.Name;

                    bool hasCatch = n.IndexOf("catch", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool hasDistance = n.IndexOf("distance", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool hasProgress = n.IndexOf("progress", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (hasCatch && (hasDistance || hasProgress))
                    {
                        if (!this.catchFinalizeFloats.Contains(f))
                            this.catchFinalizeFloats.Add(f);
                    }
                }

                // If we can’t find any finalize floats or perfect field, treat as unknown => vanilla
                if (this.catchFinalizeFloats.Count == 0 || this.fPerfect is null)
                {
                    this.ForceVanillaFallback = true;
                    return;
                }

                // Treasure fields (optional)
                this.fTreasure = t.GetField("treasure", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                this.fTreasureCatchLevel = t.GetField("treasureCatchLevel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                this.fTreasureCaught = t.GetField("treasureCaught", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                this.hasTreasure = TryGetField(menu, "treasure", out bool treasure) && treasure;
                this.isGoldenTreasure =
                    (TryGetField(menu, "goldenTreasure", out bool goldenA) && goldenA) ||
                    (TryGetField(menu, "isGoldenTreasure", out bool goldenB) && goldenB) ||
                    (TryGetAnyBoolFieldContaining(menu, "golden", out bool goldenC) && goldenC);

                int whichBobber = TryGetField(menu, "whichBobber", out int wb) ? wb : -1;
                this.hasTreasureHunter = whichBobber == 693;
                this.hasTrapBobber = whichBobber == 694;

                this.bubblesPresent = DetectBubbles();

                // Seed values
                this.tension = 0.5f;
                this.target = Math.Clamp(0.45f + (float)(this.rand.NextDouble() * 0.10), 0f, 1f);
                this.targetVel = 0f;

                this.catchProgress = 0.35f;
                this.perfectStillPossible = true;

                this.treasureTimer = 0f;
                this.treasureActive = false;
                this.treasureProgress = 0f;
                this.treasureCaught = false;

                this.FinishedThisTick = false;
            }

            public void Update(BobberBar menu, GameTime time)
            {
                this.FinishedThisTick = false;

                float dt = (float)time.ElapsedGameTime.TotalSeconds;
                dt = Math.Clamp(dt, 0f, 0.05f);

                if (ModEntry.Instance.Helper.Input.IsDown(SButton.Escape) ||
                    ModEntry.Instance.Helper.Input.IsDown(SButton.ControllerB))
                {
                    this.catchProgress = 0f;
                    this.FinishedThisTick = true;
                    return;
                }

                bool holdReel = IsReelHeld(ModEntry.Instance.Helper);
                float up = ModEntry.Instance.Config.TensionUpRate / 10f;
                float down = ModEntry.Instance.Config.TensionDownRate / 10f;
                this.tension = Math.Clamp(this.tension + (holdReel ? up : -down) * dt, 0f, 1f);

                // Fish pull
                float jerk = Math.Clamp(this.difficulty / 100f, 0.10f, 1.00f);
                float randomJerk = (float)(this.rand.NextDouble() * 2 - 1);

                this.targetVel += randomJerk * (1.2f * jerk) * dt;
                this.targetVel += (0.5f - this.target) * (0.8f + jerk) * dt;

                this.targetVel = Math.Clamp(this.targetVel, -1.25f - jerk, 1.25f + jerk);
                this.target = Math.Clamp(this.target + this.targetVel * dt, 0f, 1f);

                // Zone size
                float baseWidth = Math.Clamp(ModEntry.Instance.Config.SafeZonePercent / 100f, 0.10f, 0.80f);
                float difficultyScale = Math.Clamp(1f - (this.difficulty / 180f), 0.35f, 1f);
                float zoneWidth = baseWidth * difficultyScale;
                float half = zoneWidth / 2f;

                bool inZone = Math.Abs(this.tension - this.target) <= half;

                bool collectingTreasure =
                    ModEntry.Instance.Config.EnableTreasureMinigame &&
                    this.hasTreasure &&
                    !this.treasureCaught &&
                    IsTreasureHeld(ModEntry.Instance.Helper);

                if (ModEntry.Instance.Config.EnableTreasureMinigame && this.hasTreasure && !this.treasureCaught)
                {
                    this.treasureTimer += dt;

                    if (!this.treasureActive && this.treasureTimer >= ModEntry.Instance.Config.TreasureAppearSeconds)
                        this.treasureActive = true;

                    if (this.treasureActive)
                    {
                        float tg = ModEntry.Instance.Config.TreasureGainRate / 20f;
                        float tl = ModEntry.Instance.Config.TreasureLossRate / 20f;

                        this.treasureProgress = Math.Clamp(
                            this.treasureProgress + (collectingTreasure && inZone ? tg : -tl) * dt,
                            0f, 1f);

                        if (this.treasureProgress >= 1f)
                        {
                            this.treasureCaught = true;
                            this.treasureActive = false;
                        }
                    }
                }

                // Perfect logic exception for Treasure Hunter while collecting treasure (matches wiki behavior).
                if (!inZone)
                {
                    bool perfectException = this.hasTreasureHunter && collectingTreasure;
                    if (!perfectException)
                        this.perfectStillPossible = false;
                }

                float gain = ModEntry.Instance.Config.CatchGainRate / 30f;
                float loss = ModEntry.Instance.Config.CatchLossRate / 30f;

                if (!inZone && this.hasTrapBobber)
                    loss *= 0.67f;

                bool freezeLoss = this.hasTreasureHunter && collectingTreasure;

                this.catchProgress = Math.Clamp(
                    this.catchProgress + (inZone ? gain : (freezeLoss ? 0f : -loss)) * dt,
                    0f, 1f);

                if (this.catchProgress >= 1f || this.catchProgress <= 0f)
                    this.FinishedThisTick = true;
            }

            public bool ApplyResultToBobberBar(BobberBar menu)
            {
                try
                {
                    bool success = this.catchProgress >= 1f;

                    // Set every finalize candidate. We push far outside normal range to force vanilla end checks.
                    float forceValue = success ? 999f : -999f;
                    foreach (FieldInfo f in this.catchFinalizeFloats)
                        f.SetValue(menu, forceValue);

                    // Perfect
                    this.fPerfect!.SetValue(menu, success && this.perfectStillPossible);

                    // Treasure (vanilla awards only if caught)
                    if (this.hasTreasure && this.fTreasure is not null)
                    {
                        this.fTreasure.SetValue(menu, true);

                        if (this.fTreasureCatchLevel is not null)
                            this.fTreasureCatchLevel.SetValue(menu, this.treasureProgress);

                        if (this.fTreasureCaught is not null)
                            this.fTreasureCaught.SetValue(menu, this.treasureCaught);
                    }

                    return true;
                }
                catch
                {
                    return false;
                }
            }

            public void Draw(BobberBar menu, SpriteBatch b)
            {
                float uiScale = Game1.options.uiScale;

                // ✅ Don’t use BobberBar’s skinny dimensions; draw our own centered panel.
                int panelW = (int)(560f * uiScale);
                int panelH = (int)(360f * uiScale);

                int x = (Game1.uiViewport.Width - panelW) / 2;
                int y = (Game1.uiViewport.Height - panelH) / 2;

                b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * 0.65f);

                IClickableMenu.drawTextureBox(
                    b,
                    Game1.menuTexture,
                    new Rectangle(0, 256, 60, 60),
                    x, y, panelW, panelH,
                    Color.White,
                    drawShadow: true
                );

                string title = ModEntry.Instance.Helper.Translation.Get("minigame.title");
                Vector2 titleSize = Game1.dialogueFont.MeasureString(title);
                b.DrawString(Game1.dialogueFont, title, new Vector2(x + (panelW - titleSize.X) / 2f, y + 18f * uiScale), Game1.textColor);

                int pad = (int)(26 * uiScale);
                int meterW = panelW - pad * 2;
                int meterH = (int)(28 * uiScale);

                int meterX = x + pad;
                int meterY = y + (int)(78 * uiScale);

                b.Draw(Game1.staminaRect, new Rectangle(meterX, meterY, meterW, meterH), Color.Black * 0.35f);

                float baseWidth = Math.Clamp(ModEntry.Instance.Config.SafeZonePercent / 100f, 0.10f, 0.80f);
                float difficultyScale = Math.Clamp(1f - (this.difficulty / 180f), 0.35f, 1f);
                float zoneWidth = baseWidth * difficultyScale;
                int zonePx = (int)(zoneWidth * meterW);

                int targetPx = meterX + (int)(this.target * meterW);
                int zoneX = Math.Clamp(targetPx - zonePx / 2, meterX, meterX + meterW - zonePx);
                b.Draw(Game1.staminaRect, new Rectangle(zoneX, meterY, zonePx, meterH), Color.LimeGreen * 0.35f);

                int needleX = Math.Clamp(meterX + (int)(this.tension * meterW), meterX, meterX + meterW);
                b.Draw(Game1.staminaRect, new Rectangle(needleX - 1, meterY - 3, 2, meterH + 6), Color.White);

                string instr = ModEntry.Instance.Helper.Translation.Get("minigame.instruction.reel");
                b.DrawString(Game1.smallFont, instr, new Vector2(meterX, meterY + meterH + 10 * uiScale), Game1.textColor);

                if (this.bubblesPresent)
                {
                    string bubbles = ModEntry.Instance.Helper.Translation.Get("minigame.note.bubbles");
                    b.DrawString(Game1.smallFont, bubbles, new Vector2(meterX, meterY + meterH + 34 * uiScale), Color.Cyan);
                }

                int progY = meterY + (int)(92 * uiScale);
                int progH = (int)(18 * uiScale);

                b.Draw(Game1.staminaRect, new Rectangle(meterX, progY, meterW, progH), Color.Black * 0.35f);
                b.Draw(Game1.staminaRect, new Rectangle(meterX, progY, (int)(meterW * this.catchProgress), progH), Color.Orange);

                string catchLabel = ModEntry.Instance.Helper.Translation.Get("minigame.label.catch");
                b.DrawString(Game1.smallFont, catchLabel, new Vector2(meterX, progY - 22 * uiScale), Game1.textColor);

                if (ModEntry.Instance.Config.EnableTreasureMinigame && this.hasTreasure)
                {
                    int tY = progY + (int)(68 * uiScale);

                    string treasureLabel = this.isGoldenTreasure
                        ? ModEntry.Instance.Helper.Translation.Get("minigame.label.treasure.golden")
                        : ModEntry.Instance.Helper.Translation.Get("minigame.label.treasure");

                    b.DrawString(Game1.smallFont, treasureLabel, new Vector2(meterX, tY - 22 * uiScale), Game1.textColor);

                    b.Draw(Game1.staminaRect, new Rectangle(meterX, tY, meterW, progH), Color.Black * 0.35f);
                    b.Draw(Game1.staminaRect, new Rectangle(meterX, tY, (int)(meterW * this.treasureProgress), progH), this.isGoldenTreasure ? Color.Gold : Color.Crimson);

                    string tInstr = ModEntry.Instance.Helper.Translation.Get("minigame.instruction.treasure");
                    b.DrawString(Game1.smallFont, tInstr, new Vector2(meterX, tY + progH + 8 * uiScale), Game1.textColor);
                }

                string hint = ModEntry.Instance.Helper.Translation.Get("minigame.hint.cancel");
                Vector2 hintSize = Game1.smallFont.MeasureString(hint);
                b.DrawString(Game1.smallFont, hint, new Vector2(x + panelW - hintSize.X - 18 * uiScale, y + panelH - hintSize.Y - 16 * uiScale), Color.White * 0.85f);

                // draw mouse cursor (instance method in 1.6)
                menu.drawMouse(b);
            }

            private static bool IsReelHeld(IModHelper helper)
            {
                // Covers common defaults: mouse left, Space, C (tool), X (action), controller A.
                return helper.Input.IsDown(SButton.MouseLeft)
                       || helper.Input.IsDown(SButton.Space)
                       || helper.Input.IsDown(SButton.C)
                       || helper.Input.IsDown(SButton.X)
                       || helper.Input.IsDown(SButton.ControllerA);
            }

            private static bool IsTreasureHeld(IModHelper helper)
            {
                // Shift is nice on keyboard, Y on controller, right click as an alternate.
                return helper.Input.IsDown(SButton.LeftShift)
                       || helper.Input.IsDown(SButton.RightShift)
                       || helper.Input.IsDown(SButton.MouseRight)
                       || helper.Input.IsDown(SButton.ControllerY);
            }

            private static bool DetectBubbles()
            {
                try
                {
                    if (Game1.currentLocation is null)
                        return false;

                    FieldInfo? f = Game1.currentLocation.GetType().GetField("fishSplashPoint", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (f is not null && f.GetValue(Game1.currentLocation) is Vector2 v)
                        return v != Vector2.Zero;
                }
                catch { }
                return false;
            }

            private static FieldInfo? FindFirstBoolField(Type t, string nameContains)
            {
                foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (f.FieldType != typeof(bool))
                        continue;

                    if (f.Name.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0)
                        return f;
                }
                return null;
            }

            private static bool TryGetField<T>(object instance, string name, out T value)
            {
                value = default!;
                try
                {
                    FieldInfo? field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (field is null)
                        return false;

                    object? raw = field.GetValue(instance);
                    if (raw is T cast)
                    {
                        value = cast;
                        return true;
                    }
                }
                catch { }
                return false;
            }

            private static bool TryGetIntFieldContaining(object instance, string contains, out int value)
            {
                value = 0;
                try
                {
                    foreach (FieldInfo f in instance.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        if (f.FieldType != typeof(int))
                            continue;

                        if (f.Name.IndexOf(contains, StringComparison.OrdinalIgnoreCase) < 0)
                            continue;

                        if (f.GetValue(instance) is int i)
                        {
                            value = i;
                            return true;
                        }
                    }
                }
                catch { }
                return false;
            }

            private static bool TryGetAnyBoolFieldContaining(object instance, string contains, out bool value)
            {
                value = false;
                try
                {
                    foreach (FieldInfo f in instance.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        if (f.FieldType != typeof(bool))
                            continue;

                        if (f.Name.IndexOf(contains, StringComparison.OrdinalIgnoreCase) < 0)
                            continue;

                        if (f.GetValue(instance) is bool b)
                        {
                            value = b;
                            return true;
                        }
                    }
                }
                catch { }
                return false;
            }
        }
    }
}
