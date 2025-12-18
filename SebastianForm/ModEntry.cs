using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Tools;

namespace SebastianForm;

public sealed class ModConfig
{
    public bool Enabled { get; set; } = true;

    public KeybindList ToggleKey { get; set; } = new(SButton.K);

    public bool HideOverlayInMenus { get; set; } = true;

    public bool DrawAboveFarmer { get; set; } = true;

    public bool LocalOnlyInMultiplayer { get; set; } = true;

    public float OverlayOpacity { get; set; } = 1f;

    public float OffsetX { get; set; } = 0f;

    public float OffsetY { get; set; } = -12f;

    public bool DebugLogging { get; set; }
}

public sealed class ModEntry : Mod
{
    private const float AnimationIntervalMs = 150f;

    private ModConfig Config = new();
    private bool isFormActive;
    private Texture2D? sebastianTexture;
    private int animationFrame;
    private double animationTimer;

    public override void Entry(IModHelper helper)
    {
        this.Config = helper.ReadConfig<ModConfig>();
        this.isFormActive = this.Config.Enabled;

        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
        helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
        helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
        helper.Events.Input.ButtonsChanged += this.OnButtonsChanged;
        helper.Events.Display.RenderedWorld += this.OnRenderedWorld;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        this.RegisterConfigMenu();
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        this.isFormActive = this.Config.Enabled;
        this.animationFrame = 0;
        this.animationTimer = 0;
        this.LoadSebastianTexture();
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        this.animationFrame = 0;
        this.animationTimer = 0;
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady || !this.isFormActive || this.sebastianTexture is null)
        {
            return;
        }

        Farmer? farmer = Game1.player;
        if (farmer is null)
        {
            return;
        }

        bool isMoving = farmer.xVelocity != 0f || farmer.yVelocity != 0f || farmer.movementDirections.Count > 0;
        if (!isMoving)
        {
            this.animationFrame = 0;
            this.animationTimer = 0;
            return;
        }

        this.animationTimer += e.GameTime.ElapsedGameTime.TotalMilliseconds;
        if (this.animationTimer >= AnimationIntervalMs)
        {
            this.animationTimer %= AnimationIntervalMs;
            this.animationFrame = (this.animationFrame + 1) % 4;
        }
    }

    private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
    {
        if (!Context.IsWorldReady)
        {
            return;
        }

        if (this.Config.ToggleKey.JustPressed())
        {
            this.isFormActive = !this.isFormActive;
            this.Config.Enabled = this.isFormActive;
            this.Helper.WriteConfig(this.Config);

            string messageKey = this.isFormActive ? "hud.enabled" : "hud.disabled";
            Game1.showGlobalMessage(this.Helper.Translation.Get(messageKey));
        }
    }

    private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
    {
        if (!Context.IsWorldReady || !this.isFormActive || this.sebastianTexture is null)
        {
            return;
        }

        IEnumerable<Farmer> farmers = this.Config.LocalOnlyInMultiplayer ? new[] { Game1.player } : Game1.getAllFarmers();
        foreach (Farmer? farmer in farmers)
        {
            if (farmer is null)
            {
                continue;
            }

            if (this.ShouldFallback(farmer, out string reason))
            {
                if (this.Config.DebugLogging && Game1.ticks % 30 == 0)
                {
                    this.Monitor.Log($"Skipping Sebastian overlay for {farmer.Name}: {reason}", LogLevel.Trace);
                }

                continue;
            }

            this.DrawSebastianOverlay(farmer, e);
        }
    }

    private void DrawSebastianOverlay(Farmer farmer, RenderedWorldEventArgs e)
    {
        if (this.sebastianTexture is null)
        {
            return;
        }

        int directionRow = farmer.FacingDirection switch
        {
            Game1.up => 3,
            Game1.right => 1,
            Game1.down => 0,
            _ => 2
        };

        bool isMoving = farmer.xVelocity != 0f || farmer.yVelocity != 0f || farmer.movementDirections.Count > 0;
        int frameIndex = isMoving ? this.animationFrame : 0;
        Rectangle sourceRect = new(frameIndex * 16, directionRow * 32, 16, 32);

        Vector2 drawPosition = this.GetDrawPosition(farmer);
        float layerDepth = Math.Max(0f, farmer.getStandingY() / 10000f + (this.Config.DrawAboveFarmer ? 0.00011f : -0.00011f));
        float opacity = MathHelper.Clamp(this.Config.OverlayOpacity, 0f, 1f);
        Color tint = Color.White * opacity;

        e.SpriteBatch.Draw(
            this.sebastianTexture,
            drawPosition,
            sourceRect,
            tint,
            0f,
            Vector2.Zero,
            Game1.pixelZoom,
            SpriteEffects.None,
            layerDepth);
    }

    private Vector2 GetDrawPosition(Farmer farmer)
    {
        Vector2 basePosition = Game1.GlobalToLocal(Game1.viewport, farmer.Position + new Vector2(-32f, -96f));
        return new Vector2(basePosition.X + this.Config.OffsetX, basePosition.Y + this.Config.OffsetY);
    }

    private bool ShouldFallback(Farmer farmer, out string reason)
    {
        if (!this.isFormActive)
        {
            reason = "form disabled";
            return true;
        }

        if (Game1.activeClickableMenu != null && this.Config.HideOverlayInMenus)
        {
            reason = "menu open";
            return true;
        }

        if (Game1.eventUp || Game1.CurrentEvent is not null)
        {
            reason = "event active";
            return true;
        }

        if (farmer.UsingTool || !farmer.canMove)
        {
            reason = "tool use or cannot move";
            return true;
        }

        if (farmer.swimming.Value)
        {
            reason = "swimming";
            return true;
        }

        if (farmer.isRidingHorse() || farmer.mount is not null)
        {
            reason = "riding mount";
            return true;
        }

        if (farmer.isEating || farmer.isDrinking)
        {
            reason = "eating or drinking";
            return true;
        }

        if (farmer.CurrentTool is FishingRod rod && (rod.isFishing || rod.isCasting || rod.isTimingCast || rod.isReeling))
        {
            reason = "fishing";
            return true;
        }

        if (farmer.CurrentTool is Slingshot)
        {
            reason = "using slingshot";
            return true;
        }

        if (farmer.isInBed.Value)
        {
            reason = "sleeping";
            return true;
        }

        if (farmer.freezePause > 0)
        {
            reason = "temporarily frozen";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private void RegisterConfigMenu()
    {
        var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (gmcm is null)
        {
            return;
        }

        gmcm.Register(
            this.ModManifest,
            () => this.Config = new ModConfig(),
            () => this.Helper.WriteConfig(this.Config));

        gmcm.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("gmcm.section.general"));

        gmcm.AddBoolOption(
            this.ModManifest,
            () => this.Config.Enabled,
            value =>
            {
                this.Config.Enabled = value;
                this.isFormActive = value;
            },
            () => this.Helper.Translation.Get("gmcm.enabled.name"),
            () => this.Helper.Translation.Get("gmcm.enabled.tooltip"));

        gmcm.AddKeybindList(
            this.ModManifest,
            () => this.Config.ToggleKey,
            value => this.Config.ToggleKey = value,
            () => this.Helper.Translation.Get("gmcm.toggleKey.name"),
            () => this.Helper.Translation.Get("gmcm.toggleKey.tooltip"));

        gmcm.AddBoolOption(
            this.ModManifest,
            () => this.Config.HideOverlayInMenus,
            value => this.Config.HideOverlayInMenus = value,
            () => this.Helper.Translation.Get("gmcm.hideInMenus.name"),
            () => this.Helper.Translation.Get("gmcm.hideInMenus.tooltip"));

        gmcm.AddBoolOption(
            this.ModManifest,
            () => this.Config.DrawAboveFarmer,
            value => this.Config.DrawAboveFarmer = value,
            () => this.Helper.Translation.Get("gmcm.drawAboveFarmer.name"),
            () => this.Helper.Translation.Get("gmcm.drawAboveFarmer.tooltip"));

        gmcm.AddBoolOption(
            this.ModManifest,
            () => this.Config.LocalOnlyInMultiplayer,
            value => this.Config.LocalOnlyInMultiplayer = value,
            () => this.Helper.Translation.Get("gmcm.localOnly.name"),
            () => this.Helper.Translation.Get("gmcm.localOnly.tooltip"));

        gmcm.AddNumberOption(
            this.ModManifest,
            () => (int)(this.Config.OverlayOpacity * 100),
            value => this.Config.OverlayOpacity = MathHelper.Clamp(value / 100f, 0f, 1f),
            () => this.Helper.Translation.Get("gmcm.opacity.name"),
            () => this.Helper.Translation.Get("gmcm.opacity.tooltip"),
            10,
            100,
            5,
            value => string.Format(this.Helper.Translation.Get("gmcm.opacity.format"), value));

        gmcm.AddNumberOption(
            this.ModManifest,
            () => (int)this.Config.OffsetX,
            value => this.Config.OffsetX = value,
            () => this.Helper.Translation.Get("gmcm.offsetX.name"),
            () => this.Helper.Translation.Get("gmcm.offsetX.tooltip"),
            -32,
            32,
            1);

        gmcm.AddNumberOption(
            this.ModManifest,
            () => (int)this.Config.OffsetY,
            value => this.Config.OffsetY = value,
            () => this.Helper.Translation.Get("gmcm.offsetY.name"),
            () => this.Helper.Translation.Get("gmcm.offsetY.tooltip"),
            -32,
            32,
            1);

        gmcm.AddBoolOption(
            this.ModManifest,
            () => this.Config.DebugLogging,
            value => this.Config.DebugLogging = value,
            () => this.Helper.Translation.Get("gmcm.debug.name"),
            () => this.Helper.Translation.Get("gmcm.debug.tooltip"));
    }

    private void LoadSebastianTexture()
    {
        try
        {
            this.sebastianTexture = Game1.content.Load<Texture2D>("Characters/Sebastian");
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"Failed to load Sebastian sprite: {ex.Message}", LogLevel.Error);
            this.sebastianTexture = null;
        }
    }
}

public interface IGenericModConfigMenuApi
{
    void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);

    void AddSectionTitle(IManifest mod, Func<string> text, Func<string>? tooltip = null);

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
        string? fieldId = null);

    void AddBoolOption(
        IManifest mod,
        Func<bool> getValue,
        Action<bool> setValue,
        Func<string> name,
        Func<string>? tooltip = null,
        string? fieldId = null);

    void AddKeybindList(
        IManifest mod,
        Func<KeybindList> getValue,
        Action<KeybindList> setValue,
        Func<string> name,
        Func<string>? tooltip = null,
        string? fieldId = null);
}
