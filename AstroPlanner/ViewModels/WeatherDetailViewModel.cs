using AstroPlanner.Models;

namespace AstroPlanner.ViewModels;

public class WeatherDetailViewModel
{
    public string Title { get; }
    public IReadOnlyList<WeatherHour> Hours { get; }
    public IReadOnlyList<WeatherHourViewModel> HourRows { get; }
    public TimeZoneInfo TimeZone { get; }

    public WeatherDetailViewModel(
        IReadOnlyList<WeatherHour> hours,
        WeatherThresholds thresholds,
        string locationName,
        DateOnly date,
        TimeZoneInfo tz)
    {
        Title    = $"Weather Forecast  —  {locationName}  —  {date:MMMM d, yyyy}";
        Hours    = hours;
        TimeZone = tz;
        HourRows = hours.Select(h => new WeatherHourViewModel(h, thresholds, tz)).ToList();
    }
}
