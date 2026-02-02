using GenericModConfigMenu;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System;
using System.IO;

namespace BlackAndWhiteMod
{
    public class ModEntry : Mod
    {
        private ModConfig Config;
        private RenderTarget2D _screenBuffer;
        private Effect _grayscaleEffect;
        private bool _shaderLoaded = false;

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.Display.Rendering += this.OnRendering;
            helper.Events.Display.Rendered += this.OnRendered;
            helper.Events.Display.WindowResized += this.OnWindowResized;
        }

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            // --- Path diagnostics (check SMAPI log) ---
            string modDir = this.Helper.DirectoryPath;
            string expectedXnb = Path.Combine(modDir, "assets", "grayscale.xnb");

            this.Monitor.Log($"[DIAG] Mod directory      : {modDir}", LogLevel.Warn);
            this.Monitor.Log($"[DIAG] Expected xnb path  : {expectedXnb}", LogLevel.Warn);
            this.Monitor.Log($"[DIAG] xnb file exists?   : {File.Exists(expectedXnb)}", LogLevel.Warn);

            // List everything actually in the mod directory (2 levels)
            if (Directory.Exists(modDir))
            {
                foreach (var f in Directory.GetFiles(modDir, "*", SearchOption.AllDirectories))
                    this.Monitor.Log($"[DIAG] Found file: {f}", LogLevel.Warn);
            }
            // --- end diagnostics ---

            try
            {
                // Use a raw ContentManager pointed at the mod's directory.
                // This loads the .xnb directly from disk without relying on
                // SMAPI's asset pipeline, so it works regardless of whether
                // the build step copied the file correctly.
                var contentManager = new ContentManager(Game1.content.ServiceProvider, modDir);
                _grayscaleEffect = contentManager.Load<Effect>("assets/grayscale");
                _shaderLoaded = true;
                this.Monitor.Log("Grayscale shader loaded successfully.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                this.Monitor.Log("--------------------------------------------------", LogLevel.Error);
                this.Monitor.Log("SHADER NOT FOUND", LogLevel.Error);
                this.Monitor.Log($"Looked in: {expectedXnb}", LogLevel.Error);
                this.Monitor.Log("--------------------------------------------------", LogLevel.Error);
                this.Monitor.Log(ex.ToString(), LogLevel.Error);
            }

            // GMCM Setup
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu != null)
            {
                configMenu.Register(
                    mod: this.ModManifest,
                    reset: () => this.Config = new ModConfig(),
                    save: () => this.Helper.WriteConfig(this.Config)
                );
                configMenu.AddBoolOption(
                    mod: this.ModManifest,
                    name: () => "Enable Filter",
                    getValue: () => this.Config.Enabled,
                    setValue: value => this.Config.Enabled = value
                );
            }
        }

        private void OnWindowResized(object sender, WindowResizedEventArgs e)
        {
            _screenBuffer?.Dispose();
            _screenBuffer = null;
        }

        private void EnsureBuffer()
        {
            var device = Game1.graphics.GraphicsDevice;
            if (_screenBuffer == null ||
                _screenBuffer.Width != device.PresentationParameters.BackBufferWidth ||
                _screenBuffer.Height != device.PresentationParameters.BackBufferHeight)
            {
                _screenBuffer?.Dispose();
                _screenBuffer = new RenderTarget2D(
                    device,
                    device.PresentationParameters.BackBufferWidth,
                    device.PresentationParameters.BackBufferHeight,
                    false,
                    device.PresentationParameters.BackBufferFormat,
                    DepthFormat.Depth24Stencil8
                );
            }
        }

        private void OnRendering(object sender, RenderingEventArgs e)
        {
            if (!this.Config.Enabled || !_shaderLoaded) return;

            EnsureBuffer();
            Game1.graphics.GraphicsDevice.SetRenderTarget(_screenBuffer);
            Game1.graphics.GraphicsDevice.Clear(Color.Black);
        }

        private void OnRendered(object sender, RenderedEventArgs e)
        {
            if (!this.Config.Enabled || !_shaderLoaded || _screenBuffer == null) return;

            Game1.graphics.GraphicsDevice.SetRenderTarget(null);

            Game1.spriteBatch.Begin(
                SpriteSortMode.Immediate,
                BlendState.AlphaBlend,
                SamplerState.PointClamp,
                DepthStencilState.Default,
                RasterizerState.CullNone,
                effect: _grayscaleEffect
            );

            Game1.spriteBatch.Draw(_screenBuffer, Vector2.Zero, Color.White);
            Game1.spriteBatch.End();
        }
    }
}