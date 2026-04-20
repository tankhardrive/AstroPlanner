using AstroPlanner.Models;

namespace AstroPlanner.Services;

public class AnnotationService
{
    private readonly SettingsService _settingsService;
    private AppSettings _settings;

    public event Action? AnnotationsChanged;

    public AnnotationService(SettingsService settingsService, AppSettings settings)
    {
        _settingsService = settingsService;
        _settings = settings;
    }

    public void UpdateSettings(AppSettings settings) => _settings = settings;

    public bool IsFavorite(string key) =>
        _settings.Annotations.TryGetValue(key, out var a) && a.IsFavorite;

    public DateOnly? GetImagedOn(string key) =>
        _settings.Annotations.TryGetValue(key, out var a) ? a.ImagedOn : null;

    public void SetFavorite(string key, bool value)
    {
        GetOrCreate(key).IsFavorite = value;
        Save();
    }

    public void SetImaged(string key, DateOnly? date)
    {
        GetOrCreate(key).ImagedOn = date;
        Save();
    }

    private ObjectAnnotation GetOrCreate(string key)
    {
        if (!_settings.Annotations.TryGetValue(key, out var a))
        {
            a = new ObjectAnnotation();
            _settings.Annotations[key] = a;
        }
        return a;
    }

    private void Save()
    {
        _settingsService.Save(_settings);
        AnnotationsChanged?.Invoke();
    }
}
