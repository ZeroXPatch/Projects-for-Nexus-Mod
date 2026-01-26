using StardewValley.Menus;
using StardewValley;
using Microsoft.Xna.Framework.Graphics;
using System;
using StardewModdingAPI;

namespace FasterMenuLoad
{
    public class LazyTab : IClickableMenu
    {
        private readonly Func<int, int, int, int, IClickableMenu> _pageGenerator;

        // We must track these so we can generate the page with the correct size 
        // even if the window was resized before the user clicked the tab.
        private int _lastX, _lastY, _lastW, _lastH;

        public LazyTab(int x, int y, int w, int h, Func<int, int, int, int, IClickableMenu> pageGenerator)
            : base(x, y, w, h)
        {
            _lastX = x;
            _lastY = y;
            _lastW = w;
            _lastH = h;
            _pageGenerator = pageGenerator;
        }

        public IClickableMenu CreateRealPage()
        {
            // Only log if debug logging is enabled
            if (ModEntry.Config.EnableDebugLogging)
                ModEntry.ModMonitor.Log($"[LazyTab] Activated! Generating real page at {_lastX}, {_lastY}", LogLevel.Alert);

            // Use the most recent known coordinates
            return _pageGenerator(_lastX, _lastY, _lastW, _lastH);
        }

        // IMPORTANT: If the user resizes the window while this tab is dormant,
        // we must update our coordinates so the real page loads in the right spot later.
        public override void gameWindowSizeChanged(Microsoft.Xna.Framework.Rectangle oldBounds, Microsoft.Xna.Framework.Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            // The GameMenu updates xPositionOnScreen etc automatically for the active page,
            // but we need to ensure we capture where the GameMenu *thinks* this page should be.
            _lastX = this.xPositionOnScreen;
            _lastY = this.yPositionOnScreen;
            _lastW = this.width;
            _lastH = this.height;
        }

        // Empty overrides to prevent logic from running on a blank page
        public override void draw(SpriteBatch b) { }
        public override void receiveLeftClick(int x, int y, bool playSound = true) { }
        public override void receiveRightClick(int x, int y, bool playSound = true) { }
        public override void performHoverAction(int x, int y) { }

        // Controller safety: Prevent crashing if the game tries to snap cursor to this page
        public override void snapToDefaultClickableComponent() { }
    }
}