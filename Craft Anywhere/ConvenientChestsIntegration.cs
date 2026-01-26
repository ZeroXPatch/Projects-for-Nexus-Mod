using System;
using StardewModdingAPI;

namespace CraftAnywhere
{
    /// <summary>
    /// Integration for Convenient Chests mod to respect its craft radius setting.
    /// </summary>
    internal class ConvenientChestsIntegration
    {
        private readonly IModHelper Helper;
        private readonly IMonitor Monitor;
        private bool IsLoaded = false;
        
        private Type? EntryType;

        public ConvenientChestsIntegration(IModHelper helper, IMonitor monitor)
        {
            Helper = helper;
            Monitor = monitor;
            
            // Check if Convenient Chests mod is loaded
            var modInfo = helper.ModRegistry.Get("aEnigma.ConvenientChests");
            if (modInfo == null || !modInfo.Manifest.Version.IsNewerThan("1.5"))
            {
                return;
            }

            try
            {
                EntryType = Type.GetType("ConvenientChests.ModEntry, ConvenientChests");
                if (EntryType == null)
                {
                    Monitor.Log("Convenient Chests ModEntry not found. Integration disabled.", LogLevel.Warn);
                    return;
                }

                IsLoaded = true;
                Monitor.Log("Convenient Chests integration enabled.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Monitor.Log($"Failed to load Convenient Chests integration: {ex.Message}", LogLevel.Warn);
            }
        }

        /// <summary>
        /// Gets the craft radius from Convenient Chests config.
        /// Returns 0 if the feature is disabled or mod is not loaded.
        /// </summary>
        public int GetCraftRadius()
        {
            if (!IsLoaded || EntryType == null)
                return 0;

            try
            {
                // Get the config object from ModEntry.Config static property
                object? config = Helper.Reflection.GetProperty<object>(EntryType, "Config", false)?.GetValue();
                if (config == null)
                    return 0;

                // Check if CraftFromChests is enabled
                bool craftFromChests = Helper.Reflection.GetProperty<bool>(config, "CraftFromChests", false)?.GetValue() ?? false;
                if (!craftFromChests)
                    return 0;

                // Get the craft radius
                int radius = Helper.Reflection.GetProperty<int>(config, "CraftRadius", false)?.GetValue() ?? 0;
                return radius;
            }
            catch (Exception ex)
            {
                Monitor.LogOnce($"Failed to get Convenient Chests craft radius: {ex.Message}", LogLevel.Trace);
                return 0;
            }
        }

        /// <summary>
        /// Checks if Convenient Chests is handling crafting (radius > 0).
        /// If true, Craft Anywhere should defer to Convenient Chests.
        /// </summary>
        public bool IsHandlingCrafting()
        {
            return GetCraftRadius() > 0;
        }

        public bool IsConvenientChestsLoaded => IsLoaded;
    }
}