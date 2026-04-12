namespace AstroPlanner.Models;

public class ObservationSite
{
    public string Name { get; set; } = "Orlando, FL";

    /// <summary>Decimal degrees, positive = north.</summary>
    public double LatitudeDegrees { get; set; } = 28.5384;

    /// <summary>Decimal degrees, negative = west.</summary>
    public double LongitudeDegrees { get; set; } = -81.3789;

    public double ElevationMeters { get; set; } = 0;

    /// <summary>IANA timezone ID, e.g. "America/New_York".</summary>
    public string TimeZoneId { get; set; } = "America/New_York";

    public TimeZoneInfo GetTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId); }
        catch { return TimeZoneInfo.Local; }
    }
}
