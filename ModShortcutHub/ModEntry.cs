using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace ModShortcutHub
{
    public class ShortcutEntry
    {
        public string KeyCombo { get; set; } = "";
        public string Description { get; set; } = "";
        public bool IsCustom { get; set; } = false;
    }

    public class ModConfig
    {
        public List<ShortcutEntry> Shortcuts { get; set; } = new();
        public bool HideUncustomized { get; set; } = false;
        public string SortMode { get; set; } = "Key"; // "Key", "Description", "Custom First"
        public string OpenMenuKey { get; set; } = "K"; // Key to open the hub menu
    }

    public class ModEntry : Mod
    {
        internal static ModConfig Config = null!;
        internal static IModHelper ModHelper = null!;

        public override void Entry(IModHelper helper)
        {
            Config = helper.ReadConfig<ModConfig>();
            ModHelper = helper;

            // Initialize default shortcuts if config is empty
            if (Config.Shortcuts.Count == 0)
            {
                InitializeDefaultShortcuts();
                helper.WriteConfig(Config);
            }

            helper.Events.Input.ButtonPressed += OnButtonPressed;
        }

        private void InitializeDefaultShortcuts()
        {
            // Add all basic keys A-Z, F1-F12
            // REMOVED: 0-9 preset shortcuts as requested
            for (SButton b = SButton.A; b <= SButton.Z; b++)
                Config.Shortcuts.Add(new ShortcutEntry { KeyCombo = b.ToString(), Description = "", IsCustom = false });

            for (SButton b = SButton.F1; b <= SButton.F12; b++)
                Config.Shortcuts.Add(new ShortcutEntry { KeyCombo = b.ToString(), Description = "", IsCustom = false });
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady) return;

            // Parse the configured key
            if (Enum.TryParse<SButton>(Config.OpenMenuKey, true, out SButton configuredKey))
            {
                if (e.Button == configuredKey && Game1.activeClickableMenu == null)
                {
                    Game1.activeClickableMenu = new ShortcutHubMenu(Helper);
                }
            }
        }
    }

    public class ShortcutHubMenu : IClickableMenu
    {
        private readonly IModHelper Helper;
        public List<ShortcutEntry> AllShortcuts = new();
        private List<ShortcutEntry> VisibleShortcuts = new();
        private TextBox SearchBar;
        private string LastSearch = "";

        private ClickableTextureComponent ScrollBar;
        private Rectangle ScrollBarRunner;
        private int CurrentScrollIndex = 0;
        private bool IsScrolling = false;

        private ClickableComponent AddButton;
        private ClickableComponent SortButton;
        private ClickableComponent HideButton;

        private const int ROWS_VISIBLE = 9; // Reduced from 10 to prevent overlap
        private const int ROW_HEIGHT = 60;

        public ShortcutHubMenu(IModHelper helper)
            : base(Game1.uiViewport.Width / 2 - 500, Game1.uiViewport.Height / 2 - 425, 1000, 850, true)
        {
            this.Helper = helper;

            SearchBar = new TextBox(Game1.content.Load<Texture2D>("LooseSprites\\textBox"), null, Game1.smallFont, Color.Black)
            {
                X = xPositionOnScreen + 140,
                Y = yPositionOnScreen + 120,
                Width = 450
            };

            // Increased button widths and adjusted spacing to prevent text overflow
            AddButton = new ClickableComponent(
                new Rectangle(xPositionOnScreen + 610, yPositionOnScreen + 110, 110, 50),
                Helper.Translation.Get("menu.button.addNew")
            );

            SortButton = new ClickableComponent(
                new Rectangle(xPositionOnScreen + 735, yPositionOnScreen + 110, 120, 50),
                GetSortButtonText()
            );

            HideButton = new ClickableComponent(
                new Rectangle(xPositionOnScreen + 870, yPositionOnScreen + 110, 110, 50),
                ModEntry.Config.HideUncustomized
                    ? Helper.Translation.Get("menu.button.showAll")
                    : Helper.Translation.Get("menu.button.hideEmpty")
            );

            AllShortcuts = ModEntry.Config.Shortcuts;
            ScrollBar = new ClickableTextureComponent(
                new Rectangle(xPositionOnScreen + width - 60, yPositionOnScreen + 210, 24, 40),
                Game1.mouseCursors,
                new Rectangle(435, 463, 6, 10),
                4f
            );
            ScrollBarRunner = new Rectangle(ScrollBar.bounds.X, yPositionOnScreen + 210, ScrollBar.bounds.Width, height - 290);

            UpdateFilters();
        }

        private string GetSortButtonText()
        {
            return ModEntry.Config.SortMode switch
            {
                "Key" => Helper.Translation.Get("menu.button.sortKey"),
                "Description" => Helper.Translation.Get("menu.button.sortDescription"),
                "Custom First" => Helper.Translation.Get("menu.button.sortCustomFirst"),
                _ => Helper.Translation.Get("menu.button.sortKey")
            };
        }

        private void UpdateFilters()
        {
            string query = SearchBar.Text.ToLower();

            // Filter shortcuts
            VisibleShortcuts = AllShortcuts.Where(s =>
                (s.KeyCombo.ToLower().Contains(query) || s.Description.ToLower().Contains(query)) &&
                (!ModEntry.Config.HideUncustomized || !string.IsNullOrWhiteSpace(s.Description))
            ).ToList();

            // Sort shortcuts
            switch (ModEntry.Config.SortMode)
            {
                case "Key":
                    VisibleShortcuts = VisibleShortcuts.OrderBy(s => s.KeyCombo).ToList();
                    break;
                case "Description":
                    VisibleShortcuts = VisibleShortcuts.OrderBy(s => s.Description).ToList();
                    break;
                case "Custom First":
                    VisibleShortcuts = VisibleShortcuts.OrderByDescending(s => s.IsCustom).ThenBy(s => s.KeyCombo).ToList();
                    break;
            }

            CurrentScrollIndex = Math.Min(CurrentScrollIndex, Math.Max(0, VisibleShortcuts.Count - ROWS_VISIBLE));
            UpdateScrollBar();
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            // Search bar
            if (new Rectangle(SearchBar.X, SearchBar.Y, SearchBar.Width, SearchBar.Height).Contains(x, y))
            {
                SearchBar.Selected = true;
                return;
            }
            SearchBar.Selected = false;

            // Add button
            if (AddButton.containsPoint(x, y))
            {
                Game1.playSound("bigSelect");
                Game1.activeClickableMenu = new AddShortcutMenu(this, Helper);
                return;
            }

            // Sort button
            if (SortButton.containsPoint(x, y))
            {
                Game1.playSound("drumkit6");
                ModEntry.Config.SortMode = ModEntry.Config.SortMode switch
                {
                    "Key" => "Description",
                    "Description" => "Custom First",
                    _ => "Key"
                };
                SortButton.name = GetSortButtonText();
                Helper.WriteConfig(ModEntry.Config);
                UpdateFilters();
                return;
            }

            // Hide button
            if (HideButton.containsPoint(x, y))
            {
                Game1.playSound("drumkit6");
                ModEntry.Config.HideUncustomized = !ModEntry.Config.HideUncustomized;
                HideButton.name = ModEntry.Config.HideUncustomized
                    ? Helper.Translation.Get("menu.button.showAll")
                    : Helper.Translation.Get("menu.button.hideEmpty");
                Helper.WriteConfig(ModEntry.Config);
                UpdateFilters();
                return;
            }

            // Shortcut rows
            for (int i = 0; i < ROWS_VISIBLE; i++)
            {
                int index = CurrentScrollIndex + i;
                if (index >= VisibleShortcuts.Count) break;

                var shortcut = VisibleShortcuts[index];
                Rectangle rowBounds = new Rectangle(xPositionOnScreen + 50, yPositionOnScreen + 210 + (i * ROW_HEIGHT), 850, 55);

                if (rowBounds.Contains(x, y))
                {
                    // Delete button area (far right) - only for custom shortcuts
                    if (shortcut.IsCustom && x > rowBounds.X + 750)
                    {
                        Game1.playSound("trashcan");
                        AllShortcuts.Remove(shortcut);
                        ModEntry.Config.Shortcuts = AllShortcuts;
                        Helper.WriteConfig(ModEntry.Config);
                        UpdateFilters();
                        return;
                    }
                    // Edit button area (right side)
                    else if (x > rowBounds.X + 650)
                    {
                        Game1.activeClickableMenu = new EditShortcutMenu(this, shortcut, Helper);
                        return;
                    }
                }
            }

            // Scroll bar
            if (ScrollBar.containsPoint(x, y))
                IsScrolling = true;

            base.receiveLeftClick(x, y, playSound);
        }

        public override void leftClickHeld(int x, int y)
        {
            if (IsScrolling)
            {
                float percentage = Math.Clamp((y - ScrollBarRunner.Y) / (float)ScrollBarRunner.Height, 0, 1);
                CurrentScrollIndex = (int)(percentage * Math.Max(0, VisibleShortcuts.Count - ROWS_VISIBLE));
                UpdateScrollBar();
            }
        }

        public override void releaseLeftClick(int x, int y) => IsScrolling = false;

        public override void receiveScrollWheelAction(int direction)
        {
            CurrentScrollIndex = Math.Clamp(CurrentScrollIndex - (direction / 120), 0, Math.Max(0, VisibleShortcuts.Count - ROWS_VISIBLE));
            UpdateScrollBar();
        }

        private void UpdateScrollBar()
        {
            int max = Math.Max(0, VisibleShortcuts.Count - ROWS_VISIBLE);
            if (max > 0)
                ScrollBar.bounds.Y = (int)(ScrollBarRunner.Y + ((float)CurrentScrollIndex / max * (ScrollBarRunner.Height - ScrollBar.bounds.Height)));
        }

        public override void update(GameTime time)
        {
            if (SearchBar.Text != LastSearch)
            {
                LastSearch = SearchBar.Text;
                UpdateFilters();
            }
            base.update(time);
        }

        public override void draw(SpriteBatch b)
        {
            // Fade background
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.4f);

            // Main dialog box
            Game1.drawDialogueBox(xPositionOnScreen, yPositionOnScreen, width, height, false, true);

            // Title
            Utility.drawTextWithShadow(b, Helper.Translation.Get("menu.title"), Game1.dialogueFont,
                new Vector2(xPositionOnScreen + 60, yPositionOnScreen + 60), Color.Black);

            // Search bar label and box
            Utility.drawTextWithShadow(b, Helper.Translation.Get("menu.search"), Game1.smallFont,
                new Vector2(SearchBar.X - 85, SearchBar.Y + 12), Color.Black);
            SearchBar.Draw(b);

            // Buttons with improved rendering
            DrawButton(b, AddButton, Color.LightGreen);
            DrawButton(b, SortButton, Color.LightBlue);
            DrawButton(b, HideButton, Color.LightCoral);

            // Header row
            int headerY = yPositionOnScreen + 180;
            Utility.drawTextWithShadow(b, Helper.Translation.Get("menu.header.shortcut"), Game1.smallFont,
                new Vector2(xPositionOnScreen + 80, headerY), Color.DarkSlateGray);
            Utility.drawTextWithShadow(b, Helper.Translation.Get("menu.header.description"), Game1.smallFont,
                new Vector2(xPositionOnScreen + 250, headerY), Color.DarkSlateGray);

            // Shortcut rows
            for (int i = 0; i < ROWS_VISIBLE; i++)
            {
                int index = CurrentScrollIndex + i;
                if (index >= VisibleShortcuts.Count) break;

                var shortcut = VisibleShortcuts[index];
                int rowY = yPositionOnScreen + 210 + (i * ROW_HEIGHT);
                Rectangle rowBounds = new Rectangle(xPositionOnScreen + 50, rowY, 850, 55);
                bool hovered = rowBounds.Contains(Game1.getMouseX(), Game1.getMouseY());

                // Row background
                Color rowColor = shortcut.IsCustom ? Color.Wheat : Color.White;
                if (hovered) rowColor = Color.Gold;

                drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                    rowBounds.X, rowBounds.Y, rowBounds.Width, rowBounds.Height, rowColor, 1f, false);

                // Shortcut key combo
                Utility.drawTextWithShadow(b, shortcut.KeyCombo, Game1.smallFont,
                    new Vector2(xPositionOnScreen + 80, rowY + 18),
                    shortcut.IsCustom ? Color.DarkGreen : Color.DarkBlue);

                // Description with text clipping for long descriptions
                string desc = string.IsNullOrWhiteSpace(shortcut.Description)
                    ? Helper.Translation.Get("menu.row.noDescription")
                    : shortcut.Description;
                Color descColor = string.IsNullOrWhiteSpace(shortcut.Description) ? Color.Gray : Color.Black;

                // Truncate description if too long (max width ~380 pixels before Edit button)
                Vector2 descSize = Game1.smallFont.MeasureString(desc);
                if (descSize.X > 380)
                {
                    while (Game1.smallFont.MeasureString(desc + "...").X > 380 && desc.Length > 0)
                        desc = desc.Substring(0, desc.Length - 1);
                    desc += "...";
                }

                Utility.drawTextWithShadow(b, desc, Game1.smallFont,
                    new Vector2(xPositionOnScreen + 250, rowY + 18), descColor);

                // Edit button
                Utility.drawTextWithShadow(b, Helper.Translation.Get("menu.row.edit"), Game1.smallFont,
                    new Vector2(xPositionOnScreen + 720, rowY + 18), Color.DimGray, 0.8f);

                // Delete button (only for custom shortcuts)
                if (shortcut.IsCustom)
                {
                    Utility.drawTextWithShadow(b, Helper.Translation.Get("menu.row.delete"), Game1.smallFont,
                        new Vector2(xPositionOnScreen + 820, rowY + 18), Color.Red, 0.8f);
                }
            }

            // Scroll bar
            drawTextureBox(b, Game1.mouseCursors, new Rectangle(403, 383, 6, 6),
                ScrollBarRunner.X, ScrollBarRunner.Y, ScrollBarRunner.Width, ScrollBarRunner.Height, Color.White, 4f);
            ScrollBar.draw(b);

            base.draw(b);
            drawMouse(b);
        }

        private void DrawButton(SpriteBatch b, ClickableComponent button, Color color)
        {
            bool hovered = button.containsPoint(Game1.getMouseX(), Game1.getMouseY());
            drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                button.bounds.X, button.bounds.Y, button.bounds.Width, button.bounds.Height,
                hovered ? Color.Gold : color, 1f, false);

            // Improved text rendering with proper centering and scaling
            string buttonText = button.name;
            Vector2 textSize = Game1.smallFont.MeasureString(buttonText);
            float scale = 0.85f;

            // If text is too wide for button, reduce scale further
            if (textSize.X * scale > button.bounds.Width - 20)
                scale = (button.bounds.Width - 20) / textSize.X;

            // Center the text in the button
            float textX = button.bounds.X + (button.bounds.Width - textSize.X * scale) / 2;
            float textY = button.bounds.Y + (button.bounds.Height - textSize.Y * scale) / 2;

            Utility.drawTextWithShadow(b, buttonText, Game1.smallFont,
                new Vector2(textX, textY), Color.Black, scale);
        }

        public void Refresh()
        {
            AllShortcuts = ModEntry.Config.Shortcuts;
            // Update button labels when refreshing
            SortButton.name = GetSortButtonText();
            HideButton.name = ModEntry.Config.HideUncustomized
                ? Helper.Translation.Get("menu.button.showAll")
                : Helper.Translation.Get("menu.button.hideEmpty");
            UpdateFilters();
        }
    }

    public class EditShortcutMenu : NamingMenu
    {
        private ShortcutHubMenu ParentMenu;
        private ShortcutEntry Shortcut;
        private IModHelper Helper;

        public EditShortcutMenu(ShortcutHubMenu parent, ShortcutEntry shortcut, IModHelper helper)
            : base((string description) =>
            {
                shortcut.Description = description;
                ModEntry.Config.Shortcuts = parent.AllShortcuts;
                helper.WriteConfig(ModEntry.Config);
                Game1.activeClickableMenu = parent;
                parent.Refresh();
            }, helper.Translation.Get("edit.title", new { key = shortcut.KeyCombo }), shortcut.Description)
        {
            ParentMenu = parent;
            Shortcut = shortcut;
            Helper = helper;

            // Remove character limit from the description input
            if (this.textBox != null)
            {
                this.textBox.limitWidth = false;
            }
        }
    }

    public class AddShortcutMenu : IClickableMenu
    {
        private ShortcutHubMenu ParentMenu;
        private IModHelper Helper;
        private TextBox KeyComboBox;
        private TextBox DescriptionBox;
        private ClickableComponent SaveButton;
        private ClickableComponent CancelButton;

        private bool KeyComboSelected = false;

        public AddShortcutMenu(ShortcutHubMenu parent, IModHelper helper)
            : base(Game1.uiViewport.Width / 2 - 300, Game1.uiViewport.Height / 2 - 200, 600, 400, true)
        {
            ParentMenu = parent;
            Helper = helper;

            KeyComboBox = new TextBox(Game1.content.Load<Texture2D>("LooseSprites\\textBox"), null, Game1.smallFont, Color.Black)
            {
                X = xPositionOnScreen + 50,
                Y = yPositionOnScreen + 120,
                Width = 500
            };

            DescriptionBox = new TextBox(Game1.content.Load<Texture2D>("LooseSprites\\textBox"), null, Game1.smallFont, Color.Black)
            {
                X = xPositionOnScreen + 50,
                Y = yPositionOnScreen + 220,
                Width = 500,
                limitWidth = false
            };

            SaveButton = new ClickableComponent(
                new Rectangle(xPositionOnScreen + 100, yPositionOnScreen + 310, 150, 60),
                helper.Translation.Get("add.button.save")
            );

            CancelButton = new ClickableComponent(
                new Rectangle(xPositionOnScreen + 350, yPositionOnScreen + 310, 150, 60),
                helper.Translation.Get("add.button.cancel")
            );
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            Rectangle keyComboRect = new Rectangle(KeyComboBox.X, KeyComboBox.Y, KeyComboBox.Width, KeyComboBox.Height);
            Rectangle descriptionRect = new Rectangle(DescriptionBox.X, DescriptionBox.Y, DescriptionBox.Width, DescriptionBox.Height);

            if (keyComboRect.Contains(x, y))
            {
                KeyComboBox.Selected = true;
                DescriptionBox.Selected = false;
                KeyComboSelected = true;
                return;
            }

            if (descriptionRect.Contains(x, y))
            {
                DescriptionBox.Selected = true;
                KeyComboBox.Selected = false;
                KeyComboSelected = false;
                return;
            }

            if (SaveButton.containsPoint(x, y))
            {
                if (!string.IsNullOrWhiteSpace(KeyComboBox.Text))
                {
                    Game1.playSound("coin");
                    var newShortcut = new ShortcutEntry
                    {
                        KeyCombo = KeyComboBox.Text.Trim(),
                        Description = DescriptionBox.Text.Trim(),
                        IsCustom = true
                    };
                    ModEntry.Config.Shortcuts.Add(newShortcut);
                    Helper.WriteConfig(ModEntry.Config);
                    Game1.activeClickableMenu = ParentMenu;
                    ParentMenu.Refresh();
                }
                return;
            }

            if (CancelButton.containsPoint(x, y))
            {
                Game1.playSound("cancel");
                Game1.activeClickableMenu = ParentMenu;
                return;
            }

            base.receiveLeftClick(x, y, playSound);
        }

        public override void receiveKeyPress(Keys key)
        {
            // Allow Escape to always work for closing the menu
            if (key == Keys.Escape)
            {
                base.receiveKeyPress(key);
                return;
            }

            // When description box is selected, allow typing without triggering game actions
            if (DescriptionBox.Selected && !KeyComboSelected)
            {
                // Don't call base.receiveKeyPress for most keys to prevent game actions
                // But the TextBox will still handle the input normally
                return;
            }

            if (KeyComboSelected && KeyComboBox.Selected)
            {
                // Capture the key combination
                string keyText = key.ToString();

                // Check for modifiers
                var keyboardState = Keyboard.GetState();
                List<string> modifiers = new List<string>();

                if (keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift))
                    modifiers.Add("Shift");
                if (keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl))
                    modifiers.Add("Ctrl");
                if (keyboardState.IsKeyDown(Keys.LeftAlt) || keyboardState.IsKeyDown(Keys.RightAlt))
                    modifiers.Add("Alt");

                // Allow capturing modifier keys by themselves
                if (key == Keys.LeftControl)
                {
                    KeyComboBox.Text = "LeftControl";
                    return;
                }
                if (key == Keys.RightControl)
                {
                    KeyComboBox.Text = "RightControl";
                    return;
                }
                if (key == Keys.LeftShift)
                {
                    KeyComboBox.Text = "LeftShift";
                    return;
                }
                if (key == Keys.RightShift)
                {
                    KeyComboBox.Text = "RightShift";
                    return;
                }
                if (key == Keys.LeftAlt)
                {
                    KeyComboBox.Text = "LeftAlt";
                    return;
                }
                if (key == Keys.RightAlt)
                {
                    KeyComboBox.Text = "RightAlt";
                    return;
                }

                if (modifiers.Count > 0 && key != Keys.LeftShift && key != Keys.RightShift &&
                    key != Keys.LeftControl && key != Keys.RightControl &&
                    key != Keys.LeftAlt && key != Keys.RightAlt)
                {
                    KeyComboBox.Text = string.Join(" + ", modifiers) + " + " + keyText;
                }
                else if (key != Keys.LeftShift && key != Keys.RightShift &&
                         key != Keys.LeftControl && key != Keys.RightControl &&
                         key != Keys.LeftAlt && key != Keys.RightAlt)
                {
                    KeyComboBox.Text = keyText;
                }
                return;
            }

            base.receiveKeyPress(key);
        }

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.6f);
            Game1.drawDialogueBox(xPositionOnScreen, yPositionOnScreen, width, height, false, true);

            Utility.drawTextWithShadow(b, Helper.Translation.Get("add.title"), Game1.dialogueFont,
                new Vector2(xPositionOnScreen + 60, yPositionOnScreen + 40), Color.Black);

            Utility.drawTextWithShadow(b, Helper.Translation.Get("add.keyCombo"), Game1.smallFont,
                new Vector2(xPositionOnScreen + 50, yPositionOnScreen + 95), Color.Black);
            KeyComboBox.Draw(b);

            Utility.drawTextWithShadow(b, Helper.Translation.Get("add.description"), Game1.smallFont,
                new Vector2(xPositionOnScreen + 50, yPositionOnScreen + 195), Color.Black);
            DescriptionBox.Draw(b);

            // Draw buttons
            drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                SaveButton.bounds.X, SaveButton.bounds.Y, SaveButton.bounds.Width, SaveButton.bounds.Height,
                SaveButton.containsPoint(Game1.getMouseX(), Game1.getMouseY()) ? Color.LightGreen : Color.White, 1f, false);
            Utility.drawTextWithShadow(b, SaveButton.name, Game1.dialogueFont,
                new Vector2(SaveButton.bounds.X + 35, SaveButton.bounds.Y + 15), Color.Black);

            drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                CancelButton.bounds.X, CancelButton.bounds.Y, CancelButton.bounds.Width, CancelButton.bounds.Height,
                CancelButton.containsPoint(Game1.getMouseX(), Game1.getMouseY()) ? Color.LightCoral : Color.White, 1f, false);
            Utility.drawTextWithShadow(b, CancelButton.name, Game1.dialogueFont,
                new Vector2(CancelButton.bounds.X + 20, CancelButton.bounds.Y + 15), Color.Black);

            drawMouse(b);
        }
    }
}