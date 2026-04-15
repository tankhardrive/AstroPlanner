using System.Globalization;
using System.Text.Json;
using AstroPlanner.Models;

namespace AstroPlanner.Services;

/// <summary>
/// Fetches and merges weather data from Open-Meteo (hourly, 16-day) and 7timer ASTRO (3-hour, ~7-day).
/// Moon and planet positions are computed from AstronomyService.
/// All API data is cached in memory for the session; cache is invalidated when the location changes.
/// </summary>
public class WeatherService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    // ── Per-location raw cache ────────────────────────────────────────────────
    private string? _cachedLocationKey;
    private Dictionary<DateTime, OpenMeteoHourly>? _openMeteoByHour;
    private SevenTimerData? _sevenTimerData;

    // NOTE: No night-level cache — hours include scored values that depend on user thresholds.
    //       Raw API data is cached above (expensive); hour assembly is cheap math.

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the list of WeatherHour entries covering astronomical darkness for the given night,
    /// or null if the date is out of forecast range or data cannot be fetched.
    /// Scores reflect the supplied thresholds: cloud or precip over threshold → score 0.
    /// </summary>
    public async Task<List<WeatherHour>?> GetNightWeatherAsync(
        ObservationLocation location, DateOnly localDate, WeatherThresholds thresholds)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (localDate < today || localDate > today.AddDays(16))
            return null;

        string locationKey = $"{location.LatitudeDegrees:F3},{location.LongitudeDegrees:F3}";

        // Invalidate raw API cache if location changed
        if (locationKey != _cachedLocationKey)
        {
            _openMeteoByHour = null;
            _sevenTimerData  = null;
            _cachedLocationKey = locationKey;
        }

        // Fetch raw data (once per location per session)
        if (_openMeteoByHour == null)
            _openMeteoByHour = await FetchOpenMeteoAsync(location.LatitudeDegrees, location.LongitudeDegrees);

        if (_sevenTimerData == null)
            _sevenTimerData = await FetchSevenTimerAsync(location.LatitudeDegrees, location.LongitudeDegrees);

        if (_openMeteoByHour == null)
            return null; // fetch failed

        // Build per-hour list (always recomputed so scores stay in sync with thresholds)
        var site = location.ToSite();
        var (darkStart, darkEnd) = AstronomyService.GetAstronomicalDarkness(localDate, site);

        var firstHour = new DateTime(darkStart.Year, darkStart.Month, darkStart.Day, darkStart.Hour, 0, 0, DateTimeKind.Utc);
        var lastHour  = darkEnd.Minute > 0
            ? new DateTime(darkEnd.Year, darkEnd.Month, darkEnd.Day, darkEnd.Hour, 0, 0, DateTimeKind.Utc).AddHours(1)
            : new DateTime(darkEnd.Year, darkEnd.Month, darkEnd.Day, darkEnd.Hour, 0, 0, DateTimeKind.Utc);

        var hours = new List<WeatherHour>();
        for (var t = firstHour; t <= lastHour; t = t.AddHours(1))
            hours.Add(BuildHour(t, location, thresholds));

        return hours;
    }

    // ── Hour assembly ─────────────────────────────────────────────────────────

    private WeatherHour BuildHour(DateTime utcTime, ObservationLocation location, WeatherThresholds thresholds)
    {
        _openMeteoByHour!.TryGetValue(utcTime, out var om);
        var (seeing, transparency) = _sevenTimerData?.GetAt(utcTime) ?? (null, null);

        var (moonRa, moonDec, moonIllum) = AstronomyService.GetMoonPosition(utcTime);
        var (moonAlt, _) = AstronomyService.EquatorialToHorizontal(
            moonRa, moonDec, utcTime, location.LatitudeDegrees, location.LongitudeDegrees);

        var visiblePlanets = new List<string>();
        foreach (var body in PlanetBodies)
        {
            var (ra, dec) = AstronomyService.GetPlanetPosition(body, utcTime);
            var (alt, _) = AstronomyService.EquatorialToHorizontal(
                ra, dec, utcTime, location.LatitudeDegrees, location.LongitudeDegrees);
            if (alt > 5)
                visiblePlanets.Add(PlanetAbbreviation(body));
        }

        var wh = new WeatherHour
        {
            UtcTime                  = utcTime,
            CloudCoverPercent        = om?.CloudCoverPercent,
            WindSpeedMph             = om?.WindSpeedMph,
            WindChillF               = om?.ApparentTemperatureF,
            HumidityPercent          = om?.HumidityPercent,
            PrecipProbabilityPercent = om?.PrecipProbabilityPercent,
            VisibilityMiles          = om?.VisibilityMetres != null ? om.VisibilityMetres / 1609.344 : null,
            Seeing                   = seeing,
            Transparency             = transparency,
            MoonAltitudeDeg          = moonAlt,
            MoonIlluminationPercent  = moonIllum,
            VisiblePlanetNames       = visiblePlanets,
        };
        wh.Score = ComputeScore(wh, thresholds);
        return wh;
    }

    // ── Score ─────────────────────────────────────────────────────────────────
    //
    // Weights:  cloud 35%  |  seeing 20%  |  transparency 15%  |  humidity 10%
    //           precip 10% |  wind 5%     |  visibility 5%
    //
    // Hard-zero veto: cloud > MaxCloudCoverPercent  OR  precip > MaxPrecipProbabilityPercent
    // → score = 0 regardless of all other factors.

    private static int ComputeScore(WeatherHour h, WeatherThresholds t)
    {
        // Hard-zero veto: cloud or rain over threshold ruins the night
        if (h.CloudCoverPercent.HasValue && h.CloudCoverPercent.Value > t.MaxCloudCoverPercent)
            return 0;
        if (h.PrecipProbabilityPercent.HasValue && h.PrecipProbabilityPercent.Value > t.MaxPrecipProbabilityPercent)
            return 0;

        var factors = new List<(double goodness, double weight)>();

        if (h.CloudCoverPercent.HasValue)
            factors.Add((1 - Math.Clamp(h.CloudCoverPercent.Value / 100.0, 0, 1), 0.35));
        if (h.Seeing.HasValue)
            factors.Add((Math.Clamp(1 - (h.Seeing.Value - 1) / 7.0, 0, 1), 0.20));
        if (h.Transparency.HasValue)
            factors.Add((Math.Clamp((h.Transparency.Value - 1) / 7.0, 0, 1), 0.15));
        if (h.HumidityPercent.HasValue)
            factors.Add((1 - Math.Clamp(h.HumidityPercent.Value / 100.0, 0, 1), 0.10));
        if (h.PrecipProbabilityPercent.HasValue)
            factors.Add((1 - Math.Clamp(h.PrecipProbabilityPercent.Value / 100.0, 0, 1), 0.10));
        if (h.WindSpeedMph.HasValue)
            factors.Add((Math.Max(0, 1 - h.WindSpeedMph.Value / 40.0), 0.05));
        if (h.VisibilityMiles.HasValue)
            factors.Add((Math.Min(1, h.VisibilityMiles.Value / 10.0), 0.05));

        if (factors.Count == 0) return 50;

        double totalWeight   = factors.Sum(f => f.weight);
        double weightedScore = factors.Sum(f => f.goodness * f.weight);
        return (int)Math.Round(weightedScore / totalWeight * 100.0);
    }

    // ── Open-Meteo ────────────────────────────────────────────────────────────

    private static async Task<Dictionary<DateTime, OpenMeteoHourly>?> FetchOpenMeteoAsync(double lat, double lon)
    {
        var ic = CultureInfo.InvariantCulture;
        var url = "https://api.open-meteo.com/v1/forecast" +
                  $"?latitude={lat.ToString("F4", ic)}" +
                  $"&longitude={lon.ToString("F4", ic)}" +
                  "&hourly=cloud_cover,wind_speed_10m,apparent_temperature,relative_humidity_2m,precipitation_probability,visibility" +
                  "&wind_speed_unit=mph&temperature_unit=fahrenheit" +
                  "&forecast_days=16&timezone=UTC";
        try
        {
            var json = await Http.GetStringAsync(url);
            return ParseOpenMeteo(json);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WeatherService] Open-Meteo fetch failed: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static Dictionary<DateTime, OpenMeteoHourly> ParseOpenMeteo(string json)
    {
        using var doc    = JsonDocument.Parse(json);
        var hourly       = doc.RootElement.GetProperty("hourly");
        var ic           = CultureInfo.InvariantCulture;

        var times = hourly.GetProperty("time").EnumerateArray()
            .Select(t => DateTime.SpecifyKind(
                DateTime.ParseExact(t.GetString()!, "yyyy-MM-ddTHH:mm", ic),
                DateTimeKind.Utc))
            .ToArray();

        double?[] Get(string name)
        {
            if (!hourly.TryGetProperty(name, out var arr))
                return new double?[times.Length];
            return arr.EnumerateArray()
                .Select(e => e.ValueKind == JsonValueKind.Null ? (double?)null : e.GetDouble())
                .ToArray();
        }

        var cloud      = Get("cloud_cover");
        var wind       = Get("wind_speed_10m");
        var apparent   = Get("apparent_temperature");
        var humidity   = Get("relative_humidity_2m");
        var precip     = Get("precipitation_probability");
        var visibility = Get("visibility");

        var dict = new Dictionary<DateTime, OpenMeteoHourly>(times.Length);
        for (int i = 0; i < times.Length; i++)
        {
            dict[times[i]] = new OpenMeteoHourly(
                times[i], cloud[i], wind[i], apparent[i], humidity[i], precip[i], visibility[i]);
        }
        return dict;
    }

    // ── 7timer ────────────────────────────────────────────────────────────────

    private static async Task<SevenTimerData?> FetchSevenTimerAsync(double lat, double lon)
    {
        var ic = CultureInfo.InvariantCulture;
        var url = $"http://www.7timer.info/bin/api.pl" +
                  $"?lon={lon.ToString("F4", ic)}&lat={lat.ToString("F4", ic)}&product=astro&output=json";
        try
        {
            var json = await Http.GetStringAsync(url);
            return ParseSevenTimer(json);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WeatherService] 7timer fetch failed: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static SevenTimerData ParseSevenTimer(string json)
    {
        using var doc  = JsonDocument.Parse(json);
        var initStr    = doc.RootElement.GetProperty("init").GetString()!;
        var ic         = CultureInfo.InvariantCulture;
        var initTime   = new DateTime(
            int.Parse(initStr[..4], ic), int.Parse(initStr[4..6], ic), int.Parse(initStr[6..8], ic),
            int.Parse(initStr[8..10], ic), 0, 0, DateTimeKind.Utc);

        var entries = doc.RootElement.GetProperty("dataseries").EnumerateArray()
            .Select(e => new SevenTimerEntry(
                e.GetProperty("timepoint").GetInt32(),
                e.TryGetProperty("seeing",       out var s) ? s.GetInt32() : null,
                e.TryGetProperty("transparency", out var t) ? t.GetInt32() : null))
            .ToList();

        return new SevenTimerData(initTime, entries);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static readonly SolarSystemBodyType[] PlanetBodies =
    [
        SolarSystemBodyType.Mercury, SolarSystemBodyType.Venus,
        SolarSystemBodyType.Mars,    SolarSystemBodyType.Jupiter,
        SolarSystemBodyType.Saturn,  SolarSystemBodyType.Uranus,
        SolarSystemBodyType.Neptune,
    ];

    private static string PlanetAbbreviation(SolarSystemBodyType body) => body switch
    {
        SolarSystemBodyType.Mercury => "Mer",
        SolarSystemBodyType.Venus   => "Ven",
        SolarSystemBodyType.Mars    => "Mar",
        SolarSystemBodyType.Jupiter => "Jup",
        SolarSystemBodyType.Saturn  => "Sat",
        SolarSystemBodyType.Uranus  => "Ura",
        SolarSystemBodyType.Neptune => "Nep",
        _                           => body.ToString(),
    };
}

// ── Internal DTOs (internal to this assembly; not part of the public surface) ──

record OpenMeteoHourly(
    DateTime UtcTime,
    double?  CloudCoverPercent,
    double?  WindSpeedMph,
    double?  ApparentTemperatureF,
    double?  HumidityPercent,
    double?  PrecipProbabilityPercent,
    double?  VisibilityMetres);

class SevenTimerData
{
    private readonly DateTime _initTime;
    private readonly Dictionary<int, (int? Seeing, int? Transparency)> _byTimepoint;

    public SevenTimerData(DateTime initTime, List<SevenTimerEntry> entries)
    {
        _initTime    = initTime;
        _byTimepoint = entries.ToDictionary(e => e.Timepoint, e => (e.Seeing, e.Transparency));
    }

    public (int? Seeing, int? Transparency) GetAt(DateTime utcTime)
    {
        double hours = (utcTime - _initTime).TotalHours;
        if (hours < 0 || hours > 210) return (null, null);
        int tp = (int)(Math.Floor(hours / 3) * 3);
        return _byTimepoint.TryGetValue(tp, out var v) ? v : (null, null);
    }
}

record SevenTimerEntry(int Timepoint, int? Seeing, int? Transparency);
