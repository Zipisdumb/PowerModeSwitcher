using System.Text.Json;

namespace PowerModeSwitcher;

public class ModeProfile
{
    public string Name { get; set; } = "Casual";
    public string PowerPlanGuid { get; set; } = "";
    public int AfterburnerProfile { get; set; } = 0; // 0 = Disabled, 1-5 = Profile 1-5
    public int HotkeyVirtualKey { get; set; } = 0;   // e.g., 0x31 for '1' (Ctrl+Alt+1)
}

public class AppConfig
{
    public string AfterburnerPath { get; set; } = @"C:\Program Files (x86)\MSI Afterburner\MSIAfterburner.exe";
    public List<ModeProfile> Modes { get; set; } = new();

    private static string ConfigPath => Path.Combine(AppContext.BaseDirectory, "config.json");

    public static AppConfig Load()
    {
        if (File.Exists(ConfigPath))
        {
            try
            {
                string json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json);
                if (cfg != null && cfg.Modes.Count > 0) return cfg;
            }
            catch { }
        }

        // Default starter modes on first run
        var defaultCfg = new AppConfig
        {
            Modes = new List<ModeProfile>
            {
                new() { Name = "AFK / Macro", PowerPlanGuid = "", AfterburnerProfile = 2, HotkeyVirtualKey = 0x31 }, // Ctrl+Alt+1
                new() { Name = "Casual",      PowerPlanGuid = "", AfterburnerProfile = 1, HotkeyVirtualKey = 0x32 }, // Ctrl+Alt+2
                new() { Name = "Performance", PowerPlanGuid = "", AfterburnerProfile = 3, HotkeyVirtualKey = 0x33 }  // Ctrl+Alt+3
            }
        };
        defaultCfg.Save();
        return defaultCfg;
    }

    public void Save()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, options));
    }
}