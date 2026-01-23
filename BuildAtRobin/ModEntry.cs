using System;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;

namespace BuildAtRobin
{
    internal class ModEntry : Mod
    {
        public static IMonitor ModMonitor = null!;
        public static IModHelper ModHelper = null!;

        public override void Entry(IModHelper helper)
        {
            ModMonitor = this.Monitor;
            ModHelper = helper;

            try
            {
                var harmony = new Harmony(this.ModManifest.UniqueID);
                harmony.PatchAll(Assembly.GetExecutingAssembly());

                this.Monitor.Log("Build at Robin mod loaded successfully!", LogLevel.Info);
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Error loading Build at Robin mod: {ex}", LogLevel.Error);
            }
        }
    }
}