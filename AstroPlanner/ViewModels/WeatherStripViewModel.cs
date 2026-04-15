using CommunityToolkit.Mvvm.ComponentModel;
using AstroPlanner.Models;
using AstroPlanner.Services;

namespace AstroPlanner.ViewModels;

public partial class WeatherStripViewModel : ViewModelBase
{
    private readonly WeatherService _weatherService;

    [ObservableProperty] private IReadOnlyList<WeatherHour> _hours = [];
    [ObservableProperty] private DateTime _darkStart;
    [ObservableProperty] private DateTime _darkEnd;
    [ObservableProperty] private TimeZoneInfo _timeZone = TimeZoneInfo.Local;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _hasData;
    [ObservableProperty] private string _statusMessage = "";

    public WeatherStripViewModel(WeatherService weatherService)
    {
        _weatherService = weatherService;
    }

    public async Task LoadAsync(ObservationLocation location, DateOnly localDate, WeatherThresholds thresholds)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        if (localDate < today)
        {
            Hours = [];
            HasData = false;
            StatusMessage = "Historical weather data is not available.";
            return;
        }

        if (localDate > today.AddDays(16))
        {
            Hours = [];
            HasData = false;
            StatusMessage = "No forecast available beyond 16 days.";
            return;
        }

        IsLoading     = true;
        HasData       = false;
        StatusMessage = "Loading weather…";

        try
        {
            // Compute darkness window so the strip knows its X extents
            var site = location.ToSite();
            var (darkStart, darkEnd) = AstronomyService.GetAstronomicalDarkness(localDate, site);
            DarkStart = darkStart;
            DarkEnd   = darkEnd;
            TimeZone  = location.GetTimeZone();

            var hours = await _weatherService.GetNightWeatherAsync(location, localDate, thresholds);

            if (hours is { Count: > 0 })
            {
                Hours         = hours;
                HasData       = true;
                StatusMessage = "";
            }
            else
            {
                Hours         = [];
                HasData       = false;
                StatusMessage = "Weather data unavailable — check your network connection.";
            }
        }
        catch (Exception ex)
        {
            Hours         = [];
            HasData       = false;
            StatusMessage = "Failed to load weather.";
            Console.Error.WriteLine($"[WeatherStrip] {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }
}
