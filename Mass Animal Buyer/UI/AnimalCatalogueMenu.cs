using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Buildings;
using StardewValley.GameData.FarmAnimals;
using StardewValley.Menus;
using StardewValley.TokenizableStrings;
using StardewValley.Locations;

namespace MassAnimalBuyer.UI
{
    public class AnimalCatalogueMenu : IClickableMenu
    {
        private List<string> _animalTypes;
        private IModHelper _helper;

        // Logic
        private int _selectedAnimalIndex = -1;
        private string _selectedAnimalId = null;
        private List<Building> _validBuildings;
        private int _selectedBuildingIndex = 0;
        private int _quantity = 1;
        private int _maxAffordable = 0;
        private int _currentTotalCount = 0;

        // Scroll Logic
        private const int SLOTS_PER_ROW = 4;
        private const int VISIBLE_ROWS = 5;
        private const int TOTAL_SLOTS = SLOTS_PER_ROW * VISIBLE_ROWS;
        private int _currentScroll = 0;
        private int _maxScroll = 0;
        private bool _isScrolling = false;
        private Rectangle _scrollRunnerRect;

        // UI Components
        private List<ClickableTextureComponent> _gridSlots;
        private ClickableTextureComponent _scrollUp, _scrollDown, _scrollBar;

        private ClickableTextureComponent _btnBldgPrev, _btnBldgNext;
        private ClickableTextureComponent _btnMinus, _btnPlus, _btnMax;
        private ClickableTextureComponent _btnBuy;

        private string _hoverText = "";

        public AnimalCatalogueMenu(List<string> animalTypes, IModHelper helper)
            : base(0, 0, 800, 600, true) // positions updated in Init
        {
            _animalTypes = animalTypes;
            _helper = helper;
            _validBuildings = new List<Building>();
            _gridSlots = new List<ClickableTextureComponent>();

            // Calculate Max Scroll
            int totalRows = (int)Math.Ceiling((double)_animalTypes.Count / SLOTS_PER_ROW);
            _maxScroll = Math.Max(0, totalRows - VISIBLE_ROWS);

            if (_animalTypes.Count > 0)
            {
                _selectedAnimalIndex = 0;
                _selectedAnimalId = _animalTypes[0];
            }

            InitializeLayout();
            UpdateSelection();
        }

        private void InitializeLayout()
        {
            // FIX: Use Utility to calculate perfect center on any resolution/zoom
            Vector2 centeredPos = Utility.getTopLeftPositionForCenteringOnScreen(this.width, this.height);
            this.xPositionOnScreen = (int)centeredPos.X;
            this.yPositionOnScreen = (int)centeredPos.Y;

            int x = this.xPositionOnScreen;
            int y = this.yPositionOnScreen;

            // --- 1. Animal Grid Slots (Left Side) ---
            _gridSlots.Clear();
            int startX = x + 40;
            int startY = y + 100;

            for (int i = 0; i < TOTAL_SLOTS; i++)
            {
                int col = i % SLOTS_PER_ROW;
                int row = i / SLOTS_PER_ROW;

                _gridSlots.Add(new ClickableTextureComponent(
                    new Rectangle(startX + (col * 68), startY + (row * 68), 64, 64),
                    null, Rectangle.Empty, 1f)
                { myID = i });
            }

            // --- 2. Scrollbar (Right of grid) ---
            int scrollX = startX + (SLOTS_PER_ROW * 68) + 12;
            int gridHeight = (VISIBLE_ROWS * 68);

            _scrollUp = new ClickableTextureComponent(new Rectangle(scrollX, startY, 44, 48), Game1.mouseCursors, new Rectangle(421, 459, 11, 12), 4f);
            _scrollDown = new ClickableTextureComponent(new Rectangle(scrollX, startY + gridHeight - 48, 44, 48), Game1.mouseCursors, new Rectangle(421, 472, 11, 12), 4f);

            _scrollRunnerRect = new Rectangle(scrollX + 12, startY + 48, 24, gridHeight - 96);
            _scrollBar = new ClickableTextureComponent(new Rectangle(scrollX + 12, startY + 48, 24, 40), Game1.mouseCursors, new Rectangle(435, 463, 6, 10), 4f);

            // --- 3. Right Side Controls ---
            int rightCenter = x + 580;

            _btnBldgPrev = new ClickableTextureComponent(new Rectangle(rightCenter - 150, y + 180, 48, 44), Game1.mouseCursors, new Rectangle(352, 495, 12, 11), 4f);
            _btnBldgNext = new ClickableTextureComponent(new Rectangle(rightCenter + 110, y + 180, 48, 44), Game1.mouseCursors, new Rectangle(365, 495, 12, 11), 4f);

            _btnMinus = new ClickableTextureComponent(new Rectangle(rightCenter - 80, y + 290, 44, 48), Game1.mouseCursors, new Rectangle(177, 345, 7, 8), 4f);
            _btnPlus = new ClickableTextureComponent(new Rectangle(rightCenter + 40, y + 290, 44, 48), Game1.mouseCursors, new Rectangle(184, 345, 7, 8), 4f);
            _btnMax = new ClickableTextureComponent(new Rectangle(rightCenter + 100, y + 295, 40, 30), Game1.mouseCursors, new Rectangle(256, 256, 10, 10), 1f);

            _btnBuy = new ClickableTextureComponent(new Rectangle(rightCenter - 32, y + 410, 64, 64), Game1.mouseCursors, new Rectangle(128, 256, 64, 64), 1f);

            this.upperRightCloseButton = new ClickableTextureComponent(new Rectangle(x + width - 48, y - 8, 48, 48), Game1.mouseCursors, new Rectangle(337, 494, 12, 12), 4f);
        }

        // ... [Helper Methods Keep Same as Previous] ...
        private List<Building> GetAllAnimalBuildings()
        {
            var list = new List<Building>();
            foreach (var location in Game1.locations)
                foreach (var building in location.buildings)
                    if (building.GetIndoors() is AnimalHouse) list.Add(building);
            return list;
        }

        private void UpdateSelection()
        {
            if (_selectedAnimalId == null) return;
            _validBuildings.Clear();
            _selectedBuildingIndex = 0;

            bool forceMode = Game1.input.GetKeyboardState().IsKeyDown(Keys.LeftControl);
            var allBuildings = GetAllAnimalBuildings();

            foreach (var b in allBuildings)
            {
                if (b.GetIndoors() is AnimalHouse house)
                {
                    int currentCount = GetTotalAnimalCount(b);
                    if (currentCount >= b.maxOccupants.Value) continue;
                    if (forceMode || IsAnimalPermitted(b, house, _selectedAnimalId))
                        _validBuildings.Add(b);
                }
            }

            var preferred = new List<Building>();
            var others = new List<Building>();
            foreach (var b in _validBuildings)
            {
                if (!forceMode && IsAnimalPermitted(b, b.GetIndoors() as AnimalHouse, _selectedAnimalId)) preferred.Add(b);
                else others.Add(b);
            }
            _validBuildings = preferred.Concat(others).ToList();
            RecalculateMax();
        }

        private int GetTotalAnimalCount(Building b)
        {
            int count = 0;
            if (b.GetIndoors() is AnimalHouse indoors) count += indoors.animals.Count();
            GameLocation parentLoc = b.GetParentLocation();
            if (parentLoc != null)
                foreach (var animal in parentLoc.animals.Values)
                    if (animal.home == b) count++;
            return count;
        }

        private bool IsAnimalPermitted(Building b, AnimalHouse house, string animalID)
        {
            if (!Game1.content.Load<Dictionary<string, FarmAnimalData>>("Data/FarmAnimals").TryGetValue(animalID, out var animalData)) return false;
            string requiredHouse = animalData.House;
            var bData = b.GetData();
            if (bData != null && bData.ValidOccupantTypes != null && bData.ValidOccupantTypes.Count > 0) return true;
            if (requiredHouse != null && b.buildingType.Value.IndexOf(requiredHouse, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            foreach (var existingAnimal in house.animals.Values)
                if (existingAnimal.buildingTypeILiveIn.Value == requiredHouse) return true;
            return false;
        }

        private void RecalculateMax()
        {
            if (_validBuildings.Count == 0 || _selectedAnimalId == null)
            {
                _maxAffordable = 0; _quantity = 0; _currentTotalCount = 0;
                return;
            }
            var data = Game1.content.Load<Dictionary<string, FarmAnimalData>>("Data/FarmAnimals")[_selectedAnimalId];
            int cost = data.PurchasePrice;
            Building current = _validBuildings[_selectedBuildingIndex];
            _currentTotalCount = GetTotalAnimalCount(current);
            int space = current.maxOccupants.Value - _currentTotalCount;
            int canAfford = (cost > 0) ? Game1.player.Money / cost : 999;
            _maxAffordable = Math.Min(space, canAfford);
            if (_maxAffordable < 0) _maxAffordable = 0;
            if (_quantity > _maxAffordable) _quantity = _maxAffordable;
            if (_quantity < 1 && _maxAffordable > 0) _quantity = 1;
            if (_maxAffordable == 0) _quantity = 0;
        }

        // --- Inputs ---

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            // Grid
            for (int i = 0; i < _gridSlots.Count; i++)
            {
                if (_gridSlots[i].containsPoint(x, y))
                {
                    int actualIndex = (_currentScroll * SLOTS_PER_ROW) + i;
                    if (actualIndex < _animalTypes.Count)
                    {
                        _selectedAnimalIndex = actualIndex;
                        _selectedAnimalId = _animalTypes[actualIndex];
                        UpdateSelection();
                        Game1.playSound("smallSelect");
                        return;
                    }
                }
            }

            // Scrollbar - Logic runs even if maxScroll is 0 (to catch clicks), but action only if > 0
            if (_maxScroll > 0)
            {
                if (_scrollUp.containsPoint(x, y)) { Scroll(-1); Game1.playSound("shwip"); }
                if (_scrollDown.containsPoint(x, y)) { Scroll(1); Game1.playSound("shwip"); }
                if (_scrollRunnerRect.Contains(x, y)) { _isScrolling = true; SetScrollBarToMouse(y); }
            }

            // Controls
            if (_validBuildings.Count > 0)
            {
                if (_btnBldgPrev.containsPoint(x, y)) { _selectedBuildingIndex--; if (_selectedBuildingIndex < 0) _selectedBuildingIndex = _validBuildings.Count - 1; RecalculateMax(); Game1.playSound("shwip"); }
                if (_btnBldgNext.containsPoint(x, y)) { _selectedBuildingIndex++; if (_selectedBuildingIndex >= _validBuildings.Count) _selectedBuildingIndex = 0; RecalculateMax(); Game1.playSound("shwip"); }
                if (_btnMinus.containsPoint(x, y)) { _quantity--; if (_quantity < 1) _quantity = 1; Game1.playSound("smallSelect"); }
                if (_btnPlus.containsPoint(x, y)) { _quantity++; if (_quantity > _maxAffordable) _quantity = _maxAffordable; Game1.playSound("smallSelect"); }
                if (_btnMax.containsPoint(x, y)) { _quantity = _maxAffordable; Game1.playSound("smallSelect"); }
                if (_btnBuy.containsPoint(x, y)) PerformPurchase();
            }

            if (upperRightCloseButton.containsPoint(x, y)) exitThisMenu();
        }

        public override void leftClickHeld(int x, int y)
        {
            if (_isScrolling) SetScrollBarToMouse(y);
        }
        public override void releaseLeftClick(int x, int y) { _isScrolling = false; }
        public override void receiveScrollWheelAction(int direction) { Scroll(direction > 0 ? -1 : 1); }

        private void Scroll(int direction)
        {
            _currentScroll += direction;
            if (_currentScroll < 0) _currentScroll = 0;
            if (_currentScroll > _maxScroll) _currentScroll = _maxScroll;
            UpdateScrollBarPos();
        }

        private void SetScrollBarToMouse(int y)
        {
            if (_maxScroll <= 0) return;
            float percentage = (float)(y - _scrollRunnerRect.Y) / _scrollRunnerRect.Height;
            _currentScroll = (int)Math.Round(percentage * _maxScroll);
            if (_currentScroll < 0) _currentScroll = 0;
            if (_currentScroll > _maxScroll) _currentScroll = _maxScroll;
            UpdateScrollBarPos();
        }

        private void UpdateScrollBarPos()
        {
            if (_maxScroll > 0)
            {
                float percentage = (float)_currentScroll / _maxScroll;
                _scrollBar.bounds.Y = _scrollRunnerRect.Y + (int)(percentage * (_scrollRunnerRect.Height - _scrollBar.bounds.Height));
            }
        }

        public override void receiveKeyPress(Keys key)
        {
            if (key == Keys.LeftControl || key == Keys.RightControl) UpdateSelection();
            base.receiveKeyPress(key);
        }

        private void PerformPurchase()
        {
            RecalculateMax();
            if (_quantity <= 0) return;
            var data = Game1.content.Load<Dictionary<string, FarmAnimalData>>("Data/FarmAnimals")[_selectedAnimalId];
            int totalCost = data.PurchasePrice * _quantity;
            if (Game1.player.Money < totalCost)
            {
                Game1.addHUDMessage(new HUDMessage("Not enough money!", 3)); return;
            }
            Game1.player.Money -= totalCost;
            Building b = _validBuildings[_selectedBuildingIndex];
            AnimalHouse indoors = b.GetIndoors() as AnimalHouse;
            var multiplayer = _helper.Reflection.GetField<Multiplayer>(typeof(Game1), "multiplayer").GetValue();
            string displayName = TokenParser.ParseText(data.DisplayName);

            for (int i = 0; i < _quantity; i++)
            {
                long id = multiplayer.getNewID();
                FarmAnimal a = new FarmAnimal(_selectedAnimalId, id, Game1.player.UniqueMultiplayerID);
                a.Name = Dialogue.randomName();
                a.home = b;
                indoors.animals.Add(id, a);
            }
            Game1.playSound("coin");
            Game1.addHUDMessage(new HUDMessage($"Bought {_quantity} {displayName}s!", 2));
            RecalculateMax();
        }

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.75f);
            Game1.drawDialogueBox(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, false, true);

            int x = this.xPositionOnScreen;
            int y = this.yPositionOnScreen;

            SpriteText.drawStringHorizontallyCenteredAt(b, "Animal Catalogue", x + width / 2, y + 35);

            bool forceMode = Game1.input.GetKeyboardState().IsKeyDown(Keys.LeftControl);
            if (forceMode)
            {
                string overrideText = "[FORCE MODE ACTIVE]";
                Utility.drawTextWithShadow(b, overrideText, Game1.smallFont, new Vector2(x + 50, y + 460), Color.Red);
            }

            var farmAnimalsData = Game1.content.Load<Dictionary<string, FarmAnimalData>>("Data/FarmAnimals");

            // --- Draw Grid ---
            for (int i = 0; i < _gridSlots.Count; i++)
            {
                int actualIndex = (_currentScroll * SLOTS_PER_ROW) + i;
                if (actualIndex >= _animalTypes.Count) break;

                string animalID = _animalTypes[actualIndex];
                var slot = _gridSlots[i];
                bool isSelected = (_selectedAnimalId == animalID);

                IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 396, 15, 15),
                    slot.bounds.X, slot.bounds.Y, slot.bounds.Width, slot.bounds.Height,
                    isSelected ? Color.LightGreen : Color.White, 4f, false);

                if (farmAnimalsData.TryGetValue(animalID, out var data))
                {
                    try
                    {
                        Texture2D tex = Game1.content.Load<Texture2D>(data.Texture);
                        int frameW = data.SpriteWidth > 0 ? data.SpriteWidth : 16;
                        int frameH = data.SpriteHeight > 0 ? data.SpriteHeight : 16;
                        float scale = Math.Min(48f / frameW, 48f / frameH);
                        b.Draw(tex, new Vector2(slot.bounds.X + 32, slot.bounds.Y + 32),
                            new Rectangle(0, 0, frameW, frameH), Color.White, 0f,
                            new Vector2(frameW / 2, frameH / 2), scale, SpriteEffects.None, 0.8f);
                    }
                    catch { }
                }
                if (slot.containsPoint(Game1.getOldMouseX(), Game1.getOldMouseY()))
                    _hoverText = TokenParser.ParseText(farmAnimalsData[animalID].DisplayName);
            }

            // --- Draw Scrollbar (Always draw track to be visible) ---
            // Draw Track
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(403, 383, 6, 6),
                _scrollRunnerRect.X, _scrollRunnerRect.Y, _scrollRunnerRect.Width, _scrollRunnerRect.Height,
                Color.White, 4f, false);

            _scrollUp.draw(b);
            _scrollDown.draw(b);

            if (_maxScroll > 0)
                _scrollBar.draw(b); // Only draw thumb if scrolling is possible

            // --- Right Side ---
            if (_selectedAnimalId != null && farmAnimalsData.TryGetValue(_selectedAnimalId, out var selectedData))
            {
                int rightCenter = x + 580;
                string displayName = TokenParser.ParseText(selectedData.DisplayName) ?? _selectedAnimalId;
                SpriteText.drawStringHorizontallyCenteredAt(b, displayName, rightCenter, y + 100);

                string costText = $"{selectedData.PurchasePrice}g";
                b.DrawString(Game1.smallFont, costText, new Vector2(rightCenter - Game1.smallFont.MeasureString(costText).X / 2, y + 150), Color.SaddleBrown);

                if (_validBuildings.Count > 0)
                {
                    _btnBldgPrev.draw(b);
                    _btnBldgNext.draw(b);

                    Building bldg = _validBuildings[_selectedBuildingIndex];
                    string locName = bldg.GetParentLocation().Name.Equals("Farm") ? "Farm" : "Island/Mod";
                    string bName = $"{bldg.buildingType.Value} ({locName})";
                    int space = bldg.maxOccupants.Value - _currentTotalCount;

                    float maxTextWidth = 230f;
                    Vector2 textSize = Game1.smallFont.MeasureString(bName);
                    float fontScale = 1f;
                    if (textSize.X > maxTextWidth) fontScale = maxTextWidth / textSize.X;

                    Vector2 pos = new Vector2(rightCenter - (textSize.X * fontScale) / 2, y + 190 + (textSize.Y * (1f - fontScale)) / 2);
                    b.DrawString(Game1.smallFont, bName, pos, Game1.textColor, 0f, Vector2.Zero, fontScale, SpriteEffects.None, 1f);

                    Utility.drawTextWithShadow(b, $"Space: {space}", Game1.smallFont, new Vector2(rightCenter - Game1.smallFont.MeasureString($"Space: {space}").X / 2, y + 220), Color.DarkBlue);

                    _btnMinus.draw(b);
                    _btnPlus.draw(b);
                    Utility.drawTextWithShadow(b, "MAX", Game1.tinyFont, new Vector2(_btnMax.bounds.X, _btnMax.bounds.Y), Color.Red);

                    string qty = _quantity.ToString();
                    SpriteText.drawStringHorizontallyCenteredAt(b, qty, rightCenter, y + 300);

                    string total = $"Total: {_quantity * selectedData.PurchasePrice}g";
                    b.DrawString(Game1.dialogueFont, total, new Vector2(rightCenter - Game1.dialogueFont.MeasureString(total).X / 2, y + 360), Game1.textColor);

                    _btnBuy.draw(b);
                }
                else
                {
                    string msg = "No buildings with\nspace available!";
                    b.DrawString(Game1.smallFont, msg, new Vector2(rightCenter - Game1.smallFont.MeasureString(msg).X / 2, y + 230), Color.Red);
                }
            }

            upperRightCloseButton.draw(b);
            drawMouse(b);

            if (!string.IsNullOrEmpty(_hoverText))
                IClickableMenu.drawHoverText(b, _hoverText, Game1.smallFont);
            _hoverText = "";
        }

        public override void performHoverAction(int x, int y)
        {
            _btnBuy.tryHover(x, y, 0.2f);
            base.performHoverAction(x, y);
        }
    }
}