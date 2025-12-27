using System;
using System.Diagnostics;

namespace CyberpunkPriorityTray
{
    public enum PriorityChoice
    {
        Normal,
        AboveNormal,
        High
    }

    internal static class PriorityManager
    {
        public static bool TryGetProcess(string processName, out Process? process)
        {
            process = null;
            if (string.IsNullOrWhiteSpace(processName))
                return false;

            var list = Process.GetProcessesByName(processName.Trim());
            if (list.Length <= 0)
                return false;

            process = list[0];
            return true;
        }

        public static bool TrySetPriority(Process proc, PriorityChoice choice, out string? error)
        {
            error = null;

            try
            {
                proc.Refresh();
                proc.PriorityClass = ToPriorityClass(choice);
                return true;
            }
            catch (Exception ex)
            {
                error = $"{ex.GetType().Name}: {ex.Message}";
                return false;
            }
        }

        public static ProcessPriorityClass ToPriorityClass(PriorityChoice choice) =>
            choice switch
            {
                PriorityChoice.Normal => ProcessPriorityClass.Normal,
                PriorityChoice.AboveNormal => ProcessPriorityClass.AboveNormal,
                PriorityChoice.High => ProcessPriorityClass.High,
                _ => ProcessPriorityClass.Normal
            };
    }
}
