using CyberpunkPriorityTray;
using System;
using System.Windows.Forms;

namespace CyberpunkPriorityOnce
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            if (!OperatingSystem.IsWindows())
            {
                MessageBox.Show("This app only works on Windows.", "Cyberpunk Priority Tray");
                return;
            }

            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}
