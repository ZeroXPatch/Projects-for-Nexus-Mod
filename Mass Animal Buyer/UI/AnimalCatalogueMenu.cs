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

        private int _selectedAnimalIndex = 0;
        private List<Building> _validBuildings;
        private int _selectedBuildingIndex = 0;
        private int _quantity = 1;
        private int _maxAffordable = 0;
        private int _currentTotalCount = 0;

        private List<ClickableTextureComponent> _animalButtons;
        private ClickableTextureComponent _btnBldgPrev, _btnBldgNext;
        private ClickableTextureComponent _btnMinus, _btnPlus, _btnMax;
        private ClickableTextureComponent _btnBuy;

        private string _hoverText = "";

        public AnimalCatalogueMenu(List<string> animalTypes, IModHelper helper)
            : base(Game1.viewport.Width / 2 - 400, Game1.viewport.Height / 2 - 300, 800, 600, true)
        {
            _animalTypes = animalTypes;
            _helper = helper;
            _validBuildings = new List<Building>();
            _animalButtons = new List<ClickableTextureComponent>();

            InitializeLayout();
            UpdateSelection();
        }

        private void InitializeLayout()
        {
            int x = this.xPositionOnScreen;
            int y = this.yPositionOnScreen;

            // 1. Animal Grid
            int startX = x + 40;
            int startY = y + 100;
            int col = 0;
            int row = 0;

            foreach (var type in _animalTypes)
            {
                _animalButtons.Add(new ClickableTextureComponent(
                    new Rectangle(startX + (col * 68), startY + (row * 68), 64, 64),
                    null, Rectangle.Empty, 1f)
                {
                    name = type,
                    myID = _animalButtons.Count
                });

                col++;
                if (col >= 4) { col = 0; row++; }
            }

            // 2. Right Side Controls
            int rightCenter = x + 550;

            // FIX: Widened the gap between arrows to 260px (from ~200px)
            _btnBldgPrev = new ClickableTextureComponent(new Rectangle(rightCenter - 150, y + 180, 48, 44), Game1.mouseCursors, new Rectangle(352, 495, 12, 11), 4f);
            _btnBldgNext = new ClickableTextureComponent(new Rectangle(rightCenter + 110, y + 180, 48, 44), Game1.mouseCursors, new Rectangle(365, 495, 12, 11), 4f);

            _btnMinus = new ClickableTextureComponent(new Rectangle(rightCenter - 80, y + 290, 44, 48), Game1.mouseCursors, new Rectangle(177, 345, 7, 8), 4f);
            _btnPlus = new ClickableTextureComponent(new Rectangle(rightCenter + 40, y + 290, 44, 48), Game1.mouseCursors, new Rectangle(184, 345, 7, 8), 4f);
            _btnMax = new ClickableTextureComponent(new Rectangle(rightCenter + 100, y + 295, 40, 30), Game1.mouseCursors, new Rectangle(256, 256, 10, 10), 1f);

            _btnBuy = new ClickableTextureComponent(new Rectangle(rightCenter - 32, y + 410, 64, 64), Game1.mouseCursors, new Rectangle(128, 256, 64, 64), 1f);

            this.upperRightCloseButton = new ClickableTextureComponent(new Rectangle(x + width - 48, y - 8, 48, 48), Game1.mouseCursors, new Rectangle(337, 494, 12, 12), 4f);
        }

        private List<Building> GetAllAnimalBuildings()
        {
            var list = new List<Building>();
            foreach (var location in Game1.locations)
            {
                foreach (var building in location.buildings)
                {
                    if (building.GetIndoors() is AnimalHouse)
                        list.Add(building);
                }
            }
            return list;
        }

        private void UpdateSelection()
        {
            string animalID = _animalTypes[_selectedAnimalIndex];
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

                    if (forceMode || IsAnimalPermitted(b, house, animalID))
                        _validBuildings.Add(b);
                }
            }

            var preferred = new List<Building>();
            var others = new List<Building>();

            foreach (var b in _validBuildings)
            {
                if (!forceMode && IsAnimalPermitted(b, b.GetIndoors() as AnimalHouse, animalID))
                    preferred.Add(b);
                else
                    others.Add(b);
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
            {
                foreach (var animal in parentLoc.animals.Values)
                {
                    if (animal.home == b) count++;
                }
            }
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
            {
                if (existingAnimal.buildingTypeILiveIn.Value == requiredHouse) return true;
            }
            return false;
        }

        private void RecalculateMax()
        {
            if (_validBuildings.Count == 0)
            {
                _maxAffordable = 0; _quantity = 0; _currentTotalCount = 0;
                return;
            }

            string animalID = _animalTypes[_selectedAnimalIndex];
            var data = Game1.content.Load<Dictionary<string, FarmAnimalData>>("Data/FarmAnimals")[animalID];
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

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            for (int i = 0; i < _animalButtons.Count; i++)
            {
                if (_animalButtons[i].containsPoint(x, y))
                {
                    _selectedAnimalIndex = i;
                    UpdateSelection();
                    Game1.playSound("smallSelect");
                    return;
                }
            }

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

        public override void receiveKeyPress(Keys key)
        {
            if (key == Keys.LeftControl || key == Keys.RightControl) UpdateSelection();
            base.receiveKeyPress(key);
        }

        private void PerformPurchase()
        {
            RecalculateMax();
            if (_quantity <= 0) return;

            string animalID = _animalTypes[_selectedAnimalIndex];
            var data = Game1.content.Load<Dictionary<string, FarmAnimalData>>("Data/FarmAnimals")[animalID];
            int totalCost = data.PurchasePrice * _quantity;

            if (Game1.player.Money < totalCost)
            {
                Game1.addHUDMessage(new HUDMessage("Not enough money!", 3));
                return;
            }

            Game1.player.Money -= totalCost;

            Building b = _validBuildings[_selectedBuildingIndex];
            AnimalHouse indoors = b.GetIndoors() as AnimalHouse;
            var multiplayer = _helper.Reflection.GetField<Multiplayer>(typeof(Game1), "multiplayer").GetValue();
            string displayName = TokenParser.ParseText(data.DisplayName);

            for (int i = 0; i < _quantity; i++)
            {
                long id = multiplayer.getNewID();
                FarmAnimal a = new FarmAnimal(animalID, id, Game1.player.UniqueMultiplayerID);
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

            // FIX: Moved force text to bottom left to avoid overlap
            bool forceMode = Game1.input.GetKeyboardState().IsKeyDown(Keys.LeftControl);
            if (forceMode)
            {
                string overrideText = "[FORCE MODE ACTIVE]";
                // Draw near the bottom of the left panel area
                Utility.drawTextWithShadow(b, overrideText, Game1.smallFont, new Vector2(x + 50, y + 420), Color.Red);
            }

            var farmAnimalsData = Game1.content.Load<Dictionary<string, FarmAnimalData>>("Data/FarmAnimals");

            for (int i = 0; i < _animalButtons.Count; i++)
            {
                var btn = _animalButtons[i];
                string animalID = _animalTypes[i];
                bool isSelected = (i == _selectedAnimalIndex);

                IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 396, 15, 15),
                    btn.bounds.X, btn.bounds.Y, btn.bounds.Width, btn.bounds.Height,
                    isSelected ? Color.LightGreen : Color.White, 4f, false);

                if (farmAnimalsData.TryGetValue(animalID, out var data))
                {
                    try
                    {
                        Texture2D tex = Game1.content.Load<Texture2D>(data.Texture);
                        int frameW = data.SpriteWidth > 0 ? data.SpriteWidth : 16;
                        int frameH = data.SpriteHeight > 0 ? data.SpriteHeight : 16;
                        float scale = Math.Min(48f / frameW, 48f / frameH);
                        b.Draw(tex, new Vector2(btn.bounds.X + 32, btn.bounds.Y + 32),
                            new Rectangle(0, 0, frameW, frameH), Color.White, 0f,
                            new Vector2(frameW / 2, frameH / 2), scale, SpriteEffects.None, 0.8f);
                    }
                    catch { }
                }

                if (btn.containsPoint(Game1.getOldMouseX(), Game1.getOldMouseY()))
                    _hoverText = TokenParser.ParseText(farmAnimalsData[animalID].DisplayName);
            }

            int rightCenter = x + 550;
            string selectedID = _animalTypes[_selectedAnimalIndex];
            var selectedData = farmAnimalsData[selectedID];

            string displayName = TokenParser.ParseText(selectedData.DisplayName);
            if (string.IsNullOrEmpty(displayName)) displayName = selectedID;

            SpriteText.drawStringHorizontallyCenteredAt(b, displayName, rightCenter, y + 100);

            // FIX: Changed color to SaddleBrown for readability
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

                // FIX: Auto-Scaling text so it doesn't overlap arrows
                float maxTextWidth = 230f; // Gap between arrows
                Vector2 textSize = Game1.smallFont.MeasureString(bName);
                float fontScale = 1f;
                if (textSize.X > maxTextWidth) fontScale = maxTextWidth / textSize.X;

                Vector2 pos = new Vector2(rightCenter - (textSize.X * fontScale) / 2, y + 190 + (textSize.Y * (1f - fontScale)) / 2);

                b.DrawString(Game1.smallFont, bName, pos, Game1.textColor, 0f, Vector2.Zero, fontScale, SpriteEffects.None, 1f);

                // Draw Capacity below
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

                string tip = "(Hold CTRL to Force)";
                b.DrawString(Game1.tinyFont, tip, new Vector2(rightCenter - Game1.tinyFont.MeasureString(tip).X / 2, y + 270), Color.Gray);
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