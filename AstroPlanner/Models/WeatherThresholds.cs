namespace AstroPlanner.Models;

/// <summary>
/// User-configurable thresholds for observing conditions.
/// Exceeding any threshold marks an hour as failing and affects the detail table highlighting.
/// The 0–100 strip score uses absolute goodness independent of these values.
/// </summary>
public class WeatherThresholds
{
    /// <summary>Maximum acceptable cloud cover percentage (0–100). Default: 20.</summary>
    public int MaxCloudCoverPercent { get; set; } = 20;

    /// <summary>Maximum acceptable wind speed in mph. Default: 20.</summary>
    public int MaxWindSpeedMph { get; set; } = 20;

    /// <summary>Minimum acceptable wind chill in °F. Default: 30.</summary>
    public int MinWindChillF { get; set; } = 30;

    /// <summary>Maximum acceptable relative humidity percentage. Default: 85.</summary>
    public int MaxHumidityPercent { get; set; } = 85;

    /// <summary>Maximum acceptable precipitation probability percentage. Default: 10.</summary>
    public int MaxPrecipProbabilityPercent { get; set; } = 10;

    /// <summary>Minimum acceptable visibility in miles. Default: 5.</summary>
    public int MinVisibilityMiles { get; set; } = 5;

    /// <summary>
    /// Maximum acceptable 7timer seeing value (1 = best &lt;0.5″, 8 = worst &gt;2.5″). Default: 3.
    /// </summary>
    public int MaxSeeing { get; set; } = 3;

    /// <summary>
    /// Minimum acceptable 7timer transparency value (1 = worst, 8 = best). Default: 4.
    /// </summary>
    public int MinTransparency { get; set; } = 4;

    /// <summary>Maximum acceptable moon illumination percentage. Default: 50.</summary>
    public int MaxMoonIlluminationPercent { get; set; } = 50;
}
