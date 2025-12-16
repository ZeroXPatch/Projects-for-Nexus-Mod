using System;
using System.Reflection;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;

namespace SpaceToFastDialogue;

public class ModEntry : Mod
{
    private ModConfig Config = new();

    private double lastActionTimeMs = -1;
    private bool awaitingConfirm;
    private double confirmStartMs = -1;
    private string? lastDialogueKey;

    // Question/response dialogue detection (some of these are private depending on game version)
    private static readonly FieldInfo? ResponsesField = typeof(DialogueBox).GetField("responses", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? ResponseOptionsField = typeof(DialogueBox).GetField("responseOptions", BindingFlags.Instance | BindingFlags.NonPublic);

    // Dialogue typing (not always public, so reflect)
    private static readonly FieldInfo? IsTypingField = typeof(DialogueBox).GetField("isTyping", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
    private static readonly FieldInfo? CharacterIndexField = typeof(DialogueBox).GetField("characterIndexInDialogue", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
    private static readonly MethodInfo? FinishTypingMethod = typeof(DialogueBox).GetMethod("finishTyping", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    public override void Entry(IModHelper helper)
    {
        this.Config = helper.ReadConfig<ModConfig>();

        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
        helper.Events.Input.ButtonsChanged += this.OnButtonsChanged;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (gmcm is null)
            return;

        gmcm.Register(this.ModManifest, () => this.Config = new ModConfig(), () => this.Helper.WriteConfig(this.Config));

        gmcm.AddBoolOption(
            this.ModManifest,
            () => this.Config.EnableMod,
            value => this.Config.EnableMod = value,
            () => this.Helper.Translation.Get("config.enable"),
            () => this.Helper.Translation.Get("config.enable.tooltip"));

        gmcm.AddKeybindList(
            this.ModManifest,
            () => this.Config.Hotkey,
            value => this.Config.Hotkey = value,
            () => this.Helper.Translation.Get("config.hotkey"),
            () => this.Helper.Translation.Get("config.hotkey.tooltip"));

        gmcm.AddBoolOption(
            this.ModManifest,
            () => this.Config.ConfirmAutoAdvance,
            value => this.Config.ConfirmAutoAdvance = value,
            () => this.Helper.Translation.Get("config.confirm"),
            () => this.Helper.Translation.Get("config.confirm.tooltip"));

        gmcm.AddNumberOption(
            this.ModManifest,
            () => this.Config.DoubleTapMs,
            value => this.Config.DoubleTapMs = value,
            () => this.Helper.Translation.Get("config.doubleTap"),
            () => this.Helper.Translation.Get("config.doubleTap.tooltip"),
            min: 100,
            max: 2000,
            interval: 10);

        gmcm.AddNumberOption(
            this.ModManifest,
            () => this.Config.InputCooldownMs,
            value => this.Config.InputCooldownMs = value,
            () => this.Helper.Translation.Get("config.cooldown"),
            () => this.Helper.Translation.Get("config.cooldown.tooltip"),
            min: 30,
            max: 500,
            interval: 10);

        gmcm.AddBoolOption(
            this.ModManifest,
            () => this.Config.ShowConfirmMessage,
            value => this.Config.ShowConfirmMessage = value,
            () => this.Helper.Translation.Get("config.confirmMessage"),
            () => this.Helper.Translation.Get("config.confirmMessage.tooltip"));
    }

    /// <summary>
    /// This mod now targets EVENT dialogue only.
    /// It does nothing in normal NPC dialogue (non-event).
    /// </summary>
    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady)
        {
            this.ResetState();
            return;
        }

        // EVENT-ONLY: if no event is running, do nothing & clear state
        if (!Game1.eventUp && Game1.CurrentEvent is null)
        {
            this.ResetState();
            return;
        }

        var dialogueBox = Game1.activeClickableMenu as DialogueBox;
        if (dialogueBox is null || this.IsUnsupportedDialogue(dialogueBox))
        {
            // still in an event, but not in a supported DialogueBox right now
            this.ResetState();
            return;
        }

        // Reset confirm if the line changed
        var currentDialogue = this.GetCurrentDialogue(dialogueBox);
        if (!string.Equals(this.lastDialogueKey, currentDialogue, StringComparison.Ordinal))
        {
            this.awaitingConfirm = false;
            this.confirmStartMs = -1;
            this.lastDialogueKey = currentDialogue;
        }

        // Expire confirm timer
        var now = this.GetCurrentTimeMs();
        if (this.awaitingConfirm && this.confirmStartMs >= 0 && now - this.confirmStartMs > this.Config.DoubleTapMs)
        {
            this.awaitingConfirm = false;
            this.confirmStartMs = -1;
        }
    }

    private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
    {
        if (!this.Config.EnableMod || !Context.IsWorldReady)
            return;

        // EVENT-ONLY: ignore normal dialogue
        if (!Game1.eventUp && Game1.CurrentEvent is null)
        {
            this.ResetState();
            return;
        }

        var dialogueBox = Game1.activeClickableMenu as DialogueBox;
        if (dialogueBox is null || this.IsUnsupportedDialogue(dialogueBox))
        {
            this.ResetState();
            return;
        }

        if (!this.Config.Hotkey.JustPressed())
            return;

        // Only suppress while event DialogueBox is open.
        this.Helper.Input.SuppressActiveKeybinds(this.Config.Hotkey);

        var now = this.GetCurrentTimeMs();

        // cooldown (but allow a fast second tap when awaiting confirm)
        var bypassCooldown = this.awaitingConfirm;
        if (!bypassCooldown && this.lastActionTimeMs >= 0 && now - this.lastActionTimeMs < this.Config.InputCooldownMs)
            return;

        // Reset confirm if the line changed
        var currentDialogue = this.GetCurrentDialogue(dialogueBox);
        if (!string.Equals(this.lastDialogueKey, currentDialogue, StringComparison.Ordinal))
        {
            this.awaitingConfirm = false;
            this.confirmStartMs = -1;
            this.lastDialogueKey = currentDialogue;
        }

        // Rule: if typing, reveal only (don't advance on the same press)
        if (this.IsDialogueTyping(dialogueBox))
        {
            this.FinishDialogueTyping(dialogueBox);
            this.awaitingConfirm = false;
            this.confirmStartMs = -1;
            this.lastActionTimeMs = now;
            return;
        }

        // Fully revealed -> advance (optionally with confirm)
        if (this.Config.ConfirmAutoAdvance)
        {
            if (this.awaitingConfirm && this.confirmStartMs >= 0 && now - this.confirmStartMs <= this.Config.DoubleTapMs)
            {
                this.PerformAdvance(dialogueBox);
                this.awaitingConfirm = false;
                this.confirmStartMs = -1;
                this.lastActionTimeMs = now;
            }
            else
            {
                this.awaitingConfirm = true;
                this.confirmStartMs = now;

                if (this.Config.ShowConfirmMessage)
                {
                    var keyName = this.Config.Hotkey.ToString();
                    var message = this.Helper.Translation.Get("confirm.prompt", new { key = keyName }).ToString();
                    Game1.addHUDMessage(new HUDMessage(message, HUDMessage.error_type) { noIcon = true });
                }
            }
        }
        else
        {
            this.PerformAdvance(dialogueBox);
            this.lastActionTimeMs = now;
        }
    }

    private bool IsDialogueTyping(DialogueBox dialogueBox)
    {
        try
        {
            if (IsTypingField?.GetValue(dialogueBox) is bool b)
                return b;

            // fallback: compare current character index to string length (best-effort)
            if (CharacterIndexField?.GetValue(dialogueBox) is int idx)
            {
                var full = dialogueBox.getCurrentString() ?? string.Empty;
                return idx < full.Length;
            }
        }
        catch
        {
            // ignore reflection failures
        }

        return false;
    }

    private void FinishDialogueTyping(DialogueBox dialogueBox)
    {
        try
        {
            if (FinishTypingMethod is not null)
            {
                FinishTypingMethod.Invoke(dialogueBox, Array.Empty<object>());
                return;
            }

            // fallback: force typing off + jump index forward
            if (IsTypingField is not null && IsTypingField.FieldType == typeof(bool))
                IsTypingField.SetValue(dialogueBox, false);

            if (CharacterIndexField is not null && CharacterIndexField.FieldType == typeof(int))
                CharacterIndexField.SetValue(dialogueBox, int.MaxValue);
        }
        catch
        {
            // ignore reflection failures
        }
    }

    /// <summary>
    /// Advances event dialogue via a safe click inside the dialogue box,
    /// which tends to be more hotkey-agnostic than simulating a specific key.
    /// </summary>
    private void PerformAdvance(DialogueBox dialogueBox)
    {
        var clickX = dialogueBox.xPositionOnScreen + dialogueBox.width / 2;
        var clickY = dialogueBox.yPositionOnScreen + dialogueBox.height / 2;
        dialogueBox.receiveLeftClick(clickX, clickY);
    }

    /// <summary>
    /// Skip question/response dialogues (even in events) to avoid breaking choice prompts.
    /// </summary>
    private bool IsUnsupportedDialogue(DialogueBox dialogueBox)
    {
        if (dialogueBox.isQuestion)
            return true;

        var responses = ResponsesField?.GetValue(dialogueBox);
        if (responses is Array responseArray && responseArray.Length > 0)
            return true;

        var responseOptions = ResponseOptionsField?.GetValue(dialogueBox);
        if (responseOptions is System.Collections.ICollection collection && collection.Count > 0)
            return true;

        return false;
    }

    private string GetCurrentDialogue(DialogueBox dialogueBox)
    {
        return dialogueBox.getCurrentString() ?? string.Empty;
    }

    private double GetCurrentTimeMs()
    {
        return Game1.currentGameTime?.TotalGameTime.TotalMilliseconds ?? Environment.TickCount64;
    }

    private void ResetState()
    {
        this.awaitingConfirm = false;
        this.confirmStartMs = -1;
        this.lastDialogueKey = null;
        this.lastActionTimeMs = -1;
    }
}

public class ModConfig
{
    public bool EnableMod { get; set; } = true;

    public KeybindList Hotkey { get; set; } = new(SButton.Space);

    public bool ConfirmAutoAdvance { get; set; } = true;

    public int DoubleTapMs { get; set; } = 350;

    public int InputCooldownMs { get; set; } = 90;

    public bool ShowConfirmMessage { get; set; } = true;
}

public interface IGenericModConfigMenuApi
{
    void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);

    void AddBoolOption(
        IManifest mod,
        Func<bool> getValue,
        Action<bool> setValue,
        Func<string> name,
        Func<string>? tooltip = null,
        string? fieldId = null);

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

    void AddKeybindList(
        IManifest mod,
        Func<KeybindList> getValue,
        Action<KeybindList> setValue,
        Func<string> name,
        Func<string>? tooltip = null,
        string? fieldId = null);

    void AddSectionTitle(IManifest mod, Func<string> text, Func<string>? tooltip = null);
}
