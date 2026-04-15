using Avalonia.Media;
using AstroPlanner.Models;

namespace AstroPlanner.ViewModels;

/// <summary>
/// Wraps a WeatherHour for display in the detail window table and chart.
/// Provides formatted strings, per-cell threshold pass/fail brushes, and color helpers.
/// </summary>
public class WeatherHourViewModel
{
    private readonly WeatherHour _h;
    private readonly WeatherThresholds _t;
    private readonly TimeZoneInfo _tz;

    public WeatherHourViewModel(WeatherHour hour, WeatherThresholds thresholds, TimeZoneInfo tz)
    {
        _h  = hour;
        _t  = thresholds;
        _tz = tz;
    }

    // ── Raw hour (used by WeatherChartControl) ────────────────────────────────
    public WeatherHour Hour => _h;

    // ── Display strings ───────────────────────────────────────────────────────
    public string TimeLabel
    {
        get
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(_h.UtcTime, DateTimeKind.Utc), _tz);
            return local.ToString("h tt");  // "9 PM"
        }
    }

    public string CloudDisplay       => _h.CloudCoverPercent.HasValue   ? $"{_h.CloudCoverPercent.Value:F0}%"  : "—";
    public string WindDisplay        => _h.WindSpeedMph.HasValue         ? $"{_h.WindSpeedMph.Value:F0} mph"   : "—";
    public string WindChillDisplay   => _h.WindChillF.HasValue           ? $"{_h.WindChillF.Value:F0}°F"       : "—";
    public string HumidityDisplay    => _h.HumidityPercent.HasValue      ? $"{_h.HumidityPercent.Value:F0}%"   : "—";
    public string PrecipDisplay      => _h.PrecipProbabilityPercent.HasValue ? $"{_h.PrecipProbabilityPercent.Value:F0}%" : "—";
    public string VisibilityDisplay  => _h.VisibilityMiles.HasValue      ? $"{_h.VisibilityMiles.Value:F1} mi" : "—";

    public string SeeingDisplay => _h.Seeing.HasValue ? _h.Seeing.Value switch
    {
        1 => "1 – Excellent",
        2 => "2 – Good",
        3 => "3 – Good",
        4 => "4 – Average",
        5 => "5 – Average",
        6 => "6 – Poor",
        7 => "7 – Poor",
        8 => "8 – Terrible",
        _ => _h.Seeing.Value.ToString(),
    } : "—";

    public string TransparencyDisplay => _h.Transparency.HasValue ? _h.Transparency.Value switch
    {
        1 => "1 – Terrible",
        2 => "2 – Poor",
        3 => "3 – Poor",
        4 => "4 – Average",
        5 => "5 – Average",
        6 => "6 – Good",
        7 => "7 – Good",
        8 => "8 – Excellent",
        _ => _h.Transparency.Value.ToString(),
    } : "—";

    public string MoonDisplay
    {
        get
        {
            var arrow = _h.MoonIsUp ? "↑" : "↓";
            return $"{_h.MoonIlluminationPercent:F0}% {arrow}";
        }
    }

    public string PlanetsDisplay => _h.VisiblePlanetNames.Count > 0
        ? string.Join(", ", _h.VisiblePlanetNames)
        : "—";

    public string ScoreDisplay => _h.Score.ToString();

    // ── Score / color ─────────────────────────────────────────────────────────
    public int Score => _h.Score;

    public IBrush ScoreColorBrush => new SolidColorBrush(ScoreToColor(_h.Score));

    public static Color ScoreToColor(int score)
    {
        score = Math.Clamp(score, 0, 100);
        if (score <= 50)
        {
            double t = score / 50.0;
            return Color.FromRgb(220, (byte)(50 + 110 * t), 50);
        }
        else
        {
            double t = (score - 50) / 50.0;
            return Color.FromRgb((byte)(220 - 170 * t), (byte)(160 + 40 * t), (byte)(50 + 30 * t));
        }
    }

    // ── Threshold fail brushes (transparent red tint on failing cells) ────────
    private static readonly IBrush FailBrush = new SolidColorBrush(Color.FromArgb(55, 220, 50, 50));
    private static readonly IBrush OkBrush   = Brushes.Transparent;

    public IBrush CloudBackground      => (_h.CloudCoverPercent        ?? 0)  > _t.MaxCloudCoverPercent        ? FailBrush : OkBrush;
    public IBrush WindBackground       => (_h.WindSpeedMph             ?? 0)  > _t.MaxWindSpeedMph             ? FailBrush : OkBrush;
    public IBrush WindChillBackground  => (_h.WindChillF               ?? 999) < _t.MinWindChillF              ? FailBrush : OkBrush;
    public IBrush HumidityBackground   => (_h.HumidityPercent          ?? 0)  > _t.MaxHumidityPercent          ? FailBrush : OkBrush;
    public IBrush PrecipBackground     => (_h.PrecipProbabilityPercent ?? 0)  > _t.MaxPrecipProbabilityPercent ? FailBrush : OkBrush;
    public IBrush VisibilityBackground => (_h.VisibilityMiles          ?? 999) < _t.MinVisibilityMiles         ? FailBrush : OkBrush;
    public IBrush SeeingBackground     => (_h.Seeing                   ?? 0)  > _t.MaxSeeing                   ? FailBrush : OkBrush;
    public IBrush TransparencyBackground => (_h.Transparency           ?? 8)  < _t.MinTransparency             ? FailBrush : OkBrush;
    public IBrush MoonBackground       => _h.MoonIlluminationPercent > _t.MaxMoonIlluminationPercent ? FailBrush : OkBrush;
    public IBrush ScoreBackground      => new SolidColorBrush(Color.FromArgb(40, ScoreToColor(_h.Score).R, ScoreToColor(_h.Score).G, ScoreToColor(_h.Score).B));
}
