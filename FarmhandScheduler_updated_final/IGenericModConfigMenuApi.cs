using System;
using StardewModdingAPI;

#nullable enable

namespace FarmhandScheduler;

/// <summary>
/// GMCM API interface. The method signatures must match GMCM exactly, or SMAPI can't proxy it.
/// This is a minimal subset of the official interface.
/// </summary>
public interface IGenericModConfigMenuApi
{
    void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);

    void AddSectionTitle(IManifest mod, Func<string> text, Func<string>? tooltip = null);
    void AddParagraph(IManifest mod, Func<string> text);

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

    void AddKeybind(
        IManifest mod,
        Func<SButton> getValue,
        Action<SButton> setValue,
        Func<string> name,
        Func<string>? tooltip = null,
        string? fieldId = null
    );
}
