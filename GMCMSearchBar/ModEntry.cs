using GMCMSearchBar.Framework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GMCMSearchBar
{
    public sealed class ModEntry : Mod
    {
        private ModConfig Config = new();
        private IGenericModConfigMenuApi? Gmcm;

        // cached list of GMCM-registered manifests (refreshed when opening)
        private List<IManifest> Registered = new();

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            this.Gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (this.Gmcm is null)
            {
                this.Monitor.Log("GMCM not found. This mod requires Generic Mod Config Menu to function.", LogLevel.Warn);
                return;
            }

            // Register our own config in GMCM
            this.Gmcm.Register(
                mod: this.ModManifest,
                reset: () => this.Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.Config)
            );

            this.Gmcm.AddSectionTitle(
                this.ModManifest,
                text: () => "GMCM Search",
                tooltip: () => "Search and open other mods' GMCM configs quickly."
            );

            this.Gmcm.AddKeybindList(
                this.ModManifest,
                getValue: () => this.Config.OpenSearchMenuKey,
                setValue: v => this.Config.OpenSearchMenuKey = v,
                name: () => "Open search menu",
                tooltip: () => "Hotkey to open the GMCM Search menu."
            );

            this.Gmcm.AddBoolOption(
                this.ModManifest,
                getValue: () => this.Config.ShowUniqueId,
                setValue: v => this.Config.ShowUniqueId = v,
                name: () => "Show UniqueID",
                tooltip: () => "Show each entry's UniqueID under its name."
            );

            this.Gmcm.AddBoolOption(
                this.ModManifest,
                getValue: () => this.Config.IncludeContentPacks,
                setValue: v => this.Config.IncludeContentPacks = v,
                name: () => "Include content packs",
                tooltip: () => "Show GMCM-registered content packs (e.g. Content Patcher configs)."
            );

            this.Gmcm.AddParagraph(
                this.ModManifest,
                text: () => "Tip: Type to filter. Enter opens the selected config. Esc closes."
            );
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            // hotkey pressed?
            if (!this.Config.OpenSearchMenuKey.JustPressed())
                return;

            // don’t open over other menus (except TitleMenu, which is fine)
            if (Game1.activeClickableMenu is not null && Game1.activeClickableMenu is not TitleMenu)
                return;

            // allow in-world, or on title screen
            if (!Context.IsWorldReady && Game1.activeClickableMenu is not TitleMenu)
                return;

            if (this.Gmcm is null)
            {
                Game1.showRedMessage("Generic Mod Config Menu is not installed.");
                return;
            }

            // refresh list right before opening
            this.Registered = GMCMRegistryScanner.GetRegisteredModsOrFallback(
                helper: this.Helper,
                gmcmApiObj: this.Gmcm,
                monitor: this.Monitor,
                selfManifest: this.ModManifest,
                includeContentPacks: this.Config.IncludeContentPacks
            );

            if (this.Registered.Count == 0)
            {
                Game1.showRedMessage("No GMCM-registered configs found.");
                return;
            }

            Game1.activeClickableMenu = new SearchMenu(
                helper: this.Helper,
                monitor: this.Monitor,
                title: "GMCM Search",
                showUniqueId: this.Config.ShowUniqueId,
                mods: this.Registered,
                openMod: this.TryOpenMod
            );
        }

        private bool TryOpenMod(IManifest manifest)
        {
            if (this.Gmcm is null)
                return false;

            try
            {
                // child menu = user can back out to our search screen
                this.Gmcm.OpenModMenuAsChildMenu(manifest);
                return true;
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Failed to open GMCM menu for '{manifest.UniqueID}': {ex}", LogLevel.Warn);
                return false;
            }
        }
    }

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
        private string lastSearch = "";

        private Rectangle searchRect;
        private Rectangle listRect;
        private Rectangle scrollTrackRect;

        private int selectedIndex = 0;
        private int scrollOffset = 0;

        private bool draggingThumb = false;
        private int dragGrabOffsetY = 0;

        // UI tuning knobs (keep your existing element sizes)
        private const int OuterPad = 32;
        private const int TitleGap = 14;
        private const int SearchHeight = 48;
        private const int InstructionHeight = 28;
        private const int ScrollbarWidth = 24;
        private const int RowPadX = 14;

        // Row layout (fix highlight not covering text)
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

        // Scrollbar styling (clean, non-9-slice)
        private const int ScrollOuterBorder = 2;
        private const int ScrollInnerPad = 4;
        private const int ThumbMinHeight = 44;

        public SearchMenu(IModHelper helper, IMonitor monitor, string title, bool showUniqueId, List<IManifest> mods, Func<IManifest, bool> openMod)
            : base(0, 0, 0, 0, showUpperRightCloseButton: true)
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

            // build search box
            Texture2D tex = Game1.content.Load<Texture2D>("LooseSprites/textBox");
            this.searchBox = new TextBox(tex, null, Game1.smallFont, Game1.textColor)
            {
                X = this.searchRect.X + 8,
                Y = this.searchRect.Y + 8,
                Width = this.searchRect.Width - 16,
                Text = ""
            };

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

            // keep textbox aligned
            this.searchBox.X = this.searchRect.X + 8;
            this.searchBox.Y = this.searchRect.Y + 8;
            this.searchBox.Width = this.searchRect.Width - 16;

            this.ClampScroll();
        }

        private void InitializeLayout()
        {
            int vw = Game1.uiViewport.Width;
            int vh = Game1.uiViewport.Height;

            // BIGGER overall window (elements keep same sizes; list area grows)
            int maxW = Math.Max(640, vw - 80);
            int maxH = Math.Max(520, vh - 80);

            this.width = Math.Min(1100, maxW);
            this.height = Math.Min(860, maxH);

            this.xPositionOnScreen = (vw - this.width) / 2;
            this.yPositionOnScreen = (vh - this.height) / 2;

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

            // list box starts after instruction line
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
            if (this.filtered.Count == 0)
                return;

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

                    if (ok)
                        this.filtered.Add(m);
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

            // no TextBox.OnTextChanged in 1.6: poll text changes
            string now = this.searchBox.Text ?? "";
            if (!string.Equals(now, this.lastSearch, StringComparison.Ordinal))
            {
                this.lastSearch = now;
                this.ApplyFilter();
            }
        }

        public override void receiveKeyPress(Keys key)
        {
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
            base.receiveLeftClick(x, y, playSound);

            // focus search
            if (this.searchRect.Contains(x, y))
            {
                Game1.keyboardDispatcher.Subscriber = this.searchBox;
                return;
            }

            // click in list
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

            // scrollbar interactions
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
                    // jump scroll to clicked position
                    this.SetScrollFromThumbCenter(y);
                }
            }
        }

        public override void leftClickHeld(int x, int y)
        {
            base.leftClickHeld(x, y);

            if (!this.draggingThumb)
                return;

            this.SetScrollFromThumbTop(y - this.dragGrabOffsetY);
        }

        public override void releaseLeftClick(int x, int y)
        {
            base.releaseLeftClick(x, y);
            this.draggingThumb = false;
        }

        private void OpenSelected()
        {
            if (this.filtered.Count == 0)
                return;

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

        // ===== Scrollbar =====

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

            int thumbInsetX = 2;
            return new Rectangle(
                rail.X + thumbInsetX,
                thumbY,
                Math.Max(1, rail.Width - thumbInsetX * 2),
                thumbH
            );
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
            if (thumb.Height <= 0 || thumb.Width <= 0)
                return;

            FillRect(b, thumb, new Color(255, 255, 255, 170));
            DrawBorder(b, thumb, 1, new Color(0, 0, 0, 110));

            Rectangle inner = InsetRect(thumb, 2, 2);
            if (inner.Width > 0 && inner.Height > 0)
                FillRect(b, inner, new Color(255, 255, 255, 55));
        }

        private static Rectangle InsetRect(Rectangle r, int dx, int dy)
        {
            int x = r.X + dx;
            int y = r.Y + dy;
            int w = r.Width - dx * 2;
            int h = r.Height - dy * 2;
            if (w < 0) w = 0;
            if (h < 0) h = 0;
            return new Rectangle(x, y, w, h);
        }

        private static void FillRect(SpriteBatch b, Rectangle rect, Color color)
        {
            if (rect.Width <= 0 || rect.Height <= 0)
                return;

            b.Draw(Game1.fadeToBlackRect, rect, color);
        }

        private static void DrawBorder(SpriteBatch b, Rectangle rect, int thickness, Color color)
        {
            if (thickness <= 0 || rect.Width <= 0 || rect.Height <= 0)
                return;

            FillRect(b, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            FillRect(b, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
            FillRect(b, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            FillRect(b, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
        }

        // ===== End scrollbar =====

        public override void draw(SpriteBatch b)
        {
            this.drawBackground(b);

            IClickableMenu.drawTextureBox(
                b,
                x: this.xPositionOnScreen,
                y: this.yPositionOnScreen,
                width: this.width,
                height: this.height,
                color: Color.White
            );

            int titleX = this.xPositionOnScreen + this.width / 2;
            int titleY = this.yPositionOnScreen + OuterPad - 4;
            SpriteText.drawStringHorizontallyCenteredAt(b, this.title, titleX, titleY);

            IClickableMenu.drawTextureBox(b, this.searchRect.X, this.searchRect.Y, this.searchRect.Width, this.searchRect.Height, Color.White);
            this.searchBox.Draw(b);

            if (string.IsNullOrEmpty(this.searchBox.Text) && Game1.keyboardDispatcher.Subscriber != this.searchBox)
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
            base.draw(b);
        }
    }
}
