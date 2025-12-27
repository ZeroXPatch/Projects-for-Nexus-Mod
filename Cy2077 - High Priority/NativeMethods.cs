using System;
using System.Runtime.InteropServices;

namespace CyberpunkPriorityOnce
{
    internal static class NativeMethods
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        /// <summary>
        /// Returns true if the given process ID owns the current foreground window.
        /// </summary>
        public static bool IsProcessInForeground(int pid)
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
                return false;

            _ = GetWindowThreadProcessId(hwnd, out uint windowPid);
            return windowPid == (uint)pid;
        }
    }
}
