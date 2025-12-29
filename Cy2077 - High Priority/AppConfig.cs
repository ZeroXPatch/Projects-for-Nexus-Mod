using System;
using System.IO;
using System.Text.Json;

namespace CyberpunkPriorityOnce
{
    public enum Mode
    {
        Disabled = 0,
        Manual = 1,
        AutoFocus = 2
    }

    public enum PriorityChoice
    {
        Normal,
        AboveNormal,
        High
    }

    public sealed class AppConfig
    {
        // Target identification
        public string ProcessName { get; set; } = "Cyberpunk2077"; // without .exe

        // Optional: remember a specific EXE path
        public bool RememberExePath { get; set; } = false;
        public string ExePath { get; set; } = ""; // full path to .exe (optional)

        // Modes
        public Mode Mode { get; set; } = Mode.AutoFocus;

        // AutoFocus mode
        public PriorityChoice FocusedPriority { get; set; } = PriorityChoice.High;
        public PriorityChoice UnfocusedPriority { get; set; } = PriorityChoice.Normal;

        // Manual mode
        public PriorityChoice ManualPriority { get; set; } = PriorityChoice.High;

        // Polling
        public int PollMs { get; set; } = 250;

        // UX
        public bool MinimizeToTray { get; set; } = true;
        public bool StartMinimized { get; set; } = false;

        // New: auto-launch game
        public bool AutoLaunchEnabled { get; set; } = false;

        /// <summary>
        /// Command string to launch game, e.g.:
        /// "C:\Games\Cyberpunk 2077\bin\x64\Cyberpunk2077.exe" -arg1 -arg2
        /// </summary>
        public string LaunchCommand { get; set; } = "";

        // New: close game when tool exits
        public bool CloseGameOnExit { get; set; } = false;

        public static string GetConfigPath()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "CyberpunkPriorityOnce"
            );
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "config.json");
        }

        public static AppConfig Load()
        {
            try
            {
                string path = GetConfigPath();
                if (!File.Exists(path))
                    return new AppConfig();

                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
            catch
            {
                return new AppConfig();
            }
        }

        public void Save()
        {
            string path = GetConfigPath();
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(path, JsonSerializer.Serialize(this, options));
        }
    }
}
