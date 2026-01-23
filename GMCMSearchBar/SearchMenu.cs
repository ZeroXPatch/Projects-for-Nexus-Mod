using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GMCMSearchBar
{
    internal sealed class SearchMenu : IClickableMenu
    {
        private readonly IModHelper helper;
        private readonly IMonitor monitor;
        private readonly Func<IManifest, bool> openMod;
        private readonly bool showUniqueId;
        private readonly string title;

        private readonly List<IManifest> all;
        private readonly List<IManifest> filtered = new();

        private TextBox searchBox = null!;
        private ClickableTextureComponent upperRightCloseButton = null!;
        private string lastSearch = "";

        private Rectangle searchRect;
        private Rectangle listRect;
        private Rectangle scrollTrackRect;

        private int selectedIndex = 0;
        private int scrollOffset = 0;

        private bool draggingThumb = false;
        private int dragGrabOffsetY = 0;

        // UI tuning knobs
        private const int OuterPad = 32;
        private const int TitleGap = 14;
        private const int SearchHeight = 48;
        private const int InstructionHeight = 28;
        private const int ScrollbarWidth = 24;
        private const int RowPadX = 14;
        private const int RowPadTop = 10;
        private const int RowPadBottom = 8;
        private const int RowLineGap = 4;

        private readonly SpriteFont nameFont;
        private readonly SpriteFont idFont;
        private readonly int nameLineH;
        private readonly int idLineH;

        private int RowHeight =>
            RowPadTop
            + this.nameLineH
            + (this.showUniqueId ? (RowLineGap + this.idLineH) : 0)
            + RowPadBottom;

        private const int ScrollOuterBorder = 2;
        private const int ScrollInnerPad = 4;
        private const int ThumbMinHeight = 44;

        public SearchMenu(IModHelper helper, IMonitor monitor, string title, bool showUniqueId, List<IManifest> mods, Func<IManifest, bool> openMod)
            // Disable base close button to manually position it
            : base(0, 0, 0, 0, showUpperRightCloseButton: false)
        {
            this.helper = helper;
            this.monitor = monitor;
            this.title = title;
            this.showUniqueId = showUniqueId;
            this.openMod = openMod;

            this.nameFont = Game1.dialogueFont;
            this.idFont = Game1.smallFont;
            this.nameLineH = (int)Math.Ceiling(this.nameFont.MeasureString("ABC").Y);
            this.idLineH = (int)Math.Ceiling(this.idFont.MeasureString("ABC").Y);

            this.all = mods
                .Where(m => m is not null)
                .OrderBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(m => m.UniqueID, StringComparer.OrdinalIgnoreCase)
                .ToList();

            this.filtered.AddRange(this.all);

            this.InitializeLayout();

            Texture2D tex = Game1.content.Load<Texture2D>("LooseSprites/textBox");
            this.searchBox = new TextBox(tex, null, Game1.smallFont, Game1.textColor)
            {
                X = this.searchRect.X + 8,
                Y = this.searchRect.Y + 8,
                Width = this.searchRect.Width - 16,
                Text = ""
            };

            // Select it immediately
            this.searchBox.Selected = true;
            Game1.keyboardDispatcher.Subscriber = this.searchBox;
        }

        protected override void cleanupBeforeExit()
        {
            if (Game1.keyboardDispatcher.Subscriber == this.searchBox)
                Game1.keyboardDispatcher.Subscriber = null;

            base.cleanupBeforeExit();
        }

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            this.InitializeLayout();

            this.searchBox.X = this.searchRect.X + 8;
            this.searchBox.Y = this.searchRect.Y + 8;
            this.searchBox.Width = this.searchRect.Width - 16;

            this.ClampScroll();
        }

        private void InitializeLayout()
        {
            int vw = Game1.uiViewport.Width;
            int vh = Game1.uiViewport.Height;

            int maxW = Math.Max(640, vw - 80);
            int maxH = Math.Max(520, vh - 80);

            this.width = Math.Min(1100, maxW);
            this.height = Math.Min(860, maxH);

            this.xPositionOnScreen = (vw - this.width) / 2;
            this.yPositionOnScreen = (vh - this.height) / 2;

            // Manual close button positioning
            this.upperRightCloseButton = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + this.width - 36, this.yPositionOnScreen - 8, 48, 48),
                Game1.mouseCursors,
                new Rectangle(337, 494, 12, 12),
                4f
            );

            int innerX = this.xPositionOnScreen + OuterPad;
            int innerY = this.yPositionOnScreen + OuterPad;

            int innerW = this.width - OuterPad * 2;
            int innerH = this.height - OuterPad * 2;

            int titleH = SpriteText.getHeightOfString(this.title) + TitleGap;

            this.searchRect = new Rectangle(
                x: innerX,
                y: innerY + titleH,
                width: innerW,
                height: SearchHeight
            );

            int instructionY = this.searchRect.Bottom + 10;
            int listTop = instructionY + InstructionHeight + 8;
            int listHeight = innerY + innerH - listTop;

            this.listRect = new Rectangle(
                x: innerX,
                y: listTop,
                width: innerW - ScrollbarWidth - 10,
                height: listHeight
            );

            this.scrollTrackRect = new Rectangle(
                x: this.listRect.Right + 10,
                y: this.listRect.Y,
                width: ScrollbarWidth,
                height: this.listRect.Height
            );
        }

        private int ItemsPerPage => Math.Max(1, this.listRect.Height / this.RowHeight);
        private int MaxScroll => Math.Max(0, this.filtered.Count - this.ItemsPerPage);

        private void ClampScroll()
        {
            this.scrollOffset = Math.Clamp(this.scrollOffset, 0, this.MaxScroll);
            this.selectedIndex = Math.Clamp(this.selectedIndex, 0, Math.Max(0, this.filtered.Count - 1));
            this.EnsureSelectionVisible();
        }

        private void EnsureSelectionVisible()
        {
            if (this.filtered.Count == 0) return;

            if (this.selectedIndex < this.scrollOffset)
                this.scrollOffset = this.selectedIndex;

            int bottomIndex = this.scrollOffset + this.ItemsPerPage - 1;
            if (this.selectedIndex > bottomIndex)
                this.scrollOffset = Math.Min(this.MaxScroll, this.selectedIndex - this.ItemsPerPage + 1);
        }

        private void ApplyFilter()
        {
            string q = (this.searchBox.Text ?? "").Trim();
            this.filtered.Clear();

            if (string.IsNullOrWhiteSpace(q))
            {
                this.filtered.AddRange(this.all);
            }
            else
            {
                string[] parts = q.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var m in this.all)
                {
                    string hay1 = m.Name ?? "";
                    string hay2 = m.UniqueID ?? "";
                    bool ok = true;
                    foreach (string p in parts)
                    {
                        if (hay1.IndexOf(p, StringComparison.CurrentCultureIgnoreCase) < 0
                            && hay2.IndexOf(p, StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            ok = false;
                            break;
                        }
                    }
                    if (ok) this.filtered.Add(m);
                }
            }

            this.selectedIndex = 0;
            this.scrollOffset = 0;
            this.ClampScroll();
        }

        public override void update(GameTime time)
        {
            base.update(time);
            this.searchBox.Update();

            string now = this.searchBox.Text ?? "";
            if (!string.Equals(now, this.lastSearch, StringComparison.Ordinal))
            {
                this.lastSearch = now;
                this.ApplyFilter();
            }
        }

        public override void performHoverAction(int x, int y)
        {
            base.performHoverAction(x, y);
            this.upperRightCloseButton.tryHover(x, y, 0.25f);
        }

        public override void receiveKeyPress(Keys key)
        {
            // IGNORE 'E' (or Menu Button) if the user is typing in the box
            if (Game1.options.doesInputListContain(Game1.options.menuButton, key) && this.searchBox.Selected)
            {
                return;
            }

            if (key == Keys.Escape)
            {
                this.exitThisMenu();
                return;
            }

            if (key == Keys.Enter)
            {
                this.OpenSelected();
                return;
            }

            // Handle WASD/Arrow navigation manually to prevent cursor snapping
            if (key == Keys.Up)
            {
                if (this.filtered.Count > 0)
                {
                    this.selectedIndex = Math.Max(0, this.selectedIndex - 1);
                    this.EnsureSelectionVisible();
                }
                return;
            }
            if (key == Keys.Down)
            {
                if (this.filtered.Count > 0)
                {
                    this.selectedIndex = Math.Min(this.filtered.Count - 1, this.selectedIndex + 1);
                    this.EnsureSelectionVisible();
                }
                return;
            }
            if (key == Keys.PageUp)
            {
                this.selectedIndex = Math.Max(0, this.selectedIndex - this.ItemsPerPage);
                this.EnsureSelectionVisible();
                return;
            }
            if (key == Keys.PageDown)
            {
                this.selectedIndex = Math.Min(Math.Max(0, this.filtered.Count - 1), this.selectedIndex + this.ItemsPerPage);
                this.EnsureSelectionVisible();
                return;
            }
            if (key == Keys.Home)
            {
                this.selectedIndex = 0;
                this.scrollOffset = 0;
                this.EnsureSelectionVisible();
                return;
            }
            if (key == Keys.End)
            {
                this.selectedIndex = Math.Max(0, this.filtered.Count - 1);
                this.scrollOffset = this.MaxScroll;
                this.EnsureSelectionVisible();
                return;
            }

            // If it's the menu button (and we aren't typing), close
            if (!this.searchBox.Selected && Game1.options.doesInputListContain(Game1.options.menuButton, key))
            {
                this.exitThisMenu();
                return;
            }

            base.receiveKeyPress(key);
        }

        public override void receiveScrollWheelAction(int direction)
        {
            base.receiveScrollWheelAction(direction);
            if (!this.listRect.Contains(Game1.getMouseX(), Game1.getMouseY()))
                return;

            int delta = direction > 0 ? -1 : 1;
            this.scrollOffset = Math.Clamp(this.scrollOffset + delta, 0, this.MaxScroll);
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            if (this.upperRightCloseButton.containsPoint(x, y))
            {
                if (playSound) Game1.playSound("bigDeSelect");
                this.exitThisMenu();
                return;
            }

            base.receiveLeftClick(x, y, playSound);

            if (this.searchRect.Contains(x, y))
            {
                this.searchBox.SelectMe();
                return;
            }

            if (this.listRect.Contains(x, y))
            {
                int row = (y - this.listRect.Y) / this.RowHeight;
                int idx = this.scrollOffset + row;

                if (idx >= 0 && idx < this.filtered.Count)
                {
                    this.selectedIndex = idx;
                    this.OpenSelected();
                }
                return;
            }

            if (this.scrollTrackRect.Contains(x, y))
            {
                Rectangle thumb = this.GetScrollThumbRect();
                if (thumb.Contains(x, y))
                {
                    this.draggingThumb = true;
                    this.dragGrabOffsetY = y - thumb.Y;
                }
                else
                {
                    this.SetScrollFromThumbCenter(y);
                }
            }
        }

        public override void leftClickHeld(int x, int y)
        {
            base.leftClickHeld(x, y);
            if (!this.draggingThumb) return;
            this.SetScrollFromThumbTop(y - this.dragGrabOffsetY);
        }

        public override void releaseLeftClick(int x, int y)
        {
            base.releaseLeftClick(x, y);
            this.draggingThumb = false;
        }

        private void OpenSelected()
        {
            if (this.filtered.Count == 0) return;
            this.selectedIndex = Math.Clamp(this.selectedIndex, 0, this.filtered.Count - 1);
            var target = this.filtered[this.selectedIndex];

            bool ok = this.openMod(target);
            if (!ok)
            {
                Game1.showRedMessage("That entry couldn't be opened in GMCM.");
                this.all.RemoveAll(m => m.UniqueID == target.UniqueID);
                this.filtered.RemoveAll(m => m.UniqueID == target.UniqueID);
                this.ClampScroll();
            }
        }

        // ===== Scrollbar Helpers =====
        private Rectangle GetScrollRailRect()
        {
            int inset = ScrollOuterBorder + ScrollInnerPad;
            return InsetRect(this.scrollTrackRect, inset, inset);
        }

        private Rectangle GetScrollThumbRect()
        {
            Rectangle rail = this.GetScrollRailRect();
            if (rail.Width <= 0 || rail.Height <= 0)
                return new Rectangle(this.scrollTrackRect.X, this.scrollTrackRect.Y, this.scrollTrackRect.Width, 1);

            int count = this.filtered.Count;
            int page = this.ItemsPerPage;
            float ratio = Math.Min(1f, (float)page / Math.Max(1, count));
            int thumbH = (int)Math.Round(rail.Height * ratio);
            thumbH = Math.Max(ThumbMinHeight, Math.Min(thumbH, rail.Height));

            int minY = rail.Y;
            int maxY = rail.Bottom - thumbH;
            float t = this.MaxScroll == 0 ? 0f : (float)this.scrollOffset / this.MaxScroll;
            int thumbY = (int)Math.Round(minY + (maxY - minY) * t);

            return new Rectangle(rail.X + 2, thumbY, Math.Max(1, rail.Width - 4), thumbH);
        }

        private void SetScrollFromThumbTop(int thumbTopY)
        {
            Rectangle rail = this.GetScrollRailRect();
            Rectangle thumb = this.GetScrollThumbRect();
            int minY = rail.Y;
            int maxY = rail.Bottom - thumb.Height;
            int clamped = Math.Clamp(thumbTopY, minY, maxY);
            float t = (maxY == minY) ? 0f : (float)(clamped - minY) / (maxY - minY);
            this.scrollOffset = (int)Math.Round(t * this.MaxScroll);
        }

        private void SetScrollFromThumbCenter(int mouseY)
        {
            Rectangle thumb = this.GetScrollThumbRect();
            this.SetScrollFromThumbTop(mouseY - thumb.Height / 2);
        }

        private void DrawScrollbar(SpriteBatch b)
        {
            Rectangle trackOuter = this.scrollTrackRect;
            Rectangle rail = this.GetScrollRailRect();

            DrawBorder(b, trackOuter, ScrollOuterBorder, new Color(0, 0, 0, 90));
            FillRect(b, InsetRect(trackOuter, ScrollOuterBorder, ScrollOuterBorder), new Color(0, 0, 0, 25));
            FillRect(b, rail, new Color(0, 0, 0, 35));

            Rectangle thumb = this.GetScrollThumbRect();
            if (thumb.Height > 0 && thumb.Width > 0)
            {
                FillRect(b, thumb, new Color(255, 255, 255, 170));
                DrawBorder(b, thumb, 1, new Color(0, 0, 0, 110));
            }
        }

        private static Rectangle InsetRect(Rectangle r, int dx, int dy)
        {
            return new Rectangle(r.X + dx, r.Y + dy, Math.Max(0, r.Width - dx * 2), Math.Max(0, r.Height - dy * 2));
        }

        private static void FillRect(SpriteBatch b, Rectangle rect, Color color)
        {
            if (rect.Width > 0 && rect.Height > 0)
                b.Draw(Game1.fadeToBlackRect, rect, color);
        }

        private static void DrawBorder(SpriteBatch b, Rectangle rect, int thickness, Color color)
        {
            if (thickness <= 0 || rect.Width <= 0 || rect.Height <= 0) return;
            FillRect(b, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            FillRect(b, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
            FillRect(b, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            FillRect(b, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
        }

        public override void draw(SpriteBatch b)
        {
            // Dark overlay instead of forest background
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.75f);

            IClickableMenu.drawTextureBox(
                b,
                x: this.xPositionOnScreen,
                y: this.yPositionOnScreen,
                width: this.width,
                height: this.height,
                color: Color.White
            );

            // Draw manual close button
            this.upperRightCloseButton.draw(b);

            int titleX = this.xPositionOnScreen + this.width / 2;
            int titleY = this.yPositionOnScreen + OuterPad - 4;
            SpriteText.drawStringHorizontallyCenteredAt(b, this.title, titleX, titleY);

            IClickableMenu.drawTextureBox(b, this.searchRect.X, this.searchRect.Y, this.searchRect.Width, this.searchRect.Height, Color.White);
            this.searchBox.Draw(b);

            if (string.IsNullOrEmpty(this.searchBox.Text) && !this.searchBox.Selected)
            {
                Utility.drawTextWithShadow(
                    b,
                    "Type to filter…",
                    Game1.smallFont,
                    new Vector2(this.searchRect.X + 18, this.searchRect.Y + 14),
                    new Color(120, 120, 120)
                );
            }

            Vector2 instrPos = new Vector2(this.searchRect.X, this.searchRect.Bottom + 10);
            Utility.drawTextWithShadow(b, "Click a result or press Enter to open its config in GMCM.", Game1.smallFont, instrPos, Game1.textColor);

            IClickableMenu.drawTextureBox(b, this.listRect.X, this.listRect.Y, this.listRect.Width, this.listRect.Height, Color.White);

            int visible = this.ItemsPerPage;
            int start = this.scrollOffset;
            int end = Math.Min(this.filtered.Count, start + visible);

            for (int i = start; i < end; i++)
            {
                int rowIndex = i - start;
                int y = this.listRect.Y + rowIndex * this.RowHeight;

                Rectangle rowRect = new Rectangle(this.listRect.X + 6, y + 2, this.listRect.Width - 12, this.RowHeight - 4);

                bool selected = (i == this.selectedIndex);
                if (selected)
                    IClickableMenu.drawTextureBox(b, rowRect.X, rowRect.Y, rowRect.Width, rowRect.Height, Color.White);

                var m = this.filtered[i];

                Vector2 namePos = new Vector2(rowRect.X + RowPadX, rowRect.Y + RowPadTop);
                Utility.drawTextWithShadow(b, m.Name ?? m.UniqueID, this.nameFont, namePos, Game1.textColor);

                if (this.showUniqueId)
                {
                    Vector2 idPos = new Vector2(
                        rowRect.X + RowPadX,
                        rowRect.Y + RowPadTop + this.nameLineH + RowLineGap
                    );
                    Utility.drawTextWithShadow(b, m.UniqueID ?? "", this.idFont, idPos, new Color(90, 90, 90));
                }
            }

            this.DrawScrollbar(b);
            this.drawMouse(b);
        }
    }
}