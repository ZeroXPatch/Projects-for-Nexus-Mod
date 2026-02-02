using System;
using GenericModConfigMenu;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buffs;
using StardewValley.Characters;

namespace HorseMaster
{
    public class ModEntry : Mod
    {
        private ModConfig Config = new();
        private const string BuffId = "HorseMaster.SpeedBuff";
        private int _cachedTargetSpeed = 0;

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.Display.RenderedWorld += OnRenderedWorld;
            helper.Events.Input.ButtonsChanged += OnButtonsChanged;
        }

        private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
        {
            if (this.Config.SummonKey.JustPressed())
            {
                SummonHorse();
            }
        }

        private void SummonHorse()
        {
            if (!Context.IsPlayerFree || Game1.player.isRidingHorse())
                return;

            Horse? foundHorse = null;

            // Search for the player's horse
            foreach (NPC npc in Utility.getAllCharacters())
            {
                if (npc is Horse horse)
                {
                    // Check ownership safely
                    if (horse.ownerId.Value == Game1.player.UniqueMultiplayerID)
                    {
                        foundHorse = horse;
                        break;
                    }
                    else if (!Context.IsMultiplayer && foundHorse == null)
                    {
                        foundHorse = horse;
                    }
                }
            }

            if (foundHorse != null)
            {
                // FIX: Use 'Name' (string) and 'TilePoint' (Point)
                // This forces the game to place the horse on the specific Tile, not "Pixels as Tiles"
                Game1.warpCharacter(foundHorse, Game1.player.currentLocation.Name, Game1.player.TilePoint);

                // Play Sound and Show Message
                Game1.playSound("horse_flute");
                Game1.addHUDMessage(new HUDMessage(this.Helper.Translation.Get("msg.summoned"), HUDMessage.newQuest_type));
            }
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.player == null)
                return;

            // 1. INSTANT CHECK: Remove buff if not riding
            if (!Game1.player.isRidingHorse())
            {
                if (Game1.player.buffs.IsApplied(BuffId))
                    Game1.player.buffs.Remove(BuffId);
                return;
            }

            // 2. CALCULATION CHECK: Every 15 ticks
            if (e.IsMultipleOf(15))
                CalculateTargetSpeed();

            // 3. APPLY BUFF
            ApplyHorseBuff();
        }

        private void CalculateTargetSpeed()
        {
            if (!this.Config.UseAdaptiveSpeed)
            {
                _cachedTargetSpeed = this.Config.ConstantSpeed;
                return;
            }

            float current = Game1.player.Stamina;
            float max = Game1.player.MaxStamina > 0 ? Game1.player.MaxStamina : 1;
            float percent = (current / max) * 100f;

            if (percent > 99.5f) _cachedTargetSpeed = this.Config.Speed_100;
            else if (percent >= 70f) _cachedTargetSpeed = this.Config.Speed_99_to_70;
            else if (percent >= 40f) _cachedTargetSpeed = this.Config.Speed_69_to_40;
            else if (percent >= 10f) _cachedTargetSpeed = this.Config.Speed_39_to_10;
            else _cachedTargetSpeed = this.Config.Speed_09_to_00;
        }

        private void ApplyHorseBuff()
        {
            if (_cachedTargetSpeed == 0)
            {
                if (Game1.player.buffs.IsApplied(BuffId))
                    Game1.player.buffs.Remove(BuffId);
                return;
            }

            if (Game1.player.buffs.IsApplied(BuffId))
            {
                var existing = Game1.player.buffs.AppliedBuffs[BuffId];
                if (existing.effects.Speed.Value == _cachedTargetSpeed)
                    return;
            }

            Buff horseBuff = new Buff(
                id: BuffId,
                displayName: "Horse Master",
                iconTexture: null,
                iconSheetIndex: 0,
                duration: Buff.ENDLESS,
                effects: new BuffEffects() { Speed = { _cachedTargetSpeed } }
            );

            Game1.player.applyBuff(horseBuff);
        }

        private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
        {
            if (this.Config.DebugMode && Context.IsWorldReady && Game1.player != null && !Game1.eventUp && Game1.player.isRidingHorse())
            {
                Vector2 screenPos = Game1.GlobalToLocal(Game1.viewport, Game1.player.Position);
                Vector2 textPos = new Vector2(screenPos.X, screenPos.Y - 120);
                float p = (Game1.player.Stamina / (float)Math.Max(1, Game1.player.MaxStamina)) * 100f;
                e.SpriteBatch.DrawString(Game1.dialogueFont, $"Horse Master\nEnergy: {p:0.0}%\nSpeed+: {_cachedTargetSpeed}", textPos, Color.Orange);
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

            configMenu.AddKeybindList(
                mod: this.ModManifest,
                getValue: () => this.Config.SummonKey,
                setValue: value => this.Config.SummonKey = value,
                name: () => this.Helper.Translation.Get("config.hotkey.name"),
                tooltip: () => this.Helper.Translation.Get("config.hotkey.tooltip")
            );

            configMenu.AddBoolOption(this.ModManifest, () => this.Config.DebugMode, v => this.Config.DebugMode = v,
                () => this.Helper.Translation.Get("config.debug.name"), () => this.Helper.Translation.Get("config.debug.tooltip"));
            configMenu.AddBoolOption(this.ModManifest, () => this.Config.UseAdaptiveSpeed, v => this.Config.UseAdaptiveSpeed = v,
                () => this.Helper.Translation.Get("config.adaptive.name"), () => this.Helper.Translation.Get("config.adaptive.tooltip"));

            // Constant
            configMenu.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("config.section.constant"));
            AddSpeedSlider(configMenu, "config.constant.name", () => this.Config.ConstantSpeed, v => this.Config.ConstantSpeed = v, "config.constant.tooltip");

            // Adaptive
            configMenu.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("config.section.adaptive"));
            configMenu.AddParagraph(this.ModManifest, () => this.Helper.Translation.Get("config.adaptive.note"));

            AddSpeedSlider(configMenu, "config.energy.100", () => this.Config.Speed_100, v => this.Config.Speed_100 = v);
            AddSpeedSlider(configMenu, "config.energy.99_70", () => this.Config.Speed_99_to_70, v => this.Config.Speed_99_to_70 = v);
            AddSpeedSlider(configMenu, "config.energy.69_40", () => this.Config.Speed_69_to_40, v => this.Config.Speed_69_to_40 = v);
            AddSpeedSlider(configMenu, "config.energy.39_10", () => this.Config.Speed_39_to_10, v => this.Config.Speed_39_to_10 = v);
            AddSpeedSlider(configMenu, "config.energy.09_00", () => this.Config.Speed_09_to_00, v => this.Config.Speed_09_to_00 = v);
        }

        private void AddSpeedSlider(IGenericModConfigMenuApi api, string labelKey, Func<int> get, Action<int> set, string? tooltipKey = null)
        {
            api.AddNumberOption(
                mod: this.ModManifest,
                getValue: get,
                setValue: set,
                name: () => this.Helper.Translation.Get(labelKey),
                tooltip: tooltipKey != null ? () => this.Helper.Translation.Get(tooltipKey) : null,
                min: -5, max: 10, interval: 1
            );
        }
    }
}