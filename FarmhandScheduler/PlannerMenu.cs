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

        // big comfy menu
        width = 800;
        height = 700;
        xPositionOnScreen = (Game1.uiViewport.Width - width) / 2;
        yPositionOnScreen = (Game1.uiViewport.Height - height) / 2;

        _rows = new List<ToggleRow>
        {
            new(_i18n.Get("menu.row.hire"),    () => _config.HelperEnabled,      v => _config.HelperEnabled = v),
            new(_i18n.Get("menu.row.water"),   () => _config.WaterCrops,         v => _config.WaterCrops = v),
            new(_i18n.Get("menu.row.pet"),     () => _config.PetAnimals,         v => _config.PetAnimals = v),
            new(_i18n.Get("menu.row.feed"),    () => _config.FeedAnimals,        v => _config.FeedAnimals = v),
            new(_i18n.Get("menu.row.harvest"), () => _config.HarvestCrops,       v => _config.HarvestCrops = v),
            new(_i18n.Get("menu.row.lowtier"), () => _config.HarvestLowTierOnly, v => _config.HarvestLowTierOnly = v),
            new(_i18n.Get("menu.row.chests"),  () => _config.OrganizeChests,     v => _config.OrganizeChests = v)
        };

        int top = yPositionOnScreen + 96;

        // work-hours controls in the top-right
        int workX = xPositionOnScreen + width - 260;
        int workY = top - 10;

        _startRect = new Rectangle(workX, workY + 76, 64, 64);
        _endRect = new Rectangle(workX + 90, workY + 76, 64, 64);

        // buttons at the bottom
        _saveRect = new Rectangle(xPositionOnScreen + width - 200, yPositionOnScreen + height - 80, 160, 64);
        _cancelRect = new Rectangle(xPositionOnScreen + 40, yPositionOnScreen + height - 80, 160, 64);
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        base.receiveLeftClick(x, y, playSound);

        // toggle rows
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

        // start hour +/-
        if (_startRect.Contains(x, y))
        {
            AdjustHours(+1, start: true);
            return;
        }

        // end hour +/-
        if (_endRect.Contains(x, y))
        {
            AdjustHours(+1, start: false);
            return;
        }

        // save
        if (_saveRect.Contains(x, y))
        {
            _saveConfig(_config);
            exitThisMenu();
            Game1.playSound("bigSelect");
            return;
        }

        // cancel
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
        // background
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

        // title
        string title = _i18n.Get("menu.title");
        Vector2 titleSize = Game1.dialogueFont.MeasureString(title);
        Game1.spriteBatch.DrawString(
            Game1.dialogueFont,
            title,
            new Vector2(xPositionOnScreen + (width / 2 - titleSize.X / 2), yPositionOnScreen + 32),
            Color.DarkSlateBlue
        );

        int labelX = xPositionOnScreen + 64;
        int labelY = yPositionOnScreen + 96;

        // toggle rows on the left
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

            string toggleText = row.Getter() ? "✔" : "✗";

            Game1.spriteBatch.DrawString(
                Game1.smallFont,
                toggleText,
                new Vector2(rowRect.X + 12, rowRect.Y + 16),
                row.Getter() ? Color.DarkGreen : Color.DarkGray
            );

            Game1.spriteBatch.DrawString(
                Game1.smallFont,
                row.Label,
                new Vector2(rowRect.X + 48, rowRect.Y + 16),
                Color.Black
            );
        }

        // work-hours block on the right
        int workX = xPositionOnScreen + width - 260;
        int workY = labelY - 10;

        string workHoursLabel = _i18n.Get("menu.workhours");
        string startText = string.Format(_i18n.Get("menu.workhours.start"), _config.StartHour);
        string endText = string.Format(_i18n.Get("menu.workhours.end"), _config.EndHour);

        Game1.spriteBatch.DrawString(Game1.smallFont, workHoursLabel, new Vector2(workX - 10, workY - 10), Color.Black);
        Game1.spriteBatch.DrawString(Game1.smallFont, startText, new Vector2(workX - 10, workY - 10 + 26), Color.Black);
        Game1.spriteBatch.DrawString(Game1.smallFont, endText, new Vector2(workX - 10, workY - 10 + 26 + 26), Color.Black);

        drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60), _startRect.X, _startRect.Y, _startRect.Width, _startRect.Height, Color.White);
        drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60), _endRect.X, _endRect.Y, _endRect.Width, _endRect.Height, Color.White);

        Game1.spriteBatch.DrawString(Game1.smallFont, "+/-", new Vector2(_startRect.X + 16, _startRect.Y + 20), Color.DarkSlateGray);
        Game1.spriteBatch.DrawString(Game1.smallFont, "+/-", new Vector2(_endRect.X + 16, _endRect.Y + 20), Color.DarkSlateGray);

        // cost + hint + keybind between rows and buttons
        string cost = string.Format(_i18n.Get("menu.cost"), _config.DailyCost);
        string hint = _i18n.Get("menu.hint");
        string openWith = string.Format(_i18n.Get("menu.openwith"), _config.PlannerMenuKey);

        Game1.spriteBatch.DrawString(Game1.smallFont, cost, new Vector2(labelX, yPositionOnScreen + height - 176), Color.Black);
        Game1.spriteBatch.DrawString(Game1.smallFont, hint, new Vector2(labelX, yPositionOnScreen + height - 146), Color.Gray);
        Game1.spriteBatch.DrawString(Game1.smallFont, openWith, new Vector2(labelX, yPositionOnScreen + height - 116), Color.DarkSlateBlue);

        // bottom buttons
        drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60), _saveRect.X, _saveRect.Y, _saveRect.Width, _saveRect.Height, Color.White);
        drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60), _cancelRect.X, _cancelRect.Y, _cancelRect.Width, _cancelRect.Height, Color.White);

        Game1.spriteBatch.DrawString(Game1.smallFont, _i18n.Get("menu.button.save"), new Vector2(_saveRect.X + 32, _saveRect.Y + 20), Color.DarkGreen);
        Game1.spriteBatch.DrawString(Game1.smallFont, _i18n.Get("menu.button.cancel"), new Vector2(_cancelRect.X + 16, _cancelRect.Y + 20), Color.DarkRed);

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
        int labelY = yPositionOnScreen + 96 + (index * 52);
        return new Rectangle(labelX, labelY, 400, 60);
    }

    private readonly record struct ToggleRow(string Label, Func<bool> Getter, Action<bool> Setter);
}
