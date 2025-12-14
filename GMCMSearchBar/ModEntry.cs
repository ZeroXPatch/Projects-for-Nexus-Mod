using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GenericModConfigMenu; // for the API interface below
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;

namespace GMCMSearchBar;

public sealed class ModEntry : Mod
{
    private ModConfig config = new();
    private IGenericModConfigMenuApi? gmcmApi;
    private Texture2D? textBoxTexture;

    public override void Entry(IModHelper helper)
    {
        this.config = helper.ReadConfig<ModConfig>();
        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        helper.Events.Input.ButtonsChanged += this.OnButtonsChanged;
    }

    private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
    {
        if (!this.config.ToggleKey.JustPressed())
        {
            return;
        }

        if (!this.config.AllowOnTitleScreen && !Context.IsWorldReady)
        {
            this.Monitor.Log("Search is disabled until a save is loaded.", LogLevel.Info);
            return;
        }

        this.OpenSearchMenu();
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        this.gmcmApi = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (this.gmcmApi is null)
        {
            this.Monitor.Log("Generic Mod Config Menu not detected; the search overlay will not open.", LogLevel.Warn);
            return;
        }

        this.gmcmApi.Register(
            this.ModManifest,
            () => this.config = new ModConfig(),
            () => this.Helper.WriteConfig(this.config),
            true
        );
        this.gmcmApi.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("config.title"));
        this.gmcmApi.AddParagraph(this.ModManifest, () => this.Helper.Translation.Get("config.description"));
        this.gmcmApi.AddKeybindList(
            this.ModManifest,
            () => this.config.ToggleKey,
            value => this.config.ToggleKey = value,
            () => this.Helper.Translation.Get("config.toggle"),
            () => this.Helper.Translation.Get("config.toggle.description")
        );
        this.gmcmApi.AddBoolOption(
            this.ModManifest,
            () => this.config.AllowOnTitleScreen,
            value => this.config.AllowOnTitleScreen = value,
            () => this.Helper.Translation.Get("config.openAtTitle"),
            () => this.Helper.Translation.Get("config.openAtTitle.description")
        );
    }

    private void OpenSearchMenu()
    {
        if (this.gmcmApi is null)
        {
            this.Monitor.Log(this.Helper.Translation.Get("menu.gmcmMissing"), LogLevel.Warn);
            return;
        }

        this.textBoxTexture ??= Game1.content.Load<Texture2D>("LooseSprites/textBox");
        List<IManifest> modOptions = this.GetRegisteredMods();
        Func<IManifest, bool> opener = manifest => this.TryOpenModMenu(manifest);
        Game1.activeClickableMenu = new SearchMenu(
            this.Helper,
            this.Monitor,
            opener,
            modOptions,
            this.textBoxTexture,
            Game1.smallFont
        );
    }

    private List<IManifest> GetRegisteredMods()
    {
        // Best case: reflect into GMCM internals to get the list of registered mods.
        try
        {
            if (this.gmcmApi is not null)
            {
                object apiObj = this.gmcmApi;

                // 1) Look for any public property that is IEnumerable<IManifest>
                foreach (var prop in apiObj.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (!prop.CanRead)
                    {
                        continue;
                    }

                    if (typeof(IEnumerable<IManifest>).IsAssignableFrom(prop.PropertyType))
                    {
                        if (prop.GetValue(apiObj) is IEnumerable<IManifest> manifests)
                        {
                            return this.CleanSort(manifests);
                        }
                    }
                }

                // 2) Look for a field/property holding a dictionary keyed by IManifest
                IEnumerable<IManifest>? foundFromDict = TryExtractManifestsFromAnyDictionary(apiObj);
                if (foundFromDict is not null)
                {
                    return this.CleanSort(foundFromDict);
                }

                // 3) Look for a nested manager object, then repeat the dictionary scan on it
                foreach (var field in apiObj.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    object? inner = field.GetValue(apiObj);
                    if (inner is null)
                    {
                        continue;
                    }

                    foundFromDict = TryExtractManifestsFromAnyDictionary(inner);
                    if (foundFromDict is not null)
                    {
                        return this.CleanSort(foundFromDict);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"Failed to reflect GMCM registered mods list; falling back to all mods.\n{ex}", LogLevel.Trace);
        }

        // Fallback: list all installed mods (some won’t be registered with GMCM).
        return this.CleanSort(this.Helper.ModRegistry.GetAll().Select(m => m.Manifest));

        static IEnumerable<IManifest>? TryExtractManifestsFromAnyDictionary(object obj)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            // scan fields
            foreach (var field in obj.GetType().GetFields(flags))
            {
                if (field.GetValue(obj) is null)
                {
                    continue;
                }

                if (TryExtractFromDictionaryObject(field.GetValue(obj)!, out var manifests))
                {
                    return manifests;
                }
            }

            // scan properties
            foreach (var prop in obj.GetType().GetProperties(flags))
            {
                if (!prop.CanRead)
                {
                    continue;
                }

                object? value;
                try
                {
                    value = prop.GetValue(obj);
                }
                catch
                {
                    continue;
                }

                if (value is null)
                {
                    continue;
                }

                if (TryExtractFromDictionaryObject(value, out var manifests))
                {
                    return manifests;
                }
            }

            return null;
        }

        static bool TryExtractFromDictionaryObject(object value, out IEnumerable<IManifest> manifests)
        {
            manifests = Array.Empty<IManifest>();

            // If it's IDictionary<IManifest, T> we can grab Keys.
            var type = value.GetType();
            var idictIface = type
                .GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));

            if (idictIface is null)
            {
                return false;
            }

            var args = idictIface.GetGenericArguments();
            if (args.Length != 2 || args[0] != typeof(IManifest))
            {
                return false;
            }

            var keysProp = idictIface.GetProperty("Keys");
            if (keysProp?.GetValue(value) is IEnumerable<IManifest> keys)
            {
                manifests = keys;
                return true;
            }

            return false;
        }
    }

    private List<IManifest> CleanSort(IEnumerable<IManifest> manifests)
    {
        return manifests
            .Where(m => !string.Equals(m.UniqueID, this.ModManifest.UniqueID, StringComparison.OrdinalIgnoreCase))
            .DistinctBy(m => m.UniqueID, StringComparer.OrdinalIgnoreCase)
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private bool TryOpenModMenu(IManifest manifest)
    {
        if (this.gmcmApi is null)
        {
            this.Monitor.Log(this.Helper.Translation.Get("menu.gmcmMissing"), LogLevel.Warn);
            return false;
        }

        try
        {
            this.gmcmApi.OpenModMenu(manifest);
            this.Monitor.Log(string.Format(this.Helper.Translation.Get("menu.opened"), manifest.Name), LogLevel.Info);
            return true;
        }
        catch (Exception ex)
        {
            this.Monitor.Log(string.Format(this.Helper.Translation.Get("menu.failed"), manifest.Name), LogLevel.Warn);
            this.Monitor.Log($"Failed to open GMCM menu for {manifest.UniqueID}.\n{ex}", LogLevel.Trace);
            return false;
        }
    }
}

public sealed class ModConfig
{
    public KeybindList ToggleKey { get; set; } = new(SButton.F8);

    public bool AllowOnTitleScreen { get; set; } = true;
}

internal sealed class SearchMenu : IClickableMenu
{
    private readonly ITranslationHelper translation;
    private readonly IMonitor monitor;
    private readonly Func<IManifest, bool> openMod;
    private readonly List<IManifest> mods;
    private readonly TextBox searchBox;
    private readonly SpriteFont font;
    private readonly List<IManifest> filtered;
    private int scrollOffset;

    public SearchMenu(
        IModHelper helper,
        IMonitor monitor,
        Func<IManifest, bool> openMod,
        List<IManifest> mods,
        Texture2D textBoxTexture,
        SpriteFont font
    ) : base(Game1.uiViewport.Width / 2 - 450, Game1.uiViewport.Height / 2 - 300, 900, 600)
    {
        this.translation = helper.Translation;
        this.monitor = monitor;
        this.openMod = openMod;
        this.mods = mods;
        this.filtered = new List<IManifest>(mods);
        this.font = font;

        this.searchBox = new TextBox(textBoxTexture, null, this.font, Game1.textColor)
        {
            X = this.xPositionOnScreen + 48,
            Y = this.yPositionOnScreen + 96,
            Width = this.width - 96,
            Text = string.Empty
        };
        this.searchBox.OnTextChanged += (_, _) => this.ApplyFilter();
        Game1.keyboardDispatcher.Subscriber = this.searchBox;
    }

    public override void exitThisMenu(bool playSound = true)
    {
        if (Game1.keyboardDispatcher.Subscriber == this.searchBox)
        {
            Game1.keyboardDispatcher.Subscriber = null;
        }

        base.exitThisMenu(playSound);
    }

    public override void receiveScrollWheelAction(int direction)
    {
        base.receiveScrollWheelAction(direction);
        int itemsPerPage = this.GetItemsPerPage();
        if (this.filtered.Count <= itemsPerPage)
        {
            return;
        }

        this.scrollOffset -= Math.Sign(direction);
        this.scrollOffset = Math.Clamp(this.scrollOffset, 0, Math.Max(0, this.filtered.Count - itemsPerPage));
    }

    public override void receiveKeyPress(Keys key)
    {
        base.receiveKeyPress(key);
        if (key == Keys.Escape)
        {
            this.exitThisMenu(true);
        }
        else if (key == Keys.Enter || key == Keys.Return)
        {
            IManifest? manifest = this.filtered.FirstOrDefault();
            if (manifest is not null)
            {
                this.OpenAndClose(manifest);
            }
        }
    }

    public override void update(GameTime time)
    {
        base.update(time);
        this.searchBox.Update();
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        base.receiveLeftClick(x, y, playSound);
        if (this.searchBox.Bounds.Contains(x, y))
        {
            Game1.keyboardDispatcher.Subscriber = this.searchBox;
            return;
        }

        int index = 0;
        int itemTop = this.yPositionOnScreen + 180;
        int itemsPerPage = this.GetItemsPerPage();
        foreach (IManifest manifest in this.filtered.Skip(this.scrollOffset).Take(itemsPerPage))
        {
            Rectangle row = new(this.xPositionOnScreen + 60, itemTop + index * 64, this.width - 120, 60);
            if (row.Contains(x, y))
            {
                this.OpenAndClose(manifest);
                break;
            }

            index++;
        }
    }

    public override void draw(SpriteBatch b)
    {
        base.draw(b);
        this.drawBackground(b);
        IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60), this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, Color.White, Game1.pixelZoom);

        string title = this.translation.Get("menu.title");
        Vector2 titleSize = this.font.MeasureString(title);
        b.DrawString(
            this.font,
            title,
            new Vector2(this.xPositionOnScreen + (this.width - titleSize.X) / 2f, this.yPositionOnScreen + 32),
            Game1.textColor
        );

        this.searchBox.Draw(b);
        if (string.IsNullOrWhiteSpace(this.searchBox.Text))
        {
            b.DrawString(
                this.font,
                this.translation.Get("menu.placeholder"),
                new Vector2(this.searchBox.X + 12, this.searchBox.Y + 10),
                Color.Gray
            );
        }

        b.DrawString(
            this.font,
            this.translation.Get("menu.subtitle"),
            new Vector2(this.xPositionOnScreen + 60, this.yPositionOnScreen + 150),
            Game1.textColor
        );

        int index = 0;
        int itemsPerPage = this.GetItemsPerPage();
        foreach (IManifest manifest in this.filtered.Skip(this.scrollOffset).Take(itemsPerPage))
        {
            int y = this.yPositionOnScreen + 180 + index * 64;
            Rectangle row = new(this.xPositionOnScreen + 60, y, this.width - 120, 60);
            IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60), row.X, row.Y, row.Width, row.Height, Color.White * 0.35f, Game1.pixelZoom);
            b.DrawString(this.font, manifest.Name, new Vector2(row.X + 16, row.Y + 16), Game1.textColor);
            b.DrawString(this.font, manifest.UniqueID, new Vector2(row.X + 16, row.Y + 36), Color.DarkGray);
            index++;
        }

        if (this.filtered.Count == 0)
        {
            b.DrawString(
                this.font,
                this.translation.Get("menu.noResults"),
                new Vector2(this.xPositionOnScreen + 60, this.yPositionOnScreen + 200),
                Game1.textColor
            );
        }

        this.drawMouse(b);
    }

    private void ApplyFilter()
    {
        string term = this.searchBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(term))
        {
            this.filtered.Clear();
            this.filtered.AddRange(this.mods);
        }
        else
        {
            this.filtered.Clear();
            this.filtered.AddRange(
                this.mods.Where(manifest => manifest.Name.Contains(term, StringComparison.OrdinalIgnoreCase) || manifest.UniqueID.Contains(term, StringComparison.OrdinalIgnoreCase))
            );
        }

        this.scrollOffset = 0;
    }

    private void OpenAndClose(IManifest manifest)
    {
        if (Game1.keyboardDispatcher.Subscriber == this.searchBox)
        {
            Game1.keyboardDispatcher.Subscriber = null;
        }

        bool opened = this.openMod.Invoke(manifest);
        if (opened)
        {
            this.exitThisMenu(true);
        }
        else
        {
            Game1.playSound("cancel");
        }
    }

    private int GetItemsPerPage()
    {
        return Math.Max(1, (this.height - 220) / 64);
    }
}

namespace GenericModConfigMenu
{
    public interface IGenericModConfigMenuApi
    {
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);

        void AddSectionTitle(IManifest mod, Func<string> text, Func<string>? tooltip = null);

        void AddParagraph(IManifest mod, Func<string> text);

        void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue, Func<string> name, Func<string>? tooltip = null, string? fieldId = null);

        void AddKeybindList(IManifest mod, Func<KeybindList> getValue, Action<KeybindList> setValue, Func<string> name, Func<string>? tooltip = null, string? fieldId = null);

        void OpenModMenu(IManifest mod);
    }
}
