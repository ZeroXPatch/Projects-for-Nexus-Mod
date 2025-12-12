using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace ShopSearchBar;

public class ModEntry : Mod
{
    private const int SearchBoxWidth = 360;
    private const int SearchBoxMargin = 16;

    private ShopMenu? activeShop;
    private List<ISalable> originalForSale = new();
    private Dictionary<ISalable, int[]> originalStock = new();
    private TextBox? searchBox;
    private string lastSearch = string.Empty;
    private IKeyboardSubscriber? previousSubscriber;

    private ModConfig Config { get; set; } = new();

    public override void Entry(IModHelper helper)
    {
        this.Config = helper.ReadConfig<ModConfig>();

        helper.Events.Display.Rendered += this.OnRendered;
        helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
        helper.Events.Display.MenuChanged += this.OnMenuChanged;
        helper.Events.Input.ButtonPressed += this.OnButtonPressed;
        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (gmcm is null)
        {
            return;
        }

        gmcm.Register(
            this.ModManifest,
            () => this.Config = new ModConfig(),
            () => this.Helper.WriteConfig(this.Config)
        );

        gmcm.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("gmcm.section.shops"));
        gmcm.AddBoolOption(
            this.ModManifest,
            () => this.Config.EnableForPierre,
            value => this.Config.EnableForPierre = value,
            () => this.Helper.Translation.Get("gmcm.enable.pierre"),
            () => this.Helper.Translation.Get("gmcm.tooltip.pierre")
        );
        gmcm.AddBoolOption(
            this.ModManifest,
            () => this.Config.EnableForClint,
            value => this.Config.EnableForClint = value,
            () => this.Helper.Translation.Get("gmcm.enable.clint"),
            () => this.Helper.Translation.Get("gmcm.tooltip.clint")
        );
        gmcm.AddBoolOption(
            this.ModManifest,
            () => this.Config.EnableForRobin,
            value => this.Config.EnableForRobin = value,
            () => this.Helper.Translation.Get("gmcm.enable.robin"),
            () => this.Helper.Translation.Get("gmcm.tooltip.robin")
        );

        gmcm.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("gmcm.section.behavior"));
        gmcm.AddBoolOption(
            this.ModManifest,
            () => this.Config.FocusSearchOnOpen,
            value => this.Config.FocusSearchOnOpen = value,
            () => this.Helper.Translation.Get("gmcm.focus"),
            () => this.Helper.Translation.Get("gmcm.tooltip.focus")
        );
        gmcm.AddBoolOption(
            this.ModManifest,
            () => this.Config.ShowLabel,
            value => this.Config.ShowLabel = value,
            () => this.Helper.Translation.Get("gmcm.showlabel"),
            () => this.Helper.Translation.Get("gmcm.tooltip.showlabel")
        );
    }

    private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
    {
        this.CleanupSearch();

        if (e.NewMenu is ShopMenu shop && this.IsSupportedShop(shop))
        {
            this.SetupSearch(shop);
        }
    }

    private bool IsSupportedShop(ShopMenu menu)
    {
        string? ownerName = menu.portraitPerson?.Name ?? menu.storeContext;
        if (string.IsNullOrWhiteSpace(ownerName))
        {
            return false;
        }

        return (ownerName.Equals("Pierre", StringComparison.OrdinalIgnoreCase) && this.Config.EnableForPierre)
            || (ownerName.Equals("Clint", StringComparison.OrdinalIgnoreCase) && this.Config.EnableForClint)
            || (ownerName.Equals("Robin", StringComparison.OrdinalIgnoreCase) && this.Config.EnableForRobin);
    }

    private void SetupSearch(ShopMenu menu)
    {
        this.activeShop = menu;
        this.originalForSale = menu.forSale.ToList();
        this.originalStock = menu.itemPriceAndStock.ToDictionary(pair => pair.Key, pair => pair.Value);
        this.lastSearch = string.Empty;

        Texture2D textBoxTexture = Game1.content.Load<Texture2D>("LooseSprites\\textBox");
        this.searchBox = new TextBox(textBoxTexture, null, Game1.smallFont, Game1.textColor)
        {
            Width = SearchBoxWidth,
            Text = string.Empty
        };
        this.UpdateSearchBoxPosition();

        this.previousSubscriber = Game1.keyboardDispatcher.Subscriber;
        if (this.Config.FocusSearchOnOpen)
        {
            Game1.keyboardDispatcher.Subscriber = this.searchBox;
            this.searchBox.SelectMe();
        }

        this.ApplyFilter(string.Empty);
    }

    private void CleanupSearch()
    {
        if (this.searchBox != null && Game1.keyboardDispatcher.Subscriber == this.searchBox)
        {
            Game1.keyboardDispatcher.Subscriber = this.previousSubscriber;
        }

        this.searchBox = null;
        this.activeShop = null;
        this.originalForSale.Clear();
        this.originalStock.Clear();
        this.lastSearch = string.Empty;
        this.previousSubscriber = null;
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady || this.activeShop == null || this.searchBox == null)
        {
            return;
        }

        this.UpdateSearchBoxPosition();

        if (!string.Equals(this.searchBox.Text, this.lastSearch, StringComparison.Ordinal))
        {
            this.lastSearch = this.searchBox.Text;
            this.ApplyFilter(this.lastSearch);
        }
    }

    private void UpdateSearchBoxPosition()
    {
        if (this.activeShop == null || this.searchBox == null)
        {
            return;
        }

        int x = this.activeShop.xPositionOnScreen + ((this.activeShop.width - SearchBoxWidth) / 2);
        int y = this.activeShop.yPositionOnScreen + SearchBoxMargin;

        this.searchBox.X = x;
        this.searchBox.Y = y;
    }

    private void ApplyFilter(string search)
    {
        if (this.activeShop == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(search))
        {
            this.RestoreFullStock();
            return;
        }

        string term = search.Trim();
        List<ISalable> filtered = this.originalForSale
            .Where(item => item.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToList();

        this.activeShop.forSale.Clear();
        this.activeShop.forSale.AddRange(filtered);

        this.activeShop.itemPriceAndStock.Clear();
        foreach (ISalable item in filtered)
        {
            if (this.originalStock.TryGetValue(item, out int[]? stock))
            {
                this.activeShop.itemPriceAndStock[item] = stock;
            }
        }

        this.ResetScroll();
    }

    private void RestoreFullStock()
    {
        if (this.activeShop == null)
        {
            return;
        }

        this.activeShop.forSale.Clear();
        this.activeShop.forSale.AddRange(this.originalForSale);

        this.activeShop.itemPriceAndStock.Clear();
        foreach (var pair in this.originalStock)
        {
            this.activeShop.itemPriceAndStock[pair.Key] = pair.Value;
        }

        this.ResetScroll();
    }

    private void ResetScroll()
    {
        if (this.activeShop == null)
        {
            return;
        }

        this.activeShop.currentItemIndex = 0;
        this.activeShop.SetScrollBarToCurrentIndex();
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (this.searchBox == null || this.activeShop == null)
        {
            return;
        }

        if (e.Button is SButton.MouseLeft)
        {
            Point clickPoint = Game1.getMousePosition();
            Rectangle bounds = new(this.searchBox.X, this.searchBox.Y, this.searchBox.Width, this.searchBox.Height);
            if (bounds.Contains(clickPoint))
            {
                Game1.keyboardDispatcher.Subscriber = this.searchBox;
                this.searchBox.SelectMe();
            }

            this.searchBox.Update(clickPoint.X, clickPoint.Y);
        }
    }

    private void OnRendered(object? sender, RenderedEventArgs e)
    {
        if (this.searchBox == null || this.activeShop == null)
        {
            return;
        }

        SpriteBatch spriteBatch = e.SpriteBatch;

        if (this.Config.ShowLabel)
        {
            Vector2 labelPosition = new(this.searchBox.X, this.searchBox.Y - 32);
            spriteBatch.DrawString(Game1.smallFont, this.Helper.Translation.Get("search.label"), labelPosition, Game1.textColor);
        }

        this.searchBox.Draw(spriteBatch);
    }
}

public class ModConfig
{
    public bool EnableForPierre { get; set; } = true;

    public bool EnableForClint { get; set; } = true;

    public bool EnableForRobin { get; set; } = true;

    public bool FocusSearchOnOpen { get; set; } = true;

    public bool ShowLabel { get; set; } = true;
}

public interface IGenericModConfigMenuApi
{
    void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);

    void AddSectionTitle(IManifest mod, Func<string> text, Func<string>? tooltip = null);

    void AddBoolOption(
        IManifest mod,
        Func<bool> getValue,
        Action<bool> setValue,
        Func<string> name,
        Func<string>? tooltip = null,
        string? fieldId = null);
}
