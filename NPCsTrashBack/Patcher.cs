using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using System;

namespace NPCsTrashBack
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

        // We capture the NPC (__instance) and the Item (o) argument
        public static void Postfix_ReceiveGift(NPC __instance, Item o)
        {
            try
            {
                ModEntry.Instance.OnGiftGiven(__instance, o);
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log($"Error in NPCsTrashBack patch: {ex}", LogLevel.Error);
            }
        }
    }
}