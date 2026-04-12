using System.Text.Json;
using AstroPlanner.Models;

namespace AstroPlanner.Services;

public class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        IncludeFields = false,
    };

    private readonly string _settingsPath;

    public SettingsService()
    {
        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AstroPlanner");
        Directory.CreateDirectory(configDir);
        _settingsPath = Path.Combine(configDir, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            AppSettings settings;
            if (!File.Exists(_settingsPath))
            {
                settings = new AppSettings();
            }
            else
            {
                var json = File.ReadAllText(_settingsPath);
                settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
            settings.MigrateIfNeeded();
            return settings;
        }
        catch
        {
            var s = new AppSettings();
            s.MigrateIfNeeded();
            return s;
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(_settingsPath, json);
        }
        catch
        {
            // Best-effort save; non-fatal
        }
    }
}
