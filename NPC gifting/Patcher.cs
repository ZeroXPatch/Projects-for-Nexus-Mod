using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using System;

namespace GiftBack
{
    public static class Patcher
    {
        public static void Apply(Harmony harmony, IMonitor monitor)
        {
            try
            {
                harmony.Patch(
                    original: AccessTools.Method(typeof(NPC), nameof(NPC.receiveGift)),
                    postfix: new HarmonyMethod(typeof(Patcher), nameof(Postfix_ReceiveGift))
                );
            }
            catch (Exception ex)
            {
                monitor.Log($"Failed to patch NPC.receiveGift: {ex}", LogLevel.Error);
            }
        }

        public static void Postfix_ReceiveGift(NPC __instance)
        {
            try
            {
                ModEntry.Instance.OnGiftGiven(__instance);
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log($"Error in GiftBack patch: {ex}", LogLevel.Error);
            }
        }
    }
}