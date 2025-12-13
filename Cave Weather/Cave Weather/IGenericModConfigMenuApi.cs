using System;
using StardewModdingAPI;

namespace CaveWeather
{
    public interface IGenericModConfigMenuApi
    {
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);

        void AddSectionTitle(IManifest mod, Func<string> text, Func<string> tooltip = null);

        void AddNumberOption(
            IManifest mod,
            Func<int> getValue,
            Action<int> setValue,
            Func<string> name,
            Func<string> tooltip = null,
            int? min = null,
            int? max = null,
            int? interval = null,
            Func<int, string> formatValue = null,
            string fieldId = null
        );

        void AddBoolOption(
            IManifest mod,
            Func<bool> getValue,
            Action<bool> setValue,
            Func<string> name,
            Func<string> tooltip = null,
            string fieldId = null
        );
    }
}
