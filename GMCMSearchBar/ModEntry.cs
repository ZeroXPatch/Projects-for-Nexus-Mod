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
                text: () => "GMCM Quick Search",
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

            // don't open over other menus (except TitleMenu, which is fine)
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
                title: "GMCM Quick Search",
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

        // UI tuning knobs
        private const int OuterPad = 32;
        private const int TitleGap = 14;
        private const int SearchHeight = 48;
        private const int InstructionHeight = 28;
        private const int ScrollbarWidth = 24;

        private const int RowPadX = 14;
        private const int RowPadY = 8;

        // dynamic row sizing (fixes “highlight smaller than text”)
        private int rowHeight;
        private int nameLineH;
        private int idLineH;

        public SearchMenu(
            IModHelper helper,
            IMonitor monitor,
            string title,
            bool showUniqueId,
            List<IManifest> mods,
            Func<IManifest, bool> openMod
        )
            : base(0, 0, 0, 0, showUpperRightCloseButton: true)
        {
            this.helper = helper;
            this.monitor = monitor;
            this.title = title;
            this.showUniqueId = showUniqueId;
            this.openMod = openMod;

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

            this.width = Math.Min(920, vw - 160);
            this.height = Math.Min(720, vh - 140);

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

            // ---- dynamic row sizing ----
            this.nameLineH = Game1.dialogueFont.LineSpacing;
            this.idLineH = this.showUniqueId ? Game1.smallFont.LineSpacing : 0;

            // rowHeight big enough for text + padding (and never cramped)
            this.rowHeight = this.nameLineH + this.idLineH + (RowPadY * 2) + (this.showUniqueId ? 2 : 0);
            this.rowHeight = Math.Max(this.rowHeight, 56);
        }

        private int ItemsPerPage => Math.Max(1, this.listRect.Height / this.rowHeight);
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
                int row = (y - this.listRect.Y) / this.rowHeight;
                int idx = this.scrollOffset + row;

                if (idx >= 0 && idx < this.filtered.Count)
                {
                    this.selectedIndex = idx;
                    this.OpenSelected(); // single click opens
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
                // remove it so the list stays “honest”
                this.all.RemoveAll(m => m.UniqueID == target.UniqueID);
                this.filtered.RemoveAll(m => m.UniqueID == target.UniqueID);
                this.ClampScroll();
            }
        }

        private Rectangle GetScrollThumbRect()
        {
            int trackH = this.scrollTrackRect.Height;

            if (this.filtered.Count <= 0)
                return new Rectangle(this.scrollTrackRect.X, this.scrollTrackRect.Y, this.scrollTrackRect.Width, Math.Min(80, trackH));

            float ratio = Math.Min(1f, (float)this.ItemsPerPage / Math.Max(1, this.filtered.Count));
            int thumbH = Math.Max(42, (int)(trackH * ratio));

            int maxThumbY = this.scrollTrackRect.Bottom - thumbH;
            int minThumbY = this.scrollTrackRect.Y;

            float t = this.MaxScroll == 0 ? 0f : (float)this.scrollOffset / this.MaxScroll;
            int thumbY = (int)Math.Round(minThumbY + (maxThumbY - minThumbY) * t);

            return new Rectangle(this.scrollTrackRect.X, thumbY, this.scrollTrackRect.Width, thumbH);
        }

        private void SetScrollFromThumbTop(int thumbTopY)
        {
            Rectangle thumb = this.GetScrollThumbRect();

            int minY = this.scrollTrackRect.Y;
            int maxY = this.scrollTrackRect.Bottom - thumb.Height;

            int clamped = Math.Clamp(thumbTopY, minY, maxY);

            float t = (maxY == minY) ? 0f : (float)(clamped - minY) / (maxY - minY);
            this.scrollOffset = (int)Math.Round(t * this.MaxScroll);
        }

        private void SetScrollFromThumbCenter(int mouseY)
        {
            Rectangle thumb = this.GetScrollThumbRect();
            this.SetScrollFromThumbTop(mouseY - thumb.Height / 2);
        }

        public override void draw(SpriteBatch b)
        {
            this.drawBackground(b);

            // outer frame
            IClickableMenu.drawTextureBox(
                b,
                x: this.xPositionOnScreen,
                y: this.yPositionOnScreen,
                width: this.width,
                height: this.height,
                color: Color.White
            );

            // title
            int titleX = this.xPositionOnScreen + this.width / 2;
            int titleY = this.yPositionOnScreen + OuterPad - 4;
            SpriteText.drawStringHorizontallyCenteredAt(b, this.title, titleX, titleY);

            // search frame
            IClickableMenu.drawTextureBox(b, this.searchRect.X, this.searchRect.Y, this.searchRect.Width, this.searchRect.Height, Color.White);

            // search box
            this.searchBox.Draw(b);

            // placeholder
            if (string.IsNullOrEmpty(this.searchBox.Text) && Game1.keyboardDispatcher.Subscriber != this.searchBox)
            {
                float phY = this.searchRect.Y + (this.searchRect.Height - Game1.smallFont.LineSpacing) / 2f;
                Utility.drawTextWithShadow(
                    b,
                    "Type to filter…",
                    Game1.smallFont,
                    new Vector2(this.searchRect.X + 18, phY),
                    new Color(120, 120, 120)
                );
            }

            // instruction line
            Vector2 instrPos = new Vector2(this.searchRect.X, this.searchRect.Bottom + 10);
            Utility.drawTextWithShadow(b, "Click a result or press Enter to open its config in GMCM.", Game1.smallFont, instrPos, Game1.textColor);

            // list frame
            IClickableMenu.drawTextureBox(b, this.listRect.X, this.listRect.Y, this.listRect.Width, this.listRect.Height, Color.White);

            // rows
            int visible = this.ItemsPerPage;
            int start = this.scrollOffset;
            int end = Math.Min(this.filtered.Count, start + visible);

            for (int i = start; i < end; i++)
            {
                int rowIndex = i - start;
                int y = this.listRect.Y + rowIndex * this.rowHeight;

                // full-height row box (matches computed rowHeight)
                Rectangle rowRect = new Rectangle(
                    this.listRect.X + 6,
                    y + 2,
                    this.listRect.Width - 12,
                    this.rowHeight - 4
                );

                bool selected = (i == this.selectedIndex);
                if (selected)
                {
                    // BIG highlight (no more “short highlight”)
                    IClickableMenu.drawTextureBox(b, rowRect.X, rowRect.Y, rowRect.Width, rowRect.Height, Color.White);
                }

                var m = this.filtered[i];

                float nameY = rowRect.Y + RowPadY;
                Vector2 namePos = new Vector2(rowRect.X + RowPadX, nameY);
                Utility.drawTextWithShadow(b, m.Name ?? m.UniqueID, Game1.dialogueFont, namePos, Game1.textColor);

                if (this.showUniqueId)
                {
                    float idY = nameY + this.nameLineH - 2;
                    Vector2 idPos = new Vector2(rowRect.X + RowPadX, idY);
                    Utility.drawTextWithShadow(b, m.UniqueID ?? "", Game1.smallFont, idPos, new Color(90, 90, 90));
                }
            }

            // scrollbar rail
            IClickableMenu.drawTextureBox(b, this.scrollTrackRect.X, this.scrollTrackRect.Y, this.scrollTrackRect.Width, this.scrollTrackRect.Height, Color.White);

            // thumb
            Rectangle thumbRect = this.GetScrollThumbRect();
            IClickableMenu.drawTextureBox(b, thumbRect.X + 2, thumbRect.Y + 2, thumbRect.Width - 4, thumbRect.Height - 4, Color.White);

            // close button, then cursor last
            base.draw(b);
            this.drawMouse(b);
        }
    }
}
