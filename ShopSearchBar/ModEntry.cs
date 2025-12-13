#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ShopSearchBar;

public sealed class ModEntry : Mod
{
    private const int DefaultSearchBoxWidth = 360;
    private const int TopPaddingInsideMenu = 12;

    private ShopMenu? activeShop;
    private List<ISalable> originalForSale = new();
    private Dictionary<ISalable, ItemStockInformation> originalStock = new();

    private TextBox? searchBox;
    private string lastSearch = string.Empty;
    private bool lastFilterHadNoResults;

    private IKeyboardSubscriber? previousSubscriber;

    // Focus reliability: retry focusing a few ticks after open, because menus can steal focus during init.
    private int focusRetryTicksRemaining = 0;

    // If player unfocuses via hotkey, don't immediately re-focus due to KeepSearchFocused.
    private bool keepFocusSuspended = false;

    private ModConfig Config { get; set; } = new();

    public override void Entry(IModHelper helper)
    {
        this.Config = helper.ReadConfig<ModConfig>();

        // Hide-from-GMCM behaviors are always ON (so players aren't confused).
        // Even if config.json has them false from earlier tests, we force true at runtime.
        this.Config.FocusSearchOnOpen = true;
        this.Config.KeepSearchFocused = true;

        helper.Events.Display.MenuChanged += this.OnMenuChanged;
        helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
        helper.Events.Display.RenderedActiveMenu += this.OnRenderedActiveMenu;
        helper.Events.Input.ButtonPressed += this.OnButtonPressed;
        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (gmcm is null)
            return;

        gmcm.Register(
            this.ModManifest,
            () =>
            {
                this.Config = new ModConfig();

                // Force these ON (hidden from GMCM).
                this.Config.FocusSearchOnOpen = true;
                this.Config.KeepSearchFocused = true;
            },
            () => this.Helper.WriteConfig(this.Config)
        );

        gmcm.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("gmcm.section.shops"));

        gmcm.AddBoolOption(
            this.ModManifest,
            () => this.Config.EnableForPierre,
            v => this.Config.EnableForPierre = v,
            () => this.Helper.Translation.Get("gmcm.enable.pierre"),
            () => this.Helper.Translation.Get("gmcm.tooltip.pierre")
        );

        gmcm.AddBoolOption(
            this.ModManifest,
            () => this.Config.EnableForClint,
            v => this.Config.EnableForClint = v,
            () => this.Helper.Translation.Get("gmcm.enable.clint"),
            () => this.Helper.Translation.Get("gmcm.tooltip.clint")
        );

        gmcm.AddBoolOption(
            this.ModManifest,
            () => this.Config.EnableForRobin,
            v => this.Config.EnableForRobin = v,
            () => this.Helper.Translation.Get("gmcm.enable.robin"),
            () => this.Helper.Translation.Get("gmcm.tooltip.robin")
        );

        gmcm.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("gmcm.section.custom"));

        gmcm.AddTextOption(
            this.ModManifest,
            () => this.Config.AllowedShopTokens,
            v => this.Config.AllowedShopTokens = v ?? string.Empty,
            () => this.Helper.Translation.Get("gmcm.allowedtokens"),
            () => this.Helper.Translation.Get("gmcm.tooltip.allowedtokens")
        );

        gmcm.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("gmcm.section.position"));

        gmcm.AddNumberOption(
            this.ModManifest,
            () => this.Config.SearchOffsetX,
            v => this.Config.SearchOffsetX = v,
            () => this.Helper.Translation.Get("gmcm.offsetx"),
            () => this.Helper.Translation.Get("gmcm.tooltip.offsetx"),
            min: -500,
            max: 500,
            interval: 1
        );

        gmcm.AddNumberOption(
            this.ModManifest,
            () => this.Config.SearchOffsetY,
            v => this.Config.SearchOffsetY = v,
            () => this.Helper.Translation.Get("gmcm.offsety"),
            () => this.Helper.Translation.Get("gmcm.tooltip.offsety"),
            min: -500,
            max: 500,
            interval: 1
        );

        gmcm.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("gmcm.section.hotkeys"));

        gmcm.AddKeybindList(
            this.ModManifest,
            () => this.Config.FocusSearchKey,
            v => this.Config.FocusSearchKey = v,
            () => this.Helper.Translation.Get("gmcm.focuskey"),
            () => this.Helper.Translation.Get("gmcm.tooltip.focuskey")
        );

        gmcm.AddBoolOption(
            this.ModManifest,
            () => this.Config.ForceAttachOnHotkey,
            v => this.Config.ForceAttachOnHotkey = v,
            () => this.Helper.Translation.Get("gmcm.forceattach"),
            () => this.Helper.Translation.Get("gmcm.tooltip.forceattach")
        );

        // NOTE: "Focus search box on open" + "Keep search focused" are intentionally NOT shown in GMCM.
    }

    private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
    {
        this.CleanupSearch();

        if (e.NewMenu is ShopMenu shop && this.IsSupportedShop(shop))
            this.SetupSearch(shop, reason: "auto");
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady || Game1.player is null)
            return;

        if (this.activeShop is null || this.searchBox is null)
            return;

        if (!ReferenceEquals(Game1.activeClickableMenu, this.activeShop))
            return;

        // Retry focus for a short window after open.
        if (this.focusRetryTicksRemaining > 0)
        {
            this.FocusSearchBox();
            this.focusRetryTicksRemaining--;
        }

        // Keep focus if enabled (unless user suspended via hotkey).
        if (this.Config.KeepSearchFocused && !this.keepFocusSuspended)
            this.FocusSearchBox();

        this.searchBox.Update();
        this.UpdateSearchBoxPosition();

        if (!string.Equals(this.searchBox.Text, this.lastSearch, StringComparison.Ordinal))
        {
            this.lastSearch = this.searchBox.Text;
            this.ApplyFilter(this.lastSearch);
        }
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!Context.IsWorldReady || Game1.player is null)
            return;

        if (this.Config.FocusSearchKey.JustPressed())
        {
            this.Helper.Input.Suppress(e.Button);

            if (Game1.activeClickableMenu is not ShopMenu shop)
                return;

            // Attach if needed
            if (this.activeShop is null || !ReferenceEquals(this.activeShop, shop))
            {
                if (!this.Config.ForceAttachOnHotkey)
                {
                    this.ShowShopTokenHelp(shop);
                    Game1.addHUDMessage(new HUDMessage(this.Helper.Translation.Get("hud.notattached"), HUDMessage.error_type));
                    Game1.playSound("cancel");
                    return;
                }

                this.SetupSearch(shop, reason: "hotkey-force");
                this.ShowShopTokenHelp(shop);
            }

            if (this.searchBox is null)
                return;

            // Toggle focus
            if (ReferenceEquals(Game1.keyboardDispatcher.Subscriber, this.searchBox))
            {
                Game1.keyboardDispatcher.Subscriber = this.previousSubscriber;
                this.keepFocusSuspended = true; // don't instantly re-focus
                Game1.playSound("bigDeSelect");
            }
            else
            {
                this.keepFocusSuspended = false;
                this.FocusSearchBox();
                Game1.playSound("bigSelect");

                if (this.lastFilterHadNoResults)
                {
                    Game1.addHUDMessage(new HUDMessage(this.Helper.Translation.Get("hud.noresults"), HUDMessage.error_type));
                    Game1.playSound("cancel");
                }
            }

            return;
        }

        // Click to focus still supported
        if (this.searchBox is null || this.activeShop is null)
            return;

        if (!ReferenceEquals(Game1.activeClickableMenu, this.activeShop))
            return;

        if (e.Button is SButton.MouseLeft)
        {
            Point clickPoint = Game1.getMousePosition();
            Rectangle bounds = new(this.searchBox.X, this.searchBox.Y, this.searchBox.Width, this.searchBox.Height);

            if (bounds.Contains(clickPoint))
            {
                this.keepFocusSuspended = false;
                this.FocusSearchBox();
                Game1.playSound("bigSelect");
            }
        }
    }

    private void OnRenderedActiveMenu(object? sender, RenderedActiveMenuEventArgs e)
    {
        if (!Context.IsWorldReady || Game1.player is null)
            return;

        if (this.searchBox is null || this.activeShop is null)
            return;

        if (!ReferenceEquals(Game1.activeClickableMenu, this.activeShop))
            return;

        this.searchBox.Draw(e.SpriteBatch);
    }

    private void SetupSearch(ShopMenu menu, string reason)
    {
        this.activeShop = menu;
        this.originalForSale = menu.forSale.ToList();
        this.originalStock = menu.itemPriceAndStock.ToDictionary(pair => pair.Key, pair => pair.Value);

        this.lastSearch = string.Empty;
        this.lastFilterHadNoResults = false;

        Texture2D textBoxTexture = Game1.content.Load<Texture2D>("LooseSprites\\textBox");
        this.searchBox = new TextBox(textBoxTexture, null, Game1.smallFont, Game1.textColor)
        {
            Width = DefaultSearchBoxWidth,
            Text = string.Empty
        };

        this.UpdateSearchBoxPosition();

        this.previousSubscriber = Game1.keyboardDispatcher.Subscriber;

        this.keepFocusSuspended = false;

        if (this.Config.FocusSearchOnOpen || this.Config.KeepSearchFocused)
        {
            this.FocusSearchBox();
            this.focusRetryTicksRemaining = 12;
        }
        else
        {
            this.focusRetryTicksRemaining = 0;
        }

        this.Monitor.Log($"ShopSearchBar attached ({reason}). Token: {this.GetShopIdentityToken(menu)}", LogLevel.Info);

        this.ApplyFilter(string.Empty);
    }

    private void CleanupSearch()
    {
        if (this.searchBox != null && ReferenceEquals(Game1.keyboardDispatcher.Subscriber, this.searchBox))
            Game1.keyboardDispatcher.Subscriber = this.previousSubscriber;

        this.searchBox = null;
        this.activeShop = null;
        this.originalForSale.Clear();
        this.originalStock.Clear();
        this.lastSearch = string.Empty;
        this.lastFilterHadNoResults = false;
        this.previousSubscriber = null;

        this.focusRetryTicksRemaining = 0;
        this.keepFocusSuspended = false;
    }

    private void FocusSearchBox()
    {
        if (this.searchBox is null)
            return;

        if (!ReferenceEquals(Game1.keyboardDispatcher.Subscriber, this.searchBox))
            Game1.keyboardDispatcher.Subscriber = this.searchBox;

        this.searchBox.SelectMe();
    }

    private void UpdateSearchBoxPosition()
    {
        if (this.activeShop is null || this.searchBox is null)
            return;

        int baseY = this.activeShop.yPositionOnScreen + TopPaddingInsideMenu + this.Config.SearchOffsetY;

        int baseX;
        if (this.TryGetFirstForSaleRowBounds(this.activeShop, out Rectangle rowBounds))
            baseX = rowBounds.X + this.Config.SearchOffsetX;
        else
            baseX = (this.activeShop.xPositionOnScreen + 64) + this.Config.SearchOffsetX;

        int maxRight = this.activeShop.xPositionOnScreen + this.activeShop.width - 16;

        if (this.TryGetUpperRightCloseButtonBounds(this.activeShop, out Rectangle closeBounds))
            maxRight = Math.Min(maxRight, closeBounds.X - 8);

        int maxWidth = Math.Max(200, maxRight - baseX);
        this.searchBox.Width = Math.Min(DefaultSearchBoxWidth, maxWidth);

        this.searchBox.X = baseX;
        this.searchBox.Y = baseY;

        int minY = this.activeShop.yPositionOnScreen + 4;
        int maxY = (this.activeShop.yPositionOnScreen + this.activeShop.height) - this.searchBox.Height - 4;
        if (this.searchBox.Y < minY) this.searchBox.Y = minY;
        if (this.searchBox.Y > maxY) this.searchBox.Y = maxY;
    }

    private void ApplyFilter(string search)
    {
        if (this.activeShop is null)
            return;

        if (string.IsNullOrWhiteSpace(search))
        {
            this.RestoreFullStock();
            this.lastFilterHadNoResults = false;
            return;
        }

        string term = search.Trim();

        List<ISalable> filtered = this.originalForSale
            .Where(item => item.DisplayName.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();

        this.activeShop.forSale.Clear();
        this.activeShop.forSale.AddRange(filtered);

        this.activeShop.itemPriceAndStock.Clear();
        foreach (ISalable item in filtered)
        {
            if (this.originalStock.TryGetValue(item, out ItemStockInformation stock))
                this.activeShop.itemPriceAndStock[item] = stock;
        }

        this.lastFilterHadNoResults = filtered.Count == 0;
        this.ResetScrollBestEffort();
    }

    private void RestoreFullStock()
    {
        if (this.activeShop is null)
            return;

        this.activeShop.forSale.Clear();
        this.activeShop.forSale.AddRange(this.originalForSale);

        this.activeShop.itemPriceAndStock.Clear();
        foreach ((ISalable key, ItemStockInformation value) in this.originalStock)
            this.activeShop.itemPriceAndStock[key] = value;

        this.ResetScrollBestEffort();
    }

    private void ResetScrollBestEffort()
    {
        if (this.activeShop is null)
            return;

        this.activeShop.currentItemIndex = 0;

        this.TryInvokeNoArg(this.activeShop, "SetScrollBarToCurrentIndex");
        this.TryInvokeNoArg(this.activeShop, "setScrollBarToCurrentIndex");
        this.TryInvokeNoArg(this.activeShop, "UpdateScrollBar");
        this.TryInvokeNoArg(this.activeShop, "updateScrollBar");
    }

    private void ShowShopTokenHelp(ShopMenu menu)
    {
        string token = this.GetShopIdentityToken(menu);
        this.Monitor.Log($"ShopSearchBar token (use in AllowedShopTokens): {token}", LogLevel.Info);
        Game1.addHUDMessage(new HUDMessage(this.Helper.Translation.Get("hud.tokenlogged"), HUDMessage.newQuest_type));
    }

    private bool IsSupportedShop(ShopMenu menu)
    {
        string token = this.GetShopIdentityToken(menu);
        if (string.IsNullOrWhiteSpace(token))
            return false;

        foreach (string allow in this.GetAllowedTokens())
        {
            if (token.IndexOf(allow, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        bool hasPierre = token.IndexOf("Pierre", StringComparison.OrdinalIgnoreCase) >= 0;
        bool hasClint = token.IndexOf("Clint", StringComparison.OrdinalIgnoreCase) >= 0;
        bool hasRobin = token.IndexOf("Robin", StringComparison.OrdinalIgnoreCase) >= 0;

        return (hasPierre && this.Config.EnableForPierre)
            || (hasClint && this.Config.EnableForClint)
            || (hasRobin && this.Config.EnableForRobin);
    }

    private IEnumerable<string> GetAllowedTokens()
    {
        if (string.IsNullOrWhiteSpace(this.Config.AllowedShopTokens))
            yield break;

        string[] split = this.Config.AllowedShopTokens
            .Split(new[] { ',', ';', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string raw in split)
        {
            string token = raw.Trim();
            if (!string.IsNullOrWhiteSpace(token))
                yield return token;
        }
    }

    private string GetShopIdentityToken(ShopMenu menu)
    {
        List<string> parts = new();

        object? portraitNpc =
            this.TryGetMemberValue(menu, "portraitPerson") ??
            this.TryGetMemberValue(menu, "PortraitPerson") ??
            this.TryGetMemberValue(menu, "shopKeeper") ??
            this.TryGetMemberValue(menu, "ShopKeeper") ??
            this.TryGetMemberValue(menu, "shopkeeper") ??
            this.TryGetMemberValue(menu, "Shopkeeper");

        if (portraitNpc is NPC npc)
            parts.Add(npc.Name);

        object? storeContext =
            this.TryGetMemberValue(menu, "storeContext") ??
            this.TryGetMemberValue(menu, "StoreContext");

        if (storeContext is string ctx && !string.IsNullOrWhiteSpace(ctx))
            parts.Add(ctx);

        object? shopId =
            this.TryGetMemberValue(menu, "shopId") ??
            this.TryGetMemberValue(menu, "ShopId") ??
            this.TryGetMemberValue(menu, "shopID") ??
            this.TryGetMemberValue(menu, "ShopID");

        if (shopId is string id && !string.IsNullOrWhiteSpace(id))
            parts.Add(id);

        parts.Add(menu.GetType().FullName ?? menu.GetType().Name);

        return string.Join(" | ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private bool TryGetFirstForSaleRowBounds(ShopMenu menu, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;

        object? maybeButtons =
            this.TryGetMemberValue(menu, "forSaleButtons") ??
            this.TryGetMemberValue(menu, "ForSaleButtons") ??
            this.TryGetMemberValue(menu, "forSaleButton") ??
            this.TryGetMemberValue(menu, "ForSaleButton");

        if (maybeButtons is IList list && list.Count > 0 && list[0] is ClickableComponent cc)
        {
            bounds = cc.bounds;
            return true;
        }

        return false;
    }

    private bool TryGetUpperRightCloseButtonBounds(IClickableMenu menu, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;

        object? maybe =
            this.TryGetMemberValue(menu, "upperRightCloseButton") ??
            this.TryGetMemberValue(menu, "UpperRightCloseButton");

        if (maybe is ClickableTextureComponent ctc)
        {
            bounds = ctc.bounds;
            return true;
        }

        return false;
    }

    private object? TryGetMemberValue(object instance, string name)
    {
        Type t = instance.GetType();

        PropertyInfo? prop = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (prop != null && prop.GetIndexParameters().Length == 0)
        {
            try { return prop.GetValue(instance); }
            catch { }
        }

        FieldInfo? field = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null)
        {
            try { return field.GetValue(instance); }
            catch { }
        }

        return null;
    }

    private void TryInvokeNoArg(object instance, string methodName)
    {
        Type t = instance.GetType();
        MethodInfo? method = t.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.DefaultBinder, Type.EmptyTypes, null);
        if (method is null)
            return;

        try { method.Invoke(instance, null); }
        catch { }
    }
}

public sealed class ModConfig
{
    public bool EnableForPierre { get; set; } = true;
    public bool EnableForClint { get; set; } = true;
    public bool EnableForRobin { get; set; } = true;

    public string AllowedShopTokens { get; set; } = "";

    public KeybindList FocusSearchKey { get; set; } = new(SButton.F);
    public bool ForceAttachOnHotkey { get; set; } = true;

    public int SearchOffsetX { get; set; } = 0;
    public int SearchOffsetY { get; set; } = 0;

    // Hidden from GMCM, forced ON at runtime:
    public bool FocusSearchOnOpen { get; set; } = true;
    public bool KeepSearchFocused { get; set; } = true;
}

/// <summary>Minimal GMCM API used by this mod.</summary>
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
        string? fieldId = null
    );

    void AddTextOption(
        IManifest mod,
        Func<string> getValue,
        Action<string> setValue,
        Func<string> name,
        Func<string>? tooltip = null,
        string[]? allowedValues = null,
        Func<string, string>? formatAllowedValue = null,
        string? fieldId = null
    );

    void AddKeybindList(
        IManifest mod,
        Func<KeybindList> getValue,
        Action<KeybindList> setValue,
        Func<string> name,
        Func<string>? tooltip = null,
        string? fieldId = null
    );

    void AddNumberOption(
        IManifest mod,
        Func<int> getValue,
        Action<int> setValue,
        Func<string> name,
        Func<string>? tooltip = null,
        int? min = null,
        int? max = null,
        int? interval = null,
        Func<int, string>? formatValue = null,
        string? fieldId = null
    );
}
