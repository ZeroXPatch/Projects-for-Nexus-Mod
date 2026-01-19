using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Buildings;
using StardewValley.Menus;

namespace MassAnimalMover.UI
{
    public class TransferMenu : IClickableMenu
    {
        private List<Building> _allAnimalBuildings;
        private int _sourceIndex = 0;
        private int _destIndex = 0;
        private HashSet<long> _selectedAnimalIds;

        // UI Components
        private ClickableTextureComponent _btnSourcePrev, _btnSourceNext;
        private ClickableTextureComponent _btnDestPrev, _btnDestNext;
        private ClickableTextureComponent _btnTransfer;

        private List<ClickableComponent> _animalSlots;

        // --- LAYOUT SETTINGS ---
        private const int SLOT_SIZE = 64;
        private const int ANIMAL_DRAW_SIZE = 44;
        private const int GAP = 12;
        private const int COLS = 5;

        // Height adjustment to make room for text at the top
        // Increased height from 600 to 650 to "extend" the UI vertically
        private const int MENU_WIDTH = 800;
        private const int MENU_HEIGHT = 650;

        public TransferMenu()
            : base(Game1.viewport.Width / 2 - (MENU_WIDTH / 2), Game1.viewport.Height / 2 - (MENU_HEIGHT / 2), MENU_WIDTH, MENU_HEIGHT, true)
        {
            this._selectedAnimalIds = new HashSet<long>();
            this._animalSlots = new List<ClickableComponent>();

            LoadBuildings();
            InitializeComponents();
        }

        private void LoadBuildings()
        {
            _allAnimalBuildings = new List<Building>();

            foreach (GameLocation location in Game1.locations)
            {
                if (location.buildings.Count > 0)
                {
                    foreach (var building in location.buildings)
                    {
                        if (building.GetIndoors() is AnimalHouse && !building.isUnderConstruction())
                        {
                            _allAnimalBuildings.Add(building);
                        }
                    }
                }
            }

            if (_allAnimalBuildings.Count > 1) _destIndex = 1;
        }

        private void InitializeComponents()
        {
            this.upperRightCloseButton = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + this.width - 48, this.yPositionOnScreen - 8, 48, 48),
                Game1.mouseCursors, new Rectangle(337, 494, 12, 12), 4f);

            int leftColX = this.xPositionOnScreen + 64;
            int rightColX = this.xPositionOnScreen + this.width / 2 + 32;

            // --- LAYOUT CHANGE: Push arrows down to y + 140 to make room for Location Name on top ---
            int headerY = this.yPositionOnScreen + 140;

            // Source Nav
            _btnSourcePrev = new ClickableTextureComponent(new Rectangle(leftColX, headerY, 48, 44), Game1.mouseCursors, new Rectangle(352, 495, 12, 11), 4f);
            _btnSourceNext = new ClickableTextureComponent(new Rectangle(leftColX + 250, headerY, 48, 44), Game1.mouseCursors, new Rectangle(365, 495, 12, 11), 4f);

            // Dest Nav
            _btnDestPrev = new ClickableTextureComponent(new Rectangle(rightColX, headerY, 48, 44), Game1.mouseCursors, new Rectangle(352, 495, 12, 11), 4f);
            _btnDestNext = new ClickableTextureComponent(new Rectangle(rightColX + 250, headerY, 48, 44), Game1.mouseCursors, new Rectangle(365, 495, 12, 11), 4f);

            // Transfer Button
            _btnTransfer = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + this.width / 2 - 32, this.yPositionOnScreen + this.height - 96, 64, 64),
                Game1.mouseCursors, new Rectangle(128, 256, 64, 64), 1f);

            UpdateAnimalSlots();
        }

        private List<FarmAnimal> GetAnimalsForBuilding(Building b)
        {
            List<FarmAnimal> results = new List<FarmAnimal>();

            if (b.GetIndoors() is AnimalHouse indoors)
                results.AddRange(indoors.animals.Values);

            GameLocation parentLoc = b.GetParentLocation();
            if (parentLoc != null)
            {
                foreach (var animal in parentLoc.animals.Values)
                {
                    if (animal.home == b)
                    {
                        if (!results.Any(x => x.myID.Value == animal.myID.Value))
                            results.Add(animal);
                    }
                }
            }
            return results;
        }

        private void UpdateAnimalSlots()
        {
            _animalSlots.Clear();
            if (_allAnimalBuildings.Count == 0) return;

            Building source = _allAnimalBuildings[_sourceIndex];
            var animals = GetAnimalsForBuilding(source);

            int startX = this.xPositionOnScreen + 48;
            // --- LAYOUT CHANGE: Push animal grid down slightly ---
            int startY = this.yPositionOnScreen + 220;

            for (int i = 0; i < animals.Count; i++)
            {
                int col = i % COLS;
                int row = i / COLS;

                int x = startX + (col * (SLOT_SIZE + GAP));
                int y = startY + (row * (SLOT_SIZE + GAP));

                _animalSlots.Add(new ClickableComponent(
                    new Rectangle(x, y, SLOT_SIZE, SLOT_SIZE),
                    animals[i].myID.Value.ToString()
                ));
            }
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            if (_allAnimalBuildings.Count == 0) { base.receiveLeftClick(x, y, playSound); return; }

            if (_btnSourcePrev.containsPoint(x, y)) ChangeSource(-1);
            else if (_btnSourceNext.containsPoint(x, y)) ChangeSource(1);
            else if (_btnDestPrev.containsPoint(x, y)) ChangeDest(-1);
            else if (_btnDestNext.containsPoint(x, y)) ChangeDest(1);

            foreach (var slot in _animalSlots)
            {
                if (slot.containsPoint(x, y))
                {
                    long id = long.Parse(slot.name);
                    if (_selectedAnimalIds.Contains(id)) _selectedAnimalIds.Remove(id);
                    else _selectedAnimalIds.Add(id);
                    Game1.playSound("smallSelect");
                }
            }

            if (_btnTransfer.containsPoint(x, y)) PerformTransfer();

            base.receiveLeftClick(x, y, playSound);
        }

        private void ChangeSource(int direction)
        {
            _sourceIndex += direction;
            if (_sourceIndex < 0) _sourceIndex = _allAnimalBuildings.Count - 1;
            if (_sourceIndex >= _allAnimalBuildings.Count) _sourceIndex = 0;

            Game1.playSound("shwip");
            _selectedAnimalIds.Clear();
            UpdateAnimalSlots();
        }

        private void ChangeDest(int direction)
        {
            _destIndex += direction;
            if (_destIndex < 0) _destIndex = _allAnimalBuildings.Count - 1;
            if (_destIndex >= _allAnimalBuildings.Count) _destIndex = 0;
            Game1.playSound("shwip");
        }

        private void PerformTransfer()
        {
            if (_selectedAnimalIds.Count == 0 || _sourceIndex == _destIndex) return;

            Building source = _allAnimalBuildings[_sourceIndex];
            Building dest = _allAnimalBuildings[_destIndex];

            string result = AnimalManager.TransferAnimals(source, dest, _selectedAnimalIds.ToList());

            if (result.Contains("Moved"))
            {
                Item starIcon = ItemRegistry.Create("(O)268");
                HUDMessage msg = new HUDMessage(result);
                msg.messageSubject = starIcon;
                msg.timeLeft = 3500f;

                Game1.addHUDMessage(msg);
                Game1.playSound("newArtifact");
            }
            else
            {
                Game1.addHUDMessage(new HUDMessage(result, 3));
                Game1.playSound("cancel");
            }

            _selectedAnimalIds.Clear();
            UpdateAnimalSlots();
        }

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.75f);
            Game1.drawDialogueBox(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, false, true);

            if (_allAnimalBuildings.Count == 0) return;

            Building source = _allAnimalBuildings[_sourceIndex];
            Building dest = _allAnimalBuildings[_destIndex];

            var animals = GetAnimalsForBuilding(source);
            var destAnimals = GetAnimalsForBuilding(dest);

            DrawBuildingInfo(b, source, _btnSourcePrev, _btnSourceNext, true, animals.Count);
            DrawBuildingInfo(b, dest, _btnDestPrev, _btnDestNext, false, destAnimals.Count);

            string hoverText = "";

            for (int i = 0; i < _animalSlots.Count; i++)
            {
                if (i >= animals.Count) break;

                var animal = animals[i];
                var slot = _animalSlots[i];
                bool isSelected = _selectedAnimalIds.Contains(animal.myID.Value);

                IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 396, 15, 15),
                    slot.bounds.X, slot.bounds.Y, slot.bounds.Width, slot.bounds.Height,
                    isSelected ? Color.LightGreen : Color.White, 4f, false);

                DrawStrictlyScaledAnimal(b, animal, slot.bounds);

                if (slot.containsPoint(Game1.getOldMouseX(), Game1.getOldMouseY()))
                {
                    hoverText = animal.Name;
                }
            }

            bool isValid = _selectedAnimalIds.Count > 0 && _sourceIndex != _destIndex &&
                           (destAnimals.Count + _selectedAnimalIds.Count <= dest.maxOccupants.Value);

            _btnTransfer.draw(b, isValid ? Color.White : Color.Gray * 0.5f, 1f);
            Utility.drawWithShadow(b, Game1.mouseCursors, new Vector2(_btnTransfer.bounds.X + 8, _btnTransfer.bounds.Y + 8),
                new Rectangle(365, 495, 12, 11), Color.White, 0f, Vector2.Zero, 4f, false, 0.9f);

            base.draw(b);

            if (!string.IsNullOrEmpty(hoverText))
            {
                IClickableMenu.drawHoverText(b, hoverText, Game1.smallFont);
            }

            this.drawMouse(b);
        }

        private void DrawStrictlyScaledAnimal(SpriteBatch b, FarmAnimal animal, Rectangle slotBounds)
        {
            Texture2D texture = animal.Sprite.Texture;
            Rectangle sourceRect = animal.Sprite.SourceRect;

            float targetSize = ANIMAL_DRAW_SIZE;
            float scaleX = targetSize / sourceRect.Width;
            float scaleY = targetSize / sourceRect.Height;
            float scale = Math.Min(scaleX, scaleY);

            float finalWidth = sourceRect.Width * scale;
            float finalHeight = sourceRect.Height * scale;

            float destX = slotBounds.X + (slotBounds.Width - finalWidth) / 2;
            float destY = slotBounds.Y + (slotBounds.Height - finalHeight) / 2;

            b.Draw(texture, new Vector2(destX, destY), sourceRect, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 1f);
        }

        private void DrawBuildingInfo(SpriteBatch b, Building building, ClickableTextureComponent btnPrev, ClickableTextureComponent btnNext, bool isLeft, int currentCount)
        {
            int xBase = isLeft ? this.xPositionOnScreen + 64 : this.xPositionOnScreen + this.width / 2 + 32;

            // This Y matches the 'headerY' we set in InitializeComponents (approx 140px down)
            int yBase = this.yPositionOnScreen + 140;
            int centerX = xBase + 125; // 250 width / 2 = 125 center

            btnPrev.draw(b);
            btnNext.draw(b);

            // --- DATA PREP ---
            string locationName = building.GetParentLocation()?.Name ?? "Unknown";
            if (locationName == "Farm") locationName = "Farm";
            else if (locationName == "IslandWest") locationName = "Island";

            string name = building.buildingType.Value;
            if (name.Length > 12) name = name.Substring(0, 12) + ".";

            // --- DRAWING ORDER CHANGED ---

            // 1. Draw Location Name (TOP)
            // Drawn about 45 pixels ABOVE the arrows.
            Vector2 locSize = Game1.smallFont.MeasureString(locationName); // Using smallFont instead of tiny for visibility
            b.DrawString(Game1.smallFont, locationName, new Vector2(centerX - locSize.X / 2, yBase - 45), Color.DarkSlateGray);

            // 2. Draw Building Name (MIDDLE)
            // Drawn vertically aligned with the arrows (yBase + 10)
            Vector2 textSize = Game1.smallFont.MeasureString(name);
            b.DrawString(Game1.smallFont, name, new Vector2(centerX - textSize.X / 2, yBase + 10), Game1.textColor);

            // 3. Draw Capacity (BOTTOM)
            string capInfo = $"Cap: {currentCount}/{building.maxOccupants.Value}";
            Vector2 capSize = Game1.smallFont.MeasureString(capInfo);
            b.DrawString(Game1.smallFont, capInfo, new Vector2(centerX - capSize.X / 2, yBase + 50), isLeft ? Color.DarkSlateGray : Color.DarkBlue);
        }

        public override void performHoverAction(int x, int y)
        {
            _btnSourcePrev.tryHover(x, y); _btnSourceNext.tryHover(x, y);
            _btnDestPrev.tryHover(x, y); _btnDestNext.tryHover(x, y);
            _btnTransfer.tryHover(x, y);
            base.performHoverAction(x, y);
        }
    }
}