// ModEntry.cs
using GenericModConfigMenu;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
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
        private ModConfig Config = null!;
        private IGenericModConfigMenuApi? Gmcm;

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            this.Gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (this.Gmcm is null)
                return;

            this.Gmcm.Register(
                this.ModManifest,
                reset: () => this.Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.Config)
            );

            this.Gmcm.AddKeybindList(
                this.ModManifest,
                getValue: () => this.Config.OpenSearchMenuKey,
                setValue: value => this.Config.OpenSearchMenuKey = value,
                name: () => "Open GMCM Search",
                tooltip: () => "Open a searchable list of mods which registered a GMCM config menu."
            );

            this.Gmcm.AddBoolOption(
                this.ModManifest,
                getValue: () => this.Config.ShowUniqueId,
                setValue: value => this.Config.ShowUniqueId = value,
                name: () => "Show UniqueID",
                tooltip: () => "Show each mod's UniqueID under its name."
            );
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (this.Gmcm is null)
                return;

            if (Game1.activeClickableMenu is SearchMenu)
                return;

            if (!this.Config.OpenSearchMenuKey.JustPressed())
                return;

            // Context.IsOnTitleScreen doesn't exist in your SMAPI target; use the actual menu type instead.
            bool onTitle = Game1.activeClickableMenu is TitleMenu;
            if (!Context.IsWorldReady && !onTitle)
                return;

            List<IManifest> mods = GMCMRegistryScanner.GetRegisteredModsOrFallback(
                this.Helper,
                this.Gmcm,
                this.Monitor,
                this.ModManifest
            );

            bool Opener(IManifest manifest)
            {
                try
                {
                    this.Gmcm!.OpenModMenu(manifest);
                    return true;
                }
                catch (Exception ex)
                {
                    this.Monitor.Log($"Failed to open GMCM menu for '{manifest.UniqueID}'.\n{ex}", LogLevel.Warn);
                    return false;
                }
            }

            Game1.activeClickableMenu = new SearchMenu(
                monitor: this.Monitor,
                openMod: Opener,
                mods: mods,
                showUniqueId: this.Config.ShowUniqueId
            );
        }

        private sealed class SearchMenu : IClickableMenu
        {
            private readonly IMonitor monitor;
            private readonly Func<IManifest, bool> openMod;

            private readonly List<IManifest> all;
            private List<IManifest> filtered;

            private readonly SpriteFont font;
            private readonly Texture2D textBoxTexture;
            private readonly TextBox searchBox;

            private string lastSearchText = "";

            private Rectangle listRect;
            private Rectangle scrollTrackRect;

            private int scrollOffset;
            private int selectedIndex = -1;
            private int hoverIndex = -1;

            private bool draggingThumb;
            private int dragGrabOffsetY;

            private readonly bool showUniqueId;

            private const int ScrollbarWidth = 24;
            private const int RowPaddingY = 6;

            private int RowHeight => this.showUniqueId ? (int)(this.font.LineSpacing * 2f) + 12 : this.font.LineSpacing + 12;

            public SearchMenu(IMonitor monitor, Func<IManifest, bool> openMod, List<IManifest> mods, bool showUniqueId)
                : base(0, 0, 0, 0, showUpperRightCloseButton: true)
            {
                this.monitor = monitor;
                this.openMod = openMod;
                this.showUniqueId = showUniqueId;

                this.font = Game1.smallFont;
                this.textBoxTexture = Game1.content.Load<Texture2D>("LooseSprites\\textBox");

                // Clean + stable ordering
                this.all = mods
                    .Where(m => m is not null)
                    .GroupBy(m => m.UniqueID, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                this.filtered = new List<IManifest>(this.all);

                // Size + center
                int w = Math.Min(900, Game1.uiViewport.Width - Game1.tileSize * 2);
                int h = Math.Min(700, Game1.uiViewport.Height - Game1.tileSize * 2);
                this.width = w;
                this.height = h;
                this.xPositionOnScreen = (Game1.uiViewport.Width - this.width) / 2;
                this.yPositionOnScreen = (Game1.uiViewport.Height - this.height) / 2;

                // Search box
                int pad = Game1.tileSize / 2;
                int searchY = this.yPositionOnScreen + pad + 32;
                int searchX = this.xPositionOnScreen + pad;
                int searchW = this.width - pad * 2;

                this.searchBox = new TextBox(this.textBoxTexture, null, this.font, Game1.textColor)
                {
                    X = searchX,
                    Y = searchY,
                    Width = searchW,
                    Text = ""
                };

                // Layout rects
                int instructionY = searchY + this.textBoxTexture.Height * Game1.pixelZoom + 10;
                int instructionH = this.font.LineSpacing + 10;

                int listY = instructionY + instructionH + 8;
                int listH = (this.yPositionOnScreen + this.height - pad) - listY;

                int listW = searchW - ScrollbarWidth - 8;
                this.listRect = new Rectangle(searchX, listY, listW, listH);
                this.scrollTrackRect = new Rectangle(this.listRect.Right + 8, this.listRect.Y, ScrollbarWidth, this.listRect.Height);

                this.initializeUpperRightCloseButton();
                this.ClampScroll();

                // focus text input
                Game1.keyboardDispatcher.Subscriber = this.searchBox;
                this.searchBox.Selected = true;
            }

            protected override void cleanupBeforeExit()
            {
                if (Game1.keyboardDispatcher?.Subscriber == this.searchBox)
                    Game1.keyboardDispatcher.Subscriber = null;

                base.cleanupBeforeExit();
            }

            public override void update(GameTime time)
            {
                base.update(time);

                // No TextBox OnTextChanged in your target; just poll for changes.
                string now = this.searchBox.Text ?? "";
                if (!string.Equals(now, this.lastSearchText, StringComparison.Ordinal))
                {
                    this.lastSearchText = now;
                    this.ApplyFilter(now);
                }
            }

            private void ApplyFilter(string text)
            {
                string q = (text ?? "").Trim();
                if (q.Length == 0)
                {
                    this.filtered = new List<IManifest>(this.all);
                }
                else
                {
                    this.filtered = this.all
                        .Where(m =>
                            m.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                            m.UniqueID.Contains(q, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                this.scrollOffset = 0;
                this.selectedIndex = this.filtered.Count > 0 ? 0 : -1;
                this.ClampScroll();
            }

            public override void receiveKeyPress(Keys key)
            {
                if (key == Keys.Escape)
                {
                    this.exitThisMenu();
                    return;
                }

                if (key == Keys.Up)
                {
                    if (this.filtered.Count > 0)
                    {
                        this.selectedIndex = Math.Max(this.selectedIndex - 1, 0);
                        this.EnsureSelectionVisible();
                    }
                    return;
                }

                if (key == Keys.Down)
                {
                    if (this.filtered.Count > 0)
                    {
                        this.selectedIndex = Math.Min(this.selectedIndex + 1, this.filtered.Count - 1);
                        this.EnsureSelectionVisible();
                    }
                    return;
                }

                if (key == Keys.Enter)
                {
                    if (this.filtered.Count > 0 && this.selectedIndex >= 0 && this.selectedIndex < this.filtered.Count)
                        this.OpenAndClose(this.filtered[this.selectedIndex]);
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
                this.scrollOffset += delta;
                this.ClampScroll();
            }

            public override void receiveLeftClick(int x, int y, bool playSound = true)
            {
                base.receiveLeftClick(x, y, playSound);

                if (this.upperRightCloseButton?.containsPoint(x, y) == true)
                {
                    this.exitThisMenu();
                    return;
                }

                if (this.TryGetThumbRect(out Rectangle thumb) && thumb.Contains(x, y))
                {
                    this.draggingThumb = true;
                    this.dragGrabOffsetY = y - thumb.Y;
                    return;
                }

                if (this.scrollTrackRect.Contains(x, y))
                {
                    this.JumpScrollTo(y);
                    return;
                }

                if (this.listRect.Contains(x, y))
                {
                    int idx = this.GetIndexAtPoint(x, y);
                    if (idx >= 0 && idx < this.filtered.Count)
                    {
                        this.selectedIndex = idx;
                        this.OpenAndClose(this.filtered[idx]);
                    }
                }
            }

            public override void leftClickHeld(int x, int y)
            {
                base.leftClickHeld(x, y);

                if (!this.draggingThumb)
                    return;

                if (this.filtered.Count <= this.VisibleRows)
                    return;

                int maxOffset = this.MaxScrollOffset;
                int trackLen = this.scrollTrackRect.Height;
                int thumbH = this.GetThumbHeight(trackLen);

                int clampedY = Math.Clamp(y - this.dragGrabOffsetY, this.scrollTrackRect.Y, this.scrollTrackRect.Bottom - thumbH);
                float t = (clampedY - this.scrollTrackRect.Y) / (float)Math.Max(1, (trackLen - thumbH));
                this.scrollOffset = (int)Math.Round(t * maxOffset);

                this.ClampScroll();
            }

            public override void releaseLeftClick(int x, int y)
            {
                base.releaseLeftClick(x, y);
                this.draggingThumb = false;
            }

            public override void performHoverAction(int x, int y)
            {
                base.performHoverAction(x, y);

                this.hoverIndex = -1;
                if (this.listRect.Contains(x, y))
                {
                    int idx = this.GetIndexAtPoint(x, y);
                    if (idx >= 0 && idx < this.filtered.Count)
                        this.hoverIndex = idx;
                }
            }

            public override void draw(SpriteBatch b)
            {
                this.drawBackground(b);

                IClickableMenu.drawTextureBox(b, this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, Color.White);

                // Title
                SpriteText.drawStringHorizontallyCenteredAt(
                    b,
                    "GMCM Search",
                    this.xPositionOnScreen + this.width / 2,
                    this.yPositionOnScreen + 16
                );

                // Search box
                this.searchBox.Draw(b);

                // Placeholder
                if (string.IsNullOrWhiteSpace(this.searchBox.Text) && !this.searchBox.Selected)
                {
                    Vector2 pos = new Vector2(this.searchBox.X + 12, this.searchBox.Y + 10);
                    b.DrawString(this.font, "Type to filter mods...", pos, Color.Gray);
                }

                // Instruction
                int instructionY = this.searchBox.Y + this.textBoxTexture.Height * Game1.pixelZoom + 10;
                b.DrawString(this.font, "Click a result to open its config in GMCM.", new Vector2(this.searchBox.X, instructionY), Game1.textColor);

                // List box
                IClickableMenu.drawTextureBox(b, this.listRect.X - 8, this.listRect.Y - 8, this.listRect.Width + 16, this.listRect.Height + 16, Color.White);

                int rows = this.VisibleRows;
                int start = this.scrollOffset;
                int end = Math.Min(this.filtered.Count, start + rows);

                if (this.filtered.Count == 0)
                {
                    b.DrawString(this.font, "No matching mods.", new Vector2(this.listRect.X + 12, this.listRect.Y + 12), Color.Gray);
                }
                else
                {
                    for (int i = start; i < end; i++)
                    {
                        int row = i - start;
                        int y = this.listRect.Y + row * this.RowHeight;

                        Rectangle rowRect = new Rectangle(this.listRect.X, y, this.listRect.Width, this.RowHeight);
                        bool selected = i == this.selectedIndex;
                        bool hovered = i == this.hoverIndex;

                        Color boxColor = selected ? Color.LightGoldenrodYellow : (hovered ? Color.Beige : Color.White);
                        IClickableMenu.drawTextureBox(b, rowRect.X, rowRect.Y, rowRect.Width, rowRect.Height, boxColor);

                        IManifest m = this.filtered[i];
                        Vector2 namePos = new Vector2(rowRect.X + 12, rowRect.Y + RowPaddingY);
                        b.DrawString(this.font, m.Name, namePos, Game1.textColor);

                        if (this.showUniqueId)
                        {
                            Vector2 idPos = new Vector2(rowRect.X + 12, rowRect.Y + RowPaddingY + this.font.LineSpacing);
                            b.DrawString(this.font, m.UniqueID, idPos, Color.Gray);
                        }
                    }
                }

                // Scrollbar
                if (this.filtered.Count > this.VisibleRows)
                {
                    IClickableMenu.drawTextureBox(b, this.scrollTrackRect.X, this.scrollTrackRect.Y, this.scrollTrackRect.Width, this.scrollTrackRect.Height, Color.White);

                    if (this.TryGetThumbRect(out Rectangle thumb))
                        IClickableMenu.drawTextureBox(b, thumb.X, thumb.Y, thumb.Width, thumb.Height, Color.White);
                }

                this.upperRightCloseButton?.draw(b);
                this.drawMouse(b);
            }

            private int VisibleRows => Math.Max(1, this.listRect.Height / this.RowHeight);
            private int MaxScrollOffset => Math.Max(0, this.filtered.Count - this.VisibleRows);

            private void ClampScroll()
            {
                this.scrollOffset = Math.Clamp(this.scrollOffset, 0, this.MaxScrollOffset);
            }

            private void EnsureSelectionVisible()
            {
                if (this.selectedIndex < 0)
                    return;

                if (this.selectedIndex < this.scrollOffset)
                    this.scrollOffset = this.selectedIndex;
                else if (this.selectedIndex >= this.scrollOffset + this.VisibleRows)
                    this.scrollOffset = this.selectedIndex - this.VisibleRows + 1;

                this.ClampScroll();
            }

            private int GetIndexAtPoint(int x, int y)
            {
                int row = (y - this.listRect.Y) / this.RowHeight;
                if (row < 0 || row >= this.VisibleRows)
                    return -1;

                int idx = this.scrollOffset + row;
                return idx;
            }

            private void OpenAndClose(IManifest manifest)
            {
                this.exitThisMenu();
                bool ok = this.openMod(manifest);
                if (!ok)
                    Game1.addHUDMessage(new HUDMessage($"Couldn't open GMCM menu for {manifest.Name}.", HUDMessage.error_type));
            }

            private int GetThumbHeight(int trackHeight)
            {
                int total = Math.Max(1, this.filtered.Count);
                int visible = this.VisibleRows;

                float ratio = visible / (float)total;
                int h = (int)Math.Round(trackHeight * ratio);
                return Math.Clamp(h, 24, trackHeight);
            }

            private bool TryGetThumbRect(out Rectangle thumb)
            {
                thumb = Rectangle.Empty;

                int maxOffset = this.MaxScrollOffset;
                if (maxOffset <= 0)
                    return false;

                int trackLen = this.scrollTrackRect.Height;
                int thumbH = this.GetThumbHeight(trackLen);

                float t = this.scrollOffset / (float)maxOffset;
                int y = this.scrollTrackRect.Y + (int)Math.Round((trackLen - thumbH) * t);

                thumb = new Rectangle(this.scrollTrackRect.X, y, this.scrollTrackRect.Width, thumbH);
                return true;
            }

            private void JumpScrollTo(int mouseY)
            {
                if (this.filtered.Count <= this.VisibleRows)
                    return;

                int maxOffset = this.MaxScrollOffset;
                int trackLen = this.scrollTrackRect.Height;
                int thumbH = this.GetThumbHeight(trackLen);

                int targetTop = mouseY - (thumbH / 2);
                int clamped = Math.Clamp(targetTop, this.scrollTrackRect.Y, this.scrollTrackRect.Bottom - thumbH);

                float t = (clamped - this.scrollTrackRect.Y) / (float)Math.Max(1, (trackLen - thumbH));
                this.scrollOffset = (int)Math.Round(t * maxOffset);

                this.ClampScroll();
            }
        }
    }
}
