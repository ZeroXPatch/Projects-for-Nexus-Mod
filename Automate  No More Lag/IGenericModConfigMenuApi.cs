using System;

namespace ZeroXPatch
{
    /// <summary>The API provided by Generic Mod Config Menu.</summary>
    public interface IGenericModConfigMenuApi
    {
        void Register(StardewModdingAPI.IManifest mod, Action reset, Action save, bool titleScreenOnly = false);
        void AddSectionTitle(StardewModdingAPI.IManifest mod, Func<string> text, Func<string> tooltip = null);
        void AddBoolOption(StardewModdingAPI.IManifest mod, Func<bool> getValue, Action<bool> setValue, Func<string> name, Func<string> tooltip = null, string fieldId = null);
        void AddNumberOption(StardewModdingAPI.IManifest mod, Func<int> getValue, Action<int> setValue, Func<string> name, Func<string> tooltip = null, int? min = null, int? max = null, int? interval = null, Func<int, string> formatValue = null, string fieldId = null);
    }
}