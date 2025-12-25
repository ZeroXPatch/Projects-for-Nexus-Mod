using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Generic;

namespace FarmhandScheduler;

public sealed class PlannerMenu : IClickableMenu
{
    private readonly FarmhandConfig _config;
    private readonly Action<FarmhandConfig> _saveConfig;
    private readonly Action<FarmhandConfig> _applyConfig;
    private readonly List<ToggleRow> _rows;
    private readonly ITranslationHelper _i18n;

    private readonly Rectangle _startRect;
    private readonly Rectangle _endRect;
    private readonly Rectangle _saveRect;
    private readonly Rectangle _cancelRect;

    private readonly int _workX;
    private readonly int _workY;

    // Layout tuning (prevents overlap with bottom bill text)
    private const int RowsWidth = 520;
    private const int RowHeight = 56;
    private const int RowStep = 52; // tighter spacing than before
    private const int RowsTopOffset = 96;

    public PlannerMenu(
        FarmhandConfig config,
        ITranslationHelper i18n,
        Action<FarmhandConfig> save,
        Action<FarmhandConfig> apply
    )
    {
        _config = config.Clone();
        _i18n = i18n;
        _saveConfig = save;
        _applyConfig = apply;

        width = 860;
        height = 720;
        xPositionOnScreen = (Game1.uiViewport.Width - width) / 2;
        yPositionOnScreen = (Game1.uiViewport.Height - height) / 2;

        _rows = new List<ToggleRow>
        {
            new(TaskKind.None,                 _i18n.Get("menu.row.hire"),       () => _config.HelperEnabled,         v => _config.HelperEnabled = v,         ShowCost: false),

            new(TaskKind.WaterCrops,           _i18n.Get("menu.row.water"),      () => _config.WaterCrops,            v => _config.WaterCrops = v,            ShowCost: true),
            new(TaskKind.PetAnimals,           _i18n.Get("menu.row.pet"),        () => _config.PetAnimals,            v => _config.PetAnimals = v,            ShowCost: true),
            new(TaskKind.FeedAnimals,          _i18n.Get("menu.row.feed"),       () => _config.FeedAnimals,           v => _config.FeedAnimals = v,           ShowCost: true),
            new(TaskKind.HarvestCrops,         _i18n.Get("menu.row.harvest"),    () => _config.HarvestCrops,          v => _config.HarvestCrops = v,          ShowCost: true),

            // modifiers (no cost)
            new(TaskKind.HarvestLowTierOnly,   _i18n.Get("menu.row.lowtier"),    () => _config.HarvestLowTierOnly,    v => _config.HarvestLowTierOnly = v,    ShowCost: false),
            new(TaskKind.HarvestExcludeFlowers,_i18n.Get("menu.row.noflowers"),  () => _config.HarvestExcludeFlowers, v => _config.HarvestExcludeFlowers = v, ShowCost: false),

            new(TaskKind.OrganizeChests,       _i18n.Get("menu.row.chests"),     () => _config.OrganizeChests,        v => _config.OrganizeChests = v,        ShowCost: true),
        };

        // Place work-hours panel to the RIGHT of the task rows
        int rowsLeft = xPositionOnScreen + 64;
        int rowsRight = rowsLeft + RowsWidth;

        _workX = rowsRight + 18;
        _workY = yPositionOnScreen + RowsTopOffset - 10;

        _startRect = new Rectangle(_workX, _workY + 76, 64, 64);
        _endRect = new Rectangle(_workX + 90, _workY + 76, 64, 64);

        _saveRect = new Rectangle(xPositionOnScreen + width - 220, yPositionOnScreen + height - 84, 170, 64);
        _cancelRect = new Rectangle(xPositionOnScreen + 50, yPositionOnScreen + height - 84, 170, 64);
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        base.receiveLeftClick(x, y, playSound);

        for (int i = 0; i < _rows.Count; i++)
        {
            Rectangle rect = GetRowRect(i);
            if (rect.Contains(x, y))
            {
                ToggleRow row = _rows[i];
                row.Setter(!row.Getter());
                Game1.playSound("drumkit6");
                _applyConfig(_config);
                return;
            }
        }

        if (_startRect.Contains(x, y))
        {
            AdjustHours(+1, start: true);
            return;
        }

        if (_endRect.Contains(x, y))
        {
            AdjustHours(+1, start: false);
            return;
        }

        if (_saveRect.Contains(x, y))
        {
            _saveConfig(_config);
            exitThisMenu();
            Game1.playSound("bigSelect");
            return;
        }

        if (_cancelRect.Contains(x, y))
        {
            exitThisMenu();
            Game1.playSound("cancel");
        }
    }

    public override void receiveRightClick(int x, int y, bool playSound = true)
    {
        base.receiveRightClick(x, y, playSound);

        if (_startRect.Contains(x, y))
        {
            AdjustHours(-1, start: true);
            return;
        }

        if (_endRect.Contains(x, y))
        {
            AdjustHours(-1, start: false);
        }
    }

    public override void draw(SpriteBatch b)
    {
        drawTextureBox(
            b,
            Game1.menuTexture,
            new Rectangle(0, 256, 60, 60),
            xPositionOnScreen,
            yPositionOnScreen,
            width,
            height,
            Color.White,
            1f,
            drawShadow: true
        );

        string title = _i18n.Get("menu.title");
        Vector2 titleSize = Game1.dialogueFont.MeasureString(title);
        Game1.spriteBatch.DrawString(
            Game1.dialogueFont,
            title,
            new Vector2(xPositionOnScreen + (width / 2 - titleSize.X / 2), yPositionOnScreen + 32),
            Color.DarkSlateBlue
        );

        for (int i = 0; i < _rows.Count; i++)
        {
            ToggleRow row = _rows[i];
            Rectangle rowRect = GetRowRect(i);

            drawTextureBox(
                b,
                Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                rowRect.X,
                rowRect.Y,
                rowRect.Width,
                rowRect.Height,
                Color.White
            );

            bool enabled = row.Getter();
            string toggleText = enabled ? "✔" : "✗";

            // Slightly higher text baseline (since rows are tighter)
            int textY = rowRect.Y + 14;

            Game1.spriteBatch.DrawString(
                Game1.smallFont,
                toggleText,
                new Vector2(rowRect.X + 12, textY),
                enabled ? Color.DarkGreen : Color.DarkGray
            );

            Game1.spriteBatch.DrawString(
                Game1.smallFont,
                row.Label,
                new Vector2(rowRect.X + 48, textY),
                Color.Black
            );

            // Always show cost for paid action rows (gray if not selected / not hired)
            if (row.ShowCost)
            {
                int costValue = Math.Max(0, _config.GetCostForTask(row.Kind));
                string costText = $"{costValue}g";
                Vector2 costSize = Game1.smallFont.MeasureString(costText);

                bool willBeCharged = _config.HelperEnabled && enabled;
                Color costColor = willBeCharged ? Color.DarkGoldenrod : Color.Gray;

                Game1.spriteBatch.DrawString(
                    Game1.smallFont,
                    costText,
                    new Vector2(rowRect.Right - 18 - costSize.X, textY),
                    costColor
                );
            }
        }

        // Work hours panel (right side)
        string workHoursLabel = _i18n.Get("menu.workhours");
        string startText = string.Format(_i18n.Get("menu.workhours.start"), _config.StartHour);
        string endText = string.Format(_i18n.Get("menu.workhours.end"), _config.EndHour);

        Game1.spriteBatch.DrawString(Game1.smallFont, workHoursLabel, new Vector2(_workX, _workY - 10), Color.Black);
        Game1.spriteBatch.DrawString(Game1.smallFont, startText, new Vector2(_workX, _workY - 10 + 26), Color.Black);
        Game1.spriteBatch.DrawString(Game1.smallFont, endText, new Vector2(_workX, _workY - 10 + 52), Color.Black);

        drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60), _startRect.X, _startRect.Y, _startRect.Width, _startRect.Height, Color.White);
        drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60), _endRect.X, _endRect.Y, _endRect.Width, _endRect.Height, Color.White);

        Game1.spriteBatch.DrawString(Game1.smallFont, "+/-", new Vector2(_startRect.X + 16, _startRect.Y + 20), Color.DarkSlateGray);
        Game1.spriteBatch.DrawString(Game1.smallFont, "+/-", new Vector2(_endRect.X + 16, _endRect.Y + 20), Color.DarkSlateGray);

        // Bill preview (charged each morning)
        int planned = _config.CalculatePlannedBill();
        string bill = string.Format(_i18n.Get("menu.bill"), planned);
        string hint = _i18n.Get("menu.hint");
        string openWith = string.Format(_i18n.Get("menu.openwith"), _config.PlannerMenuKey);

        int labelX = xPositionOnScreen + 64;
        Game1.spriteBatch.DrawString(Game1.smallFont, bill, new Vector2(labelX, yPositionOnScreen + height - 190), Color.Black);
        Game1.spriteBatch.DrawString(Game1.smallFont, hint, new Vector2(labelX, yPositionOnScreen + height - 160), Color.Gray);
        Game1.spriteBatch.DrawString(Game1.smallFont, openWith, new Vector2(labelX, yPositionOnScreen + height - 130), Color.DarkSlateBlue);

        // Bottom buttons
        drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60), _saveRect.X, _saveRect.Y, _saveRect.Width, _saveRect.Height, Color.White);
        drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60), _cancelRect.X, _cancelRect.Y, _cancelRect.Width, _cancelRect.Height, Color.White);

        Game1.spriteBatch.DrawString(Game1.smallFont, _i18n.Get("menu.button.save"), new Vector2(_saveRect.X + 40, _saveRect.Y + 20), Color.DarkGreen);
        Game1.spriteBatch.DrawString(Game1.smallFont, _i18n.Get("menu.button.cancel"), new Vector2(_cancelRect.X + 24, _cancelRect.Y + 20), Color.DarkRed);

        drawMouse(b);
    }

    private void AdjustHours(int delta, bool start)
    {
        if (start)
        {
            _config.StartHour = Math.Clamp(_config.StartHour + delta, 4, 22);
            if (_config.StartHour > _config.EndHour)
                _config.EndHour = _config.StartHour;
        }
        else
        {
            _config.EndHour = Math.Clamp(_config.EndHour + delta, 5, 24);
            if (_config.EndHour < _config.StartHour)
                _config.StartHour = _config.EndHour;
        }

        _applyConfig(_config);
        Game1.playSound("smallSelect");
    }

    private Rectangle GetRowRect(int index)
    {
        int labelX = xPositionOnScreen + 64;
        int labelY = yPositionOnScreen + RowsTopOffset + (index * RowStep);
        return new Rectangle(labelX, labelY, RowsWidth, RowHeight);
    }

    private readonly record struct ToggleRow(TaskKind Kind, string Label, Func<bool> Getter, Action<bool> Setter, bool ShowCost);
}
