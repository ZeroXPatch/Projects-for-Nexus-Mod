using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace SleepAnywhereMod
{
    public class ModEntry : Mod
    {
        private ModConfig Config = new();

        // Button State
        private Rectangle ButtonBounds;
        private Rectangle TextureSourceRect;
        private Texture2D? ButtonTexture;
        private bool IsHovering = false;

        // Visual Settings
        private const int BaseScale = 4;
        private const string ButtonText = "Sleep";

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            // Source Rect for a blank wood piece (generic wood tile)
            this.TextureSourceRect = new Rectangle(341, 408, 14, 15);

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.Display.RenderedHud += OnRenderedHud;
            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            RecalculateButtonBounds();

            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null) return;

            configMenu.Register(
                mod: this.ModManifest,
                reset: () => { this.Config = new ModConfig(); RecalculateButtonBounds(); },
                save: () => { this.Helper.WriteConfig(this.Config); RecalculateButtonBounds(); }
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Show Button",
                tooltip: () => "Toggle the floating sleep button.",
                getValue: () => this.Config.ShowButton,
                setValue: value => this.Config.ShowButton = value
            );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => "Button X",
                getValue: () => this.Config.ButtonXPosition,
                setValue: value => this.Config.ButtonXPosition = value,
                min: 0, max: 4000
            );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => "Button Y",
                getValue: () => this.Config.ButtonYPosition,
                setValue: value => this.Config.ButtonYPosition = value,
                min: 0, max: 2500
            );

            configMenu.AddKeybind(
                mod: this.ModManifest,
                name: () => "Short Cut",
                getValue: () => this.Config.SleepKey,
                setValue: value => this.Config.SleepKey = value
            );
        }

        private void RecalculateButtonBounds()
        {
            // Make the button slightly wider to fit the text "Sleep" nicely
            int width = 20 * BaseScale;
            int height = 15 * BaseScale;

            ButtonBounds = new Rectangle(
                this.Config.ButtonXPosition,
                this.Config.ButtonYPosition,
                width,
                height
            );
        }

        private void OnUpdateTicked(object? sender, EventArgs e)
        {
            if (!Context.IsWorldReady || !this.Config.ShowButton) return;

            var cursorPos = this.Helper.Input.GetCursorPosition().GetScaledScreenPixels();
            IsHovering = ButtonBounds.Contains((int)cursorPos.X, (int)cursorPos.Y);
        }

        private void OnRenderedHud(object? sender, RenderedHudEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.currentMinigame != null || Game1.activeClickableMenu != null || Game1.eventUp)
                return;

            if (!this.Config.ShowButton)
                return;

            if (ButtonTexture == null) ButtonTexture = Game1.mouseCursors;

            float scale = IsHovering ? 1.05f : 1.0f;

            // 1. Draw the Blank Wood Button Background
            e.SpriteBatch.Draw(
                ButtonTexture,
                new Rectangle(
                    ButtonBounds.X - (int)((ButtonBounds.Width * (scale - 1)) / 2),
                    ButtonBounds.Y - (int)((ButtonBounds.Height * (scale - 1)) / 2),
                    (int)(ButtonBounds.Width * scale),
                    (int)(ButtonBounds.Height * scale)
                ),
                TextureSourceRect,
                Color.White
            );

            // 2. Draw "Sleep" Text
            Vector2 textSize = Game1.smallFont.MeasureString(ButtonText);
            Vector2 textPos = new Vector2(
                ButtonBounds.X + (ButtonBounds.Width / 2) - (textSize.X / 2),
                ButtonBounds.Y + (ButtonBounds.Height / 2) - (textSize.Y / 2) - 2
            );

            e.SpriteBatch.DrawString(Game1.smallFont, ButtonText, textPos + new Vector2(2, 2), Game1.textShadowColor);
            e.SpriteBatch.DrawString(Game1.smallFont, ButtonText, textPos, Game1.textColor);
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.currentMinigame != null || Game1.activeClickableMenu != null || Game1.eventUp)
                return;

            if (e.Button == this.Config.SleepKey)
            {
                AttemptSleep();
                return;
            }

            if (e.Button == SButton.MouseLeft && this.Config.ShowButton && IsHovering)
            {
                this.Helper.Input.Suppress(SButton.MouseLeft);
                Game1.playSound("bigDeSelect");
                AttemptSleep();
            }
        }

        private void AttemptSleep()
        {
            Game1.currentLocation.createQuestionDialogue(
                "Go to sleep now?",
                Game1.currentLocation.createYesNoResponses(),
                (Farmer _, string answer) =>
                {
                    if (answer == "Yes")
                    {
                        Game1.exitActiveMenu(); // Fixes the "Wait for map change" lag
                        Game1.player.isInBed.Value = true;
                        Game1.globalFadeToBlack(RunSleep, 0.01f);
                    }
                }
            );
        }

        private void RunSleep()
        {
            Game1.newDay = true;
        }
    }

    // THE FIX IS HERE: Updated formatValue to 'Func<int, string>?'
    public interface IGenericModConfigMenuApi
    {
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);
        void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue, Func<string> name, Func<string>? tooltip = null, string? fieldId = null);
        void AddNumberOption(IManifest mod, Func<int> getValue, Action<int> setValue, Func<string> name, Func<string>? tooltip = null, int? min = null, int? max = null, int? interval = null, Func<int, string>? formatValue = null, string? fieldId = null);
        void AddKeybind(IManifest mod, Func<SButton> getValue, Action<SButton> setValue, Func<string> name, Func<string>? tooltip = null, string? fieldId = null);
    }
}