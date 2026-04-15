namespace AstroPlanner.Models;

/// <summary>
/// Weather and astronomical conditions for a single hour of the night.
/// Open-Meteo supplies hourly weather data; 7timer ASTRO supplies 3-hour seeing/transparency blocks.
/// Moon and planet data are computed locally from AstronomyService.
/// </summary>
public class WeatherHour
{
    // ── Time ─────────────────────────────────────────────────────────────────
    public DateTime UtcTime { get; set; }

    // ── Open-Meteo (hourly) ───────────────────────────────────────────────────
    public double? CloudCoverPercent { get; set; }
    public double? WindSpeedMph { get; set; }
    /// <summary>Apparent (feels-like) temperature in °F.</summary>
    public double? WindChillF { get; set; }
    public double? HumidityPercent { get; set; }
    public double? PrecipProbabilityPercent { get; set; }
    /// <summary>Converted from metres to miles.</summary>
    public double? VisibilityMiles { get; set; }

    // ── 7timer ASTRO (3-hour blocks, null outside 7-day window) ──────────────
    /// <summary>1 = best (&lt;0.5″), 8 = worst (&gt;2.5″).</summary>
    public int? Seeing { get; set; }
    /// <summary>1 = worst, 8 = best.</summary>
    public int? Transparency { get; set; }

    // ── Computed from AstronomyService ────────────────────────────────────────
    public double MoonAltitudeDeg { get; set; }
    public double MoonIlluminationPercent { get; set; }
    public bool MoonIsUp => MoonAltitudeDeg > 0;
    public List<string> VisiblePlanetNames { get; set; } = [];

    // ── Derived score ─────────────────────────────────────────────────────────
    /// <summary>
    /// 0 (terrible) – 100 (perfect) composite observing score.
    /// Weights: cloud 35%, seeing 20%, transparency 15%, humidity 10%, precip 10%, wind 5%, visibility 5%.
    /// Uses absolute goodness (independent of user thresholds) for a consistent gradient.
    /// </summary>
    public int Score { get; set; }
}
