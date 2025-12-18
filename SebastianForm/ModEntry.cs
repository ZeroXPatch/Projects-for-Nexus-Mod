using System;
using System.Collections.Generic;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;

namespace SebastianForm;

public sealed class ModConfig
{
    public bool Enabled { get; set; } = true;

    public KeybindList ToggleKey { get; set; } = new(SButton.K);
    public KeybindList NextCharacterKey { get; set; } = new(SButton.L);

    public string CharacterName { get; set; } = "Sebastian";

    public bool LocalOnlyInMultiplayer { get; set; } = true;

    public float Opacity { get; set; } = 1f;

    public float OffsetX { get; set; } = 0f;
    public float OffsetY { get; set; } = 0f;

    public bool DebugLogging { get; set; } = false;
}

public sealed class ModEntry : Mod
{
    internal static ModEntry? Instance;

    private ModConfig Config = new();
    private bool isActive;

    private readonly Dictionary<string, Texture2D> textureCache = new(StringComparer.OrdinalIgnoreCase);
    private Texture2D? currentTexture;
    private string currentCharacter = "Sebastian";

    private const int FrameW = 16;
    private const int FrameH = 32;
    private const int AnimIntervalTicks = 9;

    private static readonly List<string> BuiltInCharacters = new()
    {
        "Sebastian",
        "Haley",
        "Abigail",
        "Sam",
        "Alex",
        "Emily",
        "Penny",
        "Maru",
        "Leah",
        "Harvey",
        "Shane",
        "Elliott",
        "Caroline",
        "Clint",
        "Demetrius",
        "Evelyn",
        "George",
        "Gus",
        "Jas",
        "Jodi",
        "Kent",
        "Lewis",
        "Linus",
        "Marnie",
        "Pam",
        "Pierre",
        "Robin",
        "Sandy",
        "Vincent",
        "Willy",
        "Wizard",
        "Bear",
        "Morris"
    };

    public override void Entry(IModHelper helper)
    {
        Instance = this;

        this.Config = helper.ReadConfig<ModConfig>();
        this.isActive = this.Config.Enabled;
        this.currentCharacter = string.IsNullOrWhiteSpace(this.Config.CharacterName) ? "Sebastian" : this.Config.CharacterName.Trim();

        helper.Events.GameLoop.GameLaunched += (_, _) => this.RegisterGmcm();
        helper.Events.GameLoop.SaveLoaded += (_, _) => this.ReloadCharacterTexture();
        helper.Events.GameLoop.ReturnedToTitle += (_, _) =>
        {
            this.currentTexture = null;
            this.textureCache.Clear();
        };
        helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
        helper.Events.Input.ButtonsChanged += this.OnButtonsChanged;

        var harmony = new Harmony(this.ModManifest.UniqueID);
        this.PatchDraw(harmony);

        this.Monitor.Log("Wizard’s Wardrobe: NPC Forms loaded. (If you don’t transform, check SMAPI log for patch messages.)", LogLevel.Trace);
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        // Ensure texture loads once the world is ready, even if SaveLoaded fired before Context.IsWorldReady.
        if (Context.IsWorldReady && this.currentTexture is null)
            this.ReloadCharacterTexture();
    }

    private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
    {
        if (!Context.IsWorldReady)
            return;

        if (this.Config.ToggleKey.JustPressed())
        {
            this.isActive = !this.isActive;
            this.Config.Enabled = this.isActive;
            this.Helper.WriteConfig(this.Config);

            Game1.showGlobalMessage(this.isActive ? "Wizard’s Wardrobe: ON" : "Wizard’s Wardrobe: OFF");
        }

        if (this.Config.NextCharacterKey.JustPressed())
        {
            this.CycleCharacter(+1);
        }
    }

    private void CycleCharacter(int delta)
    {
        int idx = BuiltInCharacters.FindIndex(n => n.Equals(this.currentCharacter, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) idx = 0;

        idx = (idx + delta) % BuiltInCharacters.Count;
        if (idx < 0) idx += BuiltInCharacters.Count;

        this.SetCharacter(BuiltInCharacters[idx], showHud: true);
    }

    private void SetCharacter(string name, bool showHud)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        this.currentCharacter = name.Trim();
        this.Config.CharacterName = this.currentCharacter;
        this.Helper.WriteConfig(this.Config);

        this.ReloadCharacterTexture();

        if (showHud && Context.IsWorldReady)
            Game1.showGlobalMessage($"Wardrobe Form: {this.currentCharacter}");
    }

    private void ReloadCharacterTexture()
    {
        this.currentTexture = null;

        if (!Context.IsWorldReady)
            return;

        string name = string.IsNullOrWhiteSpace(this.currentCharacter) ? "Sebastian" : this.currentCharacter;

        if (this.textureCache.TryGetValue(name, out var cached))
        {
            this.currentTexture = cached;
            return;
        }

        try
        {
            var tex = Game1.content.Load<Texture2D>($"Characters/{name}");
            this.textureCache[name] = tex;
            this.currentTexture = tex;

            if (this.Config.DebugLogging)
                this.Monitor.Log($"Loaded Characters/{name}", LogLevel.Trace);
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"Failed to load Characters/{name}. Falling back to Sebastian. Error: {ex.Message}", LogLevel.Warn);

            try
            {
                var tex = Game1.content.Load<Texture2D>("Characters/Sebastian");
                this.textureCache["Sebastian"] = tex;
                this.currentTexture = tex;

                this.currentCharacter = "Sebastian";
                this.Config.CharacterName = "Sebastian";
                this.Helper.WriteConfig(this.Config);
            }
            catch (Exception ex2)
            {
                this.Monitor.Log($"Failed to load fallback Characters/Sebastian too: {ex2}", LogLevel.Error);
                this.currentTexture = null;
            }
        }
    }

    private bool ShouldReplace(Farmer farmer)
    {
        if (!this.isActive)
            return false;

        // Avoid invisible player if texture isn't available.
        if (this.currentTexture is null)
            return false;

        if (this.Config.LocalOnlyInMultiplayer && !ReferenceEquals(farmer, Game1.player))
            return false;

        return true;
    }

    // -------------------------
    // Harmony patches (THIS is the key fix)
    // -------------------------
    private void PatchDraw(Harmony harmony)
    {
        // 1) Patch Farmer.draw(SpriteBatch) (implemented)
        var farmerDraw = AccessTools.Method(typeof(Farmer), "draw", new[] { typeof(SpriteBatch) });
        if (farmerDraw is not null)
        {
            harmony.Patch(farmerDraw, prefix: new HarmonyMethod(typeof(ModEntry), nameof(FarmerDraw_Prefix)));
            if (this.Config.DebugLogging)
                this.Monitor.Log("Patched Farmer.draw(SpriteBatch)", LogLevel.Trace);
        }
        else
        {
            this.Monitor.Log("Could not find Farmer.draw(SpriteBatch) to patch.", LogLevel.Warn);
        }

        // 2) Safety net: patch Character.draw(SpriteBatch, float) (declared). Some code paths use alpha draws.
        var charDrawAlpha = AccessTools.Method(typeof(Character), "draw", new[] { typeof(SpriteBatch), typeof(float) });
        if (charDrawAlpha is not null)
        {
            harmony.Patch(charDrawAlpha, prefix: new HarmonyMethod(typeof(ModEntry), nameof(CharacterDrawAlpha_Prefix)));
            if (this.Config.DebugLogging)
                this.Monitor.Log("Patched Character.draw(SpriteBatch, float)", LogLevel.Trace);
        }
    }

    public static bool FarmerDraw_Prefix(Farmer __instance, SpriteBatch b)
    {
        var inst = Instance;
        if (inst is null)
            return true;

        if (inst.currentTexture is null && Context.IsWorldReady)
            inst.ReloadCharacterTexture();

        if (!inst.ShouldReplace(__instance))
            return true;

        inst.DrawReplacement(__instance, b, alpha: 1f);
        return false; // skip original farmer sprite
    }

    public static bool CharacterDrawAlpha_Prefix(Character __instance, SpriteBatch b, float alpha)
    {
        var inst = Instance;
        if (inst is null)
            return true;

        // Only intercept farmer alpha-draws (do NOT affect NPCs/monsters)
        if (__instance is not Farmer farmer)
            return true;

        if (inst.currentTexture is null && Context.IsWorldReady)
            inst.ReloadCharacterTexture();

        if (!inst.ShouldReplace(farmer))
            return true;

        inst.DrawReplacement(farmer, b, alpha);
        return false;
    }

    private void DrawReplacement(Farmer farmer, SpriteBatch b, float alpha)
    {
        if (this.currentTexture is null)
            return;

        // Vanilla NPC row order: down=0, right=1, up=2, left=3
        int row = farmer.FacingDirection switch
        {
            Game1.down => 0,
            Game1.right => 1,
            Game1.up => 2,
            Game1.left => 3,
            _ => 0
        };

        bool moving = farmer.xVelocity != 0f || farmer.yVelocity != 0f || farmer.movementDirections.Count > 0;
        int frame = moving ? (int)((Game1.ticks / AnimIntervalTicks) % 4) : 0;

        Rectangle src = new(frame * FrameW, row * FrameH, FrameW, FrameH);

        // Align to farmer feet using bounding box
        Rectangle bbox = farmer.GetBoundingBox();

        float worldX = bbox.Center.X - (FrameW * Game1.pixelZoom) / 2f;
        float worldY = bbox.Bottom - (FrameH * Game1.pixelZoom);

        Vector2 screenPos = Game1.GlobalToLocal(Game1.viewport, new Vector2(worldX, worldY));
        screenPos.X += this.Config.OffsetX;
        screenPos.Y += this.Config.OffsetY;

        // Correct depth so trees/fences can cover you properly
        float layerDepth = Math.Max(0f, bbox.Bottom / 10000f);

        DrawShadow(b, bbox, layerDepth - 0.00001f);

        float opacity = MathHelper.Clamp(this.Config.Opacity, 0f, 1f) * MathHelper.Clamp(alpha, 0f, 1f);
        Color tint = Color.White * opacity;

        b.Draw(
            this.currentTexture,
            screenPos,
            src,
            tint,
            0f,
            Vector2.Zero,
            Game1.pixelZoom,
            SpriteEffects.None,
            layerDepth
        );
    }

    private static void DrawShadow(SpriteBatch b, Rectangle bbox, float layerDepth)
    {
        float shadowWorldX = bbox.Center.X - (12f * Game1.pixelZoom) / 2f;
        float shadowWorldY = bbox.Bottom - (4f * Game1.pixelZoom);

        Vector2 shadowPos = Game1.GlobalToLocal(Game1.viewport, new Vector2(shadowWorldX, shadowWorldY));

        b.Draw(
            Game1.shadowTexture,
            shadowPos,
            new Rectangle(0, 0, 12, 4),
            Color.White,
            0f,
            Vector2.Zero,
            Game1.pixelZoom,
            SpriteEffects.None,
            Math.Max(0f, layerDepth)
        );
    }

    // -------------------------
    // GMCM
    // -------------------------
    private void RegisterGmcm()
    {
        var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (gmcm is null)
        {
            this.Monitor.Log("GMCM not found. Install 'Generic Mod Config Menu' to configure in-game.", LogLevel.Trace);
            return;
        }

        gmcm.Register(
            this.ModManifest,
            reset: () =>
            {
                this.Config = new ModConfig();
                this.isActive = this.Config.Enabled;
                this.currentCharacter = this.Config.CharacterName;
                this.Helper.WriteConfig(this.Config);
                this.ReloadCharacterTexture();
            },
            save: () =>
            {
                this.Helper.WriteConfig(this.Config);
                this.isActive = this.Config.Enabled;
                this.currentCharacter = this.Config.CharacterName;
                this.ReloadCharacterTexture();
            }
        );

        gmcm.AddSectionTitle(this.ModManifest, () => "Wizard’s Wardrobe: NPC Forms");

        gmcm.AddBoolOption(
            this.ModManifest,
            () => this.Config.Enabled,
            v =>
            {
                this.Config.Enabled = v;
                this.isActive = v;
                this.Helper.WriteConfig(this.Config);
            },
            () => "Enabled",
            () => "Hide your farmer and draw the selected vanilla NPC sprite instead."
        );

        gmcm.AddKeybindList(
            this.ModManifest,
            () => this.Config.ToggleKey,
            v =>
            {
                this.Config.ToggleKey = v;
                this.Helper.WriteConfig(this.Config);
            },
            () => "Toggle key",
            () => "Hotkey to enable/disable the form."
        );

        gmcm.AddKeybindList(
            this.ModManifest,
            () => this.Config.NextCharacterKey,
            v =>
            {
                this.Config.NextCharacterKey = v;
                this.Helper.WriteConfig(this.Config);
            },
            () => "Next character key",
            () => "Hotkey to cycle the character list."
        );

        gmcm.AddTextOption(
            this.ModManifest,
            () => this.Config.CharacterName,
            v =>
            {
                this.Config.CharacterName = v;
                this.currentCharacter = v;
                this.Helper.WriteConfig(this.Config);
                this.ReloadCharacterTexture();
            },
            () => "Character",
            () => "Choose a vanilla NPC sprite sheet (Characters/<Name>).",
            allowedValues: BuiltInCharacters.ToArray(),
            formatAllowedValue: s => s
        );

        gmcm.AddBoolOption(
            this.ModManifest,
            () => this.Config.LocalOnlyInMultiplayer,
            v =>
            {
                this.Config.LocalOnlyInMultiplayer = v;
                this.Helper.WriteConfig(this.Config);
            },
            () => "Local-only (multiplayer)",
            () => "If enabled, only your local player is replaced on your client."
        );

        gmcm.AddNumberOption(
            this.ModManifest,
            () => (int)(this.Config.Opacity * 100f),
            v =>
            {
                this.Config.Opacity = MathHelper.Clamp(v / 100f, 0f, 1f);
                this.Helper.WriteConfig(this.Config);
            },
            () => "Opacity",
            () => "Sprite opacity (100% = fully visible).",
            min: 10, max: 100, interval: 5,
            formatValue: v => $"{v}%"
        );

        gmcm.AddNumberOption(
            this.ModManifest,
            () => (int)this.Config.OffsetX,
            v =>
            {
                this.Config.OffsetX = v;
                this.Helper.WriteConfig(this.Config);
            },
            () => "Offset X",
            () => "Nudge left/right if alignment is off.",
            min: -64, max: 64, interval: 1
        );

        gmcm.AddNumberOption(
            this.ModManifest,
            () => (int)this.Config.OffsetY,
            v =>
            {
                this.Config.OffsetY = v;
                this.Helper.WriteConfig(this.Config);
            },
            () => "Offset Y",
            () => "Nudge up/down if alignment is off.",
            min: -64, max: 64, interval: 1
        );
    }
}

// Minimal GMCM subset (includes AddTextOption)
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

    void AddKeybindList(
        IManifest mod,
        Func<KeybindList> getValue,
        Action<KeybindList> setValue,
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
}
