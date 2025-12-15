using System;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace GenericModConfigMenu
{
    /// <summary>Minimal GMCM API surface needed by this mod.</summary>
    public interface IGenericModConfigMenuApi
    {
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);

        void AddSectionTitle(IManifest mod, Func<string> text, Func<string>? tooltip = null);

        void AddParagraph(IManifest mod, Func<string> text);

        void AddKeybindList(
            IManifest mod,
            Func<KeybindList> getValue,
            Action<KeybindList> setValue,
            Func<string> name,
            Func<string>? tooltip = null,
            string? fieldId = null
        );

        void AddBoolOption(
            IManifest mod,
            Func<bool> getValue,
            Action<bool> setValue,
            Func<string> name,
            Func<string>? tooltip = null,
            string? fieldId = null
        );

        void OpenModMenu(IManifest mod);

        void OpenModMenuAsChildMenu(IManifest mod);
    }
}
