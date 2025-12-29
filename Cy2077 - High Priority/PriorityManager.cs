using System;
using System.Diagnostics;
using System.IO;

namespace CyberpunkPriorityOnce
{
    internal static class PriorityManager
    {
        public static ProcessPriorityClass ToPriorityClass(PriorityChoice choice) =>
            choice switch
            {
                PriorityChoice.Normal => ProcessPriorityClass.Normal,
                PriorityChoice.AboveNormal => ProcessPriorityClass.AboveNormal,
                PriorityChoice.High => ProcessPriorityClass.High,
                _ => ProcessPriorityClass.Normal
            };

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

        public static bool TryGetProcessByName(string processName, out Process? process)
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

        /// <summary>
        /// Try find a process by EXE path (best-effort). If we can't read MainModule due to permissions,
        /// we fall back to process-name matching.
        /// </summary>
        public static bool TryGetProcessByExePath(string exePath, out Process? process)
        {
            process = null;

            if (string.IsNullOrWhiteSpace(exePath))
                return false;

            exePath = exePath.Trim();
            if (!File.Exists(exePath))
                return false;

            string processName = Path.GetFileNameWithoutExtension(exePath);
            var list = Process.GetProcessesByName(processName);

            if (list.Length <= 0)
                return false;

            // Try to match exact path (may throw without elevation; catch and ignore)
            foreach (var p in list)
            {
                try
                {
                    string? modulePath = p.MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(modulePath) &&
                        string.Equals(Path.GetFullPath(modulePath), Path.GetFullPath(exePath), StringComparison.OrdinalIgnoreCase))
                    {
                        process = p;
                        return true;
                    }
                }
                catch
                {
                    // ignore and continue
                }
            }

            // Fallback: return first by name
            process = list[0];
            return true;
        }

        public static void KillProcessTreeSafe(Process proc)
        {
            try
            {
                if (proc.HasExited)
                    return;

                // .NET supports killing entire tree on Windows
                proc.Kill(entireProcessTree: true);
            }
            catch
            {
                // ignore
            }
        }
    }
}
