using System;
using System.IO;
using System.Text.Json;

namespace CyberpunkPriorityTray
{
    public enum Mode
    {
        Disabled = 0,
        Manual = 1,
        AutoFocus = 2
    }

    public sealed class AppConfig
    {
        public string ProcessName { get; set; } = "Cyberpunk2077"; // without .exe
        public Mode Mode { get; set; } = Mode.AutoFocus;

        // AutoFocus mode
        public PriorityChoice FocusedPriority { get; set; } = PriorityChoice.High;
        public PriorityChoice UnfocusedPriority { get; set; } = PriorityChoice.Normal;

        // Manual mode
        public PriorityChoice ManualPriority { get; set; } = PriorityChoice.High;

        public int PollMs { get; set; } = 250;

        // UX
        public bool MinimizeToTray { get; set; } = true;
        public bool StartMinimized { get; set; } = false;

        public static string GetConfigPath()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "CyberpunkPriorityTray"
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
