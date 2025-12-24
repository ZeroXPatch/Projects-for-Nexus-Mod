using System;
using StardewModdingAPI;

namespace OopsAllGeorge;

// Minimal GMCM API (works with spacechase0's Generic Mod Config Menu)
public interface IGenericModConfigMenuApi
{
    void Register(
        IManifest mod,
        Action reset,
        Action save,
        bool titleScreenOnly = false
    );

    void AddSectionTitle(
        IManifest mod,
        Func<string> text,
        Func<string>? tooltip = null
    );

    void AddParagraph(
        IManifest mod,
        Func<string> text
    );

    void AddBoolOption(
        IManifest mod,
        Func<bool> getValue,
        Action<bool> setValue,
        Func<string> name,
        Func<string>? tooltip = null
    );

    void AddNumberOption(
        IManifest mod,
        Func<float> getValue,
        Action<float> setValue,
        Func<string> name,
        Func<string>? tooltip = null,
        float min = 0,
        float max = 100,
        float interval = 1
    );

    void AddTextOption(
        IManifest mod,
        Func<string> getValue,
        Action<string> setValue,
        Func<string> name,
        Func<string>? tooltip = null
    );
}
